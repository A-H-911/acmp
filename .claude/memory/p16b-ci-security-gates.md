---
name: p16b-ci-security-gates
description: P16 Batch 2 (CI security gates) — PR
metadata: 
  node_type: memory
  type: project
  originSessionId: 2031363d-7960-40fd-b178-0a9f137f06d0
---

P16 **Batch 2** = CI security gates + supply chain (C-SUP-01/02, C-SAST-01, C-SEC-02; OQ-027 default). **PR #126 is OPEN, NOT merged** — branch `feat/P16b-ci-security-gates`. Plan: `~/.claude/plans/apply-the-controls-in-giggly-volcano.md`. Batch 1 (#124/#125) is merged + ADR-0030/0031 Accepted.

**Landed + GATING (verified locally):** dependency CVE gate. A JSON pre-scan found **4** transitive HIGH CVEs; fixed via root `Directory.Build.props` — `System.Formats.Asn1` 8.0.1 + `System.Net.Http` 4.3.4 + `System.Text.RegularExpressions` 4.3.1 (solution-wide framework pkgs), `Newtonsoft.Json` 13.0.3 (project-scoped to the 4 host projects: Api/Webex/Bootstrap/Worker). Re-scan → **0 High/Critical**. `scripts/check-vulns.mjs` (parses `dotnet list --vulnerable --format json`, `execFileSync` no-shell) replaces ci.yml's report-only step → blocks on High/Critical. Full suite **1430** + coverage 99.67% green (no regression from the bumps).

**Landed + now GATING** in `.github/workflows/security.yml` (all triaged 2026-07-16, head `5148026`):
- **Gitleaks** — 153 commits clean under `.gitleaks.toml` allowlist. Gating.
- **Semgrep OSS** — 2 `csharp-sqli` FPs in `AuditImmutabilityDbPermissionTests` (fixed test DDL/DML, no untrusted input) suppressed with **bare `// nosemgrep`** on the 2 helper lines (the targeted rule-id form FAILED — real id is doubled `csharp-sqli.csharp-sqli`). Gating.
- **Trivy fs** — `trivy-action@0.28.0` had no `v`; `@v0.28.0` resolves but is itself broken (transitive `setup-trivy@v0.2.1` retagged upstream). Replaced with **pinned docker image `aquasec/trivy:0.72.0`** (mirrors gitleaks step; NOT Dependabot-managed → bump by hand). First real scan: 0 vuln/secret/CRITICAL + **3 HIGH** Dockerfile misconfigs → recorded as **D-21** (B4). Gates on `--severity CRITICAL --exit-code 1 --ignore-unfixed`.
- SBOM (CycloneDX) + `.github/dependabot.yml` unchanged.

**⚠ YAML footgun (fixed):** a `": "` inside the Trivy step `name:` broke the workflow parse → run executed **0 jobs / failure** (looks like infra flake, isn't). Always `python -c "import yaml; yaml.safe_load(...)"` workflow files before pushing.

**STATE 2026-07-16:** **all 8 PR #126 checks GREEN on `5148026`** (CI ×3, E2E, Security secrets/sast/trivy-fs/sbom); `mergeStateStatus=CLEAN`. `main` is **unprotected** → security checks are advisory-until-added-to-branch-protection (ops follow-up). No new ADR, no AC change; new **D-21**.

**RESUME TASKS:**
1. **Merge #126** — no-review guard; needs explicit "merge without review" consent (as #124/#125). **Do not self-merge.**
2. Then **B2b**: Trivy IMAGE scan + base-image digest-pinning (see below).

**B2b (deferred trim candidates):** Trivy IMAGE scan (builds api/web images) + base-image **digest-pinning** (5 Dockerfile FROMs + 4 compose images minio/postgres/keycloak/ngrok; use `buildx imagetools` **manifest-list** digest; `compose config -q` does NOT pull → only e2e validates a bad digest).

**Then:** B3 (transit/at-rest crypto scaffold + strict CSP/HSTS, OQ-024/028), B4 (container hardening non-root/read-only-FS + rate-limit + magic-byte upload sniffing + Serilog PII redaction + Seq anomaly alerts, OQ-025/026), then **P14** Tarseem/Diagrams.

**Facts:** OQ-027 stays **Deferred** (its ZAP/DAST leg not in B2). **No new ADR**, **no AC verdict change**. Pitfall: `continue-on-error` masks a scanner's findings in the check status → always read the job log. A real historical secret from Gitleaks → **rotate**, don't allowlist. See [[p16a-audit-vote-crypto]], [[ci-gates-run-locally-pre-push]], [[coverage-and-e2e-mandate]].
