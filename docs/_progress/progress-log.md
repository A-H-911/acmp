---
artifact: progress-log
status: active
version: v1
updated: 2026-06-25
---

# ACMP Progress Log

Per-phase, dated log of execution progress. Keystone gate **G-PROGRESS**.
Newest entries on top. Each entry: what was done, decisions applied, what's next.

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
  rolldown vs vitest nested-Vite type clash). **21 tests**: nav gating, claim→role mapping, theme persistence
  (AC-042), RTL direction (AC-040), SortableList keyboard reorder, RequireRole 403, StatusChip, SideNav role
  filtering. Added `npm test` to the CI frontend job (i18n parity already wired).

**Verification.** `dotnet`-side untouched. Frontend: i18n parity (66 keys) ✅ · `tsc -b && vite build` clean
(bundle 125 kB gzip, within the <300 kB app budget) ✅ · 21/21 tests ✅ · oxlint clean ✅ · **axe (WCAG 2.2 AA)
0 violations across EN/AR × light/dark** (rendered live; fixed one finding — `.topbar-user-role` 10.5px label
was 4.49:1, raised `--text-3`→`--text-2`) ✅ · RTL render confirmed by screenshot (sidebar mirrors to
inline-end, Arabic font + content, read-only markers).

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
- CI = GitHub Actions, GitHub-hosted runners for skeleton; "self-hosted runner for prod" → new OQ (OQ-038).
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
