# Replan deferred work — activate what fired

Paste this when deferred-work triggers may have fired on `tamheed-package` (semi-auto: the
new scope stops for your approval).

---

Review and activate deferred work in the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")` if not already open.
2. `entity_query("deferred-work")` — for each Open/Scheduled row, judge its
   `activation_trigger` against current state (the triggers are prose — that judgment
   is the point of this prompt). Report: fired / not fired / unclear, with reasons.
3. For each item to activate, IN THIS ORDER:
   - the `scope-change` row FIRST (`decision_ref` naming the deciding `DEC-`/`ADR-`;
     upsert a Proposed decision if none exists);
   - flip the `DW-` status to Activated (full-row upsert);
   - upsert the `wbs-item`/`slice` rows the work becomes, with `phase_id`/`slice_id`;
   - wire the trace edges (`implements`/`relates_to`) so G-TRACE sees the linkage.
4. `gate_run()` — the new scope must not break the gates.
5. **STOP for operator approval of the activated scope before executing any of it.**
