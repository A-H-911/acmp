# 34 — Repository Structure (Deliverable 42)

**Purpose:** Define the monorepo layout for ACMP — authoritative for the Claude Code execution agent when scaffolding the project and for humans navigating the codebase. Every directory and convention described here is what the agent will create.

> Architecture constraints: modular monolith; Clean Architecture per module + vertical slices; 16 canonical modules (README §B); .NET 8 + ASP.NET Core + MediatR + EF Core; React 18 + TS + Vite; Docker Compose; no Kubernetes. Module isolation enforced at compile time (ArchUnit.NET).

---

## 1. Top-Level Directory Tree

```
acmp/                                   ← repository root
│
├── CLAUDE.md                           ← Claude Code agent context (guardrails, project map)
├── README.md                           ← Human-facing project overview + quick-start
├── .editorconfig                       ← Unified code style (tabs/spaces, charset, newline)
├── .gitignore                          ← Excludes: secrets/, *.env, build artifacts, dist/
├── .gitattributes                      ← LF line endings enforced
├── .pre-commit-config.yaml             ← Gitleaks + Prettier pre-commit hooks
├── global.json                         ← Pins .NET SDK version (e.g., "8.0.xxx")
├── acmp.sln                            ← Solution file (all C# projects)
│
├── src/                                ← All application source (§2)
│   ├── Acmp.Api/                       ← ASP.NET Core host (entry point)
│   ├── BuildingBlocks/                 ← Shared Kernel (§3)
│   └── Modules/                        ← 16 bounded-context modules (§4)
│
├── tests/                              ← All test projects (§5)
│
├── docs/                               ← Planning package + arc42 + runbooks (§6)
│
├── adr/                                ← MADR Architecture Decision Records (§7)
│
├── deploy/                             ← Dockerfiles, Compose, env samples, secrets placeholder (§8)
│
├── scripts/                            ← Dev/ops automation scripts (§9)
│
└── .github/                            ← GitHub Actions workflows + PR templates (§10)
    └── workflows/
```

---

## 2. `src/Acmp.Api/` — Host Project

```
src/Acmp.Api/
├── Acmp.Api.csproj                     ← References all Module.Infrastructure projects; no domain refs
├── Program.cs                          ← Startup: DI registration, middleware, migration runner, app.Run()
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Staging.json
├── appsettings.Production.json
├── Endpoints/                          ← Minimal API endpoint registrations (thin; delegate to MediatR)
│   ├── TopicsEndpoints.cs
│   ├── MeetingsEndpoints.cs
│   └── … (one file per module)
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   └── RequestLoggingMiddleware.cs
├── OpenApi/
│   └── SwaggerConfig.cs                ← Scalar / Swashbuckle setup; EN+AR schema descriptions
└── HealthChecks/
    └── HealthCheckConfig.cs            ← /healthz + /readyz registration
```

**Rule:** `Acmp.Api` may reference `*.Infrastructure` projects (for DI registration) and `BuildingBlocks.Shared` — it **must not** reference `*.Domain` or `*.Application` directly (only via their public interfaces registered in DI).

---

## 3. `src/BuildingBlocks/` — Shared Kernel

```
src/BuildingBlocks/
└── Acmp.Shared/
    ├── Acmp.Shared.csproj
    ├── Domain/
    │   ├── Entities/
    │   │   ├── BaseEntity.cs           ← Id (Guid), CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
    │   │   └── AuditableEntity.cs
    │   ├── ValueObjects/
    │   │   ├── LocalizedString.cs      ← { En: string, Ar: string }; both required
    │   │   └── HumanReadableId.cs      ← e.g. TOP-2026-001 generation
    │   ├── Events/
    │   │   └── IDomainEvent.cs         ← Marker interface; all domain events implement this
    │   └── Enums/
    │       └── Language.cs             ← EN, AR
    ├── Application/
    │   ├── Interfaces/
    │   │   ├── IClock.cs
    │   │   ├── ICurrentUser.cs         ← UserId, Roles, Claims
    │   │   ├── IFileStore.cs           ← Upload, GetPreSignedUrl, Delete, Exists
    │   │   └── INotificationChannel.cs ← Publish(NotificationMessage)
    │   ├── Behaviors/
    │   │   ├── ValidationBehavior.cs   ← FluentValidation pipeline behavior
    │   │   ├── LoggingBehavior.cs
    │   │   └── AuthorizationBehavior.cs
    │   └── Pagination/
    │       └── PagedResult.cs
    ├── Infrastructure/
    │   ├── Persistence/
    │   │   ├── BaseDbContext.cs        ← Shared EF configuration (audit fields, soft-delete)
    │   │   └── OutboxMessage.cs        ← Outbox table entity
    │   ├── FileStorage/
    │   │   └── MinioFileStore.cs       ← IFileStore → MinIO S3 implementation
    │   ├── Jobs/
    │   │   └── HangfireConfig.cs       ← Hangfire SQL Server + recurring job registration
    │   ├── Identity/
    │   │   └── CurrentUserService.cs   ← ICurrentUser → HttpContext.User claims
    │   └── Time/
    │       └── SystemClock.cs          ← IClock → DateTimeOffset.UtcNow
    └── Contracts/
        └── Notifications/
            └── NotificationMessage.cs  ← Shared DTO used by all modules to publish
```

**Dependency rule:** All modules depend **downward** on `Acmp.Shared`; `Acmp.Shared` depends on **no module**.

---

## 4. `src/Modules/` — 16 Bounded-Context Modules

Each module follows the same internal Clean-Architecture-per-module layout. Example: `Decisions` module.

### 4.1 Per-module folder convention

```
src/Modules/<Module>/
├── <Module>.Domain/
│   └── <Module>.Domain.csproj          ← depends only on Acmp.Shared.Domain
│   ├── Aggregates/
│   │   └── <AggregateRoot>.cs
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/                         ← Domain events (IDecisionIssued, IVoteClosed, …)
│   ├── Enums/
│   └── Exceptions/
│
├── <Module>.Application/
│   └── <Module>.Application.csproj     ← depends on <Module>.Domain + Acmp.Shared.Application
│   ├── Features/                       ← Vertical slices (one folder per use case)
│   │   ├── IssueDecision/
│   │   │   ├── IssueDecisionCommand.cs
│   │   │   ├── IssueDecisionHandler.cs
│   │   │   ├── IssueDecisionValidator.cs
│   │   │   └── IssueDecisionResponse.cs
│   │   └── GetDecision/
│   │       ├── GetDecisionQuery.cs
│   │       ├── GetDecisionHandler.cs
│   │       └── DecisionDto.cs
│   ├── Contracts/                      ← Public interfaces this module exposes to peers
│   │   └── IDecisionService.cs
│   └── DependencyInjection.cs          ← Module's IServiceCollection extension
│
├── <Module>.Infrastructure/
│   └── <Module>.Infrastructure.csproj  ← depends on <Module>.Application + EF Core + adapters
│   ├── Persistence/
│   │   ├── <Module>DbContext.cs        ← Only maps this module's schema tables
│   │   ├── Configurations/             ← EF IEntityTypeConfiguration<T> per entity
│   │   └── Migrations/                 ← EF Core migration files for this module's schema
│   ├── Repositories/
│   │   └── DecisionRepository.cs
│   ├── ReadModels/                     ← Columnstore projections, Dapper-style read queries
│   └── DependencyInjection.cs
│
└── <Module>.Contracts/                 ← OPTIONAL: separate contracts project for peer-module use
    └── <Module>.Contracts.csproj       ← depends only on Acmp.Shared; no domain or infra
    └── Events/                         ← Public domain event DTOs for cross-module subscription
```

### 4.2 Canonical modules → folder names

| Module (README §B) | Folder | SQL Schema | Notes |
|---|---|---|---|
| Membership | `src/Modules/Membership/` | `membership` | Users, roles, committee membership, streams |
| Topics | `src/Modules/Topics/` | `topics` | Topic aggregate + backlog |
| Meetings | `src/Modules/Meetings/` | `meetings` | Meeting, Agenda, MoM, Attendance |
| Decisions | `src/Modules/Decisions/` | `decisions` | Decision + Vote aggregates |
| Actions | `src/Modules/Actions/` | `actions` | Action items + progress |
| Risks | `src/Modules/Risks/` | `risks` | Risk + Mitigation |
| Dependencies | `src/Modules/Dependencies/` | `dependencies` | Dependency graph edges |
| Governance | `src/Modules/Governance/` | `governance` | ADR + Architecture Invariant |
| Research | `src/Modules/Research/` | `research` | ResearchMission, Finding, Recommendation |
| Knowledge | `src/Modules/Knowledge/` | `knowledge` | Document, Template, Wiki pages |
| Diagrams | `src/Modules/Diagrams/` | `diagrams` | Diagram spec + artifact refs |
| Notifications | `src/Modules/Notifications/` | `notifications` | Notification + outbox rows |
| Reporting | `src/Modules/Reporting/` | `reporting` | Read models, columnstore views |
| Search & Traceability | `src/Modules/Traceability/` | `trace` | Relationship typed edges |
| Audit & Records | `src/Modules/Audit/` | `audit` | AuditEvent append-only log |
| Platform (Shared Kernel) | `src/BuildingBlocks/Acmp.Shared/` | `platform` | (see §3) |

### 4.3 Clean Architecture dependency rule (per module)

```
Domain  ←  Application  ←  Infrastructure  ←  (Api.Host for DI wiring)
  ↓              ↓
Shared       Shared
```

- `Domain`: no outward dependencies except `Acmp.Shared.Domain`.
- `Application`: depends on `Domain` + `Acmp.Shared.Application`. No EF Core, no HTTP.
- `Infrastructure`: depends on `Application` + EF Core + SDK adapters.
- Cross-module: `ModuleA.Application` may depend on `ModuleB.Contracts` (never `ModuleB.Domain` or `ModuleB.Infrastructure`).

---

## 5. `tests/` — Test Projects

```
tests/
├── Acmp.Domain.Tests/                  ← Unit: domain aggregates, value objects, FSM
│   └── Acmp.Domain.Tests.csproj        ← references: xUnit, FluentAssertions, Bogus
│   └── Modules/
│       ├── Decisions/
│       │   ├── VoteTests.cs
│       │   ├── DecisionImmutabilityTests.cs
│       │   └── VotingIntegrityTests.cs
│       ├── Topics/
│       │   └── TopicStateMachineTests.cs
│       └── … (per module)
│
├── Acmp.Application.Tests/             ← Handler tests (NSubstitute mocks)
│   └── Acmp.Application.Tests.csproj
│   └── Modules/
│       └── … (per module, mirrors Domain.Tests structure)
│
├── Acmp.Api.IntegrationTests/          ← API integration (WebApplicationFactory + Testcontainers)
│   └── Acmp.Api.IntegrationTests.csproj
│   ├── Infrastructure/
│   │   ├── CustomWebApplicationFactory.cs
│   │   ├── TestAuthHandler.cs          ← Fake Keycloak JWT injector
│   │   ├── SqlServerFixture.cs         ← Testcontainers SQL Server lifecycle
│   │   └── MinioFixture.cs             ← Testcontainers MinIO lifecycle
│   └── Modules/
│       ├── Decisions/
│       │   ├── VotingIntegrationTests.cs
│       │   ├── DecisionImmutabilityTests.cs
│       │   └── PermissionMatrixTests.cs
│       └── … (per module)
│
├── Acmp.Migrations.Tests/              ← EF migration idempotency + schema correctness
│   └── Acmp.Migrations.Tests.csproj
│
├── Acmp.Architecture.Tests/            ← ArchUnit.NET: enforce no cross-module internal refs
│   └── Acmp.Architecture.Tests.csproj
│   └── ModuleBoundaryTests.cs          ← All rules in §5.1
│
└── TestBuilders/                       ← Shared test builders (fluent; used by all test projects)
    └── Acmp.TestBuilders.csproj
    └── Builders/
        ├── TopicBuilder.cs
        ├── VoteBuilder.cs
        ├── DecisionBuilder.cs
        └── … (per aggregate)
```

### 5.1 Architecture tests (ArchUnit.NET)

`Acmp.Architecture.Tests/ModuleBoundaryTests.cs` asserts:

```csharp
// No module's Application/Domain project references another module's Domain/Infrastructure
// Example rule (ArchUnit.NET API — [unverified: exact API syntax]):
Classes()
  .That().ResideInNamespace("Acmp.Modules.*.Domain")
  .Should().NotDependOnAssemblies(
      GetAllOtherModuleDomainAndInfraAssemblies()
  )

// Every module references only Acmp.Shared (downward)
// Cross-module refs are only to *.Contracts projects
```

These run in the CI unit-test stage; any violation → build failure.

---

## 6. `docs/` — Planning Package + Engineering Docs

```
docs/
├── 00-executive-summary.md
├── 01-organization-and-problem.md
├── … (complete planning package: docs/00 through docs/45)
├── arc42/                              ← arc42 architecture document (generated from docs/15-architecture.md)
├── runbooks/
│   ├── install-guide.md
│   ├── backup-restore.md               ← Numbered runbook (mirrors docs/33)
│   └── incident-response.md
└── user-guide/
    ├── en/
    │   └── user-guide.md
    └── ar/
        └── دليل-المستخدم.md
```

---

## 7. `adr/` — Architecture Decision Records (MADR)

```
adr/
├── README.md                           ← ADR process: how to create, number, supersede
├── template.md                         ← MADR-lite template (see docs/35-documentation-plan.md)
├── ADR-0001-modular-monolith.md
├── ADR-0002-backend-stack.md
├── ADR-0003-sql-server.md
├── ADR-0004-keycloak-oidc.md
├── ADR-0005-notification-strategy.md
├── ADR-0006-tarseem-diagram-engine.md
├── ADR-0007-keystone-optional.md
├── ADR-0008-traceability-model.md
├── ADR-0009-audit-immutability.md
├── ADR-0010-voting-model.md
├── ADR-0011-sql-search.md
└── ADR-0012-frontend-stack.md
```

**MADR file name convention:** `ADR-<NNNN>-<kebab-title>.md`
**Status values:** `Proposed | Accepted | Deprecated | Superseded by ADR-NNNN`

---

## 8. `deploy/` — Docker, Compose, Env, Secrets

```
deploy/
├── Dockerfile.api                      ← Multi-stage .NET 8 API image (see docs/33 §1.1)
├── Dockerfile.web                      ← Multi-stage React + nginx image (see docs/33 §1.2)
├── docker-compose.yml                  ← Base topology (see docs/33 §2)
├── docker-compose.override.yml         ← Dev overrides (local ports, debug logging)
├── docker-compose.staging.yml          ← Staging-specific (image tag, resource limits)
├── docker-compose.prod.yml             ← Prod-specific (tighter limits, health retries)
├── nginx/
│   ├── nginx.conf                      ← nginx config (TLS, proxy_pass, security headers)
│   └── certs/                          ← .gitignored; cert + key mounted on VM
├── keycloak/                           ← Self-hosted Keycloak realm bootstrap (ADR-0015): realm-export.json (ACMP realm, OIDC client, 8 roles+groups, bootstrap admin)
├── env/
│   ├── acmp-api.env.example            ← Documented template (committed)
│   └── acmp-api.env                    ← Actual values (.gitignored)
├── secrets/                            ← .gitignored directory; plain-text secret files on VM
│   └── .gitkeep
├── seed/
│   ├── staging-seed.sql                ← Bilingual test data (EN+AR topics, users, decisions)
│   └── seed-minio.sh                   ← Creates MinIO buckets + uploads sample attachments
└── registry/
    └── README.md                       ← How to configure local Docker registry + NuGet/npm mirror
```

---

## 9. `scripts/` — Dev and Ops Automation

```
scripts/
├── dev-up.sh                           ← `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d`
├── dev-down.sh
├── migrate.sh                          ← Run EF migrations locally (dev)
├── seed-dev.sh                         ← Seed dev DB from staging-seed.sql
├── backup.sh                           ← Nightly backup script (called by Hangfire or cron)
├── restore.sh                          ← Restore script (numbered runbook wrapper)
├── check-i18n.sh                       ← Verifies EN/AR key parity in translation JSON files
├── gen-sbom.sh                         ← Runs Syft to generate CycloneDX SBOM
└── update-openapi.sh                   ← Exports OpenAPI spec from running API → docs/api/openapi.json
```

---

## 10. `.github/` — CI/CD Workflows and PR Templates

```
.github/
├── workflows/
│   ├── ci.yml                          ← Stages 1–12: format, lint, build, test, SAST, scan, image build
│   ├── deploy-stg.yml                  ← Stage 13–14: staging deploy + E2E (on push to main)
│   ├── release.yml                     ← Stages 15–18: perf, approval, prod deploy (on tag v*.*.*)
│   ├── security.yml                    ← Weekly: ZAP + full dependency-check
│   └── backup-check.yml                ← Monthly: backup/restore validation on staging
├── PULL_REQUEST_TEMPLATE.md            ← Checklist: tests pass, US-### ref, i18n verified, no secrets
├── ISSUE_TEMPLATE/
│   ├── bug-report.md
│   └── feature-request.md
└── CODEOWNERS                          ← Lead = owner of deploy/, adr/, docs/
```

---

## 11. Root Config Files

| File | Purpose |
|---|---|
| `CLAUDE.md` | Claude Code agent: project map, guardrails, module conventions, what NOT to do, architectural rules. **Agent reads this first.** |
| `README.md` | Human: overview, quick-start, architecture summary, links to docs/ |
| `.editorconfig` | Universal code style: `indent_style = space`, `indent_size = 4` (.NET), `2` (JS/TS/JSON), `end_of_line = lf`, `charset = utf-8-bom` (C#) |
| `global.json` | Pins .NET SDK: `{ "sdk": { "version": "8.0.xxx", "rollForward": "latestPatch" } }` |
| `acmp.sln` | Visual Studio solution; includes all src/ and tests/ C# projects |
| `.gitignore` | Standard .NET + Node ignores; adds `deploy/secrets/`, `*.env`, `dist/`, `.DS_Store` |
| `.pre-commit-config.yaml` | Gitleaks + Prettier pre-commit; `npm install` in pre-commit setup |
| `.gitleaks.toml` | Gitleaks config + allowlists for test fixtures |

---

## 12. Module Communication Conventions (Summary for Agent)

1. **Cross-module call:** `ModuleA` dispatches a MediatR command/query whose handler lives in `ModuleB.Application`. The handler is registered in `ModuleB`'s `DependencyInjection.cs`. `ModuleA` references only `ModuleB.Contracts` for the command/response types.
2. **Side effects (audit, notify, link):** `ModuleA.Domain` raises a domain event (e.g., `DecisionIssuedEvent`). Platform modules (Audit, Notifications, Traceability) subscribe via `INotificationHandler<DecisionIssuedEvent>`. `ModuleA` does NOT directly call Notifications or Audit.
3. **Cross-aggregate FK:** stored as a plain `Guid` field (e.g., `TopicId` on a `Meeting`), never as an EF navigation property across a module boundary.
4. **No shared DbContext:** each module's `DbContext` maps only its own schema. `SELECT` across schemas is done in `Reporting` module read models only (read-only projections; no EF navigation).

---

## Traceability

Links: `docs/15-architecture.md` §6 (module boundaries, dependency rule) · `docs/31-testing-strategy.md` §5 (ArchUnit rules) · `docs/32-devsecops-plan.md` §1 (branching), §3 (workflow files) · `docs/33-containerization-and-deployment.md` §2 (Compose, Dockerfiles) · `docs/35-documentation-plan.md` (docs/ + adr/ conventions) · `../README.md` §B (canonical modules) · execution-handoff/agent-guardrails.md (agent reads CLAUDE.md first).
