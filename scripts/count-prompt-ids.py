#!/usr/bin/env python3
"""Count and RESOLVE the Tamheed identifiers a prompt file cites, and name the pattern used.

WHY THIS EXISTS. `prm-next.md` twice asserted how many identifiers a verification pass
covered — 206 in the file, 217 in the progress entry that described the same pass, both
written by commit c043ed6. Neither could be reproduced, because the pattern that produced
them was never written down. A number without its instrument is a claim, not a measurement.

So this always prints the regex alongside the count: disagree with the pattern, not with
the number. With --resolve (the default when the package is present) it also looks every
cited id up in the canonical JSONL and reports the ones that do not exist, plus the
lifecycle_status of the ones that do — the resume ceremony's mechanical half.

THE MECHANICAL HALF IS THE EASY HALF. Three separate resume passes have run clean here
while the file still carried wrong statements, because none of them was an id or a status:
a stale INSTRUCTION, a prose COUNT, an ORDINAL in a sequence, and a CAUSE inferred from a
number that was later corrected (LL-020). A clean run of this script is not a verified file.

Paths resolve from this file's own location, never the cwd (trap 29).

    python scripts/count-prompt-ids.py                       # defaults to prm-next.md
    python scripts/count-prompt-ids.py --no-resolve other.md
"""

import collections
import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DATA = REPO_ROOT / "tamheed-package" / "data"
DEFAULT_TARGET = REPO_ROOT / "tamheed-package" / "prompts" / "prm-next.md"

# Family -> the register file that owns it. Families with no register resolve to None and
# are counted but not checked, which is reported rather than hidden.
REGISTERS = {
    "DW": "deferred_work", "DEF": "defects", "DEC": "decisions", "ADR": "adrs",
    "SC": "scope_changes", "LL": "lessons", "OQ": "open_questions",
    "PE": "progress_entries", "AC": "acceptance_criteria", "AV": "audit_verdicts",
    "FR": "requirements", "NFR": "requirements", "WBS": "wbs_items", "SL": "slices",
    "PH": "phases", "ASM": "assumptions", "INV": "invariants", "WVR": None,
    "SEC": "document_sections", "DOC": "narrative_documents",
}
PATTERN = r"\b(?:" + "|".join(REGISTERS) + r")-[0-9]+(?:\.[0-9]+)?\b"


def load_register(name):
    path = DATA / f"{name}.jsonl"
    if not path.exists():
        return None
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            r = json.loads(line)
            rows[r.get("id")] = r.get("lifecycle_status")
    return rows


def report(path: Path, resolve: bool) -> int:
    text = path.read_text(encoding="utf-8")
    ids = set(re.findall(PATTERN, text))
    families = collections.Counter(i.split("-")[0] for i in ids)

    try:
        shown = path.relative_to(REPO_ROOT)
    except ValueError:
        shown = path  # a target outside the repo (a control file, say) prints absolute
    print(f"file      : {shown}")
    print(f"pattern   : {PATTERN}")
    print(f"distinct  : {len(ids)}")
    print(f"families  : {len(families)}")
    for name, n in sorted(families.items(), key=lambda kv: (-kv[1], kv[0])):
        print(f"  {name:<4} {n}")

    if not resolve:
        return 0

    cache, missing, unchecked, statuses = {}, [], [], collections.Counter()
    for i in sorted(ids):
        fam = i.split("-")[0]
        reg = REGISTERS.get(fam)
        if reg is None:
            unchecked.append(i)
            continue
        if reg not in cache:
            cache[reg] = load_register(reg)
        rows = cache[reg]
        if rows is None:
            unchecked.append(i)
        elif i not in rows:
            missing.append(i)
        else:
            statuses[f"{fam}:{rows[i]}"] += 1

    print(f"\nresolved  : {len(ids) - len(missing) - len(unchecked)} of {len(ids)}")
    print(f"UNRESOLVED: {', '.join(missing) if missing else 'none'}")
    if unchecked:
        print(f"unchecked : {', '.join(unchecked)} (no register for that family)")
    print("\nstatus of cited rows:")
    for k, n in sorted(statuses.items()):
        print(f"  {k:<28} {n}")
    print(
        # ASCII only: this prints to a Windows console whose cp1252 codec throws on the
        # warning glyph, which crashed the script the first time it ran here.
        "\nNOTE: a clean run proves ids and statuses ONLY. Stale INSTRUCTIONS, prose COUNTS,\n"
        "  ORDINALS, and CAUSES built on corrected numbers are all invisible to it (LL-020)."
    )
    return 1 if missing else 0


def main() -> int:
    args = [a for a in sys.argv[1:] if a != "--no-resolve"]
    resolve = "--no-resolve" not in sys.argv and DATA.exists()
    targets = [Path(a) for a in args] or [DEFAULT_TARGET]
    for i, target in enumerate(targets):
        if i:
            print()
        if not target.is_absolute():
            target = REPO_ROOT / target
        if not target.exists():
            print(f"missing: {target}", file=sys.stderr)
            return 2
        report(target, resolve)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
