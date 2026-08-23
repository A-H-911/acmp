"""Repair the wbs_items family from docs/planning/work-breakdown.md.

Three distinct defects, one source:

  epic titles   the 20 epic rows took the PHASE-NUMBER cell as their title
                (WBS-1.title == "1", WBS-11.title == "1-2"). The real names live
                only in custom_attributes.v1.Title.
  phase_id      NULL on all 155. The value exists in v1.Phase for the 20 epics
                ONLY - the 135 leaves carry no custom_attributes at all, so they
                must inherit from their parent epic.
  leaf titles   133 of 135 truncated at exactly 120 chars, AND every ASCII hyphen
                stripped by migrate.py::_clean_line ("FR-001" -> "FR 001",
                "BL-005" -> "BL 0"). Not recoverable from the package - only from
                this source file. All 135 are re-sourced, not just the 133: the
                2 short ones are hyphen-damaged even though they fit the cap.

Read-only. Emits JSON for review; performs no MCP calls.

    python parse_wbs.py --outdir reports/
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs" / "planning" / "work-breakdown.md"

EPIC_ROW = re.compile(
    r"^\|\s*EPIC-(?P<epic>\d+)\s*\|\s*(?P<title>[^|]+?)\s*\|\s*(?P<modules>[^|]*?)\s*\|"
    r"\s*(?P<size>[^|]*?)\s*\|\s*WBS-(?P<group>\d+)\s*\|[^|]*\|\s*(?P<phase>[^|]*?)\s*\|"
)
LEAF = re.compile(r"^-\s+\*\*WBS-(?P<id>\d+\.\d+)\*\*\s+(?P<body>.+?)\s*$")


def earliest_phase(cell: str) -> str | None:
    """'1' -> PH-1 ; '1-2' / '1-3' / '2-3' -> the EARLIEST phase.

    A span cannot be represented by a single FK, so the earliest is taken and the
    full span stays visible in the epic's untouched custom_attributes.v1.Phase.
    Note the source uses an en-dash, not a hyphen.
    """
    digits = re.findall(r"\d", cell)
    return f"PH-{digits[0]}" if digits else None


def parse(lines: list[str]) -> tuple[list[dict], list[dict]]:
    epics: dict[str, dict] = {}
    payloads: list[dict] = []

    for line in lines:
        match = EPIC_ROW.match(line.strip())
        if not match:
            continue
        wbs_id = f"WBS-{int(match.group('group'))}"
        phase = earliest_phase(match.group("phase"))
        epics[wbs_id] = {"phase": phase, "raw_phase": match.group("phase").strip()}
        row = {
            "type": "wbs-item",
            "id": wbs_id,
            # The real epic name, replacing the phase-number cell the migration stored.
            "title": match.group("title").strip(),
            "lifecycle_status": "Approved",
        }
        if phase:
            row["phase_id"] = phase
        size = match.group("size").strip()
        if size:
            row["effort"] = size
        payloads.append(row)

    leaves: list[dict] = []
    for line in lines:
        match = LEAF.match(line.strip())
        if not match:
            continue
        leaf_id = f"WBS-{match.group('id')}"
        parent = f"WBS-{match.group('id').split('.')[0]}"
        # Full untruncated text with hyphens intact, id prefix restored.
        title = f"{leaf_id} {match.group('body').strip()}"
        row = {
            "type": "wbs-item",
            "id": leaf_id,
            "title": title,
            "parent_id": parent,
            "lifecycle_status": "Approved",
        }
        inherited = epics.get(parent, {}).get("phase")
        if inherited:
            row["phase_id"] = inherited
        leaves.append(row)

    return payloads, leaves


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--outdir", default="reports")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

    lines = SOURCE.read_text(encoding="utf-8").splitlines()
    epics, leaves = parse(lines)
    payloads = epics + leaves

    outdir = pathlib.Path(args.outdir)
    outdir.mkdir(parents=True, exist_ok=True)
    (outdir / "wbs-payloads.json").write_text(
        json.dumps(payloads, indent=2, ensure_ascii=False), encoding="utf-8")

    lengths = [len(r["title"]) for r in leaves]
    restored = sum(1 for n in lengths if n > 120)
    hyphens = sum(1 for r in payloads if "-" in r["title"])
    no_phase = [r["id"] for r in payloads if "phase_id" not in r]

    print("# wbs_items repair payloads\n")
    print(f"- source: `docs/planning/work-breakdown.md`")
    print(f"- epics: **{len(epics)}**  ·  leaves: **{len(leaves)}**"
          f"  ·  total: **{len(payloads)}**")
    print(f"- leaf titles now over the old 120 cap: **{restored}**"
          f"  ·  longest: **{max(lengths, default=0)}** chars")
    print(f"- titles containing a hyphen: **{hyphens}** / {len(payloads)}"
          f"  (stored package has 0 — every one was stripped)")
    print(f"- rows with no resolvable phase: **{len(no_phase)}**"
          f"{' ' + str(no_phase) if no_phase else ''}\n")

    print("## Epic titles restored\n")
    print("| id | stored (damaged) | recovered | phase | effort |")
    print("|---|---|---|---|---|")
    store = {}
    data = ROOT / "tamheed-package" / "data" / "wbs_items.jsonl"
    if data.exists():
        for line in data.open(encoding="utf-8"):
            row = json.loads(line)
            store[row["id"]] = row.get("title", "")
    for row in epics:
        print(f"| {row['id']} | `{store.get(row['id'], '?')}` | {row['title']} "
              f"| {row.get('phase_id', '—')} | {row.get('effort', '—')} |")

    placeholder = re.compile(r"\bTODO\b|\bTBD\b|\bFIXME\b|<placeholder>|\{\{|lorem ipsum",
                             re.I)
    hits = [r["id"] for r in payloads if placeholder.search(r["title"])]
    print(f"\n**G-COMPLETE pre-scan:** {'CLEAN' if not hits else 'HITS ' + str(hits)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
