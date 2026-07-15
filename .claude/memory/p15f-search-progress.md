---
name: p15f-search-progress
description: "P15f global-search backend — OQ-034 resolved (SQL FTS + LIKE booster), Decisions vertical BUILT + AC-061 FTS mechanism PROVEN green; 4 providers + API tests remain."
metadata: 
  node_type: memory
  type: project
  originSessionId: 4e2ef3ab-3879-48f1-a081-d35f212ba76c
---

# P15f — Global Search backend (branch `feat/p15f-search-backend`, NOT committed)

**OQ-034 RESOLVED (operator ruling 2026-07-14):** engine = **SQL Server FTS + LIKE-substring booster**, single datastore, INV-002 NOT triggered, no OpenSearch, no ADR. Spike: derived FTS image (`mssql-server-fts` on the 2022 Ubuntu-22.04 base) → `IsFullTextInstalled=1`, `sys.fulltext_languages` has lcid 1025 (Arabic); FREETEXT micro-recall **82.9%** (≥80% gate), misses Arabic *derivation* (عمارة↔معماري, بحث) which the LIKE booster recovers. Spike artifacts in scratchpad `oq034-spike-result.md`. See [[p15-research-knowledge-plan]].

**Scope:** FR-143/144/145 + FR-118 (wiki in index), AC-060 (grouped ≤3s) + AC-061 (Arabic). FR-146/147/148 (traceability/impact) already shipped in P10 — untouched.

**Architecture (per IDecisionReader/P11e precedent):** `ISearchProvider` seam in `Acmp.Shared/Contracts/Search/` (+ `SearchHit` record, `SearchExcerpt` helper). 5 per-module impls query ONLY their own FTS-indexed tables: Topics · Decisions · **ADRs→Governance** · **MoMs→Meetings** · **Documents→Knowledge**. Inline `SearchEndpoints` (Acmp.Api) injects `IEnumerable<ISearchProvider>`, **sequential** fan-out (all modules share ONE per-scope DbConnection, ADR-0026 → can't multiplex), groups by ArtifactType, `RequireAuthorization()` (any role, US-078). NO ISearchIndex/OpenSearch abstraction (ISearchProvider IS the swap point).

**Provider query pattern (DecisionSearchProvider is the template):** inject the CONCRETE DbContext (need `.Database.IsSqlServer()`); `if IsSqlServer → EF.Functions.FreeText(col_ar, q, 1025) || FreeText(col_en, q, 1033) || col.Contains(q) …  else → col.Contains(q)` only (InMemory can't translate FreeText). `EF.Functions.FreeText` DOES translate over owned-type props (`d.Title.Ar` → `title_ar`). Parameterized (no injection). Order by IssuedAt desc, Take(perType). Register `AddScoped<ISearchProvider, XSearchProvider>()` in the module's InfrastructureExtensions.

**FTS migration pattern (per module, `dotnet ef migrations add X_AddSearchIndex` → empty diff → hand-add):** `migrationBuilder.Sql(..., suppressTransaction: true)` (FTS DDL can't run in a txn); guard EVERY stmt with `IF SERVERPROPERTY('IsFullTextInstalled')=1` (NO-OP on the stock `SqlBackstopFixture` image → keeps that suite green) + `EXEC('CREATE FULLTEXT …')` (batch-restriction sidestep) + `NOT EXISTS` idempotency. **Per-module catalog** `ft_<module>` (Down drops index+catalog, no shared-catalog coupling). Columns: PascalCase scalars, `*_en`/`*_ar` owned. `KEY INDEX PK_<table>`, per-column `LANGUAGE 1033`(en)/`1025`(ar), `CHANGE_TRACKING = AUTO`.

**deploy:** `deploy/Dockerfile.sqlserver` (base + mssql-server-fts) + compose `sqlserver` service now `build:`s it as `acmp/sqlserver-fts:2022`. ⚠ switching a RUNNING stack forces SQL container rebuild → `docker compose down -v` (fresh volume) or unhealthy (dev-stack landmine). Running dev stack still on the OLD stock image — untouched.

**★ PROVEN:** `tests/Acmp.Integration.Tests/DecisionSearchFtsTests.cs` (Docker-gated, mirrors MinioFileStoreTests) builds the FTS image via `ImageFromDockerfileBuilder`, boots `MsSqlBuilder().WithImage(...)`, **CREATE DATABASE Acmp first** (FTS can't live in master — MsSqlBuilder defaults to master!), migrates Decisions (FTS index fires), raw-seeds Arabic rows (**raw INSERT must supply ChairOverride + CreatedAt** — NOT NULL, no default, EF audit-stamping bypassed), polls `FULLTEXTCATALOGPROPERTY('ft_decisions','PopulateStatus')=0` (async populate), asserts Arabic FREETEXT match + English + no encoding loss. **PASSES.** Whole mechanism de-risked.

**★ ALL 5 PROVIDERS BUILT + PROVEN (2026-07-15).** Topics (monolingual Title/Description, single-col FTS 1025) · Decisions · ADR(Governance) · MoM(Meetings, no title→Summary excerpt) · Document(Knowledge). 5 guarded FTS migrations (`ft_topics/ft_decisions/ft_adrs/ft_minutes/ft_documents`), 5 registrations (fully-qualified before `return services;`), coordinator + Program wiring. `tests/Acmp.Integration.Tests/SearchProvidersFtsTests.cs` (renamed from DecisionSearchFtsTests) migrates ALL 5 contexts + asserts AC-061 Decisions Arabic match + all-5 FreeText execute — **2 tests PASS (15s)**. Added Governance+Knowledge ProjectRefs to Integration.Tests.csproj. Whole sln builds clean.

**★ P15f + P15g COMPLETE + ALL GATES GREEN (2026-07-15, awaiting commit GO on `feat/p15f-search-backend`).**
- BE: build; Domain 241/Arch 49/Application 860/Integration 36/Api 229 (0 fail, 1415 total); coverage **99.66%** (no file <95%); `dotnet format` clean (CHARSET/BOM fixed — new test files written AFTER the first format pass needed a 2nd `dotnet format`). ArchUnit 49→49 (seam respects boundaries). SqlBackstopFixture still green (guard NO-OPs on stock image).
- Coverage close-out (the tricky part): providers' SqlServer branch + projections covered by seeding ALL 5 tables in `SearchProvidersFtsTests` (raw INSERT — each table's NOT-NULL-no-default cols: Topics needs streams/systems/tags='[]'+Priority; ADR AuthorUserId/Name; MoM Version/MeetingId/MeetingKey/MeetingTitle/ApprovedBySoleAuthor; Doc Category/OwnerUserId/Version/tags); else-branch via `SearchApiTests` fan-out + `SearchProviderGuardTests`; excerpt via `SearchExcerptTests`. ⚠ WaitForPopulation must poll `int?` (FULLTEXTCATALOGPROPERTY returns NULL under parallel load → bare int cast throws "Nullable object must have a value"); `dotnet test acmp.sln` parallelism also gave ONE transient Api flaky that did NOT recur solo.
- P15g FE DONE: `api/search.ts` (`useSearch`; ⚠ `api()` already prefixes `/api` → call `/search` NOT `/api/search`) + `features/search/SearchPage.tsx` (grouped, states, deep links, no-ref INV-014) + i18n `search.*` EN+AR (1750). FE 134 files/1023 tests, build, i18n parity green. Route swapped in App.tsx (was PlaceholderPage).
- Registers updated: OQ-034→Resolved, AC-060/061→Met (Pending now only AC-004), progress-log + status-report(v1.7.3) + ph0-validation §7. **AC-060/061 → Met.**
- **REMAINING:** operator commit GO → squash-merge; live pixel-VR smoke on isolated `-p acmpe2e` (owed, no-ref so low-risk); then **P15h** template pre-fill (reads `targetType` seam). Reconciliations logged: cross-type status = raw enum via neutral chip (localization owed); Topics single mixed-lang col indexed 1025.

**Bash gotcha (this env):** Git Bash mangles container abs-paths in `docker exec /opt/...` → set `MSYS_NO_PATHCONV=1`. Spike image `acmp-fts-spike:local` + test image `acmp/sqlserver-fts:test` kept (cache).
