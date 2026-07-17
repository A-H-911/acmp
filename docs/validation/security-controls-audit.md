---
status: Living
version: 0.3.0
updated: 2026-07-17
owner: Claude Code execution agent
generation: derived
---

# Security Controls Audit — ACMP (OWASP ASVS 5.0 L2)

The evidence ledger for the P16 security-hardening slice. Each control in
[`../domain/security-controls.md`](../domain/security-controls.md) is mapped to its **implementation status**,
the **code path** that realizes it, the **test** that proves it, and the **threat** it addresses (the
control→threat binding itself is authored in the control + threat-model docs; this register records *whether and
where* each control is implemented). P16 runs as GO-gated batches — rows carry the batch that delivers/hardens
them.

**Status vocabulary:** `Met` (implemented + tested) · `Partial` (implemented, with a stated residual) ·
`Operator/P18` (in-repo scaffold + doc; effective only after operator action) · `Batch N` (scheduled later in
P16) · `Deferred-feature` (out of hardening scope — logged as a feature).

## Batch 1 — Audit & vote crypto core (this slice)

| Control | Threat | Status | Code | Test |
|---|---|---|---|---|
| **C-IMM-04** per-ballot hash chain (D-13) | T-04, T-21, AB-1 | **Met** | `Decisions.Domain/BallotChain.cs`, `Vote.SealBallotChain`/`VerifyBallotChain` (sealed at Close) | `VoteBallotChainTests` (content/link/recusal/tally tamper, legacy skip) |
| **C-IMM-04** tally-vs-ballots recompute | T-04, AB-1 | **Met** | `Vote.ComputeTally`/`VerifyTally` | `VoteBallotChainTests`, `VoteChainIntegrityCheckTests` |
| **C-INS-02** nightly integrity verify job (D-16) | T-06, AB-5 | **Met** | `IIntegrityVerifier`/`IntegrityVerifier` + `IIntegrityCheck` seam; `AuditChainIntegrityCheck`, `VoteChainIntegrityCheck`; `Acmp.Worker` `Cron.Daily(3)` | `IntegrityVerifierTests`, `VoteChainIntegrityCheckTests` |
| **C-AUDIT-04** DB-permission audit immutability (D-16) | T-06, AB-5 | **Partial** (Operator/P18) | migration `Audit_DenyMutation` (`DENY UPDATE,DELETE ON SCHEMA::audit TO acmp_app`) | `AuditImmutabilityDbPermissionTests` (Testcontainers, restricted login) — **inert until app runs least-priv (P18), see ADR-0031** |
| **C-AUDIT-01/02/03** append-only, per-transition, server-derived | T-06, T-05, T-21 | **Met** (prior) | `AuditEvent` (no mutators), `SqlAuditSink.EmitEnrichedAsync` | `AuditEventEnrichmentTests`, `AuditAtomicityTests` |
| **C-AUDIT-05/06** privileged-action + deny auditing | T-07, T-08 | **Met** (prior) | `AuthorizationBehavior`, governed emit sites | `SegregationOfDutiesTests`, `AuditApiTests` |
| **Security test suite** | — | **Met** | `[Trait("Category","Security")]` across authz/SoD/audit/immutability + Batch-1 tests | `dotnet test --filter "Category=Security"` → 339 tests green |

> The AuditEvent hash-chain + on-demand verify (`GET /api/audit/verify`) predate P16 (AC-017/018/019/020 already
> `Met`). Batch 1 **closes the "→ P16" residual notes** on those rows (nightly job + DB-permission) and adds the
> per-ballot chain (D-13) — **no AC verdict changes**; per-ballot chaining maps to no single AC (it is tech-debt).

## Authentication / Authorization / SoD (prior — verified this slice)

| Control | Threat | Status | Evidence |
|---|---|---|---|
| C-AUTH-01/03/04 OIDC PKCE, JWT-per-request, claims-only roles | T-01, T-02, T-11 | Met | `RealJwtAuthTests`, `PermissionMatrixTests` |
| C-AUTHZ-01/02/03 policy deny-by-default, resource-based, stream-scope ABAC | T-07, T-08 | Met | `PermissionMatrixTests`, `AbacHandlerTests`, `ResourceAuthorizerTests` |
| C-AUTH-05 SoD-1/3/5 hard guards | T-04, T-05, T-07 | Met | `SegregationOfDutiesTests` |
| C-AUTH-05 SoD-2 (approver≠author) | T-05 | Met (soft, per spec `warn+audit`) | `MinutesHandlerTests` (AC-014) |
| C-AUTH-05 SoD-4 (recorder≠owner/presenter + COI) | T-05 | **Partial — no distinct hard guard found; verify in a later batch** | — |
| **C-AUTHZ-04 Confidentiality ABAC (Restricted topics)** | T-08, AB-6 | **Deferred-feature** | No `Confidentiality`/`Restricted` field exists in code — requires domain field + migration + UI + search-exclusion + handler; logged in deferred-work / open-question |
| C-IMM-01/02/03 vote/decision/MoM/ADR immutability + 409 | T-04, T-05 | Met | `VoteTests`, `RowVersionConcurrencyTests` |
| C-INP-01/02/03 validation, no-SQLi, output encoding | T-12, T-13 | Met | `ValidationBehavior`; EF-only; `MarkdownView` DOMPurify allowlist |

## Batch 2 — CI security gates + supply chain (PR #126, merged `11c6372`)

| Control | Threat | Status | Code | Test |
|---|---|---|---|---|
| **C-SUP-01** dependency CVE gate | T-03, AB-10 | **Met** | `scripts/check-vulns.mjs` (blocks High/Critical); 4 transitive HIGH CVEs fixed via `Directory.Build.props` | CI `ci.yml`; re-scan → 0 High/Critical |
| **C-SUP-01** base-image digest pinning | T-03, T-17 | **Met** | 5 Dockerfile bases + minio/postgres/keycloak×2 pinned `tag@sha256:` (B2b). **ngrok deliberately NOT pinned** — `profiles:[ngrok]`, never exercised by e2e, so a bad pin would fail silently | `SearchProvidersFtsTests` builds the pinned SQL base (needs Testcontainers ≥4.x — 3.10 cannot parse a digest in `FROM`) |
| **C-SUP-02** SBOM (CycloneDX) + Dependabot | AB-10 | **Met** | `security.yml` sbom job; `.github/dependabot.yml` | CI |
| **C-SAST-01** Gitleaks + Semgrep + Trivy fs | T-03, T-17 | **Met** | `security.yml` — all gating; Trivy fs `--severity CRITICAL,HIGH` (tightened in B4 on D-21 closure) | CI (153 commits clean; 2 `csharp-sqli` FPs suppressed) |
| **C-SUP-01** Trivy image scan | T-17 | **Partial** (report-only) | `security.yml` `trivy-image` job builds + scans api/web | CI — report-only by design (rebuilds images, re-finds unfixable base CVEs) |

## Batch 3 — Web headers + crypto scaffold (this slice)

| Control | Threat | Status | Code | Test |
|---|---|---|---|---|
| **C-WEB-01** security headers + HSTS | T-13 | **Met** | `deploy/nginx/default.conf.template` — CSP, HSTS (1y + includeSubDomains, no preload), Permissions-Policy, `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`. All **server-level** so nginx inherits them into `/api/`, `/kc/`, `/acmp-recordings/` (a per-location `add_header` would drop the inherited set — none is added) | Verified live against the read-only container: 200 on `/` carries all 6; the `/api/` proxy block returns the inherited HSTS/Permissions-Policy/CSP on a 502 |
| **C-WEB-02** strict CSP — `style-src 'self'`, no `'unsafe-inline'` (OQ-028) | T-13 | **Met** | Same template. **No frontend refactor was needed:** CSP governs `<style>` elements + `style=""` attributes, not the CSSOM, and react-dom applies `style={{}}` via CSSOM writes (`style.setProperty` / `style[name]=v`). No SSR (client-rendered SPA) ⇒ no style attribute is ever serialized; `index.html` has none; `MarkdownView` DOMPurify allowlist excludes `style` attrs + `<style>`; no CSS-in-JS dep | Browser-verified under `style-src 'self'`: CSSOM writes (incl. the @dnd-kit transform shape) **apply**; `setAttribute('style')` + injected `<style>` are **blocked**. Live console sweep = zero violations. Residual hygiene → **D-22** |
| **C-WEB-02** CSRF | T-14 | **Met** | Bearer API; tokens in `sessionStorage` (oidc-client-ts), **no ambient auth cookie** ⇒ no classic CSRF surface. The one cookie, `webex_oauth_state`, is HttpOnly + Secure + single-use + 10-min, and correctly **`SameSite=Lax`** — `Strict` would withhold it on the cross-site top-level OAuth callback redirect and break every flow's state check | `WebexEndpoints` state check (constant-time compare, consume-on-use) |
| **C-CRYPTO-01** TLS everywhere (transit) | T-20, T-03 | **Partial (Operator/P18)** | **SQL leg ON** (P16-B3): `Encrypt=True` on all 4 runtime sites (compose api+worker, both `appsettings.json`) — pure connection-string config, **no code reads or overrides it**, and SQL Server's auto-generated self-signed cert means nothing is mounted. **Still Partial for two reasons:** (1) `TrustServerCertificate=True` ⇒ any cert is accepted — encrypted, **not authenticated** (stops passive sniffing T-20, not an active MITM); (2) **API↔MinIO and API↔Seq remain plaintext** (`Minio__Secure=false`; 3 Seq endpoints on http) — neither server auto-generates a cert, so both need operator-provided certs. OQ-024: TLS 1.2+, no mTLS v1 | **Live-verified**: `sys.dm_exec_connections` — every `Core Microsoft SqlClient Data Provider` connection `encrypt_option=TRUE` (baseline `FALSE`); api+worker healthy; CI `e2e` exercises it end-to-end. Mirrors the C-AUDIT-04/ADR-0031 precedent for the remainder |
| **C-CRYPTO-02** at rest (SQL TDE, MinIO SSE) | T-09, T-23, AB-4 | **Partial (Operator/P18)** | Server features, not app config. **Correction (P16-B3): TDE is NOT blocked in this stack** — it runs **Developer edition** (`EngineEdition=3`, full Enterprise feature set), so TDE would work here today. It is deferred for **certificate key custody**: the cert lives in the `mssql-data` volume, which the rebuild guidance tells operators to `down -v`, and losing it renders backups **permanently unrecoverable**. Edition only forecloses TDE if the operator picks **Express/Web** at P18 (OQ-040). MinIO SSE needs a **KES key server** (new container + its own custody) | `deployment.md` §3.4 |
| **C-CRYPTO-03** backup encryption | T-23, AB-5 | **Partial (Operator/P18)** | Operator backup tooling. **Also edition-gated:** per Microsoft's SQL Server 2022 editions table, `Encryption for backups` is Enterprise/Standard-only — Express/Web lack it, exactly as they lack TDE | `deployment.md` §3.4 / §6 |
| *(already effective)* Webex OAuth token at rest | T-20 | **Met** (prior) | `WebexTokenProtector` — authenticated **AES-GCM**, 256-bit key; the one secret ACMP persists, encrypted independently of TDE | `WebexTokenProtectorTests` |

## Batch 4 — Runtime hardening (this slice)

| Control | Threat | Status | Code | Test |
|---|---|---|---|---|
| **C-CON-001** non-root containers | T-17, T-23 | **Met** | api+worker → built-in `app` (UID 1654); web → `nginxinc/nginx-unprivileged` (UID 101, listens 8080); sqlserver `--no-install-recommends` | `trivy config deploy/` → **0 misconfig** at CRITICAL,HIGH (**D-21 closed**) |
| **C-CON-002** cap_drop ALL + no-new-privileges | T-17, T-23 | **Met** | `docker-compose.yml` — `cap_drop: [ALL]` + `no-new-privileges` on the app containers (api/worker/web); **sidecars get no-new-privileges only** (each needs its own writable data dir; dropping caps risks their engines for no gain on internal-only services) | e2e stack boots healthy with the full matrix |
| **C-CON-003** read-only root filesystem | T-17, T-23 | **Met** | `read_only: true` on api/worker/web. Writable mounts scoped to what each image actually needs: api `/tmp` = **disk-backed volume** (ASP.NET `IFormFile` spools >64 KB multipart to `Path.GetTempPath()`; recordings cap at 2 GiB vs a 512 MB–1 GB RAM budget, and tmpfs counts against the memory cgroup ⇒ a RAM-backed `/tmp` would OOM), api `/keys` = tmpfs (DataProtection key-ring; ephemeral is fine under bearer auth), worker `/tmp` = tmpfs (spools nothing), web `/tmp` = 64m tmpfs + `/etc/nginx/conf.d` tmpfs **with `uid=101,gid=101,mode=0755`** (a bare tmpfs inherits root:root 0755 ⇒ envsubst cannot write the rendered config and nginx starts with **no server block** while still reporting `running`). sqlserver keeps its writable data volume = the documented "writable only where needed" exception | e2e: read-only stack boots healthy + a large recording upload succeeds. nginx `proxy_request_buffering off` on `/api/` streams the body instead of spooling it to the now-read-only proxy disk |
| **C-API-03** rate limiting | T-10, T-22 | **Met** | `HardeningExtensions.AddAcmpRateLimiting` — fixed-window, partitioned by **`sub`** (fairer than IP; no shared-NAT penalty and no need to trust proxy-forwarded IPs); anonymous webhook = one global bucket; 429 + `Retry-After`. Applied via `.RequireRateLimiting` on search / both uploads / the Webex webhook. Proportional (~15 users, not anti-DDoS) | `RateLimitingTests`; e2e 429 check |
| **C-FILE-01** magic-byte content sniffing | T-15, AB-9 | **Met** | `IFileContentInspector`/`MimeFileContentInspector` (Mime-Detective for pdf/png/jpeg/docx; **direct magic for video** — mp4/quicktime `ftyp`@4, webm EBML — since MD's default set omits them; svg/json = structural head-check). Bounded head-read + position restore; fail-closed pre-store in `UploadRecording` + `AttachFileToTopic`; topic-attach object key now **server-derived** (guid+ext, no raw filename) | `MimeFileContentInspectorTests`, `TopicAttachmentTests`, `MeetingRecordingTests` (`.pdf`-named PNG rejected) |
| **C-FILE-02** ClamAV malware scan | AB-9 | **Deferred-feature** (OQ-026 default) | Not built — MIME/ext whitelist + magic-byte sniffing is the v1 posture; ClamAV = **operator opt-in** | — |
| **C-PRIV-01/02** Serilog PII/secret redaction | T-19, AB-8 | **Met** | `SensitiveDataMaskingEnricher` (Acmp.Shared) masks sensitive property **names** (email/token/secret/signed-url/connstring → `***`); wired into **both** hosts | `SensitiveDataMaskingEnricherTests`; e2e: Seq shows masked PII |
| **C-NOTIF-01/02** notification-body minimization | T-19 | **Met** (audit, no code change) | Audited every `*Notifications.cs` builder: bodies carry the **artifact key** (`ACT-`/`DECN-`/`ADR-`/`VOTE-`), a deep link, and day counts — no names, no emails, no vote or decision content. `NotificationMessage.RecipientUserId` is the pseudonymous `sub`. The sole content field is a meeting title (`MinutesNotifications`), which is a governance artifact title, not personal data. Channel is **in-app only** (no email in v1), so a body never leaves the authenticated surface | `NotificationMessage` contract + per-module notification tests |
| **C-INS-01** Seq anomaly-alert rules | AB-1/2/5/6 | **Met** (runbook) | `post-release-operating-model.md` §2.4 — 5 rules, each keyed off a property the app **already emits**: integrity tamper (`@MessageTemplate like 'INTEGRITY ALERT%'`), 401/403 burst + 429 spike (`StatusCode` via `UseSerilogRequestLogging`), 5xx spike, audit-write failure. OQ-025 honored (audit+alert, **no dual-control**) | Seq alert config = operator step; signals verified present in code |

> **C-INS-01 signals deliberately NOT written** (recorded rather than silently skipped): **bulk/atypical export volume** — ACMP has *no* export feature (**D-07**, Phase 3); **atypical `Restricted`-topic access** — no confidentiality tier exists (**D-20**). A rule for either would be a tripwire that can never fire. Role grants/delegations emit **audit rows to SQL**, which Seq does not carry — reviewed in the `/audit` register (§4.6), not alerted on.

## Still scheduled / out of scope

| Control group | Threat | Target |
|---|---|---|
| OQ-027 ZAP/DAST leg | T-13 | **Deferred** (not in B2) |
| C-WX-*, C-AI-* Webex (P2) / AI extraction (P3) | T-18, T-16 | Out of v1 phase scope |

## Traceability
Implements the P16 evidence leg of Deliverable 33. Controls: `../domain/security-controls.md`; threats:
`../domain/security-threat-model.md`; decisions: ADR-0009/0026/0027 (audit), **ADR-0030** (per-ballot chain),
**ADR-0031** (DB-permission immutability). Deferred: D-13 (closed this batch), D-16 (job done; DB-permission
Operator/P18), Confidentiality ABAC (new feature). OQ defaults honored: OQ-024 (no mTLS v1), OQ-025 (no
dual-control), OQ-026 (ClamAV opt-in), OQ-028 (strict CSP).

> **Open finding for the operator — OQ-040 is a security decision, not just a capacity one.** OQ-040 (SQL Server
> edition) is recorded **`Blocking? = No`** with the default *"start with Express… escalate to Standard if limits
> bind"*. Per Microsoft's SQL Server 2022 editions table, **Express and Web support neither TDE nor
> `Encryption for backups`** (both Enterprise/Standard-only). Choosing Express therefore forecloses **two P1
> controls** — C-CRYPTO-02's SQL half and C-CRYPTO-03's SQL half — as a side effect. Surfaced by the P16-B3
> review; whether to re-classify OQ-040 as blocking is the operator's call (this register records the coupling,
> it does not change the OQ).
