---
name: p8-actions-plan
description: "P8 Actions module — 4-slice plan, GO'd forks, and current progress (P8a done)."
metadata: 
  node_type: memory
  type: project
  originSessionId: 5d6ffc09-c171-48a2-9586-476c992f4111
---

**P8 = Actions module**, plan-first + GO-gated, sliced like P7. Design SoT = `ACMP Lists & Registers.dc.html` `isActions` register + detail. Canon: docs/11 (aggregate), docs/12 §7 (lifecycle), W13/W14/W22, docs/10 rows 14–15.

**4 slices:** P8a Actions backend · P8b register+detail UI · P8c Hangfire reminders/escalation + Admin job dashboard · P8d AC-029 decision-link gate.

**P8a — DONE, PR #63 GREEN on main (not self-merged; awaiting operator GO to merge).** New Actions bounded context mirroring Decisions. Aggregate is **`ActionItem`** (NOT `Action` — avoids the `System.Action` BCL clash); DbSet/table/key stay Actions/`ACT-YYYY-###`. 6-state lifecycle Open→InProgress↔Blocked→Completed→Verified + Cancelled; **overdue derived** never stored. **SoD-1 (AC-012/013)** enforced in `VerifyActionHandler` (verifier≠owner≠completer → audit denial + `ForbiddenAccessException` 403; the P4 `SegregationOfDuties.CanVerifyAction` predicate already existed). People fields = Keycloak sub strings + name snapshots (direct SoD compare). 709 tests green, coverage 99.62%. AC-012/013 stay **Partial** (Met→P17 live real-stack, matching Decisions AC-027/028 discipline).

**GO'd forks (locked):** (1) AC-029 gate applies ONLY to follow-up-bearing outcomes (Approved/ConditionallyApproved/EnhancementsRequired/DesignChangesRequired/ResearchRequired) — not Rejected/Deferred/etc. (2) Hangfire Admin dashboard **wired in P8c** (Admin-only), flips AC-056. (3) Action detail = **routed `/actions/:key`** (blessed deviation from the design's drawer, so notifications deep-link).

**Carried into later slices:** Hangfire is NOT wired anywhere yet — P8c stands it up fresh (app-owned, ACMP's SQL, ADR-0014; injectable service Hangfire only cron-triggers; thresholds→appsettings; storage bootstraps its own tables, not EF). **ASM (P8d):** AC-029 proxies the downstream link via `ActionItem.SourceId` `(SourceType,SourceId)` query until the P10 typed-edge Traceability module (ADR-0008). `SourceId` is trusted input (no cross-module existence check). Member-AiO create/verify untested (no AC) → verify in P8b UI. YAGNI-deferred: Assignees[], ProgressUpdate history table, file attachments.

See [[p7-minutes-decisions-plan]]. Next = **P8b** (register UI).
