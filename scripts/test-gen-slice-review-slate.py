#!/usr/bin/env python3
"""Regression tests for scripts/gen-slice-review-slate.mjs.  Usage:  python scripts/test-gen-slice-review-slate.py

WHY THIS EXISTS. That generator IS how LL-011 is discharged: an item it cannot render is one whose
slice review would have to be hand-built, which is the exact harm the lesson forbids. It has now been
unable to render an item three times for three different reasons — DEF-116 (it demanded exactly one
acceptance criterion, while WBS-24.4 named two and WBS-24.5 three), DEF-117 (it refused an item naming
NO criterion, though WBS-25.1's criterion-lessness is deliberate and recorded in DW-090), and DEF-119
(the DEF-116 fix swapped the count guard for a sameness guard, so WBS-24.5 — one of the two rows
DEF-116 was filed about — still aborted, because its three criteria answer to three requirements).

Every one of those was found by RUNNING it against a shape nobody had run it against. So the tests are
shapes, not assertions about today's data.

HOW IT AVOIDS TOUCHING THE PACKAGE. The generator resolves its data directory from its OWN location
(ROOT = dirname(script)/..), so copying the script and the store into a scratch tree that mirrors that
layout exercises the real code with zero changes to it and zero risk to tamheed-package/ (C31: the
package lives in the git working tree, and a test that mutated it would be indistinguishable from work).

⚠ CASE 3 IS A CALIBRATION, NOT A FEATURE TEST, AND IT IS THE ONE THAT EARNS THE OTHER TWO. LL-013: a
suite that only ever passes has not been shown to discriminate. It strips the reason out of a
criterion-less item and asserts the generator still exits non-zero — because DEF-117's fix is a
narrowing of a refusal, and a narrowing that went too far would look exactly like a passing suite.
"""
import json
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "scripts", "gen-slice-review-slate.mjs")
SCRATCH = os.path.join(ROOT, ".slate-test-tmp")


def build(mutate=None):
    """Mirror <root>/scripts/gen.mjs + <root>/tamheed-package/data/ so ROOT resolution works."""
    shutil.rmtree(SCRATCH, ignore_errors=True)
    os.makedirs(os.path.join(SCRATCH, "scripts"))
    data = os.path.join(SCRATCH, "tamheed-package", "data")
    shutil.copytree(os.path.join(ROOT, "tamheed-package", "data"), data)
    shutil.copy(SCRIPT, os.path.join(SCRATCH, "scripts", "gen.mjs"))
    if mutate:
        mutate(data)
    return data


def run(slice_id):
    p = subprocess.run(
        ["node", os.path.join(SCRATCH, "scripts", "gen.mjs"), slice_id],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    return p.returncode, (p.stdout + p.stderr).strip()


def rewrite(path, fn):
    rows = [json.loads(l) for l in open(path, encoding="utf-8") if l.strip()]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for r in rows:
            f.write(json.dumps(fn(r), ensure_ascii=False) + "\n")


def edit_item(item_id, change):
    def mutate(data):
        def f(r):
            if r.get("id") == item_id:
                change(r)
            return r
        rewrite(os.path.join(data, "wbs_items.jsonl"), f)
    return mutate


def case(name, mutate, slice_id, want_code, want_sub):
    build(mutate)
    code, out = run(slice_id)
    ok = code == want_code and want_sub in out
    print(("PASS  " if ok else "FAIL  ") + name)
    if not ok:
        print("        exit=%d (want %d)" % (code, want_code))
        print("        want substring: %r" % want_sub)
        print("        got: %s" % out.replace("\n", " | ")[:400])
    return ok


def main():
    results = []

    # DEF-117 — an item may name NO criterion when the store records why.
    results.append(case(
        "DEF-117: criterion-less item renders when a DW-/DEC- row records why",
        None, "SL-034", 0, "item(s) at Review"))

    # DEF-116 + DEF-119 — many criteria, spanning many requirements, must render.
    # SL-033's rows are all Implemented, so put the three-criterion one back at Review
    # in the COPY. Its criteria answer to FR-155, NFR-059 and NFR-060.
    results.append(case(
        "DEF-116/DEF-119: multi-criterion, multi-requirement item renders",
        edit_item("WBS-24.5", lambda r: r.update(lifecycle_status="Review")),
        "SL-033", 0, "item(s) at Review"))

    # CALIBRATION — the refusal must still fire when nothing records the reason.
    results.append(case(
        "CALIBRATION: no criterion AND no recorded reason still exits non-zero",
        edit_item("WBS-25.1",
                  lambda r: r.update(title=re.sub(r"\b(DW|DEC)-\d+\b", "XX-000", r["title"]))),
        "SL-034", 2, "nothing here can be adjudicated"))

    shutil.rmtree(SCRATCH, ignore_errors=True)
    print()
    print("%d/%d passed" % (sum(results), len(results)))
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
