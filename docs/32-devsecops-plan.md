# 32 — DevSecOps Plan (Deliverable 40)

**Purpose:** Define the CI/CD pipeline, security gates, branching model, environment promotion, and auditability of the delivery process for ACMP — embedding security at every stage, right-sized for an on-prem, single-team, ≤20-user deployment.

> Settled constraints: on-prem VM + Docker Compose (no K8s); CON-001 self-contained; .NET 8 + React 18/TS/Vite; SQL Server; Seq; MinIO; Keycloak; Hangfire. Pipeline tooling must be operable on a **self-hosted CI runner** (see §5 on-prem consideration). Treat all tooling choices marked `[unverified]` as pending org/infra confirmation.

---

## 1. Branching Model

**Trunk-based development with short-lived feature branches** (GitHub Flow / trunk-based variant). Rationale: single small team; no parallel release trains; simpler than GitFlow.

```
main  ───────●─────────────●──────────────●─── (protected; release tags here)
              \            /               \
        feature/US-042    /         feature/US-071
        (< 2 days)       /          (< 2 days)
                        ↑
                 squash merge PR
```

| Branch type | Naming | Lifetime | Merge strategy |
|---|---|---|---|
| `main` | `main` | Permanent; protected | Receives squash-merged PRs |
| Feature | `feature/<US-###>-short-desc` | < 2 days | Squash merge to main via PR |
| Hotfix | `hotfix/<issue>-short-desc` | < 1 day | Squash merge + tag |
| Release tag | `v<major>.<minor>.<patch>` | Permanent | Tag on main commit |

**PR rules (enforced via branch protection):**
- Minimum 1 approver (lead or peer).
- All required CI checks must pass before merge (see §3 stage table).
- No direct push to `main`; no force-push.
- PR title must reference an issue or US-###.
- Squash merge only (linear history).
- Stale PRs (> 3 days without update) are auto-tagged; > 7 days auto-draft.

---

## 2. Environment Promotion

```
Developer machine  →  dev (Docker Compose local)
                   →  staging (VM: docker compose up -d; mirrors prod topology)
                   →  prod (VM: docker compose up -d; release-gated)
```

| Environment | Purpose | Deploy trigger | Approval |
|---|---|---|---|
| **dev** (local) | Day-to-day development | `docker compose up -d` locally | None |
| **staging** | Integration/QA/E2E/perf | Auto-deploy on merge to `main` | None (auto) |
| **prod** | Live system | Manual release: tag + approval gate | Lead sign-off (§4) |

**Promotion rule:** code reaches prod only after staging deploy succeeds + all CI stages pass + release approval recorded.

---

## 3. CI/CD Pipeline Stages

All stages run on a **self-hosted GitHub Actions runner** (or equivalent org CI — see §5). Each PR to `main` must pass stages 1–7 before merge. Stages 8–10 run post-merge or on release.

### 3.1 Stage table

| # | Stage | Tools | Gate (block PR?) | Phase introduced |
|---|---|---|---|---|
| 1 | **Format check** | `dotnet format --verify-no-changes`; Prettier `--check`; `.editorconfig` | Yes — any diff blocks | PH-1 |
| 2 | **Lint** | `dotnet build /warnaserror` (Roslyn analyzers, nullable); ESLint (strict TS config); StyleCop [unverified: version] | Yes — any error blocks | PH-1 |
| 3 | **Build** | `dotnet build --no-restore -c Release`; `vite build --mode staging` | Yes | PH-1 |
| 4 | **Unit + Handler tests** | xUnit; Coverlet coverage; ReportGenerator | Yes — failures or coverage < 80% (domain+app layers) blocks | PH-1 |
| 5 | **Frontend tests** | Vitest + RTL; jest-axe inline a11y; i18n key parity script | Yes — failures or missing AR keys blocks | PH-1 |
| 6 | **SAST** | CodeQL (C# + JS/TS actions) [unverified: self-hosted runner access to CodeQL] | Yes — High/Critical blocks | PH-1 |
| 7 | **Dependency scan** | `dotnet list package --vulnerable` (NuGet); `npm audit --audit-level=high`; OWASP dependency-check [unverified: Java runtime needed for dep-check] | Yes — High/Critical blocks | PH-1 |
| 8 | **Secret scan** | Gitleaks (on PR diff + full history on first run) | Yes — any finding blocks | PH-1 (pre-commit + CI) |
| 9 | **API Integration tests** | xUnit + Testcontainers (SQL Server + MinIO); WebApplicationFactory | Yes — on PR→main | PH-1 |
| 10 | **Migration test** | EF migrations on fresh Testcontainers SQL Server; idempotency check | Yes | PH-1 |
| 11 | **Container build + scan** | `docker build` (multi-stage); Trivy (or Grype) image scan | Yes — Critical CVE blocks | PH-1 |
| 12 | **SBOM generation** | Syft → CycloneDX JSON; archived as artifact | No (informational) | PH-1 |
| 13 | **Deploy to staging** | `docker compose pull && docker compose up -d`; health check loop | Yes — staging unhealthy blocks | PH-1 |
| 14 | **E2E tests** | Playwright (Chromium + Firefox); `@axe-core/playwright` | Yes — critical/happy-path failures block | PH-1 |
| 15 | **Performance smoke** | k6 (NFR thresholds; 15 VU, 60 s) | No on PR; Yes on release | PH-2 |
| 16 | **Security DAST** | OWASP ZAP baseline scan [unverified] | Advisory on PR; blocks on release (PH-2+) | PH-2 |
| 17 | **Release approval** | Manual gate (lead sign-off in CI/GH environment); PR merged + tag created | Yes — release blocked until approved | PH-1 |
| 18 | **Deploy to prod** | `docker compose pull && docker compose up -d`; health + smoke | Yes — prod unhealthy = rollback | PH-1 |

### 3.2 Workflow file structure (`/.github/workflows/`)

```
.github/workflows/
  ci.yml          # Stages 1–12: triggered on PR + push to main
  deploy-stg.yml  # Stages 13–14: triggered on push to main
  release.yml     # Stages 15–18: triggered on tag v*.*.* with manual approval
  security.yml    # Weekly ZAP + full dep-check scan
  backup-check.yml# Monthly: trigger backup/restore test on staging
```

---

## 4. Security Gates Detail

### 4.1 SAST — CodeQL

- Languages: `csharp`, `javascript-typescript`.
- Queries: `security-extended` pack (OWASP Top 10 mapping).
- Suppressions require a code comment with issue reference + lead approval.
- Results posted to GitHub Security tab (or equivalent) and linked to PR.

### 4.2 Dependency Vulnerability Scan

```bash
# .NET
dotnet list package --vulnerable --include-transitive

# npm
npm audit --audit-level=high --json

# OWASP dependency-check (full; weekly job) [unverified: Java dep]
dependency-check --project acmp --scan . --format JSON --failOnCVSS 7
```

Exemptions: documented in `/docs/security-exemptions.md`; require lead + security-owner sign-off; expire after 90 days.

### 4.3 Secret Scanning — Gitleaks

- Pre-commit hook (`.pre-commit-config.yaml`): runs `gitleaks detect` on staged diff.
- CI: `gitleaks detect --source . --log-opts "origin/main..HEAD"` on every PR.
- Full history scan: run once on repo creation; re-run after any key rotation.
- `.gitleaks.toml` with project-specific allowlists (test fixtures, known-false-positives).

### 4.4 Container Image Scanning — Trivy

```bash
trivy image --exit-code 1 --severity CRITICAL acmp-api:latest
trivy image --exit-code 0 --severity HIGH --format sarif -o trivy-high.sarif acmp-api:latest
```

- Critical → blocks build.
- High → blocks release (not PR); advisory on PR.
- Results archived as pipeline artifact + uploaded to Security tab.

### 4.5 SBOM — Syft / CycloneDX

```bash
syft acmp-api:latest -o cyclonedx-json > sbom-api.json
syft acmp-web:latest -o cyclonedx-json > sbom-web.json
```

SBOM artifacts stored alongside each release tag; informational only (no gate). Required for supply-chain auditability.

---

## 5. On-Prem CI and Air-Gap Considerations

**Self-hosted runner setup:**
- GitHub Actions self-hosted runner installed on a dedicated CI VM (separate from prod/staging VMs) [unverified: org has GitHub Actions or equivalent].
- If the org uses a different CI system (GitLab CI, Azure DevOps, Jenkins), the stage table above maps directly; only the YAML syntax differs.
- Runner has Docker installed; runs all container-based tools natively.

**Air-gapped / restricted network image mirroring:**

| Concern | Mitigation |
|---|---|
| NuGet packages unavailable externally | Configure a local **NuGet feed** (e.g., GitHub Packages, Azure Artifacts, or Nexus [unverified]) as a proxy. `nuget.config` points to the mirror. |
| npm packages | **npm mirror** (Verdaccio [unverified] or Nexus npm proxy). `.npmrc` points to mirror. |
| Docker base images (`mcr.microsoft.com/dotnet/aspnet:8.0`, `nginx:alpine`, `mcr.microsoft.com/mssql/server`) | Pull once to a local **Docker registry** (Docker Registry v2 [unverified: storage] or Harbor). `docker-compose.yml` `image:` values point to internal registry. |
| CodeQL binary | Bundle with runner or pull from GitHub release mirror. |
| Trivy / Gitleaks / Syft | Binary bundled in runner image or fetched from internal artifact store. |
| Testcontainers images (`mcr.microsoft.com/mssql/server:2022-latest`, `minio/minio`) | Pre-pulled to local Docker registry; set `TESTCONTAINERS_HUB_IMAGE_NAME_PREFIX=registry.internal/` env var [unverified: Testcontainers env var name]. |

**Recommendation:** Stand up a local Docker registry (Harbor or plain Docker Registry v2) and a NuGet/npm proxy mirror as a one-time infra task before PH-1 CI setup. Document pull-through configuration in `deploy/registry/README.md`.

---

## 6. Deployment Runbook (CI-side steps)

### 6.1 Staging deploy (auto, on main push)

```bash
# On staging VM (via SSH from runner or runner is on the same network)
cd /opt/acmp
git pull origin main                        # pull latest compose files
docker compose pull                         # pull new images from registry
docker compose up -d --remove-orphans       # rolling restart (sequential)
sleep 30
curl -f http://localhost/healthz || exit 1  # health check
curl -f http://localhost/readyz  || exit 1  # readiness check
```

### 6.2 Prod release (manual-gated)

1. Lead approves the GitHub environment `production` (or equivalent approval gate).
2. CI runner SSHes to prod VM (via deploy key, not password).
3. Same `docker compose pull && up -d` sequence as staging.
4. Smoke test: `curl /healthz` + `curl /readyz` + one API call to `GET /api/topics?limit=1`.
5. If any check fails → automatic rollback (§6.3).
6. Pipeline marks deployment as success; release notes auto-generated from commit log.

### 6.3 Rollback

```bash
# Re-deploy previous image tag
docker compose down
IMAGE_TAG=<previous-tag> docker compose up -d
curl -f http://localhost/healthz || echo "CRITICAL: rollback failed"
```

Each release tag corresponds to a pinned image digest in the compose override file; rollback is deterministic. Previous image layer is cached locally on the VM (Docker layer cache), so rollback is fast (< 2 min).

---

## 7. Pipeline Auditability

| Requirement | Implementation |
|---|---|
| Who deployed, when, which version | GitHub Actions workflow run log (or CI log) stored ≥ 1 year; includes actor, trigger, image digest |
| Prod deployment approval | GitHub Environment protection rule; approver name + timestamp in run log |
| SAST/scan results | Archived as pipeline artifacts + posted to Security tab; retained per org policy |
| SBOM per release | CycloneDX JSON archived with each release tag |
| Failed gate = no deployment | All required checks enforced via branch protection; cannot merge/deploy without passing |
| Secret scan history | Gitleaks full-history scan result archived on first run |
| Dependency exemptions | `docs/security-exemptions.md` in repo, versioned, auditable |

---

## 8. Definition of "Security Cleared"

A build is security-cleared when:
1. CodeQL: zero High/Critical findings (or all findings have approved suppressions with references).
2. `dotnet list package --vulnerable` + `npm audit`: zero High/Critical vulnerabilities.
3. Gitleaks: zero secrets detected.
4. Trivy: zero Critical CVEs in any container image.
5. ZAP: (Phase 2+) zero High findings.
6. All items are recorded in the pipeline artifact and linked to the release notes.

---

## Traceability

Links: `docs/31-testing-strategy.md` (test layers used in CI stages) · `docs/33-containerization-and-deployment.md` (deploy runbook, image structure) · `docs/34-repository-structure.md` (`.github/workflows/` location, deploy scripts) · `docs/24-security-threat-model.md` (threats mitigated by SAST/DAST/scan gates) · `docs/25-security-controls.md` (OWASP ASVS L2 controls satisfied by pipeline) · `../README.md` §A (CON-001 self-contained; ADR-0002 stack; NFR-047 arch rule enforcement).
