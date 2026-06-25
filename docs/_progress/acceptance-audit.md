---
artifact: acceptance-audit
status: active
version: v1
updated: 2026-06-25
---

# ACMP Acceptance Audit

Every `AC-###` from `docs/40-acceptance-criteria.md` → verdict. Keystone gate **G-PROGRESS**.
A requirement is not "done" until its AC is `Met` and traces to ≥1 test (gate **G-TRACE**).

**Verdicts:** `Met` · `Partial` · `Not-met` · `Pending` (not yet implemented).

> Status at PH-0: all PH-1 acceptance criteria are `Pending` — no governance features built yet.
> The P1 scaffold delivers infrastructure only (no business features), so no AC flips to `Met` here.

| AC | Section | Verdict | Test ref | Notes |
|---|---|---|---|---|
| AC-001 | Auth & Identity | Pending | — | Keycloak SSO login |
| AC-002 | Auth & Identity | Pending | — | Role from claim → Secretary |
| AC-003 | Auth & Identity | Pending | — | No-claim → deny/Submitter + AuthEvent |
| AC-004 | Auth & Identity | Pending | — | Idle timeout re-auth, no data loss |
| AC-005 | RBAC | Pending | — | Submitter nav hidden + 403 |
| AC-006 | RBAC | Pending | — | Auditor read-only + 403 |
| AC-007 | RBAC | Pending | — | Admin ≠ committee-content (SoD-5) |
| AC-008 | RBAC | Pending | — | No token → 401 |
| AC-009 | ABAC | Pending | — | Owner edit allowed, non-owner 403 |
| AC-010 | ABAC | Pending | — | Stream scope → 403 |
| AC-011 | ABAC | Pending | — | Presenter scoped to topic+meeting |
| AC-012 | SoD-1 | Pending | — | Verifier ≠ owner (negative) |
| AC-013 | SoD-1 | Pending | — | Verifier ≠ owner (positive) |
| AC-014 | SoD-2 | Pending | — | MoM approver = sole author → flagged |
| AC-015 | SoD-3 | Pending | — | Chairman not sole vote-counter |
| AC-016 | SoD-3 | Pending | — | Chairman override w/ co-attestation |
| AC-017 | Audit | Pending | — | State change → audit entry |
| AC-018 | Audit | Pending | — | Audit row immutable |
| AC-019 | Audit | Pending | — | Hash-chain integrity check |
| AC-020 | Audit | Pending | — | Auditor search; others 403 |
| AC-021 | Voting | Pending | — | Vote config locked on open |
| AC-022 | Voting | Pending | — | No double-vote |
| AC-023 | Voting | Pending | — | Attributed ballots visible |
| AC-024 | Voting | Pending | — | Quorum gate on close |
| AC-025 | Voting | Pending | — | Immutable after close |
| AC-026 | Voting | Pending | — | Forward-only lifecycle |
| AC-027 | Decisions | Pending | — | Issued decision immutable |
| AC-028 | Decisions | Pending | — | Supersession back-link |
| AC-029 | Decisions | Pending | — | Downstream link required to issue |
| AC-030 | Topic lifecycle | Pending | — | Required-field validation, localized |
| AC-031 | Topic lifecycle | Pending | — | Reject needs rationale |
| AC-032 | Topic lifecycle | Pending | — | Reject → immutable event + notify |
| AC-033 | Topic lifecycle | Pending | — | Rejection event immutable |
| AC-034 | Topic lifecycle | Pending | — | Post-accept edit locked to Secretary |
| AC-035 | Topic lifecycle | Pending | — | Prepared transition + audit |
| AC-036 | MoM | Pending | — | Published MoM → versioned supersede |
| AC-037 | MoM | Pending | — | Change-request → back to Draft |
| AC-038 | MoM | Pending | — | Approve → Published + notify |
| AC-039 | Localization | Pending | — | Locale switch preserves form data |
| AC-040 | Localization | Pending | — | dir=rtl mirrored layout |
| AC-041 | Localization | Pending | — | RTL visual regression clean |
| AC-042 | Localization | Pending | — | Theme persisted across sessions |
| AC-043 | Accessibility | Pending | — | Keyboard DnD alt (backlog) |
| AC-044 | Accessibility | Pending | — | Keyboard DnD alt (agenda) |
| AC-045 | Accessibility | Pending | — | Visible focus ≥3:1 |
| AC-046 | Accessibility | Pending | — | Labels, aria, contrast, reading order |
| AC-047 | Unsaved-work | Pending | — | Route-change guard |
| AC-048 | Unsaved-work | Pending | — | beforeunload dialog |
| AC-049 | File upload | Pending | — | Size/MIME rejection, localized |
| AC-050 | File upload | Pending | — | Valid upload → MinIO + audit |
| AC-051 | Notifications | Pending | — | Agenda publish → in-app ≤5s |
| AC-052 | Notifications | Pending | — | Vote-open notification deep link |
| AC-053 | Notifications | Pending | — | In-app only, no email/Webex |
| AC-054 | Background jobs | Pending | — | Due-date reminder |
| AC-055 | Background jobs | Pending | — | Overdue escalation |
| AC-056 | Background jobs | Pending | — | Hangfire dashboard for Admin |
| AC-057 | Aging | Pending | — | SLA aging badge + notify |
| AC-058 | Membership | Pending | — | Deactivate keeps historical attribution |
| AC-059 | Membership | Pending | — | Member directory readable by all roles |
| AC-060 | Search & Trace | Pending | — | Global search grouped results |
| AC-061 | Search & Trace | Pending | — | Arabic search via word-breaker |
| AC-062 | Search & Trace | Pending | — | Traceability panel up/downstream |
| AC-063 | Search & Trace | Pending | — | Typed edge creation audited |
| AC-064 | Dashboards | Pending | — | Committee dashboard live data |
| AC-065 | Dashboards | Pending | — | Secretary dashboard |
| AC-066 | Dashboards | Pending | — | Chairman dashboard |

**Summary:** 66 ACs · 0 Met · 66 Pending (PH-0 baseline).
