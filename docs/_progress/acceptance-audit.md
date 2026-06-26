---
artifact: acceptance-audit
status: active
version: v1
updated: 2026-06-25
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
| AC-009 | ABAC | Partial | AbacHandlerTests + PermissionMatrixTests | Owner-widens-AiO proven on stub resource; live Topic edit 403 → P5 |
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
| AC-030 | Topic lifecycle | Pending | — | Required-field validation, localized |
| AC-031 | Topic lifecycle | Pending | — | Reject needs rationale |
| AC-032 | Topic lifecycle | Pending | — | Reject → immutable event + notify |
| AC-033 | Topic lifecycle | Pending | — | Rejection event immutable |
| AC-034 | Topic lifecycle | Pending | — | Post-accept edit locked to Secretary |
| AC-035 | Topic lifecycle | Pending | — | Prepared transition + audit |
| AC-036 | MoM | Pending | — | Published MoM → versioned supersede |
| AC-037 | MoM | Pending | — | Change-request → back to Draft |
| AC-038 | MoM | Pending | — | Approve → Published + notify |
| AC-039 | Localization | Pending | — | Locale switch preserves form data |
| AC-040 | Localization | Met | i18n/direction.test.ts + axe render | dir=rtl mirrored layout — sidebar→inline-end, Arabic font, logical CSS; verified live (P3) |
| AC-041 | Localization | Partial | manual render (Playwright) | RTL render confirmed clean by hand; automated visual-regression suite → P17 |
| AC-042 | Localization | Met | theme/theme.test.ts | Theme persisted via localStorage + applied as data-theme |
| AC-043 | Accessibility | Pending | — | Keyboard DnD alt (backlog) |
| AC-044 | Accessibility | Pending | — | Keyboard DnD alt (agenda) |
| AC-045 | Accessibility | Met | axe (WCAG 2.2 AA) render | Global :focus-visible (2px solid --focus, offset) — axe-clean EN/AR×light/dark (P3) |
| AC-046 | Accessibility | Met | axe (WCAG 2.2 AA) render | Labels/aria/contrast/reading order — axe 0 violations across EN/AR×light/dark; landmarks verified (P3) |
| AC-047 | Unsaved-work | Pending | — | Route-change guard |
| AC-048 | Unsaved-work | Pending | — | beforeunload dialog |
| AC-049 | File upload | Pending | — | Size/MIME rejection, localized |
| AC-050 | File upload | Pending | — | Valid upload → MinIO + audit |
| AC-051 | Notifications | Pending | — | Agenda publish → in-app ≤5s |
| AC-052 | Notifications | Pending | — | Vote-open notification deep link |
| AC-053 | Notifications | Pending | — | In-app only, no email/Webex |
| AC-054 | Background jobs | Pending | — | Due-date reminder |
| AC-055 | Background jobs | Pending | — | Overdue escalation |
| AC-056 | Background jobs | Pending | — | Hangfire dashboard for Admin |
| AC-057 | Aging | Pending | — | SLA aging badge + notify |
| AC-058 | Membership | Met | CommitteeMemberTests + MembershipFeatureTests | Deactivate → Disabled; name/email/role/attribution intact |
| AC-059 | Membership | Met | MembershipApiTests (all roles) + UsersMembership.test | Directory readable by every authenticated role; admin screen built |
| AC-060 | Search & Trace | Pending | — | Global search grouped results |
| AC-061 | Search & Trace | Pending | — | Arabic search via word-breaker |
| AC-062 | Search & Trace | Pending | — | Traceability panel up/downstream |
| AC-063 | Search & Trace | Pending | — | Typed edge creation audited |
| AC-064 | Dashboards | Pending | — | Committee dashboard live data |
| AC-065 | Dashboards | Pending | — | Secretary dashboard |
| AC-066 | Dashboards | Pending | — | Chairman dashboard |

**Summary:** 66 ACs · 9 Met (AC-001/002/008/040/042/045/046/058/059) · 12 Partial
(AC-003/005/006/007/009/010/011/012/013/015/016 + AC-041) · 45 Pending. (Through P4 + CHANGE-001 live bring-up + SSO login.)

> P4 grading rule (G-TRACE): an auth AC is **Met** only when fully demonstrable against aggregates/stores
> that exist in P4 (claim→role, 401, Membership directory + deactivation). ACs whose *mechanism* is built and
> unit-tested but whose end-to-end demonstration needs a not-yet-built aggregate (Topics P5, Actions P8,
> Votes P9, MoM P7), endpoint, or the immutable audit store (BL-066) are **Partial**, with the consuming
> phase named. This avoids over-claiming: the policy/handler/predicate is proven now; the live HTTP path
> lands with its module.
