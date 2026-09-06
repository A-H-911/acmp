# Progress sync — record what actually happened

Paste this after implementation work on `tamheed-package` to bring the package up to date.

---

Sync the `tamheed-package` Tamheed package with the work just completed:

1. `package_open("tamheed-package")` if not already open.
2. For each meaningful unit of work: `progress_update([{"entry": "<what happened>",
   "phase_id": "<PH-x>", "slice_id": "<SL-x>", "event_type": "work-done",
   "subject_id": "<WBS-x/AC-x>", "actor": "agent:<session>"}])` — concrete entries,
   not summaries. A `wbs-item` you believe finished: full-row upsert to
   `lifecycle_status: "Review"` (done-claimed; `Implemented` means verified, and
   readiness counts Review as open).
3. For each commit/PR that satisfies package entities:
   `work_bind(ref="<commit-or-PR>", entity_ids=["FR-x", "AC-y", "SL-z"], note="...")`.
4. For each acceptance criterion now verifiable:
   `audit_record([{"ac_id": "AC-x", "verdict": "Met|Partial|Not-met",
   "evidence": "tests/test_x.py::test_y; commit <sha>", "verified_by":
   "human|agent|ci", "verification_method": "auto-test|manual|inspection",
   "against_commit": "<sha>"}])` — an evidenced verdict
   beats a narrated one; never record Met without pointing at the proof.
5. If scope changed (something deferred, cancelled, expanded): write the typed
   `scope-change` row FIRST (Proposed, with `scope_adds`/`scope_modifies`/
   `scope_removes` delta edges to the affected plan rows — `amends` when it carves
   an exception out of a `DEC-`/`ADR-` ruling: DEC- full-row upsert, ADR-
   supersede); only after operator approval apply the mutation it authorizes,
   RE-READ the rows the edges name, and set the `SC-` Merged LAST.
6. Did this work teach something durable — a mistake whose fix future sessions must
   know, or a practice worth repeating? Record it NOW: a `lesson` row (`LL-`, born
   Proposed; kind improve|sustain, statement + impact_if_ignored) + a
   `learned_from` edge to its source. The operator confirms later; only Approved
   lessons bind future sessions.
7. Any requirement created during this work gets its trace edges (`derives_from` /
   `implements` / `tests`) in the SAME sync — `work_bind` stamps commits, it does
   NOT wire traceability. Edge endpoints must respect the endpoint rules — G-REL
   now FAILS `gate_run` on violating edges; `relates_to` is the escape hatch. Edges
   are keyed (from, to, relation): a wrong one is RETIRED (`retire: true` on the
   trace-edge item — journaled) and the correct one written in the same batch;
   never leave the old edge beside its replacement.
8. `gate_run()` — report the verdict delta (including `requirements_unwired`),
   then `package_close()`.
