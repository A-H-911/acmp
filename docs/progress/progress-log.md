---
artifact: progress-log
status: active
version: v1
updated: 2026-07-15
---

# ACMP Progress Log

Per-phase, dated log of execution progress. Keystone gate **G-PROGRESS**.
Newest entries on top. Each entry: what was done, decisions applied, what's next.

---

### 2026-07-15 — P15 (Research & Knowledge) design-fidelity + i18n audit remediation

**What was done.** Closed the enumerated INV-014 (design fidelity) / INV-009 (i18n) defects the P15 audit found
(the code was already correctness-green — BE/FE tests, coverage, ArchUnit, format all passed; AC-060/061 Met).
Five batches on `fix/p15-audit-remediation`; all gates re-run green.

- **B1 — tokens + i18n root.** `--serif: 'IBM Plex Serif'` token + new dep `@fontsource/ibm-plex-serif` (400/600)
  imported in `main.tsx`, scoped to LTR (`[dir="rtl"]` flips `--serif` → `--font-arabic`, so Arabic titles keep the
  Arabic sans) — the wiki serif title now renders (was falling to sans). `categoryLabel(c,t)` extracted to
  `wikiMeta.ts` and applied in `WikiReadingView` breadcrumb (was rendering the raw English category under Arabic).
- **B2 — research.** M4 descriptive subtitle (`research.subtitle`) added, the count moved to its own `mCount` line;
  **m5**: `UpdatedAt` added to `ResearchMissionSummaryDto` + projection + a new `"updated"` sort arm — the register's
  last column is now **Updated** (`updatedAt ?? createdAt`), matching the design's `cmUpdated` (was **Created**);
  m19 the mission `arrowRight` icons flip in RTL (`dir-flip`); m16 DMY dates; m2/m7/m8/m9 value-preserving
  token adoption in `research.css` (radius→`--chip-radius`/`--r-lg`, spacing→`--sp-4`).
- **B3 — wiki.** **M3**: a wiki-local full-width 7-icon formatting toolbar (Bold/Italic/Heading/List/Quote/Link/
  Cross-link) via an extracted pure `markdownInsert` util + a plain textarea — the shared `MarkdownEditor` (6 other
  surfaces) is untouched. **WK8**: minimal real draft autosave (debounced per-doc/per-lang localStorage, restored on
  reopen, cleared on save/cancel, "Draft autosaved" indicator). **WK10 (ADR-0029)**: the reading view's
  `TraceabilityPanel` replaced by a bespoke read-only `WikiLinkedArtifacts` card reusing the same relationship
  read-hook — **drops the wiki "Add relationship" affordance** (deliberate regression; wiki edge creation now only
  on routable detail pages). m15 History-button version chip → muted sans; m17 AR read-time renders Arabic-Indic
  digits (`ar-u-nu-arab`) + plural; m18 History ungated to all readers (Edit/Publish/Archive stay manager-only);
  m16 DMY; wiki copy force-matched to the mockup (search placeholder, empty-state).
- **B4 — templates.** m1 name-cell glyph `file`→`template`; m13 filtered-empty variant (distinct from
  no-templates-yet, with Clear filters); m16 DMY; reconciled the stale orphaned `admin.sub.templates` desc
  (dropped "decisions", which is not a target type post-OQ-051).
- **B5 — shared + search.** m22 search-hit status localized via a `(type)→status-namespace` map with raw fallback
  (Topics/ADRs/MoMs/Documents mapped; Decisions has no lifecycle-status i18n block → raw, documented); m6 the
  `/wiki` nav item force-matched to the design label "Knowledge"/"المعرفة" (was "Knowledge / Wiki"). **m20**: the
  `pencil` icon path was verified byte-identical to the design (dc.html lines 314/331) — no change needed.

**Decisions applied (locked pre-execution).** Force-match the mockup for shared-component/convention items; WK8 =
minimal real autosave; WK10 = full bespoke read-only card + **ADR-0029** (accepts reversing the blessed panel
substitution and dropping wiki edge-creation); m5 = add `UpdatedAt` to the summary DTO; DMY on P15 surfaces only
(a P15-scoped `formatDmy` helper, not an app-wide switch — logged as separate tech-debt). The exact text of the
un-recoverable low-severity minors (m2/m7/m8/m9 CSS tokens; m4/m11/m12 templates copy) was not preserved in the
package; reconstructed conservatively (value-preserving token adoption; the concrete stale-admin-desc reconcile).

**New shared units (≥95% per-file cov, all tested):** `lib/p15Date.ts`, `features/wiki/markdownInsert.ts`,
`features/wiki/WikiLinkedArtifacts.tsx`, `features/search/searchMeta.ts`.

**Gates (all green).** FE: 1050 tests, i18n parity 1768 keys, oxlint clean, `vite build` ✓, `test:cov` exit 0
(changed files 96.57–100%). BE: build 0 errors, `dotnet format --verify-no-changes` clean, per-file coverage
99.67% global (`check-coverage.mjs` exit 0). Live pixel-VR (isolated `-p acmpe2e` stack, **8/8 captures**):
**PASS** — element-by-element sign-off in EN-light + AR-dark on the enumerated §6 items: serif LTR title (AR
falls to Arabic sans, no broken glyphs), localized AR breadcrumb ("الحوكمة"), full-width 7-icon wiki toolbar,
WK10 "Linked artifacts" card (RTL-flipped), "Updated"/"آخر تحديث" register column, DMY dates ("15 Jul 2026" /
"١٥ يوليو ٢٠٢٦" Arabic-Indic), muted version chip, AR read-time ("قراءة ١ دقائق"), localized search status
("مُقدَّم"/"منشورة"), M4 subtitle. History-ungate (m18) is unit-tested (the VR user is a manager). No new
visual defects.

**Next.** PR (`fix/p15-audit-remediation`) → review → squash-merge. Design-update-owed: none new (WK10 matches
the `.dc.html`). ADR-0029 → operator ratification.

---

### 2026-07-15 — Register refresh: acceptance-audit + status-report reconciled to P15-complete

**What was done.** Docs-only reconciliation, no code. The `acceptance-audit.md` table body already carried
AC-060/061 as `Met` (P15f/g global search), but its **Summary rollup** and the status-report **Gate snapshot**
still read the pre-P15 counts ("34 Met · 3 Pending") — a self-contradiction. Reconciled both to the table +
verified evidence.

- **Rollup corrected** to **74 ACs · 36 Met · 37 Partial · 1 Pending · 0 Not-met** (was 34/37/3). AC-060/061 added
  to the Met list; Pending reduced to AC-004 alone (Keycloak idle-timeout, OQ-003).
- **Verified before asserting Met** (not a fresh full-suite run, per G-TRACE): the cited tests exist in-tree —
  `SearchApiTests`, `SearchPage.test` (AC-060), `SearchProvidersFtsTests` real-stack FTS (AC-061) — and the slice
  is merged green (`6c6ed95`/#118, all four CI checks incl. e2e). Green ref advanced `f32ca31` → `68d90d4`
  (through P15h).
- **Stale narrative fix.** Status-report "Latest slice" said P15h was "awaiting commit GO"; it is merged
  (`68d90d4`/#119). Corrected.
- Frontmatter bumped: acceptance-audit `updated` → 2026-07-15; status-report v1.7.4 → v1.7.5.

**Registers surveyed (no change needed, recorded for the gate).** Open-question register: **42 open** (Deferred,
default-applied) incl. **7 still-open PH-0 blockers** (OQ-003/020/024/030/031/032/038); 10 settled (incl. OQ-034
resolved by the P15f spike). Deferred-work register: **17 open** (D-02 in progress), 2 done (D-15, D-18). **D-11
(Tarseem) activates at the next slice, P14.**

**Next.** **P14 — Tarseem/Diagrams** (activates D-11), then hardening P16–P19. Residuals unchanged: D-19 flaky
`DecisionPage.test`; D-16/D-17 audit hardening → P16; AC-004 live realm policy; operator token rotation.

---

### 2026-07-15 — P15h Template pre-fill (FR-120) — the last P15 sub-slice

**What was done.** Wired the P15d/P15e template register into the four artifact-create surfaces so an author
can start from a template. **FE-only** — read-only reuse of the existing `GET /api/knowledge/templates?targetType`
+ `GET /{key}` seam; no backend, no new module/seam/migration. Branch `feat/p15h-template-prefill`.

- **One shared `TemplatePicker`** (`features/templates/TemplatePicker.tsx`, homed beside the P15e template UI and
  imported cross-feature like `TraceabilityPanel`): `useTemplates({ targetType, statuses:['Active'] })` → a
  `Select` of Active templates + a **Use template** button that reads the picked template's Markdown `Body`
  (`useTemplate(key)`) and hands it to the caller. Empty (no Active templates for the type) → renders nothing
  (INV-014 empty-state). Placeholders `{{…}}` ride through as literal editable text (FR-120 = pre-fill + edit; no
  fields-form).
- **Overwrite guard (advisor de-risk).** Apply is **disabled while the target field already holds content** — so a
  restored `SubmitTopic` draft (rehydrated from localStorage) is never silently clobbered. FR-120 is "pre-fill",
  not "replace". `// ponytail:` clear-to-switch is the ceiling; add confirm-to-replace only if it's ever needed.
- **Four wirings** (content field = the surface's existing single form-state string, mirrored en===ar via `loc()`
  only at submit — so single-Body→bilingual is a non-problem at the UI layer):
  Topic→`SubmitTopic.description`, ADR→`CreateAdrDialog.context`, MoM→`CreateMinutes.body`,
  Research Mission→`CreateMissionDialog.question`.
- **Field-mapping decisions (operator-blessed in GO).** FR-120 says "the description/content **field**" (singular)
  → each template fills the one primary content field per artifact. **ADR→Context** (the narrative field);
  **Mission→Question** (the create command's only free-form prose field — a research question is usually a
  sentence, so this is the deliberate mapping, not an oversight). Other ADR/MoM fields stay author-entered.
- **OQ-051 (Approved) note.** FR-119 says "Topics (by type)"; OQ-051 flattened topic templates to a single
  `Topic` targetType with no per-topic-type sub-filter, so the Topic picker offers all Topic templates regardless
  of the selected topic type. Forced by the approved data model, not a miss.
- **INV-014.** The "start from a template" affordance is absent from the create-flow `.dc.html` designs →
  no-reference composition from the shared `Select`/`Button` (design-update-owed, guardrail #14). RTL comes free
  from the shared primitives.
- **Tests.** `TemplatePicker.test.tsx` (lists/empty→null/apply-body/disabled-until-selected/disabled-while-loading/
  overwrite-guard). Each of the four surfaces stubs the child picker to a plain apply button and adds one test that
  clicks it and asserts the field received the body (proves the surface `onApply` closure; the real picker→body
  path is proven once in the picker's own suite).

**Gates (all green).** FE only (no BE change): `tsc -b && vite build` clean; `oxlint` clean (only pre-existing Toast/
MarkdownView fast-refresh warnings); **i18n parity 1754** (new `templates.picker.*` EN+AR); per-file coverage gate
`check-coverage.mjs` exit 0 — changed files TemplatePicker 100 / CreateAdrDialog 100 / CreateMissionDialog 100 /
MeetingMinutes 95.72 / SubmitTopic 99.46. **FR-120 → Met** (covered by the module suite; no dedicated AC, per its
traceability row). Live pixel-VR not required (no-ref affordance, functional EN+AR only).

**Next.** ★ **P15 COMPLETE** ★ → P14 Tarseem/Diagrams (+ the deferred wiki version-diff) → P16–P19 hardening.
Residuals unchanged (search result-status localization + no-ref `/search` design; FR-115 pre-P15c source-edge
backfill; D-19 flaky DecisionPage.test; Webex/ngrok token rotation; AC-070 prod live-confirm; AC-004 KC idle policy).

---

### 2026-07-15 — P15f/g Global Search (FR-143/144/145/118, AC-060/061; OQ-034 resolved)

**What was done.** Global search across the platform, backend + frontend, on branch `feat/p15f-search-backend`.

- **OQ-034 spike (the PH-0 blocker, finally run).** The stock `mssql/server:2022-latest` image ships without
  Full-Text Search (PH-0 2026-06-25 finding); built a derived `deploy/Dockerfile.sqlserver` (base +
  `mssql-server-fts`), confirmed `IsFullTextInstalled=1` and the **Arabic word-breaker (lcid 1025)** is present,
  and measured **82.9% micro-recall** over 20 committee-term queries (misses Arabic *derivation* — عمارة↔معماري,
  بحث — which a LIKE booster recovers). **Operator ruling: SQL Server FTS + LIKE booster — single datastore,
  INV-002 NOT triggered, no OpenSearch, no ADR.** OQ-034 → Resolved; ph0-validation §7 updated.
- **Backend (P15f).** New `ISearchProvider` cross-module read seam in `Acmp.Shared/Contracts/Search` (+ `SearchHit`,
  `SearchExcerpt`), the `IDecisionReader`/`ITraceabilityReader` precedent (ADR-0001) — 5 per-module impls (Topics,
  Decisions, ADRs→Governance, MoMs→Meetings, Documents→Knowledge), each querying ONLY its own FTS-indexed columns:
  `EF.Functions.FreeText(*_ar,1025) ∪ FreeText(*_en,1033) ∪ LIKE`, degrading to LIKE alone on the InMemory test
  provider. Inline `SearchEndpoints` coordinator (Acmp.Api) fans out **sequentially** (all modules share one
  per-scope `DbConnection`, ADR-0026) and groups by artifact type; any authenticated role (US-078). 5 guarded FTS
  migrations (`IF SERVERPROPERTY('IsFullTextInstalled')=1` + `suppressTransaction` + `EXEC` + per-module catalog
  `ft_*`) — NO-OP on the stock `SqlBackstopFixture` image, build only on the FTS image. `deploy/Dockerfile.sqlserver`
  wired into compose (⚠ switching a running stack needs `down -v`).
- **Frontend (P15g).** `/search` page (was a placeholder; the top-bar box + ⌘K already route to `/search?q=`) —
  `api/search.ts` `useSearch` hook + `features/search/SearchPage.tsx` grouped results (ID/title/excerpt/status/deep
  link) with prompt/loading/error/empty states. No-reference composition (INV-014 — no search `.dc.html`). i18n
  `search.*` EN+AR.
- **Tests.** `SearchProvidersFtsTests` (Docker-gated, real FTS SQL Server: AC-061 Arabic word-breaker match +
  all-5 providers execute + seeded projections) · `SearchApiTests` (coordinator/grouping/deep links, HTTP) ·
  `SearchProviderGuardTests` + `SearchExcerptTests` · `SearchPage.test`/`search.test` (FE).

**Gates (all green).** BE: build; Domain 241/Arch 49/Application 860/Integration 36/Api 229 (0 fail); coverage
**99.66%** (no file <95%); `dotnet format` clean. FE: 134 files/1023 tests; `tsc && vite build`; i18n parity 1750.
**AC-060/061 → Met.** Reconciliations: cross-type result status shown as the raw enum name via a neutral chip
(localization owed); Topics FTS single mixed-language column indexed 1025 (LIKE covers English).

**Next.** Live pixel-VR smoke on the isolated `-p acmpe2e` stack (owed, INV-014 no-ref) → **P15h** template
pre-fill (reads the `targetType` seam) → P14 Tarseem/Diagrams + version-diff → P16–P19.

---

### 2026-07-14 — P15e Knowledge UI: wiki + template management (FR-116/117/119)

**Done (all gates green + live pixel-VR PASS, awaiting commit GO)** — branch `feat/p15e-knowledge-ui`, plan
`~/.claude/plans/elegant-imagining-tome.md` (operator-approved after a devil's-advocate + advisor de-risk pass;
4 forks decided by the operator). The FE for the P15d Knowledge backend (both merged #115/#116), one combined
slice, built in two internal stages (wiki, then templates). The `MarkdownView` foundation + API modules were
built + verified by the lead; the ~25 mechanical UI files by a general-purpose subagent, then **every gate
independently re-run by the lead** (not sub-agent trust).
- **New dep:** `marked@18` + `dompurify@3.4.12` (0 vulns; both self-typed — no `@types/*`). New shared
  `components/ui/MarkdownView.tsx` = `DOMPurify.sanitize(marked.parse(md))` with an **ALLOWLIST** of markdown
  tags + link `rel=noopener` hook (react/security.md; sanitize at the call site). 7 tests incl. the cure53
  XSS-strip assertion (script/on*/`javascript:`/style/iframe stripped). Not an INV-002 concern (an npm render
  lib, not architecture).
- **Wiki** (`features/wiki/*`, route `/wiki` + `/wiki/:key`): a 260px category **tree** (`Category` grouped
  client-side into "spaces", operator-chosen bridge) + reading view (serif title, author resolved via
  `useMembers` join on `keycloakUserId`, read-time, tags, `MarkdownView` body, `TraceabilityPanel` links) + a
  split markdown/preview **editor** + a **version-history** panel (from `Versions[]`; diff→P14) + a create
  dialog. **Tree visibility rule** (no-ref): Published→all, Draft→managers only (dot-marked), Archived excluded.
  Lifecycle affordances the design omits (New page / Publish / Archive / status badge) added as no-ref
  compositions, manager-only.
- **Templates** (`features/templates/*`, standalone flat `/templates` in the Knowledge nav group — NOT the
  design's admin-only home, since Chair/Sec are the managers): the Administration `.gTpl` table (real backend
  enums Active/Deprecated + Topic/Adr/MinutesOfMeeting/ResearchMission) + a no-ref create/edit form (TargetType
  Select disabled on edit — immutable) + a **Deprecate** row action. Removed the honest-empty Admin `templates`
  tab (`AdministrationPage.tsx`).
- **Gates (independently re-verified):** `npm run build` clean, `npm run lint` (oxlint) no errors, **1016 tests
  pass** (132 files) with per-file ≥95% coverage, `check-i18n` parity OK (1736 keys; every enum value keyed in
  EN+AR). **Live pixel-VR PASS** — `e2e/p15e-knowledge-vr.spec.ts` captured wiki reading/editor + template
  table × EN-light + AR-dark on the isolated `-p acmpe2e` stack, human-compared pixel-faithful to
  `ACMP Research & Knowledge.dc.html` + `ACMP Administration.dc.html`.
- **FR-116/117/119 → Met (UI).** OQ-052 logged (tree Draft/Archived visibility default). Flags: History+status
  gated to managers (could relax to all readers — minor); TraceabilityPanel substitutes the design's compact
  linked-artifacts card (richer, also creates edges). Next = **P15f/g** global search (OQ-034 Arabic
  word-breaker spike FIRST + INV-002) → **P15h** template pre-fill (reads the `targetType` seam).

---

### 2026-07-14 — P15d-2 Knowledge backend: Template (FR-119)

**Done (all gates green, awaiting commit GO)** — branch `feat/p15d2-knowledge-template`, plan
`~/.claude/plans/nothing-clear-for-me-steady-cerf.md` (operator-approved; devil's-advocate pass + advisor
confirmation on OQ-051 and a Body-type correction). Second of the two GO-gated P15d PRs; a **mirror** onto the
P15d-1 scaffold — no new module wiring (Template rides the existing `KnowledgeDbContext` + `Knowledge.Application`
assembly, so **no** MigrationRunner / composition-root / ArchUnit / factory change; ArchUnit stayed 49→49).
- **Template** (TPL-, `Active → Deprecated`, `byte[] RowVersion` → 409): `Name` bilingual (LocalizedString,
  mirrored en===ar), `Body` a **single Markdown string** (only Name is bilingual — domain-model §403 marks Body
  plain, and en===ar makes a 2nd body locale pointless; advisor-caught over-mirror of Document), `TargetType`
  enum, `Version` a **plain counter** (no snapshot history — FR-117 versioning is a Document-only concern; FR-119
  asks only for edit). `Create`→Active v1, `Edit`→Version++ (Name+Body; TargetType immutable), `Deprecate`→
  terminal soft-delete (permanent retention).
- **OQ-051 resolved → Approved:** `TargetType` = FR-119's set `{Topic, Adr, MinutesOfMeeting, ResearchMission}`,
  not the §403 sketch `{…, Document, Action}` — requirement is the contract (LLM01); §403 was self-contradictory;
  Document/Action have no FR (YAGNI, string enum = addable later with no migration). domain-model §402-404
  reconciled in-PR; spelling mirrors Traceability `ArtifactType`.
- **CQRS/endpoints:** `CreateTemplate`/`EditTemplate`/`DeprecateTemplate`/`GetTemplateByKey`/
  `GetTemplatesRegister` (+ **`targetType` filter — the P15h seam**) → `/api/knowledge/templates/*`; mutations
  `Policies.TemplateManage` (already registered — Chair/Sec/**Admin**), reads committee-wide. Enriched audit
  (`Knowledge.Template*`). **RBAC backstop** `KnowledgeRoles.TemplateManage` **includes Administrator** (unlike
  DocumentManage) to match its matrix row — else a valid Admin passing the endpoint policy is wrongly 403'd at the
  MediatR boundary. Migration `Knowledge_AddTemplate` (one `templates` table, same DbContext — no counter-schema
  change; TPL- rides the per-prefix `knowledge_key_counters`).
- **Gates (independently re-verified):** build clean; **Domain 241 (+8) / Architecture 49 / Api 226 (+7) /
  Application 854 (+9)** all pass; `dotnet format` clean (BOM + file-scoped-namespace on the generated migration);
  coverage — **no Knowledge/Template file <95%** (the 6 flagged are the pre-existing Docker-gated shared infra,
  green on CI), global 98.69%. Admin-can-create proven at the API (the one behavioural difference from documents);
  409 via terminal-state conflict (edit/deprecate a Deprecated template — InMemory can't enforce rowversion; real
  409 is SQL-enforced). **FR-119 → Met (backend).** Next = **P15e** Knowledge UI (wiki + template management,
  INV-014 live pixel-VR) → P15f/g global search (OQ-034 + INV-002) → P15h template pre-fill (reads the targetType
  seam).

---

### 2026-07-14 — P15d-1 Knowledge backend: Document (FR-116/117)

**Done (all gates green, awaiting commit GO)** — branch `feat/p15d1-knowledge-document`, plan
`~/.claude/plans/nothing-clear-for-me-steady-cerf.md` (operator-approved after a devil's-advocate pass).
First of two GO-gated P15d PRs (Template = P15d-2). New **Knowledge** module (schema `knowledge`, 3 projects
mirroring Research), one aggregate now: **Document** (DOC-).
- **Document** (`Draft → Published → Archived`, `byte[] RowVersion` → 409): `Title`/`Body` bilingual
  (LocalizedString, mirrored en===ar), `Category` (free-text, FR-116 — domain-model omits it, added per the
  requirement), `Tags`, `OwnerUserId` (Keycloak-sub **attribution only** — a Document isn't topic-scoped so,
  like ADRs, Chair/Sec are the effective writers; OQ per-doc owner enforcement). **FR-117 versioning** = an
  **owned immutable `DocumentVersion` snapshot** (Version, Title, Body, SavedAt, SavedBy) appended on every
  content save (Create/Edit → `Version++`); Publish/Archive change status only (audited, no new version).
- **CQRS/endpoints:** `CreateDocument`/`EditDocument`/`Publish`/`Archive`/`GetDocumentByKey`(+versions)/
  `GetDocumentsRegister` → `/api/knowledge/documents/*`; mutations `Policies.DocumentManage` (already
  registered), reads committee-wide. Enriched audit (`Knowledge.Document*`) in the shared ambient tx +
  `sp_getapplock` (ADR-0028) via the shared `DbConnection` + `.AddAcmpAuditInterceptors`.
- **Registrations:** ★ `KnowledgeDbContext` in `MigrationRunner` (the P11a-bug gate), `AddKnowledgeModule` +
  MediatR assembly in `AcmpCompositionRoot`, `MapKnowledgeEndpoints` in `Program.cs`, ArchUnit boundary
  rules, InMemory swap in `AcmpWebApplicationFactory`; migration `Knowledge_Init` (schema `knowledge`:
  documents + document_versions + key_counters).
- **Gates (independently re-verified, not sub-agent trust):** build clean; **Domain 233 / Architecture 49 /
  Api 219 / Application 845** all pass; `dotnet format` clean; coverage — **no Knowledge file <95%** (the 3
  files flagged are pre-existing shared infra covered by the Docker-gated Integration suite, green on CI).
- **Decisions/flags:** `TargetType` conflict is P15d-2 (Template). **OQ-051** (FR-119 vs domain-model §403
  target types) + the Category doc-gap logged. RowVersion 409 proven via terminal-state conflict at the API
  (InMemory can't enforce rowversion — the Research precedent); real rowversion 409 is SQL-enforced. Next =
  **P15d-2 Template** → P15e Knowledge UI.

---

### 2026-07-13 — P15c-2 Research convert frontend (W16, FR-113/114/115)

**Done** — branch `feat/p15c2-research-convert-frontend`, plan `~/.claude/plans/nothing-clear-for-me-steady-cerf.md` (operator-approved after a devil's-advocate pass). **Frontend-only** — no backend/DB change; wires the SPA against the P15c-1 API surface (#113).
- **Convert flow:** new `ConvertToTopicDialog` (research-style dialog — no-reference composition, guardrail #14) — a pre-filled topic form → `POST /api/topics/from-research` → navigate to the new `TOP-`. Header **"Convert to execution topic"** button shows only on a **Completed** mission (backend needs Completed; the design mockup shows it on Active — behaviour wins, flagged INV-014). A per-recommendation **Convert** on each **Accepted** rec.
- **FR-114 panel:** the shared `TraceabilityPanel` (P10e/P11e) mounted on the mission in a two-column `1fr 320px` grid (mirrors the six other detail pages), pointed at `ResearchMission` — lists the source + produced topics, each navigable.
- **FR-115 xref:** the header "Linked topic" becomes a navigable `TOP-` link from the incoming `Topic→Mission` edge; **falls back** to the plain P15b indicator for older missions (create-time edge only — backfill still deferred).
- **Converted chip:** **edge-driven** (reads the rec's own outgoing `Informs→Topic` edge), so it shows "Converted → `TOP-`" and self-heals even if the best-effort `mark-converted` POST failed; the mark-converted call is fire-and-forget (non-fatal).
- **FR-113 open-graph:** added `ResearchMission` to `GRAPH_FOCUS_TYPES` + a `useMission` cold-resolver in `ImpactGraphPage` → the panel's "Open dependency graph" works warm **and** on refresh; Research node styling already present.
- **Reuse:** extracted `TokenInput` from `SubmitTopic` into `components/ui/TokenInput.tsx` (two callers, one copy).
- **Gates:** 939 FE tests green (+ new dialog/api/graph/mission specs), coverage **99.64%** (new files 100%, MissionPage 99%), i18n EN+AR parity (1640 keys), oxlint clean, `tsc -b && vite build` clean.
- **Live pixel-VR = PASS** (`e2e/p15c-convert-vr.spec.ts`, isolated `-p acmpe2e` stack, EN-light + AR-dark RTL): mission detail (convert button + xref + converted/accepted recs + panel), the convert dialog (pre-fill), and the impact graph focused on the mission (RMS focus ↔ source topic upstream). Pixel-faithful to `ACMP Research & Knowledge.dc.html`.
- **FR-113/114/115 → Met.** ★ **P15c COMPLETE** (backend #113 + this frontend). Next = **P15d/e Knowledge** (wiki + templates) → **P15f/g** global search (OQ-034 + INV-002 go live) → **P15h**.

---

### 2026-07-13 — P15c-1 Research convert backend (W16, FR-113/115)

**Done** — branch `feat/p15c1-research-convert-backend`, plan `~/.claude/plans/kind-watching-hinton.md` (operator-approved after a devil's-advocate pass). Target-owns architecture (mirrors P11e / ADR-0021); **no new ADR** (covered by ADR-0001/0019/0020/0021).
- **Convert (Topics module):** `POST /api/topics/from-research` → `ConvertResearchToTopicCommand` (Chairman/Secretary). Reads the source via a new **`IResearchReader`** seam, creates the Topic natively (`Topic.Draft`+`Submit`), writes a reverse **`Informs`** edge (Research→Topic) via `ITraceabilityWriter`. Mission→Topic (mission must be `Completed`) + Recommendation→Topic (rec must be `Accepted`).
- **Atomic double-convert guard:** new `Topic.SourceRecommendationId` (opaque value id) + **filtered unique index** (`IX_topics_SourceRecommendationId … IS NOT NULL`), migration `Topics_ConvertProvenance`. App-level 409 self-heals the edge (P11e); the index is the concurrent backstop (proven in `TopicConvertGuardTests` on real SQL — InMemory ignores it).
- **Recommendation Converted:** `RecommendationStatus.Converted` + `Recommendation.MarkConverted` + `MarkRecommendationConvertedCommand` + `POST /api/research/{id}/recommendations/{recId}/convert` (best-effort display disposition; the index is the real guard).
- **FR-115:** `CreateMissionHandler` emits a `Topic → ResearchMission` `References` edge when `SourceTopicId` is set (via new **`ITopicReader`** seam). *(Backfill for existing missions + `UpdateMissionDraft` edge deferred to a fast-follow.)*
- **FR-113 (graph nodes) verified free:** `GetArtifactRelationshipsQuery` is artifact-type-agnostic and Research kinds are already `ArtifactType` values — Research nodes appear/expand once edges exist (no new graph reader). FE open-graph affordance + node styling → P15c-2.
- **Gates:** 1339 BE tests (Application +15, Integration +2, Api +1 flow), ArchUnit 45 (new Research/Topics cross-module seams boundary-clean), coverage **99.64%**, `dotnet format` clean, build clean.
- **FR-113/115 → Met (backend); FR-114 → Partial** (linked-artifacts panel is P15c-2). Next = **P15c-2 frontend** (convert dialog + FR-114 panel + xref + converted chip + open-graph + live VR).

---

### 2026-07-13 — D-18 audit-append concurrency FIXED via serialization (ADR-0028)

**Done** — branch `fix/d18-audit-append-concurrency`. Resolves the race the P15b VR reproduced. The fix arc is
itself the story of the review discipline working:
- **Failing test first** (operator's approach): `Concurrent_commands_all_commit_without_forking_the_audit_chain`
  fires **8 concurrent same-tx commands** (distinct votes, one shared chain) through the full pipeline on real SQL.
- **First pass (bounded retry)** fixed the real **2-way** race, but CI then showed the 8-way case **deadlocks
  (SQL 1205)** — a deadlock victim's transaction is rolled back, so in-place retry cannot recover. Frontend CI on
  the same push failed on a **pre-existing, unrelated `DecisionPage` flake** (logged separately, not D-18).
- **Devil's-advocate review** (operator-requested) killed a sink-level app lock too: handlers are not uniform —
  `VerifyAction`/`IssueDecision`/`ConductMeeting` **emit-then-write-more**, so a lock taken at the audit append
  inverts `{lock, module-rows}` order across handlers → a new cross-handler deadlock. `UPDLOCK,HOLDLOCK` rejected
  (serializable-insert deadlock). Conclusion: safe serialization must live at **tx-open**, not the sink — a
  P16-sized, ADR-worthy core change. Operator chose to do it now.
- **Fix (ADR-0028 — serialize at tx-open):** `AmbientTransaction.EnsureStartedAsync` takes a **transaction-scoped
  exclusive `sp_getapplock`** ('acmp-audit-chain') right after `BeginTransaction`, before any module write. Every
  audited write-command acquires the chain lock first (one consistent order → no cross-handler deadlock) and
  appends serialize (no fork/2601, no 1205). Held to commit ⇒ next command reads a committed tip; `THROW` on
  lock-timeout ⇒ fail-closed; SQL-Server-only (no-op elsewhere). The **denial/autocommit path** (no ambient tx)
  keeps the bounded retry (broadened to the `PreviousHash` **or** `Hash` index; deadlock-free), covered by a new
  `Concurrent_denials_…` test.
- **Consequence (accepted, in ADR-0028):** audited write-commands now serialize globally — negligible at ≤20
  users; partition per-stream only if throughput ever bites.
- **Gates (local):** 27 Integration tests green (both concurrency tests + same-tx atomicity suite). Full BE
  suite + coverage + CI (where the 1205 first surfaced) to be re-proven on the push. Plan:
  `~/.claude/plans/d18-audit-append-concurrency-plan.md`. D-18 → **Done**; ADR-0028 Accepted.

---

### 2026-07-13 — P15b live pixel-VR (PASS) + audit-append race reproduced (D-18)

**Done**
- **P15b live VR — PASS** (PR #110, `test/p15b-research-vr`). Ran `e2e/p15b-research-vr.spec.ts` on the isolated local-authority stack (`-p acmpe2e`, `.env.example`) and screenshot-compared **register + mission detail** in **EN-light + AR-dark (RTL)** against `ACMP Research & Knowledge.dc.html`. Pixel-faithful: type/status chips (`Research mission` / `In discovery` · `Proposed`), Findings/Recommendations section cards with confidence·`Unverified`·`Verify` and priority·`Proposed`·`Accept`/`Reject`, EN title with AR mirror beneath, RTL sidebar/column flip + dark theme all correct. Guardrail-#14 design-only omissions (Hypotheses/Acceptance-criteria, Sources aside, register Topic column, Convert/Import) confirmed absent. **P15b ACs stand with live visual evidence.**
- Spec hardened on the branch (anti-flake): post-login `page.waitForLoadState('networkidle')` before the seed burst, and `OUT` → `e2e/vr-out/` to match the run doc.

**Finding — D-18 (logged, not fixed here)**
- The VR **reproduced the ADR-0009/0026-accepted audit-append concurrency race**: `SqlAuditSink` takes no app-lock, so the SPA's login-time `Membership.ProfileSynced` append and the test's `Research.MissionProposed` both chained off audit tip `1f738021` → the loser hit the `PreviousHash` UNIQUE index (SQL 2601) → 500. Pre-existing, all-write-paths, fail-closed. Fix path (bounded retry on 2601 in `AppendAsync`, or `sp_getapplock`) is a **plan-first / GO-gated P16 hardening slice** — **not** patched in this VR PR. See **D-18** in the [deferred-work register](../execution/deferred-work-register.md).

---

### 2026-07-13 — P15a + P15b (Research module: backend + UI)

**Done**
- **P15a** (PR #107, `7d82aba`): new **Research** module (schema `research`) — `ResearchMission` (RMS-) + owned `Finding` (FND-) + `Recommendation` (REC-); manual CRUD/lifecycle (`Proposed→Active→Completed` / `Cancelled`, terminal-immutable), **INV-005 enriched audit** (`ResearchDbContext` on the shared `DbConnection` + `AddAcmpAuditInterceptors`), `Research.Manage` RBAC, migration `Research_Init`, endpoints. 1320 BE tests, cov 99.65%, ArchUnit Research-isolation fact.
- **P15b** (PR #108, `c06dc85`): FE-only `/research` register + `/research/:key` detail + create/manage dialogs, mirroring the Risks slice; **role + status gating** (Chair/Sec via `hasRole`; mission must be `Active` for child mutations; Completed/Cancelled read-only). 920 vitest, per-file cov ≥95%, i18n parity 1618. Built to `ACMP Research & Knowledge.dc.html` (INV-014).
- **PR #106** (`186f24c`): governance-register refresh + Keystone-import deferral (FR-112 → D-05) + **OQ-050** + **D-17**.

**Decisions / reconciliations**
- Keystone import (FR-112) deferred out of P15 → future (D-05); **no ADR** (DEC-016/ADR-0007 optional-companion decision intact — scheduling only).
- RBAC **OQ-050**: `Research.Manage` (#26) is **A-only (Chair/Sec)** for missions — the shared `CapabilityHandler` grants AiO only for `ITopicScopedResource`, and missions aren't topic-scoped (same as ADR/Invariant); `OwnerUserId` = attribution, not authz. Member-owned governance authoring would be a cross-cutting authz change.
- Design-only omissions (guardrail #14): Hypotheses / Acceptance-criteria sections, Sources aside, Convert (→P15c) / Import (→D-05) buttons; register Topic/Sources columns. **Design-update-owed.**
- Child-op audit before/after enrichment absent (children are `BaseEntity`); audit **is** emitted (INV-005 holds) → **D-17**.

**Next**
- **P15c** — Research convert flows (Mission→Topic, Recommendation→Topic/Decision, W16) + the deferred FR-113/115 traceability graph edges (needs a key+title reader seam). Then Knowledge (P15d/e), global search (P15f/g — OQ-034/INV-002 live), template wiring (P15h).
- ~~**P15b live pixel-VR** (EN-light + AR-dark, RTL)~~ — **DONE 2026-07-13 (PASS)**, see top entry + PR #110.

---

## Audit slice — MERGED to `main` (PR #105)

**2026-07-12.** The whole audit slice (PR0 ADRs → PR4 UI, 21 commits) squash-merged to `main` as `f32ca31`. All four CI checks green: backend (3m31s), frontend (1m24s), **e2e (4m18s)**, compose. The e2e pass is the first end-to-end proof that all 11 module contexts boot on the shared `DbConnection` — validating the PR1 `PersistSecurityInfo=true` fix that CI-only could confirm. One CI-surfaced fix landed on the branch before merge: the generated `Audit_Enrich` migration needed a **file-scoped namespace** (IDE0161) to pass `dotnet format` on Linux — a gate the format check enforces that the local run's known-artifact allowance had masked. `main` is green and deployable. **AC-017/018/019/020 → Met.** Residual (P16, logged): DB-permission immutability + nightly Hangfire verify job; post-merge operator production live-confirm of `/audit`.

---

## Audit slice PR4 — `/audit` register UI — branch `feat/audit-infra`

**2026-07-12.** The Auditor-facing screen that closes the slice visually: a read-only register over the immutable log, built to the `ACMP Lists & Registers.dc.html` audit trail (`isAudit`, `gAudit` 5-col grid), read directly per INV-014.

- **`features/audit/*` + `api/audit.ts`** (cloned from `features/risks/*` + `api/risks.ts`): `AuditRegister` composes the shared kit (Table, FilterChip, StatusChip, states, Pagination, Icon). Columns Timestamp (mono, `dir=ltr`, Gregorian/Latin-digit) · Actor (avatar + sub + role, or "System") · Action (verb tone chip) · Artifact (localized CLR type + short subjectId) · Detail (localized Outcome). Read-only header chip + card-footer banner; **rows are not links** (append-only, no drill-in).
- **FE RBAC fix (ADR-0027):** `App.tsx` route-gates `/audit` → `['auditor','chairman','secretary']` (was `['administrator','auditor','chairman']`); `navModel.ACCESS.audit` drops `administrator`, adds `secretary`. FE gate now matches the API policy exactly; the `navModel` test that asserted the admin sees audit was flipped (admin sees admin, NOT audit) + a new auditor-sees-audit case added.
- **Honest design↔data reconciliations (flagged, not invented):** the mock's *Export log* button is dropped (no export endpoint — follow-up); of its 4 filters only **Artifact type** (→ `entityType`, over the 13 governed CLR names) is wired, Actor/Action/Date render as inert parity chips (need an actor directory / action catalog / date picker — same call as the risks Owner chip); Detail shows the localized Outcome because the mock's narrative sentences + human artifact keys aren't reconstructable from the structured row without a cross-module key lookup. **Design-update-owed** logged (the mock shows no loading/empty state and no verify affordance; the API's `GET /api/audit/verify` has no UI surface this slice).
- **i18n:** new `audit.*` block EN+AR (parity-gated, 1521 keys). RTL via logical properties.
- **Gates:** vitest 114 files green (new `AuditRegister.test` 8 + `auditMeta.test` 11 + `api/audit.test` 2), per-file **lines 100%** on all new FE files; tsc/oxlint/`vite build`/i18n parity all clean. **Live pixel-VR (EN-light + AR-dark) attempted, BLOCKED — not completed. The blocker surfaced a likely real deployment defect, NOT a PR4 issue.** The VR spec (`e2e/audit-vr.spec.ts`: Secretary login — an authorized reader per ADR-0027 — seeds prepared topics + a meeting for artifact/verb variety, screenshots both modes) is written and ready. On a **fully clean** `acmp`-project `.env.example` stack (volumes wiped) the API repeatedly `exit 139`'d on `Login failed for user 'sa'` (err 18456) during migration → `web` (nginx SPA :8088) never starts → Playwright global-setup times out. **Root-caused by elimination (evidence, not assertion):** (1) the API's runtime `ConnectionStrings__Acmp` env is exactly correct (`docker inspect`); (2) SQL Server accepts that exact `sa` password both locally (`sqlcmd`) AND **over the docker network** from a separate container on `acmp_default`; (3) the failure is **password-independent** — reproduced identically after overriding `MSSQL_SA_PASSWORD` to a `#`-free value (an earlier `#`-escaping hypothesis was DISPROVEN); (4) it is not a fresh-volume readiness race — an API restart with SQL fully ready fails identically. So a peer container authenticates fine but the **API's own `Microsoft.Data.SqlClient` connection cannot** — a **structural** auth failure. **RESOLVED + VR PASSED (2026-07-12).** Fix applied: `PersistSecurityInfo = true` on the shared connection (`SharedKernelExtensions.cs`) — a shared `SqlConnection` *instance* masks its password out of `.ConnectionString` once opened (`Microsoft.Data.SqlClient` default), so EF's fresh-DB `SqlServerDatabaseCreator` derived a password-less `master` connection → `18456`. After the fix the full stack boots clean (all containers healthy, migrations applied, `:8088`→200, **zero** `18456`). **Live pixel-VR captured + reviewed** (`e2e/audit-vr.spec.ts`, `vr-out/pr4-audit-register-{en-light,ar-dark}.png`): the `/audit` register is faithful to the `.dc.html` audit trail — H1 + event count + warn-tone read-only chip; 5-col table (Timestamp mono/ltr · Actor avatar+role · Action verb-tone chip · Artifact type+short-id · Detail Success chip); read-only footer banner; pagination; newest-first; rows-not-links. AR-dark: full RTL mirror + dark theme + localized entity types (موضوع/اجتماع/عضو) + Arabic month with Latin digits (INV-009). **Minor follow-ups (non-blocking):** actor/artifact render as the stored KC-sub/PublicId GUIDs (the mock's friendly names/keys need a sub→name + id→key lookup — deferred); the header/`Showing` counts can be off-by-one under a concurrent append (endpoint's `Count` + page are two queries — snapshot them if it matters). **Owed-before-merge items — now DONE (2026-07-12):** (a) **Regression test** added (`SharedConnectionWiringTests.Shared_connection_persists_security_info_...`) asserting the shared connection carries `PersistSecurityInfo=true`, Docker-free, with a comment naming the 18456-at-boot failure it guards; **BE gates re-run green with the fix** — Domain 188 / Arch 41 / App 806 / Integration **25** (+1) / Api 203, `dotnet format` clean, coverage 99.66%. (b) **Dockerfiles sped up + de-crashed**: new `deploy/Dockerfile.backend` — ONE shared build stage so the kernel + all 34 projects compile once (not twice) across api+worker, plus `COPY --parents` csproj-first restore-layer caching; compose points both services at it via `target:`; old `Dockerfile.api`/`Dockerfile.worker` deleted; verify-build green (both images built, `--parents` labs syntax valid). This removes the dual-full-compile that saturated + crashed the WSL2 daemon.

**[SUPERSEDED by the resolution above — kept for provenance.] Originally CONFIRMED cause: PR1's shared-`DbConnection` rewrite** (`SharedKernelExtensions.cs:36-40` — `new SqlConnection(new SqlConnectionStringBuilder(GetConnectionString("Acmp")){ MARS=true }.ConnectionString)`). **Decisive A/B on the SAME machine + SAME `.env.example` password:** `main` (pre-PR1) boots fully clean (api/web/worker healthy, `:8088`→200, **zero** SQL login errors); `feat/audit-infra` (with the shared connection) fails as above. So it is a real regression, NOT an environment quirk. It escaped because Testcontainers (integration green) never used a `.env.example`-shaped credential AND its fresh-DB path differs, and **CI never boots the full compose stack** — this is **precisely what the "owed push-time full-stack CI e2e" gate exists to catch** (PR1/PR2 close-out note + [[audit-slice-literal-ac017]]). The failure is on the fresh-DB creation path: EF's `SqlServerDatabaseCreator.CreateAsync` opens a `master` connection derived from the shared `SqlConnection` and the `sa` login fails there (18456) — a `Persist Security Info`/shared-instance interaction (fix confirmed above: `PersistSecurityInfo=true` on the shared connection; the shared-instance `.ConnectionString` masks the password once opened). **This WAS a PR1 merge-blocker; it is now RESOLVED (fix + regression test + green BE gates + live VR, per the resolution above).** PR4's own gates are green and independent of this; the VR is captured once the boot is fixed. (Live-VR investigation cost was heavy + wiped the operator's dev `acmp` volumes with consent; dev needs re-provisioning.)

**Next:** slice complete pending live VR + the operator's push-time full-stack CI e2e. The queued Governance lifecycle-buttons follow-up and the deferred P16 audit hardening (DB-perm immutability + nightly verify job) remain out of scope.

---

## Audit slice PR3 — Auditor read + on-demand verify API — branch `feat/audit-infra`

**2026-07-12.** The read side that closes the audit module: the Auditor (and Chairman/Secretary) can now read the immutable record and verify its chain over HTTP. **AC-017/018/019/020 → Met.**

- **`AuditEndpoints.cs` — two read-only routes, gated by `Policies.AuditRead`.** `GET /api/audit` — the register: filters `entityType`(=CLR aggregate name in `SubjectType`)/`actor`/`action`/`from`/`to`, paginated, **newest-first (Sequence DESC)**, → `PagedResult<AuditEventDto>`. `GET /api/audit/verify` — on-demand `AuditChainVerifier.Verify` over the whole log (Sequence ASC) → `{IsValid, BrokenAtSequence, Reason}`.
- **Deliberate deviation from the plan's "GetAuditEventsQuery + MediatR handler" wording** (flagged for the operator): a pure read with no validation and no cross-module concern injects `AuditDbContext` directly into the endpoint lambda — the `AdminEndpoints` precedent — rather than routing through MediatR (which would only drag it through the no-op-for-a-read `AuthorizationBehavior` + `TransactionBehavior`). ADR-0001 is respected — `AuditDbContext` is shared infrastructure, not a business module.
- **DTO normalizes the two row shapes.** The store holds enriched **v2** rows (governed state changes) and lean **v1** rows (system/integration/authZ events + pre-enrichment history). `AuditEventDto` surfaces enriched fields nullable and pre-normalizes `Action = Action ?? EventType`, `Actor = ActorUserId ?? Subject`, so a mixed log renders one uniform register. `actor`/`action` filters use the same COALESCE, matching across both shapes.
- **RBAC = {Auditor, Chairman, Secretary}, Administrator excluded (ADR-0027 / SoD-5).** Proven on **both** routes: allowed → 200, Member/Reviewer/**Administrator** → 403, no token → 401. The positive `Auditor → 200` case proves the policy string-matches (a deny-only suite can pass for the wrong reason).
- **One in-scope deviation, surfaced not buried:** this PR also adds a `RisksApiTests` case for the mitigation-status transition (`POST /mitigations/{id}/status`). A fully-clean coverage run (`rm -rf` all `TestResults` first) showed that endpoint was never HTTP-tested, dropping `RisksEndpoints.cs` below the 95% per-file gate. Whether it regressed or was always untested is **unverified** (a stale-cobertura union in an earlier run could have masked it — the documented trap — but that was not confirmed). Closed opportunistically to keep CI green; flagged for the operator to accept-in-PR or split out.
- **AC-018 (immutability) traced to what exists, not a hollow test.** `AuditEvent` has no public setters and no Update/Delete path, so a blocked-write test cannot even be attempted; the evidence is the design + `AuditEventEnrichmentTests`' valid-chain + enriched-field-tamper assertions (tamper is *detectable*, not *possible*). No new test pretends otherwise. DB-permission immutability (INSERT/SELECT-only grant) + the nightly Hangfire verify job stay **deferred to P16** (logged, not silently dropped).
- **Tests.** `AuditApiTests` (13 cases) + a `SeedAuditAsync` factory helper that seeds **both** a lean v1 row and an enriched v2 row, chained off Genesis, so the DTO's cross-shape normalization + filter/paginate are exercised deterministically (post-PR2 the API only ever produces v2 rows, so the v1 branch would otherwise be uncovered).
- **Package reconciliation folded in.** `audit-and-records.md §1.1` `SubjectType` note reconciled (CLR aggregate name, not the `ArtifactType` enum — required for the before/after drain). `traceability-matrix` FR-150…153 now cite ADR-0026/0027; FR-153's role clause note records the ADR-0027 supersession. `permission-role-matrix §C row 29` already correct (Auditor A / Administrator D).

**Next:** PR4 — the `/audit` UI (read `ACMP Lists & Registers.dc.html` directly per INV-014; clone `features/risks/*`; FE RBAC fix `App.tsx`/`navModel.ts` per ADR-0027; live VR). GO-gated; stop for GO.

---

## Audit slice PR2 — migrate emit sites to EmitEnrichedAsync — branch `feat/audit-infra`

**2026-07-12.** Every **governed-entity state-change** audit site now emits the enriched, self-describing record (`action`, `subjectType`, `subjectId`, `Outcome`, `before/after` drained from the SaveChanges capture, `CorrelationId`) instead of the lean v1 `EmitAsync`. Migrated per-module, GO-gated, one commit each, `main`-green throughout behind the PR1 compatibility overload: **Topics (9) · Decisions (10) · Risks · Dependencies · Traceability · Governance (17) · Meetings (17) · Actions · Membership** — plus the infra writers `TraceabilityWriter` and `MeetingWebexWriter`.

- **Convention.** `subjectType = nameof(<Aggregate>)` (must equal the interceptor's captured `ClrType.Name` for before/after to drain) — e.g. `Vote`, `Decision`, `Topic`, `Meeting`, `MinutesOfMeeting`, `Risk`, `ActionItem`, `Adr`, `Invariant`, `Relationship`, `Dependency`, `CommitteeMember`, `Delegation`. `subjectId = <aggregate>.PublicId`. Denials (`BallotDenied`, `ActionVerifyDenied`, `DecisionIssueDenied`) carry `Outcome=Denied` and — per PR1 — autocommit outside the command transaction, surviving the throw.
- **Data payloads dropped** (before/after replaces them). One deliberate, **documented** fidelity trade: Governance's audit-only `AuthorApprovedSelf` flag is gone — it is reconstructable from the enriched record (`ApprovedByUserId`/`ActivatedByUserId`, captured in before/after) vs the persisted `AuthorUserId`/`CreatedBy`. Tests now assert the **persisted** approver-vs-author fact (stronger than the transient flag). `DecisionIssuance` gained a `decisionId` param (subject = the issued Decision) and shed its now-unused `actorSub`.
- **Retained on v1 (out of AC-017's literal scope — system/integration/authZ/batch events, NOT governed-entity state changes):** `Authorization.Unauthenticated/Forbidden` (authZ-denial backstop, AC-006), `Authentication.NoRoleClaim`, `admin.job.requeued`, the three Webex OAuth/webhook events, `Actions.RemindersSent` (batch reminder sweep), `Notifications.AllRead` (bulk read-marking). These keep the lean-but-valid v1 row; they can be enriched later if desired but do not gate AC-017.
- **Before/after verified populated end-to-end** (not hollow). `AuditAtomicityTests.CloseVote_persists_a_populated_before_after` drives `CloseVote` through real DI + `IMediator` + SaveChanges on real SQL and asserts the persisted row's `AfterJson` is non-null and contains the `Status` delta, `ActorUserId` = the principal, `Outcome=Success`, and `CorrelationId` = the ambient `Activity.TraceId`. The complementary `BallotCast` assertion confirms `AfterJson` is **null** there — casting mutates the child `Ballot`, not the `Vote`'s own scalars, so the "subject = aggregate root" convention yields no delta for child-entity mutations (a known, documented limitation, not a stray null).
- **`subjectType` = CLR class name, NOT the `ArtifactType` enum** (deliberate, flag for PR3). It MUST equal the interceptor's captured `ClrType.Name` for the drain to match (e.g. `MinutesOfMeeting`, `ActionItem`, `CommitteeMember`, `Dependency`), which diverges from `audit-and-records.md §1.1`'s suggested `ArtifactType` values (`Minutes`, `Action`, `Member`…). This resolves drain-correctness vs documented-schema in favor of the drain. **PR3's `GET /api/audit?entityType=…` filter must query CLR names**, and the doc §1.1 note should be reconciled then.
- **AC-017 status.** The **write side is complete** — every governed state change now carries the discrete field set (`FR-151`), verified populated end-to-end. Left **Pending** pending PR3, which adds the Auditor read/verify API (`GET /api/audit` + `/verify`) that exposes these fields — the plan's "AC-017 read side" + AC-020. Re-grep confirms **0** governed-entity `EmitAsync` sites remain.
- **Gates.** All suites green (Domain 188 / Arch 41 / App 806 / Integration 23 / Api 188); coverage 99.61% (no file <95%); `dotnet format`, i18n (1478), Keystone clean. A reusable per-module migrator + test-assertion migrator (scratchpad) drove the mechanical bulk; specials (variable/ternary actions, `request.MemberPublicId`, multi-emit supersede pairs, `RecordingAudit` fakes, the wrong-typed `CommitteeMember`) were hand-verified.

**Next:** PR3 — Auditor read/verify API (GET /api/audit filter+paginate under `Policies.AuditRead`, GET /api/audit/verify), then flip AC-017/019/020 with traces (GO-gated; stop for GO).

---

## Audit slice PR1 — same-transaction atomicity (step 4, NFR-042) — branch `feat/audit-infra`

**2026-07-11.** The last piece of PR1 (ADR-0026): a module state change and its audit append now commit or roll back **together**. Before this, `SaveChangesAsync` then `EmitAsync` ran in two separate transactions on two connections — a failed audit append after a committed change left an unaudited mutation (violating NFR-042). Proven end-to-end against real SQL Server (Testcontainers) — the one thing EF-InMemory cannot show, since it ignores transactions.

- **One shared connection per scope.** A scoped `DbConnection` (`SharedKernelExtensions`) now backs every module `DbContext` + `AuditDbContext` (`(sp, options) => UseSqlServer(sp.GetRequiredService<DbConnection>())`), so a command's writes and its audit append run on ONE local transaction — no `TransactionScope`, no MSDTC escalation. `WebexDbContext` (integration OAuth store, a plain `DbContext` not a `ModuleDbContext`, written outside the MediatR pipeline) stays on its own connection by design — its writes never open the ambient transaction.
- **Lazy begin, gated to real writes.** `AmbientTransactionStarter` (a `SaveChangesInterceptor`) opens the transaction on the FIRST relational `ModuleDbContext` write. `AmbientTransactionInterceptor` (`CommandCreated`) enlists every subsequent command — reads included — so a follow-on read on a second context (e.g. `SqlAuditSink.TipHashAsync`'s SELECT on `AuditDbContext`) does not hit SqlClient's "connection has a pending local transaction" rejection. `TransactionBehavior<,>` (innermost MediatR behavior) commits on a clean handler / rolls back on a throw.
- **Denials survive rollback (ADR-0026).** A Denied/Failure audit has no paired state change, so it must NOT ride the command transaction. Because begin is gated to `ModuleDbContext` writes, an auth/validation denial (outer behaviors) or an in-handler denial like `CastBallot`'s `BallotDenied` (emits-then-throws before any module save) never opens a transaction → its audit autocommits and survives the throw. Proven by test.
- **EF interceptor auto-apply does NOT fire** (resolves the step-3 caveat). EF's DI auto-apply of registered `IInterceptor`s did not attach to these contexts — the atomicity test caught it (the tx never began; rollback was a silent no-op; scenarios 1/3 passed *trivially* because both writes just autocommitted). Interceptors are now attached EXPLICITLY per context via `.AddAcmpAuditInterceptors(sp)` (concrete scoped registrations), fanned out to all 10 module contexts + `AuditDbContext`.
- **MARS enabled on the shared connection.** Moving from per-context pooled connections to ONE shared connection introduces the "open DataReader" failure class if a handler streams a query on context A while touching context B. Current code is safe (grep confirms no `AsAsyncEnumerable`/streaming and no sync `.SaveChanges()`; EF buffers by default so readers close on await), but `MultipleActiveResultSets=true` is set on the shared connection as cheap insurance for future streaming handlers — compatible with the single ambient local transaction.
- **Proof + gates (integration, not isolation).** New `AuditAtomicityTests` (Testcontainers, real DI + `IMediator`): CastBallot commits ballot+audit together; a forced audit-append throw rolls the ballot back (no orphan mutation/row); a double-cast's `BallotDenied` persists despite the throw. `MigrationBootOnSharedConnectionTests` proves the boot-critical path with zero coverage elsewhere — several real modules + `AuditDbContext` migrate **sequentially on the one shared connection** (interceptors attached) against a fresh DB, then round-trip. `SharedConnectionWiringTests` asserts every context is on the shared connection. `AuditCapture` cold sync path + unused member removed, its `Deleted`/type-only-`Take` paths tested. All suites green (Domain 188 / Arch 41 / App 806 / Integration 24 / Api 188); coverage ≥95% per-file; `dotnet format`, i18n, Keystone clean.
- **Watch-item (accepted at v1 scope):** the transaction now spans the whole handler, so a genuinely-external side-effect inside a handler (MinIO recording upload, Webex when enabled) happens while the tx is uncommitted — a later rollback orphans it (object uploaded / call made, DB reverted). In-app notifications (DB writes) become atomic, which is correct. Webex is opt-in and recording upload is a distinct flow; noted for the emit-site migration (PR2) and any future handler that mixes external I/O with a DB write. The cross-module rollback is mechanism-proven (the generic `CommandCreated` enlister already rolls back a *second* context — the audit context — in the forced-throw test); an explicit two-module-write rollback test is a cheap follow-up.
- **No AC flip yet.** AC-017 flips to Met only in PR2, when all ~80 `EmitAsync` sites migrate to `EmitEnrichedAsync`. Step 4 is infrastructure; the atomicity property holds for both the legacy and enriched emit paths.

**Next:** PR2 — migrate the ~80 emit sites to `EmitEnrichedAsync` (GO-gated; stop for GO before starting).

---

## CI hardening — hosted-runner scarcity & minute-burn — branch `ci/harden-runner-scarcity`

**2026-07-09.** A GitHub-side incident (Actions **major_outage** since 04:34 UTC, *"Delays starting Actions runs"*, ~30% of hosted runs failing to acquire a runner) turned routine pushes red with *"job was not acquired by Runner of type hosted."* The failure is **external** — no workflow change allocates a runner during an outage, so that half self-resolves. What the repo *owns* (private-Free tier: no required checks, capped 2,000 Actions-min/month) is fragility + wasted minutes; PR #103, a one-line markdown change, ran the full backend suite **and** the 7-service e2e stack. This slice hardens both workflows so non-code churn stops drawing on the scarce pool. Billing was investigated and ruled out (intermittency ≠ hard-cliff exhaustion).

- **`ci.yml`.** `paths-ignore` on both `push` and `pull_request` (`*.md`, `**/*.md`, `docs/**`, `ACMP product context/**`, `.claude/**`) so pure non-code changes skip the whole matrix — mixed code+docs still runs (GitHub skips only when *every* file matches); verified no executable code lives under the ignore paths. Workflow-level `concurrency` (`ci-${{ github.ref }}`, `cancel-in-progress` scoped to PRs so a push-to-main never cancels). Per-job `timeout-minutes` (backend 25 / compose 10 / frontend 15) — a runaway-*execution* guard only (does not count acquisition wait, so no interaction with the outage).
- **`e2e.yml`.** Same `paths-ignore` on `pull_request` (`workflow_dispatch` left unfiltered), separate `concurrency` group, `timeout-minutes: 30`. The path-filter changes *when* e2e runs on a PR → **ADR-0016 §2 amended** (dated 2026-07-09 note, mirroring the S1 amendment): docs-only PRs skip the e2e leg (no testable surface), governed not silent.
- **Scope discipline.** The `MinioFileStoreTests` read-after-write flake is **not** folded in — separate follow-up (poll/retry around the post-upload `ExistsAsync`). No repo-local Keystone validator on this host to run the mechanical gate; the ADR change is a prose-only dated amendment (no frontmatter/ID/link change), lowest-risk for the validator — owed to CI/operator.
- **Honest limitation.** This is a cost + blast-radius cut for non-code churn; it gives **no** outage immunity to real code merges (same runners, same ~30% risk while the incident is live).

**Next:** open PR → the PR's own CI/e2e must go green (gated on the outage clearing, since it needs runners) → squash-merge; then re-run the currently-red post-merge run `29017164287` if still red. Behavioral proof of the filter = a later docs-only change triggering **zero** runs.

---

## P13 audit remediation — Slice 2 (Webex hardening) — branch `fix/p13-audit-slice2`

**2026-07-09.** The second of two GO-gated slices: the **dormant** Webex hardening the audit surfaced. All of it lives behind `Webex:Enabled` (the adapter is unregistered when disabled), so it is unit-tested here with **live Webex validation owed to the operator** (the sandbox is operator-only). No AC regressions; AC-068/069 restored to genuinely-tested `Met`.

- **M1 — fail-closed encryption key.** New `WebexOptionsValidator` (`IValidateOptions<WebexOptions>`) wired via `AddOptions().Bind().ValidateOnStart()` (plain `Configure` never validated): when Enabled, `TokenEncryptionKey` must be present, ≥16 chars, and not the `CHANGE_ME` placeholder → **boot failure** instead of encrypting the persisted OAuth tokens under a publicly derivable key. Fixed the misleading "base64" comment (the key is SHA-256-derived from any long secret). The three enabled-Webex test hosts now supply a real key (the correct consequence of fail-closed validation).
- **M2 — graceful refresh degradation.** `WebexTokenService.GetValidAccessTokenAsync` now catches a **non-transient** `WebexApiException` (400/401 — revoked/re-consent) and returns `null` per its AC-072 contract, so the meeting-create/recording jobs no-op instead of dead-lettering; transient 5xx and `WebexRateLimitException` (429) still bubble for retry.
- **m5 — audit the rotation (INV-005).** A successful background token refresh now emits `Webex.OAuthTokenRefreshed` (system actor); the initial link was already audited at the OAuth callback.
- **m7 — replay guard.** A webhook with no `Created` timestamp (un-age-checkable) is now dropped, not processed.
- **m9 — honor Retry-After everywhere.** `WebexWebhookJob` + `WebexMeetingCreateJob` now reschedule on 429 for the server-supplied delay (mirroring `WebexSendJob`); previously only the send job did.
- **m8 — deferred with an honest comment.** The HMAC is computed over the UTF-8-decoded body, byte-identical to the raw body for the valid-UTF-8 JSON Webex always sends; a raw-byte HMAC would only matter for malformed non-UTF-8 payloads Webex never emits. Kept text-based to avoid perturbing the security path (documented; upgrade only if such a payload is ever observed).
- **AC-068 → Met (real Hangfire test).** `WebexJobDeadLetterTests` runs a real in-memory Hangfire server: a job past its `[AutomaticRetry]` cap lands in the **Failed** state (dead-letter), asserted via `IMonitoringApi` (retry delays forced to 0 in the harness only). **AC-069 → Met**: reprocessing the same recording attaches the identical reference (outcome-idempotent) + the no-timestamp drop is tested.
- **m13 — gated off.** Deleting the tautological `WebexRecordingTests` would drop `WebexRecording` coverage (`DtoContractCoverageTests` doesn't cover it), so it stays; the OAuth-test file naming is cosmetic (skipped, YAGNI).
- **Gates.** BE: build clean; all suites green incl. the new Hangfire dead-letter server test (Application 796) + the m7 webhook test (API 188); coverage ≥95% per-file (`WebexEndpoints` 93/96 — the residual 3 are a defensively-unreachable disabled branch + two record-declaration lines); `dotnet format` clean. Keystone validator OK. New test-only deps: `Testcontainers.Minio` (Slice 1), `Hangfire.InMemory` (Slice 2).

**Next:** operator merge GO; then **rotate the exposed Webex + ngrok secrets** in `deploy/.env` (the last owed residual), and a one-time production live-confirm of AC-070 + the Webex-enabled paths.

---

## P13 audit remediation — Slice 1 (recording tab + test integrity) — branch `fix/p13-audit-slice1`

**2026-07-09.** An adversarial audit of the merged P13 slice surfaced a test-integrity gap, three over-claimed ACs, and UI/i18n/a11y minors on the recording tab. This first of two GO-gated slices closes the **Webex-independent, live-validatable** findings; the dormant Webex hardening (fail-open token key, refresh-degradation, audit-on-refresh, replay guard, per-job Retry-After) is deferred to Slice 2 (`fix/p13-audit-slice2`).

- **M3 — production storage now real-covered.** `MinioFileStore` (the FR-056 recording store behind AC-073/074) was behind a coverage exclusion whose "not wired into any v1 flow" justification was **false**. Removed the exclusion + rewrote the comment; added `MinioFileStoreTests` — a **Testcontainers-MinIO** integration test that boots a real MinIO container and exercises every adapter branch (bucket create + skip, presign, `ExistsAsync` found/object-missing/bucket-missing, delete). Coverage gate now counts the file (339 files, still 99.69% global, per-file ≥95%). Needs a running Docker daemon (CI `ubuntu-latest` has one; local gate runs now require Docker Desktop up).
- **M4 — honest acceptance rows.** **AC-073** stays `Met`, evidence upgraded "faked adapter"→"real adapter (Testcontainers)". **AC-068 → Partial** (interim): the dead-letter/monitor clause rests only on `[AutomaticRetry(3)]` + the Hangfire dashboard, untested — a real in-memory Hangfire dead-letter test in Slice 2 restores Met. **AC-069** stays `Met` with corrected wording ("idempotent" = the idempotent `Meeting.AttachRecording`; within-window outcome test → Slice 2).
- **Recording-tab minors.** `.mt-rec-webex` (Webex-recording link) gained the missing `:focus-visible` ring (WCAG 2.4.7, matches every sibling control); the upload picker's `accept` narrowed `video/*`→`video/mp4,video/webm,video/quicktime` (matches the server allowlist + hint); two dead i18n keys removed (`recording.uploading`, `recording.loading.body`); the `.mt-rec-foot` divider dropped to match the design reference.
- **Gates.** BE: build clean, all suites green incl. the new MinIO container test (Integration 18), coverage 99.69% (339 files, per-file ≥95%), `dotnet format` clean. FE: vitest 845 green (one `DecisionPage` timeout flaked under concurrent-container load, confirmed green in isolation), i18n parity 1478, build type-clean. Keystone validator OK.

**Next:** operator merge GO (branch → PR → green CI → squash-merge), then Slice 2 (dormant Webex hardening, unit-tested; live Webex validation owed to operator) + operator secret rotation.

---

## D-15 — Topic *Prepare* (Accepted→Prepared) UI affordance — Tier 3 (Complete) — branch `fix/d15-topic-prepare-ui`

**2026-07-09.** Closed the highest-priority defect: the SPA had no way to mark a topic `Prepared`, so the agenda-builder pool (`GET /topics?status=Prepared`) was permanently empty and the intake→agenda core loop was broken in-product (backend `POST /topics/{id}/prepare` shipped since P5, never wired). Tier 3 = the FE affordance + a backend W4-completeness notification + the honest E2E fix.

- **Frontend.** `usePrepareTopic` (invalidates backlog + `['topics','prepared']` pool + detail — the pool key is what unblocks the agenda builder). A **"Mark prepared"** button on the Accepted-topic detail (`TopicDetail.tsx`), **show-and-enforce** (rendered for any Accepted topic; backend 403s a non-owner/non-Secretary, surfaced inline — the owner is often a plain Member, so a role gate would hide it from the right person). A **Prepared** kanban badge (Accepted & Prepared share the `accepted` bucket, so Prepared was otherwise invisible). Agenda pool empty-state reworded to point at the action. `TopicPrepared` notification type added to `notifPresentation`. i18n EN+AR.
- **Backend (W4-completeness).** `PrepareTopicHandler` now notifies the **Secretary roster** on prepare via `ICommitteeDirectory.GetActiveMembersInRoleAsync(Secretary)` + `INotificationChannel`, **skipping the actor** (no self-noise, mirrors `CreateAction`). New `TopicNotifications` (bilingual, deep-links `/topics/{key}`). No ADR — reuses existing Shared.Contracts seams; no module-boundary breach.
- **Honesty / AC-035.** The `core-loop.spec` prepare leg was **switched from a direct-HTTP call to clicking the "Mark prepared" button** so the E2E *drives* the affordance rather than masking its absence, and the **full spec was run locally against the live stack and passes** (the button click drives the prepare `204`, the prepared topic reaches the agenda pool, and the agenda is added + published); CI's e2e job re-validates on the PR. `TopicApiTests` adds `POST /{id}/prepare → 204` through the real pipeline (proves the new DI seams resolve — a mocked unit test can't). Handler + FE hook/screen tests cover fan-out/skip-self, the button, and the badge.
- **Decisions applied.** Tier 3 per operator; no un-prepare/defer-from-Prepared transition → **OQ-049** (default = defer, `DeferTopic` doesn't allow Prepared so a mis-prepared topic waits until scheduled). Design omits Prepare entirely → **design-update-owed** (no-reference composition). **D-15 → Done.**
- **Gates (local = CI, incl. e2e).** BE 1217 tests green, coverage 99.69% global (per-file ≥95%), `dotnet format` clean. FE 845 tests green, per-file coverage ≥95% on all changed files, i18n parity OK (1480 keys), `npm run build` type-clean. **Playwright `core-loop.spec` run locally against the live stack — green** (isolated fresh `.env.example` compose project so the operator's dev volumes were untouched); CI's e2e job re-validates on the PR.

**Next:** operator merge (branch → PR → green CI → squash-merge); the remaining PH-2 backlog (P14 Tarseem+Diagrams · P15 Research/Knowledge) or hardening (P16–P19).

---

## P13-close — Webex integration + meeting recording, closed (Phase 2) — branch `feat/p13-recording-upload`

**2026-07-09.** P13 declared **complete**. All P13 acceptance criteria (**AC-067–074**) are `Met`; PR #99 (recording slice stacked on the Webex adapter) is open and mergeable vs `main`.

- **AC-070 (Webex recording auto-attach) — settled as an environmental caveat, not a code gap.** The dev/sandbox Webex account we validate against records **locally only** — local recording never fires the cloud `recordings/created` webhook — so a genuine cloud recording cannot traverse the attach pipeline on this stack. The **production** account is licensed for cloud recording and would exercise it. What **is** proven live: webhook **auto-registration** (audit seq=46) and a **synthetic signed** `recordings/created` webhook → **200** → worker job → **graceful drop** (dropped only because the synthetic payload carries no real recording asset to fetch — the pipeline itself ran). That sits on top of the unit/integration attach tests (`MeetingWebexWriterTests`, `WebexWebhookJobTests`). Per working-discipline ("validate before claiming") the AC keeps its `Met` verdict — the literal clause (*webhook processed → reference stored + audit*) is genuinely tested — with the real-cloud-recording confirm logged as a **one-time production residual** (deferred-work D-02).
- **Residual (non-blocking):** (1) production live-confirm of AC-070 with a real cloud recording; (2) **rotate the exposed Webex bot/OAuth token + ngrok authtoken** (operator, secrets stay in git-ignored `deploy/.env`); (3) squash-merge PR #99 → sync `main`.

**Next:** **D-15** (topic *Prepare*-UI — highest-priority defect; plan-first, GO-gated) then the remaining PH-2 backlog.

---

## P13-recording — Meeting-recording upload · presigned playback · delete (Phase 2) — branch `feat/p13-recording-upload`

**2026-07-09.** FR-056's *upload a recording file* leg — delivered end-to-end, live-validated. Reuses the shipped `IFileStore`/MinIO seam (ADR-0014); complements the Webex-webhook reference path (AC-070).

- **Upload (BE).** `POST /api/meetings/{key}/recording` (multipart, Secretary/Chairman) → validate size/MIME (`MeetingRecordingOptions`: video/mp4|webm|quicktime, ≤ 2 GB) → store in MinIO (`acmp-recordings`) under a **server-derived key** (`{meetingKey}/{guid}{ext}`, no client filename — SigV4-safe) → `Meetings.RecordingUploaded` audit. New `Meeting` recording fields + `Meetings_AddRecordingUpload` migration.
- **Playback (BE + infra).** `GET /{key}/recording/url` mints a 10-min pre-signed MinIO URL; nginx `location /acmp-recordings/` proxies to MinIO with the Host header preserved so the SigV4 signature validates (a dedicated presign `IMinioClient` on the public endpoint + explicit region). `<video src>` plays with Range/seek.
- **Delete (BE).** `DELETE /{key}/recording` (Secretary/Chairman) clears the reference and, for an uploaded file, removes the MinIO object; a Webex reference is only cleared. `Meetings.RecordingRemoved` audit.
- **UI (FE).** Recording tab rebuilt to `ACMP Meetings.dc.html` isRecording (full-width player card, source `StatusChip` Webex/Uploaded, Download/Replace/Delete via design-system `Button`/`Dialog`); upload dropzone + honest empty state; new `trash` icon; i18n EN/AR.

**Decisions applied:** ADR-0025 (recordings via MinIO `IFileStore` + presigned playback + delete; Accepted). Reference-not-file for Webex, object-key-not-filename for uploads. Delete is reversible for the Webex source (a re-fired webhook re-attaches) — documented.
**New acceptance criteria:** **AC-073** (upload + playback), **AC-074** (delete) → **Met** (trace to `MeetingRecordingTests` + `MeetingRecordingApiTests`).
**Verification:** BE build + Application/API tests green (coverage ≥95%, `MinioFileStore` excluded); `dotnet format` clean; FE vitest + i18n parity; Keystone validator PASS. **Live (`acmp.ngrok.dev`, secretary-test):** real upload 200 through nginx (was **413** at the 1 MB `client_max_body_size` default — the root cause of the operator-reported "upload/replace not working", compounded by a **stale cached bundle**; both fixed); presigned URL 200/206; delete → MinIO bucket empty; full-width design confirmed.
**Next (open):** AC-070 Webex-webhook live close (operator records a real Webex meeting) — a separate P13 item; transcript retrieval stays P19; D-15 (topic Prepare-UI) next.

---

## P13 — Webex integration (Phase 2) — branch `feat/p13-webex-integration`

**2026-07-07.** Phase-2 Webex adapter behind `INotificationChannel` + a meeting/recording client (ADR-0005, ADR-0023). **Application/adapter surface + worker-container split COMPLETE + CI-green; only the operator-run live sandbox validation remains.**

- **Multi-channel dispatch (WS1).** New `INotificationSink` + `NotificationDispatcher` (the single `INotificationChannel`) fan out to every sink; the in-app channel became a sink. **Zero changes to the ~15 existing callers.**
- **Webex adapter module (WS2).** New `Acmp.Modules.Integrations.Webex` (depends only on `Acmp.Shared`, ArchUnit-enforced): typed `WebexApiClient` (bot auth, 429→typed exception), `AdaptiveCardBuilder` (v1.3, ≤80 KB, EN/AR, absolute deep-link), `WebexNotificationSink` (Enabled-gated, committee-wide events only, **one post per event** collapse, failure-isolated), `WebexSendJob` (429→reschedule via Hangfire), testable `IWebexJobScheduler` seam.
- **Recording webhook + write-back (WS3).** First **anonymous** endpoint `POST /api/webex/webhook` authenticated by **HMAC-SHA1** (Webex default; SHA256/512 configurable) over the raw body + 5-min replay guard; `IMeetingWebexWriter` (ADR-0021 write seam) + new `Meeting` Webex fields (`WebexMeetingId` indexed for correlation, recording ref) + migration `Meetings_AddWebexFields`; recording processor fetches `GET /recordings/{id}` and attaches (idempotent, audited INV-005).
- **OAuth + meeting auto-create (WS3b).** Secretary OAuth flow (`/api/webex/oauth/start|callback`, Admin-gated); `WebexToken` store (AES-GCM encrypted, own `webex` schema, migration `Webex_TokenStore`, provisioned by `MigrationRunner`) with transparent refresh; `ScheduleMeeting` enqueues create for online meetings via the new Shared `IWebexMeetingProvisioner` (Meetings registers a no-op default; the adapter overrides when enabled) → writes id + join URL back. **Dropped the speculative `IMeetingIntegration` Shared seam** (YAGNI — used once, internal to the module).

- **Dedicated worker container (WS0, ADR-0024 → Accepted).** New `Acmp.Bootstrap` library holds the single composition root (`AddAcmpModules` + `AddAcmpHangfireStorage`) both hosts call, so their DI never drifts. New `Acmp.Worker` .NET Generic Host runs `AddHangfireServer()` + the recurring action-reminder sweep (moved off the API); the **API is now enqueue-only** (`AddHangfire` client, server removed). The **API owns EF migrations**; the worker waits for the schema (`docker compose depends_on: api healthy`), avoiding a two-host race. New `Dockerfile.worker` + compose `worker` service + an opt-in (`--profile ngrok`) configurable `ngrok` ingress for the webhook (dev + optional prod via a reserved `NGROK_DOMAIN`); Webex env plumbed through `.env.example` for both hosts. **`SystemCurrentUser` deliberately NOT added** (YAGNI): every worker job either opts out of the MediatR auth check or hardcodes its own `system:*` audit actor, and `CurrentUserService` is null-safe headless — so it would change zero behaviour. `Acmp.Worker`/`Acmp.Bootstrap` are hosts/wiring, exempt from the ArchUnit module rules.

**Decisions applied:** Hangfire-native retry (no outbox table); **space-only delivery** (per-user email absent from `ICommitteeDirectory` → per-user DM/invitations/email-attendance deferred, **D-14**); **HMAC-SHA1 default** (corrected from an initial SHA256 assumption via Webex docs); adapter **not registered at all when `Webex:Enabled=false`** (clean AC-071, avoids a Hangfire-off-in-Testing DI landmine); **worker/API split** (ADR-0024) isolates rate-limited/retry-heavy Webex jobs from request serving.

**New acceptance criteria:** **AC-067…072** added (space card + in-app; 429 reschedule; webhook HMAC 401/200 + replay; recording attach + audit; disabled = in-app only; meeting auto-create + graceful no-token). AC-069/071(edge) proven by WebApplicationFactory integration tests; the rest by unit tests.

- **OAuth flow fix + ngrok wiring (WS0 follow-up, sandbox prep).** The OAuth consent endpoints (`/api/webex/oauth/start|callback`) were `RequireAuthorization(AdminConfig)` (JwtBearer) — structurally un-completable, since both legs are top-level **browser navigations** that can't carry a bearer (ACMP is bearer-only, no session cookie), so Webex's callback redirect always 401'd. Made them **anonymous**, protected by the existing single-use `state` cookie, with a **token-link audit event** (INV-005) for attributability; ADR-0023 updated. Regression test asserts the callback is reachable without auth (400 on missing state, not 401). ngrok wired for the live sandbox: compose tunnels `web:80` (nginx serves the SPA + proxies `/api`) via reserved `${NGROK_URL}` (dev `acmp.ngrok.dev` / prod `acmp.ngrok.app`); Webex bot/space/OAuth creds + authtoken live in the git-ignored `deploy/.env`.

**Verification:** 722 Application tests (incl. **39 Webex unit** + **4 composition-root smoke** tests) + **144 API** (incl. 4 webhook/oauth integration) + **41 ArchUnit** + **188 Domain** — all green after moving the Hangfire server out of the API. Two EF migrations. Full solution builds clean; `dotnet format --verify-no-changes` clean; Keystone validator all-7 PASS. No product AC regressions.

- **Live sandbox run (2026-07-07).** `docker compose --profile ngrok up` against the real Webex sandbox on `acmp.ngrok.dev`. **Proven live:** the dedicated **worker container** (Hangfire server + dispatchers + job dequeue/retry — the definitive ADR-0024 validation); **AC-069** webhook (valid HMAC-SHA1 → 200, invalid → 401 over the public tunnel; worker dequeued the job); the **OAuth setup-key gate** (`/start` no key → 404); bot → committee-space post (200, after the operator added the bot). **Two issues surfaced that mocked tests couldn't:** (1) recording fetch used the **bot token** → Webex **403 "missing scopes"** (recordings are user-scoped), so `GetRecordingAsync` now takes the **OAuth host token** (resolved by the webhook job via `IWebexTokenService`, graceful when consent isn't done) — FIXED + tested; (2) the bot wasn't in the space (404) → operator added it.

**Next (open):** complete OAuth consent (setup-key URL) + a real recorded sandbox meeting to close **AC-070/072** live; trigger a domain event from the SPA to close **AC-067** (card render); then flip the "live pass" notes; plus the WS5/6 doc polish (`functional.md` FR-056/057 flips, status-report regen) as P13 formally closes.

---

## Keystone migration gap remediation — branch `fix/keystone-migration-gaps`

**2026-07-06.** Adversarial post-migration audit (3 exploration agents + advisor + independent devil's-advocate verification) found the PR #97 migration **faithful on register content** (all 66 ACs + Given/When/Then intact; guardrails→INV 1:1; CHANGE-001 substance preserved) but **lossy on the agentic surface**. Remediated, docs-only:

- **Execution ladder restored (the "19 phases → 3" complaint).** The migration deleted `execution-handoff/phase-prompts.md`, the only definition of the P-series build slices. Deeper finding: that file (plus `HANDOFF-RUNBOOK.md` and both READMEs' tails) had **already been truncated by an editing accident in commit `16e0577`** — P18 cut mid-sentence and **P19 (Final audit & release readiness) lost entirely**; the true ladder is **P1–P19**, recovered from baseline `c487448`. Canonical ladder now lives in `planning/roadmap.md` (P1–P12 as-shipped with merged PRs; P13–P19 planned; 3-row legacy-token map for the backlog priority codes, the Usage-Map internal phase numbers, and the audit-era "P14" tokens). Seven per-slice prompts (P13–P19) rebuilt in `handoff/follow-up-prompts.md` from git history with paths/INV refs updated and parametrics preserved. `P##` catalogued in `governance/naming-conventions.md`; D-11 (Tarseem), D-12 (email), D-13 (per-ballot crypto chaining → P16) added to the deferred-work register.
- **Design-reference wiring restored (complaint #2).** Root `CLAUDE.md` mislabeled design fidelity as INV-007 (= no-secrets) → fixed to **INV-014**. The "read the `.dc.html` directly — NOT via the design MCP" clause and the **Usage Map** per-screen-index link restored to the follow-up-prompts standard footer + review-prompts fidelity check; design-context row added to `docs/README.md` reading order; P15 prompt → `ACMP Research & Knowledge.dc.html`, P14 → `ACMP Diagrams.dc.html` (net-new wiring); `design-handoff/` bannered ARCHIVED; HANDOFF-RUNBOOK + root README rewritten off the superseded design-MCP route.
- **Link integrity.** `docs/README.md` regained a **Canonical reference (§A–§G)** pointer section (~273 dangling `§A…§G` citations across 43 files resolve again at the definition end); 3 corrupted self-referential citations fixed (open-decision/open-question registers, adr-0018:53); ~200 stale pre-migration path tokens repaired across 25+ files (147 scripted `NN-name.md` → new-path replacements the migration's sweep missed, plus `/adr/`, `docs/NN`, `execution-handoff/`, `docs/_progress` forms in docs/domain/*, deploy comments, tools/parity, memory); checkpoints' false links to the deleted `45-release-readiness-checklist.md` re-pointed at the Definition of Done.
- **Condensed content restored (operator chose restore-everything).** Test-design annex re-added to `validation/test-strategy.md` (assertion matrices, k6 scenarios, mocking boundaries, seed spec, per-layer runtime targets, AC-naming convention, per-phase focus); 4 dropped non-gating release checks re-added to `execution/definition-of-done.md` (SQLi/XSS input validation, Seq alerting, OpenAPI validity, maintenance-window + 48h rollback criteria); roadmap regained per-phase team-composition reference blocks + the reordering-rationale note; work-breakdown regained per-epic Goals, Module/Size columns + legend, and lost parametrics (aging SLA 3d/7d/21d, Webex ≤10-image cap, vote option-sets, hash-chain RISK-003/ADR-0009 refs); 3 orphaned guardrail "DO" rules sourced in `governance/contributing.md` §Working discipline and mirrored in `AGENTS.md`.
- **Mechanics.** Front-matter versions bumped to 1.1.0 on the 12 substantively changed package docs + synced in `manifest.json` (also fixed the leaked `"3.9"` compose-version string); remediation entry appended to `keystone-state.json` change_log.

**Verification:** validator **all 7 critical gates PASS** after every workstream and finally (`RESULT: OK`); scripted relative-link check over 103 md files → **all resolve**; grep-zero on stale-path patterns (historical narrative + archived files exempt); zero `src/` changes. **No product AC flips** — governance/documentation repair only. Next: PH-2 remainder via the restored per-slice prompts (P13 Webex · P14 Tarseem · P15 Research/Knowledge) or cross-cutting hardening (P16–P19).

---

## Keystone package migration — branch `chore/keystone-package-migration`

**2026-07-06.** Migrated the hand-built ACMP planning package into the **Keystone v1.0.0 package format and mechanics** (no product code behavior change). The flat `docs/00–46` + root `/adr` + `/execution-handoff` + `docs/_progress` layout is now the foldered Keystone package under `docs/`: `requirements/ decisions/ architecture/ adrs/ risks/ planning/ execution/ validation/ progress/ handoff/ governance/ domain/` plus `00-charter.md`, `01-executive-summary.md`, `README.md`, `manifest.json`, `keystone-state.json`.

**What moved / was created (faithful hybrid — operator-approved):**
- Registers reshaped to Keystone form + front-matter + IDs: `requirements/{functional(FR),non-functional(NFR + new Source col),constraint(CON),invariant(INV — NEW, from the 14 guardrails),dependency(DEP)}`, `decisions/{open-question(OQ),open-decision(DEC ← former R-##),assumption(ASM)}`, `risks/risk-register(RISK)` (RAID split 4 ways), `validation/{acceptance-criteria(66 AC → Keystone table),test-strategy(TEST-),traceability-matrix(NEW derived, FR/NFR→DEC/ADR→WBS→TEST→AC),acceptance-audit(moved)}`, `planning/{roadmap(PH),work-breakdown(WBS ← EPIC/BL)}`, `execution/{backlog(BL),definition-of-done,deferred-work-register(NEW),checkpoints}`, `architecture/{architecture,technology-comparison,diagrams/}`, `governance/{governance,naming-conventions,contributing,glossary}`, `handoff/{initial-prompt,follow-up-prompts,review-prompts,execution-readiness-report,handoff-manifest.json}`.
- **Agent-control surface:** root `CLAUDE.md` → thin loader importing new **`AGENTS.md`** (invariants inline + links, hard constraints, acceptance-criteria-first tracking protocol, current-phase pointer → `progress/status-report.md`).
- ADRs `git mv /adr/ADR-NNNN → docs/adrs/adr-NNNN` (case-rename only; **no ADR meaning changed** — amendment notes on ADR-0004/0013 moved off the Status line to preserve the immutable status field).
- 35 ACMP domain/design docs kept as **governed extensions** under `docs/domain/` (own IDs `W-/EPIC-/US-/PAIN-/DB-/T-/C-*` retained — the Keystone validator ignores non-Keystone prefixes; catalogued in `governance/naming-conventions.md`).
- Full-sweep path rewrite: `docs/NN` / `/adr/ADR-` references remapped across `docs/**`, `CLAUDE.md`, `AGENTS.md`, **190 `src`/`.github` code comments** (comment-only, +270/−270, EOL preserved — no code logic touched), all `.claude/memory/**`, `.context/`. Bare `ADR-####` **IDs** left intact (still valid).

**Verification:** `python <keystone>/scripts/validate_package.py docs` → **all 7 critical gates PASS** (G-IDS, G-DEC-STATUS, G-REQ-SRC, G-COMPLETE, G-TRACE, G-SET, G-PROGRESS) — `RESULT: OK`. Markdown link-integrity clean (the two real `../../adr/` links fixed; `%20`-encoded design links valid). G-TRACE: every PH-1 MVP FR/NFR links to ≥1 decision + work-item + test; PH-2/3 FRs marked `Scope=Full` (exempt) — no fabricated links (module-governing ADR fallback per the approved strategy).

**No product AC flips** — this is a governance/documentation reorganization; every `AC-001…066` retains its prior verdict in `validation/acceptance-audit.md`. Next: operator merge GO (squash), then resume PH-2/hardening under the new package governance.

---

## P12 audit remediation — branch `fix/p12-audit-remediation`

Adversarial P12 (Dashboards & Reports) audit → **fidelity + data-honesty fixes, NO AC flips**
(AC-064/065/066 stay Met; PR3 Reports is AC-less). FE-only; backend untouched. All fixes reconciled
against the governing `ACMP Dashboards & Reports.dc.html` (per the Usage Map).

**Data honesty (was MAJOR):** the Reports **Stream filter** now scopes **decisions and actions** too —
each resolved through its linked Topic (`DecisionSummary.topicId`; `ActionSummary` Topic-source) — so no
card silently stays committee-wide while the toolbar implies a stream scope. `applyStreamFilter` moved
into the pure, directly-tested `reportViews` (new unit test: topics/decisions/actions/risks/deps all
narrow; non-Topic-sourced actions drop). This is the honest completion of FR-095's Topic-scope model
(OQ-047) for the reporting surface.

**Fidelity (per-series colour + copy — the pixel-VR missed these below its diff threshold):**
- Decision-outcome stack **"Conditional" → green** (`--st-success`), rejoining the design's Approved+
  Conditional "approved" family (was blue/info).
- Action-status **"In progress" → accent** — added an `accent` `Zone` + `--st-accent-dot/-fg` tokens
  (the enum previously had no accent, collapsing it to info-blue).
- Backlog-by-stream bars **colour-cycle per rank** (`info/sched/success/warn/danger`) to match the
  design's multi-stream palette (was uniform blue).
- Topic-aging labels use the design **en-dash** `0–7 / 8–14 / 15–30` (were ASCII hyphens).
- **KPI headlines restored** where computable: Exec "Decision outcomes" (approved-or-conditional %) and
  "Risk exposure" (high-severity count).
- **Drill button + "View detail" footer** added to the stat cards with a real destination
  (supersede/verification/coverage/immutable/throughput-by-stream → their registers).
- Filter row **always renders** and carries a real **"Updated the elapsed-time value"** (freshest `dataUpdatedAt`),
  matching the design's filter+timestamp row (Period/Status filters stay removed — see below).
- Empty-state copy refreshed (stale P10g "risks and dependencies" → "committee activity"); dead
  `reports.p12Note` key removed. Dashboard `.dash-mtg-meta` gap 10→**8px** (design).

**Design-update-owed (guardrail #14 — reference no longer depicts the build; NOT code-fixable honestly):**
- The three **dashboards** diverge from the reference by design (AC-064/065/066-driven): Chairman =
  votes-awaiting/escalated-risks/escalated-actions/deferred≥2 (not the design's approve-decision/meeting/
  votes/snapshot cards); Committee = AC-064 committee-wide data (not the design's personalized member
  cards); Secretary drops Agenda-readiness/Pending-approvals/Throughput for the AC-065 queue+SLA. The
  DESIGN should be updated to match.
- Reports **"DATA: Live/Loading/Empty" state tabs** and the **Period/Status filters** stay removed
  (preview affordance / dishonest without a time series) — design update owed.
- KPI **delta chips** (+6% / −0.6 …) stay omitted — they need a prior-period the app doesn't keep
  (ADR-0022 defers time series); adding fake deltas would violate guardrail #11.
- Outcome 4th segment stays **"Other"** (the issued¬approved¬conditional¬rejected catch-all) rather than
  the design's "Pending" — "Other" is the accurate label; design update owed.

**Blessed (defensible, no change):** KeyList trailing chevron + accent key (navigable-row affordance);
shared `EmptyState` reuse for the reports empty (canonical component); stub-`0` audit/supersede tiles
(`missing`/`disputed` are correct-by-invariant, not tracked metrics).

**Gates:** i18n parity **1458** (EN/AR, net +2: `reports.updated` + `reports.kpi.*` − dead `p12Note`);
FE tsc/vitest(+coverage per-file ≥95)/oxlint/build — [pending clean-copy run; repo node_modules corrupted
by stray dev-servers]. Backend untouched.

## P12-PR3 — Reports shell (FE) — branch `feat/P12-pr3-reports-shell`

**The full Reports IA over six view-tabs** (executive / committee / stream / decisions / actions / audit),
replacing P10g's focused risk/dep page at `/reports`. **No AC** (the Reports view has none) — this is design-IA
+ feature completeness. Client-composed over existing REST reads (ADR-0022: no server read-model, no chart
library — CSS primitives). Pure, directly-tested assembly in `reportViews.ts` (`buildView` → `ReportCard[]`);
the page (`ReportsPage.tsx`) is the shell: view-tabs, the Stream filter, CSV export, data-states, renderers.

**~16 real cards, composed from snapshot reads** — the advisor's recount corrected an early over-estimate of
what's blocked by the missing time series. REAL: decision outcome mix (stack), risk exposure (matrix, reuses
P10g), open-items / verification / approval-coverage / immutable-records / supersede (stat), backlog-by-status
& action-status & by-stream (bars), and **topic-aging histogram (columns — a histogram of CURRENT ages, not a
time series, which is what earns the `columns` renderer)**. Added the `columns` + `stack` renderers to the
existing matrix/stat/bars set; extended the `Zone` type with the `sched` tone slug.

**Honest-empty (guardrail #11, flagged), two categories:** `trend` — per-week/quarter series the app keeps no
history for (throughput, decisions-per-quarter, created-vs-closed, overdue-trend, audit-event-volume; ADR-0022
defers time series to PH-3); `seam` — data not on the summary DTOs (meeting attendance not on MeetingSummary;
per-ballot vote attribution not on VoteSummary). Each renders a "Phase 3" card and self-documents in the CSV.

**Killed two decorative pieces (advisor):** the design's "DATA: Live/Loading/Empty" state tabs (a preview
affordance like the dashboard's role tabs — real state = query status) and the "Period: This quarter" filter (a
filter that can't filter without a time series = silent drift). Shipped only the **Stream filter**, which does
real work (narrows topics + their linked risks/deps; decisions/actions carry no stream on their DTO → stay
committee-wide, flagged). CSV = current-view aggregates → rows, one button (no PDF — ADR-0022).

**Gates:** tsc, 815 vitest (33 new: reportViews.test + rewritten ReportsPage.test), per-file coverage (ReportsPage/
reportViews/reportAgg all 100%; global 99.80%), i18n parity 1456 (EN+AR `reports.*`), oxlint, build — all green
locally. FE-only; backend untouched. **Live `.dc.html` pixel-VR PASS** (`e2e/p12-reports-vr.spec.ts` — executive +
committee tabs, EN-light + AR-dark): all five renderers (matrix/stack/stat/bars/**columns** aging histogram) + both
honest-empty states (trend/seam) pixel-faithful, RTL-mirrored (Export/tabs/filter, columns right-to-left),
light/dark correct. Replaced the superseded `p10g-reports-vr.spec.ts`. **★ Completes P12. ★**

## P12-PR2 — Role dashboards (FE) — branch `feat/P12-pr2-role-dashboards`

**AC-064/065/066 → Met.** The home route `/` now renders the real role dashboard (`features/dashboard/RoleDashboard`),
replacing the P3 placeholder. One page picks the variant for the signed-in user's highest committee role — **Chairman
> Secretary > everyone-else = Committee**; the design's "Viewing as…" role tabs are a preview affordance in the
`.dc.html`, not a live control (release checklist F-19 treats these as three distinct dashboards, one AC each, so a
Chairman seeing only chairman cards is correct — Committee is the member/fallback variant).

**Client-composed, no server aggregation (ADR-0022).** Every number is derived in the browser from the PR1 registers
plus existing reads. The AC-carrying logic sits in a pure, directly-tested `dashboardAgg.ts` (the `reportAgg.ts`
precedent) so a wrong bucket/threshold fails a unit test, not just a screenshot:
- **Committee (AC-064):** backlog by status (kanban buckets, reuses `bucketOf`/`BUCKET_TONE`) **and** urgency, next
  scheduled meeting (`nextScheduledMeeting` — soonest upcoming, not list order) + agenda link, open action counts
  (Open/InProgress/Blocked) + overdue, last-5 issued decisions (server-ordered).
- **Secretary (AC-065):** triage-awaiting count, MoMs-awaiting count, overdue-beyond-escalation count, SLA-breach list
  with aging. Keeps Backlog/Next-meeting for design fidelity.
- **Chairman (AC-066):** Closed¬Ratified votes, escalated risks, escalated actions, topics deferred ≥2 (all-time —
  `includeClosed` fetch, since a twice-deferred topic may now be closed).

**Reconciliations (guardrail #14, flagged):** the design's personalized member cards (My topics/actions/votes,
Mentions) are **not** rendered — design extras, not required by any AC (AC-064 is committee-WIDE), and Mentions has no
backing system; the committee variant shows the AC-064 data via the design's card patterns instead. AC-064's urgency
breakdown + the committee recent-decisions/action-status cards and the chairman cards have no exact design card and
reuse the segment/stat/list patterns. **"Escalated actions" = overdue beyond the escalation threshold — AC-065's own
definition** (the Action aggregate has no Escalated status); one shared `ESCALATION_THRESHOLD_DAYS` const feeds both
AC-065 and AC-066. Votes carry no title on the wire, so the awaiting-vote row shows key + a generic label → `/votes/:key`.

**Housekeeping:** removed `components/ui/Card.tsx` — orphaned once the placeholder dashboard (its only consumer) was
replaced. i18n: new `dashboard.*` namespace, EN + AR hand-written (parity 1364).

**Gates:** tsc, 802 vitest (25 new), per-file coverage (global 99.80%), i18n parity, oxlint, build — all green
locally + on remote CI (PR #94, all 4 checks incl. e2e). FE-only; backend untouched. **Live `.dc.html` pixel-VR
PASS** (`e2e/p12-dashboard-vr.spec.ts` — real login + API seed on the Docker stack; all three variants captured
EN-light + AR-dark, pixel-faithful to `ACMP Dashboards & Reports.dc.html`, RTL/light-dark correct; the populated
Escalated-actions card confirms the AC-066 overdue threshold end-to-end; empty cards = fresh-stack seeding limits,
not fidelity gaps). **Next: operator squash-merge GO. PR3 = Reports shell completes P12.**

## P12-PR1 — Reporting thin registers (backend reads) — branch `feat/P12-pr1-report-reads`

**Phase kickoff + architecture call.** P12 (Dashboards & Reports) is the design's two surfaces behind a top-bar
toggle: the role **Dashboard** (`/`, pending AC-064/065/066) and the tabbed **Reports** (`/reports`, no AC — reuses
the P10g `rpt-*` card renderers). The toggle reconciles to the app's existing nav (`home` + `reports` are already
separate items), so no toggle is built. Planned as GO-gated slices: **PR1 backend reads → PR2 role dashboards
(closes the ACs) → PR3 Reports shell.** Operator GO on all three: build the thin registers, start with PR1, record
the right-sizing as an ADR.

**Right-sizing (ADR-0022, guardrail #11/#12).** Dropped the docs/domain/reporting-dashboards.md §5 Reporting **read-model / columnstore** layer
and the `/api/v1/reports/*` aggregation endpoints — for ≤20 users with bounded registers, client-side aggregation
over plain reads (the P10f/P10g precedent) is right-sized. **No chart library** — the `.dc.html` renders every chart
as CSS primitives; this **resolves OQ-022** (no lib to RTL-validate). Server **PDF** export + **Hangfire-scheduled**
reports + time-series/advanced dashboards (docs/domain/reporting-dashboards.md DB-13/14/22/23/24) deferred to PH-3; Research dashboard DB-18 =
PH-2. CSV export (client-side) is the one export in v1.

**Built (backend only — the four reads the dashboards need; FE consumers land in PR2).**
- **`GetDecisions` register** → `GET /api/decisions` (committee-wide, optional `status`/`limit`, issued-desc). The
  existing `?topic=` route now branches: with `topic` → per-topic history; without → the register. Feeds AC-064
  "last 5 issued decisions" + the Reports decision view. (`GetDecisions.cs`, `DecisionsEndpoints.cs`)
- **`GetVotes` register** → `GET /api/votes` (committee-wide, optional `status`; same `?topic=` branch). Feeds the
  chairman queue "votes awaiting approval" = Closed¬Ratified (AC-066) + the Reports voting view. (`GetVotes.cs`,
  `VotesEndpoints.cs`)
- **`GetMinutesAwaiting`** → `GET /api/minutes` with no `meeting` → the cross-meeting InReview approval queue. Feeds
  the secretary dashboard "MoMs awaiting approval" (AC-065). Only head versions are ever InReview, so no de-dupe.
  (`GetMinutesAwaiting.cs`, `MinutesEndpoints.cs`)
- **`Topic.TimesDeferred`** — a domain counter incremented in `Topic.Defer()` (spans reactivations), exposed on the
  backlog projection (`TopicSummaryDto`). Feeds the chairman dashboard "topics Deferred ≥2×" (AC-066). This is a
  small **domain change** (not a pure read) — flagged: the aggregate previously raised `TopicDeferredEvent` but kept
  no count. Forward-only migration `Topics_AddTimesDeferred` (single non-null column, default 0).

**Reconciliations flagged.** Design "role tabs" (*Viewing as…*) = a preview affordance, not a real control — the live
dashboard renders the current user's Keycloak role (PR2). Design covers only coordinator/chairman/member; the other
5 roles' fallback (default → Committee dashboard) is a PR2 decision. AC-064 (any member) content ≠ the design's
personalized "member" card set — PR2 composes the AC-required committee data using the design's card patterns and
flags the design→behavior additions.

**Gates (backend).** `dotnet build acmp.sln` clean (only known NU1902); **Application 679 + Domain 188 tests green**
(+6 new: decisions register scope/limit + status-filter, votes register status-filter, minutes InReview queue,
`TimesDeferred` ×2); `dotnet format --verify-no-changes` clean (BOM/EOL/imports fixed). Coverage + full-suite +
ArchUnit run pre-commit.

**AC audit.** **No flips yet** — PR1 is backend enablement; AC-064/065/066 flip to Met in **PR2** when the dashboards
render the data end-to-end (G-TRACE: an AC is Met only when demonstrable through the UI). Also indexed the two
un-indexed ADRs found in `docs/adrs/README.md` (**ADR-0021** P11e, **ADR-0022** this slice) — DI-01 hygiene.

**Next:** PR2 — the role Dashboard at `/` (three variants), consuming these registers + existing reads; closes
AC-064/065/066; live `.dc.html` VR.

---

## P11 audit remediation — fidelity + robustness fixes across the Governance surfaces (branch `fix/p11-audit-remediation`)

Remediated an adversarial P11 audit (ADR + Invariant + Decision→ADR promotion) against the local `.dc.html`
references and docs/domain/audit-and-records.md/10. **No AC verdict change** (Governance is PH-2, AC-less); this is fidelity + robustness.

**Design fidelity (matched to `Lists & Registers` / `Decision, Voting & ADR` / `Create Flows`):**
- Superseded/Deprecated ADR + Superseded/Retired Invariant bodies now **dim** (`.adr-body-muted`, opacity .62);
  removed a **false comment** that claimed a non-existent `is-retired` dimming.
- Superseded chip tone `warn`→**`neutral`** (ADR + Invariant), matching the design + the neutral Decision badge.
- New `shieldPlus` invariant glyph (tab + create) and `filterLines` funnel glyph (register "Showing"); the shared
  `adr` icon → book (design's ADR mark, app-wide incl. sidenav); empty states use the generic `checklist` glyph.
- Invariant register H1 → shared **"ADRs & Invariants"** (`adrs.title`); "Showing N of M"→**"Showing N"**.
- Tab bar: real **`<nav>` + `aria-current`** (was a fake `role=tablist` with no tabpanel contract); active label
  `--accent`→`--text` (accent underline only), inactive `--text-3`→`--text-2`; both counts shown on both tabs.
- Convert-to-ADR is the **primary + leading** CTA on the Decision detail (Create-follow-up-action → secondary);
  convert dialog gains the "will be created + bidirectional" preview box.
- Supersede banner: left arrow for "Supersedes" + 26×26 icon badge. ADR detail metrics restored to reference
  (aside 280px, body 24/28 + max 720, section 24, title 8, md chip, sticky aside — scoped). Owner field half-width.
- Empty state offers **Clear filters** always (design parity, was filter-gated). Removed dead `.adr-tab-soon` CSS +
  `adrs.tab.soon` / `decisions.convertAdrSoon` i18n keys + stale comments (EN/AR parity held at 1310/1310).

**Backend robustness:**
- Promotion is honestly two-transaction (ADR insert + edge write across two DbContexts, no MSDTC): the re-promote
  path now **idempotently heals a missing reverse edge**; **ADR-0021's "in-transaction" wording corrected**.
- **BE1 — `Governance_AdrSourceDecisionUnique` migration:** a **filtered unique index** on `Adr.SourceDecisionId`
  (`WHERE IS NOT NULL`) is the DB backstop behind the app guard — a same-instant concurrent double-promote can
  never insert two ADRs for one decision (non-promoted ADRs unconstrained). InMemory ignores `HasFilter`, so unit
  tests are unaffected; the race surfaces as a DB reject, never a duplicate.
- Supersede now emits the **successor's own** `AdrApproved` / `InvariantActivated` audit event (not only the prior's
  supersede event).

**Deliberately NOT built (flagged):** error-state request-id + Contact (needs correlation-id plumbing that doesn't
exist) and the saved-view control (app-wide chrome / design-preview artifact) — both out of P11 scope.

**Gates:** BE 1061 tests + per-file cov 99.80% + `dotnet format` clean + ArchUnit 40/40; FE 772 tests + per-file
cov met + i18n parity 1310/1310 + oxlint + tsc/vite build clean. Live pixel-VR skipped this pass (local web
container served a stale bundle after rebuild — an env caching quirk, not a code issue; operator elected to trust
the green gates). **Next:** operator merge GO.

---

## P11e — Decision→ADR promotion (FR-068): full-stack, Chairman-only (branch `feat/P11e-decision-to-adr`)

### 2026-07-04 — "Convert to ADR" from an issued Decision — new IDecisionReader + ITraceabilityWriter seams, promote command, wired FE button + confirm dialog

Fifth and final planned P11 slice — **full-stack** (backend + FE), delivering **FR-068**: the Chairman promotes an
**issued** committee Decision to a new ADR, pre-filled from the decision and bidirectionally linked. The design's
"Convert to ADR" (`ACMP Decision, Voting & ADR.dc.html` convertOpen) is a **confirm dialog, not a form** — the
pre-fill is server-side.

**Two new cross-module seams (ADR-0001; house pattern = Acmp.Shared.Contracts ports, no cross-module command send):**
- **`IDecisionReader`** (read) — Governance reads a decision's content (Title/Statement/Rationale/Alternatives +
  Status) to pre-fill the ADR, without touching the Decisions tables. Impl in `Decisions.Infrastructure`.
- **`ITraceabilityWriter`** (write, NEW kind of seam) — lets an authorized action record a *system* typed edge in
  the Traceability store without referencing Traceability.Application. Impl in `Traceability.Infrastructure`;
  idempotent per (source, target, relType); audited with a `System=true` marker. (The existing seams were all
  reads; this is the first write seam.)

**`PromoteDecisionToAdr` command** (Governance): reads the decision → guards eligibility (**Issued** only, else 409)
and **idempotency** (one ADR per decision — a second promote is 409 naming the existing ADR) → `Adr.Draft(...,
sourceDecisionId)` → writes a **`RecordedAs` edge (Decision → ADR)** via ITraceabilityWriter → audits
`Governance.AdrPromotedFromDecision`. **RBAC = new `Adr.Promote` policy = Chairman ONLY** (stricter than
Adr.Create; docs/domain/permission-role-matrix.md matrix row added, `"ADDDDDDD"`). Endpoint `POST /api/adrs/from-decision`. The bidirectional
link = `SourceDecisionId` on the ADR (ADR → Decision) + the RecordedAs edge (Decision → ADR), so both detail
panels cross-link.

**Pre-fill mapping** (decision → MADR-lite ADR; both already bilingual → no mirroring): Title←Title, **Context←
Rationale**, Decision←Statement, Drivers←Alternatives; consequences/options empty. ADR born **Draft** — the
Chairman refines it and runs the normal ADR lifecycle. (Context←Rationale is the loosest map — flagged; the
decision has no dedicated "context/problem" field.)

**FE** — the Decision detail already carried a **disabled "Convert to ADR" stub**; P11e wires it: a Chairman-gated
button (`hasRole(auth,'chairman')`, shown on Issued decisions) opens the new **`ConvertToAdrDialog`** (confirm-only,
matches the design) → `POST /api/adrs/from-decision` → navigate to the new `/adrs/:key`. A 409 surfaces inline
without navigating. `usePromoteDecisionToAdr` added to `api/adrs.ts`; `decisions.convert.*` i18n (en+ar). Dialog is
**lazy-mounted** (only when open) to keep the decision-page test suite's timing headroom under coverage load.

**Reconciliations flagged:** (1) the promotion UX lives on the **Decision** side (the design's button), so the ADR
create dialog's dropped "Linked decision" field stays dropped (P11b note resolved). (2) `RecordedAs` (not the
memory's guessed "DerivesFrom") is the curated Decision→ADR RelationshipType. (3) Context←Rationale pre-fill map.

**Gates (all green locally).** Backend: `dotnet format` clean (BOM/CHARSET fixed on the new files — [[ci-gates-run-locally-pre-push]]); build 0 errors; **all tests pass** incl. **5 promote handler + 4 endpoint-contract + 3 TraceabilityWriter** tests and the **PermissionMatrix** row for Adr.Promote; per-file coverage ≥95% (global 99.79%). FE: i18n parity (1311 keys); oxlint clean; **770 vitest (+5)**; per-file line cov 100% on `adrs.ts`/`DecisionPage.tsx`/`ConvertToAdrDialog`; tsc + vite build clean. No AC flips — Governance is PH-2/AC-less; traces to **FR-068** (done).

**★ ADR-0021 recorded** (operator GO): `Adr/ADR-0021-cross-module-write-seam.md` — cross-module *system* writes go through a `Acmp.Shared.Contracts` write port (extends ADR-0001), establishing `ITraceabilityWriter` as the first write seam deliberately (prior seams were read-only; direct cross-module command sends stay disallowed).

**★ Live `.dc.html` screenshot-VR — PASS (real fresh Docker stack).** New `e2e/p11e-promote-vr.spec.ts` (login chairman → seed a real issued decision: record Approved → link a follow-up action to satisfy the AC-029 gate → issue → drive Convert-to-ADR) captured, in EN-light + AR-RTL-dark: (1) the issued Decision detail with the Chairman-only "Convert to ADR" action, (2) the confirm dialog (pixel-faithful to `ACMP Decision, Voting & ADR.dc.html` convertOpen), (3) the promoted ADR — born **Draft**, pre-filled (Context←Rationale, Decision drivers←Alternatives, Decision outcome←Statement) with the **RecordedAs bidirectional link live** in its traceability panel ("UPSTREAM · RECORDED AS · DECN-…"). RTL fully mirrors, dark clean. **FR-068 verified end-to-end on the deployed stack.**

**★ P11 COMPLETE ★** — ADRs (P11a/b) + Invariants (P11c/d) + promotion (P11e) all shipped. **Next:** operator GO →
push → CI → squash-merge. The deferred **governance-lifecycle** slice (propose/approve/supersede/deprecate buttons
for the ADR + Invariant details) remains the only queued Governance follow-up; then P12 (Reporting) or beyond.

---

## P11d — Invariant UI: register + no-reference detail + create dialog (branch `feat/P11d-invariant-ui`)

### 2026-07-04 — Invariants tab on `/adrs`, `/invariants` register + `/invariants/:key` detail, Create-Invariant dialog (FE-only)

Fourth slice of **P11**, **frontend only** — consumes the live P11c backend as-is (no backend touched). Retires
the last disabled tab: the Governance register is now a two-tab surface (ADRs · Architecture Invariants) and the
Invariant record has a full read + create-Draft path. Mirrors the P11b ADR FE shape one-for-one.

**Structure.** New `api/invariants.ts` (reads + `useCreateInvariant`; **no propose hook** — see below) + five
`features/governance` files: `invariantMeta.ts` (status tone + category-dot maps + enum lists), a small shared
`GovernanceTabs` (extracted so `/adrs` and `/invariants` keep one tab bar in sync — the previously-disabled
Invariants tab is now a real `Link`), `InvariantsRegister`, `InvariantPage`, `CreateInvariantDialog`. Routes
`/invariants` + `/invariants/:key` (the notification deep-link target) in `App.tsx`; both fold to the `adrs` nav
area with a mono leaf in `breadcrumbs.ts`. i18n `invariants.*` namespace added to **both** locales, every
Status/Category/Scope **value** included (parity check only guards keys). New `gInv` grid + category-dot CSS.

**Operator decisions applied (this slice):**
- **Detail = read-only mirror** of `AdrPage` — facts (Status/Category/Scope/Owner/Date) + supersession banner +
  real `TraceabilityPanel traceType="Invariant"`. **No propose/activate/retire/supersede buttons** and **no
  markdown export** (an Invariant is a standing rule, not a MADR doc). All lifecycle transitions (ADR *and*
  Invariant) are deferred to one later governance-lifecycle slice.
- **Create = born Draft, no Status field** (matches the design form literally). So there is **no create→propose
  chain** (unlike `CreateAdrDialog`): create → route to `/invariants/:key`. A born Draft therefore has no UI path
  to propose it until the deferred lifecycle slice — an accepted consequence of the born-Draft choice.

**Reconciliations flagged (guardrail #14):** (1) **No-reference detail** — there is no `.dc.html` for the
Invariant detail; composed from the shared design system + docs/domain/information-architecture.md, cued by the register's detail-drawer facts
panel in `ACMP Lists & Registers.dc.html` (Category/Scope/Status/Owner/ID), with the register's **Viol.** column
& fact **omitted** (operator DEFER). (2) The register drops the design's 6th **Viol.** column → 5 columns
(ID·Statement·Category·Scope·Status). (3) **Rationale + Owner are REQUIRED** in the create dialog though the
design marks them optional — the backend `CreateInvariant` validator rejects an empty Rationale/OwnerUserId
(Option A, mirroring the Risk dialog's required linked-topic). (4) **ExceptionsPolicy** is in the read model but
not the design's create form, so it is not collected (sent null); it renders on the detail when present.
(5) **Category filter** chip is rendered (design parity) but disabled — the register query has no category
*filter* param (category is a sort key only); enabling it is a backend follow-up. (6) **OQ-036** stays OPEN
(Category set incl. Compliance).

**OQ-048 doc-fix folded in (operator instruction):** struck the `+ kind (Principle/Standard/Policy/Constraint)`
clause from `docs/domain/entity-lifecycles.md` §9's Invariant-create guard row — docs/domain/standards-and-best-practices.md §A is the concept SSoT and
§9 was the lone outlier. Doc-only, no CI.

**★ Latent P11a backend defect found + fixed during live VR (operator GO to fix).** The live VR pass surfaced
that `GovernanceDbContext` was never registered in `Acmp.Api/Infrastructure/MigrationRunner.cs` — so the whole
Governance schema (ADR + Invariant tables **and** `governance.adr_key_counters`) is never created on the deployed
Docker stack. This means **ADRs have been non-functional in deployment since P11a** (create → 500
`Invalid object name 'governance.adr_key_counters'`); it went unnoticed because P11a/b/c were backend/unit-gated
and the P11b live VR was only ever "pending." One-line root-cause fix: added `GovernanceDbContext` to the
MigrationRunner context list (+ using). Backend touch inside an FE slice, but it's a genuine defect blocking the
requested VR and the entire deployed module — operator approved. Repairs ADRs + Invariants together.

**Gates (all green locally).** i18n parity OK (1306 keys); oxlint clean (one pre-existing Toast warning,
untouched); **765 vitest tests** (was 735; +30 across the 5 new test files + the updated ADR-register tab test);
**per-file line coverage 100%** on all new `features/governance` + `api/invariants` files (global 99.21%);
`tsc -b` + `vite build` clean. No AC flips — Governance is PH-2/AC-less; traces to **FR-106** (create) +
FR-107 read/register.

**★ Live `.dc.html` screenshot-VR — PASS (real fresh Docker stack).** New `e2e/p11d-invariant-vr.spec.ts` (login
as secretary → seed 5 invariants via `/api/invariants`, 2 proposed+approved → Active, 3 Draft → captures) shot
the register + create dialog + no-reference detail in **EN-light and AR-RTL-dark** to `e2e/vr-out/`. Verified
pixel-faithful: register matches `ACMP Lists & Registers.dc.html` isInvTab (5 cols ID·Statement·Category·Scope·
Status, category dots, Draft/Active chips, Viol. column correctly dropped, ADRs-link + active Invariants tab);
create matches `ACMP Create Flows & Dialogs.dc.html` invariant form (Category·Scope·Statement·Rationale·Owner,
Rationale/Owner marked required per the backend); detail is the read-only no-reference composition (facts aside +
traceability, no lifecycle/export buttons). RTL fully mirrors (nav + columns flip, Arabic enum values) and dark
theme is clean. This also validated the MigrationRunner fix end-to-end (invariants create + activate now work on
the deployed stack). **FR-106 create/read verified live.**

**Next:** operator GO → push + remote CI green → squash-merge; then **P11e** (Decision→ADR promotion, FR-068).
The deferred **governance-lifecycle** slice (propose/approve/supersede/deprecate buttons for both ADR + Invariant
detail) remains queued.

---

## P11c — Invariant backend: the `Invariant` aggregate in the Governance module (branch `feat/P11c-invariant-backend`)

### 2026-07-04 — Architecture Invariant aggregate (AIV-YYYY-###); W18/W21 lifecycle, supersede-not-edit (backend)

Third slice of **P11**. Added the **`Invariant` aggregate** to the existing `Acmp.Modules.Governance` bounded
context — a sibling of `Adr` in the same module/schema. **Backend only; the UI is P11d.** The skeleton mirrors
the `Adr` aggregate (P11a): same key-counter table, register-query/detail shape, endpoint style, InMemory test
harness; the lifecycle/immutability mirrors Decisions/ADR (supersede-not-edit, `SupersededBy…Id`,
AuditEvent-per-transition via the existing `IAuditSink`, no new store, **no hash-chain** — guardrail #5).

**★ Blocker resolved before coding — the `Kind` field dropped (OQ-048).** The re-orient note claimed "docs/domain/entity-lifecycles.md
+ the design form require an Invariant `Kind` (Principle/Standard/Policy/Constraint)". Reading the design form
directly (`ACMP Create Flows & Dialogs.dc.html` `invariant`) showed it has **Category · Scope · Statement ·
Rationale · Owner and NO Kind**; FR-106 also omits it; and **docs/domain/standards-and-best-practices.md §A** (the concept SSoT per README §G)
makes Architecture Invariant a *sibling* of Principle/Standard/Policy/Constraint, not their parent (Policy =
external register, Constraint = `CON-###`). Only docs/domain/entity-lifecycles.md §9 mentions a Kind. **Operator GO (2026-07-04): drop
Kind.** The aggregate carries **Category + Scope** only. Recorded as **OQ-048** (docs/domain/entity-lifecycles.md §9 owes a correction).

**Aggregate.** `Invariant` (in-app key `AIV-YYYY-###`, README §F): `Category` (enum — OQ-036 default set incl.
Compliance) + `Scope` (enum — FR-106's single/multi-stream/platform/org-wide) + `Statement` (req) + `Rationale`
(req) bilingual prose + optional `ExceptionsPolicy` (docs/domain/standards-and-best-practices.md §A.5) + `Owner` (userId + name snapshot, FR-106) +
activation attribution + the supersession chain (`SupersededByInvariantId`/`SupersedesInvariantId` + reasons) +
`RowVersion` (ADR-0018). Lifecycle **Draft → Proposed → Active → (Retired | Superseded)** + Proposed → Draft
(request-changes); once Active the statement is frozen (correction = a new superseding invariant).

**Features (vertical slices).** CreateInvariant · UpdateInvariantDraft · ProposeInvariant (notifies the Reviewer
roster, W18) · ApproveInvariant (activate; **SoD soft** — the author may approve, recorded `AuthorApprovedSelf` off the
server-derived creator `CreatedBy`, not the client Owner field;
committee fan-out) · RequestInvariantChanges · RetireInvariant (committee fan-out) · SupersedeInvariant (authors
+ activates a successor, links both directions, freezes the prior — one transaction, W21 ordering) ·
GetInvariantByKey (resolves peer supersession keys in-module) · GetInvariantsRegister (status filter + substring
search + sort/paging; FTS deferred to Search). Endpoints `/api/invariants` with the existing
`Invariant.Create`/`Invariant.Approve` policies (already in the docs/domain/permission-role-matrix.md matrix rows 21/22 + registered in P11a —
nothing to wire). Migration `Governance_InvariantInit` (invariants table + Key/Status indexes; the shared
`adr_key_counters` table is reused for `AIV` rows). ArchUnit already covers the Governance assemblies (40/40).

**Reconciliations flagged (guardrail #14/#11):** (1) **OQ-048** Kind dropped (above). (2) **docs/domain/entity-lifecycles.md §9 splits its
Draft/Proposed field guards** (statement+category at Draft, scope+rationale at Propose); the design's single
create-dialog collects everything at once, so we require all fields at Draft (as ADR does) — noted as an
intended simplification. (3) docs/domain/entity-lifecycles.md §9 says "notify **stream owners** on Activate", but an invariant's scope is
a *class* (single/multi/platform/org-wide), not a link to a specific stream — no stream roster to resolve — so
activation notifies the **committee** (P11d flag: add a stream link if per-stream targeting is ever wanted).
(4) **Category enum = OQ-036 default** (adds Compliance over FR-106's list) — OQ-036 stays OPEN (committee to
confirm). (5) A **request-changes** (Proposed → Draft) back-edge + `UpdateInvariantDraft` are included though
docs/domain/entity-lifecycles.md §9's table omits them — symmetric with the ADR sibling (§8), otherwise a proposed invariant needing a
fix would be stuck.

**Gates (all green locally).** Build clean; **1038 backend tests** (was 1007; +31: 10 domain, 15 application,
6 API HTTP-contract through the real policy pipeline); **per-file coverage ≥95%** (global 99.79%); `dotnet
format --verify-no-changes` exit 0 (BOM/CHARSET fixed on the new files); EF migration generated. No FE change
(P11d). No AC flips — Governance is PH-2/AC-less; traces to **FR-106/107** (create + lifecycle + supersede +
register done) with FR-108/109 violations **deferred** (operator) and FTS deferred to Search.

**Next:** operator GO → PR + remote CI green → squash-merge; then **P11d** (Invariant UI — invariants tab on
`/adrs` + a no-reference Invariant detail + Create-Invariant dialog).

---

## P11b — ADR UI: register + MADR detail + create dialog (branch `feat/P11b-adr-ui`)

### 2026-07-04 — the `/adrs` register, the MADR-lite detail, and the create dialog (FE-only)

Second slice of **P11**. Retired the `/adrs` PlaceholderPage and shipped the full ADR read+create surface
against the P11a backend, composed to the design (guardrail #14): the **`ACMP Lists & Registers.dc.html`**
isAdrs tab (register), the **`ACMP Decision, Voting & ADR.dc.html`** isAdr screen (MADR detail), and the
`adr` form in **`ACMP Create Flows & Dialogs.dc.html`** (create dialog). Mirrors the Risks P10b FE structure
(register/detail/create-dialog + api module + meta + css + tests). **FE-only — no backend touched.**

**Files (new).** `api/adrs.ts` (types + `useAdrsRegister`/`useAdrsCount`/`useAdr`/`useCreateAdr`/`useProposeAdr`);
`features/governance/` → `adrMeta.ts` (statusTone + client-side `.md` export, FR-104), `AdrsRegister.tsx`,
`AdrPage.tsx`, `CreateAdrDialog.tsx`, `governance.css`; plus 5 test files. **Wiring:** `App.tsx` routes
`/adrs` → register + `/adrs/:key` → detail; `nav/breadcrumbs.ts` adds the `adrs` mono leaf crumb; i18n
`adrs.*` namespace in en + ar (mirrored, parity-checked).

**Create dialog — born Proposed, not inert Draft (advisor-caught).** The design's create form defaults
Status to "Proposed" and the register renders a first-class `proposed` row, so an ADR is *not* born Draft.
The dialog keeps a **Draft/Proposed selector** and maps Proposed → **create (Draft) then POST `/propose`**
(both routes are `Adr.Create`, so the author is permitted). This lands ADRs in a real governance state
instead of an un-advanceable Draft. A `createdRef` guard means a failed-propose retry re-proposes without a
duplicate create. Content (Title/Context/Decision + optional Consequences +/−) is entered once and MIRRORED
to both locales.

**Design↔behaviour reconciliations (flagged; design owes updates):**
- **5 states + canon "Approved" label.** The design's 3 state-tabs (proposed/accepted/superseded) are a
  PREVIEW toggle, not a control — the detail renders a fixed status chip; the register renders Draft +
  Deprecated chips the design's preview omits, and uses "Approved" (canon) not "accepted".
- **Read-only detail (⚠ merge-GO callout).** No propose/approve/supersede/deprecate BUTTONS this slice (the
  design shows none on the ADR detail — only Export .md). The endpoints exist (P11a); their UI is a later
  lifecycle slice (mirrors Risks P10b). Consequence: a created ADR advances to Proposed via the create
  dialog, but approve/supersede/deprecate have no UI yet.
- **Register supersede column = marker only.** The lean `AdrSummary` carries `IsSuperseded` (bool), not the
  peer keys/direction; the register shows a "Superseded" marker, the full directional chain is on the detail
  (`GetAdrByKey` resolves peer keys). Not touching the merged backend DTO for a FE slice.
- **Create form:** Status-select kept (mapped to lifecycle, above); the design's **Linked-decision** field is
  dropped (= the P11e promotion path, `SourceDecisionId`); Consequences split to optional Positive/Negative
  to match the read model; Decision-drivers + considered-options not collected (optional, no draft-edit UI).
- **Tabs:** ADRs live; **Invariants** present-but-disabled (P11d); **Violations** omitted entirely (operator
  DEFER decision). **Category** filter rendered-but-disabled (no server category). Metadata "Tags" omitted
  (not modelled).
- **Options:** a non-chosen option renders as a neutral "Alternative" (the design's "Rejected" tag is a
  stronger claim the model — chosen vs not — doesn't carry).

**Traceability.** The detail's aside uses the real shared `TraceabilityPanel traceType="Adr"` (ArtifactType
already has Adr) where the design had static links. `Adr` is not in `GRAPH_FOCUS_TYPES`, so the panel's
"Open graph" affordance is absent (graceful) — an ADR isn't a graph-focus root yet.

**Gates.** 735 vitest green (+33: 7 api, 5 adrMeta, 9 register, 7 detail, 5 dialog); per-file line coverage
100% on all 5 new files (gate 99.78% global ≥95%); tsc `-b` clean, oxlint clean, i18n parity OK (1240 keys),
`vite build` clean. Register + detail + dialog are axe-clean (WCAG 2.2 AA). **Live `.dc.html` screenshot-VR
pending a running-stack pass** (like P10b) — composed pixel-faithful to the reference; offer a VR pass at
merge-GO.

**Next = P11c** (Invariant backend — the `Invariant` aggregate in the Governance module).

---

## P11a — ADR backend: the Governance bounded context + the Adr aggregate (branch `feat/P11a-adr-backend`)

### 2026-07-04 — new Governance module (ADRs); MADR-lite lifecycle, supersede-not-edit, W17/W21

First slice of **P11 — ADRs & Invariants**. Stood up the new **`Acmp.Modules.Governance`** bounded context
(Domain/Application/Infrastructure, schema `governance`) and the **`Adr` aggregate** — backend only; the UI is
P11b. Skeleton mirrors **Risks** (module layout, key-gen + counter, register query, endpoint style, InMemory
test harness); the **lifecycle/immutability mirrors Decisions** (supersede-not-edit, `SupersededBy…Id`,
AuditEvent-per-transition via the existing `IAuditSink`, no new store, **no ADR hash-chain** — guardrail #5
hash-chains only votes/decisions/audit).

**Aggregate.** `Adr` (in-app key `ADR-YYYY-###`, distinct from the planning `ADR-####` files, README §F):
MADR-lite fields — Title/Context/Decision required, DecisionDrivers/Consequences(+/−) optional bilingual
prose, an owned **`AdrOption`** collection (considered options with a `chosen` flag, same shape as Risk's
Mitigation), Author snapshot, `SourceDecisionId` (nullable, for the P11e promotion), the supersession chain
(`SupersededByAdrId` + reason, `SupersedesAdrId`, `DeprecationReason`), approval attribution, and a
`RowVersion` concurrency token (OQ-043 is resolved for new modules, ADR-0018). Lifecycle
**Draft → Proposed → Approved → (Superseded | Deprecated)** + Proposed → Draft (request-changes); once
Approved the content is frozen (no setters; correction = a new superseding ADR, FR-101).

**Features (vertical slices).** CreateAdr · UpdateAdrDraft (the request-changes revise loop) · ProposeAdr
(notifies the Reviewer roster, W17) · ApproveAdr (**SoD soft** — the author may approve, recorded as
`AuthorApprovedSelf` in the audit payload, operator decision 2026-07-04; committee fan-out) · RequestAdrChanges ·
DeprecateAdr (committee fan-out) · SupersedeAdr (authors + approves a successor, links both directions, then
freezes the prior — one transaction, W21 ordering, mirrors SupersedeDecision) · GetAdrByKey (resolves the
peer supersession keys in-module) · GetAdrsRegister (status filter + substring search + sort/paging; **FTS
FR-102 deferred to the Search phase**). Endpoints `/api/adrs` with the existing `Adr.Create`/`Adr.Approve`/
`Adr.Supersede` policies (already in the docs/domain/permission-role-matrix.md matrix — nothing to register). ArchUnit
`Governance_should_not_depend_on_other_modules` added; 40/40 boundary tests green.

**Authorization note (recorded).** Standalone ADR authoring is **Chairman/Secretary only**: `Adr.Create`'s
allow-if-owner cells (Member/Reviewer) need an ownership relationship, which a bare create has none of, so the
ABAC handler denies Member/Reviewer at create time. Consistent with the Risk pattern; the MediatR
`AllowedRoles` backstop lists the AiO roles but the endpoint is the primary gate.

**Reconciliations flagged (guardrail #14/#11, for P11b + the design update):** (1) the design ADR detail
illustrates only **proposed/accepted/superseded** (3) while the canonical model is **5** states and the label
is **"Approved"** not "accepted" — implemented the 5 canonical states; the design owes Draft/Deprecated chips +
the label. (2) FTS repository search deferred. Cross-module ADR links (FR-103) ride the existing self-describing
Traceability edges (ArtifactType already has `Adr`) — **no new Traceability seam needed** in P11a.

**Gates (all green locally).** Build clean; **1007 backend tests** (was 975; +24 Governance: 10 domain, 12
application, 6 API HTTP-contract through the real policy pipeline, +2 ArchUnit); **per-file coverage ≥95%**
(global 99.76%); `dotnet format --verify-no-changes` exit 0; EF migration `Governance_Init` generated. No FE
change (P11b). No AC flips — Governance is PH-2/AC-less; traces to FR-099/100/101/102/104 (partial: create +
lifecycle + supersede + register done; FTS + Markdown-export → P11b/Search).

**Next:** operator GO → PR + remote CI green → squash-merge; then **P11b** (ADR register + MADR detail + create
dialog, fidelity to `ACMP Decision, Voting & ADR.dc.html` `isAdr` + `Lists & Registers` + `Create Flows`).

---

## P10-graph-dialogs — impact-graph direction fix + create-dialog fidelity (branch `feat/P10-graph-dialogs`)

### 2026-07-04 — operator-driven live visual review of the impact graph + the three create dialogs

Follow-up from a hands-on visual test of the seeded stack. Operator reported: the impact-graph edges
"flow under" the focus node, the graph/list/aside disagree on direction, and the create dialogs had
alignment + header issues. Full evidence-based review (real graph JSON + live screenshots) + advisor
consult, then operator-approved fixes.

**Impact-graph tier direction — root cause + fix (operator-approved deviation, guardrail #14).**
Verified against the backend enum (`DependencyKind.cs`: *"From --Kind--> To"*): `From --DependsOn--> To`
means *From depends on To*, so **To is the prerequisite (upstream), From is the depender (downstream)**.
The composer (mirroring the reference `buildTiers`) assigned tier sign from the **raw arrow** only —
inverting `DependsOn`/`BlockedBy`. For a focus whose only neighbour is an upstream hub, that hub's whole
subtree landed on the *far downstream* side, so every subtree edge crossed the focus column ("flows
under 004"). Rendered a 3-way comparison (Current 4 crossings / Option 1 partial / **Option 2 zero**) for
the operator, who chose **Option 2 + GO** on the deviation.
- **`ImpactGraphComposer` now uses Option-2 tiering:** (1) `DependsOn`/`BlockedBy` reverse the stored
  arrow (aligning the graph with the register/panel/backend semantics); (2) a node's **side is set at its
  first hop from the focus and inherited by its whole subtree**, so a branch never crosses the focus.
  Magnitude stays the BFS depth. Verified live on `TOP-2026-004`: focus→+1(depender)→+2→+3, all
  downstream, **0 crossing edges**.
- **Direction consistency:** `buildPanelRows` (detail-panel) already inverted for inbound; the graph-page
  **aside `buildTypeGroups` did not** (dep groups used a flat `DEP_DIR[kind]`). Fixed it to invert dep
  direction per inbound/outbound (single dir, else `related`) — so aside, list, and graph now agree.
- **DEVIATION FROM REFERENCE recorded:** the design's `.dc.html` `buildTiers` still encodes the old
  raw-arrow sign (its sample data authors `depends` edges as prerequisite→depender so the pathology never
  shows). Per guardrail #14, **the design is owed an update** to the Option-2 semantics. Flagged here as a
  design-update owed; the platform is the interim source of truth for this behaviour.

**Create dialogs (risk / dependency / relationship).**
- Header icon tile → **38px accent-tinted** (`.dialog-icon` 40→38 + new `.dialog-icon-accent`, `tone="accent"`
  on all three), matching the design "Create Flows" tile instead of the neutral confirm tone.
- **Paired-field vertical misalignment fixed:** the global `.field + .field { margin-block-start }` was
  pushing the right column (Impact / Linked-topic; Type / Artifact) down. Scoped `align-items:start` +
  `.field` margin reset on `.rsk-create-row` and `.dep-picker-row`; and the risk pairs no longer collapse
  on narrow viewports (dropped the ≤860px stack for the dialog rows). Operator kept the paired layout as-is.

**Tests + verification.** Backend **975 green** (+2 new composer tests: `DependsOn_reverses_direction`,
`Subtree_inherits_the_branch_side`; updated `Builds_signed_tiers…` + `Inbound_blocks_dependency_is_upstream`
to the corrected semantics). FE build + traceMeta tests green. Live-verified against real seeded data
(graph JSON + screenshots; dialog screenshots). No AC flips (fidelity/behaviour-consistency; FR-096 graph
stays Met, now correct-for-real-data).

**Next:** commit as one PR (this branch), CI green, then the design `.dc.html` update for the tier semantics.

---

## P10-review — remediation of the adversarial P10 audit (branch `feat/P10-review`)

### 2026-07-04 — fixed the 7 MAJOR + the MINOR fidelity/i18n cluster from the exhaustive P10 audit

An adversarial, element-by-element audit of all 7 merged P10 slices (Risks · Dependencies · Traceability ·
Impact-graph · Reports) against the governing `.dc.html` files + backend correctness returned **GO on
correctness/architecture** (972 BE tests, 702 FE, per-file cov ≥95%, ArchUnit 36/36, secrets clean, audit
events present, AC-029 gate holds) but **NO-GO on "fidelity-reconciled done"**: 7 MAJOR (6 fidelity + 1
governance) + a MINOR cluster incl. one **Arabic-gender bilingual defect** (guardrail 9). This slice
remediates them (pattern = the P9-review `b904890` fast-follow). All gates green locally.

**Backend.**
- **ADR index (MAJOR):** `docs/adrs/README.md` stopped at ADR-0018; **indexed ADR-0019 (self-describing edges,
  amends 0008) + ADR-0020 (impact-graph read-time composition)** — both existed on disk + cited in code but
  were unindexed (violated the README's own "discoverable index" rule).
- **Impact-graph BFS (MINOR):** `ImpactGraphComposer` outer loop gated on `!partial`, so one node's transient
  read failure / the MaxNodes ceiling **halted every sibling branch** at that level. Decoupled — the walk is
  bounded by depth + the `expanded` cycle-guard + the ceiling; `partial` is still returned truthfully but no
  longer truncates unrelated branches. Added a regression test (`Failed_branch_does_not_halt_sibling_expansion`
  — a sibling reaches depth 3 after another branch fails at depth 2; would fail under the old gate).
- **ArchUnit gap (MINOR):** `Decisions_should_not_depend_on_other_modules` omitted **Traceability + Actions**
  (the AC-029 gate consumes both via Shared ports) — added them to the forbidden list so a future direct
  reference is caught.

**Risks.** Filter-chip order fixed to **Status → Owner → Exposure** (was Status→Exposure→Owner, MAJOR); gRsk
last-4 column widths → **120/130/112/96**; exposure chip **20px** (2px shorter than the status chip); prob/impact
cell **12px**; linked-cell **weight 500**; matrix legend **colon** (via `::after`, locale-safe); Related panel
**"Traceability — both directions" subtitle** (`risks.relatedSub`); create form pairs **Owner + Linked-topic** on
one row; empty state gains a **"New risk"** CTA; New-risk **plus icon 16**; skeleton head bar **54px**.

**Dependencies.** Blocked-work toggle rebuilt to the shared **`.fchip` metrics (34px, accent-on)** — was a
bespoke 30px pill that **turned danger-red when active** and misaligned with the Relation chip (2 MAJOR);
Relation-cell arrow → **horizontal `arrowRight` flipped by direction** (`.dep-rel-back`) instead of a vertical
↑/↓ (MAJOR); relation cell **11.5px / gap 6**; Blocked cell renders **nothing** when not a blocker (was "No") and
the blocked badge uses the shared **`.badge` metrics + the new `ban` glyph**; Relation col **132px**, From/To
min **140px**; empty state gains a **"New dependency"** CTA (Chair/Sec); detail Blocked fact → **"Yes"/"No"**
(new `deps.yes`); detail blocked badge gets the ban glyph.

**Traceability / Impact-graph.** Aside footer button now uses **`trace.graph.openGraph`** ("Open dependency
graph") instead of `graphTitle` ("Dependency & impact") which duplicated the section `<h2>` (MAJOR); **matched
blocked/cross nodes now get their affirmative `st-danger`/`st-warn` border** (`hit` field → `.ig-node--hitBlocked/
--hitCross`) — previously only the non-matches dimmed (MAJOR); non-focus nodes regain **`box-shadow: var(--shadow)`**;
the **List fallback now honours the Blocked/Cross toggles** (`buildListRows` takes `HighlightState`; rows tint via
`igl-row--hit*`) and draws the **tree-connector stub** on indented rows; list rel column **78px**; `openGraph` copy →
**"Open dependency graph"** (EN+AR). SVG geometry / RTL flip math untouched.

**Reports.** **Arabic-gender fix (the bilingual defect):** the matrix **impact-row** axis now uses a distinct
**masculine** key set `reports.impactLevel.*` (`منخفض/متوسط/عالٍ`) instead of reusing the feminine probability
`reports.level.*` (`…عالية`); EN unaffected. Drill icon → diagonal **`arrowUpRight` (↗)**, no RTL flip (matches
the reference external-link glyph); risk-exposure sub restores the **"probability × impact ·"** prefix; header
margin **`--sp-4`**, title→lead rhythm **`--sp-1` (4px)**, lead **13px**; skeleton head bar **13px/6px**.

**Accepted / deferred (NOT changed — flagged for the operator, guardrail 11):**
- **AiO dead path (B4, CONCERN):** Member/Reviewer are listed allow-if-owner on `Risk.Manage`/`Dependency.Create`
  but no endpoint sets an `ITopicScopedResource`, so the AiO path never fires (fails **closed** to Chair/Sec +
  delegation). Platform-wide (Actions/ADR too), not a P10 regression. Left as-is: the real fix is either wiring
  ABAC resources (a dedicated authz slice) or trimming the role lists (touches the 248-case permission matrix) —
  neither belongs in a fidelity review. **Recorded for a future authz slice.**
- **"Showing X of Y" (RK2/D13) + register-specific empty/error copy (RK9/RK10/D14):** kept — these are an
  **app-wide** pattern (actions/deps/risks all share it); matching one register's `.dc.html` verbatim would break
  consistency with the others. Better UX; documented deviation.
- **Reports exposure-KPI headline (R3) + Critical stat double-count (R9):** the `/reports` page is a **no-reference
  composition** (P10g); adding the 28px KPI headline or reworking the tile semantics risks inventing layout the
  design doesn't govern — deferred to **P12 Reporting** (which builds the full Reports shell).
- **Dependency detail "Impact" narrative (D16):** needs the exact reference prose (no-reference edge detail) —
  deferred rather than invent copy (guardrail 14).
- **Token-hygiene px in traceability/graph.css + aside related-arrow (T8):** SVG geometry px are legitimately
  bespoke; the aside arrow reading is arguably clearer — left, low value.

**New shared assets:** `ban` + `arrowUpRight` icons (`components/icons.tsx`). **New i18n keys** (EN+AR parity
1172): `risks.relatedSub`, `deps.yes`, `reports.impactLevel.*`; **changed copy:** `trace.graph.openGraph`,
`reports.activeCount`.

**Gates (all green locally):** BE `dotnet test` **972 green** (+1 composer regression); `dotnet format
--verify-no-changes` clean; ArchUnit **36/36**. FE `tsc -b && vite build` clean; oxlint clean; **702 vitest green**
(2 test assertions updated for the new aside label + reports sub copy); **per-file coverage gate exit 0**
(≥95% lines, `perFile:true`); i18n parity **1172**. **No AC flips** (fidelity/hygiene remediation — AC-029 stays
Met, AC-062/063 stay Partial, FR-095 stays Partial Topic-scope). Live 4-mode VR re-capture on a running stack
recommended before merge (static computed-value parity done; live render deferred, as in P10b/e).

**Next:** operator merge GO (squash) → P10 fidelity-reconciled → advance to **P11 (Governance ADRs/Invariants)**.

---

## P10g — Risk & Dependency reports: the `/reports` analytics surface (branch `feat/P10g-risk-dep-reports`)

### 2026-07-04 — the risk-exposure + dependency dashboards (frontend; the LAST P10 slice)

**Scope.** Seventh and final P10 slice. Replaces the `/reports` `PlaceholderPage` with a focused **Risk &
Dependency reports** page, REUSING the card renderers/tokens of `ACMP Dashboards & Reports.dc.html` (matrix /
stat / bars). The full Reports IA — the Dashboard/Reports toggle, role tabs, the 6 view-tabs
(executive/committee/stream/decisions/actions/audit), Export, and the filters row — is **deferred to P12
Reporting** (guardrail #14: this page is a **no-reference composition** of the design's card system, not a
verbatim port). Plan-first past `ecc:architect` (2 rounds) + advisor (both GO-WITH-FIXES, all folded).
**Operator scope call: chose the "Fuller" option — BUILD the two by-stream cards now** (override of both
reviewers' "defer by-stream" lean, same pattern as P10f's full-stack override).

**Delivered (FE-only, no backend — `src/Acmp.Web/src/features/reports/*`).**
- **`ReportsPage.tsx`** at `/reports` — a 2-col card grid over **six** cards, every number composed
  **client-side** from three existing REST reads (`useRisksRegister` + `useDependenciesRegister` +
  `useBacklog`, each at `pageSize=500`; no server pageSize clamp on any — verified). Loading (shimmer
  skeletons, gated behind `prefers-reduced-motion`), **error** (explicit branch — the design specs none;
  retry refetches all three), and **empty** (both registers total 0) states.
- **`reportAgg.ts`** — pure, 100%-tested reducers:
  - **Risk exposure** 3×3 probability×impact **count matrix**, cells coloured by **severity** (`L×I`:
    ≤2 success, 3–4 warn, ≥6 danger) — verified to reproduce the design's 9 authored `mcell` zones exactly.
    Scoped to **active** risks (exclude Closed + Accepted); "N active" sublabel.
  - **Risk stat** (Open / Mitigating / High-severity / Critical) + **Dependency stat** (Total / Open /
    Blocked via `isBlocker` / Resolved) + **Dependencies by relation** bars (the 4 kinds).
  - **Risk by stream** + **Cross-stream dependencies** (blocked-by-stream) bars — the by-stream join:
    `risk.subjectId`/`dep.{from,to}Id` → the linked **Topic's `streams`** (only Topic carries streams).
    Multi-stream topic counts under **each** of its streams (intended per-stream semantics; `Σbars ≥` the
    **distinct** KPI, which is never the bar sum); non-Topic subjects/endpoints + empty-stream topics
    excluded (flagged Topic-scope). Topics fetched with **`includeClosed=true`** — an active risk can hang
    off a closed topic, and dropping those would silently undercount the tally (architect's trap catch).
- **`reports.css`** — logical properties throughout; card/matrix/stat/bars/skeleton primitives px-matched to
  the `.dc.html`; local `acmpShimmer` keyframe.

**Reconciliations flagged (guardrail #14, design→behavior).** Whole page = a **no-reference composition** of
the design's card renderers (the design's own Reports view has exactly ONE buildable risk card — the matrix —
and ZERO from-wire dependency cards; its dep card is by-stream, which we build here). Matrix coloured by
**severity** (merges High+Critical exposure bands into one danger tint, per the design's authored cells).
By-stream cards show the raw stream **CODE** (no localized stream name on the wire — `StreamRef{code,nameEn,
nameAr}` rides only on `Member`; a Membership names seam is deferred, P10f precedent). Deferred to P12
(flagged in-UI via the `p12Note`): the view-tabs, Export, filters row, role dashboards, and non-risk/dep
cards. Drill + "View detail" affordances repoint to the real `/risks` and `/dependencies` registers.

**Decisions folded (architect + advisor).** FE-only aggregation over one full list per register (the matrix
needs per-row L×I, which count fan-out can't give; ≤20 users ⇒ one list + reduce is right-sized — the code
already comments "a stats endpoint would be overkill"). Reads are authenticated-only ("any authenticated
committee member" — no role policy), so an Administrator with `reports:view` nav access won't 403 to a blank
page. **OQ-047 scoped partial-advance:** the by-stream cards adopt the *inherit-from-topic* stream model
(model b) **for reporting aggregation only**; P10f's per-edge Topic↔Topic cross-stream semantic is unchanged
— recorded as an addendum, NOT a closure (the multi/zero-topic semantic is exactly why FR-095 stays partial).

**Gates (local, all green).** `tsc -b` clean · oxlint clean · **702 vitest pass** (+18 new: 11 reducer incl.
the money-path by-stream properties — multi-stream topic lands in each stream bar, `Σbars ≥ distinct KPI` —
+ 7 component covering all four states + retry-refetches-all-three + axe) · **per-file line coverage 100% on
both new `.ts`/`.tsx` files** · i18n EN/AR parity by hand (37 `reports.*` keys both locales, 1167 total each,
AR ported from the `.dc.html` `t` object) · axe AA clean.

**AC.** No Must-AC flips. **FR-138** (per-stream report of topics/decisions/actions/**risks**, *Should*/Ph2)
→ **Partial** — the two by-stream cards stand up per-stream risk + dependency breakdowns (no date-range
filter / export yet). **FR-095** (cross-stream) advanced in the reporting surface, stays **Partial
(Topic-scope, OQ-047)**. **FR-135/136/137** (role dashboards, AC-064/065/066) remain **Pending → P12** (this
is the *Reports* view, not the role *Dashboard* tab).

**Live VR pass (matrix, the reference-backed surface).** Booted the real stack (`npm run e2e:up`; KC bootstrap
`ChangeMe_KC#2026`) and ran `e2e/p10g-reports-vr.spec.ts`: real Keycloak PKCE login → secretary seeds 6 risks
whose `(impact, likelihood)` reproduce the design's authored matrix + a blocking cross-topic dependency →
opens `/reports`. Captured **EN-light + AR-dark** to `e2e/vr-out/` and screenshot-compared **pixel-faithful to
`ACMP Dashboards & Reports.dc.html`**: the 3×3 matrix layout (54px rowhead + Low/Med/High probability columns,
High/Med/Low impact rows, "Probability" caption), the **severity-zone cell tinting matches all 9 authored
cells**, stat tiles, relation bars, by-stream bars ("Topic-linked only") and the P12 note all render per the
design. **RTL (AR-dark): the Probability axis mirrors and the danger cells track the axis** (High×High stays
danger) — the top visual risk, resolved — with clean dark tokens and full Arabic labels. (Counts read higher
than the design's 6 only because both VR tests seed the shared DB — a harness artifact; the zone pattern is
exactly proportional. The spec asserts nothing, screenshots only, and no committed spec counts risks/deps →
no CI interference.)

**Next.** Local gates green + live VR pass → push → 4 CI checks → operator merge GO (squash). **P10 COMPLETE**
after merge.

---

## P10f PR2 — Impact-graph frontend: bespoke SVG page + group-by-type aside (branch `feat/P10f-graph-ui`)

### 2026-07-03 — the `/traceability/:type/:key` impact-graph UI (frontend; PR2 of 2)

**Scope.** Second half of P10f — the user-facing page that consumes the merged PR1 endpoint
(`GET /api/traceability/graph/{type}/{id}?depth=1..3`). Plan-first past `ecc:architect` (which looped the advisor and
confirmed all three rulings) → folded → operator GO → built → local gates green. FE-only; no backend/ADR change.

**Delivered (one focus-centric page, ported from `ACMP Traceability & Dependencies.dc.html`).**
- **Route `/traceability/:type/:key`** (one generic route; `:type` validated against `ARTIFACT_TYPES`; invalid → redirect home).
  **Focus identity**: WARM = the detail-page "Open graph" button passes `{id,title}` via router state; COLD deep-link =
  a by-key detail fetch (`useTopicDetail`/`useDecision`/`useAction`/`useRisk`) → `{id,title}`. A valid-but-non-focusable
  type with no warm state shows an "open from the artifact's page" state (never crashes). Type-aware breadcrumb (Topic→Backlog…).
- **(a) Relationships aside** (320px, group-by-TYPE): new pure `buildTypeGroups` in `traceMeta.ts` — **dependency edges group
  by KIND, relationship edges by far ARTIFACT TYPE** (architect ruling; a Topic can sit under both "Depends on" and "Blocks").
  Reuses the two 1-hop panel reads (already warm in cache on the warm path), independent of the depth selector.
- **(b) Dependency & impact section**: Graph/List segmented control · depth 1–3 · Blocked + Cross-stream highlight toggles.
  - **`useTraceGraph(type,id,depth)`** = ONE TanStack query (the FE never re-BFSes — the backend owns the traversal).
  - **`graphLayout.ts`** — pure geometry (100% unit-tested): bins the backend's SIGNED tiers into columns, cubic-bezier
    edges, per-edge colour by **direction** (`relDirection` over the real 16-RelationshipType / 4-DependencyKind vocab,
    NOT the design's toy 8-key map), highlight styling, `nextFocusIndex` (linear, no-wrap), edge dedup + dangling filter.
  - **`ImpactGraph.tsx`** — SVG render + interaction. **a11y crux reconciled** (design `role="application"` → **roving-tabindex**
    over real `<button>` nodes, `aria-current`/`aria-label`, linear arrow-nav in column/row order, `aria-live` announcements,
    `aria-hidden` edge layer). **RTL**: `scaleX(-1)` on the edges-only SVG + logical `inset-inline-start` nodes (unit-tested).
    Enter/click **re-centres** on a focusable node (warm nav with the node's own GUID) or opens its detail page.
  - **`ImpactGraphList.tsx`** — genuine list-tree fallback (role=tree/treeitem/aria-level, focus-path crumb, indented rows).
  - `graph.css` — logical properties throughout; `acmpDash` edge animation gated behind `@media (prefers-reduced-motion)`.

**Reconciliations flagged (guardrail #14, design→behavior).** role→roving-tabindex; **per-node lifecycle status chip omitted**
everywhere (backend returns no far-node status — cross-module read, ADR-0001) → graph nodes show a **type chip** instead
(a11y win: not colour-alone), aside/list drop it; cross-stream badge shows the **stream code** (avoids a Membership seam);
Blocked pill from `node.blocked`/`edge.isBlocker`; type-aware breadcrumb root; partial-notice + empty-graph = no-reference
compositions. Cross-stream toggle now **works** (per-edge `isCrossStream` on the wire) but stays **Topic-scope partial** (flagged).

**Decisions folded (architect + advisor; not re-litigated).** aside = reuse panel reads + `buildTypeGroups`; SVG coverage =
pure layout at 100% + component state tests (no coverage exclusion); RTL = replicate the design's flip exactly; edge dedup by
`from→to`; arrow-nav clamps (no wrap, matching the tree fallback).

**Gates (local, all green).** `tsc -b` clean · oxlint clean (only a pre-existing Toast warning) · **684 vitest pass** (+53
new) · **per-file line coverage 100% on the 5 new FE files**, 100% on the `traceMeta`/`api` additions, global 99.13% ·
i18n EN/AR parity by hand (`trace.graph.*` + full 17-entry `trace.type.*`, every label from the `.dc.html` `t` object) ·
**axe AA clean** on graph + aside + list.

**Live VR pass (FR-096 → Met).** Booted the real stack (`npm run e2e:up`; KC bootstrap admin = `ChangeMe_KC#2026` from
`.env.example` — the persisted-volume `Acmp_KC-…` in memory did NOT apply to this fresh boot, corrected) and ran a new
`e2e/p10f-graph-vr.spec.ts`: real Keycloak PKCE login → secretary seeds a focus topic + a 3-tier subgraph via the
self-describing edge APIs (`POST /api/traceability` + `/api/dependencies`, synthetic targets except a real 2nd topic for the
Topic-scope cross-stream case) → opens the graph via the warm-path panel button. Captured **EN-light + AR-dark + list** to
`e2e/vr-out/` and screenshot-compared **pixel-faithful to the `.dc.html`**: the **RTL edge-flip meets the nodes** (top risk,
resolved), tier columns / curved edges / type chips / focus highlight / Blocked pill / group-by-type aside / list-tree all
match. The VR spec is committed (mirrors `vr-sweep.spec.ts`; coexists in CI, no count-assertion interference).

**Next.** Operator merge GO (branch → 4 CI checks green → squash-merge #83).

---

## P10f — Impact-graph backend: read-time subgraph endpoint + FR-095 Topic-scope (branch `feat/P10f-graph-backend`)

### 2026-07-03 — the `/api/traceability/graph` transitive endpoint (backend; PR1 of 2)

**Scope.** Sixth P10 slice, split into **two PRs** (architect call): **PR1 = backend** (this entry) — the server-side impact-graph
traversal + the two cross-module read seams; **PR2 = frontend** — the bespoke SVG graph page + group-by-type aside.
Pre-planned with the `ecc:architect` subagent (two rounds) + advisor. **The operator overrode both reviewers' FE-only lean**
and chose (1) a **backend subgraph endpoint** (server-side BFS) and (2) **build FR-095 cross-stream now** (not defer); the
one-page IA + register-graph-deferred stayed as blessed. A third genuinely-unsettled sub-decision — the **stream model for
non-Topic artifacts** — was put to the operator, who chose **(a) Topic-scope** (OQ-047).

**Delivered (FR-096 transitive graph).**
- **`GET /api/traceability/graph/{type}/{id}?depth=1..3`** → `{ focusType, focusId, depth, nodes[], edges[], partial }`, read-all.
  `GetImpactGraph` query + handler in the **Traceability module** (docs/domain/search-and-traceability.md home). A pure `ImpactGraphComposer` runs the BFS;
  the handler wires it to the real reads. **Read-time composition, NOT a cross-schema CTE** (ADR-0020 clarifies ADR-0019's
  "recursive CTE" phrase — a two-schema CTE would break ADR-0001). No new tables/migration.
- **Two new `Acmp.Shared` ports (ADR-0001-safe, primitive DTOs — module enums never leak into the kernel):**
  `IDependencyArtifactReader` (Dependencies.Infrastructure impl, reuses the existing `GetDependenciesForArtifact` read — one
  source of truth) and `ITopicStreamReader` (Topics.Infrastructure impl, `Topic.AffectedStreams` codes).
- **Composer guards:** visited-set keyed on the type-**NAME** string + id (`ArtifactType`/`DependencyEndpointType` overlap by
  name only — the #2 trap), `MaxNodes=60` ceiling (OQ-018) → `partial`, depth clamp 1–3, per-node try/catch → failed node is a
  leaf + `partial` (never blanks the graph). Tier = signed BFS level at first discovery (design-faithful `buildTiers`).
  Dual-enum: **System** (dep-only, ∉ ArtifactType) skips the relationship read + dead-ends; a rel-only type (Adr/…) skips the
  dependency read. `blocked` = node touches an active blocker dep edge (`IsBlocker`); far-node lifecycle status NOT returned
  (cross-module read, ADR-0001).
- **FR-095 cross-stream = Topic-scope (partial, OQ-047):** `isCrossStream` true only when both ends are Topics with disjoint
  non-empty stream-code sets; every edge touching a non-Topic is never cross-stream. Post-pass over the built graph.

**ADR-0001 / boundaries.** Traceability composes Dependencies + Topics **only through `Acmp.Shared` ports** — never their
assemblies. The seam DTOs live in `Acmp.Shared` (the #1 CI trap: reusing `Dependencies.Application.DependencyEdgeDto` would
turn the `Traceability_should_not_depend_on_other_modules` ArchUnit test RED). **All 36 architecture tests green** — no
boundary rule needed changing (the P8d `IActionLinkDirectory` precedent already sanctions "Infrastructure implements a Shared
port").

**Gates (local, pre-push — the ones P9a's CI cycles taught us not to skip).** `dotnet build` clean; **972 tests green**
(Domain 168 / Arch 36 / Application 627 / Integration 17 / Api 124; **+24 new**: 15 composer branch tests via the handler with
faked ports, 2 `DependencyArtifactReader`, 2 `TopicStreamReader`, 3 graph API integration tests incl. 401 + a real
Topic→Decision→Action depth-1-vs-2 walk, +2 more). `dotnet format --verify-no-changes` clean (fixed the UTF-8-BOM CHARSET on
every new file — the known trap). `node scripts/check-coverage.mjs .` → **global 99.75%, exit 0, 100% line coverage on all 7
new files**.

**Docs.** **ADR-0020** (read-time composition, clarifies ADR-0019; the two ports; FR-095 Topic-scope). **OQ-047** (stream-
inheritance model, default (a) Topic-scope, inherit model (b) stays OPEN on the multi/zero-topic semantic; relates OQ-018
node cap). docs/domain/search-and-traceability.md §4.1 (composition-not-CTE note) + §6.2 (endpoint contract). Memory `p10-risks-deps-traceability-plan`
updated (the wrong `AffectedStreamIds Guid[]` note corrected to `AffectedStreams` string codes).

**AC.** **FR-096 (transitive impact graph) → data stood up** (endpoint + tests); the user-facing graph is PR2, so the
end-to-end AC verdict lands with the FE. **FR-095 → Partial (Topic-scope)** — honestly not "built" (thin coverage in a mostly
cross-TYPE graph). AC-062/063 unaffected (still Partial→P17). No live-stack pass needed for a backend contract slice.

**Next.** P10f **PR2** — the bespoke in-app SVG impact-graph page (depth 1–3, blocked toggle, roving-tabindex keyboard nav,
List-tree fallback, group-by-type aside) consuming this endpoint + `useTraceGraph`; register/panel "Open graph" wiring; full
EN/AR + RTL + light/dark + axe + visual-verify vs the `.dc.html`. Then P10g dashboards.

---

## P10e — Dependencies register + Traceability panels UI (branch `feat/P10e-deps-traceability-ui`)

### 2026-07-03 — the `/dependencies` register + the shared traceability panel + create dialogs (frontend only)

**Scope.** Fifth P10 slice — **FRONTEND only** (`src/Acmp.Web`), consuming the merged `/api/dependencies` +
`/api/traceability` contracts (no backend change). Mirrors the P10b (Risks UI) / P8b (Actions UI) template.
Pre-code reviewed by the `ecc:architect` subagent + advisor (both folded); two scope calls put to the operator
and answered: **panel = detail-page aside only** (the standalone group-by-type Relationships page + the SVG graph
ship together in P10f), and **the register's "New dependency" ships limited + flagged** (Topic/Action pickable).

**Delivered.**
- **`/dependencies` register** (`DependenciesRegister`) composed to the Lists&Registers deps table: columns
  **From · Relation · To · Blocked · Status** (rows link to the edge detail), global "N links" + "N blocked"
  header counts (filter-independent, via `useDependenciesCounts` count fan-out), a Relation filter + a
  Blocked-work toggle, key/status server sorts, loading/empty/error states, "New dependency" (Chair/Sec) + a
  disabled "Open graph" stub (→ P10f).
- **`/dependencies/:key`** (`DependencyPage`) — the **edge** detail (From/Relation/To/Blocked/Status + Notes +
  navigable endpoints); NOT an artifact, so it carries no traceability panel (architect ruling). Replaces the
  `/dependencies` PlaceholderPage; breadcrumb leaf added for the DPN key.
- **`TraceabilityPanel`** — the shared detail-page aside (AC-062) mounted on **Topic / Decision / Action / Risk**
  detail. It MERGES typed Relationship edges (`/api/traceability`) + governed Dependency edges
  (`/api/dependencies/artifact`) at read time into **Upstream / Downstream / Related** groups, each row = relation
  label + far key + title + a **navigable link** (routeless types render as plain text — no dead links). Dependency
  data is only fetched for the 4 `DependencyEndpointType` artifacts (Topic/Action/System/Decision); a **Risk** detail
  shows relationship edges only (no `DependencyEndpointType.Risk`).
- **Create dialogs (AC-063).** `CreateDependencyDialog` (composed to the design `dependency` form) + the
  no-reference `CreateRelationshipDialog`, both Chair/Sec-gated, launched contextually from the panel (From/Source
  pre-seeded, works for any type) or from the register (blank-both-ends, Topic/Action only). One shared
  `ArtifactPicker` (type→artifact over the 3 registers with FE list hooks: Topic/Action/Risk).

**Design↔behaviour reconciliations (flagged, ASM-016 / guardrail #14):**
- **Cross-stream column + filter OMITTED** (not rendered as an all-"—" column — fidelity theater is less honest than
  omission): `IsCrossStream` is not on the wire and its cross-module derivation (FR-095) is deferred to a later slice.
- **No far-artifact lifecycle status chip** in the panel (a cross-module read, ADR-0001); dependency edges instead
  carry a self-describing **"Blocked" pill** (`IsBlocker`, ADR-0019).
- **Direction axis** (Upstream/Downstream/Related) is a curated FE map (`traceMeta`): the design specifies direction
  only for the 4 dep kinds (`relMeta`) + 7 far-types (`groupDefs`), not all 16 RelationshipTypes — the rest is a
  flagged heuristic; the relation label + link carry the real semantics.
- **`CreateRelationshipDialog` is a no-reference composition** (no generic traceability-edge form in the design).
- **Only 3 of 16 artifact types are pickable** in create (Topic/Action/Risk — the only FE list sources; Decision &
  System have no register hook). Contextual create sidesteps this for the From/Source end.
- **"Open graph"** disabled stub → P10f; **`/dependencies/:key`** routed (not a drawer) for deep-linking.

**Gates (local, pre-push).** `tsc -b` clean; `oxlint` clean (only the pre-existing Toast fast-refresh warning);
`vite build` clean. **Vitest 638 green (+56 new)**, axe-clean structure/ARIA on every new screen. i18n **key parity OK
(1090 keys)** — the `deps.*` + `trace.*` namespaces added to BOTH locales by hand, every enum value covered (Kind 4,
Status 3, RelationshipType 16, direction 3, pickable types 3). **Per-file coverage ≥95% on every new file** (100%
lines on the api hooks + meta + panel + dialogs; register/page ≥97%), global threshold green. **VISUAL-VERIFY DONE** —
drove the **live stack** (real Keycloak auth-code+PKCE + real SQL: dependencies seeded via `POST /api/dependencies`,
which self-describe per ADR-0019) and screenshot-compared the three reference-backed surfaces (register, edge detail,
create dialog) vs the `.dc.html` in **EN-light + AR-RTL-dark**: From·Relation·To·Blocked·Status columns, "N links / N
blocked" counts, coloured relation arrows, Active chips, New-dependency + disabled Open-graph, and the `dependency`
create form all match; RTL fully mirrored + dark tokens clean; Cross-stream correctly absent. (The `TraceabilityPanel`
+ `CreateRelationshipDialog` are no-reference compositions — not screenshot-gated.)

**AC.** **AC-062 (traceability panel) + AC-063 (create typed edge) → Partial** (the FE panel/dialogs are structurally
proven by tests, but the live real-stack Keycloak-PKCE + SQL round-trip → **P17** per G-TRACE, matching every prior FE
slice). **FR-098** (topic-detail inbound/outbound deps) is served by the panel on Topic detail. **FR-095** (cross-stream)
stays deferred (column omitted honestly). FR-096/097 (impact graph) = P10f.

**Next.** P10f — the bespoke SVG impact graph (must UNION the Dependencies + Relationship edges) + the standalone
group-by-type Relationships page; then P10g dashboards.

---

## P10d — Dependencies backend (branch `feat/P10d-dependencies-backend`)

### 2026-07-03 — the `Dependencies` module: governed DPN edge + register/panel API (backend only)

**Scope.** Fourth P10 slice — **BACKEND only**. A new **`Dependencies` module** (docs/README core bounded context)
holding the first-class governed `Dependency` edge (`DPN-YYYY-###`), its create/resolve/remove lifecycle, and
the read APIs a later UI (P10e) consumes: register, by-key detail, and a by-artifact panel query. No `Acmp.Web`.
Pre-code reviewed by the `ecc:architect` subagent + advisor (both GO-WITH-FIXES); all fixes folded.

**Module.** `src/Modules/Dependencies/{Domain,Application,Infrastructure}`, schema `dependencies`, migration
`Dependencies_Init` (`dependencies` + `dependency_key_counters` tables), cloned from the Risks scaffold (key-gen
+ RowVersion) + Traceability (leaf wiring). `Dependency : AuditableEntity` = a self-describing directed edge
`(FromType,FromId,FromKey,FromTitle)` —`Kind`→ `(ToType,ToId,ToKey,ToTitle)`, `Note?`, `Status` + `RowVersion`
(ADR-0018, mutable root). Endpoint snapshots on both ends (no cross-module FK, ADR-0001/0019). Three enums,
all module-local (ArtifactType is Traceability-Domain-local): `DependencyEndpointType {Topic,Action,System,
Decision}`, `DependencyKind {DependsOn,BlockedBy,Blocks,RelatesTo}`, `DependencyStatus {Open,Resolved,Removed}`.
Status carries the soft-delete — `Removed` is the retract state (no separate `IsActive`; rows never hard-deleted).

**Features.** `CreateDependency` (RBAC `Policies.DependencyCreate`; DPN key-gen via per-module counter; validator =
enums + **self-loop guard** From≠To + snapshots non-empty + Note≤1000; audits `Dependency.Created`).
`ResolveDependency`/`RemoveDependency` (by PublicId; domain guard Open-only → 409; 404 unknown; audit
`Dependency.Resolved`/`Dependency.Removed`). `GetDependencyByKey` (detail). `GetDependenciesRegister` (paged;
filters Kind/Status/BlockedOnly; sorts key/status; **Removed excluded by default** unless the Status filter asks;
all predicates DB-translatable). `GetDependenciesForArtifact` (the by-artifact panel — `{outbound,inbound}` split
on `(FromType,FromId)`/`(ToType,ToId)`, Removed excluded) — **this is the read-time-composition seam** that lets
P10e's traceability panel show dependencies without a mirror edge. `IsBlocker = Kind∈{BlockedBy,Blocks} &&
Status==Open` is computed in the DTO mapping (register "Blocked" column + Blocked-work filter), never stored.

**OQ-046 — the mirror-edge decision (operator GO).** docs/domain/search-and-traceability.md §1.34/§1.1 said the Dependencies module keeps a
`depends-on` Relationship edge in sync (write-through). **Resolved to read-time composition** (store the DPN
ONCE; the panel/graph query BOTH modules and merge) — per-module DbContexts share no transaction, so a mirror
risks dual-write drift for no benefit. **Unification** of the two aggregates into one edge table was explicitly
considered (operator asked "why two tables?") and **rejected** by both reviewers: sparse/nullable columns for the
~12 non-governed RelTypes, it would fold a core bounded context into the cross-cutting Search&Traceability module
(or make Decisions depend on Dependencies for AC-029), and different lifecycle ⇒ different aggregate (docs/domain/domain-model.md §A.3
"keep both, distinct roles" is settled). Recorded via **OQ-046** (docs/decisions/open-decision-register.md), **ASM-016** (docs/risks/risk-register.md), and inline edits
to **docs/domain/search-and-traceability.md §1.1** (fixed the stale "maintained in sync" sentence — the likely source of the "are we duplicating?"
instinct) + **docs/domain/domain-model.md §Dependency**. **Consequence flagged:** P10f's recursive-CTE impact traversal must UNION the
Dependencies table (it won't see dep edges via `Relationship`).

**Design↔behaviour reconciliations (flagged, ASM-016):**
- **Kind = design's 4 chips** `{DependsOn,BlockedBy,Blocks,RelatesTo}` — docs/domain/domain-model.md's `Impacts` dropped (unused), `Blocks`
  added (FR-094 + design). docs/domain/domain-model.md edited.
- **`IsCrossStream` (FR-095) NOT modelled** — it's `(derived)`; computed read-time in P10e from real stream *sets*
  (`Topic.AffectedStreamIds` is `Guid[]`; a scalar snapshot is lossy). FR-095 = P10e work (Topics+Membership
  stream-resolution contract). Register Cross-stream column honest-partial in P10d.
- **`Note` = plain `string?`** not docs/domain/domain-model.md's `LocalizedString?` (single free-text Notes box; user annotation ≠ system
  label; mirrors `Relationship.Notes`).
- **Status `{Open,Resolved,Removed}`** vs design register `{Active,Resolved}` ("Active"=Open label; "Removed"=soft-
  delete-absent).
- **Endpoint types not validated against a per-Kind matrix** (trusted actor, ASM-015 pt6 posture).
- **Member/Reviewer `Dependency.Create` AiO dormant** — the endpoint is resourceless so `CapabilityHandler` grants
  only Chairman/Secretary; deferred because **no FE caller yet** (a topic-sourced create satisfies the existing
  `ITopicCapabilityResolver` once P10e supplies `ITopicScopedResource`; only action→action is resolver-blocked) —
  NOT because auth can't express it. Command `AllowedRoles={Chairman,Secretary}` matches the effective gate.
- **Resolve/Remove reuse `Policies.DependencyCreate`** (no `Dependency.Manage` — same trusted actors, YAGNI).

**Authz.** `Policies.DependencyCreate` + its `AuthorizationRegistration` matrix cell + `PermissionMatrixTests` cell +
docs/domain/permission-role-matrix.md row 17 **already existed from P4** — reused, no new policy wiring.

**Wiring.** `Program.cs` (AddDependenciesModule + MediatR assembly + MapDependencyEndpoints), `MigrationRunner`
contexts[], `AcmpWebApplicationFactory` InMemory swap, `ModuleBoundaryTests` (Domains/Applications arrays + a
`Dependencies_should_not_depend_on_other_modules` fact — green), `Acmp.Api.csproj` + Domain/Application test-project
references, solution (`dotnet sln add` ×3). (Api.Tests needed no ref change — `DependenciesDbContext` resolves
transitively through `Acmp.Api`, same as Risks/Traceability.)

**Gates (local, pre-push — re-verified by hand, not just the build agent).** `dotnet build -c Release` clean;
**all tests green — Domain 168 / Application 608 / Api 121 / Architecture 36 / Integration 17 = 950** (Dependencies:
10 new Domain + 22 App + 8 Api + 4 Arch rows). `node scripts/check-coverage.mjs .` **exit 0 — global 99.75%, 0 files
< 95%** (all new Dependencies files measured). `dotnet format --verify-no-changes` clean (migration CRLF/BOM fixed).
Live real-SQL boot NOT run (backend-only, InMemory factory covers wiring; the RowVersion 409 path + recursive-CTE
traversal that need real SQL are P10f/P17).

**AC.** **No AC verdicts flip** (backend-only, matching P10a/P10c discipline). Stands up the data for **FR-094**
(create DPN edge), **FR-095** (cross-stream — derivation is P10e), **FR-098** (topic-detail deps list — the
by-artifact query serves it in P10e). FR-096/097 (transitive impact + Tarseem graph) = Phase 2 → P10f. AC-062/063
stay Pending → P10e. Live real-stack legs → P17 per G-TRACE.

**Next.** P10e — Deps register + Traceability panels UI (`/dependencies` register + the deps panel on
Topic/Decision/Action/Risk detail = AC-062/063; FR-095 cross-stream read-time; FR-098 topic deps list).

---

## P10c — Relationship (traceability) backend + panel API + AC-029 widening (branch `feat/P10c-relationships-backend`)

### 2026-07-03 — the Traceability module + typed edges + widened decision-issue gate (backend only)

**Scope.** Third P10 slice — **BACKEND only**. A new **`Traceability` module** (docs/domain/search-and-traceability.md's "Search&Traceability"
home; ADR-0008) holding the generic typed `Relationship` edge, its create/deactivate/panel API, and the
cross-module seam that **widens the AC-029 decision-issue gate** to accept ANY downstream edge (Action OR
Relationship). No `Acmp.Web`. Pre-code reviewed by the `ecc:architect` subagent + advisor (both
GO-WITH-FIXES); all fixes folded (see below).

**Module.** `src/Modules/Traceability/{Domain,Application,Infrastructure}`, schema `traceability`, cloned from
the Risks scaffold. `Relationship` aggregate = a self-describing edge `(SourceType,SourceId,SourceKey,
SourceTitle)` —`RelType`→ `(TargetType,TargetId,TargetKey,TargetTitle)`, `Notes?`, `IsActive` soft-delete +
`DeactivatedAt/By`, audit stamps from `AuditableEntity`. `ArtifactType` (16 values, docs/domain/search-and-traceability.md §1.1) +
`RelationshipType` (16 values, §2.2) enums. Migration `Traceability_Init` (`relationships` table + 3 filtered
indexes on active edges: source, target, reltype). **No physical Artifact registry** — reconciled against
ADR-0008 via **ADR-0019** (amends 0008): edges self-describe, matching docs/domain/search-and-traceability.md §2.1's FK-less SQL (ASM-015).

**Features.** `CreateRelationship` (AC-063; RBAC `Traceability.Link` = Chairman/Secretary; validator incl.
**source≠target** self-loop guard + domain defence-in-depth; audits `Relationship.Created`). `Deactivate­Relationship`
(soft-delete only, never hard-delete per docs/domain/search-and-traceability.md §5 / ADR-0009; audits `Relationship.Deactivated`; 404 unknown;
idempotent on already-inactive). `GetArtifactRelationships` (AC-062; keyed by `(ArtifactType, PublicId)` →
`{outgoing[], incoming[]}`, one hop, active edges only; the far endpoint resolved per direction). The panel row
returns `relType`+`direction` enum names (no English `inverseLabel` on the wire — the SPA localizes forward +
inverse labels, guardrail #9).

**AC-029 widening.** New Shared seam `ITraceabilityLinks.DecisionHasDownstreamEdgeAsync(decisionId)` (impl in
Traceability.Infrastructure, mirrors `IActionLinkDirectory`). `IssueDecisionHandler` predicate is now
`RequiresDownstreamLink(outcome) && !(actionLink OR relationshipEdge)` — two Shared contracts OR'd (each module
owns its store; ADR-0001 forbids unifying them). **Downstream is a CURATED RelType set, NOT "any edge"**
(advisor's sharpen; both reviewers' original leans leaked): the decision is source of `recorded-as`/`resolves`
or target of `implements`; upstream/lineage (`decided-by`/`derived-from`/`supersedes`) are excluded so a
decision can never be self-satisfied by its own topic (ASM-P10c-2). The gate query is separate from the panel's
source-OR-target query. Superset of the P8d Action-only gate → **AC-029 stays Met, no regression**.

**Design↔behaviour reconciliations (flagged):**
- **No Artifact registry** (ADR-0008 prescribed one; docs/domain/search-and-traceability.md §2.1 SQL has none) → **ADR-0019** amends 0008.
- **Curated AC-029 downstream set** vs a naive "any edge" — the AC text *"Action, Risk, or other artifact"*
  maps to `implements`(Action)/`resolves`(Risk)/`recorded-as`(ADR).
- **Title snapshotted on the edge** (AC-062 requires the panel to display title; key-only would force a
  read-time resolution seam into P10e) — accepted staleness, `// ponytail:`-style note (ASM-015).
- **`Traceability.Link` authz = Chairman/Secretary only** — docs/domain/search-and-traceability.md §6.1's topic-Owner AiO create is deferred
  (no ABAC resource handler for arbitrary artifact types; no UI yet). New docs/domain/permission-role-matrix.md row 32 + matrix note.
- **Panel shows explicit edges only** — projecting existing soft-refs (Decision→Topic, Action→source,
  Risk→subject) is a later slice, and should be **read-time composition, not write-through** (ASM-015).
- **Edge endpoint types not validated against docs/domain/search-and-traceability.md §2.2** — the create validator checks enum + self-loop
  only, not the per-RelType type matrix (trusted-actor input). Consequence: a malformed edge could satisfy the
  AC-029 gate; the full 16-type matrix is deferred (ASM-015 point 6, advisor's final catch).

**Wiring.** `Program.cs` (AddTraceabilityModule + MediatR assembly + MapTraceabilityEndpoints), `MigrationRunner`,
`AcmpWebApplicationFactory` InMemory swap, `ModuleBoundaryTests` (arrays + a `Traceability_should_not_depend_on_
other_modules` fact — green), `PermissionMatrixTests` cell `Traceability.Link = AADDDDDD`, `Policies` +
`AuthorizationRegistration.Matrix`. Solution + test-project references added.

**Gates (local, pre-push).** `dotnet build` clean; **all tests green** — 158 Domain / 32 Arch / 586 App / 17
Integration / 113 Api (Traceability: 20 new App unit tests incl. the curated-predicate theory + 6 new Api
contract tests; DecisionsApiTests +1 for the edge-satisfies-AC-029 path; the 8 IssueDecision ctor sites threaded
the new `ITraceabilityLinks` substitute). `dotnet format --verify-no-changes` clean (fixed the generated
migration's CRLF + object-initializer whitespace). **Per-file coverage 99.73% global, 0 files < 95%** — all 13
new Traceability files measured. Live real-SQL boot NOT run (backend-only, InMemory factory covers wiring;
the recursive-CTE traversal that would need real SQL is P10f).

**AC.** **AC-029 stays Met** (widened superset; new test proves the edge-only path issues). **AC-062/063 stay
Pending → P10e** — this slice makes them API-testable + audited, but both assert on the FE *panel display*, which
lands with the traceability panels UI. Live real-stack legs → P17 per G-TRACE.

**Next.** P10d — Dependencies backend (`Dependency` governed edge DPN; mirrors a `depends-on` Relationship per
docs/domain/search-and-traceability.md §1.34; blocked-work + cross-stream).

---

## P10b — Risks register + detail UI (branch `feat/P10b-risks-ui`)

### 2026-07-03 — the /risks register + routed detail + create dialog (frontend only)

**Scope.** Second P10 slice — **FRONTEND only** (`src/Acmp.Web`), composed to `ACMP Lists & Registers.dc.html`
(the `risks` `gRsk` 8-col table + risk drill-in) and the `risk` create form in
`ACMP Create Flows & Dialogs.dc.html`, consuming the **already-merged** `/api/risks` contract (P10a, #77) —
**no backend change**. Read + create only; the W15 lifecycle transitions (begin-mitigation/close/escalate/
accept + mitigation edits) are a later slice, mirroring the P8b → P8b2 split. Pre-code reviewed by the
`ecc:architect` subagent + advisor (both GO); their fixes folded in (see below).

**Files.** New feature folder `features/risks/`: `RisksRegister.tsx` (register), `RiskPage.tsx` (routed
`/risks/:key` detail), `CreateRiskDialog.tsx` (create), `riskMeta.ts` (pure tone/heat mappers), `risks.css`
(ported register/detail classes + the new heat/matrix primitives). New `api/risks.ts` (server-state hooks).
Wiring: `App.tsx` (`/risks` → RisksRegister, add `/risks/:key` → RiskPage, replacing the placeholder);
`breadcrumbs.ts` (risks leaf crumb). i18n: a full `risks.*` block in **both** `en.json` + `ar.json` (every
enum value by hand — 5 statuses, 4 exposures, 3 levels). Clone template = the P8b `features/actions/*`.

**Register.** 8-col `gRsk` table (ID · Risk · Prob. · Impact · Exposure · Owner · Status · Linked): mono
accent key + title `<Link>`; Prob./Impact as colour-coded level words; **Exposure = a 3×3 7px heat mini-grid
+ a StatusChip** (the on-cell at `(x=lvlIdx[prob], yTop=2−lvlIdx[impact])` painted with the projected band's
colour); owner avatar; status chip; linked subject key or —. Header "Risk register" / "N risks" + a red-dot
"N critical" badge, both **filter-independent** (two `pageSize=1` count queries). Status + Exposure multi-
filters; Owner filter is a disabled stub (needs a Keycloak-keyed directory). **Only key/exposure/status are
server-sortable** (`GetRisksRegister.Sort`) — the other columns are not sortable. Empty/loading(8-col
skeleton)/error states. "New risk" opens the create dialog.

**Detail.** Routed `/risks/:key` (blessed deviation vs the design drawer, so RiskAssigned/RiskEscalated can
deep-link) — header chips (key + status), a 6-fact grid (Prob/Impact/Exposure/Owner/Status/Linked), the
**LARGE 3×3 exposure matrix + Probability/Impact/Exposure legend** (`role=img` with an accessible cell
description), and a **prose "Mitigation plan" card** rendering the mitigation description(s). The
Related/Traceability panel is **honest-partial** — the linked subject key display-only + a "traceability
coming" note; typed edges + the impact graph land in P10c/e.

**Create.** `Title · Likelihood · Impact · Owner · Linked topic · Mitigation plan` → `POST /api/risks`.
Content mirrored (`en === ar`, the FTS pattern). On success routes to the new `/risks/:key`.

**Design↔behaviour reconciliations (design to be updated to match, guardrail #14):**
- **Linked topic REQUIRED** (design marks it optional): `RaiseRisk` requires a non-empty `SubjectId` Guid and
  there is no "no-subject" seam, so the picker is required and maps to `SubjectType=Topic` (**Option A**,
  architect-ruled; System=2 can't satisfy the validator without a backend change).
- **No immutability warning** on the create confirm — risks are NOT immutable; neutral "Create risk" copy.
- **Likelihood/Impact default to Medium/High** (the design pre-fill) so the 1-based `RiskLevel` enum is always
  valid on submit (advisor).
- **Accepted/Escalated status tones are no-reference** (the design `riskStatus` lists only Open/Mitigating/
  Closed): Escalated → danger, Accepted → info.
- **"Mitigation plan" is a single prose card, not a mitigations table** — the design has no such table; a
  structured mitigation list is a later, no-reference extension (advisor).
- Saved-view "Open risks by exposure" chrome dropped (the default exposure-desc sort realises it), matching
  the actions-register precedent.

**Architect/advisor fixes folded:** topic picker reads one large page (`pageSize=200`) not a silent page-1;
sortable columns pinned to the backend's `key/exposure/status`; tests assert **repeated** `status=`/`exposure=`
query params (array binding); `risks.css` ports only the classes risks use + the new heat/matrix primitives
(no blind `act-→rsk-` rename); `levelColor` drops Critical (an Exposure band, never a probability/impact);
8-col skeleton (not 7); Related panel shows the subject key rather than a blank box.

**Gates (local, pre-push).** `tsc --noEmit` clean; `oxlint` clean (only the pre-existing Toast warning);
**582 vitest green** (37 new across api/riskMeta/register/detail/dialog, incl. axe WCAG 2.2 AA structure/ARIA
= 0 violations); i18n parity OK (991 keys); **per-file line coverage 100%** on all 5 new files
(`coverage-summary.json`). Live screenshot-compare against the `.dc.html` (register heat grid + detail matrix,
RTL + dark) is the one remaining manual check — pending a running-stack pass (needs seeded risk data +
backend/Keycloak/SQL); flagged, not claimed done.

**AC.** No verdicts flip — P10b is FE composition over the merged contract; the live real-stack leg → **P17**
per G-TRACE. Related/Traceability panel is honest-partial → **AC-062/063 stay Pending** (→ P10c/e).

**Next.** P10c — Relationship (traceability) backend + panel API + AC-029 widening (a generic typed edge;
widen the decision-issue gate so ANY downstream edge, Action OR Relationship, satisfies AC-029).

---

## P10a — Risks backend (branch `feat/P10a-risks-backend`)

### 2026-07-03 — the Risk aggregate + Mitigations + W15 lifecycle (backend only)

**Scope.** First slice of P10. **P10 is 2–3 phases compressed** (the design's own Usage Map splits it as P8
Actions+**Risks** and P11 Deps/Traceability) — sliced GO-gated: **P10a Risks BE** → P10b Risks UI → P10c
Relationship/traceability BE + AC-029 widening → P10d Dependencies BE → P10e Deps register + traceability
panels UI → **P10f full BFS SVG impact graph** (operator GO to build it now; pulls BL-122/123 forward from
Phase 2) → **P10g risk/dep dashboards** (operator GO). This slice = the **Risks** module backend only (no
`Acmp.Web`). Pre-code reviewed by the architect + advisor (both GO); 6 architect fixes folded in.

**Module home.** New `Risks` module (`src/Modules/Risks/{Domain,Application,Infrastructure}`) mirroring the
Actions pattern verbatim. `Risk` aggregate + owned `Mitigation` child (`risk_mitigations`, the exact
`DecisionCondition` shape). Schema `risks`; migration `Risks_Init` (`risks` + `risk_mitigations` +
`risk_key_counters`).

**Domain (W15, docs/domain/entity-lifecycles.md §10).** Single `RiskStatus{Open,Mitigating,Closed,Accepted,Escalated}` enum (no
orthogonal side column — architect B-1). Transitions: raise→Open; Open→Mitigating (needs ≥1 mitigation);
Mitigating→Closed (all mitigations Done **or** a closure note); Open/Mitigating→Accepted (rationale +
authority, terminal); Open/Mitigating→Escalated (reason + target). **Escalated is transient** — it returns via
BeginMitigation (Escalated→Mitigating) or Close (Escalated→Closed) (docs/domain/entity-lifecycles.md §10:220). `Closed`+`Accepted`
terminal. Acceptance/escalation/closure **evidence is stored on the aggregate** (not audit-only) so P10b can
render "why/to whom" (advisor #3). `RowVersion` (409 on stale write). Owned `Mitigation` = forward-only
`Planned→InProgress→Done`.

**Derived exposure (single-sourced, advisor #2).** `RiskExposureScale`: `Severity = Likelihood×Impact` (1–9,
`RiskLevel{Low=1,Medium=2,High=3}`) → `Exposure` band `≤2 Low · ≤4 Medium · ≤6 High · 9 Critical` (matches the
design heat-grid seed). **Never persisted** (docs/domain/entity-lifecycles.md:247) — projected into the DTOs; P10b's grid consumes the
band, never re-derives it.

**Authorization.** `Risk.Manage` (Chairman/Secretary; Member/Reviewer AiO) for raise/mitigate/begin/close/
escalate. **Accept is narrower** → new dedicated **`Risk.Accept`** policy (Chairman/Secretary, no AiO — architect
M-2) added to `Policies` + `AuthorizationRegistration.Matrix` + the independently-encoded `PermissionMatrixTests`
cell (`AADDDDDD`). Escalate rides `Risk.Manage` per §10 (architect M-1). MediatR `AuthorizationBehavior`
backstops every command.

**Notifications + audit.** Raise notifies the owner (skip self, docs/domain/workflows.md:201). **Escalate fans out to Secretary +
Chairman** via `ICommitteeDirectory.GetActiveMembersInRoleAsync` (BL-135), skipping the actor + de-duped. Every
state change **and every mitigation mutation** emits an `AuditEvent` via `IAuditSink`
(`Risks.RiskRaised/RiskMitigating/RiskClosed/RiskAccepted/RiskEscalated/MitigationAdded/MitigationStatusChanged`;
accept/escalate high-importance). No cross-module table read (ADR-0001) — subject is a soft `(SubjectType,
SubjectId)` + `SubjectKey` snapshot; a mitigation's `LinkedActionId` is a bare Guid, no FK.

**API.** `/api/risks` register (status/owner/exposure filters, exposure-desc default sort, paging) + `/{key}`
detail + POST transitions (`/mitigations`, `/mitigations/{id}/status`, `/begin-mitigation`, `/close`,
`/escalate` = Risk.Manage; `/accept` = Risk.Accept). Wired into `Program.cs`, `MigrationRunner`,
`SqlBackstopFixture`, `ModuleBoundaryTests` (+ `Risks_should_not_depend_on_other_modules` fact), and the Api
test factory's InMemory swaps.

**Design↔behavior reconciliations (design to be updated to match, guardrail #14 — no UI in this slice):**
- **Description optional.** docs/domain/domain-model.md types `Risk.Description` required, but the design create form collects only
  Title/Likelihood/Impact/Owner/Linked-topic/**Mitigation plan** (no Description) → backend accepts it optional.
- **Mitigation plan → first mitigation.** The create form's "Mitigation plan" textarea seeds an initial
  `Mitigation` (Type=Reduce default — the form collects no type). Flagged.
- **"Severity" vs "Exposure".** docs/domain/reporting-dashboards.md/28 call the band "Severity=Critical"; the design column is "Exposure".
  We expose both `Severity:int` (1–9 score) and the `Exposure` band; the dashboard `severity=` filter maps to
  the band. Minor doc reconciliation.
- **Dangling subject.** No cross-module existence check on `SubjectId` (ADR-0001) — the UI picks from real lists
  + the snapshot key travels (architect Q4, lean-lazy).

**Gates (local, pre-push).** Build clean; **849 tests green** (Domain 158, Application 557 incl. new Risk suites +
the Risk.Accept matrix cell, Architecture 28 incl. the new boundary fact, Api 106 incl. 7 Risks contract tests);
per-file coverage **99.72% global, no file <95%** (`check-coverage.mjs`); `dotnet format --verify-no-changes`
clean (fixed the EF-generated migration's CRLF). Integration (Testcontainers) wired but not run locally (Docker)
→ CI.

**AC.** No verdicts flip — P10a is backend; W15 risk lifecycle + escalation are domain/handler/HTTP-pipeline
proven, but the live real-stack (Keycloak-PKCE + SQL) leg → **P17** per G-TRACE. Feeds AC-066 (chairman
escalated-risks, its dashboard = P10g) + AC-053 (risk-escalation notification deep-link).

**Next.** P10b — Risks register + detail UI (`ACMP Lists & Registers` `risks` 8-col table + 3×3 exposure heat +
routed `/risks/:key` detail + create-risk dialog), consuming the shipped `/api/risks` contract.

---

## P9-review — Remediation slice (branch `feat/p9-review-remediation`)

### 2026-07-02 — F-1…F-28 from the adversarial P1–P9 audit (BL-066 + fidelity)

**Scope.** Burn-down of every finding from the pre-advance P1–P9 audit — one governance backend feature
plus ~27 frontend fidelity fixes. No advance to P10 until this lands green.

**F-1 (MAJOR, governance) — BL-066 durable hash-chained audit store.** The `IAuditSink` seam now binds to
`SqlAuditSink` (schema `audit`), replacing the interim Serilog-only sink: every state change is appended to
an immutable, append-only, SHA-256 **hash-chained** `AuditEvent` row (each row's `Hash` covers the prior
row's `Hash`; a UNIQUE index on `PreviousHash` makes the chain non-forking) and still mirrors to Seq.
New: `AuditEvent`, `AuditDbContext`, `SqlAuditSink`, `AuditChainVerifier`, `AuditDbContextFactory`,
migration `Audit_Init`; wired into `MigrationRunner` + both Api-test factories (InMemory swap). Fail-closed.
`Acmp.Shared` gains the SqlServer+Design EF packages (it already houses `ModuleDbContext`). Seam comments in
`IAuditSink`/`Vote` reconciled: **state-change hash-chaining is now shipped**; per-ballot crypto chaining
stays an explicit P14 refinement (not a silent ride). 5 new tests (chain continuity, broken-link + content
tamper detection, genesis, empty). ArchUnit still green (Shared is not a module assembly).

**F-2 (MAJOR, a11y).** MoM supersede dialog rebuilt on the shared focus-trapped `Dialog` (was a hand-rolled
`div[role=dialog]` with no trap/Esc/restore). **F-3 (MAJOR, a11y).** Removed the duplicate `<h1>` on the
agenda/workspace tabs (shell owns the single page H1). **F-4/F-5 (MAJOR).** SideNav gains the "Viewing as
{role}" indicator + audit-logging footer card; the permission-denied state gains Back-home / Request-access
actions + the `403 · /path` line. **F-6 (built, not deferred).** Real bilingual `Decision.Statement` field end-to-end — domain (required, `Draft`
guard), EF owned `statement_en/ar` + migration `Decisions_AddStatement`, Record/Supersede commands +
validators + `DecisionDetailDto` + mapping + API bodies, FE `DecisionDetail`/`SupersedeInput` types, the
"Decision statement" section on `DecisionPage` before Rationale, a statement input in the supersede dialog,
and EN/AR `decisions.statement` + `decisions.field.statement*` keys. The reconciliation-comment defer is
removed.
**F-7.** Stale agenda tokens (`--agenda-budget-h/-rail-w`) reconciled to the rendered 10px/360px and the
screens now consume them. **F-8.** (docs) acceptance-audit rollup regenerated below.

**F-9…F-28 (MINOR fidelity).** Sidebar padding, breadcrumb 12.5/7, badge 10px, brand 32px/.4px l-spacing,
lang-btn 13/12, per-glyph icon stroke prop, state-icon 52/13 + title 17/700, error retry primary+refresh+
request-id, notif panel z-index above sticky chrome + `aria-pressed` toggles (was a tab-list without
panels), agenda drop-zone/heading tones, attendance/quorum indicator, Pause icon, minutes doc max-width +
token backdrop, list chip `sm` + list avatar 19/9, kanban count 9px radius + hint copy, attachment cap
**50→25 MB** (constant + both locales), "Secretary of the Committee" copy, decision Cast/Confirm ballot icon
+ arrow-right open-vote + warn-toned recuse + quorum-threshold pip ring, supersede warn tone, Actions saved-
view stub removed + Cancelled→distinct terminal tone, decision issued-label EN/AR alignment.

**Gates.** Backend 814 (Application 536, +5 audit) + 24 ArchUnit green; per-file coverage ≥95% (fresh
`--collect` run); FE 545 green, per-file lines 100% on changed files; i18n parity 915; `dotnet format` clean.
**Next.** Advance to P10 once merged.

---

## P9b — Voting UI (branch `feat/P9b-voting-ui`)

### 2026-07-02 — the `/votes/:key` screen + "Call vote" configure dialog (W11; AC-021…026 + AC-052)

**Scope.** Frontend-only (`src/Acmp.Web`) — **no backend change**; wires the shipped P9a `/api/votes` contract
into the UI. Composed to the `isVoting` screen in `ACMP Decision, Voting & ADR.dc.html` + the `vote` form in
`ACMP Create Flows & Dialogs.dc.html` (guardrail #14). One slice (operator Fork-0 GO).

**What.**
- **`api/votes.ts`** — `useVote(key)` (read by key) + the W11 transition hooks (`useConfigureVote`,
  `useOpenVote`, `useCastBallot`, `useChangeBallot`, `useRecuseVote`, `useCloseVote`; mutate by Guid id,
  invalidate the vote detail). Mirrors `api/decisions.ts`. 100% covered vs a stubbed fetch.
- **`features/voting/voteState.ts`** — pure derivation of the six design states from live facts
  (`vote.status` + the signed-in user's ballot row + cast flag). 100% covered.
- **`features/voting/VotePage.tsx` + `voting.css`** — the screen: header (VOTE key + status + attributed
  badge), eligible-voters list, live tally + quorum pips, your-ballot radiogroup + comment + confirm dialog,
  recuse, close (manager), the not_open/ineligible/recused/closed states. Route `/votes/:key` (App.tsx);
  breadcrumb segment added to the shell (`nav/breadcrumbs.ts`, folds to the Decisions area).
- **`features/voting/CallVoteDialog.tsx`** — the configure flow, launched from the meeting workspace's
  "Call vote" button (now wired, Chair/Secretary only = `Vote.Manage`); binds the current meeting + agenda
  item's topic (Fork 5), seeds eligible voters from the roster (`Member.isVotingEligible`), fixed
  Approve/Reject options + Abstain toggle (Fork 4), one quorum number → MinPresent = MinCast; on success
  navigates to the new `/votes/:key`.
- i18n `voting.*` namespace added to EN + AR (parity gate green, 909 keys; AR strings taken from the design).

**Design↔behavior reconciliations (design to be updated to match, guardrail #14):**
- **Fork 1** — `double_error` is NOT a hard block: the backend allows `ChangeBallot` until close, so a cast
  ballot renders editable with its recorded choice shown ("change until close").
- **Fork 2** — `quorum_failed` is NOT a resting state: a failed close is a 409 with the vote still Open,
  surfaced as an announced inline error; the design's "re-open / extend" CTA is dropped (no backend).
- **Fork 3 (deferred)** — the closed panel shows counter-of-record + result summary + a Ratified note;
  "View decision record" (no reverse vote→decision link in the DTO) and "Record override" (no
  issue-from-vote UI) are omitted, not faked.
- **R-A** — quorum pips track cast-count → MinCast (the DTO carries no live present count; present-quorum is
  server-only at Open).
- **Not modeled** — motion text, called-by, and per-voter roles are not on the Vote aggregate; the honest
  header/voter rows show VOTE key + status + attributed, and name + choice + comment only. Flagged for a
  possible future backend field (motion/question).
- **Configure vs open** — the dialog CONFIGURES (Configured state) and lands on the not_open screen where the
  chair clicks "Open voting" (the design's own not_open leg); the design's "Open vote" primary is relabelled
  "Configure vote". The design's "Closes" date has no backend and is omitted (votes close manually).

**Verification.** FE `tsc --noEmit` clean; `oxlint` clean; i18n parity OK. `vitest run --coverage` green —
**544 tests pass** (new: `votes.test.ts` 6, `voteState.test.ts` 8, `VotePage.test.tsx` 11,
`CallVoteDialog.test.tsx` 4; `MeetingWorkspace.test.tsx` updated for the wired Call-vote button). Per-file
**≥95% lines gate green** (perFile, ADR-0016) — votes.ts/voteState.ts 100%, VotePage.tsx 98.3%,
breadcrumbs.ts 100%. Backend untouched (FE-only) → BE gates unaffected from the green `main` baseline.

**AC.** AC-021/022/023/024/025/026 stay **Partial** but gain the live `/votes/:key` UI leg (the final **Met**
flip still waits on the real-stack E2E → P17 per G-TRACE). AC-052 gains the live notification-center
deep-link target (`/votes/:key` now renders).

**Next.** P10 — Risks + Dependencies + Traceability (widens AC-029 to typed edges); the deferred
issue-from-vote decision path (Fork 3) is revisited when the record/issue-decision UI is built.

---

## P9a — Voting backend (branch `feat/P9a-voting-backend`)

### 2026-07-02 — the Vote aggregate + SoD-3 gate (W11; AC-021/022/023/024/025/026 + AC-015/016 + AC-052)

**Module home.** The **Vote** aggregate is built INSIDE the existing **Decisions** module (docs/domain/domain-model.md §Vote:
"Owning module: Decisions") — NOT a new bounded context, exactly like MinutesOfMeeting lives inside Meetings.
Backend only — no `Acmp.Web` (the `/votes/:id` UI + wiring the meeting-workspace "Call vote" stub = P9b).

**What (backend).**
- **Domain.** `Vote` aggregate (`AuditableEntity` + `RowVersion` + `PublicId`), 4-state `VoteStatus`
  (Configured→Open→Closed→Ratified, strictly forward-only). Owned `Ballot` collection (VoterUserId + name
  snapshot + Choice + optional mirrored bilingual Comment + `Recused`), always attributed (ADR-0010). Owned
  `QuorumRule` (MinPresent/MinCast) + serialized `Options` + frozen `VoteTally`. Transitions: `Configure`
  (≥2 options, MinCast≥1, seeds one awaiting ballot per eligible voter = eligibility), `Open` (present-quorum
  gate), `Cast` (first submission; second cast throws → AC-022), `ChangeBallot` (design's change-until-close),
  `Recuse` (excluded from quorum base + tally), `Close` (cast-quorum gate → AC-024, freezes tally, records the
  closer as `CounterUserId`), `Ratify` (Closed→Ratified, chair-coupled). **Immutable after Close** — no public
  mutators, re-transitions throw (AC-025/026); crypto hash-chain deferred to P14 (ADR-0009), same as Decision/MoM.
- **Live-attendance quorum (operator fork 1).** New Shared seam `IMeetingQuorumSource`
  (`Contracts/Meetings`, ADR-0001) → `GetPresentEligibleCountAsync(meetingId)`; impl `MeetingQuorumSource` in
  `Meetings.Infrastructure` counts Attendance rows with `IsVotingEligible` AND Status∈{Present,Late}.
  `OpenVoteHandler` resolves the present count from the linked meeting and lets the domain compare to MinPresent;
  **no MeetingId → present check skipped** (flagged). Cast-quorum (AC-024) stays local to the Vote.
- **SoD-3 (operator fork 2 = Option A).** `Close` records the closer as `CounterUserId` (the counter of record —
  no separate co-attester field). The gate is a retrofit onto `IssueDecisionHandler` (mirrors P8d's AC-029
  retrofit): a vote-coupled decision's chair-issuer may NOT be the vote's counter →
  `SegregationOfDuties.HasIndependentCoAttestation(chair, counter)` false → audited denial
  (`Decisions.DecisionIssueDenied`) + `ForbiddenAccessException` (403); on success the vote is ratified in the
  same transaction. `VoteId == null` skips the gate entirely (existing P7/P8 decision paths unchanged, verified).
- **Application.** Command slices (Configure/Open/Cast/Change/Recuse/Close = `Vote.Manage` for
  configure/open/close, `Vote.Cast` for cast/change/recuse) + reads (`GetVoteByKey`, `GetVotesForTopic` by
  Topic PublicId). `VoteOpened` notification fans out to eligible voters via `ICommitteeDirectory` +
  `INotificationChannel`, deep-link `/votes/{key}` (AC-021/AC-052). Audit on every transition.
- **Infrastructure/API.** `VoteConfiguration` (owned `vote_ballots` child table + unique `(VoteEntityId,
  VoterUserId)` one-ballot backstop; JSON `ValueConverter`+`ValueComparer` for Options/Tally; owned QuorumRule;
  RowVersion; unique Key). `VOTE-YYYY-###` via the multi-prefix key generator (shares `decision_key_counters`).
  Migration **`Votes_Init`** (forward-only). `VotesEndpoints` (`/api/votes`) wired in `Program.cs`.

**Review + hardening (csharp-reviewer, 1 HIGH + 1 MEDIUM acted on, pre-commit).**
- **HIGH — vote-coupling was unvalidated.** A caller could set `Decision.VoteId` to a nonexistent or
  cross-topic vote; in `IssueDecisionHandler` a null lookup **silently skipped** the SoD-3 gate + ratify while
  the decision still recorded as vote-coupled — defeating the very control P9a adds. Fixed: a claimed `VoteId`
  must now resolve, match the decision's topic, and be Closed/Ratified — enforced at **issue** (mandatory,
  before SoD-3) and at **record** (draft-time, exists + same-topic) so a dangling coupling is never persisted.
  Non-existent → 409; wrong topic → 409; not-closed → 409 (this also fixes the MEDIUM: the pre-close case used
  to throw a misleading "sole counter" 403 + false audit). +3 failing-first guard tests.
- **LOW:** documented `VoteTally`'s reference-equality footgun (EF comparer bypasses it) and corrected the
  `DecisionConfiguration`/`RecordDecision` comments (VoteId is an INTRA-module ref, app-validated, FK omitted
  by choice; AC-029 is enforced at issue as of P8d). **Flagged not fixed (LOW):** `ConfigureVote`'s eligible-
  voter list isn't cross-checked against `ICommitteeDirectory` — v1 trusts the Chairman/Secretary caller;
  P9b sources voters from the roster.

**Decisions applied / flagged.**
- **Cast vs ChangeBallot:** `Cast` = first submission only (AC-022 rejects a 2nd cast, audited denial);
  `ChangeBallot` = the design's "change your vote until close". Both under `Vote.Cast`.
- **Abstain counts toward the cast-quorum** (it's a deliberate position, ADR-0010) — non-recused ballots with
  any choice, incl. Abstain, count for MinCast; recused ballots don't. Flagged.
- **No-MeetingId present-check skip** (a vote run outside a live meeting has no attendance to count).

**Verification.** `dotnet build acmp.sln` 0 errors (2 pre-existing NU1902 OpenTelemetry advisories only);
`dotnet format --verify-no-changes` clean; `dotnet test` green — **Domain 138 / Application 531 / Architecture
24 / Api 99 / Integration 17** (Testcontainers ran: new `VOTE` unique-key + migration-applies backstops). New
tests: `VoteTests` (15 — configure/open/cast/recuse/close guards, tally freeze, immutability, forward-only),
`VoteHandlerTests` (25 — each command incl. authz-deny, double-vote audited denial, present-quorum via a mocked
seam, VoteOpened fan-out, the SoD-3 AC-015/016 path, + the 3 coupling-guard tests), `VotesApiTests` (9 — HTTP
round-trips incl. AC-024 close-without-quorum and AC-025 cast-after-close 409), `MeetingQuorumSourceTests`.
Per-file **≥95% coverage gate green (global 99.71%)** — a follow-up added the `MeetingQuorumSource` direct test
+ Change/Recuse validator + not-found tests to lift 3 files the seam-mock/direct-handler tests had left <95%
(the first CI run caught them; `node scripts/check-coverage.mjs` now runs locally pre-push). Backend-only — no
FE/i18n change.

**AC.** AC-021/022/023/024/025/026 **Pending → Partial** (domain + handler + HTTP proven; the **Met** flip
waits on the live real-stack + `/votes/:id` UI leg → P9b/P17 per G-TRACE). AC-015/016 (SoD-3) strengthen from
Partial — the co-attestation GATE is now enforced + tested end-to-end at the issue path (live → P17). AC-052
(vote-open notification) strengthens — the `VoteOpened` trigger it was waiting for is now raised (live center
render → P9b/P17).

**Next.** P9b — the voting UI (`/votes/:key` off `ACMP Decision, Voting & ADR.dc.html` voting screens: ballot /
eligible voters / live tally / quorum pips / COI recusal / chairman approval-override; states
open/closed/not_open/quorum_failed/ineligible/double_error), wired to `/api/votes`, and the meeting-workspace
"Call vote" stub.

---

## P8d — AC-029 decision→action downstream-link gate (branch `feat/P8d-actions-decision-gate`)

### 2026-07-02 — the AC-029 gate retrofit (OQ-045 resolved; closes the Actions module)

**What (backend-only — Fork 2 GO).** A follow-up-bearing decision cannot be **Issued** until ≥1 downstream
artifact links to it (AC-029, FR-067, US-054). New cross-module seam `IActionLinkDirectory`
(`Acmp.Shared/Contracts/Actions`, mirrors `ICommitteeDirectory`) with one primitive method
`DecisionHasLinkedActionAsync(Guid decisionId)`; impl `ActionLinkDirectory` in `Actions.Infrastructure`
counts `(SourceType=Decision, SourceId)` via `AnyAsync` (no FK, no caller-side table read — ArchUnit green,
24 pass). `IssueDecisionHandler` injects it and, **for follow-up-bearing outcomes only**
(`DecisionOutcomeRules.RequiresDownstreamLink` — Approved/ConditionallyApproved/EnhancementsRequired/
DesignChangesRequired/ResearchRequired), throws `InvalidOperationException` → **409** when zero links; the
decision stays **Draft**. Rejected/Deferred/etc. issue freely.

**Two design calls (operator GO, both surfaced pre-code):**
- **Fork 1 — hard, not soft.** AC-029 + FR-067 + OQ-045(a) all mandate reject-and-stay-Draft; the brief's
  soft lean was overridden per "OQ-045's default wins."
- **Supersession exempt (ASM-014).** The check lives in `IssueDecisionHandler`, **not** `Decision.Issue` —
  the count is cross-module so the domain can't see it, and handler-placement auto-exempts
  `SupersedeDecisionHandler` (which drafts+issues the successor atomically with zero links; gating it would
  deadlock every correction). Not a bypass: first-issue is only reachable through the gated path, so every
  lineage root already passed it. OQ-045's "domain guard" + "supersession successor's issue" phrasing amended;
  ASM-014 recorded in docs/risks/risk-register.md.

**Why no FE (Fork 2).** No affordance triggers Draft→Issue today (`api/decisions.ts` = read + supersede;
record/issue UI was out of P7b scope), and under a hard gate an Issued decision *always* has ≥1 link — so a
"missing links" indicator is dead code. The issue-UI + create-on-draft relaxation is a later slice. No
`.dc.html` change, no visual-verify needed.

**Gates.** Backend build+format clean; **173 files @ 99.67%** per-file coverage (≥95% gate green); Domain 123
/ Arch 24 / Application 509 / Integration 16 / Api 90 all pass. New tests: handler reject/exempt + the
11-outcome `RequiresDownstreamLink` theory; API 409-then-link-then-204 (OQ-045 failing-first, real
cross-module seam) + Rejected-exemption. **AC-029 → Met.**

**Next.** P8 (Actions) is **complete** — P8a/b/b2a/b2b/c-1/c-2/d all shipped. Next phase per roadmap (P9
Voting / P10 Risks+Dependencies+Traceability, which will widen the AC-029 predicate to typed edges — ASM-014).

---

## P8c-2 — Administration → Job Monitor tab (branch `feat/P8c-2-actions-jobs-admin`)

### 2026-07-02 — the designed Hangfire Job Monitor tab (AC-056; operator fork B)

**What (backend).** Two bearer-authed endpoints on the existing `/api/admin` group (inherits
`Policies.AdminConfig` → Administrator-only, guardrail #4). `GET /api/admin/jobs` resolves `JobStorage`
**optionally from DI** (only registered when background jobs are enabled) → `Configured=false` under the
Testing host / a Hangfire-less deploy, else projects `GetMonitoringApi()` via a **pure `JobsMonitorMapper`**
(5 counts + a recent-runs table). `POST /api/admin/jobs/{id}/requeue` = the design's Retry button
(`IBackgroundJobClient.Requeue`) — **not read-only, so audited** (`admin.job.requeued`, guardrail #5); 503
when Hangfire is off, 404 when the job id is unknown. Reconciled the `AdminEndpoints.cs` header (Hangfire is
no longer "monitoring not configured" — it's a live tab with an honest `Configured=false` fallback).

**What (frontend).** Filled the designed `isJobs` tab (`ACMP Administration.dc.html`), replacing the
`ComingDataTab` placeholder. `api/jobs.ts` (`useAdminJobs` 30s poll + `useRequeueJob`); `JobMonitor.tsx`
composes the shared Table / StatusChip / Button / states (5 stat tiles + recent-runs table + Retry on failed
rows only); `jobFormat.ts` formats relative "when" via native **`Intl.RelativeTimeFormat`** (EN + AR plurals
for free) + duration. Full states: loading / error / **not-configured** / empty. i18n `admin.jobs.*` EN+AR by
hand; logical-property CSS (RTL-clean); axe AA.

**Honest-sparse.** Shows only jobs that actually run (the P8c-1 sweep) — not the mock's aspirational
DigestEmail/DiagramRender catalog. On a fresh stack the tiles are mostly zero and the table empty (the empty
state handles it), which the real-SQL boot confirmed.

**Decisions / flags.**
- **Test seam (advisor call).** The mapping lives in a pure `JobsMonitorMapper` unit-tested against a
  **mocked `IMonitoringApi`** (NSubstitute, added to Api.Tests) — deterministic ≥95% without a Hangfire server
  and without the Hangfire.InMemory dep. The endpoint's live `Map`/requeue branches are covered by wired
  integration tests that substitute `JobStorage`/`IBackgroundJobClient` into the Testing host.
- **Relative time.** Native `Intl.RelativeTimeFormat` renders "2 minutes ago" / "قبل دقيقتين" rather than the
  mock's compact "2m ago" — a deliberate, honest deviation (correct Arabic dual/plural forms, zero i18n keys).
- **Retries column.** The backend doesn't populate a retry count (would need a per-row `JobDetails` call), so
  non-failed rows show "—" and the Retry button carries no "n/m" suffix — honest-sparse, upgrade later if needed.

**Verification.** Backend: Application 496 / Domain 123 / Architecture 24 / Integration 16 / **Api 88** green;
per-file coverage gate **171 files @ 99.67%** (`JobsMonitor.cs` + `AdminEndpoints.cs` both 100%); `dotnet format
--verify-no-changes` clean. Frontend: **514 tests green**, new files 100% lines (`jobs.ts` / `JobMonitor.tsx` /
`jobFormat.ts`), i18n parity 843, `tsc -b` + `vite build` + oxlint clean. **Real-stack gate (P8c-1 lesson):**
booted a throwaway console mirroring `Program.cs`'s Hangfire registration against the running `acmp-sqlserver`
container → `JobStorage` resolves as `SqlServerStorage` (non-null) and `GetStatistics()` works, proving the
`Configured=true` path is real, not the fallback. Visual-verified the populated tab via a throwaway seeded-cache
harness in **EN-light + AR-RTL-dark** (tiles, chips, Retry, RTL mirroring, dark tokens all match), then deleted it.
**AC-056 → Met.**

---

## P8c-1 — Actions reminder/escalation Hangfire sweep (branch `feat/P8c-actions-jobs`)

### 2026-07-01 — app-owned Hangfire + the due-soon/overdue/escalation sweep (W22; AC-054/055)

**What (backend).** Stood Hangfire up FRESH (it was wired nowhere) and added the recurring action
reminder/escalation job. Design note: cadence + recipients are **doc-settled** (docs/domain/notification-strategy.md §3.4), not invented —
the only operator calls were the AC-056 dashboard flavour (**B = designed Job Monitor tab**, → P8c-2) and the
overdue rhythm (**system config, default DailyWhileOverdue**).
- **Hangfire** (`Hangfire.AspNetCore` + `Hangfire.SqlServer` 1.8.14) on ACMP's **own SQL**, its own `HangFire`
  schema, storage bootstraps its own tables (NOT EF migrations) — ADR-0014 / CON-001, zero new service. Wired in
  `Program.cs` (coverage-excluded) and **gated off under the "Testing" host** so the InMemory integration factory
  never opens a real SQL connection. The recurring job body just sends a MediatR command — all logic is testable.
- **`SweepActionRemindersHandler`** (`Actions.Application/Reminders`, `SweepActionRemindersCommand`): scans live
  actions with a due date and, per docs/domain/notification-strategy.md §3.4 — (1) **T-3 due reminder** (one-shot) to the owner; (2) **overdue**
  owner notice at the configured rhythm; (3) **escalation** to the Secretary (>7d) and Chairman (>14d), one-shot
  each. Headless (no `ICurrentUser`) → audit actor = `system:action-reminders`. All calendar-day math (UTC date
  floor) so the T-3 / >7 / >14 boundaries don't drift with the run time.
- **`ActionReminderOptions`** (appsettings `ActionReminders`, Options pattern): `DueReminderDaysBefore=3`,
  `EscalateToSecretaryAfterDays=7`, `EscalateToChairmanAfterDays=14`, `OverdueMode=Once|DailyWhileOverdue`
  (default DailyWhileOverdue), `SweepCron="0 6 * * *"`.
- **Idempotency:** 4 nullable markers on `ActionItem` (`DueReminderSentAt` / `OverdueNotifiedAt` /
  `EscalatedToSecretaryAt` / `EscalatedToChairmanAt`) + migration `Actions_ReminderMarkers` (forward-only). No
  outbox exists in this repo (`INotificationChannel` writes synchronously), so the markers ARE the dedup.
  DailyWhileOverdue = at most one owner notice per calendar day; Once = a single notice.
- **Recipient resolution:** new `ICommitteeDirectory.GetActiveMembersInRoleAsync(role)` (Membership-owned; reads
  the claims-derived `CommitteeMember.Role` cache) — Actions never reads Membership tables (ADR-0001, ArchUnit
  still green). 3 new bilingual `ActionNotifications` (DueReminder/Overdue/Escalation, EN+AR by hand).

**Decisions / flags.**
- **Save-before-send (at-most-once).** Markers commit FIRST, then notifications send; a concurrent-edit 409 on
  save aborts the run with nothing sent and Hangfire retries (markers keep the retry from double-sending). Favours
  "no spam" over "never miss"; a rare in-app send failure after commit is not retried (flagged).
- **Deferred (named, not silent):** `NotificationMessage` carries no urgency, so the catalog's Normal/High/Critical
  tiers aren't reflected in the center's High+Critical filter → a Notifications-module concern. Due-reminder is
  "opt-outable" in docs but no preference model exists (RD-09) → v1 sends unconditionally. Role cache is
  login-refreshed → escalation targets whoever the Secretary/Chairman was at last login (ASM, fine for ≤20 users).
  The live Hangfire cron firing on the real stack → P17.

**Verification.** `dotnet build` clean; **Application 496 / Domain 123 / Architecture 24 / Api 74 green** (Api boots
`Program` with Hangfire correctly gated off under Testing); per-file backend coverage gate **170 files @ 99.62%**
(new files ≥95%); `dotnet format --verify-no-changes` clean; migration is forward-only. Backend-only — no FE/i18n
change. Adversarial tests: T-3/T-4 boundary, due-today, one-shot idempotency, Once vs DailyWhileOverdue,
day-7-not / day-8-escalate, day-15 chairman, no-active-secretary (no crash), terminal/no-due-date exclusions, and
the save-before-send concurrency guarantee. **AC-054/055 → Partial; AC-056 → P8c-2.**

---

## P8b2b — Actions contextual create + Owner picker (branch `feat/P8b2b-actions-create`)

### 2026-07-01 — create a follow-up action from a decision (W13; Fork A backend expose + create form)

**What.** Creating actions, always **from a source artifact** (operator call B — no standalone create). First
context = the **Decision detail**.
- **Backend (Fork A):** `MemberDto` + `GetMembersHandler` now expose `KeycloakUserId` (the OIDC subject), so a
  committee UI can assign work by stable identity. Committee-wide readable like the rest of the directory —
  mild info-exposure flagged, low-risk for a ≤20-user on-prem committee (operator-approved). + a `GetMembers`
  unit test asserting the subject round-trips.
- **`api/members.ts`** gains `keycloakUserId`; **`api/actions.ts`** gains `ActionPriority`/`ActionSourceType`
  types + `useCreateAction` (POST `/api/actions` → 201 + the new `ActionSummary`; invalidates the actions family).
- **`CreateActionDialog.tsx`** (new) — composed to `ACMP Create Flows & Dialogs.dc.html` `action` (a REAL design
  ref, not no-reference): Title · Linked-to (locked to the source) · **Owner member-select** (keyed to
  `keycloakUserId`, active members only) · Due date · Priority segmented (Low/Medium/High → wire `Low/Normal/High`,
  default `Normal`) · Description. Title/description mirrored en===ar; on success routes to the new `/actions/:key`.
- **`DecisionPage.tsx`** — a role-gated "Create follow-up action" button on active decisions opens the dialog with
  the source pre-filled (`SourceType=Decision`, `SourceId=decn.id`, `SourceKey=decn.key`).
- **`ActionsRegister.tsx`** — **retired the disabled "New action" stub** (header + empty-state); the register has no
  create entry point (create is always contextual). `actions.css` + i18n `actions.create.*`/`actions.priority.*`
  EN+AR by hand.

**Decisions applied / flagged.**
- **Priority label:** the enum's middle value is `Normal`; the UI labels it **"Medium"/"متوسطة"** to match the design
  create form (wire value stays `Normal`).
- **Linked-to is locked** (read-only source key), not the design's free select — because create is always from a
  context (call B), the source is fixed, not chosen.
- **Create button gating:** Chairman/Secretary/Member on an **active** decision (Member is allow-if-owner; the API
  re-checks). Owner assignment happens in the dialog.
- Exercises the previously-untested **Member create** path (create form → `useCreateAction`); SoD on create stays
  API-enforced. AC-012/013 unchanged (verify leg; live real-stack → P17).

**Verification.** Backend `dotnet build` clean; Membership app tests **27 pass** (incl. the new Fork-A test); Membership
API tests **10 pass**. FE `tsc -b` clean; `oxlint` clean; `vite build` clean; `vitest run --coverage` **496 passed / 0
failed** (was 491), new/changed files **100% lines** (`actions.ts`, `members.ts`, `DecisionPage.tsx`,
`CreateActionDialog.tsx`); i18n parity **820 keys**. Visual-verified (throwaway harness, real CSS) EN-light +
AR-RTL-dark — the create form + segmented Priority + locked source row compose to the ref and mirror fully.

---

## P8b2a — Actions detail lifecycle + Verify UI (branch `feat/P8b2a-actions-lifecycle`)

### 2026-07-01 — the action detail lifecycle buttons + independent Verify (W14; read→write, no backend change)

**What (frontend, `Acmp.Web`).** Made the read-only action detail actionable: a lifecycle button row + transition
dialogs on `/actions/:key`, wired to the P8a endpoints. No backend change this slice.
- **`api/actions.ts`** — seven W14 mutation hooks by Guid `id` (the detail DTO carries `id`): `useStartAction`,
  `useUnblockAction`, `useVerifyAction` (no body), `useBlockAction`/`useCancelAction` (mirrored bilingual
  `reason`), `useUpdateActionProgress` (`progressPct`), `useCompleteAction` (optional mirrored `completionNote`).
  Each POSTs `/api/actions/{id}/{op}` (204) and **invalidates the whole `['actions']` family** — detail + register
  list + the global header counts — since a status change moves the overdue/total facets and the row chip.
- **`actionMeta.ts`** — `ALLOWED_TRANSITIONS` maps each status to the transitions the `ActionItem` domain guards
  permit (Open→start/progress/cancel · InProgress→block/progress/complete/cancel · Blocked→unblock/progress/cancel
  · Completed→verify/cancel · Verified/Cancelled→none), so the UI never offers a button that would hit a 400/409.
- **`ActionActions.tsx`** (new) — the gated button row + one transition dialog (reason/note/percent per op).
  Gating (docs/domain/permission-role-matrix.md rows 14–15): Chairman/Secretary manage any action; a Member manages only actions they **own**.
  **Verify is separated (SoD-1, AC-012/013): the owner/completer never sees it** — a person may not verify their
  own work. Uses the new `AcmpAuth.userId`.
- **`AuthProvider` / `AcmpAuthContext` / `oidcProfile`** — exposed the signed-in user's Keycloak subject as
  `AcmpAuth.userId` (read from `profile.sub`; `dev-user` in the DEV stub). **Frontend-only** — the id is already in
  the caller's own ID token; this only enables owner-gating. (Operator's Fork C = yes.)
- **`actions.css`** — `.act-lifecycle` row (logical props, top-border separator) + `.act-dlg-body`. i18n `actions.op.*`
  + `actions.dlg.*` (labels/titles/bodies/errors) EN+AR **by hand** (parity ≠ completeness).

**Decisions applied / flagged (visual SoT = the `.dc.html`; behaviour SoT = the package).**
- **NO-REFERENCE COMPOSITION (guardrail #14, flagged):** the `ACMP Lists & Registers.dc.html` Actions drawer is a
  **read** view — it draws no lifecycle buttons. The button row + dialogs are composed from the shared design system
  (`Button` + the Confirmation/Destructive `Dialog` patterns in `ACMP Create Flows & Dialogs.dc.html`) for a later
  design pass. Visual-verified (throwaway harness, real CSS) EN-light + AR-RTL-dark — button variants + full RTL
  mirroring (row + dialog) correct.
- **Operator forks (this session):** A = expose the member Keycloak id for the Owner select (→ P8b2b create).
  B = **no standalone action — always from a context** (final): the register's "New action" is NOT a real flow (the
  domain requires a source artifact; no standalone/Manual `ActionSourceType`), so contextual create lands in
  **P8b2b** and the register's disabled "New action" stub is retired there. C = expose `userId` (done, above).
- **Precise scope:** this slice exercises the **FE handling of a Verify 403 denial** and the client-side SoD-1
  hide — not SoD *enforcement* (that's P8a, backend). The **Member create/verify** SoD path stays untested until
  create ships (**P8b2b**). `AC-012/013` stay **Partial** (Met → P17 live real-stack).

**Honest defers (later slices):** contextual create form + Owner member-select + retire the register "New action"
stub → **P8b2b**; Hangfire reminders/escalation + Admin job dashboard (AC-054/055/056) → **P8c**; the IssueDecision
downstream-link gate (AC-029, OQ-045) → **P8d**.

**Verification.** FE `tsc -b` clean; `oxlint` clean; `vite build` clean; `vitest run --coverage` **491 passed / 0
failed** (was 470), new/changed files **100% lines** (`actions.ts`, `features/actions/*`, `AuthProvider.tsx`);
i18n parity **796 keys**; axe AA clean (ActionPage). Visual verify DONE (throwaway harness, deleted).

---

## P8b — Actions register + detail UI (branch `feat/P8b-actions-ui`)

### 2026-07-01 — the `/actions` register + routed `/actions/:key` detail (read-only)

**What (frontend, `Acmp.Web`).** The Actions register + detail screens, composed to
`ACMP product context/ACMP Lists & Registers.dc.html` (`isActions` table + drill-in). Read-only slice —
create + the lifecycle transitions land in **P8b2**.
- **`api/actions.ts`** — `useActionsRegister(params)` (GET /api/actions, paged; server-side `status[]` +
  `overdue` filters, `due`/`progress`/`status` sorts), `useAction(key)` (GET /api/actions/{key}, 404 = no
  retry), and `useActionsCounts()` — two `pageSize=1` count queries for the GLOBAL, filter-independent
  "N actions" + "N overdue" header (the paged list can't carry those facets).
- **`ActionsRegister.tsx`** — header (count + overdue badge), filter bar (Status server filter · Owner
  disabled stub · Overdue server toggle), the 7-column table (ID · Action · Linked · Owner · Due+clock ·
  Progress bar · Status chip), and the four states (live / 8-row skeleton / empty / error). Reuses the shared
  Table / FilterChip / StatusChip / states / Pagination. Retired the `/actions` PlaceholderPage.
- **`ActionPage.tsx`** — routed detail: header (key chip + status + overdue badge), 6-fact grid, progress
  bar, description, and a related panel (source ↑ display-only + a real `/meetings/:key` deep-link).
- **`actionMeta.ts`** — status→tone (6 values incl. Cancelled), 4-way register + 3-way detail progress
  colours, initials. **`actions.css`** — cells + detail layout at the reference's literal px, logical
  properties only (RTL-safe). i18n `actions.*` EN+AR (every status value by hand — parity ≠ completeness).

**Decisions applied / flagged (visual SoT = the `.dc.html`; behaviour SoT = the package).**
- **GO'd blessed deviation:** detail is a **routed `/actions/:key`** page, not the design's in-page drawer,
  so `ActionAssigned`/`ActionVerified` notifications deep-link. The design is to be updated to match.
- **Header counts are GLOBAL** (filter-independent, as the design computes them) via `useActionsCounts`; the
  "Showing X of Y" line is the paged/filtered count. Overdue is a **server** filter (`?overdue=true`), not
  the design's client toggle — correct under paging.
- **Only server-backed sorts exposed** (due/progress/status — `GetActionsRegister.Sort`); the design's
  ID/Action/Owner sorts have no server sort, so those columns are not sortable (same call as the backlog).
- **Owner filter rendered but disabled** — needs a verified owner directory keyed to Keycloak subjects;
  follow-up. **`Priority`** is in the DTO but has no design column → omitted. **Other-language subtitle**
  omitted on the detail (bilingual content mirrored → redundant), matching the decision detail.
- **`SourceKey` nullable** → "Linked" and the related-source row render `—` / are omitted rather than a raw
  Guid. **Related links are display-only** except the meeting deep-link; cross-artifact key→route resolution
  → P10 traceability. **Cancelled** has no design visual → neutral tone (no-reference), flagged.
- **New action + Saved view are honest disabled stubs** (create UI + saved views not built).

**Honest defers (not built — later slices):** create form + owner lifecycle transitions (start/block/
progress/complete) + independent **verify** UI (which also exercises the untested Member create/verify SoD-1
path) → **P8b2**; Hangfire reminders/escalation + Admin job dashboard (AC-054/055/056) → **P8c**; the
IssueDecision downstream-link gate (AC-029, OQ-045) → **P8d**.

**Verification.** FE `tsc -b` clean; `oxlint` clean (1 pre-existing Toast warning); `vite build` clean;
`vitest run --coverage` **470 passed / 0 failed**, new files **100% lines** (per-file ≥95% gate green,
global 98.86%); i18n parity **764 keys**; axe AA clean on register + detail. **Visual verify DONE** via a
throwaway dev-stub harness (real components + stubbed data, no auth/stack — the running `:8088` served the
pre-P8b bundle with no seeded Actions data): register + detail screenshot in **EN-light + AR-RTL-dark**,
both matching the `.dc.html` `isActions` reference — header/filters/7-col table, overdue red+clock, progress
thresholds, status tones, full RTL mirroring (main↔related swap, chevron flip, right-to-left bar fill), dark
tokens. **No drift.** (Minor: Arabic dates rendered Western digits in headless ICU; full Chrome/prod yields
Arabic-Indic + month names — a harness artifact, not a code issue.)

---

## P8a — Actions module backend (branch `feat/P8a-actions-backend`)

### 2026-07-01 — Actions bounded context: lifecycle + SoD-1 verify (W13/W14; AC-012/013)

**Module home.** A NEW **Actions** bounded context (Domain / Application / Infrastructure), mirroring the
Decisions module exactly (Clean Architecture per module, MediatR vertical slices, EF owned-types, per-year
key counter, cross-module value references, targeted in-app notifications). Backend only — no `Acmp.Web`
(the Actions register UI is P8b). The C# aggregate is named **`ActionItem`**, not `Action`, to avoid an
ambiguous-reference clash with the BCL `System.Action` delegate in every file that imports the namespace;
the DbSet/table/key/DTOs stay "Actions"/`ACT-`.

**What (backend).**
- **Domain.** `ActionItem` aggregate (`AuditableEntity` + `RowVersion` + `PublicId`), 6-state
  `ActionStatus` (Open→InProgress↔Blocked→Completed→Verified) + side `Cancelled`. Transitions: `Create`
  (W13, Open + owner + source), `Start`, `Block`/`Unblock` (reason retained), `UpdateProgress` (0–100),
  `Complete` (→100%, stamps the completer), `Verify` (Completed→Verified, write-once verifier stamp),
  `Cancel` (any non-terminal→Cancelled). **Overdue is DERIVED** (`IsOverdue(now)`, DueDate < now while
  Open/InProgress/Blocked) — never a stored status (docs/domain/entity-lifecycles.md line 159). Lifecycle events on every transition.
- **Application.** 8 command slices (Create/Start/Block/Unblock/UpdateProgress/Complete/Verify/Cancel — the
  six simple transitions share the `ActionTransition` load→mutate→save→audit helper) + 2 reads
  (`GetActionsRegister` paged/filtered with the derived-overdue + text filters in memory; `GetActionByKey`).
  **SoD-1 (AC-012/013)** lives in `VerifyActionHandler`: `SegregationOfDuties.CanVerifyAction(actor, owner,
  completedBy)` — a violation **audits the denied attempt then throws `ForbiddenAccessException` (403)**; the
  P4 predicate is now wired to its enforcement point. FluentValidation requires bilingual reasons/notes
  (mirrored EN+AR). Targeted (owner-only) notifications on create (assignment) + verify (closed), deep-linked
  `/actions/{key}`; audit on every transition.
- **Infrastructure.** `ActionsDbContext` (schema `actions`), `ActionConfiguration` (owned bilingual
  Title/Description/BlockedReason/CompletionNote/CancelReason, RowVersion, unique `Key`, `OwnerUserId` index,
  **`(SourceType, SourceId)` index for the P8d downstream-link query**); `ACT` key generator + counter;
  migration **`Actions_Init`**. Wired into `Program.cs` (module + MediatR assembly + `MapActionEndpoints`),
  `MigrationRunner`, and `acmp.sln`.
- **API.** `ActionsEndpoints` (`/api/actions`): register + detail committee-wide reads; create + the six
  transitions = `Action.Create`; verify = `Action.Verify` (SoD-1 in the handler). Domain guards → 409,
  not-found → 404, SoD-1 → 403, RowVersion → 409.

**Decisions applied / flagged (right-sized for ≤20 users; visual SoT = design, behavior SoT = package).**
- **People fields are Keycloak subject strings + name snapshots** (Owner/CompletedBy/VerifiedBy), like
  `Decision.ChairApprovedByUserId` — so the SoD-1 verifier≠owner/completer check is a direct comparison.
  docs/domain/domain-model.md types the owner as a member id; storing the sub for a self-contained SoD check is a **flagged
  modeling choice** (a member-id + resolver is heavier than this on-prem tool needs).
- **YAGNI deferrals (no AC + no design surface):** Assignees[] (Owner-only now), the append-only
  `ProgressUpdate` history table (ProgressPct + optional CompletionNote + audit instead), and file
  attachments — all flagged, none blocking an AC.
- **"Raised in meeting (MTG-…)"** modeled as an optional `MeetingKey` snapshot (not silently dropped).
- **`Complete` SETS `ProgressPct=100`** rather than requiring it first — a conscious divergence from docs/domain/domain-model.md's
  "cannot mark Completed if not 100%" invariant (completing an action means it's done; avoids a clunky
  two-step). Flagged.
- **`SourceId` is trusted client input** (validated `NotEmpty` only — no cross-module existence check, ADR-0001).
  P8d's AC-029 gate queries `(SourceType, SourceId)`, so a fabricated/typo'd source id yields an action that
  satisfies no real decision's gate — acceptable for ≤20 trusted users; carried into the **P8d** plan so the
  gate's correctness is not silently resting on unvalidated input.
- **AC-029 exemption (operator GO):** the P8d gate will apply only to follow-up-bearing outcomes
  (Approved/ConditionallyApproved/EnhancementsRequired/DesignChangesRequired/ResearchRequired) — not to
  Rejected/Deferred/etc.
- **ASM (record in RAID):** AC-029 will use the `ActionItem.SourceId` back-reference as the downstream-link
  proxy **until the P10 typed-edge Traceability module (ADR-0008)** exists; reconcile then.

**Honest defers (not built — later slices):** Hangfire due-soon reminders + overdue escalation + the
Admin job dashboard (AC-054/055/056) → **P8c**; the `IssueDecision` downstream-link gate (AC-029, OQ-045)
→ **P8d**; the Actions register + detail UI (`ACMP Lists & Registers.dc.html` `isActions`, routed
`/actions/:key`) → **P8b**.

**Verification.** BE `dotnet build acmp.sln` 0 errors (2 pre-existing NU1902 OpenTelemetry advisory
warnings only); `dotnet format --verify-no-changes` clean; `dotnet test acmp.sln` **709 passed / 0 failed**
(Domain 123 incl. 18 Action · Architecture 24 incl. the Actions-isolation boundary test · Application 472
incl. ~15 Action handler/validator · Api 74 incl. 7 Actions HTTP · Integration 16 incl. the `ACT` unique-key
+ migration-applies backstops via Testcontainers/Docker). Per-file coverage gate **168 files, global 99.62%**
(≥95%; every Actions file ≥95% lines). Architecture test asserts Actions depends on no other module's
Domain/Infra (Shared contracts only).

**AC.** **AC-012 / AC-013** (SoD-1 verifier ≠ owner) move to a **stronger Partial** — the domain transition,
the audited denial + 403, and the positive `ActionVerified` path are now proven at domain + handler + HTTP
pipeline; per the standing G-TRACE rule the **Met** flip waits on the live real-stack (Keycloak PKCE + real
SQL) leg → **P17**. AC-054/055/056 stay Pending → P8c; AC-029 stays Pending → P8d.

**Next.** P8b — Actions register + detail UI off `ACMP Lists & Registers.dc.html` (`isActions`), routed
`/actions/:key` (blessed deviation from the drawer), wired to `/api/actions`; retires the `/actions`
PlaceholderPage.

---

## P7d — Minutes UI (branch `feat/P7d-minutes-ui`)

### 2026-07-01 — the `/meetings/:key/minutes` tab, governed by isMinutes + denied

**What (frontend only; wires the P7c endpoints).**
- **`api/minutes.ts`** — TanStack Query hooks over the P7c API: `useMinutesForMeeting` (version history),
  `useMinutes` (version-aware detail, head by default), and the W10 mutations (draft/revise/submit/
  request-changes/approve/publish/supersede). Content is MIRRORED (`mirror(v) → { en:v, ar:v }`) — the
  editor yields one string sent to both columns (the locked FTS pattern). Every mutation invalidates the
  meeting's version list + the detail.
- **`MeetingMinutes.tsx`** — replaces the P6a placeholder in the existing meeting-shell tab. Renders by
  MoM status × role: a not-started gate; a manager create-draft form; the Draft editor (shared
  `MarkdownEditor`, Save draft = revise / Send for review = revise→submit); the InReview review card
  (Approve & publish / Request changes); Approved (Publish & notify); Published (locked read-only doc +
  approver footer + version history + disabled Export-PDF stub); Superseded (muted + reason banner); and
  a read-only / no-access path for non-managers. `minutes.css` (logical properties + tokens, RTL-safe).
- i18n `meetings.mom.*` namespace (EN+AR, all states/actions/denied). Tests: `api/minutes.test.ts`
  (hook URLs/bodies/invalidation) + `MeetingMinutes.test.tsx` (every state, role-gating, the approve→
  publish and revise→submit chains, supersede validation) + axe AA.

**Decisions applied / blessed deviations (visual SoT = design; behavior SoT = package).**
- **5-state vs the design's 3-toggle** — the design's single **"Approve & lock"** button is honoured as one
  **"Approve & publish"** action that drives BOTH backend transitions (approve → publish; notify fires on
  publish). The distinct Approved state still exists (a "Publish & notify" card handles a mid-state MoM).
- **Numbered Decision/Actions section cards (design) → a single markdown document body** — the MoM is one
  bilingual `Summary` (mirrored), rendered as text on read (no markdown→HTML dependency, DV-04). Decision
  linking is later; Actions are P8. Flagged.
- **SHA-256 hash footer → P14** (omitted); **Export PDF** = disabled stub; **denied** — meetings routes
  carry no extra role gate (single global auth gate), so non-managers get the read-only published record or
  the "no edit access" gate when nothing is published.
- **i18n collision fix:** the new namespace is `meetings.mom.*`, NOT `meetings.minutes` — the latter is the
  existing `"the count value min"` duration scalar (JSON last-key-wins would have shadowed it, breaking agenda
  duration labels). Caught by the AgendaBuilder test; renamed.

**Verification.** FE: `tsc -b` + `vite build` clean; `oxlint` clean (only the pre-existing Toast warning);
`vitest` **440 passed** (incl. 19 new: `api/minutes` 8, `MeetingMinutes` 11) — an intermittent
NotificationCenter axe/canvas `getContext` flake is pre-existing (jsdom), unrelated; per-file coverage gate
green (`perFile: true`, lines ≥95%): `api/minutes.ts` 100%, `MeetingMinutes.tsx` ≥95%; i18n EN/AR parity
holds; new axe AA case clean. Live real-stack VR vs `isMinutes` (EN-light + AR-RTL-dark) + the denied state
is the optional P17 tail (mirrors P7b).

**AC.** AC-014 / AC-036 / AC-037 / AC-038 stay **Partial** — the UI strengthens the evidence (the states +
role-gated actions render and drive the right calls), but per G-TRACE the **Met** flip waits on the live
real-stack pass (→ P17).

**Next.** P8 — Actions module (unblocks MoM→action linkage + AC-029 retrofit, OQ-045).

---

## P7c — MinutesOfMeeting backend (branch `feat/P7c-minutes-backend`)

### 2026-07-01 — MoM 5-state lifecycle in the Meetings module (W10; AC-014/036/037/038)

**Module home.** MoM is a NEW aggregate root **inside the existing Meetings module** — NOT a new module.
docs/domain/domain-model.md §B lists `MinutesOfMeeting` under Meetings and §C names it "aggregate root within Meetings", so
this is decided by canon (no OQ). It differs from P7a (Decisions genuinely IS its own bounded context);
MoM references its `Meeting` in-module (loading it to guard status + snapshot key/title is an in-module
read, not a cross-module reach — ADR-0001) and carries only ids/snapshots.

**What (backend only — no `Acmp.Web`; the Minutes UI is P7d).**
- **Domain.** `MinutesOfMeeting` aggregate (`AuditableEntity` + `RowVersion` + `PublicId`), 5-state
  `MinutesStatus` (Draft→InReview→Approved→Published→Superseded), lifecycle events. Transitions: `Draft`
  (v1), `Revise` (Draft-only body edit), `SubmitForReview`, `RequestChanges` (InReview→Draft), `Approve`
  (records approver + the soft-SoD-2 sole-author flag), `Publish` (seals), `Supersede` (Approved|Published→
  Superseded), and `PublishedCorrection` (the one-shot successor). No public state setters — the
  immutability reflection test proves it (AC-036).
- **Application.** 7 command slices + 2 reads (`DraftMinutes`/`ReviseMinutes`/`SubmitMinutesForReview`/
  `RequestMinutesChanges`/`ApproveMinutes`/`PublishMinutes`/`SupersedeMinutes`; `GetMinutesByKey`
  (version-aware, head by default) / `GetMinutesForMeeting` (version history newest-first)). FluentValidation
  requires BOTH EN+AR (mirrored-content pattern). `MinutesNotifications` (publish fan-out + targeted
  change-request notice); audit on every transition incl. the `MinutesApprovedBySoleAuthor` flag (AC-014).
- **Infrastructure.** `MinutesOfMeetingConfiguration` (owned bilingual `Summary` nvarchar(max), unique
  `(Key, Version)`, `MeetingId` index, RowVersion); `DbSet<MinutesOfMeeting>` on `MeetingsDbContext`; `MIN`
  prefix added to the existing `MeetingKeyGenerator`/counter. Migration **`Meetings_Minutes`** (new table only).
- **API.** `MinutesEndpoints` (draft/revise/submit = `Minutes.Capture`; request-changes/approve/publish/
  supersede = `Minutes.Approve`; reads committee-wide). Domain guards → 409, not-found → 404, RowVersion → 409.

**Decisions applied / blessed deviations (visual SoT = design; behavior SoT = package).**
- **5-state vs the design's 3-toggle (BLESSED DEVIATION, first-hand).** Read `ACMP Agenda & Meeting.dc.html`
  directly: `minutesState = draft/review/published` (3) with a single **"Publish & notify"** button, a
  version-history sidebar, and a `denied` state. Our operator-locked 5-state adds a distinct persisted
  `Approved` state (so the SoD-2 approval act and the publish/notify act are separable) + `Superseded`.
  The design's one "Publish & notify" button collapses approve+publish; the backend exposes both as
  distinct transitions (**notify-all fires on Publish only**). Design to be updated at P7d once blessed.
- **Approve vs Publish are two transitions** (operator-confirmed at GO). AC-038's single-step
  "approve → Published + notify" prose maps onto Approve (SoD-2 flag, no notify) **then** Publish
  (immutable + fan-out).
- **Version-preserving supersede** (unlike Decisions, which mint a new key): a correction keeps the SAME
  `MIN-YYYY-###` key and bumps `Version` (unique `(Key, Version)`); the successor is published in one
  transaction (`PublishedCorrection`) and the prior flips to `Superseded` with a back-link, never edited
  (AC-036) — consistent with the decision supersede-creates-issued-successor blessed deviation.
- **`Content:json (structured sections)` → a single bilingual markdown `Summary`** (`LocalizedString`,
  mirrored EN===AR) — structured sections aren't exercised by any P7c AC and align with the one-editor
  markdown-as-text decision (DV-04/AM-06). **Data-model deviation, flagged** (not silent — guardrail 11).
- **AC-037's `InReview→Draft` edge** is implemented per AC (W10 + AC-037 mandate it) though doc 12 §6's
  transition table omits the row — **doc-12 gap flagged**; its notification is targeted to the author
  (`CreatedBy`), not the all-members fan-out.
- **"Sole author" = `AuditableEntity.CreatedBy`** (docs/domain/domain-model.md models no author field) — single-author via the
  create stamp; multi-contributor tracking not modeled. Flagged.

**Honest defers (not built):** crypto hash-chain of published minutes → P14; SoD-3 co-attestation → P9;
anything needing the audit-query API → P14; MoM→Action linkage → P8 (Actions don't exist yet). Minutes UI
(govern off `isMinutes` + `denied`) → P7d.

**Verification.** BE `dotnet build` 0 errors; `dotnet format --verify-no-changes` clean; `dotnet test
acmp.sln` **661 passed / 0 failed** (Domain 105 incl. 13 MoM · Arch 20 · Application 454 incl. 16 MoM · Api
67 incl. 9 MoM · Integration 15 incl. the MoM `(Key,Version)` SQL backstop + migration-applies backstop);
per-file coverage gate **150 files, global 99.63%** (≥95%).

**AC.** AC-014 / AC-036 / AC-037 / AC-038 move **Pending → Partial** (domain + application + API proven by
tests; live HTTP/UI + the Met flip land with P7d/P17 per G-TRACE).

**Next.** P7d — Minutes UI (wire the `/meetings/:key/minutes` tab, `MeetingMinutes.tsx`, off
`ACMP Agenda & Meeting.dc.html` `isMinutes` + `denied`; reuse the shared `MarkdownEditor`).

---

## P7b follow-up — mirror decision content to both bilingual columns (branch `chore/p7b-mirror-content`)

### 2026-07-01 — reverse "entered language only" → MIRROR (en === ar)

**Why.** P7b shipped decision content stored in the entered UI language only (the other column empty, reads
fell back). Operator changed the call: **mirror the typed text into both `LocalizedString` columns
(`en === ar`)** so both are always populated — cleaner data and, notably, so SQL **Full-Text Search** indexes
both the EN and AR columns (an AR query would miss an empty AR column under the entered-language model).

**What.**
- **FE.** `SupersedeDialog` writes `{ en: v, ar: v }` (was current-language-only); `DecisionPage` `pick`
  reverts to a straight per-language read (no fallback needed — both columns match).
- **BE.** The record/supersede validators go back to **both-EN-and-AR required** (Title, Rationale, Reason,
  Condition text); `Title` keeps its per-language `MaximumLength(512)`. The `DecisionText.HasEitherLanguage`
  helper (the at-least-one predicate) is deleted — no longer referenced. Both-empty is still a clean 400.
- **Tests.** The "single-language is valid" validator test flips to "both languages required" (a field
  missing one language → 400); FE assertions expect the mirrored `{ en, ar }` body.

**Verification.** BE `dotnet build`/`format` clean, full `dotnet test` green + per-file coverage gate ≥95%;
FE `tsc`/`vite build`/`oxlint` clean, i18n parity unchanged, `vitest` green (decisions files ≥95% lines).
No schema change (columns already NOT NULL); no migration.

**AC.** No verdict changes — data-model/validation reconciliation, not a feature. AC-027/028 stay Partial.

**Next.** P7c MinutesOfMeeting backend.

---

## P7b — Decision detail UI + Decision.Title (branch `feat/P7b-decision-ui`)

### 2026-07-01 — `isDecision` screen + supersede dialog, with an additive `Title` on the aggregate

**What.**
- **Decision.Title (additive model reopen).** Added a required bilingual `LocalizedString Title` to the
  `Decision` aggregate (domain guard mirrors `Rationale`), threaded through `RecordDecision` /
  `SupersedeDecision` (commands + validators: EN/AR non-empty → clean 400), `DecisionSummaryDto` /
  `DecisionDetailDto`, `DecisionMapping`, the two endpoint bodies, and a new EF owned column-pair
  `title_en`/`title_ar` (NOT NULL, maxlen 512). Migration **`Decisions_AddTitle`** (additive,
  `defaultValue: ""` for any existing rows). Operator chose a design-faithful title over
  outcome-as-headline; this intentionally reopened the merged P7a model with one forward-only migration.
- **Decision detail screen** (`features/decisions/DecisionPage.tsx` + `decisions.css`, logical CSS only),
  route `/decisions/:key`, reachable via the `DecisionIssued` notification deep-link. Header = DECN key chip +
  outcome `StatusChip` (tone by outcome) + Superseded badge + `Title` (h1) + chair-only actions; body =
  Rationale + Conditions checklist (tone by condition status) + Alternatives; sidebar = record detail
  (outcome / approving authority / issued-at). Superseded state renders the neutral badge + banner + muted
  body. Loading / not-found (404) / error states like `TopicDetail`. Breadcrumb owned by the shell
  (`deriveBreadcrumbs` gains the `decisions/:key` leaf).
- **Supersede dialog** (`SupersedeDialog.tsx`) wired to `POST /{id}/supersede`. Because the shipped command
  drafts + auto-issues a FULL successor decision in one transaction, the dialog captures that successor body
  (outcome / title / rationale / optional alternatives / conditions-when-conditional) **plus** the reason —
  the shared `MarkdownEditor` is reused for rationale/alternatives. On success it invalidates the prior
  detail and navigates to the successor key.
- **api/decisions.ts** (`useDecision` read-by-key, `useSupersedeDecision` mutate-by-id + invalidation),
  mirroring `api/meetings.ts`. New `decisions.*` i18n namespace (EN+AR, all 11 outcomes + both dialogs).

**Decisions applied / blessed deviations (visual SoT = design; behavior SoT = package).**
- **Supersede dialog extended** from the design's single reason field to the full successor body — blessed
  deviation (design to be updated); the one-field mock can't drive the transactional supersede-creates-successor.
- **Content entered in the current UI language only** (operator decision) — the text is stored in that
  language's column and the other stays empty; reads fall back to the populated column. This relaxed the
  P7a bilingual validators from "both languages required" to "at least one language required" (Title,
  Rationale, Reason, Condition text, in both commands; backward-compatible — both-empty is still a 400).
  Title keeps a per-language `MaximumLength(512)` so an over-long title is a clean 400, not a SaveChanges 500.
  Matches how Topics store single-language text today; a true bilingual content-editing surface is future work.
- **No-reference composition:** outcome→StatusChip tone map (design ships only one outcome visual) — flagged.
- **Honest defers (no fabrication):** Convert-to-ADR = disabled stub (ADR module → P9/P11); the from-topic
  link is omitted (the DTO carries the topic **Guid**, not a TOP- key — ADR-0001, no cross-module lookup);
  the superseded banner shows the reason but not the successor's DECN- key (only its Guid is on the prior's
  DTO — the supersede round-trip navigates to it directly); Alternatives render as stored text, not the
  design's structured "Not chosen" cards (one `LocalizedString`, not modeled) — operator-confirmed; Vote
  result, Effective date, Decided-in-meeting, Affected systems, and the immutable-history timeline are not
  rendered (vote → P9; audit-query → BL-066/P14; relationships not modeled).
- **Record/Issue UI is out of P7b scope** (operator-confirmed): the detail + supersede of an already-issued
  decision only; the meeting-workspace "Record decision" stub stays a stub.

**Verification.** BE: `dotnet build` 0 errors, `dotnet format --verify-no-changes` clean, `dotnet test acmp.sln`
**620 passed / 0 failed** (Domain 92 · Arch 20 · Application 436 · Api 58 · Integration 14 incl. the DECN
migration/backstop), per-file coverage gate **134 files, global 99.66%** (≥95%). FE: `tsc -b` + `vite build`
clean, `oxlint` clean, i18n parity **670 keys** (0 drift), `vitest` **422 passed** with per-file lines ≥95%
(decisions files: `api/decisions.ts` 100%, `DecisionPage.tsx`/`SupersedeDialog.tsx`/`decisionMeta.ts` 100%
lines), new axe AA case clean. **Dev-stub VR done** (`npm run dev` + Playwright route-mocked
`/api/decisions`, dev auth stub as Chairman): the Issued, supersede-dialog, and Superseded states were
screenshotted in **EN-light + AR-RTL-dark** and reconciled against `isDecision` — faithful (key/outcome
chips, rationale/conditions/alternatives, record-detail sidebar, superseded badge+banner, full RTL mirror +
dark tokens); no drift. A live real-stack pass (Keycloak PKCE, an API-issued decision) is the optional P17 tail.

**AC.** AC-027 / AC-028 stay **Partial** — the live UI read + supersede round-trip strengthen the evidence,
but per G-TRACE the **Met** flip waits on the live HTTP/UI leg (→ P17). No verdict flips this slice.

**Next.** P7c MinutesOfMeeting backend, then P7d Minutes UI (`ACMP Agenda & Meeting.dc.html` isMinutes).

---

## P7a — Decisions module backend (record / issue / supersede)

### 2026-07-01 — Decisions bounded context: W12 (issue) + decision half of W21 (supersede) (branch `feat/P7a-decisions-backend`)

**What.** New **Decisions** module (Domain / Application / Infrastructure), mirroring Topics/Meetings exactly
(Clean Architecture per module, MediatR vertical slices, EF owned-types, per-year key counter, cross-module
seams, in-app notification fan-out). Backend only — no `Acmp.Web` changes (frontend is a later slice).

- **Domain.** `Decision` aggregate root (AuditableEntity + `RowVersion` + `PublicId`) with owned
  `DecisionCondition` collection. Factory `Decision.Draft` (guards: topic + rationale required;
  ConditionallyApproved ⇒ ≥1 condition); `Issue` (Draft→Issued, override-needs-justification guard, raises
  `DecisionIssuedEvent` carrying the override flag); `Supersede` (Issued→Superseded, back-link + reason).
  **Immutability (AC-027):** no public mutators for the aggregate's own state, re-issue/re-supersede throw —
  there is intentionally no edit command. `DecisionOutcome` = the README §E committee outcomes (11 values);
  `DecisionStatus` {Draft, Issued, Superseded}; `DecisionConditionStatus` {Open, Met, Waived}.
- **Application.** `IDecisionsDbContext`; commands `RecordDecision` (policy `Decision.Record`,
  Secretary/Chairman), `IssueDecision` (policy `Decision.ChairApprove`, Chairman), `SupersedeDecision`
  (Chairman) — supersede drafts+issues the successor **before** flipping the prior (W21 ordering), one
  transaction, audits both. Queries `GetDecisionByKey` (detail) + `GetDecisionsByTopic` (history, filtered by
  Topic **PublicId** — never a TOP- key, which would breach ADR-0001). Validators check `LocalizedString.En/Ar`
  themselves so empty bilingual text returns **400** (the positional ctor doesn't validate; `Create` would
  500). Shared `DecisionIssuance` helper applies the post-issue side-effects for both issue and supersede.
- **Cross-module seam.** New `ITopicDecisionRecorder` (Shared/Contracts/Topics) → `MarkDecidedAsync` advances
  a topic InCommittee→Decided on issue, **idempotent** (no-op otherwise). Implemented as `TopicDecisionRecorder`
  in Topics.Infrastructure (mirrors `TopicScheduler`); `Topic.Decide()` already existed, so no new transition.
- **Notifications.** On issue, one in-app `DecisionIssued` notification per active member (bilingual,
  deep-link `/decisions/{Key}`) via `ICommitteeDirectory` + `INotificationChannel`. The supersession
  successor — a genuine issued decision — fires the same side-effects (idempotent seam + notification),
  deliberately (ponytail note in `SupersedeDecision`).
- **Infrastructure.** `DecisionsDbContext` (schema `decisions`), `DecisionConfiguration` (RowVersion, unique
  `Key`, owned `decision_conditions` table, owned LocalizedString column-pairs — `rationale_*` NOT NULL, the
  rest nullable, verified in the migration), `DecisionKeyGenerator` (DECN, own counter table), design-time
  factory. Migration **`Decisions_Init`** generated. Wired into `Program.cs` (module + MediatR assembly +
  `MapDecisionEndpoints`) and `MigrationRunner`; 3 projects added to `acmp.sln`.
- **API.** `DecisionsEndpoints`: `POST /api/decisions` [Decision.Record]→201, `POST /{id}/issue`
  [Decision.ChairApprove]→204, `POST /{id}/supersede` [Decision.ChairApprove]→201, `GET /{key}`,
  `GET ?topic={guid}` (authenticated). Problem Details + existing GlobalExceptionHandler (409 on stale write).

**Decisions applied.** Topic-history query is by Guid PublicId only (ADR-0001 — no cross-module key lookup, no
snapshot field added to the locked model). AC-029 (downstream-link-required-to-issue) **DEFERRED to P8** —
unbuildable until Actions exist; recorded as **OQ-045** and NOT enforced. AC-016 (SoD-3 co-attestation) records
the override choice + justification + flag now, but the co-attestation gate stays Partial→P9 (vote-coupled).

**Verification.** `dotnet build acmp.sln -c Release` → 0 errors (2 pre-existing NU1902 OpenTelemetry advisory
warnings only). `dotnet test acmp.sln` → **617 passed, 0 failed** (Domain 92, Architecture 20, Application 433,
Integration 14 incl. the DECN unique-key + migration-applies backstops via Testcontainers/Docker, API 58).
`dotnet format --verify-no-changes` → clean. Architecture tests assert Decisions depends on no other module's
Domain/Infra (Shared contracts only). Full re-verified post-review: **620 passed, 0 failed**; CI per-file
coverage gate green (134 files, global **99.61%**).

**Post-review hardening (csharp-reviewer, 1 HIGH + 1 LOW acted on).** (1) **HIGH** — a `ConditionallyApproved`
condition with null/empty bilingual `Text` reached the domain ctor and surfaced as a 409 instead of a clean
400; added per-condition `RuleForEach(...).ChildRules` text validation to **both** `RecordDecisionValidator`
and `SupersedeDecisionValidator` (+ a failing-first validator test). (2) **LOW** — `GetDecisionsByTopic`
now orders `CreatedAt DESC, Id DESC` (explicit chronological intent + a deterministic tiebreaker for
same-instant rows; ordering by `CreatedAt` alone was non-deterministic under a fixed test clock). (3) the
two-DbContext Issue→Topic-Decided commit is **non-atomic by design** — the same accepted pattern as the
existing `ITopicScheduler` seam (both contexts share one SQL connection; durable outbox is the documented
upgrade path, CLAUDE.md). Decision-immutability/supersede ordering/authz/module-boundary/audit/IDOR and the
past notification shared-instance bug were all reviewer-verified clean.

**Next.** P7b (Decisions frontend) / P8 (Actions + the AC-029 link-gate retrofit per OQ-045).

---

## Round-2 Reconcile — DV-04 (rich-text unification) to one shared editor

### 2026-07-01 — Unify the three divergent rich-text surfaces into one MarkdownEditor (branch `feat/dv-04-rich-text`)

**Why.** Rebuild-findings §8 / AM-06: rich text was decided three different ways — Submit-topic had an
**inert `aria-hidden` toolbar** (dead glyphs over a plain textarea), Meeting-notes had a **functional
markdown** editor, and Minutes-of-Meeting was deferred — with no single decision on the model or what is
stored. **DV-04 decision (operator, 2026-07-01): markdown stored as text, one shared editor.**

**What.**
- **New `components/ui/MarkdownEditor.tsx`** — a plain `<textarea>` + a small real formatting toolbar
  (bold/italic/bulleted/numbered/link) that inserts markdown marks around the selection; the body is
  **stored as markdown text** (no rich-text framework, no HTML, no sanitization surface — right-sized for
  ≤20 users). Accepts either a standalone `ariaLabel` or field-provided `id`/`aria-*` so it drops into a
  labelled `<Field>` or stands alone. Extracted verbatim from the working meeting-notes logic.
- **MeetingWorkspace** `DiscussionNote` now renders `<MarkdownEditor>` (kept its header + autosave);
  removed the duplicated `EDITOR_TOOLS` + selection-transform logic and the now-unused `useRef`.
- **SubmitTopic** description field now renders `<MarkdownEditor>` (real markdown) in place of the inert
  toolbar + `Textarea` — the formerly decorative toolbar is now functional.
- **CSS** moved `.mt-editor*` → shared `.md-editor*` in `components.css` (globally loaded); removed the
  old `.mt-editor*` (meetings.css) and the dead `.sub-rte*` (topics.css).
- **i18n** moved `meetings.editor.*` → top-level shared `editor.*` (toolbar/bold/italic/bulletList/
  numberedList/link), EN + AR; key parity verified (0 drift).

**Scope note (honest defer).** "Stored as markdown" settles the editor + data model. **Rendering** stored
markdown on read (Topic detail, meeting history) is left verbatim for now — adding a markdown→HTML
renderer brings a dependency + an XSS-sanitization surface, out of scope for this unification slice and
tracked as a follow-up. Closes the editor-consistency half of AM-06 / rebuild-findings §8.3.

**Verification.** FE 402 tests green (new MarkdownEditor.test + unchanged consumer tests; the
MeetingWorkspace Bold-wrap + SubmitTopic description tests pass through the shared component). Typecheck +
oxlint clean; EN/AR parity 0 drift. Batched VR (P3–P6) follows per the operator plan.

**Next.** Batched full VR across the P3–P6 reconciled surfaces (EN-light + AR-RTL-dark), then the audit-
reversal sign-off (P6b read-all), then net-new P7 (Minutes & Decisions) on the settled markdown model.

---

## Round-2 Reconcile — P6b (Notifications IA) to the updated design

### 2026-06-30 — Reconcile the bell popover + full inbox to ACMP.dc.html (branch `feat/p6b-notifications-ia`)

**Why.** Round-1's notification center predated the design (its own comment said "no .dc.html
exists" — a `role="region"` popover with a plain title/body list, and an inbox with a segmented
Unread/All control). The authority is now **ACMP.dc.html L92–131** (bell popover) and **L706–739**
(`isNotifPage` full inbox). `ACMP System States.dc.html` `isNotif` is the **preferences page →
NOT built in v1 (RD-09)**; no profile prefs link wired.

**Scope = (B) full reconcile incl. backend** (operator, 2026-06-30). Most of "Option B" was already
shipped by P6e (the `/api/notifications/read-all` endpoint + `MarkAllNotificationsReadHandler`, the
`Category` field, IDOR-scoped queries). Per the operator's pre-authorisation — *type = existing
`Category`, key derived from the `DeepLink`'s last segment, "derive over a new stored column"* — the
only backend delta is the audit emit. **No migration, no DTO change.**

**What.**
- **Backend — audit on bulk read.** `MarkAllNotificationsReadHandler` now takes `IAuditSink` and emits
  `Notifications.AllRead` (`{ marked }`) after `SaveChangesAsync`, only when something changed (the
  `count==0` short-circuit returns before persistence → no audit noise). **This reverses P6e's
  deliberate no-audit choice for read-all** (a one-click inbox sweep is worth a record, guardrail 5 /
  docs/domain/audit-and-records.md); the single `MarkNotificationRead` stays un-audited (accepted asymmetry — revert if the
  no-audit was intentional per ADR-0009). Stale comment updated.
- **FE presentation helper** (`api/notifPresentation.ts`): `notifType(category)` → `{ labelKey, tone,
  icon }` with a neutral `default` fallback (never a blank TYPE); `notifKey(deepLink)` → the runtime
  key from the last path segment (query/hash/trailing-slash stripped; `null` link → no key chip).
- **Bell popover** (`NotificationCenter.tsx`) → `role="dialog"` + click-away scrim; header `{n} new`
  pill + **Mark all read**; **Unread/All** segmented tabs; loading **skeleton** (shared `.skeleton`
  shimmer); rows = tone icon · artifact key (deep-link) · time · message · per-item **mark-read dot**;
  footer **View all + chevron**. Kept the TopBar `.notif-badge` class (core-loop E2E asserts it).
- **Inbox** (`NotificationsPage.tsx`) → header **Mark all read** + an in-app **channel** line;
  **Unread/All underline tabs with counts** (Unread default); card rows = tone icon · **TYPE label +
  key + time** · message · inline **Mark read** pill; check-circle empty card. Kept **Load-more**
  (DV-02). Breadcrumb is shell-owned (`deriveBreadcrumbs` already maps `/notifications`) — not
  re-added per LP-01.
- **i18n**: added top-level `notif.{newCount,markRead,viewAll,channel,type.*}` to **both** en + ar by
  hand (renamed `seeAll`→`viewAll`); EN/AR key parity verified (0 drift).

**Decisions resolved.** DV-02 (Load-more vs infinite) → **blessed** (design list has no infinite
scroll). DV-05 (Unread/All) → **confirmed by the design** (it ships the tabs). RD-09 → v1 in-app only,
**no preferences page**.

**Verification.** FE 397 tests green (new/updated notif tests incl. nested-deep-link key derivation); per-file coverage — notifPresentation
100/100/100, NotificationCenter L100 B95.6, NotificationsPage L100 B100 (lines ≥95% gate met). BE
Application 420 + Api 5 green; read-all both branches covered (marks-some emits one audit, marks-none
emits none + user-scoped). Typecheck + oxlint clean. Dev-stub VR (vite + Playwright route-mock anchored
to origin-root `/api/`): popover + inbox in **EN-light** and **AR-RTL-dark** match the design
(RTL mirrors panel/breadcrumb/rows + chevrons; mono keys stay LTR). **GO.**

**Next.** P6b done (PR opened off `main`, not self-merged — independent of unmerged #53). Next slice =
**Decisions/Minutes** (the `MeetingMinutes` placeholder points there) + remaining round-2 targets.

---

## Round-2 Reconcile — P6a (Meetings IA) to the updated design + Usage Map

### 2026-06-30 — Refactor the meeting page into a SHELL + nested content surfaces (branch `feat/p6a-meeting-ia`)

**Why.** Round-1 put the whole meeting detail on one page with an in-page 4-tab state switcher
(Agenda · Meeting · Minutes · Recording) and the lifecycle banner above the tabs. The Usage Map +
`ACMP Meetings.dc.html` reconcile to a deep-linkable IA: **Meetings owns the shell** (header card +
overview/recording + the single route-denial = the global auth gate, **RD-08**), **Agenda & Meeting
owns the content** (agenda/conduct/minutes). **NV-08**: the conduct surface is a runtime composition
of Attendance + Notes while InProgress, each deep-linkable.

**What.**
- **MeetingPage → meeting SHELL.** Header card (key chip + status chip + title + when·type·mode meta +
  the lifecycle primary action) + a 6-tab deep-linkable `NavLink` strip + `<Outlet/>`. The shell
  fetches the detail once for its header; each child route re-reads the same cached query
  (react-query dedupes), so every route element is prop-free. Loading/error/404 owned by the shell.
- **Nested routes** under `meetings/:key`: index → `MeetingOverview`; `/agenda` → `AgendaBuilder`;
  `/attendance` + `/notes` → `MeetingConduct` (renders the full `MeetingWorkspace` while InProgress,
  an honest lifecycle gate otherwise); `/minutes` → `MeetingMinutes` (P7 placeholder); `/recording` →
  `MeetingRecording` (Webex Phase-2 honest-defer).
- **New `MeetingOverview`** — the conditional lifecycle banner (moved off the top of the page) +
  grid[ agenda-preview card (reuses the exported `AgendaPreview`, only when items > 0 — the empty
  branch stays unreachable, so AgendaBuilder's `v8 ignore` on it remains honest) | readiness rows
  (Agenda published / Topics scheduled / Quorum expected `needed of eligible`) + quick-links ].
- **New thin route components** `MeetingMinutes` / `MeetingRecording`, plus a shared `MeetingGate`
  extracted from the old MeetingPage (centered placeholder card, reused by the conduct gate too).
- **`AgendaBuilder`** now derives `readOnly` from the fetched meeting status (was a prop passed by
  MeetingPage) so it's a prop-free routed element; behavior identical.
- **Lifecycle primary action (lcMap):** notReady→“Build agenda” (primary→/agenda) · ready→“Start
  meeting” (primary→start mutation) · inProgress→“Open live notes” (primary→/notes) · concluded→
  “Review minutes” (ghost→/minutes) · cancelled→“Reschedule” (ghost→/meetings/new).

**Findings closed.**
- **DV-16** — re-added the actual-time + outcome control to the workspace active item
  (minutes input + outcome Select {Discussed/Deferred/CarriedOver} + “Record time”), wired to
  `useRecordActualTime` (`POST …/agenda/items/{topicId}/actual-time`). Round-1 had deferred the UI;
  the backend command + hook were already in place.
- **DV-21** — agenda pool label “Scheduled topics”/“Ready to schedule” → **“Prepared”** (matches the
  actual source: `GET /api/topics?status=Prepared`), EN + AR.
- **DV-03** — elapsed timer confirmed `mm:ss` / `h:mm:ss` (`formatElapsed`); no change needed
  (VR shows `8:49:49`).

**Blessed deviation (guardrail 14).** The design reaches Recording via an overview quick-link; the IA
promotes it to a 6th peer tab (Overview·Agenda·Attendance·Notes·Minutes·Recording) per NV-08 + the
route map. Recorded here and in the acceptance audit.

**RD-08 “remove the duplicate denied.”** Verified by grep — there is **no** `denied` artifact in the
meetings feature; route-denial is the global `ProtectedRoute`/`RequireRole` (meetings routes carry no
extra role gate), i.e. already a single source. Nothing to dedupe in code.

**Quality.** FE suite green (384 tests; 81 in the meetings feature incl. the new MeetingPage shell +
conduct, MeetingOverview, MeetingMinutes, MeetingRecording suites and the DV-16 workspace tests).
Per-file **lines ≥95%** for every touched/added file (AgendaBuilder 95.21, MeetingWorkspace 96.32, the
rest 100); global 98.62%. `tsc -b` clean; oxlint clean (only the pre-existing Toast warning). i18n
parity OK (608 keys; new `tab.{overview,attendance,notes}` / shell-action / `overview.*` keys added to
EN **and** AR). axe-clean component tests for MeetingOverview + MeetingWorkspace. No backend code
changed (the `AgendaItemOutcome` enum was only read to source the DV-16 options).

**VR.** Live Docker stack was **down** (`:8088` refused) → **dev-stub VR**: `npm run dev` (DEV auth, no
Keycloak) + Playwright `/api/**` route-mocks. Captured Overview + conduct workspace in **EN-light** and
**AR-RTL-dark** — header card, 6-tab strip, in-progress banner, agenda preview, readiness, quick-links,
and the DV-16 control all render; AR fully mirrors (sidebar/tabs/banner/quick-link chevrons) in dark.

**Next (P6b):** notifications IA (bell popover + full inbox, **no preferences page** in v1 per RD-09) +
the DV confirmations slice.

---

## Round-2 Reconcile — P4 (Administration) to the updated design + Usage Map

### 2026-06-30 — Build the six non-Users admin tabs from `ACMP Administration.dc.html` (branch `feat/p4-admin-reconcile`)

**Why.** P4 shipped only Users & Membership; the other six sub-tabs were disabled "no-reference-yet"
placeholders. The design update added full `health / templates / streams / roles / jobs / notif` sections,
so they can now be built honestly. **Usage Map is the phasing authority** and confirms the split: P4 = users +
userdetail (Built); P14 = health + jobs; P15 = templates + streams + roles + notif. The design mockup marks
**no** sub-tab disabled, so every tab is now navigable — content truthfulness is set by data availability, not
by hiding the tab.

**What.**
- **NR-08 — System Health (built real).** New `GET /api/admin/health` (`AdminEndpoints`, `Policies.AdminConfig`)
  aggregates the registered `HealthCheckService`; added a synthetic `api` liveness check so the "Application"
  tile is truthful. FE `useSystemHealth` (30s poll) + `SystemHealth.tsx`: a **fixed 6-service catalog** overlays
  real status + latency where a check exists and renders the rest as **"monitoring not configured"** — never an
  invented status. Overall banner derives from monitored checks only. Removed the "no-reference" label.
- **Roles (built canonical).** `RolesReference.tsx` — the 8 committee roles mirrored read-only from the Keycloak
  realm / docs/domain/permission-role-matrix.md. Static reference content, EN/AR.
- **Notification Settings (built canonical, read-only).** `NotificationSettings.tsx` — channel cards (in-app
  Active; Email/Webex Phase-2 Planned, ADR-0005) + the per-event default matrix. Toggles are presentational
  (no settings store until P15).
- **Templates / Streams / Job Monitor (honest-empty).** `ComingDataTab.tsx` renders the design's empty state
  with module-specific copy; their data is P14/P15. No fabricated rows.
- **Restructure.** `AdministrationPage` is now the sub-tab container (owns `sub` + user-detail state, header,
  7-tab strip); `UsersMembership` split into `UsersDirectory` + `UserDetail` bodies. Opening a user detail
  replaces the tabbed view (design `userdetail` sub-state).
- **DI-03 / OQ-042 — confirmed.** Invite / in-app "Provision via Keycloak" stays removed (ADR-0015); provision
  is the sanctioned KC-console deep-link only (added when a console URL is configured — follow-up).
- **Assignments "—" — confirmed.** No assignee/count API exists; the honest dash + hint stands (no change).

**Recorded design deviations (op-approved — the design shows data we don't collect on-prem in v1).**
- Health tiles omit **uptime% / p95** (not collected); status + **real latency** (`HealthReportEntry.Duration`)
  + description are shown.
- Unregistered services (MinIO/Seq/Hangfire/Webex) read **"monitoring not configured"** — running, not down.
- Notification per-event toggles + channel switches are **read-only** (no persistence backend until P15).

**Tests.** BE `AdminEndpointsTests` (admin 200 + `api` entry / Member 403 / anon 401). FE `AdministrationPage.test`
(7 tabs navigable, per-tab bodies, detail-replaces-tabs), `SystemHealth.test` (loading/error/operational/degraded/
down/unmonitored/refresh), `UsersMembership.test` (directory + detail), `systemHealth` hook test. Full suite green
(371 FE tests); new files ≥95% lines (systemHealth.ts/icons.tsx/administration 100%). EN/AR parity 589/589.

**VR.** Live VR remains blocked on the operator setting the `acmp-admin` Keycloak dev password (standing P4
caveat) — captured via dev-stub VR (Vite DEV auth auto-admin), same method as P3.

**Next.** P4 remainder is data-bound to P14/P15. Natural next slice: P6 round-2 (RD-08 meeting-page ownership,
NV-08 meeting sub-tabs, DV-16 actual-time control, DV-04 rich-text unification).

---

## Round-2 Reconcile — P3 (foundation) to the updated design + Usage Map

### 2026-06-30 — Close the P3 forensic findings against `ACMP Usage Map` + `ACMP Design System` (branch `chore/p3-round2-reconcile`)

**Why.** The design update (`ACMP product context/CHANGELOG-design-update.md`) published the Usage Map as
the authoritative per-phase/flow index and settled every open question from the forensic review §10. This
slice reconciles the **P3 foundation** surface (tokens, shell, nav/IA, global states) to it. Operator chose
**Option 1 (full §G route alignment)**.

**Key finding up front — most of P3 was already correct.** The nav labels (`nav.agenda`="Agenda & Meetings",
`nav.adrs`="ADRs & Invariants", `nav.wiki`="Knowledge / Wiki"), the nav groups (Committee/Governance/
Knowledge/Insights/System), and the **StatusChip size** (md 24/9/12, sm 22/8/11.5, r6) already matched the
now-locked design canon. So DV-01/AM-01/NV-01/02/03/04 needed **no visual change** — only tokenisation +
route/label confirmation. DI-06 was already fixed in PR #50.

**What.**
- **LP-02 — named off-scale tokens.** Published the design's named tokens in `tokens.css`
  (`--chip-radius`, `--chip-h/px/fs-md|sm`, `--header-h:60`, `--sidebar-w:244`, `--row-min`, `--field-gap`,
  `--page-max`, `--agenda-budget-h/card-pad/rail-w`) and consumed them: StatusChip (no pixel change),
  `.page` max-width, header height, sidebar width. Literals deleted in favour of the variable.
- **AM-02 / DV-17 — header.** Header height **58 → 60** (`--header-h`); sidebar **248 → 244** (`--sidebar-w`),
  resolving the old "58-vs-60 self-inconsistency" comment per Usage Map decision 11. Header stays solid
  `--header` (DV-17 already resolved — no-op).
- **LP-04 — scrollbar.** Added the one custom scrollbar (11px, `--scroll` thumb, 7px radius, 3px transparent
  border + Firefox `scrollbar-width:thin`). Scroll model already correct (document scrolls, sticky chrome,
  no inner overflow) — confirmed, no change.
- **NV-07 / LP-01 — breadcrumb LIFTED into the shell.** New `nav/breadcrumbs.ts` derives the trail from the
  URL (`Home › Area › Record › Sub-tab`; record key shown verbatim + mono, like the design), rendered once in
  `AppShell` inside a `.shell-crumbs` width cap. **Removed the 7 per-page `<Breadcrumb>` renders.** Every page
  now has a consistent breadcrumb incl. **dashboard, placeholders, and 404** (which had none) with the 12px
  gap owned globally on `.breadcrumb`.
- **NV-01..06 + §G routes (Option 1).** navModel + App.tsx routes aligned to Usage Map §G: `audit`
  `/admin/audit`→`/audit` (System group); `submit` `/topics/new`→`/backlog/submit`; `wiki`
  `/knowledge`→`/wiki`; `admin` `/admin`→`/admin/users` (index redirects); **Home now `/`** (kept `/dashboard`
  as a redirect alias so deep links survive). SideNav exact-match updated `/dashboard`→`/`. Governance
  group + "ADRs & Invariants" already host invariants (detail screens = P10). NV-06: nav reconciled now;
  Diagrams placeholder shows a designed **"Phase 2"** state (new `phase2` prop), the rest keep the honest
  placeholder until their phase (P7–P12) per Usage Map §C.
- **NR-09 — callback / 404 / error reconciled to `ACMP System States`.** 404 = mono 64px faint "404" +
  "Page not found" + design body + Go-to-dashboard/Search. Error boundary = danger icon box + "Something
  went wrong" + body + Reload/Go-to-dashboard. Callback = secure-handoff spinner + "Signing you in…" + secure
  line. New i18n keys (EN+AR): `common.{breadcrumb,reload,phase2*}`, `auth.{completingBody,secureHandoff}`,
  `notFound.title`, `errorBoundary.{title,body}`.

**Verification.** `npm run build` (tsc -b + vite) clean · `npm run lint` (only the pre-existing Toast
warning) · `node scripts/check-i18n.mjs` **OK (511 keys)** · `npm run test:cov` **350 passed** (49 files;
+4: new `breadcrumbs.test.ts` ×6, PlaceholderPage phase2, ErrorBoundary/App/AuthCallback updated to the new
behaviour) · **per-file coverage gate green** (changed files 100% lines: breadcrumbs.ts, AppShell, SideNav,
NotFoundPage). **Live visual (vite dev, 1440×900):** dashboard now shows the **Home breadcrumb** (was
absent); 404 matches System States; **AR-RTL-dark** mirrors the sidebar to the inline-end with the breadcrumb
+ dark theme + Arabic labels. Frontend-only — no API/backend change.

**Acceptance.** Touches **AC-040/045/046** (EN/AR render, focus/labels, axe — unit + axe still green) and
**AC-041** (stays **Partial** → automated pixel-diff VR is P17). No verdict flips.

**Next.** Round-2 reconcile of P4–P6 surfaces (meetings ownership split RD-08, actual-time control DV-16,
rich-text unification DV-04) per the same Usage Map decisions.

---

## Doc-Integrity Slice — resolve rebuild-findings §8 (DI-01..DI-08 + guardrail #14)

### 2026-06-30 — Doc/code integrity fixes (one PR, branch `chore/doc-integrity` off `main`)

**Why.** The forensic rebuild review (`rebuild-findings.md` §8) flagged eight doc-integrity defects:
an ADR number collision, OQ numbering gaps, a doc-vs-doc search-engine contradiction, a ledger mis-record,
a doc-vs-doc SPA-serving disagreement, and a doc-vs-code drift (the documented RowVersion backstop didn't
exist). Each is evidence-backed; fixed together so the planning package stops contradicting itself and the code.

**What.**
- **DI-01 (BLOCKER) — ADR-0015 collision.** Two files both claimed ADR-0015 (React-19 amendment + Self-Hosted
  Keycloak). `git mv` renamed the React-19 ADR to **ADR-0017** (title + administrative renumber note; decision
  date kept 2026-06-25). `docs/adrs/README.md` index repaired — it had stopped at 0015, so **both ADR-0016 (coverage)
  and ADR-0017 (React-19)** were added (plus ADR-0018, below); ADR-0012's row + Links/Notes now point to
  ADR-0017. ADR-0015 stays = Keycloak, 0016 = coverage. Numbers never reused.
- **DI-04/05 — OQ numbering.** Added the missing **OQ-041** (prod CI runner, P18) to `docs/decisions/open-decision-register.md` and re-sequenced
  the Keycloak section to 038→039→040→041→042→043; OQ-041 added to the Deferred blocker list.
- **DI-08 — OQ-034 search engine.** Corrected Meilisearch → **OpenSearch** (matches ADR-0011 / R-24 / README).
- **DI-06 — ledger StatusChip.** `design-parity-ledger.md:41` corrected to DS §08 = 24/9/12 (md), §09 =
  22/8/11.5 (sm); the dc showed 23 (code tiebreaker).
- **DI-07 — OQ-012 SPA serving.** Noted the chosen **separate nginx `web` container** override of the doc's
  default (b) ASP.NET static files.
- **Guardrail #14.** Appended the USAGE MAP rule (`ACMP Usage Map.dc.html` is the per-phase/flow/component
  index; blessed deviations update the design, leaving no unreconciled drift).
- **DI-02 / OQ-043 — RowVersion (operator chose option A: implement).** Added `RowVersion` (SQL `rowversion`,
  `IsRowVersion()`) to the built mutable aggregate roots **Topic, Meeting, Agenda, CommitteeMember**; one EF
  migration per module (Topics/Meetings/Membership); `GlobalExceptionHandler` maps
  `DbUpdateConcurrencyException → 409` (docs/domain/architecture-detail.md §7.4). Recorded as **ADR-0018**; resolved OQ-043 → R-27 in
  `docs/decisions/open-decision-register.md` §A. `docs/domain/data-architecture.md` §1.5 is now true in code. Append-only/child entities excluded (guardrail #12).

**Tests.** Integration (Testcontainers, real SQL): a stale write throws `DbUpdateConcurrencyException` (SQL
rejects; InMemory silently accepts — the false-green contrast). Unit: `GlobalExceptionHandlerTests` pins the
exception→status mapping incl. the new 409 arm. Full backend suite green (579 tests); coverage gate
**99.59% global, all files ≥95%**. `dotnet format --verify-no-changes` clean.

**Follow-up (flagged, not in scope).** A 409 should eventually drive an SPA optimistic-conflict UX
(refetch/merge) — a later front-end slice (noted in ADR-0018). CLAUDE.md / guardrail #1 still say "React 18"
(pre-existing; ADR-0017/0012 govern) — separate cleanup, not a DI-### item.

---

## Test-Hardening Program — S7: flip the CI coverage gate to fail-under-95 (ADR-0016 §1)

### 2026-06-30 — Wire the hard coverage gate (global + per-file ≥95% lines, FE + BE) — the last slice

**Why.** The whole program kept `main` green while climbing to ≥95% (guardrail #13), so the hard fail-under
gate is wired only now that both stacks are already there. ADR-0016 §1: ≥95% **lines** on assertable code,
enforced **global + per-file** so a 0% file can't hide behind the average.

**What (S7, final slice).**
- **FE** — `vitest.config.ts`: added `coverage.thresholds: { lines: 95, perFile: true }` (lines only — the
  basis is lines, not functions/branches). CI `frontend` job's test step now runs **`npm run test:cov`** so
  the thresholds are evaluated; the job fails if any file drops below 95% lines.
- **BE** — coverlet's own threshold is per-assembly only, so per-file needs a custom check.
  `scripts/check-coverage.mjs` unions line-hits across every per-project cobertura report (a line counts as
  covered if **any** test project hit it — the true merged coverage, no ReportGenerator needed) and fails if
  any file or the global figure is <95% lines. The coverlet.runsettings exclusions are already applied at
  collection time, so every file it sees is in-scope. CI `backend` job now collects coverage
  (`--collect:"XPlat Code Coverage" --settings coverlet.runsettings`) and runs the gate (`+ setup-node`).

**Proven both ways (locally).** Gate at 95 → **pass** (FE: all files ≥95% lines, EXIT 0; BE: 113 files,
global **99.65%**, zero sub-95 files). Fail-path at 100 → **trips** (BE lists `Topic.cs` 96.47% and
`Agenda.cs` 98.80%, exit 1) — the script's threshold is a CLI arg (default 95), so the deliberate-red proof
is deterministic and repeatable, not a throwaway edit.

**Gates.** FE `npm run build` clean · `npm test` 346/346 · `npm run lint` (only the pre-existing Toast.tsx
warning) · `npm run test:cov` green with the new threshold. BE coverage 99.65% (every file ≥95%) via the new
gate. **This completes the ADR-0016 test-hardening program (S0–S7).** Optional follow-ups remain: the
end-of-S6b AC-verdict reconciliation, and OQ-043 (RowVersion optimistic concurrency) as its own feature
slice.

---

## Test-Hardening Program — S6b-3: RTL/Arabic + accessibility E2E (ADR-0016 §2)

### 2026-06-30 — Prove the live RTL flip and an automated axe sweep (last E2E slice)

**Why.** The mandate requires an RTL/Arabic + accessibility pass on key screens. Unit axe tests (S4)
covered components in jsdom; S6b-3 proves the **whole live page** flips to `dir="rtl"` under Arabic and is
axe-clean in both locales — the last E2E slice before S7.

**What (S6b-3, last of three small per-flow PRs).** `e2e/rtl-a11y.spec.ts` (3 tests):
- The app flips to **RTL Arabic** from the top-bar control: `<html>` goes `dir="ltr"` → `dir="rtl"`,
  `lang="ar"`, and the toggle then offers the way back to English (i18n really switched).
- **Backlog** is **axe-clean** (0 WCAG 2a/2aa violations) in **both English and Arabic/RTL**.
- **Submit-Topic** is **axe-clean** in **both English and Arabic/RTL** (form-heavy a11y surface).

**No new dependency.** Uses the already-installed `axe-core`. The app ships a strict CSP
(`script-src 'self'`), which **blocks** `page.addScriptTag` (inline injection) — a good sign for the app —
so the axe source is run via `page.evaluate(AXE_SOURCE)`, which executes through CDP and bypasses the page
CSP. `color-contrast` is disabled in the axe run to match the S4 unit convention (contrast = a
design-token/fidelity concern, out of this slice's scope; flagged, not silently dropped).

**Gates.** FE `npm run build` clean · `npm test` 346/346 (vitest, e2e excluded) · `npm run lint` (only the
pre-existing Toast.tsx warning). Live: **full e2e suite 13/13 green on a freshly reset stack** (auth ×2 +
core-loop + 7 S6b-2 + 3 S6b-3) — the exact CI shape. **The E2E mandate (ADR-0016 §2) is now complete.**
**Next: S7** — flip the CI coverage gate to fail-under-95 (per-file, both stacks); and the end-of-S6b AC
verdict reconciliation now that the live HTTP/UI legs exist.

---

## Test-Hardening Program — S6b-2: native drag paths + failure-first E2E (ADR-0016 §2)

### 2026-06-30 — Cover the S4-deferred drag paths and the adversarial denial/validation cases

**Why.** S4 marked the pointer-drag paths `/* v8 ignore */` ("deferred to S6 E2E") because jsdom can't run
native HTML5 drag. S6b-2 proves them on the real browser, and adds the failure-first cases the mandate
names (authz denial + validation guards) that only the live stack can exercise honestly.

**What (S6b-2, second of three small per-flow PRs).** `e2e/dnd-and-failures.spec.ts` (7 tests) + setup
helpers in `e2e/scenario.ts`. Setup that isn't under test (prepared topics, meetings, agenda items) is built
through the **API** with a real captured bearer; the UI is reserved for the drag/denial being asserted.
- **Drag (the S4-deferred paths):** Kanban card → Accepted column (opens the AcceptDialog); Kanban card →
  Scheduled column (the illegal move → announced rejection in the `aria-live` region, no dialog);
  AgendaBuilder pool card → agenda region (adds the item); AgendaBuilder item-2 → item-1 (single ±1 reorder).
- **Failure-first:** a member is denied scheduling (`Policies.MeetingSchedule` → **403** + the UI error);
  the schedule form blocks empty submits (required-field errors, no request) and an inverted time window;
  publish is **disabled** on an empty agenda and Start is **gated** until the agenda is published.

**Decision A realised.** The operator's "sortable reorder" target maps onto the AgendaBuilder native
handlers + Kanban card→column — `@dnd-kit SortableList` is mounted by no screen (only its own unit test), so
it isn't E2E-able and is intentionally not covered here.

**Drag mechanism (spiked).** The app's drag handlers store state in React refs/state on `dragstart` and read
it on `drop` (no `dataTransfer` payload), so `e2e/scenario.ts → dragHtml5()` dispatches the DnD events
directly (`dragstart`/`dragover`/`drop`/`dragend`) — more deterministic than geometry-based mouse
simulation, and it fired every handler on the first run. (Centralised so it's a one-line swap to
`locator.dragTo()` if ever needed.)

**Gotcha (carry forward).** JIT member provisioning (`POST /members/me`) is async on login, so the very
first API read of `/members` after a login can race it — force it with an explicit idempotent
`page.request.post('/api/members/me')` before querying the directory (added in `secretarySession`). Caught
only because the *first* secretary login in a fresh-stack run failed while later ones passed.

**Gates.** FE `npm run build` clean · `npm test` 346/346 (vitest, e2e excluded) · `npm run lint` (only the
pre-existing Toast.tsx warning). Live: **full e2e suite 10/10 green on a freshly reset stack** (auth ×2 +
core-loop + 7 S6b-2) — the exact CI shape. **AC verdict flips still deferred** to the end of S6b. **Next:
S6b-3** (RTL/Arabic + axe on a key screen), then S7 (flip the CI coverage gate to fail-under-95).

---

## Test-Hardening Program — S6b-1: core-loop E2E (topic → agenda → meeting → conduct → notify) (ADR-0016 §2)

### 2026-06-30 — Drive the whole governance loop through the real UI on the live stack

**Why.** ADR-0016 §2 names the core loop (topic → agenda → meeting → minutes → notify) as the headline
E2E. S6a proved only the auth round-trip; S6b-1 proves the product spine end-to-end against the real
8-service compose stack — the live HTTP+UI leg the InMemory/unit suites can never exercise.

**What (S6b-1, first of three small per-flow PRs).**
- `e2e/core-loop.spec.ts` — one failure-first spine: secretary submits a topic → accepts it (Kanban,
  keyboard "M" move popover, owner = the member) → **[API] prepare** → schedules a meeting → builds the
  agenda (add + assign presenter) → **publishes (= the notify fan-out)** → starts → conducts (marks
  attendance + captures a discussion note) → ends → asserts the **minutes placeholder gate** → verifies the
  notification bell for **both** recipients (member + chairman), satisfying the "≥2 members" mandate.
- `e2e/apiHelpers.ts` — the two steps with no v1 UI: `captureBearer` (reads the `Authorization` header off a
  live `/api` request — Keycloak direct-grant is off, so the PKCE session is the only token source) and
  `prepareTopic` (Topic→Prepared is API-only; the secretary's bearer satisfies `Policies.TopicEdit`).

**Honest reconciliations baked into the spec (the loop names screens that don't all ship in v1).**
- **Minutes is a placeholder** (MoM = P7): the spec asserts the Minutes *gate* ("Minutes arrive in a later
  phase"), never a faked minutes screen.
- **Notify = the publish-agenda fan-out** to all committee members; both recipients' bells are checked.
- **No DB seeder** → the stack boots empty. Members exist only after a login self-provisions them (an
  *active* `CommitteeMember`), so the two recipients log in once (own contexts) **before** the fan-out.

**What the live leg caught that unit tests didn't.** Publishing 500'd with the domain invariant *"Every
agenda item needs a presenter before publishing"* (`Agenda.Publish`). The unit suite asserts that guard in
isolation; only the live UI proves the **builder lets you satisfy it** before publish. The spec now assigns
a presenter and asserts the publish response is 200 — exactly the false-green gap §2 targets.

**Gotchas (carry forward).** (1) `getByLabel('Title', {exact})` fails on `Field`-wrapped inputs — the
`<label>` text is `"Title*"` (required `*` is `aria-hidden` from the *name* but present in label *text*); use
`getByRole('textbox', {name, exact})` (accessible name excludes the `*`). (2) Modals leave sibling chrome in
the DOM — scope dialog actions with `page.getByRole('dialog').getBy…` (the Backlog "Owner" filter chip
collided with the AcceptDialog owner Select). (3) The local stack is fast: the full 3-login loop runs in
~4s, so genuine passes look "too fast" — confirm via `docker logs acmp-api-1` (Publish/Start/Mark/Capture/
End commands) rather than wall-clock. (4) Run from `src/Acmp.Web`; `set -a; source ../../deploy/.env.example`
before `npm run e2e` so global-setup gets the KC admin creds.

**Gates.** FE `npm run build` clean · `npm test` 346/346 (vitest, e2e excluded) · `npm run lint` (only the
pre-existing Toast.tsx warning). Live: full e2e suite **3/3 green** (auth ×2 + core-loop) against the booted
stack. **AC verdict flips deferred** to the end of S6b (after the drag + RTL/axe slices land), consistent
with S5/S6a ("no AC verdicts flipped" mid-slice). **Next: S6b-2** (native HTML5 drag — AgendaBuilder
pool→agenda + within-agenda reorder, Kanban card→column — + failure-first authz/validation).

---

## Test-Hardening Program — S6a: E2E harness + real Keycloak PKCE auth round-trip (ADR-0016 §2)

### 2026-06-30 — Stand up Playwright against the real compose stack; prove the genuine auth round-trip

**Why.** ADR-0016 §2 requires E2E `@playwright/test` driving the **real** stack (web :8088 / api :8080 /
Keycloak :8085) through the **genuine authorization-code + PKCE round-trip** — the one flow the unit and
InMemory suites can never exercise. Its first task is to **verify the SPA auth-seed mechanism before
writing specs**.

**Auth-seed finding (the §2 first task).** The shipped realm (`deploy/keycloak/realm-export.json`) ships a
single user `acmp-admin` with `requiredActions: ["UPDATE_PASSWORD"]`, and the `acmp-web` client has
`directAccessGrantsEnabled: false`. So it **cannot** drive an unattended E2E login (forced password screen;
no password-grant shortcut). Resolution: seed **deterministic per-role test users via the Keycloak admin
REST API at global-setup** (fixed password, no required actions) — the **prod realm export is never
touched**, so no fixed-password accounts ship.

**What (S6a — the verifiable spine).** New Playwright harness under `src/Acmp.Web/`:
- `playwright.config.ts` (baseURL :8088; does NOT own the 7-container lifecycle — CI / `e2e:up` does).
- `e2e/global-setup.ts` — waits for realm + SPA health, then seeds `e2e-secretary/-chairman/-member`
  (admin API, idempotent). `e2e/users.ts`, `e2e/login.ts` (drives the real Keycloak form).
- `e2e/auth.spec.ts` — (1) unauthenticated deep-link → `/login`; (2) full PKCE round-trip → authenticated
  dashboard.
- `.github/workflows/e2e.yml` — separate workflow, `on: pull_request→main + workflow_dispatch` (ADR §2;
  off the per-branch hot path), brings the stack up, seeds, runs, uploads traces, tears down.
- `package.json`: `@playwright/test` + `e2e` / `e2e:up` / `e2e:down` scripts.
- `vitest.config.ts`: added `include: ['src/**/*.{test,spec}.{ts,tsx}']` so vitest never collects the
  Playwright specs in `e2e/` (they import `@playwright/test`).

**Result (validated locally against the live stack).** `docker compose up --wait` → all 8 services healthy;
global-setup seeded the 3 users; **both auth specs pass** (deep-link redirect + real PKCE sign-in to the
dashboard). Cheap gates also green: `npm run build` clean, `npm test` **346 unit tests pass** (e2e excluded),
Playwright discovers the specs. **GOTCHA (carry forward):** the compose `kcdata`/`mssql-data` volumes pin
Postgres/SQL passwords at first-init — a stale volume from a prior run fails keycloak/SA auth; **`down -v`
before `up`** to re-init with the current `.env.example` creds.

**Scope note (operator-confirmed "focused spine").** S6a is the harness + auth round-trip. The remaining
mandate specs — **core loop** (topic→agenda→meeting→minutes→notify), the **S4-deferred drag paths**
(@dnd-kit + native HTML5), and an **RTL/axe** pass — are **S6b**: each needs reading + live-iterating its
feature UI, so they ship as a focused follow-on rather than blind specs in this PR.

**Next.** S6b — core-loop + drag + RTL/a11y specs on the same harness; then S7 flips CI coverage to
fail-under-95 per-file, both stacks.

---

## Test-Hardening Program — S5: Testcontainers SQL-Server DB-backstop suite (ADR-0016 §3)

### 2026-06-29 — Prove the database-enforced invariants the EF InMemory provider silently accepts

**Why.** The whole unit suite runs on EF Core InMemory, which is a `Dictionary` — it ignores unique
indexes, FK behaviour and `WHERE`-filtered indexes entirely. So every `.IsUnique()`/FK backstop in the EF
configuration was, until now, **unverified**: a duplicate write passes green in the unit suite and would
500 in production. S5 closes that false-green gap on a real SQL Server (ADR-0016 §3).

**What.** New test project `tests/Acmp.Integration.Tests` (added to `acmp.sln`, operator-confirmed), one
shared `MsSqlContainer` collection-fixture (`Testcontainers.MsSql` 3.10.0) that applies all four module
contexts' migrations into their schemas — which **is** the "migrations apply cleanly" proof. 11 failure-
first tests, each contrasting SQL-rejects against InMemory-accepts where that contrast is the point:
- **Top-level unique (with InMemory twin):** duplicate `Meeting.Key` — SQL throws `DbUpdateException`,
  InMemory inserts both (the headline false-green proof, ADR-0016 validation line).
- **Composite owned-collection indexes** `(MeetingEntityId,UserId)` attendance and `(AgendaEntityId,TopicId)`
  agenda-item — the aggregate dedupes in memory AND EF eagerly loads owned collections, so a duplicate can
  only arrive via a **non-aggregate write** (repair script / bulk insert). Proven with a raw `INSERT … SELECT`
  that copies the seeded row with a fresh `PublicId` → SQL rejects the composite collision.
- **Business-rule unique:** second `Agenda` for one meeting (unique `Agenda.MeetingId`) → rejected.
- **Identity unique:** duplicate `CommitteeMember.KeycloakUserId` → rejected.
- **Filtered unique** `Email WHERE [Email] <> ''`: two **real** duplicate emails rejected, but two
  **empty-email** members allowed (the bootstrap-admin case) — a SQL-only filter InMemory can't model.
- **FK `OnDelete(Restrict)` (with InMemory twin):** deleting a member referenced by a `Delegation` — SQL
  blocks it, InMemory orphans the row and succeeds.

**Decisions / findings.**
- **In-solution (operator GO):** the suite lives in `acmp.sln`, so it runs in the existing `backend` CI job
  (ubuntu-latest has Docker) — no new workflow. Local `dotnet test acmp.sln` now needs Docker running.
- **Owned collections can't use a "load-without-Include" trick** — EF eagerly materializes `OwnsMany`
  children with the owner, so the aggregate dedupe always fires; raw INSERT is the honest backstop path.
- **DRIFT → OQ-043 (new):** `docs/domain/data-architecture.md` says every mutable aggregate root carries `RowVersion ROWVERSION`
  for optimistic concurrency (→ 409), but **no code has any `IsRowVersion`/`[Timestamp]`/concurrency token**
  (zero matches across all `*.cs`). S5 cannot prove a backstop that does not exist; adding it is a
  feature + migration, not a test slice. Flagged, not silently added (guardrail #11).

**Result.** `dotnet build acmp.sln -c Release` 0 errors · `dotnet format --verify-no-changes` clean ·
`dotnet test acmp.sln` **570 passed, 0 failed** (Domain 84, Architecture 16, Application 419, **Integration
11 new**, Api 40). The duplicate-`Meeting.Key` insert goes **red on SQL, green on InMemory** — the contrast
ADR-0016 §3 requires.

**Next.** S6 — E2E `@playwright/test` against the real compose stack incl. Keycloak PKCE (covers the
S4-deferred drag paths); then S7 flips CI coverage to fail-under-95 per-file, both stacks.

---

## Test-Hardening Program — S4: frontend screen-state cleanup (ADR-0016)

### 2026-06-29 — Close the FE per-file gap to make every frontend file ≥95%

**Why.** S2 took the FE auth/data surface to 100% but global FE sat at 94.83% with ~13 screen-state files
at 8–94%. S4 closes them so every FE file clears the per-file ≥95% gate S7 will enforce (operator-confirmed
"all 13").

**What.** 121 new tests (225 → 346), no product behaviour changed except 4 documented coverage-ignore
**comments** (no logic touched). New focused test files + targeted extensions:
- **UI primitives** (`components/ui/coverage.test.tsx`): Pagination (paging/disabled/aria-current), Select
  (ArrowDown/Home/End/Enter keyboard + Escape), Dialog (Tab/Shift+Tab focus-trap + Escape), Field (help
  branch), DateField (open/Escape), MultiSelect (empty-filter + Escape) → all 100%.
- **Components/pages**: `ErrorBoundary.test.tsx` (catch→safe fallback, no leak, retry-recovery),
  `PlaceholderPage.test.tsx` (localized title + axe), `meetingStatus.test.ts` (every tone/section arm),
  `NotificationCenter.coverage.test.tsx` (loading/error/see-all).
- **Feature extensions**: SubmitTopic (attachment add/remove, token backspace, save-draft, section nav,
  submit-error, autosave, beforeunload, corrupt-draft, KB/MB formatting) → 84.2% → 95.5%; AgendaBuilder
  91.6% → 95.2%.

**The one deviation — 4 documented `/* v8 ignore */` comments** (comment-only, zero behaviour change), each
for a genuinely browser-only path that jsdom cannot exercise, with the accessible/fallback path unit-tested
and the drag path deferred to the S6 Playwright E2E:
- `SortableList` `onDragEnd` (@dnd-kit pointer drag) — Move up/down buttons are unit-tested.
- `AgendaBuilder` native HTML5 drag handlers (agenda item + pool card) — click-to-add is unit-tested.
- `AgendaBuilder` `AgendaPreview` empty branch — defensive/unreachable (a locked agenda always has ≥1 item).
- `SubmitTopic` `saveDraftAndLeave` storage-failure catch — defensive (setItem throws only when storage is
  disabled/full).
(The IntersectionObserver scroll-spy effect is left naturally uncovered — jsdom has no IO and the effect
self-guards — and the file still clears ≥95% without it.)

**Result.** `npm run build` (tsc -b + vite) clean · `npm run lint` (oxlint) **0 errors** ·
`node scripts/check-i18n.mjs` **OK (501 keys — no new strings)** · `npm run test:cov` **346 passed, 0
failed**. **FE line coverage 94.83% → 98.46%; every frontend file is now ≥95%** (verified via the v8
json-summary). Both stacks are now per-file-gate-ready (BE 99.6%, FE 98.46%).

**Next.** S5 — Testcontainers SQL-Server DB-backstop suite (then S6 E2E @playwright/test, S7 flip the gate).

---

## Test-Hardening Program — S3: backend Api endpoints + per-file BE sweep (ADR-0016)

### 2026-06-29 — Close the last backend gaps to make every file per-file-gate ready

**Why.** S1 took the Application layer to 97.6%, but S7 wires **per-file** ≥95% thresholds and no other
backend slice was planned. So S3's literal scope (the Api endpoints) was widened (operator-confirmed:
"B, one PR") to **finish the whole backend to per-file ≥95%** — Api endpoints **plus** the ~14
domain/application/shared files still 50–94%. All were reachable code lacking tests; **zero new
exclusions** were needed.

**What.** 101 failure-first tests (458 → 559), no product behaviour changed. Authored largely by four
parallel sub-agents into new, non-overlapping files; integrated + fixed centrally.
- **Api endpoints (HTTP round-trips, `Acmp.Api.Tests`):** `TopicEndpointsCoverageTests` (defer / priority
  PUT / update PUT, incl. a 400 and a 403) and `MeetingsEndpointsCoverageTests` (agenda move/timebox/
  presenter on a Draft agenda; the conduct lifecycle schedule→publish→start→attendance→discussion→
  actual-time→end; cancel + a 403). The uncovered lines were request-body records for endpoints no test
  hit — Api assembly **94.7% → 100%** (all four endpoint files 100%).
- **Topics domain:** `TopicLifecycleTests` (Close/Convert + their events, immutability guards, Reopen),
  `TopicChildEntityTests` (TopicComment/TopicAttachment metadata).
- **Membership:** `MemberStreamAndDelegationTests` (MemberStreamAssignment, Delegation.IsActiveAt
  boundaries, CommitteeMember reactivate/sync), `AssignStreamsValidatorTests` (0% → 100%).
- **Application + shared sweep:** `GetBacklogCoverageTests` (every filter + sort + paging branch),
  `TopicDetailCommentMappingTests` (TopicCommentDto via detail), `NotificationCoverageTests`,
  `CurrentUserServiceTests`, `SharedKernelTests` (BaseEntity domain-events, LocalizedString),
  `TopicSchedulerAndPersistenceTests` (the cross-module `ITopicScheduler` idempotent no-op **and** success
  paths, + a TopicAttachment EF round-trip through a fresh context to materialise its private ctor).

**Notes.** `TopicScheduler` success path needed a *direct* unit test — the HTTP agenda tests use synthetic
topicIds with no matching Topic, so the seam short-circuited (`Actor()` never ran). The private EF
constructors of `TopicAttachment`/`MemberStreamAssignment` are only reachable via a real save-then-reload
in a fresh `DbContext`; covered that way rather than excluding. Two sub-agents’ in-process state was lost
when a process exited mid-run; their files were already on disk except `MeetingsEndpointsCoverageTests`,
which was authored directly.

**Result.** `dotnet build` clean · `dotnet test` **559 passed, 0 failed** · `dotnet format
--verify-no-changes` clean. **BE line coverage 97.6% → 99.6%; every backend file is now ≥95%** (the
per-file gate S7 will enforce). No AC verdicts flip (G-TRACE: live HTTP/UI legs → P17); the Api endpoint
tests deepen evidence for the Topics/Meetings workflow ACs over the real pipeline.

**Next.** S4 — FE screen-state cleanup to global ≥95% (then S5 Testcontainers, S6 E2E, S7 flip the gate).

---

## Test-Hardening Program — S2: frontend auth + data layer (ADR-0016)

### 2026-06-29 — Failure-first coverage of the OIDC wiring, route guards, API client, and TanStack Query hooks

**Why.** S2 begins the frontend climb to ≥95% by hardening the auth/data surface that was at ~0–60%.
Key fact driving the approach: the existing screen tests **mock the API hooks away**
(`vi.mock('../../api/...')`), so the real auth providers and the real query/mutation hooks had never
executed in any test. S2 flips the boundary — it runs the **real** code against a stubbed `fetch` —
so URL building, request bodies, retry rules, cache invalidation, claim→role mapping, and the route
guards are actually asserted. Test the **bad before the good**: 401/4xx surfacing, fail-closed auth,
no-retry on 4xx, role-gate denial, storage-unavailable, provision-retry-on-failure.

**What.** 88 new tests (no product behaviour changed), in 12 files, matching the existing
vitest + Testing-Library style. New harness `src/test/queryHarness.tsx` (coverage-excluded):
`makeQueryWrapper` (fresh QueryClient, retries off) + `stubFetch`.
- **Data layer** — `apiClient.test.ts` (bearer token, Accept-Language, 204→undefined, RFC-7807→typed
  `ApiError`, non-JSON-error fallback, header merge), `queryClient.test.ts` (retry predicate: 4xx no-retry,
  5xx/network one retry), `topics.test.ts` (`toQuery` repeated-status + all filters + empties omitted,
  `enabled` gate, mutation URL/body/invalidation, multipart upload with no JSON Content-Type),
  `meetings.test.ts` (all 15 hooks — agenda vs live-meeting invalidation scopes, failure surfacing),
  `notifications.test.ts` (recent vs infinite, `getNextPageParam` hasMore true/false, mark-read/all),
  `members.test.ts`.
- **Auth + routing** — `AuthProvider.test.tsx` (fail-closed in prod, DEV stub + role switch, OidcBridge
  claims→roles, provision POST `/members/me` once, retry-on-failure guard reset, expiry→authStatus,
  sign-out), `authConfig.test.ts` (oidcEnabled true/false, auth-code+PKCE no secret, scope default,
  URL strip), `authStatus.test.ts` (round-trip, clear-on-read, storage-throws caught),
  `ProtectedRoute.test.tsx` (loading/error/unauth-redirect, extended), `App.test.tsx` (route tree:
  member sees protected route in shell, admin role gate denied/allowed, 404),
  `AuthCallbackPage.test.tsx` (loading/error/onward routing), `LoginPage.test.tsx` (signed-out/expired
  status banners, extended).

**Notes / honest scope.** (1) **No optimistic-update logic exists** in the `api/` layer (zero `onMutate`),
so "optimistic rollback" tests would assert nothing — deliberately not written. (2) The unauthenticated
→ `/login` redirect is asserted in `ProtectedRoute.test.tsx` (declarative `<Routes>`, where `<Navigate>`
works); driving it through a data router in `App.test.tsx` hits a jsdom+undici `AbortSignal` brand-check
bug on client-side navigation, so the App-level test sticks to non-redirecting outcomes. (3) Fixed a
false-green in the first draft: storage-throw tests must swap the global `sessionStorage` object — spying
on `Storage.prototype` does not intercept the test environment's storage.

**Result.** `npm run build` (tsc -b + vite) clean · `npm run lint` (oxlint) clean (one pre-existing
Toast warning only) · `node scripts/check-i18n.mjs` **OK (501 keys — no new strings)** · `npm run test:cov`
**313 passed, 0 failed** (225 → 313). **FE line coverage 83.74% → 94.83%.** The S2 surface is at
**100% lines** every file: `App.tsx`, all `api/*`, all `auth/*`, `LoginPage`, `AuthCallbackPage`
(plus `Dashboard`/`Administration`/`NotFound` covered incidentally by the route-tree test). Global 94.83%
is below 95% by design — the screen-state remainder (`components/ui`, `PlaceholderPage`, `ErrorBoundary`,
`AppShell`, `Card`, etc.) is **S4**.

**Next.** S3 — backend Api endpoints (then S4 closes FE screen-state to global ≥95%).

---

## Test-Hardening Program — S1: backend adversarial invariants (ADR-0016)

### 2026-06-29 — Failure-first coverage of the 0%-covered Application handlers + validators

**Why.** S0 stood up the coverage basis; S1 begins the climb to ≥95% BE by hardening the highest-risk,
lowest-covered surface: the Application-layer governance handlers and validators that were at 0%. Test the
**bad before the good** — authz-deny, 404, domain status/immutability guards, audit emission.

**What.** 40 adversarial tests (no business behaviour changed), matching the existing InMemory
`AcmpWebApplicationFactory` fixture style:
- **Topics handlers** (`TopicHandlerTests.cs`): Update / Defer / Prepare / Prioritize / Reject — each with
  404, per-resource authz-deny (real `IResourceAuthorizer`), domain status/immutability guard, and
  `AuditEvent` assertion. UpdateTopic covers all three branches (submitter edits content / non-submitter →
  `Topic.Edit` deny / post-Accept metadata-only under `Topic.Triage` with content locked).
- **Topics validators** (`TopicApplicationTests.cs`): Prioritize, Update, Prepare.
- **Meetings handlers** (`MeetingHandlerTests.cs`): EndMeeting (both 404 branches + `Hold`/`Close` guards +
  audit), CancelMeeting (404 + wrong-status + blank-reason guard + audit), RemoveAgendaItem (404 +
  unknown-item + locked-agenda + success/renumber). The role-gate on these commands is the MediatR
  `AuthorizationBehavior` (`IAuthorizedRequest`), deliberately bypassed by direct-handler construction —
  so authz-deny is the pipeline's test, **not** faked here; the handler layer asserts 404 + domain guards +
  audit.
- **Meetings validators** (new `MeetingValidatorTests.cs`): AssignPresenter, CaptureDiscussion,
  MarkAttendance, CancelMeeting.
- **Membership validator** (`MembershipFeatureTests.cs`): CreateDelegation (target, capability length,
  forward window).

**Coverage basis amendment (ADR-0016, operator-confirmed 2026-06-29).** Added `**/*DbContextFactory.cs`
(design-time `IDesignTimeDbContextFactory` — run only by `dotnet ef migrations`, never at runtime; same
un-assertable class as the already-excluded `Program.cs`) and `**/MinioFileStore.cs` (Phase-2 S3 adapter,
already earmarked for exclusion in §1) to the coverlet `ExcludeByFile`. Rationale: S7 wires **per-file**
thresholds — a 0% design-time factory would fail a per-file gate regardless of the global, and the only way
to "cover" it is a theatre test. Excluding is the honest call (60 design-time + 6 Phase-2 lines leave the
denominator).

**Result.** `dotnet build` clean · `dotnet test` **458 passed, 0 failed** · `dotnet format
--verify-no-changes` clean. **BE line coverage 89.1% → 97.6%** (1976/2024; Meetings.Application 100%,
Topics.Application 97.7%, Membership.Application 98.6%). Acceptance-audit AC→test mapping begins this slice
(AC-031/032/034/035/043 evidence deepened; no verdict flips — live HTTP/UI legs remain → P17 per G-TRACE).

**Next.** S2 — FE auth + data layer toward ≥95% frontend.

---

## Test-Hardening Program — S0: coverage tooling + basis (ADR-0016)

### 2026-06-29 — Establish coverage measurement, exclusion basis, and slice plan

**Why.** New standing mission: ≥95% line coverage on FE **and** BE, plus comprehensive adversarial
E2E. Before writing any test, stand up real coverage tooling, agree an honest basis with the operator,
and report measured baselines. No product behaviour changes in this slice.

**Measured baselines @ `b7ab531`.**
- **FE raw:** 82.94% lines (4270/5148, `all:true` + `include src/**` — honest denominator).
- **BE raw:** 39.1% across 14 assemblies — but anchored almost entirely by EF migrations + generated
  code + `Program`. Outside `*.Infrastructure` only ~436 lines were uncovered.
- **Verified fact (not assumed):** the adversarial invariants are CODE-enforced and run under the
  existing EF **InMemory** test stack — immutability is a domain guard (`Topic.cs:264`,
  `Agenda.cs:83/133` throw), IDOR is handler/LINQ (`CurrentActor` + `ICurrentUser`), audit + hash-chain
  are C#. Only DB-enforced backstops (`.IsUnique()` indexes, FK cascade, concurrency, migrations) need
  real SQL → Testcontainers side-suite, not the path to 95%.

**Decisions applied (ADR-0016, operator GO 2026-06-29).**
- Basis = ≥95% **lines** on assertable code, **global + per-file** thresholds.
- Hard-exclude (genuinely un-assertable plumbing only): BE = `**/Migrations/*.cs`, `[GeneratedCode]`/
  `[CompilerGenerated]`/`[ExcludeFromCodeCoverage]`/`[DebuggerNonUserCode]`, `[Acmp.Api]Program`
  (repo-root `coverlet.runsettings`). FE = `src/main.tsx`, `DevRoleSwitcher.tsx`, `src/test/**`,
  `*.d.ts` (`vitest.config.ts` `coverage`). **`App.tsx` is NOT excluded** (route/dirty-form guards
  must be tested). DI extensions NOT excluded (covered at boot).
- E2E = `@playwright/test` against the real compose stack incl. genuine Keycloak PKCE, on
  `pull_request`→`main` + `workflow_dispatch`. Testcontainers DB-backstop suite = included (full).
- CI fail-under-95 gate wired only in the final slice (S7) so `main` never goes red mid-climb.

**Changes.**
- `src/Acmp.Web/vitest.config.ts` — added `coverage` (v8, `all:true`, include/exclude per basis,
  text/json-summary/html reporters; thresholds deferred to S7).
- `src/Acmp.Web/package.json` — `test:cov` script.
- `coverlet.runsettings` (repo root) — BE exclusion basis.
- `docs/adrs/adr-0016-test-coverage-basis-and-e2e.md` — the decision record.

**Baselines after exclusions (the honest gap).**
- **FE: 83.74%** (225 tests, 32 files) → ~590 lines to 95% (data layer + auth the big levers).
- **BE: 89.1%** (1864/2090; 410 tests) → ~122 lines to 95%, concentrated in the 0% handlers
  (`UpdateTopic`, `Defer/Prepare/Prioritize/Reject`, `EndMeeting`, `CancelMeeting`) + a few validators
  + endpoint gaps.

**What's next.** S1 — BE adversarial invariants (the 0% handlers, failure-first: authz-denial,
validation, 404, IDOR, immutability guard, AuditEvent, status-transition guards). Acceptance-audit
entries begin at S1 (S0 is tooling, satisfies no AC). CI gate stays report-only until S7.

---

## P6 (PR-B) Create-meeting screen UI fixes (`ACMP Meetings.dc.html` isCreate)

### 2026-06-29 — Schedule form: fix field rhythm/alignment, full-width Mode, design date + time control

**Why.** Visual pass over the Create-meeting screen (`/meetings/new`) surfaced real UI defects:
the whole form was misaligned and unevenly spaced, and the date inputs were browser-native
`datetime-local` (rendered `mm/dd/yyyy` even under `dir="rtl"`).

**Root cause (the big one).** The global `.field + .field { margin-block-start: 16px }` *double-counted*
with the schedule card's flex `gap:16` — 32px between stacked fields — and, because each two-column
`.mt-schedule-row` is a grid, it pushed the **second field in every row 16px down** (Ends below Starts,
Mode below Type). Measured live: gaps `[32,16,16,16,32]`, rows offset by 16. Fixed with one scoped
rule (`.mt-schedule-card .field + .field { margin-block-start: 0 }`) → uniform 16 and top-aligned rows.

**Changes.**
- **Spacing/alignment** fixed (above). Verified live: gaps now `[16,16,16,16,16]`, both rows
  left.top == right.top.
- **Mode** segmented now fills its grid cell (`width:100%`, items `flex:1`) so it aligns with the
  Type select above it (was 242px floating in a 310px cell — design is `width:100%`).
- **Date & time** (operator GO "Match design"): replaced the two native `datetime-local` with the
  design's pattern — a new **`DateField`** (field-styled trigger + calendar icon that opens the
  existing shared `DatePicker` in a popover, mirroring `Select`'s open/backdrop/Escape) plus two
  native `<input type="time">` (start–end). The meeting is **single-day**: start & end share the
  picked date → ISO on submit. `DateField` derives month/weekday labels from Intl (Gregorian,
  localized, RTL-safe); native time inputs localize where `datetime-local` did not.
- i18n EN+AR: `meetings.schedule.{dateLabel,datePlaceholder,dateRequired,timeLabel,startTimeLabel,
  endTimeLabel}`; DatePicker nav reuses `meetings.calendar.{prevMonth,nextMonth}`.

**Verification.** Live computed-px gate: uniform 16 rhythm, rows aligned, Mode 310==cell 310, date
field height 38 == inputs. Screenshots EN + AR: form mirrors (Date on the inline-start side with its
icon, Time pair, Mode full-width), DatePicker popover opens both directions (today ringed, chevrons
mirrored). Web **225/225** (new: a `DateField` test + a date-required schedule test; SchedulePage
tests rewired to the date/time controls), i18n parity OK, tsc + vite build (JS 180 kB gz) + oxlint
clean. Frontend-only; same `ScheduleMeeting` payload (start/end ISO), no API change. **No verdict
flips** — UI fix; touches AC-040/041/045/046 (renders EN/AR, axe-clean, RTL).

**Next.** Merge, then PR-C+ test-hardening.

---

## P6 (PR-B) Meetings list redesign + calendar view (`ACMP Meetings.dc.html` isList)

### 2026-06-29 — Meetings list to design: Upcoming/Past split + List⇄Calendar toggle + month grid

**Why.** PR-B remaining item. The meetings list was a single flat table built when the design
package was thought to have no list screen. `ACMP Meetings.dc.html` **does** carry a full `isList`
screen (Upcoming/Past split, columns ID·When·Title·Type·Status, a List⇄Calendar view toggle, and a
month grid). So the old list was drift from a known reference (guardrail 14), not justified
scaffolding. Frontend-only — the backend already exposes `type`/`status`/`scheduledStart` on
`MeetingSummary`; no API change.

**What.**
- `MeetingsList.tsx` rebuilt to the design: a **List ⇄ Calendar** segmented toggle (shared
  `Segmented`), and in List view an **Upcoming / Past** split — two shared `Table`s (already a
  bordered card via `.table-wrap`) under uppercase section labels, columns
  **ID · When · Title · Type · Status · Agenda**. Head subtitle is now the live count
  (`upcoming upcoming · past past`).
- New `MeetingsCalendar.tsx` — the design's `listCalView` month grid: Intl month label + prev/next
  chevrons (RTL-mirrored), 7 weekday headers, day cells with status-toned event pills that link to
  the meeting. Computed over real `scheduledStart` (defaults to the current month; chevrons page
  months) — not the mock's static Feb-2026 dummy data.
- New `meetingStatus.ts` — shared `meetingTone` (list rows + calendar pills read the same colour)
  and `isConcluded` (the Upcoming/Past partition: status-based — Held/Closed/Cancelled = Past — so a
  mid-session or date-slipped meeting doesn't flip sections under the user).
- i18n `meetings.{view,section,calendar,listCount,col.type,captionUpcoming,captionPast}` in EN+AR.

**Decisions applied (operator GO: "Match design, keep agenda chip").**
- **Kept** an Agenda-status chip column the design omits — it carries the PR #31 agenda lifecycle the
  committee tracks from the list. Deliberate, operator-approved deviation (guardrail 14, reconciled).
- **Omitted** the mock's filter chips + "Saved views" — static decoration with no backend; not faked
  (same call as the agenda new-vs-link radio). Can be added client-side later over the loaded list.
- Rows link via the **title** (one focusable link per row) instead of the mock's whole-row `<button>`,
  which would be invalid markup inside a real `<table>` — behaviour/a11y-justified.

**Verification.** Computed-px gate (Playwright `getComputedStyle`) — every list literal (section
label 11/700/.4/uppercase, margins 6·8·2, toolbar mb14/gap9, section mb20) and every calendar literal
(card pad 18 / radius 12, head mb14, month 15/700, navbtn 34/8, grid gap 6, cell min-h64 / radius 8 /
pad 5·6, dow 11/700, event 9.5/600 / radius 4 / pad 2·5 / mt4) matches the `.dc.html` exactly.
Screenshots EN/AR desktop + AR tablet: anatomy matches, RTL mirrors (nav + chevrons flip to the
inline-start, weekday headers Sun→Sat reversed), no overflow at 768. Full web suite 223 green
(15 in the two meetings specs), i18n parity OK, oxlint clean, `npm run build` green (JS 180 kB gz).

**Acceptance.** No dedicated calendar AC — this is a new view over existing meeting data. It adds a
surface to the localization/a11y ACs (**AC-040/041/045/046**): both screens render in EN/AR and are
axe-clean (0 violations, both meetings specs), RTL confirmed live. No verdict flips.

**Next.** PR-C+ cross-phase test-hardening. Optional later: client-side list filters; a "today"
marker / >1-event-per-day affordance on the calendar if volume grows.

---

## P6 (PR-B) Notification Center — full page (`/notifications`, IA #79)

### 2026-06-28 — Full-page notification inbox: paging backend + page, mark-all, unread/all filter

**Why.** PR-B remaining item: the in-app notification center had only a bell popover; IA page #79
(`/notifications`) is the user's full inbox. No `.dc.html` exists for it → **no-reference composition**
(guardrail 14): design-system page chrome (breadcrumb + page-title) over the shell's notification row
anatomy (`.notif-*`, shared with the popover, kept DRY).

**Backend (Notifications module).**
- `GetNotificationsQuery` now takes `Page`/`PageSize` (clamped ≤ 50); `NotificationListDto` gains
  `Total` + `HasMore`; `UnreadCount` stays the full unread total (the badge), not just the page.
- New `MarkAllNotificationsReadCommand` (+ handler) flips all of the caller's unread and returns the
  count. Like `MarkRead`, read-status is personal inbox state, not a governance change → **no AuditEvent**
  (mirrors the existing handler).
- Endpoints: `GET /api/notifications?page&pageSize`, new `POST /api/notifications/read-all`.

**Frontend.**
- `api/notifications.ts`: `useInfiniteNotifications` (infinite query, server `hasMore`), `useMarkAllNotificationsRead`;
  the popover keeps `useNotifications` (recent page-1 of 8) + a new "See all" footer link → `/notifications`.
- New page `NotificationsPage` (`/notifications`): list (reused row anatomy in a bordered card),
  Unread/All segmented filter, Mark-all-read, "Load more" paging, and the loading/empty/all-read states.

**Decisions (operator GO).**
- **Mark-all-read = real backend command** (one call, one round-trip) — not a client loop.
- **Filter = client-side Unread/All toggle only** over loaded pages — no server filter (categories are
  few; YAGNI). Trimmed the docs/domain/information-architecture.md "type" filter; flagged.
- **Paging = real** (server page/pageSize + `hasMore`) surfaced as an accessible **"Load more" button**
  rather than a scroll observer (simpler, keyboard-friendly). Flagged vs the spec's "infinite scroll".

**Verified.** Backend: 12 notification tests pass (paging + mark-all + IDOR scope). Frontend: 14 tests
(NotificationsPage 7 + NotificationCenter 7), axe-clean. Live end-to-end: page renders EN + AR(RTL),
Mark-all clears unread rows/dots + the bell badge through the new endpoint. build 178kB gz; oxlint 0;
i18n parity 482; `dotnet format` clean. **AC-051/053** remain Met (already demonstrated); this adds the
#79 surface — no verdict change.

**Next.** Meetings list/calendar (rest of PR-B); then PR-C+ test-hardening.

---

## P6 Recording tab — design-faithful empty card (`ACMP Meetings.dc.html`, isRecording)

### 2026-06-28 — Recording placeholder styled to the design recording empty-card anatomy

**Why.** Remaining-work item: style the Recording tab to its design ref. The full `isRecording` `recReady`
path (video player + searchable transcript) needs the **Webex adapter (Phase 2)** — no backend exists, and
fabricating a player/transcript would be dishonest. The design's *empty* recording states (`recNoTranscript` /
`recPending`, ~L301-308) are, however, **true now** (Webex isn't integrated → there genuinely is no recording),
so those are buildable design-faithfully today.

**What.** Aligned the shared meeting empty card (`.mt-gate`) to the design's recording empty-card literals
(recNoTranscript ~L307): centered card (12px radius), 48px rounded subtle icon (no border), 16px/700 title,
13px text-2 body capped at 380px, padding 40/24. The Recording tab is the one consumer with a design reference;
the Meeting lifecycle prompts + the Minutes placeholder are no-reference compositions that reuse the same
empty-card spec, so they improved in lockstep (verified no regression). **CSS-only** — no component or i18n
change; the existing Recording copy is already honest ("Webex recording and transcript retrieval arrive with
the Webex integration (Phase 2)").

**Deferred (unchanged).** `recReady` video + transcript → Webex adapter (Phase 2). Minutes full MoM document
(RTE, sections, decisions/actions, lock + SHA-256 hash-chain) → **P7**; building it now would fake
decisions/approval/audit data and violate the audit non-negotiables, so Minutes stays an honest placeholder.

**Verified.** computed-style px all match design literals (pad 40/24 · radius 12 · icon 48/13/no-border ·
title 16/700 · body 13/max-380); Recording + Minutes tabs screenshotted EN; `meetings` suite 50 tests pass;
build 178kB gz; oxlint 0. **No AC verdict change** (design-fidelity placeholder; no feature backend added).

**Next.** Rest of PR-B (notifications full-page, meetings list/calendar); Minutes + recording-ready land with
their backends (P7 / Webex Phase 2).

---

## P6 agenda viewer (read-only) — design "Agenda preview" card (`ACMP Meetings.dc.html`, isOverview)

### 2026-06-28 — Read-only agenda viewer ported to the isOverview preview-card anatomy + head status-chip fix

**Why.** The read-only agenda (rendered when an agenda is Published/Locked/Closed or the meeting has started)
reused the editable builder rows with controls stripped — anatomy-divergent. Replaced with a dedicated viewer
matching the design's "Agenda preview" card (`ACMP Meetings.dc.html` isOverview, ~L263): one card
(`overflow:hidden`, 12px radius), flat rows split by `--border-soft`, a 22px round number, title-over-presenter,
and a mono timebox. Literal-px throughout (verified by computed-style gate, not just pixel-diff). The dead
`readOnly` path threaded through `AgendaItemRow` + its `.mt-item-readonly` / `.mt-grid-readonly` CSS were
deleted (one read-only impl, not two).

**Bug fixed.** The meeting-detail head status chip was a binary `Published ? success/Published : warn/Draft`
check, not `readOnly`-gated — so Locked/Closed agendas (both render the viewer) mislabelled as **"Draft"/warn**.
Extracted the 4-tone `agendaTone` helper (#31) to a shared `agendaStatus.ts` and reused it in the head and the
meetings list. Verified live: a Locked agenda now reads "Locked"/info.

**Decisions (operator GO, 2026-06-28).**
- **Budget bar kept** in the viewer — the design's read-only alternative is a readiness sidebar needing data we
  don't have; the bar sits above the preview card and still flags an over-run agenda. Card fidelity untouched.
- **Topic key re-added** on the row's secondary line (`KEY · presenter`, mono) — deliberate deviation from the
  design preview row (no key) for traceability: a Locked/Closed agenda is an official record, so the canonical
  TOP-YYYY-### must stay visible (CLAUDE.md). Urgent/icons stay dropped per design.

**Verified.** computed-style px all match design literals; EN + AR (RTL mirror) + tablet (no overflow);
`AgendaBuilder.test.tsx` 16 tests (added: preview anatomy + key; Locked head-chip regression); axe-clean
viewer; i18n parity 475; build 178kB gz; oxlint 0. **No AC verdict change** — the viewer is design-fidelity and
the head-chip fix corrects status display; no `AC-###` flips.

**Next.** Minutes + Recording tabs (refs: `ACMP Agenda & Meeting.dc.html` isMinutes; `ACMP Meetings.dc.html`
isRecording); then rest of PR-B (notifications full-page, meetings list/calendar).

---

## P6 meeting workspace — design-fidelity reconciliation (`ACMP Agenda & Meeting.dc.html`, isMeeting)

### 2026-06-28 — Notes editor, action row, captured card to design anatomy + deferred concern

**Why.** A prior "pixel-exact" pass fixed spacing but missed *anatomy*: the discussion notes were a bare
textarea (no toolbar), an extra Actual-time control was injected, the "Captured on this item" card was
omitted, and the urgent pill carried an extra icon. Reconciled the workspace to the reference anatomy
(operator-confirmed decisions): notes editor = bordered box with a markdown toolbar (B/I/•/№/link inserting
marks into the plain-text body) + autosave-on-blur + "Autosaved" indicator (no Save button); the action row
is the 3 capture-button stubs (P7/P8/P9); the "Captured on this item" card renders an honest empty state;
the urgent pill is text-only. Breadcrumb→banner gap set to the design's 12px. Shared `Select` fixed to
`focus({ preventScroll: true })` so opening a low dropdown no longer scrolls the page and hides content above.

**DEFERRED CONCERN (operator decision, 2026-06-28).** The **actual-time / outcome recording control** is
**removed from the meeting-workspace UI** for now. The **backend is kept** — the `RecordActualTime` command,
endpoint, and `useRecordActualTime` mutation hook all remain wired; only the UI control was removed. Re-add a
design-faithful control in a later slice (likely near the item-time header, not the capture-button row). The
page-width enlargement that had been added to fit the old inline control was **reverted** so the meeting
detail keeps the same `72rem` page cap as every other screen (coherent widths).

**Next.** Agenda builder/viewer pixel pass; Minutes/Recording tabs; then PR-B (list/calendar, full-page
Schedule + Type/Mode backend, notifications full-page). Nothing committed pending operator GO.

---

## P5 UI refresh — rebuild Topics & Backlog vs the updated `ACMP Backlog & Topic.dc.html`

### 2026-06-27 — Backlog (5 views incl. live calendar/timeline), filter chips, submit RTE bar, 5-tab topic detail

**Why.** The `ACMP Backlog & Topic.dc.html` reference grew since P5b shipped: the filter bar is now dropdown
**chip-buttons**, the **calendar** and **timeline** are first-class live views (were "coming soon" shells), the
submit description gains a formatting toolbar, and the topic-detail tab bar is now **5 tabs** (Overview ·
Discussion · Attachments · Votes · History). This slice reconciles the built P5 screens to the updated local
reference (read directly), composing the corrected P3/P5 shared components. Branch
`feat/P5-backlog-topic-refresh` off `main`. Visual SoT = the `.dc.html`; behavior SoT = the planning package.

**Decisions (agreed at GO; match the design, build nothing whose data isn't planned):**
- **D1 — Calendar & Timeline = faithful chrome + honest empty.** Both render the design's frame (calendar: month
  nav + locale weekday header + day-cell grid + today ring + legend; timeline: Topic column + 6 week columns + a
  row per real topic) but place **no markers/bars** — the Topics API exposes no scheduled-meeting date, due/target
  date, or planned spans (those arrive with meeting scheduling, **P6**). An honest inline note states this rather
  than fabricating events. No backend change. (guardrail #14, behavior SoT.)
- **D2 — Topic-detail Votes tab = added with an honest empty state** ("voting arrives in P9"), so the 5-tab bar
  matches the design; the real vote cards land with the Voting module (P9).
- **D3 — Submit Affected-streams = kept as free-text tokens** (flagged): no committed stream registry in the web
  yet (BL-024 owns streams); the design's preset stream chips wait for that source.
- Proceeded on (no objection): keep **title-link** row navigation (a `<button>` can't legally wrap a grid row of
  interactive cells — the a11y-correct call, flagged); render the submit **RTE toolbar inert** (`aria-hidden`,
  stores plain text); keep the **single stored title** (no translated title in the data — the design's alt-language
  line is dropped, flagged); **split Attachments into its own detail tab** and wire post-create upload; restyle the
  **relationships** aside to the design header but keep it an honest empty state (no edge data until P7–P11).

**Done.**
- **New shared `FilterChip`** (`components/ui/FilterChip.tsx`, composes `Menu`) — the design's filter pill (label +
  count badge + chevron; active = accent/primary-tint). Single mode (radio + "Any …" clear) and multi mode
  (toggle, stays open, "Clear" row). Styles in `controls.css` (`.fchip*`). The Backlog filter bar now uses five
  chips (Status multi; Type/Urgency single; **Stream/Owner disabled** — no option source yet); dropped the design's
  mock "Data: live/loading/empty/error" preview toggle (real state comes from the query).
- **Backlog** (`Backlog.tsx`): saved-view restyled to the design's **accent chip** ("Triage queue", inert — no
  saved-view backend); the **coming-soon shell removed**; calendar/timeline now render live components; table/list/
  kanban unchanged (already faithful post P5-review).
- **`Calendar.tsx` / `Timeline.tsx`** (new) — the D1 faithful-chrome views; Gregorian, Intl-localized, logical-CSS
  RTL-safe (calendar prev/next use the same deliberate `scaleX(-1)` chevron flip as pagination).
- **Submit** (`SubmitTopic.tsx`): inert RTE toolbar (`aria-hidden`) wrapping the description textarea (`.sub-rte*`),
  visual parity with the design; we still store plain text.
- **Topic detail** (`TopicDetail.tsx`): 5 tabs — Overview, Discussion (id `comments`), **Attachments** (own tab:
  dropzone wired to `useUploadTopicAttachment` post-create + file list; download inert pending a presigned-URL
  endpoint), **Votes** (empty → P9), History. Attachments moved out of Overview. New `useUploadTopicAttachment`
  hook in `api/topics.ts`.
- **State components:** reused the **shared canonical** `ErrorState`/`EmptyState`/`LoadingState` rather than forking
  the reference's richer per-screen error card (the design's mono request-id line) — the app-wide P3/P4 precedent;
  a deliberate reconciliation, recorded.
- **i18n** (EN+AR, real Arabic): `topics.savedView`, `topics.calendar.*`, `topics.timeline.*`, `detail.tab`
  (comments/attachments/votes), `detail.attach.*`, `detail.votes.*`; removed the dead `topics.savedViews`/
  `topics.shell`. **Parity 438.**

**Verification (deterministic, green).** Web **197/197** (was 189; +8 — 3 Backlog [live calendar chrome, live
timeline chrome, Status-chip filter], 2 TopicDetail [Attachments tab + upload, Votes empty], 4 FilterChip
[single/multi/disabled + axe]; existing axe cases stay green), i18n parity **438**, `tsc -b` clean, vite build
clean (**JS 176.12 kB gz** < 300; CSS 23.58 kB gz), oxlint clean (only the pre-existing untouched `Toast.tsx`
fast-refresh warning), `topics.css` + `controls.css` grep = zero physical properties (RTL-safe; the calendar
prev-chevron `scaleX(-1)` is the deliberate direction-bearing exception).

**Live authenticated VR — DONE (2026-06-27).** After the operator set the `acmp-admin` dev password (KC required
action cleared) and `acmp-web` was rebuilt, drove Playwright through the **real Keycloak PKCE** flow over the live
stack and visually verified every new surface: backlog filter chips + accent saved-view chip, **live calendar**
(month grid + weekday header + today ring + legend + honest P6 note), **live timeline**, submit **inert RTE
toolbar**, and topic detail **5 tabs** (Overview · Discussion · Attachments + post-create upload · Votes empty ·
History) + the empty relationships aside — in **EN-light** and **AR-dark**, plus **tablet 768** (zero horizontal
overflow, `scrollWidth == clientWidth`). AR confirms full RTL mirroring (sidebar inline-end, chips/table mirrored,
calendar prev/next chevrons flipped, Arabic weekday header) and dark tokens. The live drive (login → backlog →
all 5 views → submit → detail tabs) doubles as the E2E smoke pass. Screenshots: `P5-{EN-light,AR-dark}-*` (12).
Automated pixel-diff VR remains **P17**.

**Acceptance audit (this entry). No verdict flips** — visual/fidelity reconciliation + honest-empty new views.
Touches **AC-040/045/046** (the new chips/calendar/timeline/tabs render RTL-mirrored + axe-clean in the component
tests) and **AC-041** (stays Partial → automated VR P17). **AC-057** aging badge unchanged. No feature AC changes;
Calendar/Timeline carry no AC of their own (views over Topics). **No new ADR** (UI on the settled stack).

**Next.** Push `feat/P5-backlog-topic-refresh` → PR → green CI → **await operator GO + live VR** to squash-merge.
The deferred faithful data for Calendar/Timeline (real scheduled/due markers) lands with P6 meeting scheduling;
the detail Votes cards with P9; the submit stream chips with BL-024.

---

## P4 UI refresh — rebuild Administration → Users & Membership vs the updated `ACMP Administration.dc.html`

### 2026-06-27 — Users & Membership rebuilt to the updated design (rich directory + read-only user detail); unplanned affordances removed

**Why.** The Administration design grew a lot since P4 shipped — it now defines a 7-tab admin area, four
explicit data-states, and a much richer Users & Membership row (committee chips with remove/add, a voting
switch, an assignments count, and a per-row view button into a user-detail panel). The built screen was a
simplified subset. This slice reconciles the **Users & Membership** screen (the P4-scoped one) + its states to
the updated local `/ACMP product context/ACMP Administration.dc.html` (read directly), composing the corrected
P3 shared components. Branch `feat/P4-users-membership-refresh` off `main`.

**Scope decisions (agreed with the operator before building).** Match the design **but build nothing whose
behavior isn't planned** — render planned-but-unwired affordances as inert, and **remove what conflicts with a
settled ADR**:
- **7-tab strip** rendered per design (Users active; Templates/System Health/Streams/Roles/Job Monitor/
  Notification Settings are **disabled placeholders** for their later phases — Templates BL-120/121, Streams
  BL-024, Job Monitor BL-006/AC-056, Notification Settings BL-082/BL-124, Roles = static Keycloak mirror;
  *System Health screen is not yet ticketed — capability = BL-009 health checks*). No-reference-yet, flagged.
- **Membership editing affordances** (committee `×` remove, dashed `+` add, voting-eligibility switch) rendered
  to match the design but **inert/disabled** — the directory stays read-only (`GET /api/members`). Stream
  assignment lands with **BL-024**; voting eligibility with **Voting (P9)**. Same precedent as the existing
  disabled switch.
- **User detail (D1, partial):** the per-row view button now opens an **in-place, read-only** user-detail panel
  (no routing — mirrors the design's `sub` state) rendering **only API-backed data** (avatar, name, email,
  role + read-only Keycloak note, status, voting-eligible, committee/stream memberships). The design's facts
  that the member API doesn't return (Keycloak ID, last sign-in, provisioned date) are **omitted** until the
  directory exposes them.
- **Removed (not planned / conflicts ADR-0015):** the header **"Provision via Keycloak"** button and the whole
  **invite panel**. In-app account creation/invitation contradicts ADR-0015 (manual Keycloak provisioning, no
  self-registration). Recorded as **OQ-042** (docs/decisions/open-decision-register.md) — the future detail slice resolves it (deep-link to the
  Keycloak console vs a new ADR vs drop the panel). The dead `admin.provision` i18n key was removed (both locales).

**Done.**
- `UsersMembership.tsx` rebuilt: 7-tab strip (shared `Tabs`, icon+label, only Users enabled); KC read-only
  banner (pad/margin reconciled to the design 11/14 + mb14); inert filter row (gaps reconciled 9/6); rich table
  — user (avatar/name/email), role + lock note, **membership** (`.adm-mchip` committee chips with `×` glyph +
  inert dashed `+add` + disabled voting switch with on/off label colors), assignments (check + honest `—`, no
  count on the API yet), status (`status-chip-sm` + the **view** button). New read-only `UserDetail` sub-view.
- `icons.tsx`: added the six Administration tab glyphs (`usersGroup`/`template`/`activity`/`stream`/`shieldUser`/
  `cog`) with paths lifted verbatim from the design file.
- `administration.css` rewritten — added `.adm-mchip`(+`×`), `.adm-add`, `.adm-view`, `.adm-vote-on/-off`,
  `.adm-status`, and the full read-only detail panel; **logical-properties only** (grep-verified zero physical
  left/right/margin/padding), the two `[dir='rtl'] … scaleX(-1)` chevron flips being the deliberate
  direction-bearing exceptions.
- `controls.css`: `.tab` gains `inline-flex`/gap so the design's icon+label tabs sit correctly (additive — safe
  for the existing text-only tab consumers).
- i18n: 7 tab labels (added streams/roles/jobs), `addCommittee`, `viewUser`, and a `detail.*` namespace
  (back/memberships/roleReadonly/votingEligible/yes/no/noMemberships) — EN+AR, **parity 427** (removed
  `provision`). Real Arabic.

**Decisions / drift (design = visual SoT; package = behavior SoT; recorded in the file header).**
- The membership chips source from `member.streams` (the single-committee model has no separate committee field);
  observer = no streams (unchanged from the prior build).
- The state screens reuse the **shared canonical** `LoadingState`/`ErrorState`/`EmptyState` (app-wide P3 pattern,
  `ACMP System States.dc.html` authority) rather than forking the admin file's richer state cards for one screen;
  **permission-denied stays at the route layer** (the Administration route is admin-gated) — a genuine behavior
  difference, not drift. Recorded.
- **No new ADR** (UI on the settled stack).

**Verification (deterministic, green).** Web **189/189** (was 184; +5 admin — 7-tab/only-users-enabled,
no-provision/invite, inert add-committee, view→read-only-detail round-trip, **+ axe WCAG 2.2 AA on the directory
AND the detail**), i18n parity **427**, `tsc -b` clean, vite build clean (JS **174.84 kB gz** < 300; CSS 23.02
kB gz), oxlint clean (only the pre-existing untouched `Toast.tsx` fast-refresh warning), `administration.css`
grep = zero physical properties (RTL-safe by construction). **Not yet run:** the live authenticated browser VR
(rebuild `acmp-web` → Keycloak PKCE login → capture EN-light/dark + AR-RTL-light/dark at tablet+desktop vs the
`.dc.html`). It is blocked solely on the operator setting a dev password for `acmp-admin` (the realm imports it
with `UPDATE_PASSWORD`, no committed secret — guardrail 7), the same standing caveat the P3/P6 UI passes carried;
automated pixel-diff VR remains **P17**.

**Acceptance audit (this entry).** **No verdict flips.** **AC-059** stays **Met** and gains the read-only
user-detail surface (directory + detail both unit-tested + axe-clean). The Administration screen adds a surface
to the localization/a11y ACs — **AC-040/045/046** render RTL-mirrored (logical CSS + the two intentional chevron
flips) and axe-clean in the component tests; **AC-041** stays **Partial** (automated VR → P17). No feature AC
changes — this is visual/fidelity reconciliation + a read-only view.

**Next.** Push `feat/P4-users-membership-refresh` → PR → green CI → **await operator GO + live VR** to
squash-merge. The operator runs the authenticated VR (set the `acmp-admin` dev password, rebuild `acmp-web`,
capture the 8 combos). Future Administration slices build the remaining sub-tabs (Templates/Streams/Jobs/Notif/
Health). **OQ-042 (invite/provision vs ADR-0015) is RESOLVED (2026-06-27): adopt (b)** — any future
"Provision via Keycloak" affordance is a **deep-link** to the Keycloak admin console only (no in-app account
creation/invite form); option (c) needs a new ADR. (docs/decisions/open-decision-register.md.)

---

## P3 foundation refresh — reconcile token/component/shell/nav layer to the updated design references

### 2026-06-27 — Foundation fidelity pass vs updated `.dc.html` (Design System / ACMP shell / Navigation & IA)

**Why.** The design-context references were re-synced (PR #24); the P3 foundation is the base every later
screen inherits, so it must match the *updated* references first. Branch `feat/P3-foundation-refresh` off
`main`. Visual SoT = the local `/ACMP product context/ACMP Design System.dc.html`, `ACMP.dc.html`,
`ACMP Navigation & IA.dc.html` (read directly).

**Finding (headline): the foundation was already ~95% faithful.** Tokens match the DS **verbatim** (spacing/
radius/motion/surface/border/text/primary/accent/focus/shadow/status — byte-identical); Dialog (440/r14/
overlay rgba(10,14,20,.5)+blur2), Toast (3px tone/r10), Menu (r13/item40), Segmented (30h, active surface+
shadow), Pagination (30sq + RTL flip), Table (11px head/42 hcell/12 pad), Tags/Badge, Button (38/9/16/13.5;
sm32/lg44), Card (r12), nav model (groups/order/icons/access/active-rail/CTA/view-only) all already matched.
So this was a **targeted reconciliation**, not a rebuild (ponytail: smallest correct diff).

**Drifts fixed (against the updated DS).**
- **StatusChip** was 22/8/11.5 (a prior P5 over-correction); DS §08 standalone chip = **24/9/12**. Restored
  default to 24/9/12 and added a `size="sm"` variant (22/8/11.5) for dense **table rows** (DS §09). **All six
  consumers were audited and sized per context:** the dense **table cells** (Backlog table, Users & Membership
  admin table, Meetings-list status+agenda cells) use `sm` (22); **standalone/header/card** chips (Topic-detail
  header, Backlog list view, Agenda-builder status + budget label, Meeting workspace Live + quorum) use the
  24/9/12 default — so the change lifts standalone chips 22→24 toward the DS and leaves dense rows at 22.
- **TopBar global search** was missing the DS **"Ctrl K"** keyboard affordance. Added the hint chip
  (`.search-kbd`, inset-inline-end) **and wired Ctrl/⌘+K to focus the search input** (real, not decorative).
  i18n key `common.searchShortcut` (EN+AR parity).
- **TopBar metrics → DS app-shell:** `.brand-word` 14→**15**px; `.icon-btn` 34→**36**px; `.chip-btn` (lang)
  34→**36**px.
- **Notification popover** aligned to the other shell popovers + DS: radius `--r-lg`→**13px**, top 48→**46**,
  border `--border-strong`→`--border`; bell **badge** 15→**16**px, offset −2→**−3**.
- **Tabs** inline padding `--sp-4`(16)→**14**px (DS §10).
- Removed dead `.topbar-user` CSS rule (only `-name`/`-role` are used).

**Decisions / reconciled inter-file deltas (no silent drift, guardrail 14).**
- **Sidebar width 248px** kept — `ACMP.dc.html` app shell (the actual shell authority) specifies 248; the
  Navigation & IA file shows 244 and the DS doc's own nav shows 224. The code already documents 248 as the
  app-shell choice; not churned.
- **Theme selector kept** as `:root` + `[data-theme="dark"]` (design uses `[data-acmp-theme]`). Token **values
  are identical**, so renaming the attribute touches every selector + theme.ts for zero visual gain — kept.
- **Domain components in the DS but OUT of P3 scope** (relationship panel, kanban/calendar/timeline, voting
  panel, rich-text editor) were NOT built here — they land in their owning phases (P10/P5+P12/P9/P7) per
  guardrail #14.
- **No new ADR** (UI on the settled stack).

**Verification (deterministic, green).** Web **184/184** (was 182; +2 — StatusChip size variant, TopBar
Ctrl+K focus + hint render; axe cases stay green), `tsc -b` + vite build clean (JS **173.98 kB gz** < 300
budget; CSS 22.64 kB gz), oxlint clean (only the pre-existing untouched `Toast.tsx` fast-refresh warning).
**i18n parity 416/416** (no missing/extra) — maintained by the symmetric EN+AR edit; note there is **no
automated i18n-parity test** in the suite (verified by a key-set diff, not a committed gate). **Live bundle verified:** rebuilt `acmp-web` image and confirmed the served `index-*.css` carries
the reconciled values (`.status-chip` 24/9/12, `.status-chip-sm` 22/8/11.5, `.search-kbd`, `.brand-word` 15,
`.icon-btn` 36).

**Live visual pass (done, desktop).** After the operator set a dev password for `acmp-admin`, drove an
authenticated Playwright pass over the rebuilt stack (real Keycloak PKCE). **EN-light** and **AR-RTL-dark**
dashboard captured and verified: brand 15px + "Ctrl K" search hint, 36px chrome toggles, profile trigger,
the full nav (groups/order/icons/active-rail/CTA/view-only eye) — and in **AR the entire shell mirrors**
(sidebar+brand on the inline-end, Arabic nav, rail + Ctrl-K hint mirrored, dark tokens applied). Screenshots:
`P3-EN-light-dashboard.png`, `P3-AR-rtl-dark-dashboard.png`. **Remaining combos** (EN-dark, AR-light, tablet
768/1024) are covered by the same mechanism proven here — theme = token swap, RTL = logical properties,
responsive = the flex/sticky shell — and can be captured on request. **Automated pixel-diff VR remains P17.**

**Acceptance audit (this entry).** **No verdict flips.** Visual/fidelity reconciliation of the foundation —
touches the surfaces behind **AC-040/045/046** (RTL mirroring, focus, labels/contrast — unit + axe still
green) and **AC-041** (RTL render; automated VR → P17, stays Partial). No feature AC changes.

**Next.** PR **#25** `feat/P3-foundation-refresh` is open with **green CI** + the live desktop visual pass
done — awaiting review/GO to squash-merge. PR **#24** (design-context sync) awaits the operator's merge
approval and can land independently.

---

## P6 — Agenda & meeting management

### 2026-06-27 — P6 hardening: fixed the 3 live-pass findings + re-verified the full notification loop live (AR/RTL)

**Scope.** Fixed the findings surfaced by the live pass below — two pre-existing (CSP fonts, JIT email-dup) and
**one real P6b defect** uncovered while re-verifying — then drove the complete notification loop live to green.
Backend **407 green** (+1 regression), all gates clean.

**Fixes.**
- **Finding 1 — CSP fonts (infra).** `deploy/nginx/default.conf.template` → `font-src 'self' data:` (the build
  inlines IBM Plex as `data:` fonts). Verified live: zero font-CSP console errors after rebuild; the configured
  CSP header now carries `font-src 'self' data:`.
- **Finding 2 — JIT 500 on emailless duplicate (Membership/P4).** `IX_committee_members_Email` is now a
  **filtered unique index** (`HasFilter("[Email] <> ''")`, migration `Membership_FilteredEmailUniqueIndex`) —
  email uniqueness applies only to real emails, so JIT can provision multiple emailless members (Keycloak users
  without an email). `KeycloakUserId` remains the stable unique identity. Verified live: `POST /api/members/me`
  now **200** (was 500), the current login is provisioned, and the directory returns both members.
- **Finding 3 — NEW, real P6b bug: notification fan-out 500 for ≥2 recipients.** The agenda/meeting fan-out
  builds the bilingual `LocalizedString` **once** and reuses that instance per recipient; EF can't track the
  same OWNED instance under two `Notification` principals → the 2nd save threw *"Notification.Body#
  LocalizedString.NotificationId is part of a key and cannot be modified"*. **`InAppNotificationChannel` now
  copies the values into fresh `LocalizedString` instances per notification.** This bug **broke notifications
  for any committee with ≥2 members** and was missed because the unit fan-out test used a fake channel and the
  integration test seeded a single recipient. **Regression coverage added:** a unit test that fans one shared
  message to 3 recipients through the real channel, and the `/api/notifications` integration test now seeds
  **two** members. (Application 319 → 320; Api 29 retained with the 2-member seed.)

**Live re-verification (green, AR/RTL).** After rebuilding `api`+`web`: scheduled **MTG-2026-003** →
`POST /api/meetings` 201 (no 500) → the fan-out reached both committee members → the **notification center**
showed the current user's item — title **"تم جدولة اجتماع"**, body **"تمت جدولة \"Payments Tokenization
Review\" بتاريخ 2026-07-15"** (bilingual + Gregorian date + Intl timestamp), the **bell badge read "1 unread"**
— and **clicking it marked it read (badge cleared) and navigated to the deep link** (`/meetings/MTG-2026-003`).
The full AC-051 / AC-052-shape / AC-053 + P6e loop is now proven end to end on the live stack.

**Notes.** A harmless dev-data artifact remains — two "ACMP Administrator" committee members (the stale `a65c…`
from a prior realm + the live `a69d…`), both emailless; production users will have emails so this is dev-only.

**Verification.** Backend **407/407** (Domain 42 · Architecture 16 · Application 320 · Api 29), `dotnet format`
+ build clean. Live: JIT 200, schedule 201, fan-out to 2 members, notification rendered + read + deep-linked.

**Next.** Push `feat/P6-meetings` → PR → green CI → review → squash-merge.

---

### 2026-06-27 — P6 live authenticated browser pass (rebuilt stack, real Keycloak PKCE, AR/RTL) + 2 pre-existing findings

**Scope.** Live pass over the P6 surfaces on the rebuilt `api`+`web` images (all 7 services healthy), driven
in Chrome via Playwright as `acmp-admin` through the **real Keycloak authorization-code + PKCE (S256)** flow,
in **Arabic / RTL**.

**Verified live (green).**
- **Real SQL migrations applied** — `[INF] Database migrations applied.`; the `meetings` + `notifications`
  schemas materialized on SQL Server (closes the deferred "live migration apply" note for P6a/P6b).
- **Login** — PKCE/S256 round-trip → `/dashboard` authenticated (token `sub`, `preferred_username=acmp-admin`).
- **Meetings list (P6c)** — renders AR/RTL with the full shell; honest empty state; the **"Schedule meeting"**
  action.
- **Schedule flow (P6 follow-up)** — the dialog renders AR/RTL (title/chair/start/end/location/join, required
  markers, placeholders); the **chair `Select` sourced `/api/members`** (showed the provisioned admin); submit
  → `POST /api/meetings` **201** → **MTG-2026-001** → navigated to the agenda builder. End to end.
- **Agenda builder (P6c)** — AR/RTL: breadcrumb, the **Agenda/Meeting tabs (P6d)**, the title + **Draft chip**,
  **Gregorian AR date via Intl**, the **time-budget bar** (0/90 min), the Prepared-pool + agenda **empty
  states**, and **Publish & Notify correctly disabled** at 0 items.
- **Meeting tab (P6d)** — the lifecycle **gate** shows "not started — publish & start first" for a Draft agenda.
- **Notification center (P6e)** — the bell + panel render AR/RTL; the empty state is correct for the current
  user (see finding 2).
- **Notification fan-out (P6b)** — scheduling **did** create a real `MeetingScheduled` notification row in
  `notifications.notifications` (confirmed by direct SQL) — the cross-module fan-out works on the live stack.

**Finding 1 (pre-existing infra, app-wide — not P6): CSP blocks the inlined fonts.** Every page logs
`font-src 'self'` CSP violations for the build's `data:` base64 IBM Plex fonts (Vite inlines them under its
asset-inline limit) → the deployed app **falls back to system fonts** instead of IBM Plex Sans / Sans Arabic.
One-line fix: `font-src 'self' data:` in `deploy/nginx/default.conf.template`. Layout/RTL/behaviour are
unaffected.

**Finding 2 (pre-existing identity/JIT — P4, CHANGE-004 lineage — not P6): JIT provisioning 500 on emailless
duplicate.** `acmp-admin`'s Keycloak user carries **no email**; JIT (`POST /api/members/me`) provisions a
member with an empty email. The realm was recreated at some point (admin `sub` changed `a65c…`→`a69d…`) while
the SQL volume kept the old member row, so this session's JIT tried to **insert** a second emailless member and
hit `Cannot insert duplicate key … 'IX_committee_members_Email' … value is ()` → **500**. Net effect: the
current login is **not** a committee member, so the fan-out notified the stale member (`a65c…`) and the live
user's (`a69d…`) center is (correctly) empty — the P6 scoping is right; the bug is upstream. **Real defect to
fix in Membership:** either require an email from Keycloak, or make `IX_committee_members_Email` a **filtered**
unique index (`WHERE Email <> ''`/`IS NOT NULL`) and have JIT match-or-update by `KeycloakUserId` so a changed
`sub` reconciles instead of duplicating.

**Net.** P6 is **functionally validated live** end to end through the schedule → agenda → (gate) flow in
AR/RTL, and the notification fan-out is proven at the data layer; the only unproven UI step (the notification
*appearing* in the recipient's center) is blocked solely by finding 2, a pre-existing P4 identity-data bug. No
P6 code change resulted from this pass. **Recommended follow-ups (separate from the P6 PR):** fix finding 2
(JIT/email index) and finding 1 (CSP fonts); then the recipient-center demo will pass.

**Next.** Push `feat/P6-meetings` → PR → green CI → review → squash-merge (the two findings can be tracked as
their own fixes — finding 2 in particular gates a clean live notification demo).

---

### 2026-06-27 — P6 follow-up: /api/meetings + /api/notifications WebApplicationFactory integration tests

**Scope.** The optional HTTP-contract integration tests for the P6 endpoints, through the real pipeline
(MediatR + FluentValidation + policy authorization + Problem Details), proving the cross-module wiring
(Meetings → Membership directory → Notifications) end to end over HTTP. Branch `feat/P6-meetings`. Backend
**406 green** (was 397; +9 Api), all gates clean (`dotnet format`, build).

**Done.**
- **`AcmpWebApplicationFactory`** now swaps the **Meetings + Notifications** DbContexts to private InMemory
  stores too (it already swapped Membership + Topics) — so the whole P6 surface runs against InMemory with the
  header-driven `TestAuthHandler` standing in for Keycloak.
- **`MeetingsApiTests`** (5): schedule without a token → **401** (AC-008); a **Member is 403** on schedule and
  on agenda-publish (docs/domain/permission-role-matrix.md Meeting.Schedule / Agenda.Publish); **schedule → list → detail (Draft agenda) →
  unknown-key 404**; **add item → publish → agenda `Published` v1**.
- **`NotificationsApiTests`** (4): notifications without a token → **401**; **AC-051 end to end** — a Secretary
  schedules + builds + publishes an agenda, and a seeded committee **Member then sees the `AgendaPublished`
  notification** in their feed with the meeting title in the body and the `/meetings/{key}` deep link;
  **mark-read is scoped to the caller** (the owner gets 204 and the unread count drops; a **different user gets
  404** on the same id — the IDOR guard over HTTP) and an unknown id → **404**.

**Decisions / notes.**
- The publish path needs each agenda item to have a presenter (the domain guard) — the test items carry one.
- The cross-module seams resolve against InMemory exactly as in production (the publish fan-out goes
  Meetings → `ICommitteeDirectory` (Membership) → `INotificationChannel` (Notifications)); the test proves no
  module reaches another's tables — it all flows through the Acmp.Shared contracts.
- **No new ADR**; tests only.

**Verification (deterministic, green).** Backend **406/406** (Domain 42 · Architecture 16 · Application 319 ·
**Api 29**), `dotnet format --verify-no-changes` + build clean. The two new files were BOM-normalized to the
format gate.

**Acceptance audit (this entry).** **No verdict flips** — these tests *strengthen* already-recorded verdicts
with HTTP evidence: **AC-051** now has a full publish→recipient-feed round-trip over HTTP; **AC-053** gains the
HTTP scoping/IDOR proof; **AC-008** gains 401 coverage on the meetings + notifications surfaces.

**Next.** **Push `feat/P6-meetings` → PR → green CI → review → squash-merge**, then the **live authenticated
browser pass** across the P6 surfaces (schedule → agenda → publish/notify → start → conduct → end; AR/RTL +
dark + live axe).

---

### 2026-06-27 — P6 follow-up: meeting-schedule flow (un-defers the new-meeting form) + server-implicit committee

**Scope.** Build the previously-deferred **schedule-a-meeting** flow and remove its only blocker: the SPA
no longer needs a `committeeId`. Branch `feat/P6-meetings`. Backend **397 green** (unchanged count), web
**182 green** (was 177; +5 ScheduleMeetingDialog), i18n parity **412**, all gates clean (`dotnet format`,
`tsc -b`, vite build, oxlint, CSS RTL-safe).

**Done.**
- **Backend — committee is now implicit (CON-001).** `CommitteeId` is removed from `ScheduleMeetingCommand`;
  the handler anchors every meeting to a new well-known `Meeting.SingleCommitteeId` constant. The field was
  **stored but never read for any logic** (there is no Committee aggregate), so this is a refinement, not an
  architecture change — **no ADR**. The endpoint binds the command directly, so the API request body simply
  drops `committeeId`. Domain `Meeting.Schedule` keeps its `committeeId` parameter (the constant is passed in);
  the handler test's `ScheduleCmd()` + the unused test field were updated. 397 backend tests stay green.
- **Frontend.** `api/meetings.ts` → `useScheduleMeeting()` (`POST /api/meetings` → 201 + the new
  `MeetingSummary`, invalidates the list). `features/meetings/ScheduleMeetingDialog.tsx` — a shared-`Dialog`
  form (title, **chair** `Select` from `/api/members` defaulting to the Chairman, start/end `datetime-local`,
  optional location/join URL) with client validation (title required, chair required, end-after-start) and
  bilingual error messages; on success it opens the new meeting's agenda builder (`/meetings/{key}`).
  `MeetingsList.tsx` gains a **"Schedule meeting"** header action (the deferred-note is replaced).
  `datetime-local` values are converted to ISO 8601 for the API.

**Decisions / drift (no silent drift, guardrail 11).**
- **Committee is server-implicit** — the cleaner modelling for a single-committee system than threading a
  magic GUID through the SPA. `Meeting.SingleCommitteeId` is the single anchor; a second committee would need
  an ADR + a real `Committee` entity (noted at the constant).
- **Chair picker** sources `/api/members` (active), defaults to the **Chairman** role; `chairUserId` = the
  member's `publicId` (the value the meeting stores), `chairName` = a display snapshot.
- The design has **no schedule screen** — this composes shared components (Dialog/Field/Select/Button); the
  meetings list itself was already flagged as no-reference scaffolding.

**Verification (deterministic, green).** Backend **397/397** (the command change carried through Domain/
Application/Api). Web **182/182** (Vitest+RTL: schedule with the defaulted Chairman → asserts the POST payload
+ navigation to the new meeting; title-required + end-after-start validation block submit; AR chrome; **+ axe
WCAG 2.2 AA**), i18n parity 412, `dotnet format --verify-no-changes` + `tsc -b` + vite build + oxlint clean,
new meetings CSS grep = zero physical properties. **Not yet run:** the live authenticated round-trip
(schedule → 201 → land on the agenda builder, AR/RTL + dark) — recommended.

**Acceptance audit (this entry).** **No verdict flips** — meeting scheduling (W5) has no dedicated `AC-###`;
this un-defers the flow noted across P6c/P6d/P6e and makes the P6 surfaces reachable end to end (schedule →
build agenda → publish/notify → start → conduct → end).

**Next.** P6 UI is now complete and self-reachable. Remaining before the PR: optional `/api/meetings` +
`/api/notifications` WebApplicationFactory integration tests, then **push `feat/P6-meetings` → PR → green CI →
review → squash-merge**, and the **live authenticated browser pass** across the P6 surfaces (AR/RTL + dark + live axe).

---

### 2026-06-27 — P6e UI: notification center wired to the live feed + bell badge (closes the AC-051/053 loop)

**Scope.** Wire the app-shell **NotificationCenter** (the bell popover, a P3 empty shell) to the live
`/api/notifications` feed from the P6b backend, and add the **unread bell badge** — the recipient-facing half
of the AC-051/AC-053 floor. Branch `feat/P6-meetings`. Web **177 tests green** (was 168; +9 — 7
NotificationCenter, +2 TopBar badge), i18n parity **393**, `tsc -b` + vite build + oxlint clean, CSS RTL-safe.

**Done.**
- `api/notifications.ts` — `useNotifications()` (`GET /api/notifications` → `{ items, unreadCount }`, a 30s
  background poll + refetch-on-focus) and `useMarkNotificationRead()` (`POST /api/notifications/{id}/read`,
  invalidates the feed). Title/body arrive **bilingual** from the server (ADR-0005); the UI picks the locale.
- `NotificationCenter.tsx` — renders the live list (unread-styled rows via the existing `.notif-item.unread`,
  an unread-count header, loading/error/empty states; the calm "all caught up" empty state is preserved).
  Each row is a button: clicking **marks it read** (if unread), closes the popover, and **follows its deep
  link** (`/meetings/{key}`) — the AC-051 deep-link + AC-052 navigation shape. Still a non-modal click-away
  region (Escape + outside-click dismiss).
- `TopBar.tsx` — an **unread badge** on the bell (count, capped "9+"), shown **only when `unreadCount > 0`**
  (honours the CHANGE-002 "no always-on dot over an empty inbox" rule); the bell's `aria-label` announces the
  unread count.
- `components.css` — `.notif-list`/`.notif-item` (button reset)/`.notif-dot`/`.notif-item-*`/`.notif-status`/
  `.notif-unread-count`/`.notif-badge`, all logical-properties-only (RTL-safe, grep-verified). Full EN+AR
  `notif.*` additions (titleUnread / unreadCount / loading / error; parity 393).

**Decisions / drift (no silent drift, guardrail 11).**
- **No `.dc.html` reference exists** for the live notification list (the panel is specified in the planning
  doc docs/domain/information-architecture.md p.79, not in `/ACMP product context/`). It composes the shell's existing `notif-*` styles +
  the design-system tokens — recorded in the file header. (See the "no-reference surfaces" note below.)
- **No "mark all read"** — the backend exposes only per-id read, and clicking an item marks it; a bulk
  endpoint isn't warranted at committee scale (YAGNI). **No push channel** — a 30s poll + refetch-on-focus
  keeps the badge fresh for ≤20 users (`ponytail`: add SSE/WebSocket only if the latency matters).
- **No new ADR** (UI on the settled stack).

**Verification (deterministic, green).** Web **177/177** (Vitest+RTL: live list render, unread styling,
click → mark-read + close + deep-link navigation, the already-read/no-deep-link path is a no-op, empty state,
AR content, **+ axe WCAG 2.2 AA** on the panel; TopBar badge shows only when unread>0). The shared `a11y.test`
+ `TopBar.test` now mock `api/notifications` (the test harness has no QueryClientProvider). i18n parity 393,
`tsc -b`, vite build, oxlint (only the pre-existing untouched `Toast.tsx` warning), notif CSS grep = zero
physical properties. **Not yet run:** the live cross-session browser pass (user A publishes an agenda → user B
sees the badge + the item appear within the poll window, clicks → deep link), AR/RTL + dark — recommended, and
it needs two provisioned members + a scheduled/published meeting.

**Acceptance audit (this entry).** **AC-051 Partial → Met** — the full path is now built and unit-tested end
to end: the P6b synchronous fan-out creates one bilingual notification per active member carrying the meeting
date + agenda title + deep link; the center renders it (unread badge + list) and the deep link navigates.
**AC-053 Partial → Met** — exactly one channel (in-app) is registered and rendered; no email/Webex is attempted.
**AC-052** stays **Partial** — the deep-link *navigation* mechanism is now proven (clicking a notification with
a deepLink routes to its target), but the *vote-open* notification itself is raised in **P9 (Voting)**. Live
browser confirmation is the recommended closing step for AC-051 (same standing caveat the other Met UI ACs carry).

**Next.** P6 UI is functionally complete (agenda builder · meeting workspace · notification center). Remaining
before the PR: the **deferred meeting-schedule flow** (committee/chair pickers — needs `committeeId` exposed),
optional `/api/meetings` + `/api/notifications` **WebApplicationFactory integration tests**, then **push
`feat/P6-meetings` → PR → green CI → review → squash-merge**, and the **live authenticated browser pass** across
the P6 surfaces (AR/RTL + dark + live axe).

---

### 2026-06-27 — P6d UI: live meeting workspace (the design's meeting tab) — agenda spine, attendance, discussion, actual-time

**Scope.** The **live meeting workspace** — the `isMeeting` block of the local design
`/ACMP product context/ACMP Agenda & Meeting.dc.html` — wired to the P6a conduct-meeting API (W7–W9), and
the **tab integration** that hosts both P6c's agenda builder and this workspace under one
`/meetings/:key` route. The MoM/minutes screen (the design's `isMinutes` block) stays **P7**. Branch
`feat/P6-meetings`. Web **168 tests green** (was 151; +17 — 9 workspace, 8 page; the 11 P6c agenda tests stay
green through the breadcrumb refactor), i18n parity **389**, `tsc -b` + vite build + oxlint clean, CSS RTL-safe.

**Done.**
- `MeetingPage.tsx` (route `/meetings/:key`, replacing the direct agenda route) — owns the page breadcrumb +
  a shared in-page **`Tabs`** switcher (Agenda builder | Meeting) + the **lifecycle gate**: a Scheduled
  meeting with a Published agenda shows **Start meeting** (`POST /start`, W7 — server-enforced); a Draft
  agenda shows a calm "publish & start first" prompt; Held/Closed shows a concluded prompt (minutes = P7).
  Default tab follows status (InProgress → Meeting). Renders `<AgendaBuilder/>` or `<MeetingWorkspace/>`.
- `MeetingWorkspace.tsx` — the design screen: header (title + **Live** pulse chip + an **Elapsed** timer
  ticking from `startedAt` via a 1s interval with cleanup + **End → Minutes** = `POST /end`); a 3-column grid:
  **agenda spine** (click-to-select, done-check from `outcome`, `aria-current` on the running item),
  **active-item workspace** (key/urgent/title + `actual/timebox` time; a **discussion notes** textarea →
  `POST /discussion` on explicit Save/onBlur, empty/unchanged bodies never sent; an **actual-time** control —
  minutes input + outcome `Select` → `POST /…/actual-time`; the **Record decision / Create action / Call
  vote** buttons are disabled stubs → P7/P8/P9), and an **attendance** aside (roster = active `/api/members`
  merged with `meeting.attendance` by `publicId`; a present/absent toggle → `POST /attendance`; a client-side
  quorum *display* heuristic).
- `api/meetings.ts` — typed `attendance: AttendanceEntry[]` + `discussions: Discussion[]`; added
  `useStartMeeting`/`useEndMeeting`/`useMarkAttendance`/`useCaptureDiscussion`/`useRecordActualTime` (each by
  meeting id, invalidating the detail). Enums (`AttendanceRole`/`AttendanceStatus`/`AgendaItemOutcome`) travel
  as string names; committee role → AttendanceRole is mapped client-side (Chairman→Chair, …, else Guest).
- `AgendaBuilder.tsx` — its internal breadcrumb moved up to `MeetingPage` (no duplicate); all 11 P6c tests
  stay green. `meetings.css` extended (logical-properties-only). Full EN+AR `meetings.*` additions (parity 389).

**Decisions / drift (design = visual SoT; package = behavior SoT; recorded in the file headers).**
- **In-page shared `Tabs`** instead of the design's top-bar tab switcher; the breadcrumb drops the design's
  third "Agenda builder" segment (the active tab conveys it).
- **Pause / RTE toolbar / "Autosaved" pill / "Captured on this item" / inline quick-create are mock chrome** →
  omitted or disabled. Discussion save is **explicit** (Save note + onBlur) → `POST /discussion`; a "Saved"
  indicator follows a successful save (there's no separate autosave endpoint).
- **Record decision / Create action / Call vote** are disabled stubs → P7/P8/P9.
- **"End → Minutes"** ends the meeting (`POST /end`) and navigates to `/meetings` — the Minutes screen is P7,
  so no minutes UI is built here (chosen over a tab-flip to avoid an already-InProgress-meeting landing bug).
- **Quorum is a client-side display heuristic** (majority of voting-eligible present), never gates an action —
  the authoritative quorum gate is Voting (P9).
- **Attendance roster** sourced from `/api/members` (active), seeded server-side on first mark; `member.publicId`
  is the attendance `userId` (matches the MeetingsDbContext "attendee = CommitteeMember.PublicId" rule).
- **No new ADR** (UI on the settled stack).

**Verification (deterministic, green).** Web **168/168** (Vitest+RTL behaviour: tab switch, start-meeting gate +
call, live workspace render, spine selection, discussion save asserting the capture call + single-POST dedup,
actual-time + outcome record, attendance present/absent toggle, the disabled stubs, **+ axe WCAG 2.2 AA** on the
workspace), i18n parity 389, `tsc -b`, vite build (140 modules), oxlint (only the pre-existing untouched
`Toast.tsx` warning), `meetings.css` grep = zero physical properties (RTL-safe). **Not yet run:** the live
authenticated browser pass (real start → attendance/discussion/actual-time → end, AR/RTL + dark, live axe) —
recommended, and it needs a scheduled+published meeting (pairs with the deferred schedule flow / a seeded meeting).

**Acceptance audit (this entry).** **No verdict flips** — P6d is the UI for the W7–W9 workflows whose ACs are
already covered by the P6a backend; the meeting screens add a new surface to the localization/a11y ACs
(AC-040/045/046 render RTL-mirrored + axe-clean in the component tests; AC-041 stays Partial → VR P17).
AC-051/053 still Partial → P6e.

**Next.** **P6e** — wire `NotificationCenter.tsx` to the live `/api/notifications` feed (bell badge + list +
mark-read), flipping AC-051/053 toward Met. Then the deferred meeting-schedule flow, the optional `/api/meetings`
+ `/api/notifications` WebApplicationFactory integration tests, and the `feat/P6-meetings` PR.

---

### 2026-06-27 — P6c UI: Agenda builder (the design's agenda tab) wired to the Meetings API + a meetings list

**Scope.** The **Agenda Builder** screen — the `isAgenda` block of the local design
`/ACMP product context/ACMP Agenda & Meeting.dc.html` — composed from the shared component library and
wired to the P6a Meetings API, plus a read-only **Meetings list** to reach it. The live meeting workspace
(the design's `isMeeting` tab) is **P6d**. Branch `feat/P6-meetings`. Web **151 tests green** (was 94; +57
across the suite — 17 new meetings tests incl. 2 axe cases), i18n parity **344**, `tsc -b` + vite build +
oxlint clean.

**Done.**
- `api/meetings.ts` — typed hooks mirroring `api/topics.ts` (read-by-key, mutate-by-id, query invalidation):
  `useMeetings`, `useMeetingDetail(key)`, `usePreparedTopics` (the pool), and the agenda mutations
  (`useAddAgendaItem`/`useRemoveAgendaItem`/`useMoveAgendaItem`/`useSetTimebox`/`useAssignPresenter`/
  `usePublishAgenda`) DRY'd through a shared `useAgendaMutation` that invalidates the meeting detail + the
  Prepared pool on success.
- `features/meetings/AgendaBuilder.tsx` (route `/meetings/:key`) — the design screen: breadcrumb; header
  (title + Draft/Published `StatusChip` + when/length); a **time-budget bar** (server-summed used minutes vs
  the meeting's scheduled duration, over/under-coloured via `--st-*`, a `role="progressbar"`); a two-column
  grid — **left** the Prepared-topics pool (count, search, draggable Add cards, empty state) and **right** the
  agenda items (drop zone, empty state, per-item: index, key, urgent pill, title, a **timebox −/+ stepper**, a
  **presenter `Select`**, **move up/down**, **remove**); and the **publish confirm dialog** (items + minutes →
  `usePublishAgenda`). Four states (loading/error/not-found/live) driven by the query.
- **AC-044 keyboard reorder.** The move up/down buttons are the accessible reorder path (each sends a single
  ±1 `move`), disabled at the ends, with a synchronous **`aria-live`** announce; native HTML5 drag is
  progressive enhancement on top. Unit-tested (asserts the ±1 mutation + the announce).
- `features/meetings/MeetingsList.tsx` (route `/meetings`, replacing the placeholder) — composed list of
  scheduled meetings linking to each builder; honest empty state.
- `features/meetings/meetings.css` — **logical-properties-only**, token-driven (RTL-safe by construction,
  grep-verified: zero physical left/right/margin/padding).
- i18n: full `meetings.*` EN+AR namespace (real Arabic, parity 344); 5 new icons lifted from the design;
  routes wired in `App.tsx`.

**Decisions / drift (design = visual SoT; package = behavior SoT; recorded in the file header comment).**
- **Pool labeled "Scheduled topics" (design) but sourced from the PREPARED backlog** (`GET /api/topics?status=
  Prepared`) — topics only become Scheduled when the agenda is published; items already placed are deduped out
  of the pool (they'd otherwise show in both columns pre-publish).
- **±1 reorder only** (the AC-044 contract): the buttons/keyboard carry the behavior; an in-agenda pointer drag
  fires a single ±1 nudge toward the drop target, never N chained moves; a free-position drag would need an
  absolute-index `move` variant on the backend.
- **"Preview" button + the dialog's "notify groups" checkboxes + the RTE toolbar are mock chrome** — Preview is
  rendered disabled; the publish dialog shows one honest "all committee members will be notified" line (the
  backend notifies everyone unconditionally — P6b).
- **Presenter** is an accessible shared `Select` sourced from `GET /api/members` (replaces the design's
  avatar-cycle). A member's `publicId` **is** the `presenterUserId` the agenda stores (confirmed against the
  MeetingsDbContext "presenter = CommitteeMember.PublicId" rule — not an assumption).
- **Scheduling a NEW meeting is deferred** — it needs committee + chair pickers and `committeeId` isn't exposed
  to the SPA yet; the design shows no schedule screen. The list's empty state is honest about it.
- **No new ADR** (UI on the settled stack).

**Verification (deterministic, green).** Web **151/151** (Vitest+RTL behaviour: add-from-pool, move ±1 +
announce, timebox step, remove, publish dialog → publish, loading/empty/not-found, **+ axe WCAG 2.2 AA** on
both new screens), i18n parity 344, `tsc -b`, vite build (138 modules), oxlint (only the pre-existing
untouched `Toast.tsx` fast-refresh warning). **RTL-safety** confirmed deterministically (logical-CSS grep).
**Not yet run:** the live authenticated browser pass (real `GET /api/meetings/{key}` + the agenda mutations,
AR/RTL + dark, live axe) — recommended, and it needs a scheduled meeting (so it pairs with the deferred
schedule flow or a seeded meeting).

**Acceptance audit (this entry).** **AC-044 Partial → Met** — the keyboard-accessible agenda reorder
(move-up/-down → ±1, disabled at ends, `aria-live` announce) is shipped and unit-tested, with the jsdom axe
case clean; the live browser axe/RTL pass is the confirmatory step. AC-040/045/046 gain a new surface (the
meetings screens render RTL-mirrored + axe-clean in the component tests); AC-041 stays Partial (automated VR →
P17). AC-051/053 stay Partial → P6e.

**Next.** **P6d** — the live meeting workspace (the design's `isMeeting` tab: agenda spine, attendance,
discussion notes, actual-time; stub record-decision/create-action/call-vote → P7–P9). **P6e** — wire
`NotificationCenter.tsx` to the live feed. Then the deferred meeting-schedule flow, the optional
`/api/meetings` + `/api/notifications` integration tests, and the `feat/P6-meetings` PR.

---

### 2026-06-27 — P6b backend: in-app Notifications module + the agenda-publish / meeting-schedule fan-out (AC-051/053 floor)

**Scope.** The in-app notification floor for P6 (AC-051 + AC-053 only — preferences/digests/reminder-Hangfire/
Webex stay deferred). A new `Notifications` module (the v1 `INotificationChannel`), plus the cross-module
`ICommitteeDirectory` seam so the Meetings publish/schedule handlers fan out to the committee roster without
reading Membership's tables. Branch `feat/P6-meetings`; **397 tests green** (Domain 42 · **Architecture 16** ·
Application 319 · Api 20), up from 388. Solution + `dotnet format` clean.

**Done.**
- **Notifications module** (Domain → Application → Infrastructure, mirroring the established module pattern):
  - `Notification : AuditableEntity` — bilingual `LocalizedString` Title/Body, Category, optional DeepLink,
    IsRead/ReadAt; `Create(...)` + idempotent `MarkRead(now)`. Referenced externally by PublicId (inbox items
    have no human-readable display key).
  - `InAppNotificationChannel : INotificationChannel` — the single registered channel (ADR-0005, AC-053): a
    **synchronous** write of one row per `PublishAsync` (≤5s for a ≤20-user committee — no queue/Hangfire).
  - Reads scoped to `ICurrentUser`: `GetNotifications` (the signed-in user's own feed, newest-first, bounded
    to 50, with an unread count) and `MarkNotificationRead` (filters by PublicId **AND** RecipientUserId — the
    IDOR guard, guardrail 4 — and 404s on a miss so a stranger's id leaks nothing).
  - `NotificationsDbContext` (schema `notifications`, owned bilingual columns, `RecipientUserId,IsRead` index);
    migration `Notifications_P6_Initial`; wired into `Program.cs` + `MigrationRunner` (the 4th context).
  - `GET /api/notifications` + `POST /api/notifications/{id}/read` — authentication-only (the per-user scope in
    the handlers *is* the authorization; no role policy).
- **Cross-module seam** `ICommitteeDirectory` (`Acmp.Shared/Contracts/Membership`) → `GetActiveMembersAsync`
  returning `(UserId, FullName)`. Implemented in **Membership.Infrastructure** (`CommitteeDirectory`, reads
  active members only — AC-058 disabled members get nothing), registered alongside the ABAC ports. Same shape
  as `ITopicScheduler`.
- **Fan-out wiring.** `ScheduleMeetingHandler` (→ `MeetingScheduled`) and `PublishAgendaHandler` (→
  `AgendaPublished`) now inject `ICommitteeDirectory` + `INotificationChannel` (both Shared) and deliver one
  bilingual notification per active member after the governance write + audit. **AC-051 content contract:** the
  `AgendaPublished` body carries the **meeting date + agenda title** and the message carries a **deep link**
  (`/meetings/{key}`) to the agenda view. `PublishAgendaHandler` now also loads the Meeting for that content.
- **ArchUnit.** Notifications added to the parameterized Clean-Architecture rules + a new
  `Notifications_should_not_depend_on_other_modules` leaf fact; the Meetings isolation fact now also forbids any
  Notifications-assembly edge — proving Meetings→Notifications composes purely through the Shared interface
  (12 → 16 facts).

**Decisions / drift (no silent drift, guardrail 11).**
- **No new ADR** — new module + cross-module contract on the settled architecture; ADR-0005's `INotificationChannel`
  contract is now first-implemented.
- **Content is bilingual `LocalizedString`** built in the Meetings handler (guardrail 9), with the user-content
  meeting title embedded verbatim into both languages and the date as an invariant Gregorian `yyyy-MM-dd`. The
  SPA's notification-center *labels* (and any per-locale date reformat) land with the UI in P6e.
- **MeetingScheduled has no AC content contract** (phase-scope only) — a sensible bilingual heads-up, not
  over-specified; the AC-051 three-field contract applies to `AgendaPublished`.
- **ponytail ceilings (commented at the code):** a **re-publish re-notifies** every member (notifications aren't
  deduped — intentional: a changed agenda is worth re-announcing); the schedule/publish write and the
  notification write are **separate DbContexts / not one transaction** (acceptable at committee scale — the
  governance record + audit are the source of truth; a failed fan-out doesn't roll back the meeting).

**Verification (deterministic, green).** Backend **397/397** (Domain 42 · Architecture 16 · Application 319 ·
Api 20), 0 skipped — cross-module isolation (Meetings ⟂ Topics ⟂ Membership ⟂ Notifications) enforced by
ArchUnit. New tests: 4 NotificationHandlerTests (channel write, current-user-scoped feed + unread count,
mark-read, **IDOR — user B cannot mark user A's item read**) + the **AC-051 fan-out content assertion** in
MeetingHandlerTests (2 members → 2 messages, each `AgendaPublished`, deep link `/meetings/MTG-2026-001`, body
contains the meeting title + date in EN *and* AR). `dotnet build` + `dotnet format --verify-no-changes` clean.
**Not yet run:** live SQL-Server apply of `Notifications_P6_Initial` + an authenticated `/api/notifications`
round-trip — covered when P6e wires the live notification center (and the optional `/api/meetings` integration tests).

**Acceptance audit (this entry).** **AC-051 / AC-053 Pending → Partial.** The in-app channel, the
publish-time fan-out to every active member, the three-field content contract, and channel-exclusivity (a single
registered `INotificationChannel`, no email/Webex attempted) are all built and unit-proven. They stay Partial
until the live HTTP round-trip + the notification-center UI render (P6e) demonstrate "appears in the recipient's
notification center within 5s" end-to-end. AC-052 (vote-open deep link) stays Pending → P9 (the deep-link
*mechanism* exists; the vote notification is raised in the Voting phase).

**Next.** **P6c / P6d** — the agenda builder + live meeting workspace UI (match the local
`/ACMP product context/ACMP Agenda & Meeting.dc.html`, compose the shared library, EN+AR+RTL, axe AA; AC-044's
keyboard-accessible agenda reorder lands here). **P6e** — wire `NotificationCenter.tsx` to the live feed (bell
badge + list + mark-read), flipping AC-051/053 toward Met. Then the optional `/api/meetings` +
`/api/notifications` WebApplicationFactory integration tests, and the `feat/P6-meetings` PR.

---

### 2026-06-27 — P6a backend complete: Meetings module (domain → application → infrastructure → API) + cross-module scheduler seam

**Scope.** The backend half of P6 — agenda building, meeting scheduling/lifecycle, attendance, discussion
notes, and actual-time tracking (workflows W5–W9) — built as a new `Meetings` module on the established
modular-monolith pattern. The UI (P6c/P6d) and in-app Notifications floor (P6b) follow. Branch
`feat/P6-meetings` (2 commits); **388 tests green** (Domain 42 · **Architecture 12** · Application 314 ·
Api 20), 0 skipped — module boundaries intact. Solution builds.

**Done.**
- **Domain** (commit `befb496`). `Meeting` aggregate — full lifecycle (docs §5): Scheduled → InProgress →
  Completed, with Cancel; **owns Attendance + Discussion** child collections; `StartMeeting` requires a
  Published agenda (W7). `Agenda` aggregate — Draft → Published (versioned on re-publish); **owns
  `AgendaItem`** (timebox bounds, presenter snapshot, urgent flag). `Agenda.MoveItem(topicId, ±1)` is the
  **AC-044** reorder primitive (pointer drag and keyboard move-up/-down both send a ±1 delta). Lifecycle
  events raised. **Cross-module identity is by id + display snapshots only** (topic key/title/urgent,
  presenter id+name) — Meetings never reads another module's tables (ADR-0001). 42 domain unit tests.
- **Application** (commit `eeb9edf`; MediatR slices, FluentValidation, `IAuditSink` on every governance
  transition, `IAuthorizedRequest` RBAC per command): ScheduleMeeting (W5, creates the Meeting + an empty
  Draft Agenda), CancelMeeting; the agenda builder micro-commands (add/remove/**move ±1**/timebox/presenter,
  W6); PublishAgenda (W6 — versions the agenda then advances each placed topic Prepared → Scheduled via the
  seam below); StartMeeting/EndMeeting (W7), MarkAttendance (W8), CaptureDiscussion (W9), RecordActualTime;
  plus GetMeetings / GetMeetingDetail reads. Builder edits are not individually audited — the governance
  event is `AgendaPublished`; `MeetingScheduled` + `AgendaPublished` are the two notification hooks for P6b.
- **Cross-module seam** (ADR-0001). New `ITopicScheduler` contract in `Acmp.Shared/Contracts/Topics`,
  **implemented in `Topics.Infrastructure`** (`TopicScheduler`) against the Topics DbContext — mirrors how
  Membership implements the grant-on-accept writer for Topics. Both methods are **idempotent** (a topic not
  in the expected source state is left untouched): `ScheduleAsync` (Prepared → Scheduled on agenda publish)
  and `EnterCommitteeAsync` (Scheduled → InCommittee on meeting start). So a re-publish or mid-loop retry
  never throws. Meetings advances topic lifecycle without ever touching Topics' tables.
- **Infrastructure.** `MeetingsDbContext` (schema `meetings`) — attendance/discussion/agenda-items as owned
  child tables; enums as int. Forward-only migration `Meetings_P6_Initial`; `MeetingKeyGenerator` (gap-free
  `MTG-YYYY-###` / `AGN-YYYY-###`). Wired into `Program.cs` (module registration + MediatR assembly) and
  `MigrationRunner` (third context).
- **API.** `MeetingsEndpoints` — schedule/cancel, agenda build/reorder/timebox/presenter, publish,
  start/end, attendance, discussion, actual-time, + meetings list/detail; **policy-gated per docs/domain/permission-role-matrix.md**
  (Meeting.Schedule, Agenda.Publish, Attendance.Record, Minutes.Capture). 20 API/handler tests.
- **ArchUnit.** Boundary tests extended to enforce Meetings module isolation (8 → 12 tests, all green).

**Decisions / drift (no silent drift, guardrail 11). Settled, do not re-derive:**
- **No new ADR** — new module on the settled architecture; no architecture change.
- **MoM / minutes screen is P7**, out of P6 scope (the meeting workspace stubs record-decision / create-action
  / call-vote → P7–P9).
- **Notification scope floor = AC-051 + AC-053 only** for P6b; preferences, digests, reminder-Hangfire jobs,
  and the Webex channel are deferred (ADR-0005 / docs/domain/data-architecture.md). The ≤5s constraint (AC-051) is met by a synchronous
  in-app write.
- **StartMeeting requires a Published agenda** (W7) — enforced in the domain.
- **Attendance roster is seeded client-side via `MarkAttendance`** (name/role from the SPA, which sources
  `/api/members`); Meetings stores attendance display snapshots, it never reads Membership.
- **The agenda builder's "Prepared topics" pool comes from the Topics API**, not Meetings — the builder passes
  topic id + display snapshots into `AddAgendaItem`.

**Verification (deterministic, green).** Backend **388/388** (Domain 42 · Architecture 12 · Application 314 ·
Api 20), 0 skipped — cross-module isolation (Meetings ⟂ Topics ⟂ Membership) enforced by ArchUnit. Handler
tests run against a real InMemory context with a faked `ITopicScheduler`. **Not yet run:** live SQL-Server
migration apply + an authenticated `/api/meetings` round-trip (WebApplicationFactory integration tests are
the optional P6 tail); the InMemory handler tests don't exercise the owned-table persistence on real SQL.

**Acceptance audit (this entry).** **AC-044 Pending → Partial** — the backend reorder is built and tested
(the `MoveAgendaItem` ±1 command + `Agenda.MoveItem`, the path a keyboard move-up/-down drives); the
**keyboard-accessible agenda reorder UI** itself lands in P6c (mirrors the AC-043 backend-then-UI split).
**AC-051 / AC-053 stay Pending → P6b** (the in-app Notifications backend: the channel + the publish/schedule
fan-out). No other verdicts flip — P6a is a server surface.

**Next.** **P6b** — in-app Notifications backend (the AC-051/053 floor): a Notifications module + an
`InAppNotificationChannel : INotificationChannel`, `GET /api/notifications` + mark-read, and an
`ICommitteeDirectory` (Shared contract, implemented in Membership) to resolve "all committee members"; fire
`AgendaPublished` (from PublishAgendaHandler) and `MeetingScheduled` (from ScheduleMeetingHandler) — the
hooks are already noted in those handlers. Then P6c/P6d (agenda builder + live meeting workspace UI), P6e
(wire the NotificationCenter shell to the live feed), and the optional `/api/meetings` integration tests.

---

## P5 review remediation — design-fidelity fixes + AC-043 correction

### 2026-06-27 — Fixed every finding from the pre-advance P5 audit (all severities)

**Why.** A pre-advance P5 review (Topics & Backlog) flagged 3 MAJOR design-fidelity defects, a batch of MINOR
drifts, one acceptance over-claim, and a few process items. This slice fixes all of them. Branch
`fix/P5-review-remediation`. Visual SoT = the local `/ACMP product context/ACMP Backlog & Topic.dc.html`
(read directly); inter-file primitive conflicts resolved by the Design System file (the documented authority).

**Design fidelity.**
- **MAJOR — detail affected-streams chips** now render in the **info** tone (blue `st-info`), matching the
  design (systems stay neutral). Added a `tone` prop to the shared `Tag`.
- **MAJOR — urgency selection cards** are color-coded by their **semantic urgency** (normal=info, urgent/
  critical=danger) with a soft dot ring, not the generic accent/primary-tint (type cards keep accent).
- **MAJOR — shared `.status-chip`** corrected to Design System §08 (**22px / pad-inline 8 / 11.5px**; was
  24/9/12) — benefits every screen; **shared table cell padding 16→12px** to match the reference.
- **MINOR** — backlog table column widths (key 112 / type 124 / owner 140 / status 104 / urgency 84); type &
  age cells 12.5px; search input 34h/210w; submit fieldset 22px inline padding; **table-shaped loading
  skeleton** (replacing the generic one); empty-state **search** icon; dropzone **upload** icon (was a
  download/down-arrow); one-row title hint+counter (hint kept associated via `aria-describedby`); detail
  discussion-count **badge** (was inline "(3)"); compose **avatar**; history timeline dot 11px + double ring.
- **Copy** — backlog count → "the total value active topics"; autosave → "Saved · just now"; dropzone → "Drop
  files here or click to upload" (EN+AR, parity preserved at 278).
- **Left unchanged (Design-System authority):** shared button (38/9/13.5), input (38/12/9), segmented
  (30/14/7) already match the DS file; the backlog screen's slightly tighter values are an inter-file delta —
  forking the primitives would regress the DS and other screens (guardrail 11/14 reconciliation, recorded).

**Acceptance correction (no silent over-claim, guardrail 11). AC-043 Met→Partial.** The kanban "M" move
popover is a keyboard alternative for **status-bucket** moves, not the AC's literal **priority-ordinal
move-up/down with a persisted ordinal**. Priority reordering (BL-039 within-column reorder, BL-041 ordinal +
keyboard alt) is **deferred** to a focused follow-up; the `SortableList` primitive exists but is not yet
wired into the backlog. Audit table + summary updated.

**Process.**
- Fixed the SubmitTopic test's swallowed `isPending` render error (`afterEach` no longer `mockReset()`s the
  mutation mock → no undefined return on a trailing re-render). Run log is now clean of that throw.
- **OpenTelemetry 1.10.0 → 1.12.0** (latest). The **NU1902 moderate advisory GHSA-4625-4j76-fww9 has no
  patched release** (1.12.0 is still flagged) — **accepted**: the OTLP exporter is **internal-only egress** to
  the bundled Seq sidecar (CON-001), and the DoD blocks only high/critical. Revisit when upstream ships a fix.
- **Recommended, not done (flagged, not silently dropped):** the live 4-theme × 2-width render pass and
  re-enabling axe `color-contrast` in the component tests remain confirmatory follow-ups (carried from the
  audit; jsdom can't compute contrast).

**Verification (deterministic, green).** Web **94/94** (22 files); backend **358/358** (Domain 23 ·
Application 307 · **Architecture 8** · Api 20), 0 skipped — **module boundaries intact**; i18n parity **278**;
`tsc -b` + vite build clean (164 kB gz JS < 300 kB budget). No new ADR (UI + dependency bump on the settled stack).

**Next.** Optional live visual pass, then **P6 — Agenda & Meetings**.

---

## CHANGE-004 — Keycloak access-token `sub` claim (JIT provisioning fix)

### 2026-06-26 — Fixed: `acmp-web` access token had no `sub`, silently breaking JIT provisioning + subject identity

**Symptom.** The committee member directory was empty and `POST /api/members/me` (JIT, ADR-0004) threw
`UnauthorizedAccessException("Authentication required")` for every caller — `ICurrentUser.UserId` resolved
empty. Surfaced during the P5b PR4 live kanban pass: the accept owner-picker had no candidates.

**Root cause.** `acmp-web`'s `defaultClientScopes` was `["openid","profile","email","roles"]`. `"openid"` is
the OIDC request scope, not a client scope; and **`"basic"` was missing**. In Keycloak 24+ the `sub` (and
`auth_time`) claim lives in the built-in **`basic`** client scope — so without it the access token carried
`preferred_username`/roles/`aud` but **no `sub`** → `ICurrentUser.UserId` (`NameIdentifier ?? sub`) was empty
→ the JIT guard threw. Topics handlers only *appeared* healthy: they display the `name` claim, so their
subject/actor id was silently empty too — a latent identity bug across every subject-dependent path
(JIT, subject-scoped ABAC, actor attribution).

**Fix (two parts, no new ADR — config-bug fix; ADR-0004/0015 stand).**
1. **Keycloak realm** (`deploy/keycloak/realm-export.json`, the bundled-realm SoT, ADR-0015): `acmp-web`
   `defaultClientScopes` → `["basic","profile","email","roles"]`. Fresh `docker compose up` now emits `sub`.
   Applied to the **running dev realm** via the Keycloak admin API (added the `basic` default client scope)
   for immediate verification.
2. **SPA wiring** (`AuthProvider`): the documented "SPA calls `POST /me` on login" was never implemented, so
   JIT never ran. `OidcBridge` now calls `POST /api/members/me` once per authenticated session (idempotent
   provision-or-sync). No `CurrentUserService`/handler change — they were correct once `sub` is present.

**Live verification (real Keycloak PKCE, AR/RTL).** Re-login → token now carries `sub` (`hasSub: true`);
`POST /api/members/me` → **200**, provisioning "ACMP Administrator" (Secretary); `GET /api/members` → **1**.
End-to-end the kanban accept then worked: keyboard **M-move → AcceptDialog → owner "ACMP Administrator" →
POST /accept → 204**; TOP-2026-002 → status **Accepted**, owner assigned, re-bucketed to Accepted —
**grant-on-accept (AC-009) proven live through the UI**.

**Impact.** Unblocks live JIT provisioning (makes AC-002's JIT actually function end-to-end), subject-scoped
ABAC, and `sub`-based actor attribution — not just the kanban accept. Web 94/94, build/oxlint clean.

---

## P5 — Topic & Backlog Management

### 2026-06-26 — P5b PR4: Backlog kanban + accessible DnD (triage transitions) — final P5b slice

**Scope.** Last P5b slice — the **kanban view** with accessible drag-and-drop, replacing the "coming soon"
shell. Branch `feat/P5b-kanban`. Web **94 tests green** (was 87; +7 kanban/meta), i18n parity 278, oxlint +
build clean. With this, all three design screens (backlog 3 live views, submit, detail) are built.

**Done.**
- `topicMeta.ts` — pure, unit-tested **bucket model**: `bucketOf(status)` (canonical status → 5 display
  buckets, P5a decision) and `moveAction(from,to)` (classifies a move as accept/return/illegal/none).
- `api/topics.ts` — `useAcceptTopic` (POST `/accept`, owner) + `useReturnTopic` (POST `/reject` or `/defer`,
  reason + optional revisit).
- `features/topics/Kanban.tsx` — 5-column board grouping the backlog page by bucket; **native HTML5 drag**
  (pointer) + a **keyboard "M" move popover** (AC-043) + an **aria-live** region announcing every move; cards
  link to detail. Wired into the backlog view switcher (kanban is now a live view; calendar/timeline stay shells).
- **Transition dialogs** (the only P5-legal cross-bucket moves): **AcceptDialog** (owner `Select` from the
  member directory → POST `/accept`) and **ReturnDialog** (defer/reject radio + required reason + native date
  for the defer revisit → POST `/reject`|`/defer`). Illegal drops (→scheduled/→done/etc.) are **rejected with
  an announced reason**, never a silent no-op.

**Decisions / drift (design = visual SoT; package = behavior SoT; in the file header comment).**
- **No input-free cross-bucket move exists in P5**, so every legal move opens a dialog and **two columns reject
  all drops** (scheduling needs a Meeting → P6; there's no decide/close/un-accept endpoint). This is the
  design-conflict flagged at P5b kickoff, made concrete.
- **Native drag + "M" popover** (matches the design) rather than `@dnd-kit` multi-container; the popover is the
  keyboard-accessible path (AC-043).
- **Native `<input type="date">`** for the defer revisit (vs the heavy custom DatePicker) — simpler, accessible.
- **No new ADR** (UI on the settled stack).

**Verification.** Web **94/94** (Vitest+RTL: bucket/move mapping, column grouping, M-popover → accept-dialog →
accept with owner, illegal-move announce, return-with-reason), i18n parity 278, oxlint, `tsc -b` + build.
- **AA contrast** — kanban text is `--text-2`/`--text` on `--surface`/card (pass); the lone `--text-3` is the
  disabled "current" move item (WCAG-exempt, and 4.74 anyway). **RTL-safety** confirmed (logical-CSS audit).
- **Live kanban pass — done (2026-06-26, Playwright on the rebuilt `web`, real Keycloak PKCE, AR/RTL).**
  Verified live: the board renders with correct bucketing (فرز/Triage = 2, others 0), the keyboard **"M"**
  opens the move popover (current bucket disabled, others enabled — AC-043 live), and picking **مقبول/Accepted**
  routes to the **AcceptDialog** ("قبول TOP-2026-002…") with the owner picker. **Finding (dev-data gap, not a
  bug):** the accept can't be completed end-to-end because the member directory is empty in the dev DB
  (`GET /api/members` → 200 `[]`), so the owner picker has no candidates — the dialog correctly requires an
  owner. The transition POSTs (accept/reject/defer) are unit-tested (`Kanban.test`) and server-tested (P5a);
  exercising a live accept needs a provisioned committee member (Membership). Recorded for the next live run.

**Acceptance audit (this entry).** **Met (newly): AC-043** (keyboard DnD alternative on the backlog — the "M"
move popover, unit-tested). **AC-009** advances (owner assignment via the accept dialog is wired to the
grant-on-accept endpoint; live grant/403 → live pass). AC-031 (mandatory rejection reason) is now collected in
the UI. AC-044 (keyboard DnD on the agenda) stays Pending → P6.

**Next.** Live kanban pass (optional), then **P5b is complete** — all three screens shipped. Remaining topic
work (per-topic live **edit**/lock for AC-034, calendar/timeline once P6 meeting data exists, saved-views/export)
is tracked for later phases.

---

### 2026-06-26 — P5b PR3: Topic detail (read + discussion + history) wired to GET /api/topics/{key}

**Scope.** Third P5b slice — the **Topic detail** screen. Branch `feat/P5b-detail`. Web **87 tests green**
(was 79; +8 TopicDetail incl. its axe case), i18n parity 249, oxlint + build clean.

**Done.**
- `api/topics.ts` — `useTopicDetail(key)` (`retry:false` so an unknown key surfaces "not found" immediately)
  + `useAddTopicComment` (POST `/{id}/comments`; body field is `reason`, BL-033).
- `features/topics/TopicDetail.tsx` — header (key chip, status chip, urgent chip, title, owner, created date
  via `Intl`/Gregorian); tabs **Overview / Discussion / History**; Overview (description, justification,
  affected streams/systems, attachments when present); **Discussion** (comment list + compose → live POST by
  the DTO's Guid id); **History** (status-event timeline, localized `from → to` + reason + actor·time); the
  **empty Relationships sidebar**. The `topics/:key` route now resolves to it (replacing the interim placeholder).

**Decisions / drift (design = visual SoT; package = behavior SoT; in the file header comment).**
- **Single-language title** — the design's alt-language line is dropped (P5a).
- **Relationships sidebar is EMPTY in P5** — topic→decision/ADR/action/risk links land later; the aside shows
  its header + an honest empty state, no fabricated links.
- **Add-to-agenda** (needs a Meeting → P6) and **Edit** (AC-034 edit flow → a focused follow-up) are disabled
  affordances; the read view + comment posting are this slice's live behavior.
- **Attachments surfaced in Overview** when present (real topic data; the design's static overview omitted them).
- **No new ADR** (UI on the settled stack).

**Verification.** Web **87/87** (Vitest+RTL: read, urgent chip, tab switch, comment POST by id, history
timeline, 404 not-found, loading + **axe WCAG 2.2 AA**), i18n parity 249, oxlint, `tsc -b` + build.
- **AA contrast verified offline** both themes — fixed three `--text-3`-on-`--bg-app` spots (`.dt-section-label`,
  `.dt-tl-meta`, `.sub-file-meta` = 4.02 < AA, the exact CHANGE-003 value) → `--text-2`.
- **RTL-safety** confirmed (logical-properties-only audit).
- **Pending — live detail pass** (real `GET /{key}` + comment POST, AR/RTL + dark): the detail read/comment
  path is unit-tested with mocks, not yet run end-to-end. Recommended before merge.

**Acceptance audit (this entry).** **No verdict flips** — read + comment-display surface. **AC-009/034** (owner
is shown; live per-topic **edit**/lock) stay Partial — the edit flow is a deliberate follow-up slice. The
History tab surfaces the read side of AC-032's immutable status/rejection events. BL-033 comment posting is now
live in the UI.

**Next.** Live detail pass, then **PR4** — Kanban + all DnD incl. AC-043 (the last P5b slice).

---

### 2026-06-26 — P5b PR2: Submit topic form (W1) wired to POST /api/topics

**Scope.** Second P5b slice — the **Submit topic** screen matching the design's submit screen. Branch
`feat/P5b-submit`. Web suite **79 tests green** (was 72; +7 SubmitTopic incl. its own axe case), i18n parity
226, oxlint + build clean. Also resolves the **auth-bootstrap 401** found in PR1 (shipped separately as #12,
already on `main`).

**Done.**
- **Router migrated to a data router** (`createBrowserRouter(createRoutesFromElements(...))`, keeping App's
  JSX route tree) so `useBlocker` is available for the unsaved-work guard (AC-047). Providers unchanged.
- `api/topics.ts` — `useSubmitTopic` (POST) + `uploadTopicAttachment` (multipart, field `file`).
- `features/topics/SubmitTopic.tsx` — sticky section nav + 5 fieldsets (Type & title / Justification /
  Scope / Attachments / Urgency); **4 type cards** + **3 urgency cards** (canonical taxonomies);
  title counter; client-side **localized required-field validation** (AC-030/049 display); free-text
  **token inputs** for streams & systems; **drop-zone file staging** with a 50 MB client check, uploaded to
  the new topic on submit (AC-049/050 path); **autosave to localStorage** with a live indicator; **Save draft**.
- **Unsaved-work guard:** `useBlocker` route-change guard → confirm Dialog (AC-047); `beforeunload` listener
  when dirty (AC-048). Programmatic post-submit / save-draft navigation bypasses the guard via a ref.
- On submit: POST → upload staged files → clear draft → redirect to the new topic's detail route.

**Decisions / drift (design = visual SoT; package = behavior SoT; in the file header comment).**
- **No Scope/Source picker** — `source` defaults to `CommitteeMember`, Scope is derived server-side (P5a).
- **4 types / Urgency Normal·Urgent·Critical** (canonical), not the design's 3 + "low".
- **Plain textarea** for description — the design's rich-text toolbar is mock chrome; we store plain text.
- **Streams & systems are free-text token inputs** (no committed stream registry in the web yet), not the
  design's fixed stream toggle-chips — revisit when a streams endpoint exists.
- **Autosave is client-side (localStorage)** — there is no server draft endpoint in P5; the indicator and
  "Save draft" reflect that. The guard warns before leaving an unsubmitted topic (the draft is kept either way).
- **Section nav scrolls to fieldsets** (single scrollable form), not a multi-step wizard.
- **No new ADR** (UI on the settled stack; the router-config change is the same react-router, data-router mode).

**Verification.** Automated gate green: web **79/79** (Vitest+RTL behavior — validation, AC-039 locale-preserve,
AC-047 guard, submit payload, file-size reject — + **axe WCAG 2.2 AA**), i18n parity 226, oxlint, `tsc -b` +
vite build.
- **AA contrast verified offline** for the submit screen's text/bg combos (both themes); fixed three
  light-mode `--text-3` real-text spots that fell below 4.5:1 (`.sub-drop-hint`/`.sub-foot-note` on `--subtle`
  = 4.37; selected `.sub-card-desc` on `--primary-tint` = 4.15) → `--text-2` (CHANGE-003 precedent).
- **RTL-safety** confirmed (logical-properties-only audit of `topics.css`).
- **Live authenticated pass — done (2026-06-26, Playwright on the rebuilt `web`, real Keycloak PKCE).** Filled
  the form (type=ArchitectureDecision, title/description/justification, stream `platform`, a staged PDF) and
  submitted: `POST /api/topics` → **201** (TOP-2026-002); `POST /api/topics/{id}/attachments` → **201** —
  the multipart upload to **real MinIO** succeeded (closes the deferred "live MinIO → P5b", AC-049/050), and
  the `{id}` used for the attachment confirms the submit-returns-`{id,key}` → use-`id` flow. Redirected to the
  new topic; the guard correctly did not fire on the programmatic post-submit navigation. The submit form was
  also confirmed rendering in **AR/RTL** with full i18n (section nav, type/urgency cards, token inputs, drop
  zone, autosave indicator).

**Acceptance audit (this entry).** **Met (newly):** **AC-039** (locale switch preserves form data — unit-tested),
**AC-047** (in-app route-change guard via useBlocker — unit-tested). **Partial (newly):** **AC-048**
(`beforeunload` wired when dirty; native browser dialog isn't unit-testable in jsdom → live pass). AC-030 gains
a client-side localized-validation UI test ref (server-side localized messages still BL-016); AC-049/050 gain
the upload-wiring UI (live MinIO → the live pass). AC-009/034 (per-topic edit lock over the live UI) → PR3.

**Next.** Live authenticated pass (real submit + MinIO), then PR3 (Topic detail: header, Overview/Discussion/
History tabs, comment POST, empty relationships sidebar; AC-009/034 over the live UI).

---

### 2026-06-26 — P5b PR1: Backlog read path (table + list views) wired to GET /api/topics

**Scope.** First of four P5b slices (the design's three screens — backlog/submit/detail). PR1 ships the
**Backlog read path**: the `useBacklog` server-state hook + the Backlog screen (table & list views live,
full filter bar, four screen states, pagination, the SLA aging badge, and honest "coming soon" shells for
the not-yet-data-backed views). Branch `feat/P5b-backlog`. Web suite **72 tests green** (was 59; +13 Backlog
behavior, +1 axe), prod build + oxlint clean, i18n parity **175 keys**.

**Done.**
- `api/topics.ts` — `useBacklog(params)` (TanStack Query) over `GET /api/topics`; typed `TopicSummary` /
  `PagedResult`; repeated-`status` query binding; `placeholderData` keeps the page visible during refetch.
  Read-by-key vs mutate-by-id documented for the later slices.
- `features/topics/Backlog.tsx` — composed from the shared library (Breadcrumb, Segmented, Select,
  MultiSelect, Table, StatusChip, Tag, Pagination, states). **Table** (8 cols, API-backed sorts on
  title/status/age/urgency) and **List** (cards) live; search + Status/Type/Urgency filters functional;
  4 states (loading/error/empty/live) driven by the query; **SLA aging badge** from the DTO's `slaBreached`
  (AC-057 signal); pagination. `topicMeta.ts` holds the pure status→tone / initials mappers (unit-reusable).
- **Honest shells (agreed "live-3 + honest-shells" decision):** Kanban/Calendar/Timeline render a
  "coming soon" shell (kanban → PR4; calendar/timeline need meeting/decision data → P6); Export + Saved-views
  are disabled affordances. No faking data that doesn't exist yet.
- **Shared-component a11y fix (root cause):** `MultiSelect` input gained `role="combobox"` —
  `aria-expanded`/`aria-haspopup` are invalid on a bare textbox; surfaced by the new backlog axe case.
- i18n: full `topics.*` EN+AR namespace (parity green); 6 new view/toolbar icons; `/backlog` route wired;
  interim `topics/:key` placeholder route so row links don't 404 before PR3.

**Decisions / drift (design = visual SoT; package = behavior SoT; recorded in the file header comment).**
- The design's **Data: live/loading/empty/error** segmented is a mock preview toggle, not a product control —
  dropped (state comes from the query), like the dev role switcher.
- **Aging color is driven by `slaBreached`** (real time-in-status SLA, AC-057), not the design's raw age-day
  thresholds.
- **Only API-backed sorts** exposed (title/status/age/urgency); the design's Owner sort has no server sort.
- **Stream/Owner filters rendered but disabled** this slice — they need a verified option source (stream
  registry + owner directory keyed to topic owner ids); follow-up.
- **Row navigation = a title link** (accessible primary action), not a whole-row button (doesn't nest in
  table grid semantics).
- **Error state** drops the design's request-id line + Contact-support (no client request-id / support flow).
- **Re-slice:** priority reorder (SortableList) + kanban DnD + the "M" move (AC-043) moved to **PR4** so all
  DnD lands in one coherent slice; PR1 is the pure read path.
- **No new ADR** (UI on the settled stack).

**Verification.** Automated gate green: web **72/72** (Vitest+RTL behavior + **axe WCAG 2.2 AA** structure/ARIA
on the live table), i18n parity (175), oxlint, `tsc -b` + vite build, **remote CI green (PR #11)**.
- **AA contrast (the gap the jsdom axe gate skips) verified offline** for every backlog text/background combo
  in **both light and dark** — all clear 4.5:1 (lowest 5.35: `.bk-count` text-2 on the page bg; it would have
  failed at ~3.9 with `--text-3`, confirming the `--text-3`→`--text-2` fix was necessary).
- **RTL-safety confirmed deterministically** — `topics.css` uses logical properties only (zero physical
  left/right/margin/padding), so mirroring is guaranteed by construction (same approach as the shared components).
- **Authenticated live browser pass — done (2026-06-26, Playwright on the rebuilt `web` @ `localhost:8088`,
  authenticated as `acmp-admin` via real Keycloak PKCE).** `GET /api/topics` → **200**; TOP-2026-001 renders
  with the wire contract confirmed live: `GovernanceStandardization`→"Governance", `Submitted`, `Critical`
  (urgent marker), streams `identity`/`platform` tags, null owner→"Unassigned", `ageDays 0`→"0d", "Showing 1
  of 1". **EN-light** faithful to the design (breadcrumb, header, 5-view switcher, disabled Export/Saved-views,
  greyed Stream/Owner filters, 8-col table with Age sorted). **AR + dark**: full RTL mirroring (sidebar→inline-end,
  columns reversed, controls mirrored), dark theme, complete Arabic i18n; user-content title + stream codes
  correctly stay LTR. Confirms AC-040 RTL on a new surface and AC-057 aging badge end-to-end.
- **Finding (pre-existing, app-wide — not P5b-specific): hard-load/refresh/deep-link to a data route races the
  auth bootstrap.** A direct `GET` of `/backlog` (page reload) fired the query before the auth layer rewired the
  token getter → transient **401** → error state until **Retry** (then 200). Affects any data route on
  refresh/bookmark (the SPA's normal in-app nav keeps the token wired, so click-through works first try). Root
  fix belongs in the auth/query bootstrap (gate queries until the token getter is set, or expose `accessToken`
  and `enabled`-gate) — a shared-infra follow-up, deliberately **not** folded into this UI slice.
- **Minor nit:** the count reads "1 topics" — plural suffixes were avoided to keep EN/AR i18n key parity
  (Arabic has 6 plural categories); reword to a count-free phrasing later if desired.

**Acceptance audit (this entry).** **No verdict flips** — PR1 is a read-only surface; the headline ACs land
in later slices. AC-057 gains a UI test ref (badge now rendered in the backlog, unit-tested; stays Partial
pending live browser + breach-notification). AC-043 stays Pending (DnD → PR4); AC-039/047/048 → PR2;
AC-009/034 live edit/owner → PR3.

**Next.** Live visual/RTL pass on the rebuilt stack, then PR2 (Submit form: 5 fieldsets, autosave-draft,
unsaved-work guard, file upload, locale-preserve).

---

### 2026-06-26 — P5a backend complete: Topics module (domain → application → infrastructure → API), live-verified on real SQL Server

**Scope.** The backend half of P5 — the core-loop heart (intake → triage → backlog) — built as a new
`Topics` module on the established modular-monolith pattern. The UI (P5b) follows. Branch `feat/P5-topics`
(4 commits); **353 tests green** (23 domain · 3→**8** arch · 307 application · 20 API); solution builds.

**Done.**
- **Domain** (`Topic` aggregate). Full canonical lifecycle state machine (docs/domain/entity-lifecycles.md §1) — Submit/Triage/
  Accept/Reject/Defer/Reactivate/Prepare/Reopen/Schedule/Decide/Close/Convert; guards reject illegal
  transitions; content locks after Accept (AC-034); metadata editable until Decided, then immutable.
  Enums per docs/domain/topic-taxonomy.md (Type×4, Urgency Normal/Urgent/Critical, Scope×4, Source×10, Status×13). Child
  entities: `TopicAttachment` (MinIO metadata), `TopicComment` (immutable), `TopicStatusEvent`
  (append-only history → immutable rejection record, AC-032/033). ABAC contracts implemented
  (`IStreamScopedResource`/`ITopicScopedResource`).
- **Application** (MediatR slices, FluentValidation, `IAuditSink` on every state change): SubmitTopic
  (W1), AcceptTopic (W2 + grant-on-accept), RejectTopic/DeferTopic (W20), PrepareTopic (W4),
  PrioritizeTopic (W3), UpdateTopic (AC-034 phase-aware), AddTopicComment (BL-033), AttachFileToTopic
  (AC-049/050), GetBacklog (filter/sort/page + SLA aging AC-057), GetTopicDetail. **Live ABAC** via a new
  shared `IResourceAuthorizer` seam (the P4→P5 deferral made concrete): handlers load the Topic then
  `EnsureAsync(topic, policy)` against the registered `CapabilityRequirement`.
- **Infrastructure.** `TopicsDbContext` (schema `topics`) — streams/systems/tags as JSON columns
  (value-converter), attachments/comments/history as owned child tables, enums as int. Forward-only
  migration `Topics_P5_Initial`; `TopicKeyGenerator` (gap-free `TOP-YYYY-###`); the Membership-side
  `ITopicCapabilityWriter` (grant-on-accept) registered alongside the ABAC read providers; `MigrationRunner`
  now migrates every module context.
- **API.** `/api/topics` (submit/backlog/detail/accept/reject/defer/prepare/priority/update/comments/
  attachments) with policy RBAC + in-handler ABAC; global `JsonStringEnumConverter` wire contract.
- **ArchUnit.** Boundary tests extended to both modules + cross-module isolation (Topics ⟂ Membership);
  3 → 8 tests, all green.

**Live verification (real stack, 2026-06-26).** `docker compose up -d --build` → **all 7 services HEALTHY**;
api log `Database migrations applied.` (both module contexts on real SQL Server). All five Topics tables
materialized: `topics.topics`, `topic_attachments`, `topic_comments`, `topic_status_events`,
`topic_key_counters` (+ `membership.topic_capability_grants` for grant-on-accept). `/api/topics` → **401**
without a token (fail-closed). **Authenticated round-trip through the real PKCE login** (acmp-admin →
token `iss=keycloak.localhost/realms/acmp`, `aud=acmp-api`, roles `[Administrator,Secretary]`):
`GET /api/topics` 200; `POST /api/topics` 201 → **TOP-2026-001**; `GET` detail reads back
`streams=[identity,platform]`, `tags=[SecurityArch]`, `history=1`. **Direct SQL confirms** the JSON
columns persisted (`streams = ["identity","platform"]`, `tags = ["SecurityArch"]`), the owned
`topic_status_events` row exists, and `topic_key_counters` advanced to `2026 → 2`. This closes the
InMemory-only gap: the write path persists JSON columns + owned tables on real SQL Server.

**Decisions / drift (no silent drift, guardrail 11).**
- **No new ADR** — new module on the settled architecture; no architecture change.
- **5 design↔behavior reconciliations** (design = visual SoT, package = data SoT; recorded in code
  comments at each point): (1) **4 topic types** not the design's 3 (doc 09 adds EnhancementInnovation);
  (2) **Urgency = Normal/Urgent/Critical** not the design's "low" (doc 09 §B.1 SLAs); (3) **single-language
  topic title/description** (the design's bilingual sample is demo; UI chrome stays full EN/AR i18n);
  (4) **Scope derived** from affected-stream count + **Source defaulted**, both Secretary-adjustable in
  triage (the submit form has no picker for either); (5) **kanban 5 buckets** are a backlog view grouping
  over canonical status — DnD performs only P5-legal transitions (schedule needs a Meeting → P6).
- **Identity model.** Actor/author/submitter = Keycloak subject + name snapshot (matches `IAuditSink`/
  `ICurrentUser`, no per-command member lookup); Owner = member PublicId + name; grant-on-accept resolves
  owner → grant inside Membership. Corrected mid-build from an initial `Guid actorId`.
- **Attachment limit = 50 MB** (AC-049 configurable default), not the design's "25 MB" hint (display copy).
- **ponytail ceilings noted:** key generator is a get-increment-save (gap-free, fine at committee scale;
  unique `Key` index fails loud on the rare race); backlog stream/text filters run in memory post-fetch.

**Acceptance audit (this entry).** **Met:** AC-031 (reject needs reason, 400 over HTTP). **Partial**
(mechanism built + tested; live-HTTP or consuming phase named): AC-030 (server validation + 400 proven;
localized messages → BL-016), AC-032 (immutable event persisted; submitter notify → Notifications phase),
AC-033 (no mutation surface; DB-enforced immutability + hash-chain → BL-066), AC-034 (content lock +
metadata-only-Secretary enforced in domain + handler; live 403 path → P17), AC-035 (Prepared + audit
proven), AC-049/050 (size/MIME validation + IFileStore upload + DocumentAttached audit via handler tests;
live MinIO → P5b), AC-057 (aging badge live-verified; SLA-breach notification → Notifications phase),
AC-009 (grant-on-accept + ABAC owner check proven live on accept; per-topic edit 403 → P5b). AC-010
stays Partial (stream-scope on actions → P8).

**Next.** P5a PR (push → CI green → review GO → squash-merge). Then **P5b** — the three design-matched
screens (`ACMP Backlog & Topic.dc.html`: backlog 5 views, submit form, topic detail) wired to this API.

---

## CHANGE-003 — Local-design source of truth + shared component library + screen composition

### 2026-06-26 — Re-did the UI for fidelity against the LOCAL `/ACMP product context/*.dc.html`

**Why.** The design source of truth moved from the claude_design MCP to the **local
`.dc.html` files** at the repo root. Re-verified the built UI against those files directly
(file tools, not MCP) and built out the full shared component library so every screen
composes from it rather than re-styling per screen (instruction #3 / guardrail 14).

**Approach.** Devil's-advocate review first (verified claims by direct inspection, not
transcripts): tokens were a **byte-for-byte value match** to `ACMP Design System.dc.html`
(CHANGE-002 held); the design folder is **structurally excluded** from every gate (frontend
job is scoped to `src/Acmp.Web`, scripts scan only their targets, backend is `acmp.sln`);
demo scaffolding (`greetMap`/`isCoord`/`defaultRole`/Tweaks panel) was never ported. So the
real work was the **shared library + screen composition**, not a token rebuild.

**Done (8 ordered commits on `chore/ui-fidelity-local-design`).**
- **Reference folder vendored** (`3deae68`) — committed `/ACMP product context/` as read-only,
  in-repo, reproducible source of truth (inert: never imported/served/built/linted) (Q1).
- **Shared component library** (`98749c4`,`c5393ad`,`f9ef21c`,`026df88`) — the full Design
  System §05–§12 set, token-driven, RTL/dark, a11y, each with tests, **strings via props
  (zero i18n-parity impact)**: Button (variant×size, icon-only, loading), Field+Input+Textarea,
  Checkbox/Radio/Toggle, Tabs, Segmented, Tag/Badge, Breadcrumb, Pagination, Menu, Dialog,
  Toast, Select, Table, MultiSelect, DatePicker (+ existing Card/StatusChip/states/SortableList).
- **Security fix** (in `c5393ad`) — Breadcrumb `href` scheme allowlist (XSS hardening; flagged
  by the automated commit-review): only relative/#/http(s)/mailto link; `javascript:`/`data:`/
  malformed fall through to text. Regression test added.
- **Shell + nav metrics** (`0ae7093`, Q4 = `ACMP.dc.html` authority) — header 60→58px, gap 18,
  pad 16/18, **solid `var(--header)` (dropped the Design-System-doc translucent blur)**; sidebar
  244→248px, padding 20/14, offset pinned to the 58px header (resolved the design file's own
  58-vs-60 self-inconsistency). **Q3:** `DevRoleSwitcher` now dynamically imported behind
  `import.meta.env.DEV` → tree-shaken out of the prod bundle (verified: no DevRoleSwitcher chunk).
- **Admin screen composition** (`bf1b82b`) — `UsersMembership` now composes shared Tabs/Table/
  Button/Tag/StatusChip/Icon (was bespoke `.adm-*` table+tabs+provision + hand-rolled inline
  SVGs); `administration.css` trimmed to domain directory cells only. **Behavior unchanged — all
  8 existing behavior-focused tests pass untouched.** Login already composed Button/Card/states.
- **Logo fix** (`51e970f`) — header + login use the **primary 4-stroke 'plinth' mark**
  (`public/acmp-mark.svg`); `favicon.svg` (simplified) stays the browser-tab icon, per
  `Logo.dc.html` (16px favicon vs 24px+ UI header).

**Verification (deterministic, green).** `tsc -b` + `vite build` clean (**131 kB gz JS** <300 kB
budget, CSS 17.8 kB gz); **web 54/54 tests** (37 prior + 17 new component/security tests); i18n
parity **103 keys**; prod bundle confirmed to exclude the dev role switcher. Backend untouched.

**Decisions / drift (no silent drift, guardrail 11).**
- **No new ADR** — visual/composition reconciliation to the approved design; no architecture change.
- **Q2 = full library** (operator chose full over atoms-only) — built all §05–§12 primitives; the
  §07 pickers (MultiSelect/DatePicker) have no consumer until P5's topic form but are built + tested.
- **Authority split** for design inter-file conflicts: shell chrome/nav-container metrics →
  `ACMP.dc.html` (Q4); nav-item anatomy → `Navigation & IA`; primitives → `Design System`.

**Live visual pass — done (2026-06-26).** Playwright across the shell, Admin, **and Login** in
**EN-light and AR-RTL-dark** — **live in-browser axe (WCAG 2.2 AA) clean on every surface in both
directions/themes** — after fixing **two** real contrast gaps the jsdom axe can't compute:
`.brand-sub` (`--text-3` on `--header`, **4.49:1**) and `.login-invite` (`--text-3` on `--bg-app`,
**4.02:1**), both → `--text-2` (same AA bump CHANGE-002 made for the other small chrome labels).
RTL fully mirrors (sidebar→inline-end, active accent rail, underline tabs, search, login controls
→inline-end, the CTA enter-glyph flips), dark surfaces legible, the **primary plinth mark** renders,
and the **AR tagline** (…لجنة الهندسة المعمارية) is correct. Login was rendered by running the dev
server with `VITE_OIDC_*` set (bypasses the auto-auth dev stub → `/login` shows the Keycloak CTA
without completing the round-trip). Six frames screenshotted. (The populated Admin table is covered
by unit + axe tests; the backend-less dev run shows the composed ErrorState.)

**Next.** Push branch → PR → monitor remote CI to green → review GO → squash-merge. Then P5.

---

## CHANGE-002 — Design-fidelity reconciliation (frontend ↔ Claude Design package)

### 2026-06-26 — UI reconciled to the "ACMP product context" design across all built surfaces

**Why.** A surface-by-surface audit (4 parallel comparison agents, then independent
source-verification of every CRITICAL/MAJOR finding against the design `.dc.html` files)
found the implemented UI had drifted from the design system on shared components, shell
chrome, the brand mark, and several copy/AR gaps. Reviewed adversarially (devil's-advocate
pass) before any edit — which corrected one **wrong fix mechanism** (header scroll) and
surfaced an inter-file authority rule for design drift.

**Done (one branch `chore/design-fidelity-reconciliation`, 6 ordered commits).**
- **Tokens (1/6):** added `--control-radius: 9px` (the design's off-scale control radius).
  Token *values* were already a byte-match (light + dark) — no value changes.
- **Shared components (2/6):** buttons → 38px / 13.5px / `--control-radius`, primary
  `box-shadow`, **ghost reads `--accent`** (was gray) with `--primary-tint` hover, + danger
  variant. State tiles → 40px rounded-square (was 44px circle); **permission-denied is now a
  neutral "No access" tile** (was amber-warn) per the design's calm treatment; glyphs
  document/circle-exclamation/padlock. Removed the dead `building` icon.
- **Shell (3/6):** **brand mark replaced** — the house-glyph favicon → the design's "A"
  monogram (drives header + login + favicon); two-line brand. Topbar 60px, **sticky** +
  translucent blur; **sidebar sticky** below it (document keeps scrolling — matches the
  design; no `app-main` overflow container). Notification bell **drops the always-on red dot**
  (it showed over an empty inbox); empty panel is a calm "all caught up" success state.
  Search: descriptive placeholder (EN+AR), 38/9/13.5, 560px. Chrome reordered lang→theme→bell.
- **Nav (4/6):** active item gains the design's **3px accent rail** (inline-start, RTL-safe);
  rows 40px/13.5px (CTA 38px); "My Session" uses a distinct video glyph; EN labels → Title Case.
- **Screens + copy (5/6):** **Sign In** restructured to the design — top-right bordered
  controls, **tonal status banner** (signed-out=info / expired=warn + icon), divider, "Sign in
  to continue" subtitle, 48px CTA with an enter glyph (RTL-flipped), lock + secure-hint row,
  invite footer, heavier local card shadow. New `auth.subtitle/secure/invite` (EN+AR, verbatim
  from the design). **AR tagline fixed** (`منصة إدارة لجنة المعمارية` → `…لجنة الهندسة المعمارية`
  — was missing الهندسة; guardrail 9). Admin: disabled the non-functional filter buttons.

**Verification (deterministic, green at each step):** `tsc -b` + `vite build` clean
(gzip 130.9 kB JS < 300 kB budget), **web 37/37 tests**, oxlint clean, **i18n parity 102 keys**.
Backend untouched. Design-side targets were source-verified verbatim from the Design System,
Logo, Sign In, ACMP shell, and Navigation & IA `.dc.html` files (not agent transcription).

**Decisions / notes (no silent drift, guardrail 11).**
- **No new ADR** — visual reconciliation to the approved design package; no architecture change.
- **Design authority per surface** (for inter-file drift): tokens/components/shell → Design
  System; nav → Navigation & IA; sign-in → Sign In; brand → Logo. Surface-specific file wins.
- **Sign In card shadow** kept as a *local* override (operator decision) — global `--shadow-lg`
  token untouched (blast-radius control).
- **Skipped (honest):** admin grid-column-width tweak (DF-28) — unverified, marginal cosmetic;
  changing toward an unverified target risked regression. Per-role nav labels ("My Submissions"
  for submitter) deferred — a behavior feature for the submitter flow (P5), not a fidelity defect.

**Remaining verification (not blocking the diff):** live browser axe (WCAG 2.2 AA) + RTL/dark
visual re-check across EN-light and AR-RTL-dark per surface — the deterministic gates and
source-verified token contrast hold, but the live axe/screenshot pass is the confirmatory step
(AC-040/041/045/046). To run against `vite dev` + the DEV auth stub.

**Next:** push branch → PR → monitor remote CI to green → review GO → squash-merge. Then P5.

---

## CHANGE-001 — Self-Hosted Keycloak; all runtime dependencies bundled (ADR-0015)

### 2026-06-25 — Carry-forward findings resolved: logout UI control + CSP templating

Both findings from the change-slice review are closed and verified:
- **Logout UI control.** Added a sign-out button to the `TopBar` (new `logout` icon + `auth.signOut`
  EN/AR keys) wired to the auth-context `signOut` (`oidc.signoutRedirect()`). New `TopBar.test.tsx`
  asserts the control invokes `signOut`. **Browser-verified end-to-end:** authenticated `/dashboard` →
  clicked sign-out → Keycloak end-session → back to `/login` (logged out).
- **CSP templating.** The nginx CSP Keycloak origin is no longer hardcoded: `nginx.conf` →
  `default.conf.template` using `${KEYCLOAK_ORIGIN}`, substituted at container start (nginx envsubst with
  `NGINX_ENVSUBST_FILTER=KEYCLOAK_ORIGIN` so `$host`/`$uri`/`$scheme` are preserved). Driven by a runtime
  `KEYCLOAK_ORIGIN` env on the `web` service (compose + `.env`/`.env.example`) — each environment sets its
  own origin with no rebuild. Verified live: CSP header renders the substituted origin; `/api/` proxy still works.

Verification: web **34/34** tests (incl. the new TopBar test), i18n parity **94 keys**, self-contained lint
green, web image rebuilt + healthy, full browser login→logout re-run clean. Backend untouched (still 311).

### 2026-06-25 — Review remediation: 6/6 healthy + full browser login/logout cycle

Acting on the change-slice review (NO-GO on stack health), fixed the gaps and then drove the **full
browser cycle** end-to-end — which surfaced one more real bug (CSP).

**Remediation (infra/config only):**
- **seq was down** — root cause was *not* the port: recent `datalust/seq` requires a first-run admin
  password or an explicit opt-out. Added `SEQ_FIRSTRUN_NOAUTHENTICATION: "true"` (internal dev
  observability; prod sets `SEQ_FIRSTRUN_ADMINPASSWORD`), **pinned seq by digest** (was `:latest` — the
  unpinned bump is what broke it; OQ-031), and remapped the Seq UI host port **8081→8341** (operator's
  host-conflict request; app uses internal `seq:5341`).
- **Healthchecks added for seq and minio** (both images ship `curl`): `…/health` and
  `…/minio/health/live`. Item 1 can now assert all services healthy.
- **CSP bug (found by the real browser flow).** The deployed SPA could not start login: the nginx CSP was
  `connect-src 'self'`, which blocked the SPA's cross-origin OIDC metadata/token `fetch` to the Keycloak
  origin (top-level redirects aren't governed by `connect-src`, which is why the direct authz URL worked
  but the app button silently failed — `signinRedirect` rejected on "Failed to fetch"). Added the Keycloak
  origin to **`connect-src`** and **`frame-src`** (silent-renew iframe). Dev origin hardcoded; prod must
  template its real KC origin (P18). Rebuilt `web`; verified the CSP header live.

**Live verification (clean `docker compose down` → up):** **all 6 services HEALTHY** (api, web, keycloak,
sqlserver, seq, minio) + keycloak-db healthy. Backend **311/311** green (Domain 5 · Application 290 incl.
the 248-case permission-matrix · Architecture 3 · Api 13). Self-contained lint green. Realm verified live
via admin API (8 realm roles + 8 groups = canonical names; client `acmp-web` public + standardFlow + PKCE
S256; `acmp-admin` enabled).

**Full browser cycle (Chrome, real UI):**
1. **ACMP → Keycloak:** `/login` → "Sign in via Keycloak" → SPA `signinRedirect` builds its own PKCE
   request (`redirect_uri=/auth/callback`, S256) → Keycloak login page.
2. **Keycloak → ACMP:** `acmp-admin` creds → submit → `/auth/callback` → SPA exchanges the code →
   **`/dashboard` authenticated** (sessionStorage holds the access token; API authorizes).
3. **Logout:** clear local token + Keycloak end-session (`/logout` with `id_token_hint`) → redirect to the
   post-logout URI → app finds no token → **`/login`** (logged out).

**Finding (not blocking CHANGE-001; → P5/UI backlog):** the app has **no logout UI control** — `signOut`
(`oidc.signoutRedirect()`) is wired in the auth context but no component surfaces it (the P3 identity
cluster is read-only). The logout *mechanism* works (demonstrated above); a sign-out button/menu needs
adding to the TopBar. Logged for the UI backlog.

**AC-001 → Met (UI-verified):** the SSO login round-trip now completes through the app UI, not just the
direct protocol flow. **AC-004** still Pending (realm idle-timeout policy, OQ-003).

### 2026-06-25 — Infra change-slice applied (post-P4, before P5). No P4/app rework.

**Why.** ASM-001 (org provides Keycloak) is **false** — the org has no Keycloak. Per **ADR-0015**
(secretary-directed), ACMP now **self-hosts Keycloak** as a bundled container with an **ACMP-owned realm**,
and SQL Server stays bundled → **v1 has zero external runtime services** (CON-001 strengthened; ADR-0013's
"two external exceptions" carve-out withdrawn). The OIDC contract is unchanged (authz-code + PKCE, roles
from realm-role/group claims, no self-registration; manual provisioning in the KC admin console), so the
**P4 identity/Membership code needs no rework** — verified by reading it (`AuthenticationExtensions` is
purely `Authentication:Keycloak:Authority`-driven; `KeycloakRoleClaimMapper` normalizes against `AcmpRoles.All`).

**Done (infra + config only).**
- **`deploy/docker-compose.yml`** — added `keycloak` (`quay.io/keycloak/keycloak:26.0`, `start-dev
  --import-realm`, health on mgmt `/health/ready`) + `keycloak-db` (`postgres:16`, `kcdata` volume,
  `pg_isready` health). Wired `api` → `Authentication__Keycloak__Authority` at the in-stack realm
  (`RequireHttpsMetadata=false` for the http dev profile), `depends_on: keycloak service_started`
  (JwtBearer fetches metadata lazily, so api boot need not block on KC readiness). `sqlserver` already bundled.
- **`deploy/keycloak/realm-export.json`** — realm `acmp`; **public PKCE client `acmp-web`** (standard flow,
  S256) with an **audience mapper → `acmp-api`** (the api validates `aud`) + realm-role/group claim mappers;
  the **8 canonical roles as realm roles AND groups**, named verbatim from `AcmpRoles.All`
  (`…,Submitter,Guest` — **not** "Guest/Presenter", which the leaf-after-`/` mapper would mis-map to
  `presenter`); initial admin user `acmp-admin` (Administrator+Secretary) with **no committed credential**
  (`UPDATE_PASSWORD` required action — guardrail 7).
- **`deploy/keycloak/README.md`** — realm import, manual provisioning (Q3), the issuer/hostname wiring,
  the OQ-038 datastore decision, and P18 prod-hardening notes.
- **Env** — `deploy/.env.example` + local `deploy/.env` gained `KC_BOOTSTRAP_ADMIN_*`, `KC_DB_*`,
  `KEYCLOAK_AUTHORITY`; `src/Acmp.Web/.env.example` `VITE_OIDC_AUTHORITY` now points at the bundled realm.
  `appsettings.json` keeps its **secure defaults** (empty Authority + `RequireHttpsMetadata=true`); in-stack
  values live only in compose/env. No secrets committed.
- **Self-contained lint** — new `scripts/check-self-contained.mjs` (Node, matches `check-i18n.mjs`):
  scans compose runtime hosts, allowing only in-stack services + loopback/`*.localhost` + `*.webex.com`
  (Phase 2). Wired into CI as a new `compose` job alongside `docker compose config` validation.

**Issuer/hostname (the one real subtlety).** `AuthenticationExtensions` exposes only `Authority` (one URL
for metadata fetch *and* issuer validation), so the issuer must be byte-identical for browser and api.
Pinned `KC_HOSTNAME=http://keycloak.localhost:8085`: the browser auto-resolves `*.localhost` to loopback;
the api reaches the same host via `extra_hosts: keycloak.localhost:host-gateway`. **No P4 code change.**
Prod uses a real reverse-proxy hostname + TLS (P18).

**Datastore = OQ-038 → (a) Postgres-for-Keycloak.** `docs/decisions/open-decision-register.md` default; app data stays SQL-only (ADR-0003).

**Verification — live (2026-06-25).** `node scripts/check-self-contained.mjs` ✅ (7 services, 0 external) +
negative-tested (flags an external host, exit 1). `docker compose --env-file .env config -q` ✅ parses.
**`docker compose up -d --build` brought the full 6-service stack up — all HEALTHY:** api, web, keycloak,
keycloak-db, sqlserver healthy; minio running (no healthcheck). The KC `/health/ready` probe (bash `/dev/tcp`
on mgmt port 9000) works. **Keycloak realm import succeeded** — log: `Realm 'acmp' imported … Import finished
successfully` (KC 26.0.8). **OIDC discovery issuer = `http://keycloak.localhost:8085/realms/acmp`** (byte-identical
to the pinned `KC_HOSTNAME`; PKCE **S256** advertised), and the API resolves it via `extra_hosts` (api healthy).
**`GET /api/members` → 401** against the real authority (fail-closed still holds). **P4 migration applied** on
api startup: `Database migrations applied.` (`Membership_P4_Identity` — closes the P4-deferred `docker compose up`
apply). Backend **311** + web **33** untouched (no app code changed).

**Browser login round-trip — done (2026-06-25).** Set a password for the `acmp-admin` realm user via the
Keycloak admin API, then drove the **full authorization-code + PKCE flow in Chrome**: Keycloak login page →
submit → redirect to `http://localhost:8088/?code=…&iss=http://keycloak.localhost:8085/realms/acmp` (state
matched). Exchanged the code (with the PKCE verifier) → access token with **`iss`** correct, **`aud: acmp-api`**,
**`realm_access.roles: [Administrator, Secretary]`**, groups `[/Administrator, /Secretary]`; **`GET /api/members`
with that bearer → 200**. End-to-end identity contract proven (browser login → mapped roles → API authorizes).
**SPA build-arg wiring — fixed (2026-06-25).** `deploy/Dockerfile.web` now takes `VITE_OIDC_AUTHORITY`/
`VITE_OIDC_CLIENT_ID`/`VITE_OIDC_SCOPE` as `ARG`→`ENV` before `npm run build`; compose passes them via
`web.build.args` from `KEYCLOAK_AUTHORITY` (so SPA + api share one issuer). Rebuilt `web`; verified the issuer
is **baked into the bundle** (`grep` of `/usr/share/nginx/html/assets`) and the SPA now redirects to `/login`
and renders the **"Sign in via Keycloak"** CTA (was failing closed). **AC-001 → Met** (SSO login round-trip +
role mapping + API authorization proven; SPA initiates login; automated UI regression → P17). Idle-timeout/
session policy still pending → AC-004 (OQ-003).

**Decisions / drift (guardrail 11).**
- **No new ADR** — ADR-0015 covers this; this is its rollout (CHANGE-001 §6).
- **OQ-038 ID collision fixed.** Canon `docs/decisions/open-decision-register.md` binds **OQ-038 = Keycloak datastore**; a stale PH-0 note had
  reused OQ-038 for "prod CI runner" (never canonicalized) → **renumbered to OQ-041** in `ph0-validation.md`
  + this log. Surfaced, not silently resolved.
- **OQ-040** (bundled SQL Server prod edition/licensing) remains for human confirmation at deploy (P18);
  **OQ-039** (future upstream federation) deferred.

---

## P4 — Identity & Permissions

### 2026-06-25 — P4 complete: claim→role mapping, policy + ABAC authorization, SoD, full Membership module, Users & Membership screen

**Done.** Implemented the authorization framework + the Membership module fully, plus the admin
Users & Membership UI.

- **Authentication (host, ADR-0004).** Config-driven Keycloak `JwtBearer` (`Authentication:Keycloak`);
  `OnTokenValidated` maps realm/group role claims → canonical ACMP role claims via `IRoleClaimMapper`.
  Local token validation (signature/issuer/audience); with no Authority configured the scheme rejects
  every token so protected endpoints return **401** (fail-closed). `UseAuthentication/UseAuthorization`
  wired; the members group is `RequireAuthorization()`.
- **Claim→role mapping.** `KeycloakRoleClaimMapper` mirrors the SPA `roles.ts` normalization (bare /
  `acmp-` / `/acmp/` / group-path / `coordinator`→Secretary alias) + config overrides
  (`Authorization:RoleMapping:ClaimToRole`). No-claim default = **deny** (`DefaultRole=null`, AC-003) with
  an `AuthEvent`.
- **401-vs-403 fix (carried defect).** New `ForbiddenAccessException`→**403**; `UnauthorizedAccessException`
  stays **401**. Primary gate is ASP.NET policy authorization (middleware → correct 401/403); the MediatR
  `AuthorizationBehavior` is defense-in-depth and now throws Forbidden for authenticated-wrong-role and
  emits an audit signal on deny.
- **Policy registry (docs/domain/permission-role-matrix.md §C).** 31 named policies registered as `CapabilityRequirement(allowRoles,
  ownerRoles)`; Deny = absence of both, so **SoD-5** (Administrator walled off committee content) is
  structural. `CapabilityHandler` evaluates RBAC → allow-if-owner relationship → delegation widening.
- **ABAC (docs/domain/permission-role-matrix.md §D/§E).** `IAbacResource` contracts (`ITopicScopedResource`/`IStreamScopedResource`),
  `StreamScopeHandler`, capability/ownership + delegation handlers, and Membership-implemented resolvers
  (`IUserStreamProvider`/`ITopicCapabilityResolver`/`IDelegationResolver`). `ConfidentialityRequirement`
  deliberately **cut** (no P4 AC; YAGNI). Per-capability gating (Owner-edit vs Presenter-read) and the
  grant-on-accept flow are P5 (no Topics aggregate yet).
- **SoD predicates.** `SegregationOfDuties.CanVerifyAction` (SoD-1) and `HasIndependentCoAttestation`
  (SoD-3) — pure guards the Actions (P8) / Voting (P9) modules will call; proven now.
- **Membership module (ADR-0004 reconciliation).** `CommitteeMember` reworked: `Role` is a **claims-derived
  cache** refreshed each login (JIT `Provision`/`SyncFromClaims`) — **not** admin-settable; the
  role-setting `InviteMember` was removed. Added `MembershipStatus` (Active/Invited/Disabled),
  `IsVotingEligible`, stream assignments, `Stream`, `TopicCapabilityGrant`, `Delegation`. Features:
  `GetMembers` (directory), `GetStreams`, `ProvisionCurrentUser` (`/me`), `DeactivateMember` (AC-058),
  `AssignStreams`, `CreateDelegation`. `CommitteeRole.GuestPresenter`→`Guest` (aligns enum ↔ `AcmpRoles` ↔
  SPA). New forward-only migration `Membership_P4_Identity`. `IAuditSink` (Serilog→Seq interim; immutable
  store = BL-066).
- **Frontend.** Administration → **Users & Membership** screen (the design's "ACMP Administration" file,
  that screen only), wired to `GET /api/members` via TanStack Query: Keycloak read-only banner,
  role + "from Keycloak" lock, committee/stream chips + Observer + Voting-eligible, status chips, the four
  states, and the disabled future tabs. Reuses P3 design tokens (`--st-*` match the design exactly) and
  CSS logical properties (mirrors in RTL). 25 EN/AR keys added (parity green). Route `/admin` (admin-gated).

**Verification.** Backend **302/302** green (5 domain · 3 ArchUnit boundary · 281 application incl. the
**248-case permission-matrix suite** with independently-encoded A/AiO/D expectations · 13 WebApplicationFactory
integration via `TestAuthHandler` **+ the real Keycloak JwtBearer scheme** (anonymous/bogus-token → 401,
not 500; health stays anonymous)). Web **33/33**, `tsc -b && vite build` clean (130 kB gzip < 300 kB budget),
oxlint clean, i18n parity **93/93**. New integration project `Acmp.Api.Tests`.

**Post-review hardening (advisor pass).**
- **Frontend re-matched the design** — restored the 5th *Assignments* column (placeholder `—`; topic/action
  counts land P5/P8) and rendered voting eligibility as the design's **read-only switch** (was a badge).
  Visually verified by rendering the real CSS in Chrome (Playwright screenshot) against the design source.
- **Migration corrected** — EF inferred an `IsActive`→`IsVotingEligible` column *rename* (would carry the old
  active-flag values into the unrelated eligibility flag); rewritten as explicit drop + add. SQL re-generated
  and inspected (`ef migrations script`); full `docker compose up` apply is the operator's check (the
  sandbox blocked launching the stack).
- **Config placeholders** — documented `Authentication:Keycloak:*` + `Authorization:RoleMapping:*` in
  `appsettings.json` and `deploy/.env.example` (fail-closed defaults; no secrets).

**P4 review — NO-GO gaps closed (round 2).** A full phase audit (acceptance, coverage, DoD, guardrails)
returned NO-GO on four fixable gaps; all closed:
- **AuditEvent on every state-mutating op** (guardrail 5 / docs/domain/audit-and-records.md / DoD [HARD]) — `DeactivateMember`,
  `AssignStreams`, `CreateDelegation`, and `ProvisionCurrentUser` now emit via `IAuditSink` on success
  (entity, action, actor, before/after); emission asserted in tests. Field-stamping was not enough; the
  immutable hash-chained store remains BL-066.
- **RTL visual verification** (DoD [HARD]) — rendered the Users & Membership screen with `dir=rtl` +
  Arabic in Chrome: fully mirrored (provision button + count to inline-end, columns right→left, switch
  knob mirrors, email stays LTR), no LTR artifacts.
- **Untested handlers + JWT extraction** — added direct tests for `AssignStreams`/`CreateDelegation`/
  `GetStreams`; extracted the Keycloak `realm_access`/`resource_access`/`groups` JSON parsing to a
  testable `KeycloakClaims.RoleValues` helper (host wiring now calls it) with unit tests for every shape.
- **CS0108 warning** — renamed `TestAuthHandler.Scheme` → `SchemeName`. Code warnings now **zero**
  (only 4 tracked NU1902 OpenTelemetry advisories → P16).

Backend now **311 tests** (5 domain · 3 ArchUnit · 290 application · 13 integration), all green. **Verdict: GO.**

**Decisions recorded (no silent drift, guardrail 11):**
- **Role not admin-settable.** Per ADR-0004 ("roles sourced from Keycloak; ACMP creates the profile, not the
  identity") + the design banner. Reworked the aggregate to JIT provisioning; this aligns code to a settled
  ADR — no new ADR. The design has no create-user form ("Provision via Keycloak" is external).
- **AC-003 default role = deny** (`DefaultRole=null`, configurable). Fail-closed matches deny-by-default;
  docs/validation/acceptance-criteria.md allows "deny OR minimum default".
- **OQ-AUTH-001/002/003** resolved to docs/domain/permission-role-matrix.md recommended defaults: read-visible/write-scoped streams
  (already settled in README §C), single `Guest` role + Presenter relationship, Reviewer non-voting.
- **Audit interim.** `IAuditSink`→Serilog/Seq now; the immutable hash-chained `AuditEvent` store is BL-066
  (sequenced before votes). AC-003/006 are **Partial** for this reason (advisor-flagged).
- **ABAC trimmed** to stream/ownership/delegation (no Confidentiality) and no standalone capability-grant
  endpoint — both YAGNI until Topics exist (P5).

**Acceptance audit.** **Met:** AC-002, AC-008, AC-058, AC-059. **Partial** (mechanism proven; end-to-end
deferred to the consuming phase): AC-003/005/006/007 (RBAC/SoD-5; audit→BL-066), AC-009/010/011 (ABAC→P5+),
AC-012/013/015/016 (SoD→P8/P9). AC-001/004 stay Pending (live Keycloak realm + idle-timeout).

**Deferrals → phase:** per-capability ABAC gating + grant-on-accept + live ABAC HTTP 403 → P5 · SoD-1
enforcement → P8 · SoD-3 + chair-approve → P9 · MoM SoD-2 → P7 · immutable hash-chained audit store
(AuthEvent) → BL-066 · live Keycloak login + idle timeout (AC-001/004) → needs a realm · automated
visual-regression/axe of the new screen → P17.

**Next (await go-ahead):** P5 Topics & Backlog — the core loop; consumes `SortableList`, the ABAC
`IAbacResource` contracts (Topic implements stream/owner), and grant-on-accept for per-topic capabilities.

---

## P3 — Frontend Foundation & App Shell

### 2026-06-25 — P3 complete: design-system shell, role-filtered nav, OIDC wiring, states, accessible DnD

**Done (all in `src/Acmp.Web`).** Built the React + TS + Vite application shell to match the Claude Design
**ACMP Design System** and **Navigation & IA** files (visual layer) over docs/domain/information-architecture.md behavior:
- **Design tokens** (`styles/tokens.css`) — full light+dark token set from the design system (surfaces, 6
  semantic status roles each with bg/fg/dot, `--sp-*`/`--r-*`/motion, IBM Plex type). `global.css` +
  `components.css` migrated to the design token names. **Fonts self-hosted via `@fontsource`** (bundled by
  Vite, not a CDN) so the SPA runs air-gapped — CON-001 / guardrail 3.
- **App shell** — `TopBar` (brand, global search, locale + theme toggles, notification bell, read-only
  role/identity cluster), 244px role-filtered `SideNav` (design GROUPS: Committee/Governance/Knowledge/
  Insights/System + CTA group), `NotificationCenter` shell (empty state; feed is a later phase), `AppShell`
  (skip link → chrome → routed main inside an `ErrorBoundary`). All layout via CSS logical properties → mirrors
  in RTL with no per-direction overrides.
- **Auth** (`auth/`) — `react-oidc-context` + `oidc-client-ts` for Keycloak auth-code+PKCE, config from
  `VITE_OIDC_*` (no secrets in source). `useAuth` exposes canonical roles mapped from claims
  (`rolesFromClaims`, README §C, "coordinator"→secretary alias). `ProtectedRoute` + `RequireRole` route gates;
  Login/AuthCallback pages. **DEV-only auth stub** (role switcher) gated behind `import.meta.env.DEV` — absent
  from the prod bundle; prod with no IdP **fails closed**. Nav/route gating hides UI only; the API enforces (P4).
- **Server state** — `@tanstack/react-query` provider + `apiClient` (bearer token + `Accept-Language` + RFC7807
  Problem Details → typed `ApiError`). No endpoint hooks yet (no real data in P3).
- **States** — Empty/Loading(skeleton)/Error/PermissionDenied + class `ErrorBoundary` (docs/domain/information-architecture.md §4); `StatusChip`
  (label + dot, never color-alone), `Button`, `Card`.
- **Accessible DnD** — shared generic `SortableList` (`@dnd-kit` pointer+keyboard) **plus** explicit Move up/down
  keyboard fallback (docs/domain/information-architecture.md §5, ADR-0012). Component + test only; backlog/agenda consume it at P5/P6.
- **i18n** — `en.json`/`ar.json` expanded to the full shell vocabulary (66 keys), parity green. Routing for all
  nav areas → foundation placeholders (no feature screens). NotFound page.
- **Tests/CI** — Vitest + RTL + jsdom (`vitest.config.ts` separate from `vite.config.ts` to avoid the Vite 8/
  rolldown vs vitest nested-Vite type clash). **25 tests**: nav gating, claim→role mapping, OIDC profile helpers, theme persistence
  (AC-042), RTL direction (AC-040), SortableList keyboard reorder, RequireRole 403, StatusChip, SideNav role
  filtering. Added `npm test` to the CI frontend job (i18n parity already wired).

**Verification.** `dotnet`-side untouched. Frontend: i18n parity (66 keys) ✅ · `tsc -b && vite build` clean
(bundle 125 kB gzip, within the <300 kB app budget) ✅ · **25/25 tests** ✅ · oxlint clean ✅ · **axe (WCAG 2.2
AA) 0 violations across EN/AR × light/dark** ✅ — the axe pass rendered live and covered the chrome, a
placeholder route (EmptyState), the error state + skeleton, **all six StatusChip tones**, and the **open
notification panel**; fixed two findings: `.topbar-user-role` 10.5px label was 4.49:1 (`--text-3`→`--text-2`),
and `NotificationCenter` was `role="dialog"` without focus management → changed to a labelled `role="region"`
(non-modal popover). RTL + dark confirmed by screenshot (sidebar mirrors to inline-end, Arabic font + content,
read-only markers; dark surfaces legible).

**Decisions recorded (no silent drift, guardrail 11):**
- **React 19 vs ADR-0012 (says 18).** P1 silently installed React 19. Surfaced and resolved via **ADR-0017**
  (amends ADR-0012, keeps 19) — a settled-ADR change needs an ADR, not just a log line (guardrail 1). ADR-0012
  carries a forward-link note; adr/README index updated. (Originally filed as ADR-0015; renumbered to ADR-0017
  on 2026-06-30 to resolve a collision with ADR-0015 = Self-Hosted Keycloak — see doc-integrity slice.)
- **Self-hosted fonts (CON-001).** The design loads IBM Plex from Google Fonts CDN; replaced with `@fontsource`
  packages so production runs air-gapped. No new ADR — implements an existing constraint.
- **OIDC dev-stub.** DEV-gated, never in prod bundle; recorded as the P3→P4 boundary (live Keycloak login +
  server claim→role mapping = P4).
- `strict: true` added to `tsconfig.app.json` (CLAUDE.md requires it).

**Acceptance audit.** **AC-040, AC-042, AC-045, AC-046 → Met** (trace to tests + axe render); **AC-041 →
Partial** (manual RTL; automated VR → P17). AC-039 (form-data preservation) stays Pending — no form in the shell
yet. AC-043/044 (keyboard DnD on backlog/agenda) stay Pending — the shared component is built+tested but not yet
wired into those screens (P5/P6). AC-001/005/006/008 (Keycloak login, RBAC 403) stay Pending → P4.

**Deferrals → phase:** live Keycloak login + claim→role server mapping + 401/403 → P4 · automated RTL/visual
regression + Lighthouse gate → P17 · notification feed → Notifications phase · search results page → later ·
favicon.ico 404 in dev is cosmetic (a `favicon.svg` exists).

**Next (await go-ahead):** P4 Identity & Permissions (Membership full: claim→role mapping, policy + ABAC, SoD,
permission-matrix suite, 401/403 fix) — or P5 Topics/Backlog (core loop), which will consume `SortableList`.

---

## P2 — Backend Foundation & Reference Module Pattern

### 2026-06-25 — P2 verified: pattern already delivered by the P1 scaffold; closed with deferral notes

**Finding.** Every P2 deliverable was already implemented during the P1 scaffold. Re-read the actual code
(not the log summary) against the P2 checklist and re-verified from ground truth: `dotnet test acmp.sln`
→ **7/7 pass** (2 domain, 2 application, 3 architecture); only NU1902 (moderate, logged for P16) remains.
No new production code was warranted — rebuilding what exists would violate guardrail 12 / ponytail.

**P2 checklist → status (Membership = reference module):**
- Domain/Application/Infrastructure layers — ✅ `CommitteeMember` aggregate (factory + `CommitteeMemberInvitedEvent`),
  `InviteMember` command slice, `GetMembers` query slice, `MembershipDbContext` + config + migration.
- MediatR pipeline behaviors — ✅ logging → authorization → validation (outer→inner, registered in
  `SharedKernelExtensions`). Validation via FluentValidation (`InviteMemberValidator`); authorization via the
  `IAuthorizedRequest` opt-in marker + `AllowedRoles` (guardrail-4 day-one hook; full ABAC/SoD = P4).
- EF Core schema-per-module — ✅ `HasDefaultSchema("membership")`, maps only its own `DbSet`; enforced by the
  ArchUnit boundary tests.
- Forward-only migration — ✅ `Membership_Initial`.
- Problem Details error model — ✅ `GlobalExceptionHandler`: `ValidationException`→400 (+`errors`),
  `InvalidOperationException`→409, `UnauthorizedAccessException`→401, else 500.
- REST + OpenAPI — ✅ `/api/members` GET+POST (`Results.Created` + location); Swagger wired (non-prod).
- Abstractions — ✅ `IClock`/`ICurrentUser`/`IFileStore` registered + implemented; **`INotificationChannel`
  interface established, concrete impl deferred to BL-052** (in-app notification center). 3 wired + 1 established.
- Vertical-slice proof — ✅ one command (InviteMember) + one query (GetMembers) + tests (domain; handler
  invite→get + duplicate-reject; ArchUnit boundary). Also proven live in P1 (`docker compose up` healthy,
  `/api/members`=401 confirmed the auth pipeline executes).

**Deliberate deviations / deferrals (recorded — no silent drift, guardrail 11):**
- **Audit = field-stamping, not a pipeline behavior.** The P2 prompt lists "audit" among the behaviors;
  implemented instead as central `CreatedBy/At` + `UpdatedBy/At` stamping in `ModuleDbContext.SaveChangesAsync`
  (every `AuditableEntity`, one place). Rationale: the append-only `AuditEvent` log + hash chain is BL-066,
  sequenced before votes/decisions; emitting `AuditEvent`s now would pre-empt that phase with no store to write
  to. Stamping satisfies who/when traceability at P2 level. Consistent with ADR-0009 — no new ADR needed.
- **401-vs-403 (finding for P4, ties to BL-020 / AC-005/006/008).** `AuthorizationBehavior` throws
  `UnauthorizedAccessException` for both "not authenticated" and "authenticated-but-wrong-role", and the host
  maps both to 401. Role-denial for an authenticated user must be **403** (only missing/invalid token = 401).
  Fix belongs in P4 (authorization rework + permission-matrix suite). Both touch-points are single centralized
  files (shared behavior + host handler), so deferral carries no per-module-copy cost and no AC depends on it yet.
- **API integration tests** (WebApplicationFactory + Testcontainers + a fake-Keycloak `TestAuthHandler`,
  docs/domain/repository-structure.md §5) deferred to P4, when a JWT injector exists to exercise the HTTP authz path meaningfully.
  Handler-level slice tests cover Invite→Get end-to-end today.

**Acceptance audit.** Unchanged — all 66 ACs remain `Pending`. P2 is a pattern/foundation phase; the Membership
feature ACs (AC-058/059) land in P4 with HTTP + authz + UI. Domain capability + unit tests exist but the criteria
are not yet demonstrable end-to-end, so nothing flips to Met/Partial (conservative; G-TRACE).

### 2026-06-25 — P2 review: closed the one blocker (pipeline/validator test coverage)

P2 review (audit-only) found a single blocking gap: handler tests bypassed the MediatR pipeline, leaving
`InviteMemberValidator` and all three behaviors at **0%** coverage and `Membership.Application` at **70.5%
line** (below the 80% gate, docs/validation/test-strategy.md §6.2). Closed it with `MembershipPipelineTests` — 4 tests driving
`InviteMemberCommand` through the **real** pipeline (logging→authz→validation→handler) per docs/validation/test-strategy.md §2.2:
valid+Administrator passes; invalid command → `ValidationException` (handler never runs); unauthenticated
and wrong-role → `UnauthorizedAccessException`. Result: **11/11 tests pass, 0 warnings**;
`Membership.Application` **100% line/branch**, `Membership.Domain` **100%**, validator + behaviors **100%**.
Tracked deferrals unchanged (AuditEvent→BL-066, policy authz + 401/403→P4, localized errors→BL-016,
integration tests→P4). **P2 verdict now GO.**

**Next (await go-ahead):** P3 frontend-foundation completion (OIDC/Keycloak login, TanStack Query, `@dnd-kit`)
and/or P4 Identity & Permissions (claim→role mapping, policy + ABAC handlers, permission-matrix suite, 401/403 fix).

---

## PH-0 — Validation & Repository Foundation

### 2026-06-25 — P1 scaffold complete (STOP point; report before P2)

**Done**
- Solution `acmp.sln` (.NET 8, SDK pinned 8.0.422) + `Acmp.Shared` kernel + **Membership** reference
  module (Domain/Application/Infrastructure), MediatR pipeline (validation, authorization, audit-stamp,
  logging), `IClock`/`ICurrentUser`/`IFileStore`(MinIO)/`INotificationChannel`, ProblemDetails,
  health checks, Serilog→Seq, OpenTelemetry, EF migration `Membership_Initial`. **Builds clean; 7 tests pass**
  (3 ArchUnit boundary, 2 domain, 2 handler).
- React 18 + Vite web shell: routing, i18n EN/AR, RTL (logical CSS), light/dark tokens. **Builds clean;
  i18n parity OK (21 keys).**
- `deploy/`: `Dockerfile.api` (+curl for healthcheck), `Dockerfile.web` (nginx, SPA + `/api` proxy, CSP),
  `docker-compose.yml` (api/web/sqlserver/seq/minio), `.env.example`. `.github/workflows/ci.yml`
  (format/build/test + web build/i18n/audit), dev scripts.
- **`docker compose up --build` → healthy:** api (migrations applied on startup), sqlserver, web all
  `healthy`; seq + minio running. `/healthz`=200, `/readyz`=Healthy, `/api/members`=401 (auth lands P3 —
  pipeline + authorization behavior confirmed working).

**Fixes during bring-up**
- OTel OTLP exporter 1.9.0 → 1.10.0 (cleared an advisory; remaining NU1902 is moderate, allowed by DoD;
  logged for P16 dependency scan).
- web healthcheck `localhost`→`127.0.0.1` (busybox wget resolved IPv6 `::1`; nginx is IPv4-only).

**Decisions recorded**
- OQ-012 resolved to (a): separate nginx `web` container (per user instruction + docs/domain/repository-structure.md §8), overriding
  the recommended default (b). Logged in ph0-validation §3/§6.

**Next (P2 — await go-ahead):** backend reference-module deepening / Identity & Permissions per phase-prompts.

### 2026-06-25 — PH-0 kickoff

**Done**
- Read and confirmed the planning package: `CLAUDE.md`, `docs/README.md`, `agent-guardrails.md`,
  `claude-code-execution-package.md`, `phase-prompts.md`, `34-repository-structure.md`,
  `40-acceptance-criteria.md`, `42-open-decisions.md`.
- Produced `ph0-validation.md` (domain/module/role/core-loop understanding, OQ defaults, toolchain).
- Seeded `acceptance-audit.md` with AC-001…AC-066 → all `Pending` (no features yet).
- Verified local toolchain: .NET SDK 8.0.422 present (pinned via `global.json`), Node v26.3.1,
  Docker CLI 29.5.3, Git 2.54.0. SQL Server 8.0 runtime present.

**Decisions applied (OQ defaults + org answers 2026-06-25)**
- Env not air-gapped on build machine → direct NuGet/npm, public registry + digest pinning (OQ-031/032).
  Prod VM air-gap recorded as an open item for P16/P18 (offline images + mirror path), not a scaffold blocker.
- CI = GitHub Actions, GitHub-hosted runners for skeleton; "self-hosted runner for prod" → new OQ (OQ-041;
  renumbered from OQ-038 on 2026-06-25 — ADR-0015/CHANGE-001 canonically took OQ-038 for the Keycloak datastore).
- TLS 1.2+ default, flag for security review at P16 (OQ-024).
- MFA: Chairman+Secretary required, 60-min idle — recorded, finalized at Keycloak setup (OQ-003).
- Standby: cold + documented restore, revisit P18 (OQ-020).
- Search: v1 = SQL Server FTS (ADR-0011 / R-24). Fallback if spike fails = **app-owned OpenSearch**
  (per ADR-0011), behind a search abstraction — NOT Meilisearch. See ph0-validation §Search-discrepancy.

**Open finding**
- Docker daemon was not running at PH-0 start (CLI present, Desktop Linux engine down). Started Docker
  Desktop; FTS spike + `docker compose up` proceed once the daemon is healthy.

**Next**
- Run Arabic FTS spike (OQ-034) and record result in `ph0-validation.md`.
- Scaffold P1 per `docs/domain/repository-structure.md`; stop when `docker compose up` is healthy; report before P2.
