# Integrity check — is the package trustworthy right now?

Paste this to audit the `tamheed-package` package without changing anything.

---

Run a read-only integrity check on the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")`, then `gate_run()` — every Critical gate must pass;
   treat a G-TRACE "passed vacuously" warning as a finding, not a pass.
2. Spot-check counts: `entity_query("requirement", limit=1)` and read `total`;
   compare `total` per family against expectations from the roadmap/charter.
3. `trace_query` a sample of MVP requirements — each should reach a decision,
   a work item, and a test; note any that only reach one bucket.
4. Audit honesty: from `gate_run`'s audit_evidence split, list every NARRATED
   verdict (no evidence) — each is the graded party grading itself.
5. Check staleness: compare the freshness line in a fresh `export_html()` against
   `git log -1` — if git is ahead of the package's recorded activity, the package
   is stale; recommend a progress sync.
6. Report: gate verdict, count anomalies, trace gaps, narrated verdicts, staleness
   — then `package_close()`. Change NOTHING in this run.
