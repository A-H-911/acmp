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


def stage(slice_id, item_id, title=None):
    """Put EXACTLY item_id at Review inside slice_id, optionally replacing its title.

    ⚠ DEF-120 — AN EARLIER VERSION SET ONE ROW TO Review AND LEFT THE REST ALONE, so each case
    silently depended on which OTHER rows happened to be at Review in the live store. Promoting
    WBS-25.1 to Implemented deleted the calibration's subject: the mutation still applied, the
    generator still ran, and it rendered a DIFFERENT item that was never under test. That failed
    loudly here only by luck — had the surviving row lacked citations, the calibration would have
    PASSED for the wrong reason, which is the hollow pass it exists to prevent.

    So every case now stages the whole slice: one row at Review, all its siblings forced out, and
    the shape under test written into the title. The tests assert a PROPERTY of the generator, not
    a fact about today's register.
    """
    def mutate(data):
        def f(r):
            if r.get("slice_id") == slice_id:
                if r.get("id") == item_id:
                    r["lifecycle_status"] = "Review"
                    if title is not None:
                        r["title"] = title
                else:
                    r["lifecycle_status"] = "Implemented"
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

    # DEF-117 — an item may name NO criterion when the store records why. The title cites a
    # requirement and a DW- row and no AC-, which is exactly the shape WBS-25.1 had.
    results.append(case(
        "DEF-117: criterion-less item renders when a DW-/DEC- row records why",
        stage("SL-034", "WBS-25.1",
              "Criterion-less by design. Requirement NFR-054; the reason is recorded in DW-090."),
        "SL-034", 0, "item(s) at Review"))

    # DEF-116 + DEF-119 — many criteria, spanning many requirements, must render. WBS-24.5's real
    # title names AC-149/AC-150/AC-151, which answer to FR-155, NFR-059 and NFR-060 respectively:
    # three criteria across three requirements, the shape that still aborted after DEF-116.
    results.append(case(
        "DEF-116/DEF-119: multi-criterion, multi-requirement item renders",
        stage("SL-033", "WBS-24.5"),
        "SL-033", 0, "item(s) at Review"))

    # CALIBRATION — the refusal must still fire when NOTHING records the reason. No AC-, no DW-,
    # no DEC-; a requirement alone is not a reason. If this ever passes, the DEF-117 arm has been
    # widened past the point where it discriminates.
    results.append(case(
        "CALIBRATION: no criterion AND no recorded reason still exits non-zero",
        stage("SL-034", "WBS-25.1",
              "Requirement NFR-054. Nothing here records why there is no criterion."),
        "SL-034", 2, "nothing here can be adjudicated"))

    shutil.rmtree(SCRATCH, ignore_errors=True)
    print()
    print("%d/%d passed" % (sum(results), len(results)))
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
