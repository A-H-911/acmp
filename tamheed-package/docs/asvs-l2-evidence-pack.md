---
status: Living
version: 1.0.0
updated: 2026-08-29
owner: Claude Code execution agent
generation: derived
work_item: WBS-25.2
deferred_work: DW-079
requirement: NFR-018
decision: DEC-091 d2, DEC-091 d3
---

# OWASP ASVS 5.0 Level 2 — Evidence Pack for ACMP

**An input to an assessment. Not the assessment, and not a claim of conformance.**

---

## 0. Read this first — what this document is, and what it is not

`NFR-018` requires ACMP to meet OWASP ASVS 5.0 Level 2 for all applicable chapters. **This pack does
not and cannot establish that.** Level 2 conformance is established by an external assessor, never by
self-assertion, and nothing in this repository can substitute for that judgement.

What this pack *is*: a map from the controls ACMP **already has** to the ASVS 5.0 chapters, so an
assessor does not start cold and does not bill for rediscovering what is already built. It answers
*"where should I look, and what has this team already done?"* — never *"are these controls adequate?"*,
which is precisely the judgement an assessor is paid to make.

**No acceptance criterion is written from this pack, deliberately.** `DW-079` and the package's own
rules forbid it: verdicts are counted by retirement rather than by lifecycle status, so a criterion
written ahead of an assessor's report would hold package readiness false permanently. `NFR-018`
therefore stays `Approved` with no criterion, and that is the correct state, not an oversight.

> ⛔ **A specific inherited claim is NOT carried forward.** `docs/domain/security-controls.md` §20
> concludes *"L2 is met across all applicable chapters."* **That sentence is a self-assertion of
> conformance and this pack does not repeat it.** It is the single most likely thing to be copied
> forward by someone assembling a pack from the existing catalogue, and it is the one sentence that
> would turn an orientation document into a false claim.

### The three questions this pack answers, and the one it refuses

| Question | Answered here? |
|---|---|
| Which ASVS 5.0 chapters apply to ACMP, and why is any excluded? | Yes — §3 |
| Which ACMP controls map to each chapter, and where does their evidence live? | Yes — §4 |
| What is known to be missing, partial, or unverified? | Yes — §5, and it is the most important section |
| Does ACMP conform to ASVS 5.0 L2? | **No. That is the assessor's finding.** |

---

## 1. Provenance — how this document was built, so its claims can be checked

Every structural claim below was derived mechanically. The method is recorded so a reader can disagree
with the instrument rather than with an assertion.

| What | How it was established |
|---|---|
| Chapter numbers, titles, requirement counts | Parsed from the authoritative machine-readable release, `OWASP/ASVS` `5.0/docs_en/…_5.0.0_en.flat.json`, fetched 2026-08-29 |
| Level counts | Same file's `L` field |
| Control → chapter mapping | Parsed from the per-control *ASVS / Top 10* column **and** each section heading's own `V`-suffix in `docs/domain/security-controls.md`, taking the union where the two keys differ |
| Every file path cited | Asserted to exist by `scripts/check-asvs-pack-paths.mjs`, which fails closed |

**Measured from the standard:** 17 chapters · 345 requirements · L1 70, L2 183, L3 92. ASVS levels are
cumulative, so **"Level 2" is the 253 requirements at L1+L2**, not the 183 marked L2.

**Chapter titles verified.** `security-controls.md` §20 carried the marker `[unverified titles]`. All
17 titles were compared to the authoritative release: **17 claimed, 17 official, 0 mismatched, none
missing.** The comparison was calibrated first by injecting ASVS 4.0's chapter name *"Malicious Code"*
in place of V5 and confirming the checker reported it — a clean comparison from an uncalibrated checker
would prove nothing. **The `[unverified titles]` marker is discharged.**

> ⚠ **What was NOT done, stated so nobody inherits an unearned assurance.** The control → chapter
> mapping is inherited from `security-controls.md` and was **not re-derived from the ASVS requirement
> text**. It says which chapter a control was *intended* to answer; it does not establish that the
> control satisfies any particular V-numbered requirement within it. Establishing that is the
> assessment. Likewise, the code paths below are asserted to **exist**, not to be **sufficient**.

---

## 2. The system under assessment

ACMP is an on-premises, bilingual (EN/AR) governance platform for a **single** architecture committee,
≤20 users, low traffic. It is a client-rendered SPA over a .NET modular monolith, with Keycloak as the
identity provider, SQL Server as the store, MinIO for objects and Seq for observability.

Three scope facts materially shape which chapters apply:

- **ACMP holds no passwords.** Authentication is delegated entirely to a self-hosted Keycloak realm.
  Credential storage, password policy, brute-force lockout and MFA are the IdP's responsibility.
- **There is no ambient auth cookie.** The SPA holds tokens in memory and sends a bearer token, which
  removes the classic CSRF surface rather than mitigating it.
- **Webex (Phase 2) and AI extraction (Phase 3) are not in v1.** Controls named `C-WX-*` and `C-AI-*`
  exist in the catalogue as forward design, not as shipped code.

---

## 3. Chapter applicability

| Chapter | Applies to v1? | Note |
|---|---|---|
| V1 Encoding and Sanitization | Yes | |
| V2 Validation and Business Logic | Yes | |
| V3 Web Frontend Security | Yes | |
| V4 API and Web Service | Yes | |
| V5 File Handling | Yes | Uploads and meeting recordings |
| V6 Authentication | Yes — **largely delegated** | Assessor should scope the Keycloak realm explicitly |
| V7 Session Management | Yes | Token lifetimes are IdP-configured |
| V8 Authorization | Yes — **the highest-value chapter here** | See §5 for the one known gap |
| V9 Self-contained Tokens | Yes | JWT validated per request |
| V10 OAuth and OIDC | Yes — **largely delegated** | Authorization-Code + PKCE, public client |
| V11 Cryptography | Yes, with a stated L3 skip | No HSM — see §5 |
| V12 Secure Communication | Yes — **known gaps, see §5** | |
| V13 Configuration | Yes | |
| V14 Data Protection | Yes | |
| V15 Secure Coding and Architecture | Yes | |
| V16 Security Logging and Error Handling | Yes | Hash-linked immutable audit chain |
| V17 WebRTC | **No** | ACMP runs no WebRTC media. Webex (Phase 2) is an external conferencing system reached through an API adapter, never embedded. **If Phase 2 embeds any media client, this exclusion expires.** |

---

## 4. Chapter → control map

72 controls are defined in `docs/domain/security-controls.md`. The mapping below is the union of that
document's two independent keys. **Controls are listed where the catalogue places them; inclusion here
is not evidence of implementation** — see §5 for what is actually missing or partial.

| Chapter | ACMP controls |
|---|---|
| **V1** Encoding and Sanitization | C-INP-01, C-INP-02, C-INP-03 |
| **V2** Validation and Business Logic | C-API-01…04, C-INP-01…03, C-SUP-03, C-WEB-03 |
| **V3** Web Frontend Security | C-FILE-05, C-INP-01…03, C-SESS-02, C-WEB-01, C-WEB-02 |
| **V4** API and Web Service | C-API-01…04, C-AUTH-03, C-WEB-02, C-WX-01…03 |
| **V5** File Handling | C-FILE-01…05 |
| **V6** Authentication | C-AUTH-01…05 |
| **V7** Session Management | C-SESS-01, C-SESS-02 |
| **V8** Authorization | C-ADM-02, C-API-02, C-AUTH-04, C-AUTH-05, C-AUTHZ-01…05, C-IMM-01…04, C-INS-01…03, C-PRIV-03 |
| **V9** Self-contained Tokens | C-AUTH-01…05 |
| **V10** OAuth and OIDC | C-AUTH-01…05 |
| **V11** Cryptography | C-CRYPTO-01…03, C-IMM-04, C-SEC-01…03 |
| **V12** Secure Communication | C-CRYPTO-01…03, C-WX-01…03 |
| **V13** Configuration | C-ADM-01…03, C-AUDIT-04, C-BAK-01, C-BAK-02, C-CON-001…003, C-SEC-01…03, C-WX-02 |
| **V14** Data Protection | C-AUDIT-07, C-AUTHZ-04, C-CRYPTO-02, C-FILE-01…05, C-NOTIF-01, C-NOTIF-02, C-PRIV-01…03 |
| **V15** Secure Coding and Architecture | C-CON-001…003, C-DAST-01, C-SAST-01, C-SEC-02, C-SUP-01…03, C-WEB-01…03, C-WX-03 |
| **V16** Security Logging and Error Handling | C-ADM-03, C-AUDIT-01…08, C-BAK-02, C-DAST-01, C-IMM-01…04, C-INS-01…03, C-SAST-01, C-WEB-01…03 |
| **V17** WebRTC | — none — |

> `C-AI-01`, `C-AI-02` and `C-AI-03` map to no ASVS chapter by either key. That is correct: they answer
> to the **OWASP LLM Top 10** (`LLM01`), and they are Phase 3 in any case.

### 4.1 Primary evidence anchors

Where an assessor should start reading. **Every path here is asserted to exist by
`scripts/check-asvs-pack-paths.mjs`.** Existence is all that is asserted — not sufficiency.

| Area | Anchor |
|---|---|
| Policy registration, deny-by-default | `src/BuildingBlocks/Acmp.Shared/Authorization/AuthorizationRegistration.cs` |
| Per-request authorization pipeline | `src/BuildingBlocks/Acmp.Shared/Application/Behaviors/AuthorizationBehavior.cs` |
| Confidentiality ABAC (Restricted topics) | `src/BuildingBlocks/Acmp.Shared/Authorization/Abac/ConfidentialityRequirement.cs` |
| Deny-by-default read gate for guests | `src/Acmp.Api/Infrastructure/Authentication/GuestSurfaceMiddleware.cs` |
| Segregation of duties | `tests/Acmp.Application.Tests/Authorization/SegregationOfDutiesTests.cs` |
| Role/permission matrix | `tests/Acmp.Application.Tests/Authorization/PermissionMatrixTests.cs` |
| Audit-log export (role-scoped, audited) | `src/Acmp.Api/Endpoints/AuditEndpoints.cs` |
| Security headers, HSTS, strict CSP | `deploy/nginx/default.conf.template` |
| Idle sign-out | `src/Acmp.Web/src/auth/useIdleSignOut.ts` (test: `src/Acmp.Web/src/auth/useIdleSignOut.test.tsx`) |
| Dependency CVE gate | `scripts/check-vulns.mjs` |
| Per-file coverage gate | `scripts/check-coverage.mjs` |
| SAST / secrets / SBOM / image scanning | `.github/workflows/security.yml`, `.gitleaks.toml` |

> ⚠ **The per-control status/code/test table in `docs/validation/security-controls-audit.md` is
> RICH AND STALE. Read §5.1 before using it.**

---

## 5. Known gaps — the section that makes this pack worth reading

A pack that omits a gap the project's own register already holds is worse than no pack. Everything
below is recorded in the live package; identifiers are given so an assessor can ask for the row.

### 5.1 The existing audit document is stale (`DEF-118`)

`docs/validation/security-controls-audit.md` is the P16 evidence ledger — control, status, code path,
proving test, threat. It is genuinely useful **and it was last touched 2026-07-17**, and `docs/` is a
frozen read-only archive superseded 2026-07-22. Two measured problems:

1. **Its title claims an ASVS mapping it does not contain.** Grepped for `ASVS`, `Chapter`, `Level 2`
   and any V-numbered clause, the only match in the whole document is its own title line. It is
   organized by ACMP control family and by P16 batch. *(The grep is proven to have had a subject,
   because it returned that line.)*
2. **At least one row is now false.** Its `C-AUTHZ-04` row reads *"Deferred-feature — No
   `Confidentiality`/`Restricted` field exists in code"*. That feature shipped: `Confidentiality`
   resolves across ten or more C# files. Two further statements it relies on are also now false — see
   §5.3.

**Treat every status in that document as of 2026-07-17 and re-verify before citing it.**

### 5.2 Authorization — one member of a five-part P1 control never shipped (`DW-093`)

`C-AUTH-05` specifies segregation of duties as **SoD-1…SoD-5**, mapped to **V8**. Measured across
`src/**/*.cs`: `SoD-1`, `SoD-2`, `SoD-3` and `SoD-5` appear 11, 10, 13 and 11 times. **`SoD-4` —
decision recorder ≠ owner/presenter, plus conflict-of-interest exclusion — appears zero times.** The
four non-zero siblings are the control proving the sweep had a subject.

Its only prior record was a `Partial — verify in a later batch` in the frozen archive; no later batch
did, and no register row knew. **The required strength is genuinely unsettled** — the catalogue calls
SoD-1…5 *"hard authZ checks"* while qualifying SoD-2 as *warn+audit* — which is why this is an open
decision rather than a pending fix.

### 5.3 Insider-risk alerting — two signals declined on premises that have since expired

`C-INS-01` names two anomaly signals that were **deliberately not built**, each with a stated reason.
Both reasons are now false, and each is recorded:

| Signal | Stated reason | Status now |
|---|---|---|
| Bulk/atypical export volume | *"ACMP has no export feature"* | **False** — the audit-log export shipped. `DW-087` |
| Atypical `Restricted`-topic access | *"no confidentiality tier exists"* | **False** — Confidentiality ABAC shipped. `DW-092` |

Both alerts remain unbuilt, deliberately: anomaly detection needs a baseline and none exists, so any
threshold set today would be a guess dressed as a control. The underlying events are already recorded,
so the baselines accumulate. `NFR-046`'s alert configuration is separately outstanding (`DW-058`).

### 5.4 Secure communication — internal hops run plaintext (`NFR-019`, `DEF-100`, `DW-074`)

`NFR-019` mandates TLS on internal hops. **Measured in `deploy/docker-compose.yml`, at least four
service-to-service endpoints are plaintext:** `keycloak:8080`, `minio:9000`, `seq:5341`, and nginx→api.

⚠ **Two records each name a different subset and neither is complete** — the archive names API↔MinIO
and API↔Seq; the defect register names app↔Keycloak and nginx↔api. The measured set above supersedes
both. The operator **kept the requirement rather than narrowing it**, so `NFR-019` stays `Approved`
with no criterion and `DEF-100` stays open deliberately. This is not a configuration oversight:
service-to-service TLS needs a certificate story, and the public certbot flow does not extend to
services addressing each other by compose name.

### 5.5 Data protection — retention has a mechanism, no periods, and no legal hold (`OQ-079`, `OQ-080`)

A configuration store for retention settings exists and is an audited, Administrator-only action.
**v1 ships no retention periods and nothing purges**, which is canon rather than an omission — the
period values await legal input (`OQ-079`).

> ⛔ **The trap, for whoever builds Phase 2 enforcement.** The architecture asserts that a legal hold
> overrides any retention or purge, and **no hold mechanism exists**: no flag, no place/release path,
> no audit of either. It is harmless today only because nothing purges. **Build enforcement without it
> and that guarantee becomes false silently**, since nothing enforces it and no test asserts it.
> `OQ-080` records this; answer it *first*, not alongside.

### 5.6 Cryptography and configuration — operator-dependent controls

| Control | State | Why |
|---|---|---|
| Encryption at rest (SQL TDE, object SSE) | **Operator** | Server features, not app config. TDE is deferred for certificate key custody, not capability. Object SSE needs a key server |
| Backup encryption | **Operator** | Edition-gated |
| DB-permission audit immutability | **Partial** | The `DENY UPDATE,DELETE` migration and its test exist, but are **inert until the app runs under a least-privilege login** |
| TLS to SQL Server | **Partial** | Encrypted but **not authenticated** — certificate trust is not enforced, which stops passive interception but not an active MITM |
| Minimum 1 PR approver | **Unsatisfiable** (`DW-081`) | Branch protection is configured and enforced, but the required-approver count is 0 because this is a one-person team and the platform forbids self-approval. **The clause is unsatisfiable, not merely unmet** |
| Container base-image assertion | **Missing** (`DW-090`) | Images are minimal-base and measured, but **no CI check asserts image size or base layer.** A size check alone would pass on the larger base too, reporting compliance with a minimal-base clause from the base that clause excludes |

### 5.7 Not attempted, and stated as such

- **No DAST/ZAP leg.** `C-DAST-01` is scoped but no dynamic scan runs in CI.
- **No HSM** (V11) — explicitly right-sized for ≤20 on-prem users.
- **No step-up re-authentication** per sensitive action (V6/V7 L3 territory).
- **Accessibility, performance and DR** are governed by separate requirements and are out of this
  pack's scope entirely.

---

## 6. What an assessor should ask for

1. **Scope the IdP explicitly.** V6 and V10 are largely delegated to a self-hosted Keycloak realm.
   Assessing ACMP without the realm leaves both chapters half-covered; the realm's export and its
   live reconciliation script are the artifacts to request.
2. **Start at V8.** It is where this system's real threat lives — an insider with legitimate access —
   and where the controls are deepest *and* where the one known unbuilt control sits (§5.2).
3. **Ask for the audit chain's verification path.** Immutability is hash-linked and there is an
   on-demand verify endpoint plus a nightly job.
4. **Treat §5 as the starting list of findings, not the complete one.** It is what the team already
   knows. The value of an assessment is what it adds.

---

## 7. Maintenance

- This document lives under `tamheed-package/docs/` by `DEC-091` d3, because `docs/` is frozen.
- `scripts/check-asvs-pack-paths.mjs` asserts every path cited above still exists, and **fails closed**
  if the pack cites nothing (a checker with no subject must fail, not pass).
- **`NFR-018` does not close from here.** Only an external assessor's report can evidence it.
- When a gap in §5 is closed, update it here **and** close its register row; a gap fixed in one place
  and left standing in the other is how the surviving copy becomes the one the next reader believes.
