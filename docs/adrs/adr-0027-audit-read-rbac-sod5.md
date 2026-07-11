# ADR-0027: Audit-Read Excludes Administrator — Segregation of Duties (supersedes the role clause of FR-151/FR-153)

- Status: Proposed
- Date: 2026-07-11
- Deciders: Architecture Committee execution (secretary to ratify)
- Supersedes: the audit-read **role clause** of FR-151 ("visible to Auditor and Administrator"), FR-153 ("Auditor and Administrator roles shall be able to search the audit log"), and the corresponding wording of AC-017/AC-019 (brief §6.18). Field/immutability/search requirements of those FRs are unchanged.

## Context and Problem Statement

The package contradicts itself on **who may read the audit log**:

- **Must-priority `FR-151`/`FR-153`** and the original brief §6.18 — and consequently `AC-017` ("visible to Auditor and Administrator") and `AC-019` ("the Auditor or Administrator runs the integrity check") — name **Auditor + Administrator**.
- The **permission-role matrix §C row 29**, **`docs/domain/audit-and-records.md §6/§7`**, the **SoD-5** control (`docs/domain/security-controls.md` C-INS-03), and the **already-built backend policy** `Policies.AuditRead = {Chairman, Secretary, Auditor}` (verified: `AuthorizationRegistration.cs:52`) **exclude Administrator** and instead grant Chairman + Secretary.

The frontend is a third, wrong, variant: it gates `/audit` to `{administrator, auditor, chairman}` (verified: `App.tsx:93`, `navModel.ts:63`) — including Administrator, omitting Secretary. Building the Audit slice forces a single answer; per the governance rule, code/package disagreement is resolved by an ADR, not left to drift.

## Decision Drivers

- **Segregation of duties (SoD-5).** The Administrator manages the system (users, config, jobs) and holds no committee-content capability. Letting the operator of the platform also read the tamper-evident governance record weakens the very separation the audit log exists to protect (abuse case AB-5). `audit-and-records.md §7` is explicit: "**`Administrator` has no audit-delete capability**"; the read exclusion is the same principle.
- **Auditor is the oversight role.** `Audit.Read` already unifies read-only oversight (Auditor) with the two governance officers accountable for the record (Chairman, Secretary) — a coherent, least-privilege set.
- **The FR wording predates SoD-5.** `FR-151/153` inherit the brief's informal "Auditor and Administrator"; the matrix + SoD-5 are the later, considered refinement. One must supersede the other; SoD-5 is the stronger governance position.

## Considered Options

1. **Exclude Administrator; audit read = {Auditor, Chairman, Secretary}** (this ADR) — align to the matrix + SoD-5; supersede the FR role clause; fix the frontend.
2. **Honor FR-153 literally; add Administrator** — change `Policies.AuditRead` + the permission-matrix tests + `audit-and-records.md §6`; relax SoD-5 for read. Rejected: weakens segregation of duties for the record's own operator; contradicts three settled design artifacts.
3. **Auditor + Administrator only (drop Chairman/Secretary)** — literal to the AC pair. Rejected: contradicts the matrix on both ends (removes the officers, adds the admin).

## Decision Outcome

Chosen: **Option 1.** Audit read (and the on-demand integrity check) = **{Auditor, Chairman, Secretary}**; **Administrator is excluded** from reading, searching, exporting, and deleting the audit log.

- The backend `Policies.AuditRead` is already correct — no change.
- The frontend is corrected: `App.tsx` `RequireRole` → `['auditor','chairman','secretary']`; `navModel.ts` `ACCESS.audit = {auditor:'full', chairman:'full', secretary:'full'}` (drop `administrator`, add `secretary`).
- `AC-017/019/020` wording is reconciled to the `Audit.Read` set; the traceability matrix `FR-153→AC-020` note records this supersession; `FR-151/153` carry a pointer to this ADR.

### Consequences

- Good: one consistent, least-privilege audit-read set across policy, matrix, ACs, and UI; SoD-5 preserved; the record's operator cannot silently inspect it.
- Trade-off: a Must-priority FR's role clause is superseded — recorded explicitly here and in the traceability matrix so the deviation is governed, not silent. An Administrator who needs an audit answer requests it from an Auditor/officer (proportional at ≤20 users).

## Validation

- API test: Auditor / Chairman / Secretary → 200 on `/api/audit` and `/api/audit/verify`; **Administrator** / Member / Submitter → 403; unauthenticated → 401.
- The existing `PermissionMatrixTests` continue to encode `Audit.Read = {Chairman, Secretary, Auditor}` (no change needed — confirms alignment).
- Frontend test: the `/audit` route + nav item render for auditor/chairman/secretary and show `PermissionDenied` for administrator/member.

## Links / Notes

- Supersedes the role clause of `FR-151/FR-153` (brief §6.18); realizes `permission-role-matrix.md §C row 29`, `audit-and-records.md §6/§7`, SoD-5 (C-INS-03). Paired with ADR-0026 (audit enrichment/atomicity).
