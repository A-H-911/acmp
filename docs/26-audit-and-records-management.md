# 26 — Audit & Records Management (Deliverable 34)

**Purpose:** The append-only **AuditEvent** design, what is audited, tamper-evidence and immutability rules, the records lifecycle (keep-all/configurable retention, archival, legal hold), who may read/export audit, audit backup, and the duties-segregation that stops privileged insiders from deleting the record — the evidence layer that makes ACMP's votes and decisions trustworthy.

> Realizes **ADR-0009** (append-only audit; votes and issued decisions immutable). Entity fields from `11-domain-model.md` (AuditEvent) and `16-data-architecture-and-model.md` §2.14 **verbatim**; immutability/SoD policy from `10-permission-role-matrix.md` §E; controls from `25-security-controls.md` (C-AUDIT-*, C-IMM-*, C-INS-*, C-BAK-*). Retention canon: **keep everything, configurable, no auto-purge in v1** (`../README.md` §A, NFR-059/060). `[unverified]` marks design options not yet org-confirmed.

---

## 1. AuditEvent — append-only design

`audit.AuditEvent` is the **immutable system of record** for every consequential action and the **source of status history** (`16` §1.7 — no separate `StatusHistory` table; the audit log *is* the history).

### 1.1 Fields

| Field | Type | Meaning | Notes |
|---|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` (or monotonic `BIGINT IDENTITY`) | Event identity / order | Monotonic ordering matters for the hash-chain (§3); a `BIGINT IDENTITY` sequence or sequential-GUID gives stable order. |
| `OccurredAt` | `DATETIMEOFFSET` (UTC) | **Server-set** event time | Never client-supplied (defeats back-dating — `25` C-AUDIT-03). Gregorian/UTC (`../README.md` §A). |
| `ActorUserId` | `Guid?` | Who acted | = validated OIDC `sub`→User; `null` only for system/job actors. **Never** from request body. |
| `ActorRole` | `enum?` | Role exercised | The role under which the action was authorized (RBAC claim). |
| `Action` | `NVARCHAR` | What happened | Dotted verb, e.g. `Decision.Issued`, `Vote.Closed`, `Vote.BallotCast`, `Auth.Denied`, `Transcript.Read`, `Role.Granted`, `Export.Performed`. |
| `SubjectType` | `enum (ArtifactType)` | Kind of subject | From the closed `ArtifactType` enum (`16` §4). |
| `SubjectId` | `Guid` | Which subject | **Soft polymorphic reference — no FK** (`16` §1.9) so the row survives subject changes and crosses module boundaries. |
| `Outcome` | `enum{Success,Denied,Failure}` | Result | `Denied` captures authZ denials (`25` C-AUDIT-06). |
| `Before` | `NVARCHAR(JSON)?` | Prior state snapshot | For state changes; omit/redact sensitive fields per privacy (`25` C-PRIV-01). |
| `After` | `NVARCHAR(JSON)?` | New state / event payload | For reads (e.g. transcript access) this is the access descriptor, not content. |
| `CorrelationId` | `NVARCHAR` | Request/trace correlation | Ties the event to the OTel trace + Serilog logs (NFR-044). |
| `IpOrSource` | `NVARCHAR?` | Origin | Client IP / source channel; pseudonymous, not PII-heavy. |
| `PrevHash` | `NVARCHAR?` | Hash of the previous row (chain) | Optional tamper-evidence (§3); `null` until enabled. |
| `RowHash` | `NVARCHAR?` | Hash over `(PrevHash ‖ canonical(this row))` | Optional; recommended for vote/decision-scoped chains. |

### 1.2 Append-only enforcement (defence-in-depth)

Insert-only is enforced at **three** layers so a single bypass does not defeat it:

1. **Application:** no UPDATE/DELETE code path or ORM mapping touches `AuditEvent` (NFR-040); only an internal `IAuditRecorder.Append(...)` exists. No write/delete API is exposed to any role (`25` C-AUDIT-01).
2. **Transaction:** every state transition appends its audit row **in the same DB transaction** as the change (NFR-042) — atomic; you cannot mutate state without leaving a trace, and a failed audit append rolls back the action.
3. **Database permission:** the application's SQL principal is **granted INSERT + SELECT only on `audit.*`** — **no UPDATE, no DELETE** (`25` C-AUDIT-04). DBA-level deletion is itself an out-of-band, controlled, alertable event (§7).

> Append throughput target ≥100 events/s sustained (NFR-012) — comfortably within one SQL instance at this scale.

---

## 2. What is audited

| Category | Examples | Importance | Control |
|---|---|---|---|
| **All state changes on governed entities** | Topic transitions; agenda publish; meeting schedule/start/cancel; `Vote` open/close + **each ballot cast**; `Decision` issue/supersede + chair approve/override; ADR approve/supersede; MoM approve/publish/version; Risk accept/escalate/close; Action complete/verify; Invariant activate/retire; Dependency/Relationship create/deactivate. | High for votes/decisions/ADRs/MoM/role-grants; normal otherwise. | C-AUDIT-02 |
| **Authentication & authorization events** | Login (via IdP context), logout/SLO, token-validation failures, **authZ denials** (`Outcome=Denied`), delegation grant/expiry. | High (probing/escalation signal). | C-AUDIT-06 |
| **Reads of sensitive items** | **Every transcript/recording read** (NFR-025); reads of `Restricted` topics; sensitive exports. (Routine reads of non-sensitive items are **not** audited — proportional to scale, avoids noise.) | High. | C-AUDIT-07 |
| **Privileged/administrative actions** | Role grant/revoke, user provision/suspend/deactivate, config change, **retention/immutability config change**, template changes. | High. | C-AUDIT-05 |
| **Exports** | Report/data export: actor, scope, volume. | High (exfiltration signal). | C-AUDIT-08 |
| **AI-candidate lifecycle (Phase 3)** | Candidate extraction created, human approve/reject of a candidate (no auto-commit). | Normal. | C-AI-02 |

**Privacy in audit.** Snapshots minimize PII (`25` C-PRIV-01): store ids and changed-field deltas, not full names/emails/secret values; vote *content* is recorded (attributed voting is in scope, ADR-0010/NFR-029) but treated as sensitive and access-controlled (§6). Pre-signed URLs/tokens never enter `Before`/`After` (`25` C-PRIV-02).

---

## 3. Tamper-evidence

Append-only + DB-permission segregation makes the audit log **hard to alter in place**. For the **highest-stakes records** (votes, issued decisions, the audit log itself) we add **detection** so that even a DBA/host-level alteration is *evident*. This is the design decision behind **OQ-DATA-002**.

| Option | Mechanism | Strength | Cost | Recommendation |
|---|---|---|---|---|
| **(a) Insert-only + DB permissions** (baseline, **always on**) | No UPDATE/DELETE path (app) + no UPDATE/DELETE grant (DB) + same-tx append. | Prevents in-band tampering by any application role and the app's own SQL account. | None beyond design. | **Adopt for all audit.** Necessary, not sufficient against a DBA/host compromise. |
| **(b) Hash-chain** `PrevHash`/`RowHash` (**recommended for votes & decisions**) | Each new row stores `RowHash = H(PrevHash ‖ canonical(row))`; a verifier recomputes the chain and detects any insert/edit/delete/reorder. Optionally a **per-aggregate sub-chain** for a vote/decision so a record carries its own integrity proof. | Makes silent tampering **detectable** (not preventable) even at DB/host level. Non-repudiation for A1/A2/A3. | Modest: a hashing step on append + a periodic verification job (`25` C-INS-02). Deterministic canonical serialization required. | **Recommend ON for votes/decisions** (and the audit chain overall) in v1. Size/throughput is trivial here. `[unverified — final algorithm/canonicalization chosen at build]` |
| **(c) External notarization / signed anchor** | Periodically sign the chain head (or anchor a digest off-host / to a WORM store). | Strong external proof. | Higher ops; key management. | **[L3-skip] / defer.** Disproportionate for ≤20-user on-prem; revisit only if an external auditor mandates it. Captured as a future option, not v1. |

**Recommendation (OQ-DATA-002):** **(a) for everything + (b) hash-chain enabled for votes, issued decisions, and the audit log**; (c) deferred. Verification: a scheduled Hangfire job recomputes the chain and a Seq alert fires on any mismatch or gap (`25` C-INS-02, NFR-046).

---

## 4. Immutability rules (votes / decisions / ADRs / published minutes)

The **superseded-not-edited** principle (ADR-0009, `10` §E.5, `16` §1.6). No role — **including Administrator and Chairman** — may bypass these; a violating Allow is rejected and audited (`25` C-IMM-03).

| Record | Freeze event | After freeze | Correction mechanism | Enforcement |
|---|---|---|---|---|
| **Vote** (ballots + tally) | `Closed` | No field mutation; ballots/tally frozen | A **new vote**, recorded as such; old vote retained | 409 on mutation (NFR-041); no DB UPDATE grant on closed rows; `25` C-IMM-01 |
| **Decision** (`DECN-…`) | `Issued` | No edit of outcome/rationale/`IssuedAt` | **Supersede** by a new `Decision` (`SupersededByDecisionId`) | 409 (NFR-041); `25` C-IMM-02 |
| **ADR** (in-app `ADR-…`) | `Approved` | No edit of decision text/consequences | **Supersede / Deprecate** by a new ADR | `25` C-IMM-02 |
| **Minutes (MoM)** (`MIN-…`) | `Published` | No edit | **New version** supersedes prior (`Version++`, `Superseded`) | `25` C-IMM-02 |
| **Architecture Invariant** (`AIV-…`) | `Active` | Statement not edited in place | **Supersede / Retire** by a new invariant | analogous to ADR |
| **AuditEvent / ProgressUpdate** | on insert | Never updated/deleted | Append a corrective event | §1.2; `25` C-AUDIT-01 |

Everything else is mutable under normal authZ (with full mutation audit); **hard-delete is prohibited for all governance records** — they are archived/superseded, never deleted (`16` §1.6). User-discardable content (`Comment`, `Document` archive, `Attachment`) uses **soft-delete** preserved for audit.

---

## 5. Records lifecycle — retention, archival, legal hold

**Canonical posture (settled):** **keep everything; retention configurable; no automatic purge in v1** (`../README.md` §A; NFR-059/060). No background deletion/archival job that removes data exists in v1; the Configuration table (`16` §2.15) holds retention settings for legal/compliance to set **later** — and even then, immutable records (votes, decisions, ADRs, MoM, audit) are protected from purge.

| Stage | v1 behaviour | Future (configurable) |
|---|---|---|
| **Active** | Records live and queryable. | — |
| **Closed/Archived** | Closed topics, ended memberships, superseded ADRs/decisions/MoM **stay queryable** — archival is a *status/visibility* change, **not** deletion. No purge. | Optional archive tier (still no delete of immutable classes). |
| **Retention window** | **Indefinite** (no auto-purge). Periods configurable but unset in v1 (`[unverified]` org/legal to define — NFR-059). | Legal sets per-class periods; purge jobs (if ever built) **must exclude** immutable classes + audit. |
| **Legal hold** | Any record (or a meeting/topic and its graph) can be flagged **on legal hold**; a hold **overrides any future retention/purge** — held records cannot be deleted/superseded-away regardless of policy. `[unverified — confirm hold workflow with legal]` | Hold lifecycle (place/release) is itself an audited privileged action. |
| **Deletion** | **Hard-delete prohibited** for governance records; soft-delete only for user-discardable content, preserved for audit (`16` §1.6). | Unchanged for immutable classes. |

> **Right-sizing.** At ≤20 users / ~200–500 topics/yr / low-thousands of artifacts by year 3 (NFR-008/009), keep-all is operationally trivial; building a purge engine now would add risk (accidental loss) for no benefit. Defer until legal defines a need.

---

## 6. Who may read audit; audit export for compliance

| Capability | Roles | Policy | Notes |
|---|---|---|---|
| **Read audit log** | **Auditor**, Chairman, Secretary | `Audit.Read` (`10` row 29) | **Auditor is read-only oversight** across the system incl. the audit log; never mutates governance data. |
| **Export audit / compliance reports** | Auditor, Chairman, Secretary | `Report.Export` (`10` row 30) + `Audit.Read` | Export is itself **audited** (`25` C-AUDIT-08) and anomaly-alertable (`25` C-INS-01). |
| **Write / delete audit** | **None** | — | No write API beyond the internal recorder; **no delete for any role**, including Administrator (`25` C-AUDIT-01/04). |

**Auditor read view** honours privacy: attributed vote content and PII deltas are visible to the Auditor/Chairman for oversight (NFR-029) but the export redacts secrets/URLs and is access-controlled. The Auditor cannot reach `Restricted`-topic *content* beyond what audit metadata reveals unless separately granted — audit visibility ≠ content visibility.

---

## 7. Audit backup & segregation of duties

- **Backup:** the `audit` schema is included in the **nightly SQL backup to the standby VM/storage** (NFR-056/058), **encrypted** (`25` C-CRYPTO-03), with an **offline/immutable copy recommended** so a privileged insider who tampers with live data cannot reach the backup → the chain (§3) can be re-verified and the record reconstructed (`25` C-BAK-02).
- **Segregation — admins cannot delete audit:** the application SQL account holds **no DELETE/UPDATE on `audit.*`**; **`Administrator` has no audit-delete capability** in-app (SoD-5, `25` C-INS-03); any DBA/host-level deletion is out-of-band, requires `C-ADM-01` controls, and is **detectable** via the hash-chain + Seq alert on chain gaps (`25` C-INS-02). This closes the loop on abuse case **AB-5** (audit-log tampering).
- **Integrity monitoring:** a scheduled verification job recomputes the hash-chain and asserts no gap/mismatch; **any audit-write failure or chain break raises a Seq alert** within 60s (NFR-046, `25` C-INS-02).

---

## 8. Record-class summary

| Record class | Immutable? | Retention (v1 = keep) | Who can read | Export |
|---|---|---|---|---|
| **Vote** (ballots/tally) | **Yes — after `Closed`** (hash-chained) | Keep (indefinite); legal-hold capable | Members/Chair (results); Auditor/Chair (attributed detail) | `Report.Export` (scoped); audited |
| **Decision** (`DECN-…`) | **Yes — after `Issued`** (supersede-only; hash-chained) | Keep | Per stream-scope/confidentiality; Auditor (read) | `Report.Export`; audited |
| **ADR** (`ADR-…`) | **Yes — after `Approved`** (supersede/deprecate) | Keep | Committee-wide read (per scope) | `Report.Export` |
| **Minutes (MoM)** (`MIN-…`) | **Yes — after `Published`** (new version) | Keep | Committee-wide (per scope); Auditor | `Report.Export` |
| **Architecture Invariant** (`AIV-…`) | **Yes — after `Active`** (supersede/retire) | Keep | Committee-wide read | `Report.Export` |
| **AuditEvent** | **Yes — append-only** (no update/delete; hash-chained) | **Longest class**; never purged in policy window | **Auditor**, Chairman, Secretary | `Audit.Read` + `Report.Export`; audited |
| **Topic** | No (mutable pre-decision; archived after close) | Keep (archived, not deleted) | Per stream-scope/confidentiality | `Report.Export` (scoped) |
| **Action / Risk / Dependency** | No (mutable) | Keep | Per scope | `Report.Export` (scoped) |
| **Recording / Transcript** | No (metadata mutable; content fixed) | Keep; configurable media retention; **legal-hold capable**; PII-sensitive | **Chair/Coord/Auditor only** (NFR-025); **every read audited** | Access via pre-signed ≤1h URL; export tightly controlled + audited |
| **ProgressUpdate** | **Yes — append-only** | Keep (with parent) | Per parent scope | with parent |
| **Relationship** (trace edge) | Append-then-deactivate (no hard-delete) | Keep (graph integrity) | Per scope | with subject |
| **Comment / Attachment** | No (soft-delete, preserved) | Keep (soft-deleted retained for audit) | Per subject scope | with subject |
| **Notification** | No | Keep; configurable; no purge v1 | Recipient (own) | n/a |

---

## 9. New open-question candidates raised here

| ID | Item | Default |
|---|---|---|
| **OQ-DATA-002** *(resolved-here recommendation)* | Tamper-evidence strength for critical records: insert-only+DB-perms only, vs **+ hash-chain**, vs + external notarization. | **Insert-only + DB-perms everywhere; hash-chain ON for votes/decisions/audit; external notarization deferred** (§3). Confirm with org/audit. |
| **OQ-DATA-003** | **Legal-hold workflow** (who places/releases, granularity: per-record vs per-meeting/topic-graph) and its interaction with future retention. | Per-record + per-meeting-graph hold; place/release is an audited privileged act; overrides purge. Confirm with legal. |
| **OQ-DATA-004** | Future **retention periods per record class** once legal defines them; confirm immutable classes (votes/decisions/ADRs/MoM/audit) are **always exempt from purge** and that any purge job excludes held records. | Indefinite in v1; immutable classes never purged; revisit when legal sets periods (NFR-059). |

---

## Traceability
Implements **Deliverable 34**. Realizes **ADR-0009** (append-only audit; vote/decision immutability). AuditEvent fields/storage from `11-domain-model.md` (Audit & Records) and `16-data-architecture-and-model.md` §2.14/§1.7/§1.8/§1.9. Immutability + read/export authorization from `10-permission-role-matrix.md` §C (rows 29/30) and §E.5 (immutability guard, SoD-5). Controls realized in `25-security-controls.md` — C-AUDIT-01..08 (audit), C-IMM-01..04 (immutability + hash-chain), C-INS-01..03 (insider detection/integrity), C-BAK-01/02 (backup), C-PRIV-01/02 (privacy), C-CRYPTO-03 (backup encryption); threats `T-04/05/06/09/10/21` and abuse cases `AB-1/2/5/6` from `24-security-threat-model.md`. NFRs: NFR-012 (throughput), NFR-025 (transcript read auditing), NFR-040/041/042 (append-only/immutability/same-tx), NFR-046 (alerting), NFR-056/058 (backup), NFR-059/060 (retention keep-all/configurable), NFR-029 (attributed voting). Retention canon: `../README.md` §A. New: OQ-DATA-002 (recommendation here), OQ-DATA-003/004 (for `42-open-decisions.md`).
