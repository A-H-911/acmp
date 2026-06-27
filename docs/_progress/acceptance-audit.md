---
artifact: acceptance-audit
status: active
version: v1
updated: 2026-06-27
---

# ACMP Acceptance Audit

Every `AC-###` from `docs/40-acceptance-criteria.md` → verdict. Keystone gate **G-PROGRESS**.
A requirement is not "done" until its AC is `Met` and traces to ≥1 test (gate **G-TRACE**).

**Verdicts:** `Met` · `Partial` · `Not-met` · `Pending` (not yet implemented).

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

| AC | Section | Verdict | Test ref | Notes |
|---|---|---|---|---|
| AC-001 | Auth & Identity | Met | manual (live UI: ACMP /login → Keycloak → /dashboard authenticated; + token roles Administrator,Secretary / aud acmp-api / GET /api/members 200) | Full SSO round-trip through the app UI verified (after CSP connect-src fix). Logout button added (TopBar) and verified end-to-end (dashboard → /login). Automated UI regression → P17 |
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
| AC-012 | SoD-1 | Partial | SegregationOfDutiesTests | Verifier≠owner predicate proven; Action.Verify enforcement → P8 |
| AC-013 | SoD-1 | Partial | SegregationOfDutiesTests | Independent-verifier predicate proven; positive path at Action.Verify → P8 |
| AC-014 | SoD-2 | Pending | — | MoM approver = sole author → MoM module (P7) |
| AC-015 | SoD-3 | Partial | SegregationOfDutiesTests | Co-attestation predicate proven; Vote close + chair-approve enforcement → P9 |
| AC-016 | SoD-3 | Partial | SegregationOfDutiesTests | Co-attestation predicate proven; override-with-co-attest record → P9 |
| AC-017 | Audit | Pending | — | State change → audit entry |
| AC-018 | Audit | Pending | — | Audit row immutable |
| AC-019 | Audit | Pending | — | Hash-chain integrity check |
| AC-020 | Audit | Pending | — | Auditor search; others 403 |
| AC-021 | Voting | Pending | — | Vote config locked on open |
| AC-022 | Voting | Pending | — | No double-vote |
| AC-023 | Voting | Pending | — | Attributed ballots visible |
| AC-024 | Voting | Pending | — | Quorum gate on close |
| AC-025 | Voting | Pending | — | Immutable after close |
| AC-026 | Voting | Pending | — | Forward-only lifecycle |
| AC-027 | Decisions | Pending | — | Issued decision immutable |
| AC-028 | Decisions | Pending | — | Supersession back-link |
| AC-029 | Decisions | Pending | — | Downstream link required to issue |
| AC-030 | Topic lifecycle | Partial | SubmitTopicValidator tests + TopicApiTests + SubmitTopic.test (client validation) | Server validation + HTTP 400 + no record; submit form now shows localized client-side required-field errors; server-side localized messages → BL-016 |
| AC-031 | Topic lifecycle | Met | TopicApplicationTests + TopicApiTests (reject no-reason → 400) | Mandatory rejection rationale enforced |
| AC-032 | Topic lifecycle | Partial | TopicTests + TopicHandlerTests | Immutable rejection history event persisted; submitter notify → Notifications phase |
| AC-033 | Topic lifecycle | Partial | TopicTests | Rejection event append-only (no mutation surface); DB-enforced immutability + hash-chain → BL-066 |
| AC-034 | Topic lifecycle | Partial | TopicTests + UpdateTopic handler | Content locked post-accept; metadata-only Secretary edit; live 403 path → P17 |
| AC-035 | Topic lifecycle | Partial | TopicTests + PrepareTopic handler | Accepted→Prepared transition + TopicPrepared audit proven |
| AC-036 | MoM | Pending | — | Published MoM → versioned supersede |
| AC-037 | MoM | Pending | — | Change-request → back to Draft |
| AC-038 | MoM | Pending | — | Approve → Published + notify |
| AC-039 | Localization | Met | SubmitTopic.test (locale-switch preserves value) | Submit form state survives an EN↔AR switch (React state, form not keyed on language) |
| AC-040 | Localization | Met | i18n/direction.test.ts + axe render | dir=rtl mirrored layout — sidebar→inline-end, Arabic font, logical CSS; verified live (P3) |
| AC-041 | Localization | Partial | manual render (Playwright) | RTL render confirmed clean by hand; automated visual-regression suite → P17 |
| AC-042 | Localization | Met | theme/theme.test.ts | Theme persisted via localStorage + applied as data-theme |
| AC-043 | Accessibility | Partial | Kanban.test (M-move popover) + topicMeta.test | Keyboard alternative for **status** moves shipped (the "M" move popover; legal moves open accept/return dialogs, illegal moves announced). The AC's literal **priority-ordinal move-up/down with a persisted ordinal** (BL-039 within-column reorder, BL-041) is **not yet built** — deliberately deferred to a follow-up slice. Corrected from Met (P5-review remediation, 2026-06-27). |
| AC-044 | Accessibility | Met | AgendaBuilder.test (move ±1 + aria-live announce, axe AA) + MeetingHandlerTests (move ±1) | Keyboard-accessible agenda reorder shipped: the move up/down buttons send a single ±1 `move` (disabled at the ends) with a synchronous `aria-live` announce; native drag is progressive enhancement on top. Unit-tested + jsdom axe clean; live browser axe/RTL pass recommended. From Partial (P6c, 2026-06-27). |
| AC-045 | Accessibility | Met | axe (WCAG 2.2 AA) render | Global :focus-visible (2px solid --focus, offset) — axe-clean EN/AR×light/dark (P3) |
| AC-046 | Accessibility | Met | axe (WCAG 2.2 AA) render | Labels/aria/contrast/reading order — axe 0 violations across EN/AR×light/dark; landmarks verified (P3) |
| AC-047 | Unsaved-work | Met | SubmitTopic.test (guard dialog on dirty nav) | useBlocker (data router) → confirm Dialog on in-app route change while the submit form is dirty; Keep editing / Leave |
| AC-048 | Unsaved-work | Partial | SubmitTopic.tsx (beforeunload wired) | beforeunload listener added when dirty (reload/close/hard-nav); the native browser dialog isn't unit-testable in jsdom → live pass |
| AC-049 | File upload | Partial | TopicAttachmentTests (validator) + SubmitTopic.test (size reject) | Server size/MIME rejection (400); submit form adds a 50 MB client-side pre-check with a localized message; server-side localized message → BL-016 |
| AC-050 | File upload | Met | TopicAttachmentTests (handler) + live (POST /{id}/attachments → 201 on real MinIO) | Submit UI stages a file and POSTs multipart to the new topic; live pass confirmed 201 against real MinIO (handler does IFileStore store + SQL metadata + DocumentAttached audit) |
| AC-051 | Notifications | Met | MeetingHandlerTests (AgendaPublished fan-out: date+title+deep link, EN+AR) + NotificationHandlerTests + NotificationCenter.test (live list + deep-link nav) + TopBar.test (badge) | End to end: PublishAgenda fans out one in-app notification per active member (synchronous ≤5s write) carrying the meeting date + agenda title + a `/meetings/{key}` deep link; the notification center renders it (unread badge + list) and clicking follows the deep link. Live cross-session browser pass recommended (standing caveat). From Partial (P6e, 2026-06-27). |
| AC-052 | Notifications | Partial | NotificationCenter.test (deep-link click → navigate) | The notification deep-link **navigation** mechanism is built + tested (clicking a notification with a deepLink routes to its target — no extra steps). The **vote-open** notification itself is raised in P9 (Voting). From Pending (P6e, 2026-06-27). |
| AC-053 | Notifications | Met | NotificationHandlerTests + DI (single INotificationChannel = InAppNotificationChannel) + NotificationCenter.test | Exactly one channel is registered and rendered (in-app); no email/Webex is attempted and the absence raises no error. Structurally guaranteed + unit-proven on both server (fan-out) and client (center). From Partial (P6e, 2026-06-27). |
| AC-054 | Background jobs | Pending | — | Due-date reminder |
| AC-055 | Background jobs | Pending | — | Overdue escalation |
| AC-056 | Background jobs | Pending | — | Hangfire dashboard for Admin |
| AC-057 | Aging | Partial | TopicApplicationTests + TopicHandlerTests (live SlaBreached) + Backlog.test (badge rendered) | Aging badge computed + rendered in the backlog UI (slaBreached-driven, unit-tested); live browser pass + SLA-breach notification → Notifications phase |
| AC-058 | Membership | Met | CommitteeMemberTests + MembershipFeatureTests | Deactivate → Disabled; name/email/role/attribution intact |
| AC-059 | Membership | Met | MembershipApiTests (all roles) + UsersMembership.test | Directory readable by every authenticated role; admin screen built |
| AC-060 | Search & Trace | Pending | — | Global search grouped results |
| AC-061 | Search & Trace | Pending | — | Arabic search via word-breaker |
| AC-062 | Search & Trace | Pending | — | Traceability panel up/downstream |
| AC-063 | Search & Trace | Pending | — | Typed edge creation audited |
| AC-064 | Dashboards | Pending | — | Committee dashboard live data |
| AC-065 | Dashboards | Pending | — | Secretary dashboard |
| AC-066 | Dashboards | Pending | — | Chairman dashboard |

**Summary:** 66 ACs · 13 Met (AC-001/002/008/031/039/040/042/045/046/047/050/058/059) · 21 Partial
(AC-003/005/006/007/009/010/011/012/013/015/016/030/032/033/034/035/043/048/049/057 + AC-041) · 32 Pending.
(Through P5b PR4 + the 2026-06-27 P5-review remediation, which corrected AC-043 Met→Partial — the kanban
keyboard move covers status, not the priority-ordinal reorder the AC specifies.)

> P4 grading rule (G-TRACE): an auth AC is **Met** only when fully demonstrable against aggregates/stores
> that exist in P4 (claim→role, 401, Membership directory + deactivation). ACs whose *mechanism* is built and
> unit-tested but whose end-to-end demonstration needs a not-yet-built aggregate (Topics P5, Actions P8,
> Votes P9, MoM P7), endpoint, or the immutable audit store (BL-066) are **Partial**, with the consuming
> phase named. This avoids over-claiming: the policy/handler/predicate is proven now; the live HTTP path
> lands with its module.
