# Progress sync — record what actually happened

Paste this after implementation work on `tamheed-package` to bring the package up to date.

---

Sync the `tamheed-package` Tamheed package with the work just completed:

1. `package_open("tamheed-package")` if not already open.
2. For each meaningful unit of work: `progress_update([{"entry": "<what happened>",
   "phase_id": "<PH-x>", "slice_id": "<SL-x>"}])` — concrete entries, not summaries.
3. For each commit/PR that satisfies package entities:
   `work_bind(ref="<commit-or-PR>", entity_ids=["FR-x", "AC-y", "SL-z"], note="...")`.
4. For each acceptance criterion now verifiable:
   `audit_record([{"ac_id": "AC-x", "verdict": "Met|Partial|Not-met",
   "evidence": "tests/test_x.py::test_y; commit <sha>"}])` — an evidenced verdict
   beats a narrated one; never record Met without pointing at the proof.
5. If scope changed (something deferred, cancelled, expanded): write the typed
   `scope-change` row FIRST, then the mutation it authorizes.
6. `gate_run()` — report the verdict delta, then `package_close()`.
