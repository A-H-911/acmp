---
artifact: acceptance-audit
status: active
version: v1
updated: 2026-06-30
---

# ACMP Acceptance Audit

Every `AC-###` from `docs/40-acceptance-criteria.md` → verdict. Keystone gate **G-PROGRESS**.
A requirement is not "done" until its AC is `Met` and traces to ≥1 test (gate **G-TRACE**).

**Verdicts:** `Met` · `Partial` · `Not-met` · `Pending` (not yet implemented).

> P12-PR1 update (2026-07-05): Reporting thin registers (backend reads, branch `feat/P12-pr1-report-reads`) —
> **NO AC flips.** PR1 is backend enablement for the P12 role dashboards: `GET /api/decisions` + `GET /api/votes`
> registers, `GET /api/minutes` InReview approval queue, and a `Topic.TimesDeferred` counter on the backlog
> projection (migration `Topics_AddTimesDeferred`). **AC-064/065/066 (FR-135/136/137) stay Pending** — they flip to
> Met in **PR2** when the dashboards render this data end-to-end through the UI (G-TRACE: an AC is Met only when
> demonstrable, not when its data source exists). Right-sizing recorded in **ADR-0022** (no columnstore read-model
> layer, no chart library; resolves OQ-022). +6 backend tests (decisions/votes/minutes registers, TimesDeferred);
> Application 679 + Domain 188 green; format clean. See progress-log "P12-PR1".
>
> P11 audit-remediation update (2026-07-05): adversarial-audit fixes across the Governance surfaces (branch
> `fix/p11-audit-remediation`), **NO AC flips** (Governance is PH-2, AC-less). Design-fidelity: superseded-body
> dimming (+ removed a false `is-retired` comment), Superseded chip tone `warn`→`neutral`, `shieldPlus`/`book`/
> `filterLines`/`checklist` glyphs, shared Invariant H1, `nav`+`aria-current` tab bar, Convert-to-ADR primary CTA +
> preview box, supersede left-arrow/badge, numeric metrics + sticky aside, "Showing N", dead-code/comment cleanup
> (EN/AR parity 1310/1310). Backend robustness: **BE1 `Governance_AdrSourceDecisionUnique`** filtered unique index
> (one ADR per decision, DB backstop), re-promote **edge-heal**, corrected ADR-0021 "in-transaction" wording,
> supersede successor `AdrApproved`/`InvariantActivated` audit. The general **audit-immutability AC-017/018** still
> hold (every governance transition audited; approved ADRs / active invariants frozen). Gates: BE 1059 + cov 99.80%
> + format + ArchUnit 40/40; FE 772 + cov + parity + build. See progress-log "P11 audit remediation".
>
> P11b update (2026-07-04): ADR **UI** — the `/adrs` register + `/adrs/:key` MADR-lite detail + create dialog
> (branch `feat/P11b-adr-ui`), **FE-only, NO AC flips** (Governance is PH-2, AC-less). Advances the UI half of
> **FR-099/100/103** (author MADR-lite, register, supersede back-link display) to done and **FR-104** (Export
> `.md`, now shipped client-side). FR-101 lifecycle transitions are surfaced only as far as create→Proposed
> (approve/supersede/deprecate BUTTONS are a later slice — read-only detail, mirrors Risks P10b); FR-102 FTS
> still deferred to Search. Create dialog lands ADRs in **Proposed** (design default) not inert Draft
> (create→`/propose`, both `Adr.Create`). Reconciliations flagged in the progress log: 5 states + "Approved"
> label; read-only detail; register supersede = marker-only (lean summary); Invariants tab disabled (P11d),
> Violations tab omitted (operator DEFER); Category filter disabled. 735 vitest green (+33), per-file line cov
> 100% on 5 new FE files, tsc/oxlint/build/i18n-parity all clean. Live `.dc.html` screenshot-VR pending a
> running-stack pass. **Next = P11c (Invariant backend).**

> P11e update (2026-07-04): Decision→ADR promotion (FR-068) — full-stack, branch `feat/P11e-decision-to-adr`, **NO
> AC flips** (Governance PH-2/AC-less; traces to **FR-068**, now done). The Chairman promotes an **Issued** decision
> to a new **Draft** ADR, pre-filled + bidirectionally linked. Backend: two cross-module Shared.Contracts seams —
> **IDecisionReader** (read, pre-fill) + **ITraceabilityWriter** (first WRITE seam — records a system `RecordedAs`
> Decision→ADR edge, idempotent, audited); **PromoteDecisionToAdr** command guards **Issued-only** + **one-ADR-per-
> decision** (both 409); new **Adr.Promote policy = Chairman ONLY** (docs/10 matrix row `"ADDDDDDD"`); endpoint POST
> /api/adrs/from-decision. Bidirectional link = SourceDecisionId (ADR→Decision) + RecordedAs edge (Decision→ADR).
> FE: wired the pre-existing disabled "Convert to ADR" stub → Chairman-gated button + confirm dialog (design-
> referenced, `ACMP Decision, Voting & ADR.dc.html` convertOpen) → navigate to the new ADR; 409 surfaces inline.
> Pre-fill map Context←Rationale/Decision←Statement/Drivers←Alternatives (flagged). Gates green: backend format
> clean + all tests pass (5 promote handler + 4 endpoint + 3 writer + PermissionMatrix row) + coverage 99.79%; FE
> i18n parity (1311 keys) + 770 vitest + per-file 100% on new/changed files + tsc/vite/oxlint clean. **ADR-0021
> recorded** (cross-module write-seam precedent, extends ADR-0001). **Live `.dc.html` screenshot-VR DONE → PASS**
> (real Docker stack; issued Decision detail + confirm dialog + promoted Draft ADR with the live RecordedAs
> bidirectional link, EN-light + AR-dark, pixel-faithful; RTL mirrors) — **FR-068 verified end-to-end.** **★ P11
> COMPLETE ★** (ADRs+Invariants+promotion). See progress-log "P11e".
>
> P11d update (2026-07-04): Invariant UI — invariants tab + `/invariants` register + `/invariants/:key` detail +
> Create-Invariant dialog (branch `feat/P11d-invariant-ui`), **FE + one backend hotfix, NO AC flips.** Governance is PH-2/AC-less;
> traces to **FR-106** (create Category/Scope/Statement/Rationale/Owner, born Draft) + FR-107 read/register. Detail
> is **read-only** (no lifecycle buttons, no markdown export) — all propose/activate/retire/supersede UI (ADR +
> Invariant) deferred to one later governance-lifecycle slice. Create dialog lands invariants in **Draft** with
> **no create→propose chain** (design form has no Status field, born-Draft per operator). Reconciliations flagged
> (progress log): no-reference detail (guardrail #14, cued by the register's detail-drawer facts, Viol. omitted);
> register drops the Viol. column (5 cols); Rationale + Owner REQUIRED though design marks optional (backend
> validator, Option A); ExceptionsPolicy not collected (sent null, shown-if-present); Category filter disabled (no
> server filter param); OQ-036 open. **OQ-048 doc-fix folded in**: struck the `+ kind (…)` clause from docs/12 §9's
> Invariant-create guard row (docs/22 §A = SSoT). Gates green: **765 vitest** (+30), per-file line cov 100% on the
> new FE files (global 99.21%), tsc/oxlint/vite-build/i18n-parity all clean. **Live `.dc.html` screenshot-VR DONE →
> PASS** (real fresh Docker stack; register+create+detail, EN-light+AR-dark, pixel-faithful; RTL mirrors, dark
> clean) — **FR-106 create/read verified live.** During VR, found + fixed a **latent P11a backend defect**:
> `GovernanceDbContext` was missing from `MigrationRunner`, so the Governance schema was never created on the
> deployed stack (ADRs non-functional in deployment since P11a) — one-line fix (operator GO), repairs ADR +
> Invariant together (dotnet format clean; backend `dotnet test`/coverage to run pre-push). **Next = P11e
> (Decision→ADR promotion, FR-068).** See progress-log "P11d".
>
> P11c update (2026-07-04): Invariant backend — the `Invariant` aggregate in the **Governance** module (branch
> `feat/P11c-invariant-backend`), **BACKEND only, NO AC flips.** Governance is PH-2 scope and AC-less; this slice
> traces to **FR-106/107** (create Category/Scope/Statement/Rationale/Owner + lifecycle Draft→Proposed→Active→
> (Retired|Superseded) + supersede back-link/immutability + register — all done). **FR-108/109** (violation
> recording + list) stay **deferred** (operator, model contested — violations = Risk/Action/AuditEvent per
> docs/22 §A.5, not a sub-entity). FTS deferred to Search. The general **audit-immutability AC-017/018** apply —
> every invariant transition emits an `IAuditSink` event and Active invariants are frozen (supersede-not-edit).
> **Operator decision (2026-07-04):** the `Invariant.Kind` enum was **dropped** (OQ-048) — the design form +
> FR-106 omit it and docs/22 §A (concept SSoT, README §G) makes Invariant a sibling of Principle/Standard/
> Policy/Constraint, not their parent; docs/12 §9 owes a correction. **SoD soft** on activate (author may
> approve, recorded `AuthorApprovedSelf` off the server-derived creator `CreatedBy`, not the client Owner field). Design↔behaviour flags (progress log): all-fields-at-Draft vs docs/12
> §9's split guards; "stream owners on Activate" → committee (scope is a class, no stream link); Category enum =
> OQ-036 default (adds Compliance). Gates green: **1038 BE tests** (+31), per-file cov ≥95% (global 99.79%),
> format clean, migration `Governance_InvariantInit`. No FE change, no AC verdict change. See progress-log "P11c".
>
> P11a update (2026-07-04): ADR backend — the new **Governance** module + the `Adr` aggregate (branch
> `feat/P11a-adr-backend`), **BACKEND only, NO AC flips.** Governance is PH-2 scope and AC-less (the docs/40
> ACs are PH-1 MVP); it traces to **FR-099/100/101/102/104** (create MADR-lite + lifecycle Draft→Proposed→
> Approved→(Superseded|Deprecated) + supersede back-link/immutability + register — all done; FTS FR-102 and
> Markdown-export FR-104 deferred to P11b/Search). The general **audit-immutability AC-017/018** apply — every
> ADR transition emits an `IAuditSink` event and Approved ADRs are frozen (supersede-not-edit, mirrors
> Decisions AC-027/028). **Operator decisions (2026-07-04):** violations FR-108/109 **deferred** (model
> contested → OQ owed); **SoD soft** on approve (author may approve, recorded `AuthorApprovedSelf`); promotion
> FR-068 **build in P11e**. Design↔behaviour flag: the `.dc.html` shows 3 ADR states + "accepted" label vs the
> canonical 5 states + "Approved" (design update owed, guardrail #14). Gates green: **1007 BE tests** (+24),
> per-file cov ≥95% (global 99.76%), format clean, ArchUnit 40/40, migration `Governance_Init`. No FE change,
> no AC verdict change. See progress-log "P11a".
>
> P10-graph-dialogs update (2026-07-04): operator live-review fixes (branch `feat/P10-graph-dialogs`) —
> **behaviour-consistency + fidelity, NO AC flips.** Impact-graph tier direction corrected (kind-aware:
> `DependsOn`/`BlockedBy` reverse to match the backend `From --Kind--> To` semantics) + Option-2 subtree-side
> inheritance so a branch never crosses the focus column (**operator-approved deviation from the reference
> `buildTiers`, guardrail #14 — design `.dc.html` update owed**). Aside `buildTypeGroups` direction made
> consistent with `buildPanelRows` + the graph. Create-dialog fidelity: 38px accent header tile + paired-field
> top-alignment (`.field + .field` margin reset). **FR-096** (impact graph) stays **Met** — now correct on real
> data. Backend 975 green (+2 composer tests), FE 702 green. See progress-log "P10-graph-dialogs".
>
> P10-review update (2026-07-04): remediation of the adversarial P10 audit (branch `feat/P10-review`) —
> **fidelity + i18n + doc-integrity hygiene only, NO AC flips.** Fixed 7 MAJOR (Risks filter order; Deps
> blocked-toggle colour/height + relation-arrow orientation; Traceability aside `openGraph` key + matched-node
> highlight border; **ADR-0019/0020 now indexed** in `adr/README.md`) + a MINOR cluster incl. the **Arabic-gender
> matrix impact-axis** defect (new masculine `reports.impactLevel.*`). Backend: decoupled the impact-graph BFS
> `partial` loop-halt (+1 regression test) and closed the Decisions ArchUnit forbidden-list gap. **AC-029 stays
> Met** (gate untouched), **AC-062/063 stay Partial**, **FR-095 stays Partial (Topic-scope)**, **AC-064/065/066
> stay Pending → P12**. Accepted/deferred (recorded, guardrail 11): the AiO dead-path (B4, fails closed →
> future authz slice), app-wide "Showing X of Y" + register copy, the reports KPI headline (R3 → P12), and the
> dep-detail Impact prose (D16). Gates green: 972 BE + 702 FE tests, format clean, per-file cov ≥95%, i18n 1172.
> See progress-log "P10-review".
>
> P10g update (2026-07-04): Risk & Dependency **reports** (branch `feat/P10g-risk-dep-reports`) — the LAST P10
> slice, **FRONTEND only**. Replaces the `/reports` placeholder with a focused analytics surface that REUSES
> the card renderers/tokens of `ACMP Dashboards & Reports.dc.html` (matrix / stat / bars); the full Reports IA
> (view-tabs, Export, filters, role dashboards, other-domain cards) is **deferred to P12 Reporting** — this page
> is a **no-reference composition** of the design's card system (guardrail #14), not a verbatim port. Six cards,
> every number composed **client-side** from three existing REST reads (risks + dependencies + topics; no
> backend): a **risk-exposure 3×3 count matrix** (coloured by severity `L×I`, active-scoped), risk + dependency
> **stat** tiles, **dependencies-by-relation** bars, and — per the operator's "Fuller" scope call (override of
> both reviewers' defer-lean) — the two **by-stream** cards (Risk by stream, Cross-stream/blocked deps by stream)
> that join each risk/dep to its linked **Topic's streams** (`includeClosed` topics fetch; multi-stream topic
> counts under each stream, distinct KPI ≠ bar sum; stream **code** shown, no localized name on the wire).
> **FR-138** (per-stream report of topics/decisions/actions/**risks**, *Should*/Ph2) → **Partial** (per-stream
> risk + dependency breakdowns stand up; no date-range filter / export yet). **FR-095** (cross-stream) advanced
> in the reporting surface, stays **Partial (Topic-scope, OQ-047)** — recorded as a scoped partial-advance
> (inherit-from-topic model adopted for reporting aggregation only; P10f's per-edge semantic unchanged).
> **FR-135/136/137** (role dashboards, **AC-064/065/066**) remain **Pending → P12** — this is the *Reports* view,
> not the role *Dashboard* tab. No Must-AC flips. Reconciliations flagged (guardrail #14): whole page is a
> no-reference composition; matrix merges High+Critical exposure bands into one danger tint (per the design's
> authored cells); by-stream shows raw stream code; reads are authenticated-only so Administrator won't 403.
> Gates (local): `tsc -b` clean, oxlint clean, **702 vitest green** (+18 new incl. the money-path by-stream
> reducer properties + axe), **per-file coverage 100% on both new files**, i18n EN/AR parity by hand (37
> `reports.*` keys both locales). **Live VR PASS** (`e2e/p10g-reports-vr.spec.ts`): real Keycloak login,
> secretary seeds risks reproducing the design's matrix, opens `/reports`, captured **EN-light + AR-dark** and
> screenshot-compared **pixel-faithful to `ACMP Dashboards & Reports.dc.html`** — the 3×3 matrix layout +
> severity-zone tinting (all 9 cells), stat tiles, bars and P12 note match; **RTL Probability axis mirrors,
> cells track the axis** (dark tokens + Arabic labels clean). See progress-log P10g entry.
>
> P10f PR2 update (2026-07-03): Impact-graph **frontend** (branch `feat/P10f-graph-ui`, **PR2 of 2**) — the bespoke
> in-app page at **`/traceability/:type/:key`** consuming the merged PR1 endpoint. One focus-centric page: a 320px
> group-by-TYPE **Relationships aside** (`buildTypeGroups` — dependency edges by KIND, relationship edges by far TYPE)
> + a **Dependency&impact** section with a Graph/List segmented control, depth 1–3, Blocked + Cross-stream highlight
> toggles, a depth-tiered **SVG** (pure `graphLayout.ts` geometry; RTL = `scaleX(-1)` on the edges-only layer + logical
> `inset-inline-start` nodes) and a genuine **list-tree fallback** (role=tree). **FR-096** (transitive impact graph) →
> **Met**: a **live real-stack VR pass** (`e2e/p10f-graph-vr.spec.ts` — real Keycloak login, secretary seeds a focus
> topic + a 3-tier subgraph via the self-describing edge APIs, opens the graph via the warm-path panel button) captured
> **EN-light + AR-dark + list** and screenshot-compared **pixel-faithful to `ACMP Traceability & Dependencies.dc.html`**:
> the RTL edge-flip (top visual risk) meets the logically-positioned nodes, tier columns / type chips / focus highlight /
> Blocked pill / group-by-type aside all render per the design. **FR-095** cross-stream toggle is now **wired** (per-edge
> `isCrossStream` is on the wire) but stays **Partial (Topic-scope)** — flagged in-UI honestly. Reconciliations flagged
> (guardrail #14, design→behavior): a11y `role="application"`→**roving-tabindex**; per-node lifecycle **status chip
> omitted** (ADR-0001, no far-node status on the wire) — graph nodes show a **type chip** instead, aside/list drop it;
> cross-stream badge shows the **stream code** (no Membership seam); type-aware breadcrumb root; partial + empty-graph
> are no-reference compositions. Entry point: the P10e detail-page panel gains an **"Open graph"** affordance (warm
> path, passes `{id,title}` via router state); the `/dependencies` register stub stays deferred (no single focus).
> Gates (local): `tsc -b` clean, oxlint clean, **684 vitest green** (+53 new), **per-file coverage 100% on the 5 new
> FE files** (global 99.13%), i18n EN/AR parity by hand (`trace.graph.*` + full 17-type `trace.type.*`), axe AA clean
> on graph + aside + list. No backend/ADR changes. **Next: operator merge GO + optional live VR sweep.**
>
> P10f update (2026-07-03): Impact-graph **backend** (branch `feat/P10f-graph-backend`, **PR1 of 2**) — the
> server-side transitive traversal endpoint + two cross-module read seams. **The operator overrode both reviewers'
> FE-only lean**: a backend subgraph endpoint (server-side BFS) + FR-095 built now. `GET
> /api/traceability/graph/{type}/{id}?depth=1..3` composes this module's `Relationship` edges with the Dependencies
> module's edges (via the new `Acmp.Shared` `IDependencyArtifactReader` port) at **read time** — NOT a cross-schema
> recursive CTE (ADR-0020 clarifies ADR-0019; a two-schema CTE breaks ADR-0001). All 36 ArchUnit boundary tests stay
> green (the seam DTOs are Shared-owned). **FR-096** (transitive impact graph) — the **data/endpoint is stood up +
> tested** (composer branch tests + a real Topic→Decision→Action HTTP walk); the user-facing graph is **PR2 (FE)**, so
> the end-to-end AC verdict lands there — **structurally proven, not yet Met**. **FR-095** (cross-stream) → **Partial
> (Topic-scope, OQ-047)**: `isCrossStream` is computed only for Topic↔Topic edges (disjoint `Topic.AffectedStreams`
> codes, via the new `ITopicStreamReader` port); non-Topic endpoints carry no stream — honestly *partial*, not "built"
> (the inherit-from-topic model stays OPEN). No AC verdict flips (backend contract slice; live real-stack leg → P17).
> Node cap `MaxNodes=60` honours OQ-018 with a `partial` flag. Gates: 972 tests green (+24 new), format clean,
> per-file coverage 100% on all 7 new files (global 99.75%). ADR-0020 + OQ-047 recorded. **Next: P10f PR2 (FE graph).**
> See progress-log P10f entry.

> P10e update (2026-07-03): Dependencies register + Traceability panels UI (branch
> `feat/P10e-deps-traceability-ui`) — **FRONTEND only**, consuming the merged `/api/dependencies` +
> `/api/traceability` contracts (no backend change). Ships the `/dependencies` register (From·Relation·To·
> Blocked·Status; Relation + Blocked-work filters; key/status server sorts; global link/blocked counts),
> a routed `/dependencies/:key` **edge** detail, and the shared **`TraceabilityPanel`** aside on
> Topic/Decision/Action/Risk detail — merging typed Relationship edges + governed Dependency edges at read
> time into Upstream/Downstream/Related groups with navigable links, plus Chair/Sec-gated create dialogs
> (dependency + generic relationship). **AC-062** (traceability panel: upstream + downstream typed relationships
> with type + target ID + title + navigable link) and **AC-063** (create a typed edge → both panels reflect it,
> audited) move **Pending → Partial**: the FE panel/dialogs are structurally proven (56 new vitest, axe-clean,
> per-file coverage ≥95%), but the **Met** flip waits on the live real-stack (Keycloak-PKCE + SQL) round-trip →
> **P17** per G-TRACE, matching every prior FE slice. **FR-098** (topic-detail inbound/outbound deps) is served by
> the panel on Topic detail. **FR-095** (cross-stream) stays deferred — the Cross-stream column/filter is omitted
> honestly (not modelled on the wire; its cross-module stream derivation is a later slice). FR-096/097 (impact
> graph) = P10f. Reconciliations flagged (ASM-016 / guardrail #14): no far-artifact status chip (ADR-0001; dep
> "Blocked" pill instead), direction axis is a curated FE heuristic for the 16 relTypes, generic-relationship
> dialog is a no-reference composition, only 3 of 16 artifact types are pickable in create (Topic/Action/Risk).
> **Visual-verify DONE** — drove the live stack (real Keycloak PKCE + SQL) and screenshot-compared the three
> reference-backed surfaces (register, edge detail, create dialog) vs the `.dc.html` in EN-light + AR-RTL-dark; all
> match, RTL mirrored + dark tokens clean, Cross-stream correctly absent. (This is a visual-fidelity pass, NOT the
> AC-062/063 Met evidence — those still need the full create-via-UI → both-panels-reflect round-trip, which is P17.)
> FE gates green: tsc + oxlint + vite build clean, 638 vitest green, i18n parity (1090 keys). See progress-log
> P10e entry.
>
> P10d update (2026-07-03): Dependencies module backend (branch `feat/P10d-dependencies-backend`) — the
> first-class governed `Dependency` edge (`DPN-YYYY-###`) in a new `Dependencies` module (schema `dependencies`,
> migration `Dependencies_Init`), with create/resolve/remove lifecycle (`Open→Resolved|Removed`, RowVersion,
> hash-chained audit on every state change), a self-loop-guarded typed edge with endpoint snapshots (no
> cross-module FK), the `/api/dependencies` register + by-key detail + **by-artifact panel query** (the read-time
> composition seam for P10e), and RBAC via the pre-existing `Policies.DependencyCreate`. **No AC verdicts flip**
> (P10d is backend; the lifecycle is domain + handler + HTTP-pipeline proven, but the live real-stack Keycloak-PKCE
> + SQL leg → **P17** per G-TRACE). It stands up the data for **FR-094** (create DPN edge), **FR-095** (cross-stream
> — the derivation is P10e read-time work), and **FR-098** (topic-detail deps list — the by-artifact query serves
> it in P10e). FR-096/097 (transitive impact + Tarseem graph) = Phase 2 → P10f. **AC-062/063 stay Pending → P10e**
> (they assert on the FE traceability/deps panel display). **OQ-046 resolved** (read-time composition, no mirror
> edge; unification rejected — ASM-016). Backend 950 tests green (Domain 168 / App 608 / Api 121 / Arch 36 /
> Integration 17); per-file coverage ≥95% (global 99.75%, 0 files <95%); `dotnet format` clean. See progress-log
> P10d entry.
>
> P10b update (2026-07-03): Risks register + detail UI (branch `feat/P10b-risks-ui`) — FRONTEND only,
> composed to `ACMP Lists & Registers.dc.html` (`risks` 8-col table + drill-in) and the `risk` create form,
> consuming the merged `/api/risks` contract (no backend change). Ships the `/risks` register (status +
> exposure filters, key/exposure/status server sorts, 3×3 heat mini-grid, global count + critical badge),
> a routed `/risks/:key` detail (facts + LARGE exposure matrix + legend + prose Mitigation-plan card), and a
> "New risk" create dialog (Title/Likelihood/Impact/Owner/Linked-topic/Mitigation-plan → POST /api/risks).
> **No AC verdicts flip** — P10b is FE composition; the live real-stack (Keycloak-PKCE + SQL) leg → **P17**
> per G-TRACE. The detail's Related/Traceability panel is honest-partial: it shows the linked subject key
> display-only; typed edges + the impact graph (**AC-062/063**) remain **Pending → P10c/e**. FE gates green:
> tsc + oxlint clean, 582 vitest (37 new, axe-clean structure/ARIA), i18n parity (991 keys), per-file line
> coverage 100% on all 5 new files. Live screenshot-compare against the `.dc.html` (register heat grid +
> detail matrix, RTL + dark) is the one remaining manual check — pending a running-stack pass. See
> progress-log P10b entry.
>
> P10a update (2026-07-03): Risks module backend (branch `feat/P10a-risks-backend`) — the `Risk` aggregate +
> owned `Mitigation` + full W15 lifecycle (raise/mitigate/close/accept/escalate), derived Severity/Exposure,
> `RSK-YYYY-###` keys, migration `Risks_Init`, `/api/risks` endpoints, hash-chained audit on every state +
> mitigation change, escalation fan-out to Secretary+Chairman (BL-135), and a new dedicated **`Risk.Accept`**
> policy (Chairman/Secretary, no owner-AiO). **No AC verdicts flip** (P10a is backend; the risk lifecycle is
> domain + handler + HTTP-pipeline proven, but the live real-stack Keycloak-PKCE + SQL leg → **P17** per
> G-TRACE). It stands up the data for **AC-066** (chairman escalated-risks widget → P10g dashboards) and
> **AC-053** (risk-escalation notification). Backend 849 tests green (incl. the Risk.Accept permission-matrix
> cell + the new module-boundary fact); per-file coverage ≥95% (global 99.72%); `dotnet format` clean. See
> progress-log P10a entry.
>
> P9-review remediation (2026-07-02): the F-1…F-28 audit burn-down (branch `feat/p9-review-remediation`).
> **BL-066 — the durable, immutable, hash-chained AuditEvent store — now ships** behind `IAuditSink`
> (`SqlAuditSink` + `AuditDbContext` schema `audit` + `AuditChainVerifier`, migration `Audit_Init`), so the
> audit non-negotiable is no longer log-only. Verdict deltas (conservative, G-TRACE):
> **AC-019 Pending → Met** (hash-chain + integrity check that reports the first broken link / entry —
> unit-proven: valid chain, broken-link, content-tamper, genesis). **AC-018 → Partial** (append-only is
> enforced at the app layer — no mutators, no delete path — plus a UNIQUE `PreviousHash` non-forking index;
> a DB-level UPDATE/DELETE-blocking trigger for out-of-app/DBA writes is P16 hardening). **AC-017 → Partial**
> (entry carries eventType/subject/payload-JSON/UTC/hash on every state change; strict before/after JSON +
> correlation-id remain). **AC-020 stays Pending** (no Auditor search UI yet). Also: the stale rollup below
> (was "66 ACs · through P5b", F-8) is superseded — regenerate before the next phase gate. FE fidelity
> F-2…F-28 are design-parity fixes (no AC verdict change); see progress-log P9-review entry.
>
> P7a update (2026-07-01): Decisions module backend (record / issue / supersede). **Partial** (domain +
> application + API proven by tests; live HTTP/UI confirmation → P7b/P17 per G-TRACE): **AC-027** (issued
> decision immutable — no-mutator + re-issue/re-supersede guards), **AC-028** (supersession back-link, both
> readable, prior unchanged). **AC-029** (downstream-link-required-to-issue) stays **Pending → P8 (OQ-045)** —
> the gate is unbuildable until the Actions module exists, so P7a does NOT enforce it; it must be retrofitted
> onto the shipped IssueDecision path when Actions land. **AC-016** (SoD-3) gains the chair-override record
> (choice + justification + `DecisionIssued` override flag) but the co-attestation GATE stays Partial→P9
> (vote-coupled). See progress-log P7a entry.
>
> P7b update (2026-07-01): Decision detail UI (`isDecision`) + supersede dialog + an additive bilingual
> `Decision.Title` (new migration `Decisions_AddTitle`). Route `/decisions/:key` (deep-link target); read
> by key, supersede by id (full successor body — blessed deviation). **AC-027 / AC-028 stay Partial** — the
> live UI read + the supersede round-trip strengthen the evidence, but per G-TRACE the **Met** flip waits on
> the live HTTP/UI leg (→ P17); no verdict flips. Backend 620 green (per-file gate 99.66%); FE 422 green
> (decisions files 100% lines), i18n parity 670, axe AA clean. Honest defers unchanged (Convert-to-ADR stub;
> from-topic/successor-key links omitted per ADR-0001; vote/audit-timeline → P9/P14). See progress-log P7b.
>
> P8a update (2026-07-01): Actions module backend (Domain/Application/Infrastructure/API) — the `ActionItem`
> aggregate + W13/W14 lifecycle (create/start/block/unblock/progress/complete/verify/cancel), derived
> overdue, targeted owner notifications, `ACT-YYYY-###` keys, migration `Actions_Init`. **SoD-1 enforcement**
> (verifier ≠ owner/completer) is now wired to `VerifyActionHandler` with the audited denial → 403. **AC-012 /
> AC-013 stay Partial** (domain + handler + HTTP-pipeline proven; the **Met** flip waits on the live real-stack
> Keycloak-PKCE + SQL leg → P17 per G-TRACE). AC-054/055/056 (Hangfire reminders/escalation + Admin job
> dashboard) stay Pending → P8c; AC-029 (decision downstream-link gate, OQ-045) stays Pending → P8d. Backend
> 709 green (per-file coverage gate 168 files, global 99.62%). See progress-log P8a entry.
>
> P8b update (2026-07-01): Actions register + routed detail UI (`ACMP Lists & Registers.dc.html` `isActions`)
> — read-only slice. GET /api/actions (paged, server-side status + overdue filters, due/progress/status sorts)
> + GET /api/actions/{key}; global header counts via two count queries; 6-state status chips incl. Cancelled
> (EN+AR by hand). **GO'd blessed deviation:** routed `/actions/:key` (not the design's in-page drawer) so
> notifications deep-link — retired the `/actions` PlaceholderPage. **No verdict flips:** AC-012/013 unchanged
> (SoD-1 verify UI + Member create/verify path → **P8b2**, live real-stack leg → P17). Create + lifecycle
> transitions deferred → P8b2. FE 470 green (actions files 100% lines), i18n parity 764, axe AA clean,
> tsc + vite build clean. See progress-log P8b entry.
>
> P8c-1 update (2026-07-01): Actions reminder/escalation Hangfire sweep (AC-054/055) — app-owned Hangfire on
> ACMP's OWN SQL (own `HangFire` schema, ADR-0014/CON-001; storage bootstraps its own tables, not EF). The
> recurring `SweepActionRemindersHandler` (all logic; Hangfire only cron-triggers it) turns derived-overdue
> into in-app notifications per docs/29 §3.4: T-3 due reminder (one-shot) → owner; overdue owner notice at the
> configured rhythm (system config `ActionReminders:OverdueMode`, default DailyWhileOverdue, de-duped by day) →
> owner; one-shot escalation to the Secretary (>7d) and Chairman (>14d) via a new role-aware
> `ICommitteeDirectory.GetActiveMembersInRoleAsync`. Idempotent via 4 nullable markers on `ActionItem`
> (migration `Actions_ReminderMarkers`, forward-only); save-before-send (at-most-once, favour no-spam);
> audited (`Actions.RemindersSent`, guardrail #5). **AC-054 / AC-055 Pending → Partial** (logic proven on a
> fake clock + in-memory actions; the live Hangfire cron firing on the real stack → P17 per G-TRACE).
> **AC-056 stays Pending → P8c-2** (the designed Administration → Job Monitor tab, operator fork B). Backend
> 496 Application + 123 Domain + 24 Architecture + 74 Api green; per-file coverage gate 170 files @ 99.62%
> (new files ≥95%); `dotnet format` clean. Backend-only slice — no FE/i18n change. See progress-log P8c-1.
>
> P9a update (2026-07-02): Voting backend — the `Vote` aggregate built INSIDE the Decisions module (docs/11
> "Owning module: Decisions"), 4-state Configured→Open→Closed→Ratified (forward-only, immutable after Close),
> always-attributed ballots, live-attendance quorum via the new `IMeetingQuorumSource` seam (present-quorum at
> Open) + cast-quorum at Close, `VoteOpened` fan-out, `VOTE-YYYY-###` keys, migration `Votes_Init`. **SoD-3
> (Option A)** is now enforced: the vote's closer is the counter of record, and `IssueDecisionHandler` refuses
> (403 + audited) a vote-coupled decision whose issuing chair is that counter, ratifying the vote on a clean
> issue. **AC-021/022/023/024/025/026 Pending → Partial** (domain + handler + HTTP proven; the Met flip waits on
> the live real-stack + `/votes/:id` UI → P9b/P17 per G-TRACE). **AC-015/016** strengthen — the co-attestation
> GATE is wired + tested on the issue path (live → P17). **AC-052** strengthens — the vote-open (`VoteOpened`)
> trigger it awaited is now raised (live center render → P9b/P17). Pre-commit review (csharp-reviewer) fixed a
> HIGH (unvalidated `Decision.VoteId` could silently skip the SoD-3 gate) + a MEDIUM (misleading 403 pre-close)
> by validating the coupling (exists + same-topic + closed) at both record and issue, with 3 guard tests.
> Backend green: Domain 138 / Application 531 / Architecture 24 / Api 99 / Integration 17; per-file gate ≥95% (global 99.71%).
> Backend-only — no FE/i18n change. See progress-log P9a entry.
>
> P9b update (2026-07-02): Voting UI (FE-only) — the `/votes/:key` screen (`isVoting`) + the meeting-workspace
> "Call vote" configure dialog, wired to `/api/votes`. **AC-021/022/023/024/025/026 stay Partial** but the
> `/votes/:id` UI leg they awaited is now shipped (screen states + cast/change/recuse/close/configure); the
> **Met** flip still waits on the real-stack E2E → P17 per G-TRACE. **AC-052** gains the live deep-link target
> (`/votes/:key` now renders the vote-open notification's destination). Reconciliations (design to update):
> double-vote is editable-until-close not a hard block (Fork 1); quorum-fail is a 409 toast not a resting state
> (Fork 2); the closed panel defers View-decision / Record-override (Fork 3). FE green: 544 tests, per-file
> ≥95% lines (votes.ts/voteState.ts 100%, VotePage 98.3%); i18n parity 909 keys. See progress-log P9b entry.
>
> Status at PH-0: all PH-1 acceptance criteria are `Pending` — no governance features built yet.
> The P1 scaffold delivers infrastructure only (no business features), so no AC flips to `Met` here.
>
> P2 update (2026-06-25): reference module pattern verified (build clean, 7/7 tests). Still a pattern/
> foundation phase — no feature AC flips. The Membership domain capability behind AC-058 (deactivate keeps
> attribution) and AC-059 (directory readable by all roles) exists with unit tests, but both criteria require
> HTTP + authorization + UI, which land in P4 — so they remain `Pending`. See progress-log P2 entry.
>
> P3 update (2026-06-25): frontend foundation (app shell, role-filtered nav, OIDC wiring, design system,
> states, dnd). First phase to move localization/a11y ACs: **AC-040, AC-042, AC-045, AC-046 → Met** (Vitest +
> live axe render across EN/AR × light/dark, 0 violations), **AC-041 → Partial** (RTL confirmed by hand;
> automated VR → P17). AC-039 (locale switch preserves form data) stays `Pending` — no form in the shell yet
> (P5+). AC-043/044 (keyboard DnD alternative for backlog/agenda) stay `Pending`: the shared keyboard-accessible
> `SortableList` is built + tested in P3 but isn't wired into those screens until P5/P6. AC-001/005/006/008
> (Keycloak login, RBAC 403) stay `Pending` → P4 (no Keycloak container; server enforcement is P4). Nav/route
> gating in P3 hides UI only — it is not authorization. See progress-log P3 entry.
>
> P4 update (2026-06-25): Identity & Permissions. Server-side Keycloak claim→role mapping, ASP.NET
> policy-based authorization over the full docs/10 §C matrix, ABAC handlers (stream/ownership/delegation),
> SoD predicates, the 401-vs-403 fix, and the Membership module (JIT provisioning, deactivation, streams,
> delegation) + the Users & Membership admin screen. **Met:** AC-002, AC-008, AC-058, AC-059. **Partial**
> (mechanism proven, end-to-end deferred to the consuming phase): AC-003/005/006/007 (RBAC/SoD-5 + audit→BL-066),
> AC-009/010/011 (ABAC, → P5+), AC-012/013/015/016 (SoD predicates, → P8/P9). See progress-log P4 entry.
>
> CHANGE-001 update (2026-06-25): self-hosted-Keycloak infra change-slice (ADR-0015). Infrastructure/ownership
> only (bundled Keycloak + ACMP-owned realm + realm bootstrap); no app behavior changed. **Live-verified:**
> `docker compose up` brought all 6 services up HEALTHY, Keycloak imported the `acmp` realm, OIDC discovery
> issuer = `http://keycloak.localhost:8085/realms/acmp` (PKCE S256), `GET /api/members` → 401 (fail-closed),
> and the **P4-deferred `Membership_P4_Identity` migration applied** on api startup. **Browser login
> round-trip driven successfully** (Chrome authz-code + PKCE → access token with `aud: acmp-api` +
> `realm_access.roles: [Administrator, Secretary]` → `GET /api/members` 200). The deployed SPA was then
> wired (`Dockerfile.web` + compose `build.args` bake `VITE_OIDC_*`) and rebuilt; it now redirects to
> `/login` and presents the Keycloak sign-in CTA against the same baked issuer. **AC-001 → Met** (SSO
> login + role mapping + API authorization proven end-to-end; SPA initiates login; automated UI regression
> → P17). **AC-004 stays Pending** (realm idle-timeout/session policy not yet configured — OQ-003).
> See progress-log CHANGE-001 entry.
>
> CHANGE-002 update (2026-06-26): design-fidelity reconciliation across all built surfaces
> (tokens, shared components, shell, nav, Sign In, Admin) to the "ACMP product context" Claude
> Design package. **No AC verdict changes** — this is visual/copy reconciliation, not new
> features. Deterministic gates green (web 37/37, build, oxlint, i18n parity 102 keys); design
> targets source-verified against the design files. Touches the localization/a11y surfaces behind
> **AC-040/041/045/046** (RTL active-rail mirroring, neutral permission-denied tone, tonal sign-in
> banners, AR tagline fix): their **live axe (WCAG 2.2 AA) + RTL/dark re-verification is pending a
> browser pass** and is the confirmatory step before re-asserting those verdicts. See progress-log
> CHANGE-002 entry.
>
> CHANGE-003 update (2026-06-26): local-design source of truth + full shared component
> library (Design System §05–§12) + screen composition. **No AC verdict changes** — this is
> visual/composition reconciliation against the local `.dc.html` files plus new reusable
> primitives, not new features. Deterministic gates green (web 54/54, prod build 131 kB gz,
> i18n parity 103); design targets source-verified against the local files; Breadcrumb XSS
> hardening added. Touches the a11y/RTL surfaces behind **AC-040/041/045/046** (shell/nav
> metrics, Admin/Login composition, primary logo mark): their **live axe (WCAG 2.2 AA) +
> RTL/dark re-verification remains the confirmatory step** (component a11y semantics are now
> unit-tested; token contrast is a byte-match to the design). See progress-log CHANGE-003.
>
> CHANGE-003 live visual pass (2026-06-26): ran Playwright across the shell, Admin, **and Login**
> in **EN-light and AR-RTL-dark** — **live in-browser axe (WCAG 2.2 AA) clean on all surfaces in
> both**, after fixing two real contrast gaps (`.brand-sub` 4.49, `.login-invite` 4.02 → AA via
> `--text-3`→`--text-2`). RTL mirroring + dark + the AR tagline confirmed visually on every surface
> (Login rendered via an `VITE_OIDC_*`-enabled dev server). **AC-045/046 reconfirmed Met** (live axe
> both directions/themes, all surfaces incl. Login); **AC-040** RTL-mirror confirmed; **AC-041**
> stays Partial (automated visual-regression suite → P17).

> P5a update (2026-06-26): Topics backend (domain → application → infrastructure → API), live-verified on
> the real Docker stack (all 7 services healthy, both migrations applied on SQL Server, authenticated PKCE
> round-trip POST/GET `/api/topics` → TOP-2026-001, JSON columns + owned tables confirmed in SQL). **Met:**
> AC-031. **Partial** (mechanism built + tested; live-HTTP or consuming phase named): AC-009, AC-030,
> AC-032, AC-033, AC-034, AC-035, AC-049, AC-050, AC-057. The Topics UI (P5b) and the Notifications/Hangfire
> + immutable-audit (BL-066) phases carry the remaining end-to-end demonstrations. See progress-log P5a entry.

> P5b PR1 update (2026-06-26): Backlog read path (table + list views) wired to `GET /api/topics`. Read-only
> surface — **no verdict flips**. **AC-057** aging badge is now rendered in the backlog UI (`Backlog.test`),
> stays Partial pending the live browser pass + the SLA-breach notification (Notifications phase). Web 72/72
> (incl. live **axe WCAG 2.2 AA** on the table), i18n parity 175, oxlint + build clean. **Live authenticated
> browser pass done** (Playwright, real Keycloak PKCE): `GET /api/topics` 200, wire contract confirmed live
> (enum→label, streams, null-owner, age); EN-light faithful to the design; AR+dark RTL-mirrored with full i18n;
> AA contrast computed offline (all combos pass, both themes). Found a pre-existing app-wide auth-bootstrap race
> (hard-reload of a data route → transient 401 until retry) — shared-infra follow-up, not P5b. AC-043 (keyboard
> DnD on backlog) re-slotted to P5b PR4 (all DnD in one slice). See progress-log P5b PR1 entry.

> P5b PR2 update (2026-06-26): Submit topic form (W1) wired to POST /api/topics. **Met (newly): AC-039**
> (locale switch preserves form data) and **AC-047** (in-app route-change guard via useBlocker, after migrating
> to a data router). **Partial (newly): AC-048** (beforeunload wired; native dialog not unit-testable in jsdom
> → live pass). AC-030 gains client-side localized validation; AC-049/050 gain the submit upload UI (live MinIO
> → live pass). Web 79/79 (incl. axe AA), i18n parity 226, build/oxlint clean; submit-screen AA contrast
> verified offline (three light-mode text-3 spots fixed → text-2). The PR1 auth-bootstrap 401 was fixed in #12
> (token getter wired during render), already on main. **Live authenticated pass done** (Playwright, real
> Keycloak PKCE): `POST /api/topics` → 201 (TOP-2026-002) and `POST /{id}/attachments` → 201 on **real MinIO**
> (AC-050 → Met); submit form confirmed in AR/RTL with full i18n. See progress-log P5b PR2 entry.

> P5b PR3 update (2026-06-26): Topic detail (read + Overview/Discussion/History + empty relationships sidebar)
> wired to GET /api/topics/{key}; comment POST by Guid id (BL-033). **No verdict flips** — read + comment-display
> surface. **AC-009/034** stay Partial: the owner is shown but the live per-topic **edit**/lock flow is a
> deliberate follow-up slice. The History tab surfaces the read side of AC-032's immutable status/rejection
> events. Web 87/87 (incl. axe AA), i18n parity 249, build/oxlint clean; detail AA contrast verified offline
> (three text-3-on-bg-app spots = 4.02 fixed → text-2). Live detail pass (real GET + comment POST, AR/RTL)
> recommended. See progress-log P5b PR3 entry.

> P5b PR4 update (2026-06-26): Backlog kanban + accessible DnD (final P5b slice). **Met (newly): AC-043** —
> the keyboard "M" move popover is the accessible alternative to drag (unit-tested). The board groups topics
> into 5 buckets over canonical status; the only P5-legal cross-bucket moves open dialogs (accept needs an
> owner; reject/defer need a reason) and two columns reject all drops (scheduling → P6). AC-009 advances
> (owner assignment wired to grant-on-accept; live grant/403 → live pass); AC-031's mandatory reason is now
> collected in the UI. Web 94/94 (incl. axe AA), i18n parity 278, build/oxlint clean. Live kanban pass
> recommended. **P5b screens complete** (backlog 3 live views, submit, detail). See progress-log P5b PR4 entry.

> CHANGE-004 update (2026-06-26): fixed the Keycloak `acmp-web` access token missing `sub` (the built-in
> `basic` client scope was unassigned in KC 24+) — JIT provisioning (`POST /me`) threw "Authentication
> required" for every user, leaving the member directory empty. Realm-export fix + the SPA now calls `POST /me`
> on login. **Live-verified end-to-end:** provisioning → 200, directory → 1 member, then the kanban accept
> (M-move → owner → `POST /accept` 204 → status Accepted + owner assigned) — **AC-009 grant-on-accept now
> proven live through the UI** (stays Partial pending the per-topic edit-403 path). Also makes **AC-002**'s
> live JIT actually function (was test-proven only). See progress-log CHANGE-004.

> P5-review remediation (2026-06-27): acted on the pre-advance P5 audit — fixed all flagged design-fidelity
> defects (detail affected-streams → info-toned chips; urgency cards color-coded by semantic urgency + dot ring;
> shared status-chip corrected to the Design-System 22/8/11.5; shared table cell padding 16→12px; backlog table
> column widths + type/age cell sizes; search input dims; submit fieldset padding; table-shaped loading skeleton;
> empty-state search icon; dropzone **upload** icon + "Drop files…" copy + one-row title hint/counter;
> topic-detail discussion-count badge + compose avatar; history timeline dot ring; copy: backlog count +
> autosave indicator) and corrected the one over-claim: **AC-043 Met→Partial** (the kanban "M" popover is a
> keyboard alternative for *status* moves, not the AC's priority-ordinal reorder — BL-039/BL-041 deferred).
> Shared primitives already matching the Design System (button 38/9, input 38, segmented 30) were left
> unchanged (forking them would regress the DS + other screens). Gates: web 94/94, backend 358/358 (ArchUnit
> 8/8), i18n parity 278, build clean. OpenTelemetry bumped 1.10→1.12 (latest; the NU1902 moderate advisory
> GHSA-4625-4j76-fww9 has no patched release — accepted: internal-only OTLP egress, DoD permits moderate).
> See progress-log P5-review remediation.

> P6a update (2026-06-27): Meetings module backend (domain → application → infrastructure → API) — agenda
> building, meeting scheduling/lifecycle, attendance, discussion, actual-time (W5–W9), plus the cross-module
> `ITopicScheduler` seam (Prepared→Scheduled on publish, Scheduled→InCommittee on start; idempotent,
> implemented in Topics.Infrastructure — Meetings never reads Topics' tables, ADR-0001). Backend 388/388
> (Domain 42 · Architecture 12 · Application 314 · Api 20); ArchUnit enforces Meetings⟂Topics⟂Membership.
> **AC-044 Pending→Partial** — the backend reorder (`MoveAgendaItem` ±1 + `Agenda.MoveItem`, the path
> keyboard move-up/-down drives) is built + tested; the keyboard-accessible **agenda reorder UI** lands in
> P6c (same backend-then-UI split as AC-043). **AC-051/053 stay Pending → P6b** (in-app Notifications backend:
> `InAppNotificationChannel` + `GET /api/notifications` + the publish/schedule fan-out via a new
> `ICommitteeDirectory`). **AC-011** (presenter meeting-window enforcement) stays Partial → its UI/runtime
> path. Live SQL migration apply + an authenticated `/api/meetings` round-trip are the optional P6 tail.
> See progress-log P6a entry.

> P6b update (2026-06-27): in-app Notifications module (the AC-051/053 floor) + the publish/schedule fan-out.
> New `Notifications` module (`Notification` entity + `InAppNotificationChannel` = the v1 `INotificationChannel`,
> synchronous write; `GET /api/notifications` + mark-read scoped to the current user with an IDOR guard) and the
> cross-module `ICommitteeDirectory` seam (Shared contract, implemented in Membership, active members only —
> AC-058). `ScheduleMeeting`/`PublishAgenda` now fan out one bilingual notification per active member; the
> `AgendaPublished` body carries the meeting date + agenda title and a deep link to the agenda view (AC-051
> content contract). Backend 397/397 (Domain 42 · Architecture 16 · Application 319 · Api 20); ArchUnit enforces
> Notifications isolation + a no-assembly-edge Meetings→Notifications seam. **AC-051 / AC-053 Pending → Partial**
> (mechanism + content + channel-exclusivity unit-proven; live HTTP + the notification-center render → P6e).
> **AC-052** stays Pending (the deep-link mechanism exists; the vote-open notification is raised in P9).
> See progress-log P6b entry.

> P6c update (2026-06-27): Agenda builder UI (the design's agenda tab) wired to the Meetings API + a read-only
> meetings list. `api/meetings.ts` (read-by-key / mutate-by-id hooks), `features/meetings/AgendaBuilder.tsx`
> (pool from Prepared topics, drop-zone agenda, timebox stepper, presenter Select from /api/members, time-budget
> bar, publish dialog) and `MeetingsList.tsx`, composed from the shared library, logical-CSS RTL-safe, full
> EN+AR `meetings.*` namespace (parity 344). **AC-044 Partial → Met** — the keyboard-accessible reorder
> (move-up/-down → ±1, disabled at ends, aria-live announce) is shipped + unit-tested, jsdom axe clean. Web
> 151/151 (incl. 2 axe AA cases on the new screens), tsc + build + oxlint clean. The design's Preview button /
> notify-group toggles / RTE are mock chrome (disabled/honest-static); scheduling a NEW meeting is deferred
> (committee/chair pickers; committeeId not exposed). Live browser pass (real API, AR/RTL+dark, live axe)
> recommended — needs a scheduled meeting. AC-051/053 stay Partial → P6e. See progress-log P6c entry.

> P6d update (2026-06-27): live meeting workspace UI (the design's meeting tab) — agenda spine, attendance
> (present/absent → POST /attendance), discussion notes (→ POST /discussion), actual-time + outcome (→ POST
> /actual-time), the start/end lifecycle, and the in-page Tabs hosting both the agenda builder (P6c) and the
> workspace under `/meetings/:key`. Record-decision/create-action/call-vote are disabled stubs (P7/P8/P9); MoM
> is P7. **No verdict flips** — this is the UI for the W7–W9 workflows whose ACs are already covered by the P6a
> backend; the new screens add a surface to the localization/a11y ACs (AC-040/045/046 render RTL + axe-clean in
> the component tests; AC-041 stays Partial → VR P17). Web 168/168 (incl. a workspace axe AA case), parity 389,
> tsc + build + oxlint clean, CSS RTL-safe. Live browser pass (real conduct-meeting round-trip, AR/RTL+dark)
> recommended — needs a scheduled+published meeting. AC-051/053 stay Partial → P6e. See progress-log P6d entry.

> P6e update (2026-06-27): notification center wired to the live `/api/notifications` feed + the unread bell
> badge. `api/notifications.ts` (feed + mark-read, 30s poll), `NotificationCenter.tsx` (live list, unread
> styling, click → mark-read + close + deep-link navigation, calm empty state preserved), `TopBar.tsx` (badge
> only when unread>0). **AC-051 Partial → Met** (end-to-end: P6b fan-out → the center renders the date/title/
> deep-link item + badge, deep link navigates) and **AC-053 Partial → Met** (single in-app channel, no email/
> Webex). **AC-052 Pending → Partial** (the deep-link navigation mechanism is proven; the vote-open trigger is
> P9). Web 177/177 (incl. a panel axe AA case), parity 393, tsc + build + oxlint clean, CSS RTL-safe. No
> `.dc.html` reference exists for the live list (planning doc docs/14 p.79 only) — composed from the shell's
> notif-* styles. Live cross-session browser pass recommended. See progress-log P6e entry.

> P6 follow-up (2026-06-27): the deferred meeting-schedule flow is built (ScheduleMeetingDialog +
> useScheduleMeeting; MeetingsList "Schedule meeting" action), and its blocker removed — the committee is now
> implicit server-side (`Meeting.SingleCommitteeId`; `CommitteeId` dropped from ScheduleMeetingCommand, a
> never-read field, no ADR). Chair picked from /api/members (defaults to Chairman). **No verdict flips** —
> meeting scheduling (W5) has no dedicated AC; this makes the P6 loop reachable end to end. Backend 397/397
> (command change carried through Domain/Application/Api), web 182/182 (incl. a dialog axe AA case), parity 412,
> dotnet format + tsc + build + oxlint clean. Live schedule round-trip recommended. See progress-log P6 follow-up.

> P6 live + hardening (2026-06-27): the full P6 loop was driven live (rebuilt stack, real Keycloak PKCE, AR/RTL)
> and 3 findings fixed — CSP `font-src 'self' data:`; a **filtered** unique email index so JIT provisions
> emailless Keycloak users (was a 500); and a **real P6b fan-out bug** (the shared owned-`LocalizedString`
> instance 500'd the notification for the 2nd+ recipient — broke notifications for any committee with ≥2
> members), fixed in `InAppNotificationChannel` with a unit + 2-member integration regression. **AC-051/052-shape/
> AC-053 are now LIVE-verified end to end:** scheduling MTG-2026-003 → the current member's notification center
> shows the bilingual item + a "1 unread" bell badge → clicking marks-read (badge clears) and follows the deep
> link. AC-051/053 stay **Met** (now with live proof); **AC-052** stays **Partial** (the deep-link *navigation*
> is proven live; the vote-open *trigger* is P9). Backend 407/407. See progress-log "P6 hardening".

> P3 foundation refresh (2026-06-27): reconciled the token/component/shell/nav foundation to the *updated*
> design references (Design System / ACMP shell / Navigation & IA). Tokens already matched verbatim; targeted
> drift fixes — StatusChip restored to DS §08 24/9/12 (+ `sm` 22/8/11.5 for table rows), TopBar "Ctrl K" search
> hint + real Ctrl/⌘+K focus, brand-word 15 / icon-btn 36 / chip-btn 36, notification popover r13/top46 +
> badge 16/−3, tabs pad-inline 14, dead `.topbar-user` removed. **No verdict flips** — visual/fidelity only.
> Touches **AC-040/045/046** (RTL/focus/labels — unit + axe still green) and **AC-041** (stays Partial →
> automated VR P17). Web 184/184, tsc+build clean (JS 173.98 kB gz), oxlint clean; live bundle verified to
> carry the reconciled CSS. Live authenticated pass done on desktop (EN-light + AR-RTL-dark, real Keycloak
> PKCE) — shell/nav/chrome verified incl. full RTL mirroring + dark tokens; remaining combos (EN-dark/AR-light/
> tablet) covered by the same token/logical-CSS mechanism; automated pixel-diff VR → P17. See progress-log
> "P3 foundation refresh".

> DV-04 rich-text unification (2026-07-01): unified the three divergent rich-text surfaces (Submit-topic
> inert toolbar, Meeting-notes functional markdown, Minutes deferred) into one shared `MarkdownEditor`
> (markdown stored as text). Closes AM-06 / rebuild-findings §8.3. No AC verdict flips (editor mechanism +
> data model, not an acceptance criterion); read-rendering of stored markdown deferred (no new dependency).
> FE 402 green; EN/AR parity 0 drift. See progress-log "DV-04".

> P6b Notifications IA reconcile (2026-06-30): reconciled the bell popover + full inbox to the design
> references (`ACMP.dc.html` L92–131 + L706–739) — `role="dialog"` popover with `{n} new` pill, Unread/All
> tabs, loading skeleton, tone-icon · artifact-key · time · message rows + per-item mark-read, View-all
> footer; inbox channel line + Unread/All underline tabs with counts + TYPE-label rows + Mark-read pills,
> Load-more kept. **DV-02** (Load-more vs infinite) → **blessed**; **DV-05** (Unread/All) → **confirmed by
> design**; **RD-09** → v1 in-app only, **no preferences page** built. Backend: `read-all` now emits a
> `Notifications.AllRead` AuditEvent after persistence (reverses P6e's no-audit for the bulk sweep; single
> mark-read stays un-audited — **signed off 2026-07-01 as OQ-044**, the asymmetry is intentional and clarifies
> ADR-0009) — **type = existing `Category`, key derived from `DeepLink`** (no migration,
> no DTO change). **No verdict flips** — AC-051/053 stay **Met**, now exercised through the reconciled
> components; AC-052 stays Partial (vote-open notification → P9). FE 397 green + per-file lines ≥95%; BE
> Application 420 + Api 5 green (read-all both branches + user-scoped); EN/AR parity (0 drift); dev-stub VR
> (EN-light + AR-RTL-dark) matches. See progress-log "P6b (Notifications IA)".

> P4 UI refresh (2026-06-27): rebuilt Administration → Users & Membership to the updated
> `ACMP Administration.dc.html` — 7-tab strip (Users active, six later-phase placeholders disabled), richer
> directory (committee `.adm-mchip` chips with `×` + inert dashed `+add` + read-only voting switch, assignments
> check + honest `—`, per-row view button) and a new **read-only user-detail** panel (in-place, API-backed data
> only — no invite). **Removed** the "Provision via Keycloak" button + the invite panel (conflicts ADR-0015,
> manual Keycloak provisioning → **OQ-042**). **No verdict flips** — visual/fidelity + a read-only view.
> **AC-059** stays **Met** (directory + detail unit-tested + axe-clean). Touches **AC-040/045/046** (the admin
> screens render RTL-mirrored + axe-clean in the component tests) and **AC-041** (stays Partial → automated VR
> P17). Web 189/189 (incl. directory + detail axe AA cases), i18n parity 427, tsc + vite build + oxlint clean,
> administration.css grep = zero physical properties. Live authenticated VR (8 combos vs the `.dc.html`) is the
> recommended confirmatory step — blocked on the operator setting the `acmp-admin` dev password. See progress-log
> "P4 UI refresh".

> P5 UI refresh (2026-06-27): rebuilt Topics & Backlog to the updated `ACMP Backlog & Topic.dc.html` — new shared
> `FilterChip` dropdowns (Status multi; Type/Urgency single; Stream/Owner disabled — no option source yet), the
> accent saved-view chip, and the previously "coming soon" **calendar** and **timeline** now render as first-class
> live views with **faithful chrome + an honest empty body** (no scheduled/due/span data in the Topics API → P6;
> D1). Submit gains an inert RTE toolbar; Topic detail is now **5 tabs** (Overview · Discussion · **Attachments**
> own tab + post-create upload · **Votes** empty → P9 · History). **No verdict flips** — visual/fidelity + honest-
> empty new views. Touches **AC-040/045/046** (new chips/calendar/timeline/tabs render RTL-mirrored + axe-clean in
> the component tests) and **AC-041** (stays Partial → automated VR P17); **AC-057** aging badge unchanged.
> Web 197/197 (incl. FilterChip + new-view + new-tab axe/behavior cases), i18n parity 438, tsc + vite build +
> oxlint clean, topics.css + controls.css grep = zero physical properties. **Live authenticated VR DONE**
> (2026-06-27): real Keycloak PKCE pass over the rebuilt stack visually verified all 5 new surfaces (filter chips,
> calendar, timeline, submit RTE, detail 5 tabs) in EN-light + AR-dark + tablet-768 (no overflow; full RTL mirror
> + dark tokens); doubles as the E2E smoke pass. Automated pixel-diff VR → P17. See progress-log "P5 UI refresh".

> P6 Meetings list redesign (2026-06-29): rebuilt the meetings list to the design's `isList` screen
> (`ACMP Meetings.dc.html`) — an **Upcoming/Past** split (two shared `Table`s, columns
> ID·When·Title·Type·Status) with a **List⇄Calendar** toggle, plus a new `MeetingsCalendar` month grid
> (Intl month/weekday labels, RTL-mirrored prev/next, status-toned event pills over real
> `scheduledStart`, defaults to current month). The old single flat table was drift from this known
> reference, not no-design scaffolding. Operator GO "Match design, keep agenda chip": **kept** an
> Agenda-status chip column the design omits (deliberate deviation), **omitted** the mock's
> filter chips + Saved-views (no backend — not faked). Frontend-only; no API change. **No verdict
> flips** — a new view over existing meeting data, no dedicated AC. Adds a surface to
> **AC-040/045/046** (both screens render EN/AR + axe-clean, 0 violations across the two meetings
> specs; computed-px gate confirms every list + calendar literal matches the `.dc.html`) and
> **AC-041** (RTL mirror live-confirmed EN/AR desktop + AR tablet; automated pixel-diff VR → P17).
> Web 223/223 (incl. MeetingsCalendar + list-split/toggle axe + behaviour cases), i18n parity OK,
> tsc + vite build (JS 180 kB gz) + oxlint clean, meetings.css zero physical properties. See
> progress-log "Meetings list to design".

> P6 Create-meeting UI fixes (2026-06-29): visual pass over `/meetings/new` (design `isCreate`) fixed
> real defects — a global `.field + .field` margin double-counted the schedule card's flex gap (32px
> rhythm) and pushed the 2nd field of every two-column row 16px down (Ends/Mode misaligned); the Mode
> segmented didn't fill its cell. One scoped CSS reset → uniform 16 + top-aligned rows; Mode now
> `width:100%`. Operator GO: replaced native `datetime-local` (rendered mm/dd/yyyy under RTL) with the
> design's Date + Start/End times — a new shared **`DateField`** (trigger + calendar icon → `DatePicker`
> popover, Intl labels) + native `<input type=time>`; meeting is single-day (start/end share the date).
> Frontend-only, same `ScheduleMeeting` payload — **no verdict flips**. Touches **AC-040/045/046**
> (form renders EN/AR + axe-clean; live computed-px gate: gaps 16, rows aligned, Mode 310==cell) and
> **AC-041** (RTL mirror live-confirmed EN/AR incl. the date popover; pixel-diff VR → P17). Web 225/225
> (new DateField + date-required tests), parity OK, tsc + build + oxlint clean. See progress-log
> "Create-meeting screen UI fixes".

> P6a Meetings IA reconcile (2026-06-30): refactored the meeting detail into a **shell** (header card +
> 6-tab deep-linkable `NavLink` strip + `<Outlet/>`) over nested routes — index `MeetingOverview`,
> `/agenda` `AgendaBuilder`, `/attendance`+`/notes` `MeetingConduct` (→ `MeetingWorkspace` while
> InProgress, else gate), `/minutes` (P7 placeholder), `/recording` (Webex Phase-2 defer). RD-08
> ownership split applied; "remove duplicate denied" verified as a no-op (route-denial = the global
> auth gate, single source). Closed **DV-16** (actual-time + outcome recorder re-added to the workspace,
> wired to `useRecordActualTime`), **DV-21** (agenda pool label → "Prepared", EN+AR), **DV-03** (timer
> `mm:ss`/`h:mm:ss` confirmed, VR `8:49:49`). Blessed deviation: Recording promoted to a 6th peer tab
> (NV-08 + route map). Frontend-only, no backend change — **no verdict flips**. Touches
> **AC-040/045/046** (Overview + workspace render EN/AR + axe-clean component tests) and **AC-041** (RTL
> mirror confirmed via dev-stub VR EN-light + AR-RTL-dark; pixel-diff VR → P17). Live stack was down →
> dev-stub VR (`npm run dev` + Playwright `/api/**` mocks). Web 384/384 (81 in the meetings feature:
> new shell+conduct, MeetingOverview, MeetingMinutes, MeetingRecording, DV-16 workspace), per-file
> lines ≥95% (global 98.62%), i18n parity 608, tsc + oxlint clean. See progress-log "P6a (Meetings IA)".

| AC | Section | Verdict | Test ref | Notes |
|---|---|---|---|---|
| AC-001 | Auth & Identity | Met | manual (live UI: ACMP /login → Keycloak → /dashboard authenticated; + token roles Administrator,Secretary / aud acmp-api / GET /api/members 200) | Full SSO round-trip through the app UI verified (after CSP connect-src fix). Logout button added (TopBar) and verified end-to-end (dashboard → /login). Automated UI regression now landed — auth.spec (S6a) asserts the unauthenticated deep-link→/login guard and the real Keycloak PKCE round-trip → authenticated dashboard, in CI on the live stack |
| AC-002 | Auth & Identity | Met | KeycloakRoleClaimMapperTests + MembershipFeatureTests + MembershipApiTests (/me) | Claim→Secretary mapped; JIT profile gets the role end-to-end |
| AC-003 | Auth & Identity | Partial | KeycloakRoleClaimMapperTests + MembershipFeatureTests | No-claim → deny (fail-closed default) + AuthEvent to log sink; immutable store → BL-066 |
| AC-004 | Auth & Identity | Pending | — | Idle timeout re-auth (ACMP-realm session policy, OQ-003 + form auto-save); needs live realm |
| AC-005 | RBAC | Partial | PermissionMatrixTests + MembershipApiTests | Submitter denied (matrix every restricted policy + HTTP 403); nav hidden P3; named feature endpoints P5–P9 |
| AC-006 | RBAC | Partial | PermissionMatrixTests + MembershipApiTests | Auditor 403 on mutate (matrix + HTTP); audit-on-deny → BL-066; feature endpoints P5+ |
| AC-007 | RBAC | Partial | PermissionMatrixTests | SoD-5 proven: Administrator denied on every committee-content policy; live vote/decision API 403 → P7/P9 |
| AC-008 | RBAC | Met | MembershipApiTests (No_token_returns_401) | RequireAuthorization + JwtBearer → 401 without a token |
| AC-009 | ABAC | Partial | AbacHandlerTests + TopicApiTests (grant-on-accept) | Grant-on-accept + ABAC owner check proven live on accept; per-topic edit 403 → P5b |
| AC-010 | ABAC | Partial | AbacHandlerTests + MembershipResolverTests | Stream scope handler + resolver proven; live action-on-out-of-scope-topic 403 → P5/P8 |
| AC-011 | ABAC | Partial | AbacHandlerTests | Capability scoped to the specific topic proven; presenter meeting-window runtime enforcement → P9 (live vote/meeting-window path) |
| AC-012 | SoD-1 | Partial | SegregationOfDutiesTests + ActionHandlerTests (owner-verify denied + `ActionVerifyDenied` audit, stays Completed) + ActionsApiTests (owner verify → 403, HTTP) + ActionActions.test (Verify hidden from owner/completer; server 403 surfaced without closing the dialog) | P8a: the SoD-1 gate is enforced in `VerifyActionHandler` — the owner's verify attempt is audited then refused (403). P8b2a: the Verify UI hides the button from the owner/completer (client SoD-1 convenience gate) and surfaces the API 403; the API stays the real gate. Live real-stack (Keycloak PKCE + SQL) leg → P17 |
| AC-013 | SoD-1 | Partial | SegregationOfDutiesTests + ActionHandlerTests (independent verifier → Verified + `ActionVerified` audit) + ActionsApiTests (third-party verify → 204, Verified) + ActionActions.test (a non-owner verifier sees + triggers Verify → `useVerifyAction`) | P8a: the positive verify path (verifier ≠ owner ≠ completer) transitions Completed→Verified, stamps the verifier, notifies the owner. P8b2a: a non-owner verifier's Verify button POSTs `/actions/{id}/verify`. Live real-stack leg → P17 |
| AC-014 | SoD-2 | Partial | MinutesHandlerTests (sole-author approval allowed + flagged; different approver clears the flag) + MinutesApiTests (sole-author publish → ApprovedBySoleAuthor) + MeetingMinutes.test (published record renders read-only) | P7c: soft SoD-2 flag + `ApprovedBySoleAuthor`; P7d: the minutes tab renders the approved/published record; live real-stack UI → P17 |
| AC-015 | SoD-3 | Partial | SegregationOfDutiesTests + VoteHandlerTests (chair counted the vote → issue 403 + audited denial; vote stays Closed, not ratified) | P9a: the SoD-3 GATE is now enforced on the decision-issue path — the issuing chair may not be the vote's counter of record (Option A). Live real-stack → P17 |
| AC-016 | SoD-3 | Partial | SegregationOfDutiesTests + DecisionHandlerTests (override + justification + flag) + VoteHandlerTests (secretary counted → chair issue allowed + vote Ratified) | P9a: the co-attestation GATE is now wired — a secretary-counted vote lets the chair issue + ratify; a chair-counted vote is refused (AC-015). Live real-stack → P17 |
| AC-017 | Audit | Pending | — | State change → audit entry |
| AC-018 | Audit | Pending | — | Audit row immutable |
| AC-019 | Audit | Pending | — | Hash-chain integrity check |
| AC-020 | Audit | Pending | — | Auditor search; others 403 |
| AC-021 | Voting | Partial | VoteTests + VoteHandlerTests (Open locks config + present-quorum via the seam + VoteOpened fan-out) + VotesApiTests | P9a: config locks at Open, present-quorum gate (live-attendance seam), eligible voters notified (deep link /votes/{key}). Live real-stack + /votes UI → P9b/P17 |
| AC-022 | Voting | Partial | VoteTests (second cast throws) + VoteHandlerTests (double-vote audited denial `Decisions.BallotDenied`) + VotesApiTests (2nd cast → 409) | P9a: one ballot/voter — first ballot unchanged, denial audited, DB unique-index backstop. Live → P17 |
| AC-023 | Voting | Partial | VoteHandlerTests + VotesApiTests (GetVoteByKey returns attributed ballots, no masking) | P9a: ballots attributed by name in the detail DTO + aggregate tally. Live Auditor view → P17 |
| AC-024 | Voting | Partial | VoteTests (close cast-quorum guard) + VoteHandlerTests + VotesApiTests (close-without-quorum → 409, stays Open) | P9a: "Quorum not met: {cast} of {MinCast}"; vote stays Open. Live → P17 |
| AC-025 | Voting | Partial | VoteTests (no mutators post-Close; re-transition throws) + VotesApiTests (cast-after-close → 409) | P9a: immutable after Close (frozen tally/ballots); crypto hash-chain → P14. Live → P17 |
| AC-026 | Voting | Partial | VoteTests (forward-only; Ratify only from Closed) | P9a: Configured→Open→Closed→Ratified strictly forward-only. Live → P17 |
| AC-027 | Decisions | Partial | DecisionTests (no-mutator / re-issue + re-supersede throw) + DecisionHandlerTests + DecisionsApiTests (issue→Issued) + DecisionPage.test (read-only detail, no edit surface) | Domain immutability + issue path proven; P7b renders the read-only detail (no edit affordance); live HTTP/UI confirmation → P17 (G-TRACE) |
| AC-028 | Decisions | Partial | DecisionTests (Supersede back-link) + DecisionHandlerTests (successor Issued, prior Superseded, both readable, prior unchanged) + DecisionsApiTests (supersede 201 + prior back-link) + DecisionPage.test (superseded badge/banner + supersede dialog → POST /supersede) | Both readable + prior unchanged proven; P7b adds the supersede dialog + superseded-state UI; live HTTP/UI → P17 (G-TRACE) |
| AC-029 | Decisions | Met | DecisionHandlerTests (follow-up outcome + no link → rejected, stays Draft; non-follow-up exempt; `RequiresDownstreamLink` theory over all 11 outcomes) + DecisionsApiTests (P8d: `/issue` 409 + stays Draft, then link → 204; Rejected exempt → 204) | **P8d (OQ-045 resolved):** follow-up-bearing outcomes (Approved/ConditionallyApproved/EnhancementsRequired/DesignChangesRequired/ResearchRequired) reject `/issue` with 409 unless ≥1 downstream link; gate lives in `IssueDecisionHandler` (cross-module count via Actions-owned `IActionLinkDirectory`, not a domain guard), so supersession auto-exempts (ASM-014). "Downstream link" = ≥1 ActionItem sourced from the decision until Risk/Traceability land (P10, ADR-0008). **P10c widened the gate** (still Met, superset): satisfied by ≥1 linked Action (`IActionLinkDirectory`) OR ≥1 curated downstream traceability edge (`ITraceabilityLinks` — decision as source of recorded-as/resolves or target of implements; upstream/lineage excluded, ASM-P10c-2). New DecisionsApiTests case proves the edge-only path issues |
| AC-030 | Topic lifecycle | Partial | SubmitTopicValidator tests + TopicApiTests + SubmitTopic.test (client validation) | Server validation + HTTP 400 + no record; submit form now shows localized client-side required-field errors; server-side localized messages → BL-016 |
| AC-031 | Topic lifecycle | Met | TopicApplicationTests (Reject/Defer require a reason) + TopicApiTests (reject no-reason → 400) + TopicHandlerTests (S1: reject-deny keeps Submitted; wrong-status domain guard) | Mandatory rejection rationale enforced; S1 adds adversarial handler coverage (authz-deny, status guard) |
| AC-032 | Topic lifecycle | Partial | TopicTests + TopicHandlerTests (S1: Reject_records_the_rationale_as_immutable_history_and_audits) | Immutable rejection history event (reason+actor+timestamp) + TopicRejected audit adversarially proven in S1; submitter notify → Notifications phase |
| AC-033 | Topic lifecycle | Partial | TopicTests | Rejection event append-only (no mutation surface); DB-enforced immutability + hash-chain → BL-066 |
| AC-034 | Topic lifecycle | Partial | TopicTests + TopicHandlerTests (S1: post-Accept 403 authz-deny + content-lock + metadata-only edit; pre-Accept non-submitter denied) + TopicApplicationTests (S1: UpdateTopicValidator) | Content locked post-accept + 403 (authz-deny) adversarially proven at handler in S1; live HTTP 403 UI → P17 |
| AC-035 | Topic lifecycle | Met | TopicTests + TopicHandlerTests (S1: Prepare deny + wrong-status guard + Accepted→Prepared + TopicPrepared audit) + TopicApplicationTests (S1: PrepareTopicValidator) + core-loop.spec (S6b-1: live UI accept → live-HTTP Accepted→Prepared → the prepared topic appears in the live agenda pool, is added + published) | Accepted→Prepared transition + TopicPrepared audit adversarially proven (S1); the live HTTP leg now lands end-to-end in the core-loop E2E and the prepared topic is visibly consumed by the live agenda builder (no standalone prepare UI by design). From Partial (S6b-1, 2026-06-30) |
| AC-036 | MoM | Partial | MinutesOfMeetingTests (supersede from Approved/Published + no public setters) + MinutesHandlerTests (v2 under same key, prior Superseded + back-link, readable) + MinutesApiTests (supersede 201 v2, prior back-link) + MeetingMinutes.test (supersede dialog validates + posts; superseded state + reason render) | P7c: version-preserving supersede (same MIN key, Version++), prior immutable + linked; P7d: supersede UI + version history; live real-stack UI → P17 (G-TRACE) |
| AC-037 | MoM | Partial | MinutesOfMeetingTests (InReview→Draft) + MinutesHandlerTests (change-request → Draft + author notified) + MinutesApiTests (request-changes → Draft) + MeetingMinutes.test (Request changes calls the mutation) | P7c: change-request returns to Draft, targeted author notification; P7d: Request-changes action in the review card; live real-stack UI → P17 |
| AC-038 | MoM | Partial | MinutesOfMeetingTests (Approved→Published) + MinutesHandlerTests (publish fans out per active member + deep link + audit) + MinutesApiTests (draft→submit→approve→publish→Published) + MeetingMinutes.test (Approve & publish drives approve→publish) | P7c: publish seals + notifies all members (deep link `/meetings/{key}/minutes`); AC-038's single-step prose maps to Approve+Publish (5-state); P7d: one "Approve & publish" action; live real-stack UI → P17 |
| AC-039 | Localization | Met | SubmitTopic.test (locale-switch preserves value) | Submit form state survives an EN↔AR switch (React state, form not keyed on language) |
| AC-040 | Localization | Met | i18n/direction.test.ts + axe render | dir=rtl mirrored layout — sidebar→inline-end, Arabic font, logical CSS; verified live (P3) |
| AC-041 | Localization | Partial | manual render (Playwright) | RTL render confirmed clean by hand; automated visual-regression suite → P17 |
| AC-042 | Localization | Met | theme/theme.test.ts | Theme persisted via localStorage + applied as data-theme |
| AC-043 | Accessibility | Partial | Kanban.test (M-move popover) + topicMeta.test + TopicHandlerTests (S1: Prioritize sets ordinal / immutability guard / authz-deny) + TopicApplicationTests (S1: PrioritizeTopicValidator) | Keyboard alternative for **status** moves shipped (the "M" move popover; legal moves open accept/return dialogs, illegal moves announced). The backend **priority-ordinal persist** (`SetPriority`) is now adversarially tested in S1 (ordinal set + audited, immutable-topic guard, `Backlog.Prioritize` authz-deny). The AC's literal **UI move-up/down wired to the persisted ordinal** (BL-039 within-column reorder, BL-041) is still **not yet built** — deferred to a follow-up slice. Corrected from Met (P5-review remediation, 2026-06-27). |
| AC-044 | Accessibility | Met | AgendaBuilder.test (move ±1 + aria-live announce, axe AA) + MeetingHandlerTests (move ±1) + dnd-and-failures.spec (S6b-2: native HTML5 agenda reorder ±1) + rtl-a11y.spec (S6b-3: live axe AA EN/AR) | Keyboard-accessible agenda reorder shipped: the move up/down buttons send a single ±1 `move` (disabled at the ends) with a synchronous `aria-live` announce; native drag is progressive enhancement on top. Unit-tested + jsdom axe clean; the recommended live browser pass now lands — the native drag-reorder is exercised on the real browser (S6b-2) and live axe AA is clean in EN/AR (S6b-3). From Partial (P6c, 2026-06-27). |
| AC-045 | Accessibility | Met | axe (WCAG 2.2 AA) render | Global :focus-visible (2px solid --focus, offset) — axe-clean EN/AR×light/dark (P3) |
| AC-046 | Accessibility | Met | axe (WCAG 2.2 AA) render | Labels/aria/contrast/reading order — axe 0 violations across EN/AR×light/dark; landmarks verified (P3) |
| AC-047 | Unsaved-work | Met | SubmitTopic.test (guard dialog on dirty nav) | useBlocker (data router) → confirm Dialog on in-app route change while the submit form is dirty; Keep editing / Leave |
| AC-048 | Unsaved-work | Partial | SubmitTopic.tsx (beforeunload wired) | beforeunload listener added when dirty (reload/close/hard-nav); the native browser dialog isn't unit-testable in jsdom → live pass |
| AC-049 | File upload | Partial | TopicAttachmentTests (validator) + SubmitTopic.test (size reject) | Server size/MIME rejection (400); submit form adds a 50 MB client-side pre-check with a localized message; server-side localized message → BL-016 |
| AC-050 | File upload | Met | TopicAttachmentTests (handler) + live (POST /{id}/attachments → 201 on real MinIO) | Submit UI stages a file and POSTs multipart to the new topic; live pass confirmed 201 against real MinIO (handler does IFileStore store + SQL metadata + DocumentAttached audit) |
| AC-051 | Notifications | Met | MeetingHandlerTests (AgendaPublished fan-out: date+title+deep link, EN+AR) + NotificationHandlerTests + NotificationCenter.test (live list + deep-link nav) + TopBar.test (badge) | End to end: PublishAgenda fans out one in-app notification per active member (synchronous ≤5s write) carrying the meeting date + agenda title + a `/meetings/{key}` deep link; the notification center renders it (unread badge + list) and clicking follows the deep link. The standing cross-session caveat now lands — core-loop.spec (S6b-1) publishes an agenda and verifies the unread bell in TWO separate live browser contexts (member + chairman), confirming the ≥2-member fan-out end-to-end. From Partial (P6e, 2026-06-27). |
| AC-052 | Notifications | Partial | NotificationCenter.test (deep-link click → navigate) + VoteHandlerTests (Open fans out VoteOpened to eligible voters, deep link /votes/{key}) | The deep-link **navigation** mechanism is built + tested. P9a now raises the **vote-open** (`VoteOpened`) notification it was waiting for — one per eligible voter. The live notification-center render of it → P9b/P17. From Pending (P6e, 2026-06-27). |
| AC-053 | Notifications | Met | NotificationHandlerTests + DI (single INotificationChannel = InAppNotificationChannel) + NotificationCenter.test | Exactly one channel is registered and rendered (in-app); no email/Webex is attempted and the absence raises no error. Structurally guaranteed + unit-proven on both server (fan-out) and client (center). From Partial (P6e, 2026-06-27). |
| AC-054 | Background jobs | Partial | ActionReminderSweepTests (T-3 exact / due-today / outside-window / one-shot across runs) | P8c-1: the recurring `SweepActionRemindersHandler` sends the owner a one-shot "due soon" reminder inside the configured window (docs/29 §3.4, T-3 default); idempotent via `DueReminderSentAt`. Logic proven on a fake clock; the live Hangfire cron firing on the real stack → P17 (G-TRACE) |
| AC-055 | Background jobs | Partial | ActionReminderSweepTests (overdue owner notice + Once/DailyWhileOverdue rhythm + day-7-vs-8 secretary escalation + day-15 chairman + no-active-secretary) + CommitteeDirectoryTests (role resolution) | P8c-1: overdue owner notice (rhythm = system config, default DailyWhileOverdue) + one-shot escalation to the Secretary (>7d) and Chairman (>14d) via the role-aware `ICommitteeDirectory`; audited, idempotent. Live scheduled firing → P17 (G-TRACE) |
| AC-056 | Background jobs | Met | JobsMonitorMapperTests (counts / per-state rows / null-job / ordering / duration) + AdminJobsEndpointTests (Admin 200 shape + not-configured + 403 + 401 + wired live-counts + requeue 204/404/503) + JobMonitor.test + jobs.test (hook) + jobFormat.test | P8c-2: designed Administration → Job Monitor tab (operator fork B). `GET /api/admin/jobs` (Admin-only, `JobStorage` optional-from-DI → honest `Configured=false` fallback) projected by the pure `JobsMonitorMapper`; `POST /api/admin/jobs/{id}/requeue` = the Retry button (`IBackgroundJobClient.Requeue`, audited `admin.job.requeued`). FE tab = 5 tiles + recent-runs table + Retry (shared Table/StatusChip/Button), EN+AR+RTL, axe AA. Real-SQL boot confirmed the live `Configured=true` path; visual-verified EN-light + AR-RTL-dark |
| AC-057 | Aging | Partial | TopicApplicationTests + TopicHandlerTests (live SlaBreached) + Backlog.test (badge rendered) | Aging badge computed + rendered in the backlog UI (slaBreached-driven, unit-tested); live browser pass + SLA-breach notification → Notifications phase |
| AC-058 | Membership | Met | CommitteeMemberTests + MembershipFeatureTests | Deactivate → Disabled; name/email/role/attribution intact |
| AC-059 | Membership | Met | MembershipApiTests (all roles) + UsersMembership.test | Directory readable by every authenticated role; admin screen built |
| AC-060 | Search & Trace | Pending | — | Global search grouped results |
| AC-061 | Search & Trace | Pending | — | Arabic search via word-breaker |
| AC-062 | Search & Trace | Pending | TraceabilityTests (panel groups outgoing/incoming, "other" endpoint per direction, excludes inactive) + TraceabilityApiTests (`GET /api/traceability/{type}/{id}`) | **P10c: panel API in place + audited** (`GetArtifactRelationships`, one hop). Stays Pending → **P10e**: AC-062 asserts the FE *panel display*, which lands with the traceability panels UI. |
| AC-063 | Search & Trace | Pending | TraceabilityApiTests (Secretary creates edge → both source + target panels show it) + TraceabilityTests (create audits `Relationship.Created`) | **P10c: typed edge create + soft-delete + audit in place** (RBAC `Traceability.Link`). Stays Pending → **P10e**: AC-063 asserts the FE panel shows the new edge on both artifacts. |
| AC-064 | Dashboards | Pending | — | Committee dashboard live data |
| AC-065 | Dashboards | Pending | — | Secretary dashboard |
| AC-066 | Dashboards | Pending | — | Chairman dashboard |

**Summary:** 66 ACs · 14 Met (AC-001/002/008/031/035/039/040/042/045/046/047/050/058/059) · 20 Partial
(AC-003/005/006/007/009/010/011/012/013/015/016/030/032/033/034/043/048/049/057 + AC-041) · 32 Pending.
(Through P5b PR4 + the 2026-06-27 P5-review remediation, which corrected AC-043 Met→Partial — the kanban
keyboard move covers status, not the priority-ordinal reorder the AC specifies — + the S6b-1 E2E
reconciliation, 2026-06-30, which flipped **AC-035 Partial→Met** once its live HTTP leg landed.)

> **Test-hardening S1 (2026-06-29):** the AC→test mapping begins here. S1 adds **adversarial, failure-first
> backend coverage** (BE 89.1% → 97.6% lines, ADR-0016) for the Topics triage/edit handlers
> (Update/Defer/Prepare/Prioritize/Reject), the Meetings conduct/cancel/agenda-edit handlers
> (End/Cancel/RemoveAgendaItem) + their validators, and the Membership delegation validator — each asserting
> authz-deny, 404, domain status/immutability guards, and `AuditEvent` emission on real behaviour.
> **No verdict flips this slice:** per the standing G-TRACE rule an AC is `Met` only once its live HTTP/UI
> leg lands (→ P17), so S1 deepens the *evidence* behind the existing `Partial`/`Met` rows (AC-031/032/034/
> 035/043) without over-claiming. No business behaviour changed.

> **Test-hardening S2 (2026-06-29):** **adversarial, failure-first frontend coverage** of the auth + data
> layer (FE 83.74% → 94.83% lines; the whole S2 surface — `App.tsx`, `api/*`, `auth/*`, `LoginPage`,
> `AuthCallbackPage` — at **100% lines**, ADR-0016). 88 tests run the *real* OIDC providers, route/role
> guards, API client, and TanStack Query hooks against a stubbed `fetch` (the screen tests mock these away,
> so this is their first real exercise). This deepens the **client-side** evidence for **AC-001** (Keycloak
> claim→role mapping + auth-code/PKCE config now unit-tested) and the route/role gating noted in P3/P4
> (`ProtectedRoute`/`RequireRole`/route tree — UI gating, not authorization). **No verdict flips:** the live
> auth round-trip already carries AC-001 (P4/CHANGE-001); these are unit-level FE assertions and the
> automated UI-regression leg remains → P17. No product behaviour changed. Screen-state FE remainder → S4.

> **Test-hardening S3 (2026-06-29):** **adversarial backend coverage** that takes every backend file to
> **≥95% lines** (overall 97.6% → **99.6%**; Api assembly 94.7% → **100%**), so the per-file S7 gate can
> flip. 101 failure-first tests (458 → 559): HTTP round-trips over the real pipeline for the Topics/Meetings
> endpoints not previously exercised (defer/priority/update; agenda move/timebox/presenter; conduct
> attendance/discussion/actual-time; cancel — with 403/400 cases), plus domain/application/shared unit tests
> (Topic Close/Convert + events, TopicComment/TopicAttachment, MemberStreamAssignment/Delegation/
> CommitteeMember, AssignStreamsValidator, GetBacklog filter/sort branches, Notification, CurrentUserService,
> BaseEntity, LocalizedString, the ITopicScheduler seam). **No verdict flips** — the HTTP endpoint tests run
> the real authorization pipeline (deepening evidence for the Topics/Meetings workflow ACs) but the live
> end-to-end UI legs remain → P17. No exclusions added; no product behaviour changed.

> **Test-hardening S4 (2026-06-29):** **frontend screen-state coverage** taking every FE file to **≥95%
> lines** (global 94.83% → **98.46%**), so the per-file S7 gate is FE-ready. 121 tests (225 → 346) over the
> UI primitives (Pagination, Select/Dialog/Field/DateField/MultiSelect keyboard + edge paths), ErrorBoundary,
> PlaceholderPage, NotificationCenter states, meetingStatus, and feature gaps (SubmitTopic attachments/
> draft/autosave/submit-error; AgendaBuilder). Deepens FE-side evidence for the a11y/keyboard ACs but flips
> **no verdicts** — automated UI-regression remains → P17. **4 documented `/* v8 ignore */` comments**
> (comment-only, no behaviour change) cover genuinely browser-only paths jsdom can't run — @dnd-kit + native
> HTML5 **drag** (accessible Move up/down + click-to-add are unit-tested; drag → S6 E2E) and two defensive/
> unreachable guards. Both stacks now per-file-gate-ready (BE 99.6%, FE 98.46%).

> **Test-hardening S6b + S7 — E2E mandate complete + coverage gate live; AC reconciliation (2026-06-30):**
> S6a stood up the Playwright harness against the real compose stack (genuine Keycloak PKCE). **S6b** added
> the live functional E2E the InMemory/unit suites can never run — `core-loop.spec` (submit→accept→prepare→
> schedule→build→publish→notify→start→conduct→end, with the notify fan-out verified across two browser
> contexts), `dnd-and-failures.spec` (the S4-deferred native drag paths + failure-first authz/validation),
> and `rtl-a11y.spec` (live `dir=rtl` flip + axe AA on Backlog/Submit-Topic in EN+AR). **S7** wired the hard
> per-file ≥95%-lines coverage gate (FE+BE) into CI. **This is the slice where the long-standing "live HTTP/UI
> leg → P17" caveats finally land, so G-TRACE flips are now justified — but conservatively:**
> - **AC-035 Partial→Met** — the Accepted→Prepared transition's live HTTP leg now lands end-to-end in
>   `core-loop.spec` (UI accept → live-HTTP prepare → the prepared topic visibly flows into the live agenda
>   pool, is added + published). Per the project's own G-TRACE rule ("Met once the live HTTP/UI leg lands")
>   this is the one clean flip.
> - **Caveats closed, no verdict change** (evidence strengthened on already-`Met` rows): **AC-001** (auth.spec
>   = the automated UI-regression that was "→ P17"); **AC-044** (the "recommended live browser axe/RTL pass" —
>   native drag-reorder on a real browser + live axe AA); **AC-051** (the "standing cross-session browser
>   pass" — publish fan-out verified in two live contexts).
> - **Deliberately NOT flipped** (honest scope — these gaps are not what functional E2E closes): **AC-041**
>   stays Partial — it needs an automated **pixel-diff visual-regression** suite (RTL *mirroring* correctness),
>   which functional E2E does not provide; the new live `dir=rtl`+axe evidence strengthens it but is not VR.
>   **AC-034/043/048/057** stay Partial — their gaps are **unbuilt UI** (topic-edit 403 UI, the priority-
>   ordinal within-column reorder UI) or future-phase work (beforeunload native dialog; SLA-breach
>   notification), not a missing test the E2E supplies. **No product behaviour changed in this slice** — it is
>   docs-only AC reconciliation against evidence that already merged (PRs #44–#48).

> P4 grading rule (G-TRACE): an auth AC is **Met** only when fully demonstrable against aggregates/stores
> that exist in P4 (claim→role, 401, Membership directory + deactivation). ACs whose *mechanism* is built and
> unit-tested but whose end-to-end demonstration needs a not-yet-built aggregate (Topics P5, Actions P8,
> Votes P9, MoM P7), endpoint, or the immutable audit store (BL-066) are **Partial**, with the consuming
> phase named. This avoids over-claiming: the policy/handler/predicate is proven now; the live HTTP path
> lands with its module.
