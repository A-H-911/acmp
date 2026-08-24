#!/usr/bin/env python3
"""Count the distinct Tamheed identifiers a prompt file cites, and name the pattern used.

WHY THIS EXISTS. `prm-next.md` twice asserted how many identifiers a verification pass
covered — 206 in the file, 217 in the progress entry that described the same pass, both
written by commit c043ed6. Neither could be reproduced, because the pattern that produced
them was never written down. A number without its instrument is a claim, not a measurement.

So this script always prints the regex alongside the count: disagree with the pattern, not
with the number. Paths resolve from the repo root derived from this file's own location, so
it behaves the same however it is invoked (trap 29).

    python scripts/count-prompt-ids.py                       # defaults to prm-next.md
    python scripts/count-prompt-ids.py path/to/other.md ...
"""

import collections
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_TARGET = REPO_ROOT / "tamheed-package" / "prompts" / "prm-next.md"

# The 18 entity families that carry NNN-numbered identifiers in this package's prose.
# WBS ids are dotted (WBS-24.1), so the fraction part is optional and part of the id.
FAMILIES = (
    "DW DEF DEC ADR SC LL OQ PE AC AV FR NFR WBS SL PH ASM INV WVR SEC DOC"
).split()
PATTERN = r"\b(?:" + "|".join(FAMILIES) + r")-[0-9]+(?:\.[0-9]+)?\b"


def report(path: Path) -> int:
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
    return len(ids)


def main() -> int:
    targets = [Path(a) for a in sys.argv[1:]] or [DEFAULT_TARGET]
    for i, target in enumerate(targets):
        if i:
            print()
        if not target.is_absolute():
            target = REPO_ROOT / target
        if not target.exists():
            print(f"missing: {target}", file=sys.stderr)
            return 1
        report(target)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
