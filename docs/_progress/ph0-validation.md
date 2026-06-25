---
artifact: ph0-validation
status: active
version: v1
updated: 2026-06-25
---

# PH-0 — Validation

Confirms domain/architecture understanding, applies the open-decision defaults that gate PH-0/PH-1,
verifies the local toolchain, and records the Arabic FTS spike. Precedes any scaffolding.

---

## 1. Domain & core loop (confirmed)

ACMP is the single, auditable, bilingual (EN/AR) system of record for **one** Architecture Committee —
on-prem, ≤20 users, low traffic. It is **architecture governance, not project management**.

**Core governance loop (PH-1 must work end-to-end):**
`Topic intake → triage → backlog → agenda → meeting → minutes → vote → decision → ADR → action`,
with in-app notifications, immutable audit history, basic dashboards, EN+AR (RTL), light/dark.

**Non-negotiables wired from day one:** policy (role) + ABAC authorization on every operation;
an `AuditEvent` on every state change; votes/decisions/ADRs/published-minutes immutable (superseded,
never edited) with SHA-256 hash-chaining; always-attributed voting; no hardcoded strings; self-contained
deployment (CON-001).

## 2. Modules (16) & roles

**Core:** Membership · Topics · Meetings · Decisions(+Vote) · Actions · Risks · Dependencies ·
Governance(ADRs+Invariants) · Research · Knowledge · Diagrams.
**Cross-cutting:** Notifications · Reporting · Search&Traceability(`trace`) · Audit&Records · Platform(`Acmp.Shared`).
Rule: schema-per-module; no module reads another's tables; comms via MediatR contracts / domain events only.

**Global roles (from Keycloak claims):** Chairman, Secretary, Member, Reviewer, Auditor, Administrator,
Submitter, Guest/Presenter. **Per-topic capabilities (ABAC):** Owner, Assignee/Contributor, Presenter.

## 3. Open decisions gating PH-0/PH-1 — defaults applied

Org answers received 2026-06-25 are folded in. Defaults are applied to stay unblocked; flagged items
still need human confirmation where noted.

| OQ | Topic | Applied for PH-0/P1 | Confirm by |
|---|---|---|---|
| OQ-030 | CI system | **GitHub Actions, GitHub-hosted runners** (repo `github.com/A-H-911/acmp`) | self-hosted-for-prod → OQ-038 |
| OQ-031 | Registry | **Public registry + image-digest pinning** (build machine has direct internet) | prod air-gap → P16/P18 |
| OQ-032 | NuGet/npm mirror | **Direct access** (not air-gapped) | prod air-gap → P16/P18 |
| OQ-024 | TLS | **TLS 1.2+**, no internal mTLS v1 | security review → P16 |
| OQ-003 | MFA / session | **MFA Chairman+Secretary; 60-min idle** (Keycloak realm config, no code) | Keycloak setup |
| OQ-020 | Standby VM | **Cold standby + documented restore** | revisit → P18 |
| OQ-034 | Arabic FTS | **SQL Server FTS** in v1; spike below decides P2 escalation | spike (this doc §7) |
| OQ-002 | Reviewer-may-vote | **No** | Secretary/Chairman |
| OQ-004 | TopicRequest | **Merged into Topic** | — |
| OQ-012 | SPA serving | **Resolved → (a) separate nginx `web` container** (proxies `/api`→api), per user instruction 2026-06-25 + docs/34 §8; overrides recommended default (b) | resolved |
| OQ-013 | OIDC client | **oidc-client-ts + react-oidc-context** | — |
| OQ-014 | Clustered key | **BIGINT IDENTITY** + GUID alt-key; `TOP-YYYY-###` separate display ID | — |
| OQ-015 | Ballot storage | **Normalized tables** (VoteSession/VoteOption/VoteCast) | — |
| OQ-016 | Notifications | **30s short polling** | — |
| OQ-017 | Relationship types | **Fixed 8-type enum** (+`Other`) | — |
| OQ-021 | Breakpoints | **Tablet + desktop only** | — |
| OQ-022 | Charts | **Recharts + RTL validation spike** in dashboard build | — |
| OQ-027 | SAST/DAST | **Semgrep OSS (block high) + ZAP baseline (alert); `dotnet list package --vulnerable` + `npm audit`** | — |
| OQ-028 | CSP | **`script-src 'self'`** (Vite bundles, no inline) | — |
| OQ-006/010/008 | Enum lists (Source / Meeting type / cadence) | proposed defaults; **Secretary validates before P5/P6** | Secretary |

**New open question raised at PH-0 (to be added to `docs/42` after confirmation):**
- **OQ-038 — Prod CI runner:** PH-1 skeleton uses GitHub-hosted runners. Production builds for an
  on-prem (possibly air-gapped) VM likely need a **self-hosted GitHub Actions runner** + package/registry
  mirrors. Owner: DevSecOps/Org IT. Needed-by: P18. Default: provision a self-hosted runner before prod cutover.

## 4. Finding — search-engine discrepancy (recorded, follow ADR)

`docs/42 OQ-034` prose names **Meilisearch** as the FTS escalation target, but the **canonical** decision
`ADR-0011` / `README §A` / `docs/42 R-24` names **app-owned OpenSearch**. Canon wins:
**v1 = SQL Server FTS; escalation fallback = self-hosted OpenSearch behind a search abstraction** (`ISearchIndex`).
No engine is switched silently. Recommend a one-line correction to OQ-034 to match ADR-0011 (needs
Tech-Lead sign-off before editing canon — not edited unilaterally here).

## 5. Toolchain verification

| Tool | Required | Found | Status |
|---|---|---|---|
| .NET SDK | 8.0.x LTS | 8.0.422 (also 9.0.315, 10.0.301) | ✅ pinned via `global.json` → resolves to 8.0.4xx |
| .NET runtime | 8.0 | ASP.NET 8.0.28 + NETCore 8.0.28 | ✅ |
| Node.js | LTS | v26.3.1 | ✅ |
| Docker CLI | Compose | 29.5.3 | ✅ |
| Docker daemon | running | Desktop Linux engine was **down** at start | ⚠️ started Docker Desktop; see §7 |
| Git | — | 2.54.0 | ✅ |
| Keycloak / SQL Server / Seq / MinIO | reachable or stubbed | not provisioned | ⏳ via `deploy/docker-compose.yml`; tests stub Keycloak with `TestAuthHandler` |

## 6. Services & Compose plan

Compose stack (P1): `api` (ASP.NET Core 8 REST API), `web` (nginx serving the Vite-built SPA, proxies
`/api`→`api` — OQ-012 resolved to (a)), `sqlserver` (own instance), `seq` (self-hosted logs/traces),
`minio` (S3 storage). Tarseem sidecar = P2;
Keycloak federated (not bundled) — for PH-1 dev, point at a dev Keycloak or use the test auth handler.
No org runtime infra (CON-001). Secrets via git-ignored `.env` + `deploy/secrets/` (never committed).

## 7. Arabic FTS spike (OQ-034)

**Goal:** does SQL Server's built-in Arabic word-breaker give ≥80% recall on representative committee
queries? If yes → keep SQL FTS for v1. If no → escalate to OpenSearch (per ADR-0011, §4) in P2.

**Method:** SQL Server 2022 container; DB with a `topics` test table (Arabic title/description);
full-text catalog + index with the Arabic LCID (1025); seed ~20 bilingual governance rows; run a set of
representative `CONTAINS`/`FREETEXT` queries (technical terms, transliterations, inflected forms);
recall = matched-expected / total-expected.

**Result (2026-06-25):** **Inconclusive on recall — but a concrete deployment finding surfaced.**
The stock `mcr.microsoft.com/mssql/server:2022-latest` image returns `SERVERPROPERTY('IsFullTextInstalled') = 0`:
Full-Text Search is **not bundled**, and the `mssql-server-fts` package is **not in the image's default apt
sources**. So the Arabic word-breaker recall test could not be run on the stock image.

**Interpretation (per advisor guidance):** "couldn't test → defer," **not** "SQL FTS inadequate."
The v1 search decision is **unchanged**: SQL Server FTS (ADR-0011 / R-24); escalation fallback = app-owned
OpenSearch behind a search abstraction. The Arabic recall measurement moves to the **P5/Topics+search build**,
when the FTS-enabled image exists.

**Action item (before P5 search work):** v1 SQL FTS requires a **derived SQL Server image** that installs
`mssql-server-fts` from the Microsoft `mssql-server` apt repo (or FTS enabled on the on-prem SQL instance).
The P1 `docker-compose` uses the stock image (no FTS needed yet for the foundation); a `Dockerfile.sqlserver`
(FROM the base + FTS) is added when search lands. Logged to progress-log; carry into P5 backlog.

## 8. P1 scaffold scope (what's in / deferred)

**In (P1):** `global.json`, `acmp.sln`, `Acmp.Api` host, `Acmp.Shared` kernel, **Membership** reference
module end-to-end (1 command + 1 query + validator + DbContext + migration), MediatR behaviors
(validation/authorization/audit/logging), `IClock`/`ICurrentUser`/`IFileStore`/`INotificationChannel`,
health checks, Serilog→Seq + OpenTelemetry, React shell (routing + i18n EN/AR + RTL + light/dark),
`deploy/` (Dockerfiles + compose api/web/sqlserver/seq/minio), ArchUnit boundary tests, CI skeleton.

**Deferred (later phases, per plan):** full OIDC wiring (P3), full Membership/ABAC (P4), all other
modules (P5+), Hangfire jobs UX, Tarseem/Webex/Keystone, security hardening, full test matrix.
No business features in P1 (per phase-prompt P1).

## 9. PH-0 exit checklist

- [x] Domain/modules/roles/core-loop confirmed against canon.
- [x] PH-0/P1 open-decision defaults applied and recorded; org answers folded in.
- [x] Toolchain verified (.NET 8 / Node / Docker CLI / Git).
- [ ] Docker daemon healthy.
- [x] Arabic FTS spike run and recorded (§7) — stock image lacks FTS; recall test deferred to P5; v1 = SQL FTS stands.
- [ ] Repo scaffolded per `docs/34`; `docker compose up` healthy. → **report before P2**.
