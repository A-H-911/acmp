"""Recover docs/planning/roadmap.md, which the migration consumed into 4 phase rows.

The migration produced PH-0..PH-3 and MS-001..006 and then discarded the file. Lost
from the package entirely: each phase's Goal / Scope / Deliverables / Validation /
Exit gate / Status prose, the authoritative P1-P19 build-slice ladder, and the Legacy
token map - the only record that "P1-P4" in backlog.md are PRIORITY CODES and that the
design package's "P12"/"P15" are a different numbering scheme.

Emits three payload sets:

  doc      DOC-053 narrative document + document-sections (faithful copy of the file)
  phases   objective / exit_criteria / lifecycle_status for PH-0..PH-3
  slices   SL-001..SL-019 from the ladder tables

Sections split on BOTH ## and ###. The migration split on ## only, which is defect D-7
in findings_4 - splitting finely here is the fix, not editorialising: the prose is
copied verbatim, only the section boundaries differ.

Read-only. Emits JSON for review; performs no MCP calls.

    python parse_roadmap.py --outdir reports/
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
ROADMAP = ROOT / "docs" / "planning" / "roadmap.md"
DOC_ID = "DOC-053"

HEADING = re.compile(r"^(#{2,3})\s+(.*)$")
FIELD = re.compile(r"^-\s+\*\*(?P<label>[^*]+?)\.?\*\*\s*(?P<body>.*)$")
STATUS_SENTENCE = re.compile(r"\*\*Status:\s*(?P<status>[^*]+?)\.?\*\*\s*$")
LADDER_ROW = re.compile(r"^\|\s*P(?P<num>\d{1,2})\s*\|\s*(?P<theme>.+?)\s*\|")

# Roadmap prose status -> Tamheed phase lifecycle. "substantially delivered" is NOT
# complete (PH-2's own text says the Webex/Tarseem/Knowledge remainder is backlog), so
# it stays Approved rather than being inflated to Implemented.
PHASE_STATUS = {
    "complete": "Implemented",
    "substantially delivered": "Approved",
    "not started": "Approved",
}


MD_LINK = re.compile(r"\[([^\]]+)\]\((?:[^)]+)\)")


def delink(text: str) -> str:
    """`[checkpoints](../execution/checkpoints.md)` -> `checkpoints`.

    Applied to TYPED COLUMNS only, never to the DOC-053 body. The roadmap's relative
    links were written for docs/planning/ and resolve to nothing from the package -
    carrying them into a slice title reproduces defect D-8 (links not re-anchored on
    relocation). The document body stays verbatim because it is an archive copy.
    """
    return MD_LINK.sub(r"\1", text).strip()


def phase_for(num: int) -> str:
    """P1-P12 shipped under PH-1; P13-P15 are the PH-2 remainder; P16-P19 are
    cross-cutting and live under the PH-4 created by DEC-029/SC-001."""
    if num <= 12:
        return "PH-1"
    if num <= 15:
        return "PH-2"
    return "PH-4"


def split_sections(lines: list[str]) -> list[dict]:
    """(heading, body) pairs on ## and ###, with ### qualified by its parent."""
    sections: list[dict] = []
    heading, parent, buf = "Preamble", "", []

    def flush() -> None:
        body = "\n".join(buf).strip()
        if body or heading != "Preamble":
            sections.append({"heading": heading, "body": body})

    for line in lines:
        match = HEADING.match(line)
        if not match:
            buf.append(line)
            continue
        flush()
        level, text = len(match.group(1)), match.group(2).strip()
        if level == 2:
            parent, heading = text, text
        else:
            heading = f"{parent} › {text}" if parent else text
        buf = []
    flush()
    return sections


def parse_phases(sections: list[dict]) -> list[dict]:
    """Goal -> objective, Exit gate -> exit_criteria, Status -> lifecycle_status."""
    payloads = []
    for section in sections:
        match = re.match(r"^(PH-\d+)\s*[-—]\s*(.+)$", section["heading"])
        if not match:
            continue
        phase_id, title = match.group(1), match.group(2).strip()
        fields = {}
        for line in section["body"].splitlines():
            field = FIELD.match(line.strip())
            if field:
                fields[field.group("label").strip().lower()] = field.group("body").strip()

        exit_gate = fields.get("exit gate", "")
        status_match = STATUS_SENTENCE.search(exit_gate)
        lifecycle = None
        if status_match:
            raw = status_match.group("status").strip().lower()
            # Strip the status sentence out of exit_criteria: phase status belongs in
            # lifecycle_status, and leaving it inline is how the handoff manifest ended
            # up asserting a stale "remainder outstanding" (defect D-9 / plan W14).
            exit_gate = STATUS_SENTENCE.sub("", exit_gate).strip()
            for key, value in PHASE_STATUS.items():
                if raw.startswith(key):
                    lifecycle = value
                    break

        row = {"type": "phase", "id": phase_id, "title": title}
        if fields.get("goal"):
            row["objective"] = delink(fields["goal"])
        if exit_gate:
            row["exit_criteria"] = delink(exit_gate)
        if lifecycle:
            row["lifecycle_status"] = lifecycle
        payloads.append(row)
    return payloads


def parse_slices(lines: list[str]) -> list[dict]:
    """SL-001..SL-019 from the two ladder tables."""
    payloads, seen = [], set()
    for line in lines:
        match = LADDER_ROW.match(line.strip())
        if not match:
            continue
        num = int(match.group("num"))
        if not 1 <= num <= 19 or num in seen:
            continue
        seen.add(num)
        theme = delink(match.group("theme").strip())
        # Short name for `title`, full prose for `objective`. P14's theme carries a
        # multi-sentence deferral note; a 500-char title helps nobody.
        title = theme.split("**")[0].strip(" .—-") or theme
        row = {
            "type": "slice",
            "id": f"SL-{num:03d}",
            "title": f"P{num} — {title}",
            "phase_id": phase_for(num),
            "sort_order": num,
            "objective": theme,
            # Everything shipped except P14, deferred indefinitely by DEC-028.
            "lifecycle_status": "Deferred" if num == 14 else "Implemented",
        }
        payloads.append(row)
    return sorted(payloads, key=lambda r: r["sort_order"])


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--outdir", default="reports")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

    lines = ROADMAP.read_text(encoding="utf-8").splitlines()
    sections = split_sections(lines)
    phases = parse_phases(sections)
    slices = parse_slices(lines)

    doc = [{
        "type": "narrative-document", "id": DOC_ID, "doc_kind": "other",
        "title": "Roadmap — ACMP", "lifecycle_status": "Approved",
        "custom_attributes": json.dumps(
            {"v1": {"path": "planning/roadmap.md"},
             "recovered_by": "tamheed-repair: migration consumed this file into "
                             "PH-/MS- rows and emitted no narrative document"},
            ensure_ascii=False),
    }]
    doc += [
        {"type": "document-section", "id": f"SEC-{900 + i}", "document_id": DOC_ID,
         "heading": section["heading"], "body": section["body"], "sort_order": i}
        for i, section in enumerate(sections, 1) if section["body"]
    ]

    outdir = pathlib.Path(args.outdir)
    outdir.mkdir(parents=True, exist_ok=True)
    for name, payload in (("doc053", doc), ("phases", phases), ("slices", slices)):
        (outdir / f"{name}-payloads.json").write_text(
            json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")

    print("# roadmap recovery payloads\n")
    print(f"- source: `docs/planning/roadmap.md` ({len(lines)} lines)")
    print(f"- {DOC_ID} sections: **{len(doc) - 1}**  ·  phases: **{len(phases)}**"
          f"  ·  slices: **{len(slices)}**\n")

    print("## Sections\n")
    for section in doc[1:]:
        print(f"- `{section['id']}` {section['heading']}  ({len(section['body'])} chars)")

    print("\n## Phases\n")
    print("| id | lifecycle | objective | exit_criteria |")
    print("|---|---|---|---|")
    for row in phases:
        print(f"| {row['id']} | {row.get('lifecycle_status', '(unchanged)')} "
              f"| {len(row.get('objective', ''))} chars "
              f"| {len(row.get('exit_criteria', ''))} chars |")

    print("\n## Slices\n")
    print("| id | phase | lifecycle | title |")
    print("|---|---|---|---|")
    for row in slices:
        print(f"| {row['id']} | {row['phase_id']} | {row['lifecycle_status']} "
              f"| {row['title'][:66]} |")

    placeholder = re.compile(r"\bTODO\b|\bTBD\b|\bFIXME\b|<placeholder>|\{\{|lorem ipsum",
                             re.I)
    hits = [s["id"] for s in doc[1:] if placeholder.search(s["body"])]
    print(f"\n**G-COMPLETE pre-scan:** {'CLEAN' if not hits else 'HITS ' + str(hits)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
