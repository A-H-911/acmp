---
artifact: rebuild-findings
status: active
version: v1
updated: 2026-06-30
author: forensic self-review (read-only)
purpose: Drive a design update + a second re-fidelity rebuild. Findings only — no code/design/docs changed.
---

# ACMP Rebuild Findings — Forensic Self-Review

**Scope.** Read-only forensic audit of ACMP execution through P6 + the S1–S7 test-hardening program. Sources: full git history (P3–P6 fidelity-refresh commits `125c0c5`→`b7ab531`; coverage commits `5b7141c`→`83aa33f`; the original UI-fidelity commit `16f2f8b`); the four `_progress` docs (progress-log, design-parity-ledger, acceptance-audit, ph0-validation); the running UI (`src/Acmp.Web`); every `.dc.html` in `/ACMP product context/`; and canon (`docs/14`, `docs/10`, `docs/40`, `docs/42`, `/adr`).

**Severity key.** `BLOCKER` = misleads the rebuild or breaks doc/auth integrity, must resolve first · `MAJOR` = real divergence or gap needing a design/data decision · `MINOR` = cosmetic / small / already-justified.

**Tagging.** Each row carries: ID · Category · Surface · Description · Evidence · Severity · Needs-from-design · Needs-restructure. "Op-approved" = operator signed off in the log; "Flagged" = recorded as drift but not yet blessed; "Settled-by-new-file?" answers whether a design file added after the build now governs it.

**One load-bearing correction up front (verified against code, not the log).** The design-parity-ledger and the progress-log **disagree** on the StatusChip canonical size. `StatusChip.tsx:12` + `components.css:48` show the implementation uses **md 24/9/12 (DS §08)** and **sm 22/8/11.5 (§09)**. Ledger line 41 records "StatusChip 22/8/11.5 (DS §08 canonical, overrides the dc's 23/9/12)" — which conflates the §09 *sm* value (22) with the §08 *md* canon (24). The code is the tiebreaker: §08 = 24, §09 = 22, the `.dc` showed 23. This surfaces as both a §1 deviation (DV-01) and a §8 doc-integrity item (DI-06).

---

## §1 — Design Deviations / Drift

Divergences from a `.dc.html` (visual or behavioural). "Settled-by-new-file?" = does a NEW/updated design file now resolve it.

| ID | Surface | Description | Evidence | Class | Severity | Needs from design | Needs restructure |
|---|---|---|---|---|---|---|---|
| DV-01 | StatusChip (all screens) | Impl md = **24/9/12**, sm = **22/8/11.5**; the `.dc` showed **23/9/12**. Impl overrides the dc to the DS-canon size. | `StatusChip.tsx:12`; `styles/components.css:48,52`; ledger:41 (mis-records); git `125c0c5` | Token/anatomy (DS canon overrides dc) | MINOR (visual) / see DI-06 | One canonical chip size (is §08 = 24 or 23?) | No |
| DV-02 | Notifications full page `/notifications` | Spec/docs/14 implies infinite scroll; impl uses an explicit **"Load more"** button over real server paging. | `api/notifications.ts:49` (`useInfiniteNotifications`); `NotificationsPage.tsx:120`; progress-log:597 | Op-approved ("simpler, keyboard-friendly") | MINOR | Bless Load-more vs infinite-scroll | No |
| DV-03 | Meeting workspace — Elapsed timer | Live elapsed timer renders **mm:ss / h:mm:ss**; timeboxes/durations render in **minutes** (the intended mm:ss→minutes swap applies to timeboxes only). | `MeetingWorkspace.tsx:68-75`; ledger:44 ("mm:ss→minutes" intended) | Mixed (timer mm:ss live; timebox minutes intended) | MINOR | Confirm elapsed-timer format the design wants | No |
| DV-04 | Rich text (Submit / Meeting notes / MoM) | Design shows rich-text editors; impl renders **three different things**: inert `aria-hidden` toolbar + plain `<Textarea>` (Submit), a **functional markdown-into-plaintext** toolbar (Meeting notes), and a deferred MoM. Stored content is plain text. | Submit `SubmitTopic.tsx:317-334`; Meeting `MeetingWorkspace.tsx:379-400`; MoM → P7 | Behavior SoT (data = plain text) | MAJOR | Decide rich vs markdown vs plain, one model | Yes — unify into one RTE component |
| DV-05 | Notifications full page | docs/14 "type" filter dropped; impl ships a client-side **Unread/All** toggle only. | `NotificationsPage.tsx`; progress-log:595 | Flagged (YAGNI) | MINOR | Keep or restore a type/category filter | No |
| DV-06 | Backlog rows / Kanban cards / Agenda viewer | Topic key (`TOP-YYYY-###`) **re-added** to rows the design omits, for traceability of official records. | `Backlog.tsx:295,339`; `Kanban.tsx:144`; progress-log:660 | Op-approved (CLAUDE.md canonical ID) | MINOR | Bless key-on-row | No |
| DV-07 | Agenda publish dialog | Design's "notify groups" checkboxes → one honest static line (backend notifies all). | `AgendaBuilder.tsx:289-291`; progress-log:1280 | Op-approved | MINOR | Bless single-line vs group toggles | No |
| DV-08 | Meetings list (`ACMP Meetings` isList) | **Kept** an Agenda-status chip column the design omits; **omitted** the design's filter chips + Saved-views (no backend, not faked). | git `8d55a5a`; progress-log:544-548 | Op-approved ("keep agenda chip") | MINOR | Bless added chip column; decide filters/saved-views | No |
| DV-09 | Backlog filter bar | Stream + Owner filter chips rendered but **disabled** — no option source (stream registry = BL-024). | `Backlog.tsx:179-180`; progress-log (P5 refresh) | Flagged (no data) | MAJOR | Provide the stream/owner option source | No — needs BL-024 |
| DV-10 | Submit topic — Affected streams | Free-text token input instead of the design's preset stream chips. | `SubmitTopic.tsx:355-380`; progress-log:718 (D3) | Flagged | MAJOR | Provide stream registry / chip source | No — needs BL-024 |
| DV-11 | Backlog Calendar + Timeline views | Faithful chrome, **empty body** (no markers/bars) — Topics API exposes no scheduled/due/span data. | `Calendar.tsx:65-68`; `Timeline.tsx:40-43`; progress-log:711 (D1) | Flagged (data → P6) | MAJOR | Define the date/span data contract | No — needs API |
| DV-12 | Topic detail | Single stored title; design's alternate-language title line dropped. | progress-log:723 | Flagged | MINOR | Decide bilingual title storage | No |
| DV-13 | Backlog / Meetings rows | Row navigation via a single **title link**, not the design's whole-row `<button>` (invalid inside a real `<table>`). | `Backlog.tsx`; progress-log:549,720 | Behavior/a11y-justified | MINOR | Bless title-link rows | No |
| DV-14 | Agenda builder — presenter | Design's avatar-cycle → an accessible shared `Select` sourced from `/api/members`. | `AgendaBuilder.tsx`; progress-log:1283 | A11y-justified | MINOR | Bless Select vs avatar-cycle | No |
| DV-15 | Meeting page | In-page shared `Tabs` instead of the design's top-bar tab switcher; breadcrumb drops the 3rd "Agenda builder" segment. | `MeetingPage.tsx:109`; progress-log:1208 | Op-approved | MINOR | Confirm tab placement | Maybe (see NV-08) |
| DV-16 | Meeting workspace — actual-time / outcome | The actual-time + outcome recording control was **removed from the UI** (backend `RecordActualTime` + hook retained). | progress-log:687-692 | Deferred concern (op) | MAJOR | A design-faithful actual-time control | Yes — re-add to workspace |
| DV-17 | App shell header | Solid `var(--header)` instead of the Design-System doc's translucent blur (resolved to `ACMP.dc.html` authority). | progress-log:1870; CHANGE-002 | Reconciled inter-file | MINOR | Confirm header treatment one place | No |
| DV-18 | Schedule meeting | Full-page `SchedulePage` replaced the dialog; agenda "new vs link" radio omitted (locked); Title + Chair kept though the mock omits them. | git `5cc49a9`; progress-log (P6 follow-up) | Op-approved | MINOR | Confirm create-meeting field set | No |
| DV-19 | Schedule meeting — date | Native `datetime-local` (rendered mm/dd/yyyy under RTL) replaced by a shared `DateField` + `<input type=time>`; single-day meeting. | git `b7ab531`; `ui/DateField.tsx` | Justified fix | MINOR | Confirm single-day assumption | No |
| DV-20 | Agenda viewer (read-only) | Budget bar kept; the design's read-only readiness sidebar needs data the app doesn't have. | progress-log:658 | Op-approved | MINOR | Provide readiness data or bless budget bar | No |
| DV-21 | Agenda pool label | Pool labeled "Scheduled topics" in the design but sourced from **Prepared** topics (status precedes Scheduled). | `AgendaBuilder.tsx`; progress-log:1274 | Semantic correction | MINOR | Fix the design label to "Prepared" | No |

---

## §2 — No-Reference Compositions

Screens/controls/states built with **no matching `.dc.html` at the time**. "Now exists?" reflects design files added later (`ACMP System States`, `ACMP Meetings`, `ACMP Create Flows & Dialogs`, `ACMP Diagrams` — all added in `3f7d06d` / PR #24, 2026-06-27).

| ID | Surface | Built (commit) | Reference at build time | Reference NOW | Severity | Needs from design | Needs restructure |
|---|---|---|---|---|---|---|---|
| NR-01 | Notification inbox full page `/notifications` | `bd2584f` | None (progress-log:574) | **Yes** — `ACMP.dc.html` `notifications` screen + `ACMP System States` `notif` + docs/14 p.79 | MAJOR | One spec for the full inbox; reconcile with NR-02 | Yes — reconcile to design |
| NR-02 | Notification bell popover | P6e (`#35` lineage) | None (docs/14 p.79 only) | **Yes** — `System States` `notif` / `ACMP.dc.html` `notifState` | MAJOR | Decide popover vs full-page anatomy split | Yes |
| NR-03 | Meetings list (flat table) | original `16f2f8b` / `749cb12` | Assumed none → was actually drift | **Yes** — `ACMP Meetings` isList (added later); rebuilt `8d55a5a` | MINOR (now reconciled) | — (done) | Done |
| NR-04 | Schedule-meeting dialog (original) | `749cb12` | None | **Yes** — `ACMP Meetings` `create` (p.17); replaced by `SchedulePage` `5cc49a9` | MINOR (now reconciled) | Confirm create fields (DV-18) | Done |
| NR-05 | Meeting lifecycle prompts (notReady / ready / concluded / cancelled gates) | `d297169`/P6d | None — composed from empty-card | **Yes** — `ACMP Meetings` `overview` + `lifecycle` enum (notready/scheduled/inprogress/concluded/cancelled) | MAJOR | Reconcile prompts to the lifecycle screen | Yes |
| NR-06 | Minutes placeholder tab | P6d | `isMinutes` exists but unbuilt | `ACMP Agenda & Meeting` `minutes` (minutesState draft/review/published) — **unbuilt → P7** | MINOR (honest placeholder) | Build MoM in P7 from existing design | Later phase |
| NR-07 | Recording empty card | `1862501` | Styled to `isRecording recNoTranscript` | `ACMP Meetings` `recording` (recState ready/pending/notranscript) — recReady → Webex P2 | MINOR | recReady (player/transcript) waits on Webex | Later phase |
| NR-08 | Admin "System Health" tab | `245b32c` (disabled) | "not yet ticketed" (progress-log:796) | **Yes** — `ACMP Administration` `health` section exists | MAJOR | Build health from the existing design section | Yes — remove "no-ref" label |
| NR-09 | OIDC callback / 404 / generic error pages | P3 (`5fae8a8`) | None (predate System States) | **Yes** — `ACMP System States` `callback` / `404` / `error` | MINOR | Reconcile to System States | Yes |
| NR-10 | `PlaceholderPage` "coming soon" screen | P3 | None (generic stand-in) | n/a — the *target* screens have designs (Lists & Registers, Decision/Voting/ADR, Research & Knowledge, Diagrams, Dashboards) | MAJOR | Build the real screens; retire PlaceholderPage | Yes (see NV-06) |

---

## §3 — Redundant / Not-Meant-to-Exist

### (a) In the BUILD

| ID | Surface | Description | Evidence | Severity | Keep / Remove / Expose |
|---|---|---|---|---|---|
| RD-01 | TopBar | `DevRoleSwitcher` — dev-only role switcher; dynamically imported behind `import.meta.env.DEV`, tree-shaken from prod, second guard on `devSetRoles`. | `TopBar.tsx:19-21,96-100`; `DevRoleSwitcher.tsx:34` | MINOR | Keep (dev-only); confirm intended |
| RD-02 | Auth | DEV auth stub mints `Dev User`/`DV` + `devSetRoles`; prod-unconfigured path fails closed. | `AuthProvider.tsx:86-90,114-117` | MINOR | Keep (dev-only) |
| RD-03 | Meeting workspace | Disabled stubs: **Pause** (`:146`), **Record decision** (`:267`), **Create action** (`:270`), **Call vote** (`:273`) — all `disabled title=comingSoon`. | `MeetingWorkspace.tsx:146-275` | MAJOR | Decide keep-disabled vs hide-until-built (P7/P8/P9) |
| RD-04 | Agenda builder / Topic detail / Backlog | More disabled stubs: **Preview** (`AgendaBuilder.tsx:200`), **Add-to-agenda** + **Edit** (`TopicDetail.tsx:139,142`), **Attachment download** (`TopicDetail.tsx:214`), **Export** (`Backlog.tsx:123`), **Saved view** (`Backlog.tsx:146`), Stream/Owner filters (`Backlog.tsx:179-180`). | as cited | MAJOR | Decide keep-disabled vs hide |
| RD-05 | Topic detail | **Votes tab** renders only an EmptyState ("voting arrives in P9"). | `TopicDetail.tsx:225-232` | MINOR | Keep (honest) until P9 |
| RD-06 | Submit topic | Inert `aria-hidden` RTE toolbar (dead glyphs). | `SubmitTopic.tsx:317-326` | MINOR | Tie to DV-04 RTE decision |
| RD-07 | (verification) Leftover mock/demo data | **None in shipped feature code** — only `Dev User` in the dev stub; `Khalid A.` appears only in `*.test.tsx`. Original prototype scaffolding (`greetMap`/`Tweaks`) was never ported. | agent sweep; progress-log:1855 | — | Affirm clean — no action |

### (b) In the DESIGN

| ID | Surface | Description | Evidence | Severity | Needs from design |
|---|---|---|---|---|---|
| RD-08 | `ACMP Meetings` vs `ACMP Agenda & Meeting` | **Overlap**: both render a meeting-detail shell; the `denied` state is duplicated. Neither is authoritative for "the meeting page" end-to-end — Meetings owns `list/create/overview/recording`, Agenda&Meeting owns `agenda/meeting(conduct)/minutes`. | `.dc` screen enums; design-agent §B | BLOCKER | Assign single ownership of the meeting header + de-dupe `denied`/`overview`/`meeting` |
| RD-09 | Notification surfaces | Three overlapping references: `ACMP.dc.html` `notifications` screen, `ACMP System States` `notif`, docs/14 p.79 inbox + p.80 preferences. | design-agent §A,§E | MAJOR | Collapse to one notification spec (popover + full page + preferences) |
| RD-10 | Registers vs detail | `ACMP Lists & Registers` carries `decisions`/`adrs` registers while `ACMP Decision, Voting & ADR` carries decision/adr **detail** — likely list-vs-detail, but confirm no contradiction. | design-agent §A | MINOR | Confirm register vs detail split is intentional |

---

## §4 — Hidden / Dev-Only / Source-Only Controls

Present in code but not visible (or inert) in the running prod UI.

| ID | Control | Gating | Evidence | Disposition question |
|---|---|---|---|---|
| HD-01 | `DevRoleSwitcher` | `import.meta.env.DEV` dynamic import + `devSetRoles` guard; tree-shaken from prod | `TopBar.tsx:19-21`; `DevRoleSwitcher.tsx:34` | Keep dev-only |
| HD-02 | DEV `DevAuthProvider` (auto-auth stub) | `import.meta.env.DEV`; prod returns `FAIL_CLOSED` | `AuthProvider.tsx:106-118` | Keep dev-only |
| HD-03 | Submit inert RTE toolbar | `aria-hidden="true"`, non-interactive spans | `SubmitTopic.tsx:317` | Remove or activate (DV-04) |
| HD-04 | Disabled `comingSoon` controls (RD-03/04) | `disabled` attribute + tooltip; present in prod DOM but inert | as cited | Keep-disabled vs hide |
| HD-05 | `/search` route | No nav item; reachable only via TopBar search box / Ctrl-⌘K → renders PlaceholderPage | `App.tsx:50`; `TopBar.tsx:58-61` | Build search or hide the entry |
| HD-06 | Functional markdown RTE (meeting notes) | **Not hidden** — works, unlike the inert Submit toolbar (HD-03). Inconsistency. | `MeetingWorkspace.tsx:379` | Reconcile with HD-03 (DV-04) |

---

## §5 — Navigation & IA

| ID | Item | Description | Evidence | Severity | Needs from design | Needs restructure |
|---|---|---|---|---|---|---|
| NV-01 | "Agenda" → `/meetings` | The `agenda` nav item routes to `/meetings` (MeetingsList); no `/agenda` route exists. **Canon-correct** per docs/14:19 ("Agenda & Meetings" = Meetings module), but the label key is `nav.agenda` ("Agenda") vs canon "Agenda & Meetings". | `navModel.ts:33`; `App.tsx:38`; docs/14:19 | MINOR | Confirm label text "Agenda & Meetings" | No |
| NV-02 | Decisions / ADRs | Impl ships **two** nav items (`decisions`, `adrs`); docs/14:20 **groups** them as "Decisions & ADRs". | `navModel.ts:34,36`; docs/14:20 | MAJOR | Confirm grouped vs split | Yes — nav model |
| NV-03 | Research / Knowledge | Impl ships **two** items (`research`, `wiki`); docs/14:25 groups them as "Research". | `navModel.ts:39,40`; docs/14:25 | MAJOR | Confirm grouped vs split | Yes — nav model |
| NV-04 | Governance / Architecture Invariants | **Missing** — impl has no Governance/Invariants nav area; docs/14:21 lists it as #5. | `navModel.ts:28-45`; docs/14:21 | MAJOR | Confirm Governance nav + screen | Yes — add area |
| NV-05 | Audit | Impl has a top-level `audit` → `/admin/audit`; docs/14 places audit under Admin/System + per-page History, not a top-level nav. | `navModel.ts:43`; `App.tsx:56`; docs/14 §C | MINOR | Confirm audit placement | Maybe |
| NV-06 | Placeholder nav walls | **11 sidebar items + the "My Session" CTA** route to `PlaceholderPage` ("coming soon"): decisions, actions, adrs, risks, deps, research, wiki, diagrams, reports, audit, session. Designs **exist** for most (Lists & Registers, Decision/Voting/ADR, Research & Knowledge, Diagrams, Dashboards). | `App.tsx:34,41-50,56`; `navModel.ts:34-43`; UI-agent §1 | MAJOR | Decide: hide unbuilt items vs show coming-soon | Yes — build from existing designs |
| NV-07 | Breadcrumb location | Breadcrumb rendered **per page** (`Backlog.tsx:112`, `MeetingPage.tsx:100`, `NotificationsPage.tsx:54`), not in the shell; `PlaceholderPage`/Dashboard have none → inconsistency. | `AppShell.tsx` (no breadcrumb); UI-agent §1d | MAJOR | Confirm breadcrumb on every page | Yes — lift into shell/layout |
| NV-08 | Meeting-detail sub-tabs | docs/14:85 = Agenda · Attendance · Notes · MoM · Recording (5); impl collapses to **Agenda builder \| Meeting (attendance+notes inline) \| Minutes \| Recording**. | `MeetingPage.tsx:109-123`; docs/14:85 | MAJOR | Reconcile the meeting tab IA (with RD-08) | Yes |
| NV-09 | Global search | `/search` → PlaceholderPage; docs/14:38 specifies grouped cross-artifact results. | `App.tsx:50`; docs/14:38 | MAJOR | Search results design exists? Build it | Later phase |
| NV-10 | "My Session" CTA | `session` CTA → `/session` PlaceholderPage (dashboard quick action, unbuilt). | `navModel.ts:29`; `App.tsx:34` | MINOR | Define My-Session screen | Later phase |

---

## §6 — Layout / Spacing / Padding / Scrollbars / Breadcrumbs (systemic)

| ID | Pattern | Description | Evidence | Severity | Needs from design | Needs restructure |
|---|---|---|---|---|---|---|
| LP-01 | Breadcrumb 12px gap | Owned globally on shared `.breadcrumb` (12px gap below); page-scoped override removed. Good — but per-page **rendering** means pages without a breadcrumb (placeholders) lose the rhythm. | git `5cc49a9`; memory rule; NV-07 | MINOR | — | Yes — lift breadcrumb into shell |
| LP-02 | Literal-px vs token drift | Recurring: design's **off-scale** values rounded to nearest token, then reconciled with `/* design literal */` px passes (e.g. `35db5a2` ~16 agenda values; `--control-radius:9px` added CHANGE-002; chip 11.5px). Root cause: token scale doesn't cover the design's literals. | git `35db5a2`; CHANGE-002; `components.css:52` | MAJOR | Publish off-scale literals as named tokens | Yes — extend token scale |
| LP-03 | Form rhythm collision | Global `.field + .field` margin double-counted a flex/grid `gap` (pushed 2nd field of two-col rows 16px down); fixed with a scoped reset. | git `b7ab531`; progress-log:274 (parity-ledger) | MINOR | Spacing model for stacked vs grid fields | Yes — one field-rhythm rule |
| LP-04 | Scroll container | Document scrolls; TopBar + SideNav are sticky; **no `app-main` overflow container**; no custom scrollbar styling found. Matches `ACMP.dc.html`. | CHANGE-002; progress-log:1930 | MINOR | Confirm scroll model + scrollbar treatment | No |
| LP-05 | Page-width cap | Coherent `72rem` cap across screens; the meeting workspace's earlier widening was reverted to match. | progress-log:692 | MINOR | Confirm max-width per surface | No |
| LP-06 | RTL logical CSS | Logical-properties-only enforced (grep = zero physical L/R/margin/padding in meetings/admin/topics CSS); two deliberate `scaleX(-1)` chevron flips. | progress-log:821,1268 | MINOR (good) | — | No |

---

## §7 — Design Ambiguities / Contradictions / Gaps

Where the design was unclear/silent and an assumption was forced.

| ID | Area | Assumption made | What design must clarify | Evidence | Severity |
|---|---|---|---|---|---|
| AM-01 | StatusChip size | Used DS §08 = 24/9/12 (md) + §09 = 22/8/11.5 (sm), overriding the dc's 23. | The single canonical chip size (is §08 = 24 or 23?). | `StatusChip.tsx:12`; ledger:41 | MAJOR |
| AM-02 | Shell header height | Resolved the design file's own **58-vs-60px** self-inconsistency to 58. | Fix the header height in the design. | progress-log:1871 | MINOR |
| AM-03 | Meeting page ownership | Composed from both meeting files; assumed Meetings owns list/overview/recording, Agenda&Meeting owns agenda/conduct/minutes. | Which file owns the meeting header + `denied` (RD-08). | design-agent §B | BLOCKER |
| AM-04 | Meeting tab IA | Collapsed 5 docs/14 sub-tabs into 4 impl tabs. | One reconciled meeting sub-tab structure (NV-08). | `MeetingPage.tsx`; docs/14:85 | MAJOR |
| AM-05 | Missing states | Invented meeting lifecycle prompts / minutes placeholder / recording empty / route-level permission-denied where the design was silent. | Confirm these now map to System States + Meetings lifecycle. | NR-05/06/07; progress-log:622 | MAJOR |
| AM-06 | Rich text model | ~~Assumed plain text (Submit inert; Notes markdown-into-plaintext; MoM deferred).~~ **RESOLVED (DV-04, 2026-07-01): markdown stored as text, one shared `MarkdownEditor`** (Submit + Notes + Minutes). Read-rendering deferred (no markdown→HTML dependency yet). | ~~Rich vs markdown vs plain, and what is stored.~~ Decided. | DV-04; progress-log "DV-04" | ~~MAJOR~~ DONE |
| AM-07 | Calendar/Timeline data | Assumed honest-empty (no date/span source in Topics API). | Define the scheduled/due/span data contract. | DV-11; progress-log:711 | MAJOR |
| AM-08 | Filter option source | Assumed Stream/Owner filters disabled (no registry). | Where filter options come from (BL-024). | DV-09 | MAJOR |
| AM-09 | Notification preferences | Built inbox only; assumed v1 = in-app, no preferences page. | Whether v1 includes `/notifications/preferences` (docs/14 p.80). | RD-09; docs/14 p.80 | MINOR |
| AM-10 | Agenda pool label | Used "Prepared" semantics under the design's "Scheduled topics" label. | Correct the pool label in the design. | DV-21 | MINOR |

---

## §8 — Open Questions / Doc-Integrity

| ID | Item | Description | Evidence | Severity | Action (flag only) |
|---|---|---|---|---|---|
| DI-01 | **ADR-0015 number collision** | Two distinct files both titled "ADR-0015", both Accepted, both 2026-06-25: *Adopt React 19 (amends 0012)* and *Self-Hosts Keycloak*. `adr/README.md:25` indexes **only** the Keycloak one; the React-19 ADR-0015 is an unindexed orphan, violating the README's own "numbers are never reused". | `adr/ADR-0015-react-19-amends-0012.md:1`; `adr/ADR-0015-self-hosted-keycloak-...md:1`; `adr/README.md:25` | BLOCKER | Renumber the React-19 ADR (e.g. ADR-0017) + index it |
| DI-02 | **OQ-043 RowVersion drift** | `docs/16` §1.5 requires `RowVersion ROWVERSION` on every mutable aggregate root (→ 409 on stale write); **zero** `IsRowVersion`/`[Timestamp]`/concurrency token in any `*.cs` or migration. Documented backstop doesn't exist. | progress-log:238; `docs/42` OQ-043 | MAJOR | Add the feature+migration+ADR, or amend docs/16 to last-writer-wins |
| DI-03 | OQ-042 invite/provision | RESOLVED (2026-06-27, option b): any future "Provision via Keycloak" is a deep-link to the KC console only; build removed the in-app invite (conflicted ADR-0015). | `docs/42` OQ-042; progress-log:806,857 | MINOR (resolved) | Record as closed |
| DI-04 | OQ-041 gap | ph0-validation renumbered the CI-runner note from OQ-038 → **OQ-041**, but `docs/42` has **no OQ-041 entry** (jumps 040 → 042). | ph0-validation:67-73; `docs/42` (no 041) | MINOR | Add OQ-041 to docs/42 or fix the cross-ref |
| DI-05 | OQ ordering | `docs/42` appends OQ-042/043 out of order after OQ-040; no 041. | `docs/42` OQ-040..043 | MINOR | Re-sequence |
| DI-06 | Ledger StatusChip mis-record | design-parity-ledger:41 records DS §08 = 22/8/11.5; code + P3-refresh entry say §08 = 24/9/12 (22 is §09 sm). | ledger:41; `components.css:48`; progress-log (P3 refresh) | MINOR | Correct the ledger row |
| DI-07 | OQ-012 SPA serving | `docs/42` default = (b) ASP.NET static files; the project chose (a) **separate nginx web container** per user instruction. Resolved, but the docs disagree on the default vs the choice. | `docs/42` OQ-012; ph0-validation:55 | MINOR (reconciled) | Note the override in docs/42 |
| DI-08 | OQ-034 search engine | `docs/42` OQ-034 names **Meilisearch** as the FTS escalation; ADR-0011 / README name **OpenSearch**. ph0-validation §4 flags canon wins but the correction was **not yet applied**. | ph0-validation:86-92; `docs/42` OQ-034 | MAJOR | Correct OQ-034 to OpenSearch (needs Tech-Lead) |

---

## §9 — Per-Phase Summary

### P3 — Foundation fidelity refresh (`125c0c5`)
- **Changed.** Tokens/components/shell/nav reconciled to the *updated* `Design System` / `ACMP.dc.html` / `Navigation & IA`. StatusChip → DS §08 24/9/12 + new `sm` 22/8/11.5; TopBar "Ctrl K" search + real focus shortcut; brand/icon/chip metrics; notification popover geometry.
- **Residual caveats.** Domain components (relationship/kanban/voting/editor) deferred to owning phases; sidebar-248 / header-58 kept as documented inter-file deltas (resolved the design's own 58-vs-60 self-inconsistency); **no automated i18n parity gate exists** (noted). No surface still references an old design at this layer.

### P4 — Administration → Users & Membership (`245b32c`)
- **Changed.** Rebuilt to `ACMP Administration.dc.html`: 7-tab admin strip (only Users active), rich directory (committee chips, read-only voting switch, assignments), in-place read-only UserDetail. Removed "Provision via Keycloak" + invite (ADR-0015 conflict) → **OQ-042**.
- **Residual caveats.** **Six admin sub-tabs unbuilt and disabled** (Templates / System Health / Streams / Roles / Job Monitor / Notification Settings) — their designs **exist** as `ACMP Administration` sections (`templates/health/streams/roles/jobs/notif`); assignments show honest "—" (no count API); live VR blocked on the operator setting the `acmp-admin` dev password.

### P5 — Topics & Backlog refresh (`6e28110`)
- **Changed.** Rebuilt to `ACMP Backlog & Topic.dc.html`: new shared `FilterChip` bar, Calendar + Timeline promoted to first-class live views, 5-tab topic detail (Overview/Discussion/Attachments/Votes/History), inert RTE on Submit.
- **Residual caveats.** Stream/Owner filter chips **disabled** (no registry, BL-024); Calendar/Timeline render **empty** (no date/span data → P6); Votes tab empty → P9; submit stores plain text only; affected-streams = free-text.

### P6 — Meetings module + Notifications (`d297169`, `5cc49a9`, `f212a3e`, `9d31ac4`, `35db5a2`, `3e287c6`, `1862501`, `bd2584f`, `8d55a5a`, `b7ab531`)
- **Changed.** Meeting workspace to `isMeeting`; full-page Schedule + Type/Mode backend + migration; agenda chip tones + localized Locked/Closed; agenda builder literal-px pass; read-only agenda viewer (`isOverview`); recording empty card (`isRecording recNoTranscript`); full-page notification inbox; meetings list rebuilt to `isList` (Upcoming/Past + List⇄Calendar); create-meeting field-rhythm + `DateField`.
- **Residual caveats.** Record-decision / Create-action / Call-vote = disabled stubs → P7/P8/P9; **actual-time/outcome control removed from the UI** (backend retained); Minutes = honest placeholder → P7; Recording `recReady` → Webex P2; meetings list keeps the Agenda-status chip the design omits (op-approved); the notification full page was a no-reference composition (now governed by System States / `ACMP.dc.html`); Preview / notify-group / Pause = mock chrome.

### S1–S7 — Test-hardening (`5b7141c`→`83aa33f`)
- **Changed.** Coverage tooling + ADR-0016 basis; adversarial BE invariants (S1), FE auth/data (S2), BE Api endpoints (S3), FE screen-states (S4), Testcontainers SQL backstop (S5), E2E harness + real Keycloak PKCE (S6a), core-loop / drag / RTL-a11y E2E (S6b), ≥95% per-file coverage gate (S7), AC reconciliation (`83aa33f`: **AC-035 Partial→Met**; caveats closed on AC-001/044/051; AC-041/034/043/048/057 stay Partial).
- **Residual caveats.** Four `/* v8 ignore */` browser-only paths (drag); `RowVersion` backstop can't be tested because it doesn't exist (**OQ-043**); AC-041 still needs pixel-diff visual-regression (→ P17); several Partials are unbuilt UI, not missing tests.

---

## §10 — Patterns & Root Causes

### Recurring root causes

1. **Design landed AFTER the screen was built → no-reference scaffolding.** `16f2f8b` (2026-06-26) built the shell, Login, Admin, and a **flat meetings table**; `3f7d06d`/PR #24 (2026-06-27) then added `ACMP Meetings`, `ACMP System States`, `ACMP Create Flows & Dialogs`, `ACMP Diagrams`. So the meetings list, notification center, lifecycle prompts, and callback/404/error pages were all composed before their governing design existed (NR-01/02/03/05/09). **The second rebuild's main job is to reconcile each of these to the now-existing file.**
2. **Design omits a required state → it gets invented.** Meeting lifecycle prompts, minutes placeholder, recording empty, route-level permission-denied (AM-05). Several are now retroactively covered by System States + Meetings `lifecycle`/`recState`.
3. **Two design files cover one area → overlapping authority.** Meetings vs Agenda&Meeting (RD-08, BLOCKER); three notification surfaces (RD-09); registers vs detail (RD-10). Drift risk until ownership is assigned per surface.
4. **Token scale doesn't cover the design's off-scale literals → round-then-reconcile churn.** The repeated P6 "pixel passes" and `--control-radius:9px` / chip-11.5px additions (LP-02) are all this one cause.
5. **Backend data contract lags the design → honest-empty surfaces.** Calendar/Timeline, relationships sidebar, assignments count, Votes tab (DV-09/10/11, RD-05). The design assumes data the API doesn't expose yet.
6. **Demo scaffolding did NOT leak** (RD-07) — the only runtime-present dev affordances (DevRoleSwitcher, dev auth stub) are `import.meta.env.DEV`-gated and tree-shaken. Clean.
7. **Doc-integrity drift accumulated** — ADR number reuse (DI-01), OQ numbering gaps (DI-04/05), ledger mis-record (DI-06), doc-vs-code (DI-02), doc-vs-doc (DI-07/08).
8. **Rich-text was decided three different ways** (DV-04 / HD-06) — inert toolbar, functional markdown, deferred — with no single rich-text decision.

### What the DESIGN UPDATE must resolve (prioritized)

1. **(BLOCKER) Meeting-page ownership** — assign one authoritative file for the meeting header/detail; de-duplicate `denied`/`overview`/`meeting`; define the single meeting sub-tab IA (RD-08, NV-08, AM-03/04).
2. **(BLOCKER) Resolve the ADR-0015 collision** — renumber the React-19 ADR and index it (DI-01).
3. **Notification surfaces** — collapse `ACMP.dc.html` `notifications` + System States `notif` + docs/14 p.79 inbox + p.80 preferences into one spec; decide if preferences ships in v1 (RD-09, NR-01/02, AM-09).
4. **Data contracts** — define the date/span/stream/owner/vote/assignment-count data the Calendar, Timeline, filters, Votes tab, and assignments column need so honest-empty can be filled (DV-09/10/11, AM-07/08).
5. **One canonical chip size + off-scale token set** — settle StatusChip (24 vs 23) and publish the design's off-scale literals (radius 9, chip 11.5, etc.) as named tokens to kill pixel-drift (AM-01, LP-02, DI-06).
6. **Rich-text decision** — one model (rich / markdown / plain) and what is stored, across Submit / Notes / MoM (DV-04).
7. **Nav IA** — group Decisions&ADRs (NV-02) and Research&Knowledge (NV-03), add Governance/Invariants (NV-04), place Audit (NV-05), and decide how unbuilt nav items present (hide vs coming-soon) (NV-06).
8. **Bless or revise the deliberate deviations** — topic-key on rows (DV-06), kept agenda-chip column (DV-08), Load-more vs infinite (DV-02), presenter Select (DV-14), title-link rows (DV-13), single-day meeting (DV-19), notify-single-line (DV-07).
9. **Provide/confirm the still-missing states** — System Health (Administration `health` exists — build it, NR-08), meeting lifecycle (Meetings `overview`/`lifecycle` exists — reconcile, NR-05), notification preferences (AM-09).

### Where a FILE / COMPONENT RESTRUCTURE would help (prioritized)

1. **Lift the breadcrumb into the shell / a layout route** so every page (incl. placeholders and the dashboard) gets a consistent breadcrumb + 12px gap (NV-07, LP-01).
2. **Build the unbuilt later-phase modules from their EXISTING designs** and retire `PlaceholderPage` for them: actions/risks/deps/adrs/decisions/audit (`ACMP Lists & Registers`), decisions/voting/ADRs (`ACMP Decision, Voting & ADR`), research/wiki (`ACMP Research & Knowledge`), diagrams (`ACMP Diagrams`), reports/dashboards (`ACMP Dashboards & Reports`), the 6 remaining Admin sub-tabs (`ACMP Administration`) (NV-06, NR-08, NR-10).
3. **Unify rich text into one shared component** (currently 3 divergent implementations) (DV-04, HD-06).
4. **Reconcile the meeting-detail tabs** into a single structure once the design ownership is decided (NV-08, DV-15).
5. **Extend the design-token scale** with the off-scale literals so screens stop hard-coding `/* design literal */` px (LP-02).
6. **Re-add the actual-time/outcome control** to the meeting workspace (backend already wired) (DV-16).
7. **Doc hygiene pass** — renumber ADR-0015, add OQ-041, correct OQ-034 search engine, decide RowVersion, fix the ledger chip row (DI-01/02/04/06/08).

---

### Appendix — design file → built surface map (governance for rebuild #2)

| `.dc.html` | Added | Governs | Built? |
|---|---|---|---|
| ACMP (shell) | `16f2f8b` | shell, home, backlog, topic, notifications, search | shell/home/backlog/topic ✅; notifications/search partial |
| Design System | `16f2f8b` | tokens + component library | ✅ shared lib |
| Navigation & IA | `16f2f8b` | role-filtered nav + RTL | ✅ (with NV-02/03/04 deltas) |
| Sign In | `16f2f8b` | `/login` + expired re-auth | ✅ |
| Logo | `16f2f8b` | brand asset | ✅ |
| Backlog & Topic | `16f2f8b` | backlog (5 views), submit, detail | ✅ (Calendar/Timeline empty) |
| Agenda & Meeting | `16f2f8b` | agenda builder, conduct, **minutes** | agenda+conduct ✅; minutes ❌ (P7) |
| Administration | `16f2f8b` | users, templates, **health**, streams, roles, jobs, notif, userdetail | users+detail ✅; 6 tabs ❌ |
| Decision, Voting & ADR | `16f2f8b` | voting, decision, adr | ❌ (Placeholder) |
| Lists & Registers | `16f2f8b` | actions, risks, deps, adrs, decisions, audit | ❌ (Placeholder) |
| Dashboards & Reports | `16f2f8b` | role dashboards + reports | ❌ (Placeholder) |
| Research & Knowledge | `16f2f8b` | research, wiki | ❌ (Placeholder) |
| Traceability & Dependencies | `16f2f8b` | relationships/impact graph | ❌ (empty sidebar) |
| **Meetings** | **`3f7d06d` #24** | list, create, overview, recording, lifecycle | list/create/recording ✅; overview/lifecycle partial |
| **System States** | **`3f7d06d` #24** | profile, notif, callback, 404, error | callback/404/error ✅; profile/notif partial |
| **Create Flows & Dialogs** | **`3f7d06d` #24** | create forms + dialogs (decision/adr/invariant/action/risk/vote/dependency/mission) | ❌ (those modules unbuilt) |
| **Diagrams** | **`3f7d06d` #24** | gallery, viewer, spec (Phase 2) | ❌ (Placeholder) |

*Bold = design file added AFTER the original build (root cause #1).*
