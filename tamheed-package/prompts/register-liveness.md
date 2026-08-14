# Register liveness — work the amber list

Paste this periodically during execution of `tamheed-package` (not only at close): the
readiness engine NAGS about decaying registers — this is the playbook that answers it.
Registers stay alive because someone works the ambers, in one sitting, on a cadence.

---

Sweep the advisory findings of the `tamheed-package` Tamheed package. Advisories never block,
which is exactly why they rot — resolve each finding or carry it DELIBERATELY; silence
is the only wrong answer.

1. `package_open("tamheed-package")` if not already open, then `readiness_check("package")`.
   Work every advisory rule whose status is `fail` (an `indeterminate` rule means the
   keyed column is empty everywhere — populating it IS the fix). Do not touch blocking
   failures here — they belong to the close-out prompts.
2. **Ambiguity markers** (`clarifications-open`): each entry names a field carrying
   `[NEEDS-CLARIFICATION: OQ-NNN]`. Resolve the OQ if you now can, then remove the
   marker from the field (full-row upsert); still genuinely open → leave both.
3. **Open questions** (`open-questions-overdue`, then `open-questions-resolved`):
   overdue ones first — answer it (set `resolution` + `resolved_by`), re-date it
   (new `due_by`, full row), or escalate what the silence is costing into a `risk`
   row. A question with no `owner`/`due_by` gets both.
4. **Assumptions** (`assumptions-current`): past `validation_date` — re-validate
   (confirm it still holds; set a fresh `validation_date`) or, if it no longer holds,
   record the fallout: a `risk` row (or a `scope-change` if the plan must move) and
   the assumption to `Rejected`.
5. **Risks** (`risk-liveness`): every open high-probability/high-impact risk gets an
   `owner` and a `response_strategy` (avoid|mitigate|transfer|accept) — no owner means
   nobody monitors.
6. **Hypotheses** (`hypotheses-measurable`): past Draft without `metric` + `threshold`
   — set both NOW (the number is decided BEFORE the experiment runs) or send the
   hypothesis back to Draft honestly.
7. **Decisions that look architectural** (`decisions-look-architectural`): apply the
   one-way-door test (hard to reverse? broad blast radius? re-debated?). If it passes,
   draft the `adr` row (context/decision/consequences/`confirmation`) and set
   `promoted_to` — **STOP for operator approval before the ADR leaves Proposed**.
   If it genuinely is a two-way door, note why in the decision's `rationale`
   (full-row upsert) so the nag has an answer on record.
8. **Unmerged scope changes** (`scope-changes-merged`): an Approved `SC-` whose deltas
   never landed — apply the row changes its `scope_adds`/`scope_modifies`/
   `scope_removes` edges name (via `entity_upsert`), then set the `SC-` to `Merged`.
9. **Unbound ACs** (`acs-slice-bound`): bind each to its slice (full-row upsert —
   NOTE: an Approved AC's content is immutable; if the binding itself is the change,
   supersede instead), or record the deliberate choice to verify at package scope
   only.
10. **Minor defects** (`defects-minor`): fix the quick ones now (the defect-triage
    flow: fix, evidence-chained `audit_record`, status flip, `work_bind`). For ones
    worth carrying, propose a waiver to the operator — **waivers are operator-only:
    you NEVER author a `WVR-` row without their words** — or convert to
    `deferred-work` with a trigger.
11. **Deferred work** (`deferred-work-reviewed`): read each open `DW-`'s activation
    trigger against current reality. Fired → say so and point the operator at the
    replan-deferred prompt (activation is a scope decision, not yours). Not fired →
    it is deliberately carried; nothing to write.
12. **Execution plans** (`execution-plans-approved`): plans still Draft/Proposed for
    active slices — finish them and **STOP for operator approval**.
13. **Unwired requirements** (`requirements-wired`): every listed requirement gets its
    real edges — `derives_from` the deciding DEC-/ADR-, `implements` from its slice/
    work item, `tests` from its test. `relates_to` only when nothing typed fits.
14. Close the sweep: `progress_update([{"entry": "liveness sweep: <per-family tally —
    resolved / carried / escalated / awaiting operator>", "event_type": "note",
    "actor": "agent:<session>"}])`, then `readiness_check("package")` again and report
    the advisory delta plus everything now awaiting operator words (promotions,
    waivers, plan approvals, activations).
