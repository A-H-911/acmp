---
name: p8-actions-plan
description: "P8 Actions module — 4-slice plan, GO'd forks, and current progress (P8a+P8b done & merged; next P8b2)."
metadata: 
  node_type: memory
  type: project
  originSessionId: 5d6ffc09-c171-48a2-9586-476c992f4111
---

**P8 = Actions module**, plan-first + GO-gated, sliced like P7. Design SoT = `ACMP Lists & Registers.dc.html` `isActions` register + detail. Canon: docs/11 (aggregate), docs/12 §7 (lifecycle), W13/W14/W22, docs/10 rows 14–15.

**4 slices:** P8a Actions backend · P8b register+detail UI · P8c Hangfire reminders/escalation + Admin job dashboard · P8d AC-029 decision-link gate.

**P8a — DONE & MERGED to main (squash `26dd245`, PR #63).** New Actions bounded context mirroring Decisions. Aggregate is **`ActionItem`** (NOT `Action` — avoids the `System.Action` BCL clash); DbSet/table/key stay Actions/`ACT-YYYY-###`. 6-state lifecycle Open→InProgress↔Blocked→Completed→Verified + Cancelled; **overdue derived** never stored. **SoD-1 (AC-012/013)** enforced in `VerifyActionHandler` (verifier≠owner≠completer → audit denial + `ForbiddenAccessException` 403; the P4 `SegregationOfDuties.CanVerifyAction` predicate already existed). People fields = Keycloak sub strings + name snapshots (direct SoD compare). 709 tests green, coverage 99.62%. AC-012/013 stay **Partial** (Met→P17 live real-stack, matching Decisions AC-027/028 discipline).

**GO'd forks (locked):** (1) AC-029 gate applies ONLY to follow-up-bearing outcomes (Approved/ConditionallyApproved/EnhancementsRequired/DesignChangesRequired/ResearchRequired) — not Rejected/Deferred/etc. (2) Hangfire Admin dashboard **wired in P8c** (Admin-only), flips AC-056. (3) Action detail = **routed `/actions/:key`** (blessed deviation from the design's drawer, so notifications deep-link).

**Carried into later slices:** Hangfire is NOT wired anywhere yet — P8c stands it up fresh (app-owned, ACMP's SQL, ADR-0014; injectable service Hangfire only cron-triggers; thresholds→appsettings; storage bootstraps its own tables, not EF). **ASM (P8d):** AC-029 proxies the downstream link via `ActionItem.SourceId` `(SourceType,SourceId)` query until the P10 typed-edge Traceability module (ADR-0008). `SourceId` is trusted input (no cross-module existence check). Member-AiO create/verify untested (no AC) → verify in P8b UI. YAGNI-deferred: Assignees[], ProgressUpdate history table, file attachments.

**P8b — DONE & MERGED to main (squash `d1b09a6`, PR #64).** Read-first split (operator GO): P8b = the `/actions` register + routed `/actions/:key` detail (READ-ONLY); writes → **P8b2**. Built to `ACMP Lists & Registers.dc.html` `isActions`. Files: `api/actions.ts` (useActionsRegister paged + server status/overdue filters + due/progress/status sorts; useAction by-key; **useActionsCounts** = two `pageSize=1` count queries for the GLOBAL filter-independent header — the paged list can't carry those facets), `features/actions/{ActionsRegister,ActionPage,actionMeta,actions.css}` + i18n `actions.*` EN+AR (every status incl. Cancelled by hand). **Server-side filtering** (operator GO, not the design's client toggle — correct under paging). Only due/progress/status sortable (server-backed); Owner filter disabled stub; New-action + Saved-view disabled stubs. Visual-verified via throwaway dev-stub harness (register+detail × EN-light + AR-RTL-dark) — matches the ref, no drift. 470 FE tests green, new files 100% lines, axe AA, i18n parity 764.

**Next = P8b2** (create form + owner lifecycle transitions start/block/progress/complete + independent **verify** UI — also exercises the untested Member create/verify SoD-1 path). Then P8c (Hangfire) · P8d (AC-029 gate). See [[p7-minutes-decisions-plan]].
