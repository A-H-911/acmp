---
artifact: progress-log
status: active
version: v1
updated: 2026-06-26
---

# ACMP Progress Log

Per-phase, dated log of execution progress. Keystone gate **G-PROGRESS**.
Newest entries on top. Each entry: what was done, decisions applied, what's next.

---

## P5 — Topic & Backlog Management

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
- **Domain** (`Topic` aggregate). Full canonical lifecycle state machine (docs/12 §1) — Submit/Triage/
  Accept/Reject/Defer/Reactivate/Prepare/Reopen/Schedule/Decide/Close/Convert; guards reject illegal
  transitions; content locks after Accept (AC-034); metadata editable until Decided, then immutable.
  Enums per docs/09 (Type×4, Urgency Normal/Urgent/Critical, Scope×4, Source×10, Status×13). Child
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

**Datastore = OQ-038 → (a) Postgres-for-Keycloak.** `docs/42` default; app data stays SQL-only (ADR-0003).

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
- **OQ-038 ID collision fixed.** Canon `docs/42` binds **OQ-038 = Keycloak datastore**; a stale PH-0 note had
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
- **Policy registry (docs/10 §C).** 31 named policies registered as `CapabilityRequirement(allowRoles,
  ownerRoles)`; Deny = absence of both, so **SoD-5** (Administrator walled off committee content) is
  structural. `CapabilityHandler` evaluates RBAC → allow-if-owner relationship → delegation widening.
- **ABAC (docs/10 §D/§E).** `IAbacResource` contracts (`ITopicScopedResource`/`IStreamScopedResource`),
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
- **AuditEvent on every state-mutating op** (guardrail 5 / docs/26 / DoD [HARD]) — `DeactivateMember`,
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
  docs/40 allows "deny OR minimum default".
- **OQ-AUTH-001/002/003** resolved to docs/10 recommended defaults: read-visible/write-scoped streams
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
**ACMP Design System** and **Navigation & IA** files (visual layer) over docs/14 behavior:
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
- **States** — Empty/Loading(skeleton)/Error/PermissionDenied + class `ErrorBoundary` (docs/14 §4); `StatusChip`
  (label + dot, never color-alone), `Button`, `Card`.
- **Accessible DnD** — shared generic `SortableList` (`@dnd-kit` pointer+keyboard) **plus** explicit Move up/down
  keyboard fallback (docs/14 §5, ADR-0012). Component + test only; backlog/agenda consume it at P5/P6.
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
- **React 19 vs ADR-0012 (says 18).** P1 silently installed React 19. Surfaced and resolved via **ADR-0015**
  (amends ADR-0012, keeps 19) — a settled-ADR change needs an ADR, not just a log line (guardrail 1). ADR-0012
  carries a forward-link note; adr/README index updated.
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
  docs/34 §5) deferred to P4, when a JWT injector exists to exercise the HTTP authz path meaningfully.
  Handler-level slice tests cover Invite→Get end-to-end today.

**Acceptance audit.** Unchanged — all 66 ACs remain `Pending`. P2 is a pattern/foundation phase; the Membership
feature ACs (AC-058/059) land in P4 with HTTP + authz + UI. Domain capability + unit tests exist but the criteria
are not yet demonstrable end-to-end, so nothing flips to Met/Partial (conservative; G-TRACE).

### 2026-06-25 — P2 review: closed the one blocker (pipeline/validator test coverage)

P2 review (audit-only) found a single blocking gap: handler tests bypassed the MediatR pipeline, leaving
`InviteMemberValidator` and all three behaviors at **0%** coverage and `Membership.Application` at **70.5%
line** (below the 80% gate, docs/31 §6.2). Closed it with `MembershipPipelineTests` — 4 tests driving
`InviteMemberCommand` through the **real** pipeline (logging→authz→validation→handler) per docs/31 §2.2:
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
- OQ-012 resolved to (a): separate nginx `web` container (per user instruction + docs/34 §8), overriding
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
- Scaffold P1 per `docs/34`; stop when `docker compose up` is healthy; report before P2.
