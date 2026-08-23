"""Build progress-entry (PE-) payloads from the frozen v1 progress log.

The log has two generations and they must be parsed together:

  older:  ## <slice title> - branch `feat/pN-...`      <- slice identity lives HERE
            ### YYYY-MM-DD - <detail>                  <- date lives here
  newer:  ### YYYY-MM-DD - <slice> - <summary>         <- both in the heading

So a per-heading regex is wrong: 59 of 106 dated headings carry no P-token at all.
Resolution walks the section context.

Three date sources, each stamped on the row so no reader mistakes one for another:

  authored          - the "### YYYY-MM-DD" heading itself           (106)
  inferred-from-body- first ISO date inside an undated ## section   ( 14)
  derived-from-git  - merge-commit date, for sections with no date  (  5)
                      anywhere in the log (4x P12, 1x P11-remediation)

Read-only. Emits JSON payloads for review; performs no MCP calls.

    python parse_progress_log.py            # summary + mapping table
    python parse_progress_log.py --json out/pe-payloads.json
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
LOG = ROOT / "docs" / "progress" / "progress-log.md"

H2 = re.compile(r"^##\s+(?!#)(.*)$")
H3_DATED = re.compile(r"^###\s+(\d{4}-\d{2}-\d{2})\s*[-—]*\s*(.*)$")
ISO = re.compile(r"(\d{4}-\d{2}-\d{2})")
P_TOKEN = re.compile(r"\bP(\d{1,2})[a-z0-9-]*\b")

# The roadmap's "Legacy token map" warns that Round-2 Reconcile entries citing P14/P15
# use the DESIGN package's numbering, not the build ladder. Mapping those to SL-014
# (Tarseem, deferred) or SL-015 would fabricate history. Verified live at
# progress-log.md:3297, :3313, :3335.
DESIGN_SCHEME_SECTION = re.compile(r"Round-2\s+Reconcile", re.I)
DESIGN_SCHEME_TOKENS = {"P14", "P15"}

# A token qualified by one of these words is a REFERENCE to another slice, not this
# entry's own slice: "post-P4, before P5" / "No P4 rework" belongs to neither.
# Attributing it anyway is exactly the guessing the plan forbids - leave slice NULL.
# Must not over-fire: "P1 scaffold complete (report before P2)" is legitimately P1,
# and stays so because the scan takes the first UNQUALIFIED token in order.
QUALIFIER = re.compile(r"(?:post|pre|before|after|no|not|vs\.?|versus)[-\s]+$", re.I)

# Sections carrying no date anywhere in the log. Dates are the real merge-commit dates,
# recovered from git - evidence, not fabrication. Reproduce with:
#   git log --date=short --pretty="%h %ad %s" | grep -E "#9[2-6]\)"
GIT_DATES = {
    "P12-PR1": ("2026-07-05", "20a451b"),
    "P12-PR2": ("2026-07-05", "4648b19"),
    "P12-PR3": ("2026-07-05", "6252134"),
    "P12 audit remediation": ("2026-07-05", "b3c1a45"),
    "P11 audit remediation": ("2026-07-05", "6468fb7"),
}

# P1-P12 shipped under PH-1; P13-P15 are the PH-2 remainder; P16-P19 are cross-cutting
# and live under the PH-4 created by DEC-029/SC-001.
def phase_for(slice_num: int) -> str:
    if slice_num <= 12:
        return "PH-1"
    if slice_num <= 15:
        return "PH-2"
    return "PH-4"


def slice_token(text: str, *, allow_design_tokens: bool) -> str | None:
    """First build-ladder P-token in `text`, or None."""
    for match in P_TOKEN.finditer(text):
        token = "P" + match.group(1)
        if not allow_design_tokens and token in DESIGN_SCHEME_TOKENS:
            continue  # design-package numbering, not the ladder
        if QUALIFIER.search(text[:match.start()]):
            continue  # a reference to another slice, not this entry's own
        num = int(match.group(1))
        if 1 <= num <= 19:
            return token
    return None


def parse(lines: list[str]) -> tuple[list[dict], list[dict]]:
    """Return (entries, sections). Entries are dated; sections carry slice context."""
    entries: list[dict] = []
    sections: list[dict] = []
    current: dict | None = None

    for lineno, line in enumerate(lines, 1):
        h2 = H2.match(line)
        if h2:
            current = {
                "heading": h2.group(1).strip(),
                "line": lineno,
                "dated_children": 0,
                "body_date": None,
                "is_design_scheme": bool(DESIGN_SCHEME_SECTION.search(h2.group(1))),
            }
            sections.append(current)
            continue

        h3 = H3_DATED.match(line)
        if h3:
            if current:
                current["dated_children"] += 1
            parent = current["heading"] if current else ""
            design = current["is_design_scheme"] if current else False
            token = (slice_token(h3.group(2), allow_design_tokens=not design)
                     or slice_token(parent, allow_design_tokens=not design))
            entries.append({
                "date": h3.group(1),
                "heading": h3.group(2).strip(),
                "parent": parent,
                "line": lineno,
                "token": token,
                "provenance": "authored",
            })
            continue

        if current and not current["body_date"]:
            found = ISO.search(line)
            if found:
                current["body_date"] = found.group(1)

    # Sections with no dated child contribute one entry each, dated from the body or git.
    for sec in sections:
        if sec["dated_children"]:
            continue
        date, provenance, sha = sec["body_date"], "inferred-from-body", None
        if not date:
            for key, (git_date, git_sha) in GIT_DATES.items():
                if sec["heading"].startswith(key):
                    date, provenance, sha = git_date, "derived-from-git", git_sha
                    break
        if not date:
            sec["unresolved"] = True
            continue
        entry = {
            "date": date,
            "heading": sec["heading"],
            "parent": "",
            "line": sec["line"],
            "token": slice_token(sec["heading"],
                                 allow_design_tokens=not sec["is_design_scheme"]),
            "provenance": provenance,
        }
        if sha:
            entry["sha"] = sha
        entries.append(entry)

    return entries, sections


def build_payloads(entries: list[dict]) -> list[dict]:
    """PE- rows, oldest first so ids ascend with time."""
    ordered = sorted(entries, key=lambda e: (e["date"], e["line"]))
    payloads = []
    for index, entry in enumerate(ordered, 1):
        attrs = {"date_provenance": entry["provenance"], "v1_line": entry["line"]}
        if "sha" in entry:
            attrs["git_sha"] = entry["sha"]
        row = {
            "type": "progress-entry",
            "id": f"PE-{index:03d}",
            # Heading only. The body carries **Next:** paragraphs whose bare TODO/TBD
            # text would break G-COMPLETE, and **Slice:** exists on only 4 of 106.
            "entry": entry["heading"],
            "occurred_at": entry["date"],
            # Safe here and only here: progress_entries is an empty family, so there is
            # no migrated {"v1": ...} blob to overwrite (guard G1 concerns UPDATES).
            "custom_attributes": json.dumps(attrs, ensure_ascii=False),
        }
        if entry["token"]:
            num = int(entry["token"][1:])
            row["slice_id"] = f"SL-{num:03d}"
            row["phase_id"] = phase_for(num)
        payloads.append(row)
    return payloads


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", metavar="PATH")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

    entries, sections = parse(LOG.read_text(encoding="utf-8").splitlines())
    payloads = build_payloads(entries)

    by_prov: dict[str, int] = {}
    for entry in entries:
        by_prov[entry["provenance"]] = by_prov.get(entry["provenance"], 0) + 1
    resolved = sum(1 for p in payloads if "slice_id" in p)
    unresolved_sections = [s for s in sections if s.get("unresolved")]

    print(f"# progress-entry payloads\n")
    print(f"- source: `docs/progress/progress-log.md`")
    print(f"- `##` sections: **{len(sections)}**  ·  PE- rows: **{len(payloads)}**")
    print(f"- date provenance: " + "  ·  ".join(
        f"**{v}** {k}" for k, v in sorted(by_prov.items())))
    print(f"- slice resolved: **{resolved}** / {len(payloads)}"
          f"  ·  slice NULL (never guessed): **{len(payloads) - resolved}**")
    if unresolved_sections:
        print(f"- sections with NO date anywhere, skipped: "
              f"**{len(unresolved_sections)}**")
        for sec in unresolved_sections:
            print(f"    - line {sec['line']}: {sec['heading'][:78]}")
    print()

    covered = sorted({p["slice_id"] for p in payloads if "slice_id" in p},
                     key=lambda s: int(s[3:]))
    print(f"## Slice coverage\n")
    print("| slice | entries |")
    print("|---|---|")
    for slice_id in covered:
        print(f"| {slice_id} | {sum(1 for p in payloads if p.get('slice_id') == slice_id)} |")
    missing = [f"SL-{n:03d}" for n in range(1, 20) if f"SL-{n:03d}" not in covered]
    print(f"\n**No entries:** {', '.join(missing) if missing else 'none'}")

    if args.json:
        pathlib.Path(args.json).write_text(
            json.dumps(payloads, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"\n_{len(payloads)} payloads written to {args.json}_", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
