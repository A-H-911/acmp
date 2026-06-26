---
artifact: acceptance-audit
status: active
version: v1
updated: 2026-06-26
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
> (token getter wired during render), already on main. Live authenticated pass (real submit + MinIO) recommended
> before merge. See progress-log P5b PR2 entry.

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
| AC-011 | ABAC | Partial | AbacHandlerTests | Capability scoped to the specific topic proven; presenter meeting-window enforcement → P6 |
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
| AC-043 | Accessibility | Pending | — | Keyboard DnD alt (backlog) |
| AC-044 | Accessibility | Pending | — | Keyboard DnD alt (agenda) |
| AC-045 | Accessibility | Met | axe (WCAG 2.2 AA) render | Global :focus-visible (2px solid --focus, offset) — axe-clean EN/AR×light/dark (P3) |
| AC-046 | Accessibility | Met | axe (WCAG 2.2 AA) render | Labels/aria/contrast/reading order — axe 0 violations across EN/AR×light/dark; landmarks verified (P3) |
| AC-047 | Unsaved-work | Met | SubmitTopic.test (guard dialog on dirty nav) | useBlocker (data router) → confirm Dialog on in-app route change while the submit form is dirty; Keep editing / Leave |
| AC-048 | Unsaved-work | Partial | SubmitTopic.tsx (beforeunload wired) | beforeunload listener added when dirty (reload/close/hard-nav); the native browser dialog isn't unit-testable in jsdom → live pass |
| AC-049 | File upload | Partial | TopicAttachmentTests (validator) + SubmitTopic.test (size reject) | Server size/MIME rejection (400); submit form adds a 50 MB client-side pre-check with a localized message; server-side localized message → BL-016 |
| AC-050 | File upload | Partial | TopicAttachmentTests (handler) + SubmitTopic.tsx (multipart upload wired) | Upload → IFileStore + SQL metadata + DocumentAttached audit (substituted store); submit UI stages files and POSTs multipart to the new topic; live MinIO → live pass |
| AC-051 | Notifications | Pending | — | Agenda publish → in-app ≤5s |
| AC-052 | Notifications | Pending | — | Vote-open notification deep link |
| AC-053 | Notifications | Pending | — | In-app only, no email/Webex |
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

**Summary:** 66 ACs · 12 Met (AC-001/002/008/031/039/040/042/045/046/047/058/059) · 21 Partial
(AC-003/005/006/007/009/010/011/012/013/015/016/030/032/033/034/035/048/049/050/057 + AC-041) · 33 Pending.
(Through P5b PR2 — submit topic form. PR2 flipped AC-039 + AC-047 to Met and AC-048 to Partial.)

> P4 grading rule (G-TRACE): an auth AC is **Met** only when fully demonstrable against aggregates/stores
> that exist in P4 (claim→role, 401, Membership directory + deactivation). ACs whose *mechanism* is built and
> unit-tested but whose end-to-end demonstration needs a not-yet-built aggregate (Topics P5, Actions P8,
> Votes P9, MoM P7), endpoint, or the immutable audit store (BL-066) are **Partial**, with the consuming
> phase named. This avoids over-claiming: the policy/handler/predicate is proven now; the live HTTP path
> lands with its module.
