# 25 — Security Controls (Deliverable 33)

**Purpose:** The ACMP security-control plan — each control has an ID, ties to one or more threats from `24-security-threat-model.md`, maps to OWASP ASVS 5.0 Level 2 (and OWASP Top 10 where applicable), and is sized to a **≤20-user on-prem** deployment, with explicit notes where ASVS L3 would be overkill.

> Target: **OWASP ASVS 5.0 L2** (NFR-018). Threat IDs (`T-…`, `AB-…`) and asset IDs (`A…`) are from `24`. ASVS chapter bindings use the published ASVS 5.0 (May 2025) 17-chapter structure (V1–V17); chapter-title bindings are `[unverified]` against a live re-fetch in this environment. OWASP Top 10 (2021) cross-refs as `A0x`. Phase: **P1** = v1 / **P2** = Webex phase / **P3** = AI-extraction phase. Sources: https://owasp.org/www-project-application-security-verification-standard/ · https://github.com/OWASP/ASVS · https://owasp.org/Top10/ · https://genai.owasp.org (LLM Top 10).

---

## 0. Control-plan principles (proportional to ≤20 users on-prem)

1. **Deny-by-default, defence-in-depth.** Authorization, transport, and validation enforced at every boundary (`24` §2), not just the edge.
2. **Right-size to L2, not L3.** This is a small, internal, on-prem, low-traffic, high-sensitivity system. We meet **L2** in full. We explicitly **do not** adopt L3-only burdens that buy little here: HSM/key-vault hardware, full anti-automation/bot-defence tiers, exhaustive memory-safety proofs, or client-side cert pinning. Each such omission is noted inline as **[L3-skip]** with the reason. Insider-risk controls (SoD, immutability, audit) are nonetheless implemented at L3-grade because they are the actual threat.
3. **The IdP owns authN strength.** ACMP self-hosts Keycloak (ADR-0015); MFA, password policy, and account lockout are configured in ACMP's **own** Keycloak realm, not re-implemented in ACMP app code. ACMP validates tokens and enforces authZ.
4. **Insider-first.** Where prevention can't stop a privileged insider (P3), add **detection** (Seq anomaly alerting) and **non-repudiation** (append-only, attributed, optionally hash-chained audit).
5. **No immutability bypass for any role** — including Administrator and Chairman (`10-permission-role-matrix.md` §E.5).

---

## 1. Authentication (Keycloak OIDC) — V6 / V9 / V10

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-AUTH-01** | Browser↔IdP login via **OIDC Authorization-Code flow with PKCE** (public SPA client); no implicit flow, no ROPC. Tokens never in URL. | T-01, AB-7 | V10, V9 · A07 | P1 |
| **C-AUTH-02** | **MFA enforced at ACMP's self-hosted Keycloak realm** (ADR-0015) for all human accounts; password policy, brute-force/lockout, and credential storage are the IdP's responsibility (ACMP holds no passwords). **No public self-registration** (ADR-0004) — invitation/provisioned onboarding only. | T-01, AB-3 | V6 · A07 | P1 |
| **C-AUTH-03** | **Validate the OIDC JWT on every API request** (signature via Keycloak JWKS, issuer, audience, expiry, `nbf`). Missing/invalid → **401**; insufficient role/scope → **403**. **No unauthenticated endpoint** except `/health` and the OIDC callback (NFR-020). **Actor identity = validated `sub` claim**, never a client-supplied id. | T-01, T-02, T-11, AB-7 | V9, V6, V4 · A07 | P1 |
| **C-AUTH-04** | **Roles sourced only from Keycloak group/realm-role claims**, mapped to canonical roles (ADR-0004); the SPA cannot self-assert roles. Token claims are the single source of role truth, re-checked server-side per request. | T-07, AB-3 | V8, V9 | P1 |
| **C-AUTH-05** | **Segregation-of-duties guards (SoD-1…SoD-5)** enforced as hard authZ checks (`10` §E.4): action **verifier ≠ owner/assignee** (SoD-1); MoM approver ≠ sole author, warn+audit (SoD-2); vote **cast ≠ count ≠ chairman override**, co-attested tally (SoD-3); decision recorder ≠ sole owner/presenter + COI exclusion (SoD-4); **Administrator excluded from all committee-content authority** (SoD-5). | T-04, T-05, T-07, T-11, AB-1, AB-2, AB-3, AB-7 | V8 | P1 |

> **[L3-skip]** No step-up re-authentication per sensitive action and no hardware-token binding — disproportionate for ≤20 trusted, MFA'd internal users; SoD + audit cover the residual.

---

## 2. Authorization (policy-based RBAC + ABAC, least privilege) — V8

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-AUTHZ-01** | **ASP.NET Core policy-based authorization**, deny-by-default. Each action maps to a named policy (`Policy.Vote.Cast`, `Policy.Decision.ChairApprove`, …) per the `10-permission-role-matrix.md` capability matrix. Absence of an explicit Allow = Deny. | T-02, T-07, AB-3 | V8 · A01 | P1 |
| **C-AUTHZ-02** | **Resource-based authorization** (`IAuthorizationService.AuthorizeAsync(user, resource, policy)`) for every per-instance decision, so the handler evaluates the **actual** target aggregate (the `Topic`/`Vote`/`Action`) for relationship + scope + SoD — preventing IDOR/horizontal access. | T-07, T-08, AB-3 | V8 · A01 | P1 |
| **C-AUTHZ-03** | **Stream-scope ABAC** (`StreamScopeRequirement`): a `Member`/`Reviewer`/`Submitter` may act only on topics intersecting their assigned streams; `Chairman`/`Secretary`/`Auditor`/`Administrator` are committee-wide (read). Default visibility per OQ-AUTH-001. | T-08, AB-6 | V8 · A01 | P1 |
| **C-AUTHZ-04** | **Confidentiality ABAC** (`ConfidentialityRequirement`): `Restricted` topics (security findings, sensitive partner matters) visible only to Chair/Coord, explicit grantees, Owner/Assignees, and Auditor (read); excluded from default stream visibility **and from search results** for non-grantees. Confidentiality narrows, never widens, scope. | T-08, AB-6, AB-8 | V8, V14 · A01 | P1 |
| **C-AUTHZ-05** | **Least-privilege onboarding + delegation:** provisioned users default to `Member`/`Submitter`; elevated roles explicit, audited, time-bounded; delegation (`Auth.Delegate`) is a bounded-window, scoped, auto-expiring, audited grant (`10` §E.3). | T-07, AB-3 | V8 | P1 |

---

## 3. Session management — V7

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-SESS-01** | **Idle timeout ≤8h, absolute session ≤24h** without re-auth (NFR-023, `[unverified]` exact value → OQ-SEC-002); enforced via Keycloak token lifetimes + refresh policy. Logout/Keycloak SLO invalidates the session. | T-01 | V7 · A07 | P1 |
| **C-SESS-02** | If any browser-stored session cookie is used (e.g. anti-forgery/auth helper), it is `HttpOnly`, `Secure`, `SameSite=Strict/Lax`. Access tokens kept in memory (not `localStorage`) to limit XSS theft. | T-01, T-13, T-14 | V7, V3 · A07 | P1 |

> **[L3-skip]** No per-request token rotation / continuous session re-evaluation — overkill at this scale; idle+absolute timeout suffices for L2.

---

## 4. API security (rate-limit, validation, surface) — V4 / V2

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-API-01** | **Minimal authenticated surface:** every endpoint authenticated+authorized except `/health` and OIDC callback (NFR-020); REST verbs match intent; no debug/admin endpoints exposed through nginx beyond the single `/api` route (`15-architecture.md` §9). | T-02 | V4 · A01 | P1 |
| **C-API-02** | **Export endpoints are scoped by role** (`Report.Export`): Auditor/Chair/Secretary full; Member/Reviewer scoped; Submitter own-only (`10` row 30). Bulk/list responses honour stream-scope + confidentiality. | T-10, AB-6 | V8, V4 · A01 | P1 |
| **C-API-03** | **Basic rate-limiting** (ASP.NET rate-limiter / nginx `limit_req`) on auth, search, and export endpoints to blunt scripted enumeration/export and brute force. **Proportional:** thresholds tuned for ~15 concurrent users, not anti-DDoS. | T-10, T-22, AB-6 | V2 (anti-automation, L2-light) · A04 | P1 |
| **C-API-04** | **Strict request validation & content negotiation:** reject unexpected content types, enforce max body/JSON depth/array size, validate all DTOs (`C-INP-01`); stable error contract (`C-WEB-03`). | T-12, T-13, T-22 | V2, V4 | P1 |

---

## 5. Input validation & output encoding (XSS / SQLi) — V1 / V2 / V3

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-INP-01** | **Server-side validation of all input** (FluentValidation / data annotations on every command/DTO): type, length, range, allow-lists for enums/ids; bilingual text length caps. Client validation is UX only, never trusted. | T-12, T-13, AB-8 | V2 · A03 | P1 |
| **C-INP-02** | **No SQL injection vectors:** EF Core LINQ or **parameterized queries only**; **raw SQL string concatenation with user input is prohibited** (NFR-021), enforced by SAST + review gate. Recursive-CTE traceability queries are parameterized. | T-12, AB-10 | V2 · A03 | P1 |
| **C-INP-03** | **Output encoding / contextual escaping:** React default JSX escaping retained; **no `dangerouslySetInnerHTML`** on user content (or sanitized via DOMPurify if Markdown render is needed for `Document`/`Comment`); API returns data, not HTML. | T-13, AB-8 | V1, V3 · A03 | P1 |

---

## 6. File-upload & media handling (uploads, recordings, transcripts) — V5 / V14

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-FILE-01** | **Content-type allow-list + size limit:** validate declared and **sniffed** content type against an allow-list (presentations, docs, images, audio/video for recordings); enforce per-file ≤100 MB (NFR-011); reject mismatches. | T-15, AB-9 | V5 · A04 | P1 |
| **C-FILE-02** | **Malware scanning** of every upload before it is made available: **ClamAV sidecar container** on the Compose network; **scan-on-upload, quarantine-until-clean**; infected → reject + audit + alert. (Approach/AV choice → OQ-SEC-004.) | T-15, AB-9, AB-10 | V5 · A08 | P1 |
| **C-FILE-03** | **Safe storage:** bytes in **MinIO** (not the webroot), random/opaque object keys, **no execute**, metadata in SQL (`Attachment`); served only via the API/pre-signed URL, never a guessable path. | T-15, AB-4, AB-9 | V5 · A01 | P1 |
| **C-FILE-04** | **Recordings/transcripts access control:** retrieval only via **pre-signed, time-limited (≤1h) MinIO URLs** (NFR-027), **role-gated to Chairman/Secretary/Auditor** (NFR-025); the URL is single-purpose and expires. Direct bucket access blocked (creds are app-only secrets). | T-09, AB-4 | V5, V14 · A01 | P1 |
| **C-FILE-05** | **Render-time sanitization:** SVG/diagram artifacts (Tarseem outputs, uploaded SVGs) are sanitized/served with a restrictive `Content-Security-Policy` and `Content-Disposition` to prevent script execution in the browser; treat the **Tarseem spec as untrusted input** (`24` TB-10). | T-13, AB-9, AB-10 | V5, V3 | P1 (sanitize) / P2 (Tarseem render) |

---

## 7. Cryptography — transit & at rest — V11 / V12

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-CRYPTO-01** | **TLS everywhere, incl. internal:** nginx terminates TLS at the edge; **all internal container links (API↔SQL, API↔MinIO, API↔Seq, API↔Tarseem) use TLS ≥1.2**; TLS 1.0/1.1 disabled (NFR-019). Verified by TLS scan. | T-20, T-03 | V12 · A02 | P1 |
| **C-CRYPTO-02** | **Encryption at rest:** **SQL Server TDE** for the database; **MinIO server-side encryption (SSE)** for objects (recordings/transcripts/attachments). Stolen volumes/buckets are not plaintext. | T-09, T-23, AB-4 | V11, V14 · A02 | P1 |
| **C-CRYPTO-03** | **Backup encryption:** nightly SQL + MinIO backups to the standby VM are encrypted at rest; backup media is not plaintext if exfiltrated. | T-23, AB-5 | V11 · A02 | P1 |

> **[L3-skip]** Keys are managed via OS/Docker secret material + SQL TDE key hierarchy on the host; **no HSM/dedicated key-vault** — disproportionate for on-prem ≤20 users (see RISK-SEC-002). Revisit if the org mandates HSM-backed keys.

---

## 8. Secret management — V13 / V11

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-SEC-01** | **No secrets in source, build artifacts, or images** (NFR-024): DB connection string, Keycloak client secret, MinIO keys, Webex bot token (P2), Tarseem token (P2) injected via **Docker secrets / environment files excluded from VCS** (`.env` git-ignored), read at runtime. Config table holds **non-secret** config only (`16` §2.15). | T-03, AB-8 | V13 · A05 | P1 |
| **C-SEC-02** | **Secret scanning in CI** (gitleaks/trufflehog) blocks commits/builds containing secrets; image scan checks for embedded credentials. | T-03, AB-8, AB-10 | V13, V15 · A05 | P1 |
| **C-SEC-03** | **Rotation:** documented rotation procedure + cadence for all secrets/tokens; rotation does not require a code change (config-only, NFR-053). Compromise response rotates the affected secret and invalidates sessions. | T-03 | V13 | P1 |

---

## 9. Audit logging (append-only) — V16

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-AUDIT-01** | **Append-only `AuditEvent`:** no UPDATE/DELETE triggers, constraints, or ORM paths target the audit table (NFR-040); the platform exposes only an internal recorder — **no write/delete API** for any role. Full design in `26-audit-and-records-management.md`. | T-06, T-21, AB-5 | V16 · A09 | P1 |
| **C-AUDIT-02** | **Every state transition writes one audit row in the same DB transaction** (NFR-042) — no transition without an audit entry is a defect. Votes (each ballot, open/close), decisions, ADRs, MoM, role grants all covered. | T-04, T-05, T-21, AB-1 | V16 · A09 | P1 |
| **C-AUDIT-03** | **Server-derived, attributed, correlated events:** actor = validated `sub` (never client-supplied); server-set timestamps (`OccurredAt`, `IssuedAt`); `CorrelationId`, `Action`, `SubjectType/Id`, `Outcome`, before/after. Defeats back-dating/impersonation in the record. | T-05, T-11, AB-2, AB-7 | V16 · A09 | P1 |
| **C-AUDIT-04** | **DB-permission segregation:** the application's SQL principal has **no DELETE/UPDATE grant on `audit.*`** (and none on closed-vote/issued-decision rows); **administrators cannot delete audit** (`26`). Insert-only at the database, not merely the app layer. | T-06, AB-5 | V16, V13 · A09 | P1 |
| **C-AUDIT-05** | **Privileged actions audited + alertable:** role grants/revocations, delegations, config changes, retention changes emit high-importance audit events feeding `C-INS-01`. | T-07, AB-3 | V16 · A09 | P1 |
| **C-AUDIT-06** | **Audit on deny:** authorization denials of sensitive actions emit an `AuthEvent`/`AuditEvent` (`Outcome=Denied`) — visibility into probing/escalation attempts. | T-07, T-08, AB-3 | V16 · A09 | P1 |
| **C-AUDIT-07** | **Read-auditing of sensitive items:** every **transcript/recording read** generates an audit entry (NFR-025); reads of `Restricted` topics audited. (Routine reads of non-sensitive items are not audited — proportional.) | T-09, AB-4 | V16, V14 | P1 |
| **C-AUDIT-08** | **Export auditing:** every report/data export is an audited sensitive event (who, scope, volume) feeding anomaly detection. | T-10, AB-6 | V16 · A09 | P1 |

---

## 10. Immutability (votes / decisions / ADRs / minutes) — V8 / V16

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-IMM-01** | **Votes immutable after `Closed`:** no API permits mutation of a closed vote's ballots/tally; attempts → **409** (NFR-041). Corrections = a new vote, recorded as such. No role bypass (`10` §E.5). | T-04, AB-1 | V8, V16 · A04 | P1 |
| **C-IMM-02** | **Issued decisions immutable:** an `Issued` `Decision` is **superseded by a new decision, never edited**; field mutation → 409 (NFR-041). Same pattern for **approved ADRs** (superseded/deprecated) and **published MoM** (new version). | T-05, AB-2 | V8, V16 · A04 | P1 |
| **C-IMM-03** | **Immutability guard above RBAC/ABAC:** even an Allow cell is rejected if it would mutate an immutable record; handlers return a domain error, audited (`10` §E.5). Hard-delete prohibited for governance records (`16` §1.6). | T-04, T-05, T-06, AB-1, AB-2, AB-5 | V8, V16 | P1 |
| **C-IMM-04** | **Tamper-evidence (hash-chain) for the most critical records:** optional `PrevHash`/`RowHash` chain on `AuditEvent` and on vote/decision records makes silent tampering **detectable** (recommended for votes/decisions; ties to **OQ-DATA-002**). Full design + verification in `26`. | T-04, T-05, T-06, T-21, AB-1, AB-2, AB-5 | V16, V11 | P1 (design) / P1–P2 (enable) |

---

## 11. Privacy & data protection — V14

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-PRIV-01** | **PII minimization in logs/traces:** logs/traces carry **pseudonymous `UserId` only** — no full names, emails, or vote content in log payloads (NFR-028); Serilog destructuring policies redact sensitive fields. | A8 disclosure (T-19), AB-8 | V14 · A09 | P1 |
| **C-PRIV-02** | **No sensitive values in logs/notifications:** pre-signed URLs, tokens, secrets, and recording/transcript content **never** appear in Seq output or notification bodies (NFR-027). Log scrubber + review. | T-09, T-19, AB-4, AB-8 | V14 | P1 |
| **C-PRIV-03** | **Attributed-vote handling:** voting is always attributed in v1 (ADR-0010, NFR-029); individual choices are treated as sensitive PII — visible to Auditor/Chairman + the results view, access-controlled and audited, never leaked to logs. (No anonymity pathway in v1.) | A1/A8 (T-09), AB-1 | V14, V8 | P1 |

---

## 12. Insider-risk controls (dual-control, anomaly alerting) — V16 / V8

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-INS-01** | **Anomaly alerting via Seq** (the detection tripwire for privileged insiders P3): alert rules on **bulk/atypical export volume**, atypical access to `Restricted` topics or recordings, role grants/delegations, bursts of authz denials, and off-hours privileged actions. Optional **dual-control (four-eyes)** for the most sensitive ops (bulk export, chairman override) as a configurable enhancement (→ OQ-SEC-003). | AB-1, AB-2, AB-5, AB-6, T-04, T-05, T-07, T-10 | V16, V8 · A09 | P1 (alerting) / P1+ (dual-control opt) |
| **C-INS-02** | **Audit-integrity monitoring:** a Seq alert fires on **any audit-log write failure or detected chain gap** (NFR-046); periodic hash-chain verification job (`C-IMM-04`, `26`) flags tampering. | T-06, AB-5 | V16 · A09 | P1 |
| **C-INS-03** | **Separation of platform vs governance power** (SoD-5, restating C-AUTH-05 for emphasis): `Administrator` runs the platform but **cannot vote, decide, approve governance, or read decision content beyond config needs**, and cannot self-grant committee authority. Reduces the blast radius of a rogue admin. | T-07, AB-3, AB-5 | V8 | P1 |

---

## 13. Notification security — V14

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-NOTIF-01** | **No sensitive content in notifications:** in-app notifications (v1) and Webex cards (P2) carry **type + minimal metadata + an in-app link**, never decision rationale, vote content, restricted-topic detail, transcript text, or pre-signed URLs. The recipient must authenticate + pass authZ in-app to see the subject. | T-19, AB-8 | V14 | P1 (in-app) / P2 (Webex) |
| **C-NOTIF-02** | **Channel abstraction keeps content policy central** (`INotificationChannel`, ADR-0005): the same redaction/minimization applies regardless of channel; adding Webex/Email later cannot widen what a notification reveals. | T-19 | V14 | P1 |

---

## 14. Webex integration security (Phase 2) — V4 / V12

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-WX-01** | **Webhook signature verification:** validate the Webex webhook **HMAC signature** (shared secret) and reject/replay-protect unsigned or mismatched payloads before processing (`18-webex-feasibility.md`). | T-18 | V4, V12 · A08 | P2 |
| **C-WX-02** | **Least-scope tokens:** OAuth integration (user-scoped) or bot token granted the **minimum scopes** needed (meetings/recordings/messaging); tokens stored as secrets (`C-SEC-01`), rotated (`C-SEC-03`); honour 429 + `Retry-After` (rate limits ~300 req/min). | T-03, T-18 | V13, V4 | P2 |
| **C-WX-03** | **Adapter isolation:** all Webex calls behind the integration adapter; ACMP **never hard-depends** on Webex; a compromised/abusive integration cannot reach beyond the adapter's contract. Transcript automation is **not assumed** (gated by Webex Assistant, `.context/brief-digest.md` §5.3). | T-18, AB-10 | V15, V4 | P2 |

---

## 15. Supply-chain security — V15

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-SUP-01** | **SBOM + pinned dependencies:** generate an SBOM (CycloneDX) for app + container; all NuGet/npm deps pinned or semver-constrained, no wildcards (NFR-051); base images pinned by digest. | T-17, AB-10 | V15 · A06 | P1 |
| **C-SUP-02** | **Continuous scanning in CI:** dependency CVE scan (`dotnet list package --vulnerable`, `npm audit`, Dependabot ≤48h), **secret scan** (`C-SEC-02`), and **container image scan** (Trivy/Grype) gate the build; known-high CVEs block release. | T-03, T-17, AB-10 | V15 · A06 | P1 |
| **C-SUP-03** | **Treat external inputs as untrusted:** Tarseem diagram specs and Keystone imported manifests are validated/sanitized on ingress and **never auto-promoted** (`24` TB-10/TB-11; LLM01 posture). | T-16, AB-10 | V15, V2 | P1 (import) / P2 (Tarseem) |

---

## 16. Container & host security — V13 / V15

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-CON-01** | **Non-root containers:** the ACMP app (and sidecars) run as a non-root user; drop Linux capabilities; `no-new-privileges`. | T-17, T-23, AB-10 | V15 · A05 | P1 |
| **C-CON-02** | **Minimal base image + small surface:** build from a minimal/distroless base (`mcr.microsoft.com/dotnet/aspnet:8.0` minimal or distroless), image ≤500 MB (NFR-054); no shells/build tools in the runtime image. | T-17, T-23 | V15 · A06 | P1 |
| **C-CON-03** | **Read-only root filesystem** for app containers with explicit writable tmpfs/volumes only where needed; only nginx is network-exposed; internal services not published to the host (`15` §9). | T-23, AB-10 | V15, V13 · A05 | P1 |

---

## 17. Secure SDLC — SAST / DAST — V15 / V16

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-SAST-01** | **SAST in CI** (Semgrep/SonarQube): flags injection (raw SQL), insecure deserialization, weak crypto, hardcoded secrets, missing authz attributes; high findings block merge (NFR-021). | T-12, T-13, T-03 | V15 · A03 | P1 |
| **C-DAST-01** | **DAST on staging** (OWASP ZAP/equivalent) before go-live: CSRF, XSS, headers, auth/session, common misconfig (NFR-018/022); plus a **manual penetration test pre-production** focused on authZ/IDOR, immutability bypass, and media access. | T-13, T-14, T-08, T-04 | V15, V16 · A05 | P1 |
| **C-WEB-01** | **Security headers / CSP:** nginx/app emit `Content-Security-Policy`, `Strict-Transport-Security`, `X-Content-Type-Options:nosniff`, `X-Frame-Options/Frame-Ancestors`, `Referrer-Policy`. | T-13 | V3 · A05 | P1 |
| **C-WEB-02** | **CSRF protection** for all state-changing requests via ASP.NET anti-forgery token and/or `SameSite` cookie policy (NFR-022). | T-14 | V3, V4 · A01 | P1 |
| **C-WEB-03** | **Safe error handling:** generic error responses to clients (no stack traces, SQL errors, or internal paths); details only to Seq. Stable problem-details contract. | T-12, AB-8 | V16, V2 · A05 | P1 |

---

## 18. Backup / recovery & administrative access — V13

| Control-ID | Control | Threat addressed | ASVS / Top 10 | Phase |
|---|---|---|---|---|
| **C-BAK-01** | **Nightly SQL + MinIO backup to a separate standby VM/storage** (RPO ≤4h, RTO ≤8h, NFR-056/057/058); **quarterly restore test**. This is how 99.9% is met at this scale (not HA) and the recovery path for ransomware/destructive insider acts. | T-22, T-06, AB-5 | V13 | P1 |
| **C-BAK-02** | **Audit + immutable records included and protected in backups**, encrypted (`C-CRYPTO-03`), ideally **offline/immutable copy** so a privileged insider who deletes/alters live data cannot reach the backup → audit reconstruction possible. | T-06, AB-5 | V13, V16 | P1 |
| **C-ADM-01** | **Administrative access controls:** host/Docker/secret-file/backup access restricted to named operators, MFA on the host/jump path, least-privilege OS accounts; admin actions logged outside the app where feasible. | T-23, AB-5 | V13 · A05 | P1 |
| **C-ADM-02** | **Hangfire dashboard + ops endpoints authz-gated** to `Administrator`; not exposed through the public ingress. | T-07, T-23 | V8, V13 | P1 |
| **C-ADM-03** | **Change control for retention/immutability config:** changing retention or any safety toggle is a privileged, audited action (`C-AUDIT-05`); immutability cannot be toggled off for existing records. | T-06, AB-5 | V13, V16 | P1 |

---

## 19. AI / LLM extraction & recording privacy (Phase 3) — LLM01

| Control-ID | Control | Threat addressed | ASVS / Top 10 (LLM) | Phase |
|---|---|---|---|---|
| **C-AI-01** | **Treat transcript/recording content as untrusted (OWASP LLM01 prompt injection):** all LLM inputs derived from transcripts are untrusted data; prompts are structured to resist injection; no transcript text is interpreted as instructions (NFR-026). | T-16, AB-10 | LLM01 | P3 |
| **C-AI-02** | **Human-in-the-loop:** LLM outputs are **candidates** (`Origin=CandidateFromTranscript`, `IsApproved=false`) until an authorized human explicitly approves; **no AI-generated content is auto-committed** to the record (NFR-026, principle 5). | T-16 | LLM01, LLM05 | P3 |
| **C-AI-03** | **No tool/DB authority from the LLM:** extraction is read-only over transcript text → proposes candidates; the LLM has no write path, no DB credentials, no ability to trigger privileged actions. Recording/transcript inputs stay access-controlled (`C-FILE-04`) and reads audited (`C-AUDIT-07`). | T-16, T-09 | LLM01, LLM02 | P3 |

---

## 20. ASVS chapter coverage summary

| ASVS 5.0 chapter `[unverified titles]` | Covered by | Level |
|---|---|---|
| V1 Encoding & Sanitization | C-INP-03, C-FILE-05, C-WEB-01 | L2 |
| V2 Validation & Business Logic | C-INP-01/02, C-API-03/04, C-AI-01, C-SUP-03 | L2 |
| V3 Web Frontend Security | C-INP-03, C-SESS-02, C-WEB-01/02 | L2 |
| V4 API & Web Service | C-API-01/02/03/04, C-WEB-02, C-WX-01/02 | L2 |
| V5 File Handling | C-FILE-01/02/03/04/05 | L2 |
| V6 Authentication | C-AUTH-02/03 (+ IdP at Keycloak) | L2 |
| V7 Session Management | C-SESS-01/02 | L2 |
| V8 Authorization | C-AUTHZ-01..05, C-AUTH-04/05, C-IMM-01/02/03, C-INS-01/03 | L2+ (insider) |
| V9 Self-contained Tokens | C-AUTH-01/03/04 | L2 |
| V10 OAuth & OIDC | C-AUTH-01/02 | L2 |
| V11 Cryptography | C-CRYPTO-02/03, C-IMM-04 (hash) | L2 (no HSM — [L3-skip]) |
| V12 Secure Communication | C-CRYPTO-01, C-WX-01/02 | L2 |
| V13 Configuration | C-SEC-01/02/03, C-CON-03, C-BAK-01/02, C-ADM-01/02/03 | L2 |
| V14 Data Protection | C-AUTHZ-04, C-FILE-04, C-PRIV-01/02/03, C-NOTIF-01/02 | L2 |
| V15 Secure Coding & Architecture | C-SUP-01/02/03, C-CON-01/02/03, C-SAST-01, C-DAST-01 | L2 |
| V16 Security Logging & Error Handling | C-AUDIT-01..08, C-IMM-04, C-INS-01/02, C-WEB-03 | L2+ (insider) |
| V17 WebRTC | **N/A in v1** — ACMP does not run WebRTC media; Webex (P2) is the external conferencing system, accessed via API/adapter, not embedded. | N/A |

> **Proportionality recap.** L2 is met across all applicable chapters. The **insider-critical** chapters (V8 authorization, V16 logging/integrity) are implemented at **L3-grade** (SoD, immutability, append-only + DB-permission-segregated + hash-chained audit) because that is the real threat. The deliberately **right-sized** areas — no HSM (V11), L2-light anti-automation (V2), no WebRTC hardening (V17), backup-not-HA for availability — are each justified by ≤20-user on-prem scale.

---

## 21. New open-question candidates raised here

| ID | Item | Default |
|---|---|---|
| **OQ-SEC-005** | Confirm SAST/DAST/secret/image-scan tool choices and CI gating thresholds (which severities block merge/release). | Semgrep/SonarQube (SAST), ZAP (DAST), gitleaks (secrets), Trivy (image); block on High+. |
| **OQ-SEC-006** | Confirm whether a `Content-Security-Policy` strict enough to forbid inline scripts is compatible with the chosen React build/runtime (nonce/hash strategy). | Strict CSP with hashes/nonces; validate during build. |
| (see also) | OQ-SEC-001..004 and RISK-SEC-001..002 from `24-security-threat-model.md`. | — |

---

## Traceability
Implements **Deliverable 33**. Every control ties to a threat (`T-…`/`AB-…`) and asset (`A…`) in `24-security-threat-model.md` and to an OWASP ASVS 5.0 chapter (V1–V17) + OWASP Top 10 (2021)/LLM Top 10. Authorization/SoD/immutability policy realized in `10-permission-role-matrix.md` §E; data-protection mechanics in `16-data-architecture-and-model.md` §1.6/§1.8/§1.10; audit/records design in `26-audit-and-records-management.md`. NFRs satisfied: NFR-018 (ASVS L2), NFR-019 (TLS), NFR-020 (authN/Z surface), NFR-021 (injection), NFR-022 (CSRF), NFR-023 (session), NFR-024 (secrets), NFR-025/027 (recordings/transcripts), NFR-026 (LLM01), NFR-028/029 (privacy/attributed voting), NFR-040/041/042 (audit/immutability), NFR-046 (alerting), NFR-051 (CVEs), NFR-054 (image size), NFR-056..058 (backup/DR). Settled decisions: ADR-0004 (Keycloak/OIDC), ADR-0005 (notifications/channels), ADR-0009 (audit/immutability), ADR-0010 (attributed voting), ADR-0006/0007 (Tarseem/Keystone untrusted inputs). Webex specifics: `18-webex-feasibility.md`. Standards: `22-standards-and-best-practices.md`. New: OQ-SEC-005..006 (plus OQ-SEC-001..004, RISK-SEC-001..002 from `24`).
