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

⚠ WHAT WBS-31 CHANGED HERE, AND WHY IT IS NOT COSMETIC. The generator now reads the tamheed exports
under tamheed-package/exports/ rather than data/*.jsonl (DEC-135 d1, ruled compliant by DEC-139 d1), so
this harness mirrors exports/ and mutates JSON. It deliberately no longer copies data/ AT ALL — that
copy was itself a read of the store, which is the thing the ruling forbids, and it would have been a
non-compliant read hiding inside the instrument that proves compliance.

⚠ THE HARNESS ITSELF CHANGED SHAPE, SO THE HARNESS ITSELF IS CALIBRATED. Cases 6 and 7 inject faults
into the EXPORT (a short read, and two families from different package states) and assert the generator
refuses. Those two guards replaced the old paging walk and are brand new, so a suite that never exercised
them would be reporting on machinery nobody has seen fail — LL-013 applied to the test tree rather than
to the code under it.

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
    """Mirror <root>/scripts/{gen.mjs,lib/} + <root>/tamheed-package/exports/ so ROOT resolution works."""
    src = os.path.join(ROOT, "tamheed-package", "exports")
    present = [f for f in os.listdir(src) if f.endswith(".json")] if os.path.isdir(src) else []
    if not present:
        # Fail LOUD. An empty exports/ would make every case exit 2 for a reason that has nothing to
        # do with what it tests, and a suite of uniform failures reads like a broken generator.
        sys.exit(
            "FATAL: no exports in %s\n"
            "  This harness reads the tamheed exports, never data/*.jsonl (DEC-135 d1 / DEC-139 d1).\n"
            "  Export the seven families the slate needs FIRST, e.g.\n"
            '    entity_export("wbs_items.json", args={"type": "wbs-item", "limit": 5000})\n'
            "  ...then acceptance_criteria, requirements, deferred_work, decisions, audit_verdicts, slices."
            % src
        )
    shutil.rmtree(SCRATCH, ignore_errors=True)
    os.makedirs(os.path.join(SCRATCH, "scripts"))
    exports = os.path.join(SCRATCH, "tamheed-package", "exports")
    shutil.copytree(src, exports)
    shutil.copy(SCRIPT, os.path.join(SCRATCH, "scripts", "gen.mjs"))
    shutil.copytree(os.path.join(ROOT, "scripts", "lib"), os.path.join(SCRATCH, "scripts", "lib"))
    if mutate:
        mutate(exports)
    return exports


def run(slice_id):
    p = subprocess.run(
        ["node", os.path.join(SCRATCH, "scripts", "gen.mjs"), slice_id],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    return p.returncode, (p.stdout + p.stderr).strip()


def rewrite(path, fn, envelope=None):
    """Map fn over an export's rows in place. `envelope` may mutate the whole document.

    Row count is preserved, so result.count/total stay honest — a case that silently made an export
    look partial would be testing the short-read guard while claiming to test something else.
    """
    with open(path, encoding="utf-8") as f:
        doc = json.load(f)
    doc["result"]["rows"] = [fn(r) for r in doc["result"]["rows"]]
    if envelope:
        envelope(doc)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, ensure_ascii=False)


KEEP = object()  # stage(): leave the row's custom_attributes as exported


def stage(slice_id, item_id, title=None, source_span=None, custom_attributes=KEEP):
    """Put EXACTLY item_id at Review inside slice_id, optionally replacing title/source_span.

    ⚠ DEF-142 ADDED `source_span`, AND IT IS NOT A CONVENIENCE. That fix made the criterion-less
    DEC- lookup read `title` AND `source_span`, so a case that overrides only the title no longer
    controls the whole input: case 3 asserts "nothing here records the reason" while a DEC- id could
    still be sitting in the untouched source_span. It would fail loudly rather than pass hollowly,
    but it would be testing a fact about today's register instead of a property of the generator —
    exactly what DEF-120 rewrote this helper to stop.

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
    def mutate(exports):
        def f(r):
            if r.get("slice_id") == slice_id:
                if r.get("id") == item_id:
                    r["lifecycle_status"] = "Review"
                    if title is not None:
                        r["title"] = title
                    if source_span is not None:
                        r["source_span"] = source_span
                    if custom_attributes is not KEEP:
                        r["custom_attributes"] = custom_attributes
                else:
                    r["lifecycle_status"] = "Implemented"
            return r
        rewrite(os.path.join(exports, "wbs_items.json"), f)
    return mutate


def case(name, mutate, slice_id, want_code, want_sub, html_has=None, html_lacks=None):
    """html_has / html_lacks are checked against the page the generator WROTE, not its stdout."""
    build(mutate)
    code, out = run(slice_id)
    html = ""
    if html_has is not None or html_lacks is not None:
        page = os.path.join(SCRATCH, "tamheed-package", "slice-review-slate.html")
        if os.path.exists(page):
            with open(page, encoding="utf-8") as f:
                html = f.read()
    ok = (code == want_code and want_sub in out
          and (html_has is None or html_has in html)
          and (html_lacks is None or (html and html_lacks not in html)))
    print(("PASS  " if ok else "FAIL  ") + name)
    if not ok:
        print("        exit=%d (want %d)" % (code, want_code))
        print("        want substring: %r" % want_sub)
        if html_has is not None:
            print("        page must contain: %r (%s)" % (html_has, html_has in html))
        if html_lacks is not None:
            print("        page must be written and lack: %r (written=%s)" % (html_lacks, bool(html)))
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
              "Requirement NFR-054. Nothing here records why there is no criterion.",
              source_span=""),
        "SL-034", 2, "nothing here can be adjudicated"))

    # DEF-142 — AN INSTRUMENT ITEM: criterion-less AND naming no requirement at all. WBS-29 was
    # commissioned by DEC-134 d1 to make a DEF-129 occurrence answerable; its own row says it
    # "diagnoses nothing", so it satisfies no FR/NFR by construction. The old guard scraped a
    # requirement id out of the title and fatalled when it found none, which silently re-narrowed
    # what DEF-117 had just opened. Note the deciding DEC- lives in source_span, which is where this
    # store puts provenance — the title cites only decisions the item REFERENCES.
    results.append(case(
        "DEF-142: instrument item (no criterion, NO requirement) renders from its deciding DEC-",
        stage("SL-038", "WBS-29",
              "An instrument. It diagnoses nothing and satisfies no requirement.",
              source_span="Commissioned by DEC-134 d1; scoped by SC-047."),
        "SL-038", 0, "item(s) at Review"))

    # CALIBRATION for the DEF-142 arm, and it is the pair that earns it. Identical to the case above
    # in every respect EXCEPT that nothing anywhere records the reason. If this ever exits 0, the
    # requirement guard has been widened into the fail-closed case that DEF-117 deliberately kept.
    results.append(case(
        "CALIBRATION: no criterion, no requirement AND no reason still exits non-zero",
        stage("SL-038", "WBS-29",
              "An instrument. It diagnoses nothing and satisfies no requirement.",
              source_span="No deciding row is named anywhere on this item."),
        "SL-038", 2, "nothing here can be adjudicated"))

    # ---- WBS-31: the two guards that replaced the paging walk, each calibrated ----------------
    # Both faults are injected into an otherwise-PASSING invocation — the same stage() as the
    # multi-criterion case above, which exits 0. So the pair is controlled: identical input except
    # for the injected fault, and any exit-2 is attributable to the guard rather than to the shape.

    def combine(*mutators):
        def mutate(exports):
            for m in mutators:
                m(exports)
        return mutate

    def short_read(exports):
        """Make one export look truncated: total > count, as a limit below total would produce."""
        rewrite(
            os.path.join(exports, "requirements.json"),
            lambda r: r,
            envelope=lambda doc: doc["result"].update(total=doc["result"]["count"] + 5),
        )

    def mixed_digest(exports):
        """Make one family come from a different package state than its siblings."""
        rewrite(
            os.path.join(exports, "decisions.json"),
            lambda r: r,
            envelope=lambda doc: doc["tamheed_export"].update(digest="0" * 64),
        )

    # A SHORT READ IS THE DANGEROUS ONE: it yields a slate with a hole, which reads exactly like a
    # slate without one. The old generator walked after_id to prevent it; entity_export pages
    # internally, so this assertion is now the only thing standing there.
    results.append(case(
        "WBS-31 CALIBRATION: a partial export is refused, not silently rendered",
        combine(stage("SL-033", "WBS-24.5"), short_read),
        "SL-033", 2, "PARTIAL"))

    # The digest is the PACKAGE digest, so two families exported either side of a write disagree.
    # Such a slate quotes two different states of the store while looking entirely consistent, and
    # nothing else in the pipeline can see it.
    results.append(case(
        "WBS-31 CALIBRATION: exports from two package states are refused as a mixed snapshot",
        combine(stage("SL-033", "WBS-24.5"), mixed_digest),
        "SL-033", 2, "NOT one snapshot"))

    # ---- DEF-160: the item's own custom_attributes reach the page ---------------------------------
    # WBS-40.1's completion record lived only in custom_attributes and the page never showed it. The
    # marker is a value no real row carries, so finding it on the page can only mean it was rendered.
    record = "COMPLETION-RECORD-ONLY-IN-AN-ATTRIBUTE merged as abc123, run 42"
    results.append(case(
        "DEF-160: a criterion-less item's custom_attributes are rendered, key and value",
        stage("SL-034", "WBS-25.1",
              "Criterion-less by design. Requirement NFR-054; the reason is recorded in DW-090.",
              custom_attributes=json.dumps({"DONE_CLAIMED_TEST": record})),
        "SL-034", 0, "item(s) at Review", html_has="DONE_CLAIMED_TEST</h4><p>" + record))

    # An attribute whose stored value is the empty string must still show its key. block() omits falsy
    # text, which once dropped such a key without trace, and dropped the whole section if it was the
    # only one, so the page read exactly like an item with no attributes.
    results.append(case(
        "DEF-160: an attribute with an empty-string value still shows its key",
        stage("SL-034", "WBS-25.1",
              "Criterion-less by design. Requirement NFR-054; the reason is recorded in DW-090.",
              custom_attributes=json.dumps({"EMPTY_VALUE_KEY": ""})),
        "SL-034", 0, "item(s) at Review", html_has="EMPTY_VALUE_KEY</h4>"))

    # CALIBRATION: same item with no attributes. The heading must be absent, which proves the case
    # above found the rendered block and not some fixed text that is always on the page.
    results.append(case(
        "DEF-160 CALIBRATION: no custom_attributes, no attributes heading",
        stage("SL-034", "WBS-25.1",
              "Criterion-less by design. Requirement NFR-054; the reason is recorded in DW-090.",
              custom_attributes=None),
        "SL-034", 0, "item(s) at Review", html_lacks="own attributes"))

    # ---- WBS-40.23 / DEC-177: --scan ---------------------------------------------------------------
    # Controlled input: every row gets a title naming a real criterion, so the live register's own
    # findings cannot leak into the case. Then exactly one subject item has its criterion removed.
    # Which item is the subject is read from the export, not hard-coded (DEF-120's lesson).
    def scan_input(strip=None, keep_subject=None, exempt=None):
        """exempt: the NO_CRITERION_BY_DESIGN value written onto the stripped item (DEC-180 i1)."""
        def mutate(exports):
            with open(os.path.join(exports, "acceptance_criteria.json"), encoding="utf-8") as fh:
                acs = json.load(fh)["result"]["rows"]
            some_ac = acs[0]["id"]
            bound = {a["slice_id"] for a in acs if a.get("slice_id")}

            def f(r):
                r["title"] = "Named criterion %s." % some_ac
                if r.get("id") == strip:
                    r["title"] = "This title names no criterion."
                    if exempt is not None:
                        r["custom_attributes"] = json.dumps({"NO_CRITERION_BY_DESIGN": exempt})
                is_subject = r.get("lifecycle_status") in ("Review", "Implemented") and r.get("slice_id") in bound
                if keep_subject is not None and is_subject and r.get("id") not in keep_subject:
                    r["lifecycle_status"] = "Approved"
                return r
            rewrite(os.path.join(exports, "wbs_items.json"), f)
        return mutate

    def export_rows(family):
        with open(os.path.join(ROOT, "tamheed-package", "exports", family + ".json"), encoding="utf-8") as fh:
            return json.load(fh)["result"]["rows"]

    def subject_ids():
        """(leaves, parents) of the scan subject. A parent is any row another row names as parent_id."""
        bound = {a["slice_id"] for a in export_rows("acceptance_criteria") if a.get("slice_id")}
        rows = export_rows("wbs_items")
        has_kids = {r["parent_id"] for r in rows if r.get("parent_id")}
        subject = sorted(r["id"] for r in rows
                         if r.get("lifecycle_status") in ("Review", "Implemented") and r.get("slice_id") in bound)
        return [i for i in subject if i not in has_kids], [i for i in subject if i in has_kids]

    leaves, parents = subject_ids()
    target = leaves[0]
    real_dec = export_rows("decisions")[0]["id"]
    real_dw = export_rows("deferred_work")[0]["id"]

    # CONTROL: every subject names a criterion, so the scan must pass. Without this, the finding case
    # below could pass because the scan fails on everything.
    results.append(case(
        "WBS-40.23 CONTROL: --scan exits 0 when every shipped item names a criterion",
        scan_input(), "--scan", 0, "0 finding(s)"))

    # The target is a LEAF: under DEC-180 a parent is excluded, so stripping one proves nothing here.
    results.append(case(
        "WBS-40.23: --scan reports an Implemented/Review leaf whose title names no AC- and exits 1",
        scan_input(strip=target), "--scan", 1, "FINDING  %s " % target))

    # CALIBRATION of the floor: keep three subject items, so the scan has almost nothing to look at.
    # It must refuse rather than report a clean result over a near-empty set.
    results.append(case(
        "WBS-40.23 CALIBRATION: --scan refuses a subject set below its floor",
        scan_input(keep_subject=set(leaves[:3])), "--scan", 2, "below the floor"))

    # ---- DEC-180 i1: roll-up parents are excluded by structure, and say so ------------------------
    # Paired with the leaf case above: same strip, different item, opposite verdict. The count line
    # makes the exclusion visible, so a clean 0 can never hide what it left out.
    results.append(case(
        "DEC-180: a roll-up parent naming no AC- is excluded, not a finding, and the exclusion is counted",
        scan_input(strip=parents[0]), "--scan", 0, "%d roll-up parent(s) excluded" % len(parents)))

    # ---- DEC-180 i1: NO_CRITERION_BY_DESIGN exempts only when its cite resolves -------------------
    results.append(case(
        "DEC-180: an exemption citing a DEC- in the register exempts the leaf",
        scan_input(strip=target, exempt="%s d1 - by design" % real_dec), "--scan", 0, "EXEMPT   %s " % target))

    results.append(case(
        "DEC-180: an exemption citing a DW- in the register exempts the leaf",
        scan_input(strip=target, exempt="%s - by design" % real_dw), "--scan", 0, "EXEMPT   %s " % target))

    # CALIBRATION: identical except the cite does not resolve. A flag anyone can write is a bypass;
    # if this ever exits 0, the exemption has stopped checking what it cites.
    results.append(case(
        "DEC-180 CALIBRATION: an exemption citing nothing that resolves still fails",
        scan_input(strip=target, exempt="DEC-99999 d1 - by design"), "--scan", 1, "FINDING  %s " % target))

    shutil.rmtree(SCRATCH, ignore_errors=True)
    print()
    print("%d/%d passed" % (sum(results), len(results)))
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
