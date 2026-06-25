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
>
> P2 update (2026-06-25): reference module pattern verified (build clean, 7/7 tests). Still a pattern/
> foundation phase — no feature AC flips. The Membership domain capability behind AC-058 (deactivate keeps
> attribution) and AC-059 (directory readable by all roles) exists with unit tests, but both criteria require
> HTTP + authorization + UI, which land in P4 — so they remain `Pending`. See progress-log P2 entry.
>
> P3 update (2026-06-25): frontend foundation (app shell, role-filtered nav, OIDC wiring, design system,
> states, dnd). First phase to move localization/a11y ACs: **AC-040, AC-042, AC-045, AC-046 → Met** (Vitest +
> live axe render across EN/AR × light/dark, 0 violations), **AC-041 → Partial** (RTL confirmed by hand;
> automated VR → P17). AC-039 (locale switch preserves form data) stays `Pending` — no form in the shell yet
> (P5+). AC-043/044 (keyboard DnD alternative for backlog/agenda) stay `Pending`: the shared keyboard-accessible
> `SortableList` is built + tested in P3 but isn't wired into those screens until P5/P6. AC-001/005/006/008
> (Keycloak login, RBAC 403) stay `Pending` → P4 (no Keycloak container; server enforcement is P4). Nav/route
> gating in P3 hides UI only — it is not authorization. See progress-log P3 entry.

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
| AC-040 | Localization | Met | i18n/direction.test.ts + axe render | dir=rtl mirrored layout — sidebar→inline-end, Arabic font, logical CSS; verified live (P3) |
| AC-041 | Localization | Partial | manual render (Playwright) | RTL render confirmed clean by hand; automated visual-regression suite → P17 |
| AC-042 | Localization | Met | theme/theme.test.ts | Theme persisted via localStorage + applied as data-theme |
| AC-043 | Accessibility | Pending | — | Keyboard DnD alt (backlog) |
| AC-044 | Accessibility | Pending | — | Keyboard DnD alt (agenda) |
| AC-045 | Accessibility | Met | axe (WCAG 2.2 AA) render | Global :focus-visible (2px solid --focus, offset) — axe-clean EN/AR×light/dark (P3) |
| AC-046 | Accessibility | Met | axe (WCAG 2.2 AA) render | Labels/aria/contrast/reading order — axe 0 violations across EN/AR×light/dark; landmarks verified (P3) |
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

**Summary:** 66 ACs · 4 Met (AC-040/042/045/046) · 1 Partial (AC-041) · 61 Pending. (P3 frontend foundation.)
