# 40 — Acceptance Criteria (Deliverable 48)

**Purpose:** Authoritative, testable `AC-###` criteria for PH-1 MVP requirements, written in Given/When/Then form. Covers the hard governance rules: authorization (RBAC + ABAC, least privilege, SoD), audit immutability, voting integrity, MoM approval/versioning, topic lifecycle guards, EN/AR + RTL, accessibility, in-app notifications, unsaved-work guard, and file upload validation. Every Must-priority FR and its US-### has ≥1 AC here (Keystone gate G-TRACE).

**Format:** Each AC references the FR-ID(s) and US-ID(s) it validates. ACs for cross-cutting concerns (authorization, audit, localization, accessibility) are grouped in dedicated sections and referenced by the relevant US/FR.

---

## Section 1 — Authentication & Identity (FR-001, FR-002, FR-018)

**AC-001** (US-001, FR-001)
- **Given** a user who has a valid Keycloak account
- **When** they navigate to ACMP and click "Sign In"
- **Then** they are redirected to the Keycloak authorization endpoint, authenticate there, and are redirected back to ACMP with an authenticated session; no ACMP-managed password is used or stored.

**AC-002** (US-002, FR-002, FR-018)
- **Given** a user whose Keycloak token contains a group/realm-role claim mapping to the ACMP `Secretary` role
- **When** they complete SSO login
- **Then** the ACMP session assigns the `Secretary` role and all Secretary-authorized actions are available; no manual role assignment in ACMP is required.

**AC-003** (US-002, FR-018)
- **Given** a user whose Keycloak token contains no ACMP role claim
- **When** they complete SSO login
- **Then** the system denies access or assigns the minimum default role (`Submitter`) per the configured default; an `AuthEvent` is emitted to the audit log.

**AC-004** (US-003, FR-001, FR-013)
- **Given** an authenticated user who has been idle beyond the configured session timeout
- **When** they attempt any protected action
- **Then** they are redirected to Keycloak for re-authentication and their session is terminated; no data is lost from any in-progress form that was auto-saved.

---

## Section 2 — Authorization: Role-Based Access Control (FR-018, FR-024)

**AC-005** (US-004, FR-024)
- **Given** a user with `Submitter` role
- **When** they view the application
- **Then** navigation items for triage, agenda publishing, meeting scheduling, vote management, decision recording, and chairman approval are not rendered in the UI; any direct API call to those endpoints returns HTTP 403.

**AC-006** (US-004, FR-024)
- **Given** a user with `Auditor` role
- **When** they attempt to submit a form that creates or mutates a topic, decision, vote, or action
- **Then** the submit button is not rendered in the UI, and any direct POST/PUT/DELETE API call returns HTTP 403 with an audit event emitted.

**AC-007** (FR-018)
- **Given** an `Administrator` user
- **When** they attempt to vote on a topic, approve a decision, or close a vote via the API
- **Then** the system returns HTTP 403; the administrator's platform-admin authority never extends to committee-content actions (SoD-5).

**AC-008** (FR-024)
- **Given** any request to a protected endpoint
- **When** no valid session token is present
- **Then** the response is HTTP 401; no protected data is returned.

---

## Section 3 — Authorization: ABAC Stream Scope & Ownership (FR-019, FR-022)

**AC-009** (US-015, FR-022)
- **Given** a `Member` who is assigned as `Owner` on topic TOP-2026-005
- **When** they submit an edit to that topic's description
- **Then** the edit is accepted (AiO cell allows); when the same Member submits an edit to TOP-2026-006 (where they are not Owner), the API returns HTTP 403.

**AC-010** (FR-019)
- **Given** a `Member` whose stream assignment is "Stream-A"
- **When** they attempt to create an action on a topic whose `AffectedStreams` is exclusively "Stream-B"
- **Then** the API returns HTTP 403 (stream scope requirement); the attempt is emitted to the audit log.

**AC-011** (FR-022)
- **Given** a `Guest/Presenter` assigned as Presenter on topic TOP-2026-007 for meeting MTG-2026-003
- **When** they attempt to access topic TOP-2026-008 (different topic) or any action outside the specific meeting scope
- **Then** the system returns HTTP 403; the presenter relationship is scoped only to the assigned topic and meeting window.

---

## Section 4 — Segregation of Duties (FR-085, FR-054, FR-074, FR-075)

**AC-012** (US-062, FR-085) — SoD-1: Action verifier ≠ owner
- **Given** user Alice is the owner of action ACT-2026-010 and has marked it Completed
- **When** Alice attempts to mark ACT-2026-010 as Verified via the UI or API
- **Then** the system returns an error ("Verifier cannot be the action owner"); the action remains in Completed status; the denied attempt is recorded in the audit log.

**AC-013** (US-062, FR-085) — SoD-1 positive case
- **Given** user Bob (Secretary) who is not the owner of action ACT-2026-010
- **When** Bob marks ACT-2026-010 as Verified
- **Then** the action transitions to Verified status; the verification actor and timestamp are recorded; an `ActionVerified` audit event is emitted.

**AC-014** (US-042, FR-054) — SoD-2: MoM approver ≠ sole author (soft SoD)
- **Given** Secretary Carol is both the sole author and the approver of MoM MIN-2026-002
- **When** Carol approves it
- **Then** the system allows the approval but emits a flagged audit event (`MinutesApprovedBySoleAuthor`) and displays a warning indicator in the admin UI; the MoM transitions to Approved.

**AC-015** (US-048, FR-074, FR-075) — SoD-3: Chairman cannot be sole vote-counter
- **Given** Chairman Dave attempts to close vote VOTE-2026-004 and simultaneously record the chairman override on the same decision without a Secretary or second Member co-attesting the tally
- **When** the close action is submitted
- **Then** the system blocks the close and returns an error requiring co-attestation; the vote remains Open.

**AC-016** (US-048, FR-074) — SoD-3 positive: chairman records override with co-attestation
- **Given** Secretary Eva co-attests the tally for vote VOTE-2026-004, and Chairman Dave records an Override decision
- **When** the override is submitted
- **Then** the decision records Dave's override choice, the mandatory justification, Eva's co-attestation, and the timestamp; the event is `DecisionIssued` with `overrideFlag=true`; the audit entry is immutable.

---

## Section 5 — Audit Immutability & Hash-Chain (FR-150–FR-153)

**AC-017** (US-075, FR-150, FR-151)
- **Given** a state transition occurs on any governed entity (topic, decision, vote, action, risk, MoM)
- **When** the transaction commits
- **Then** an audit log entry is created with: entity type, entity ID, action type, actor user ID, UTC timestamp, before-state JSON, after-state JSON, and correlation ID; the entry is visible to Auditor/Administrator.

**AC-018** (US-076, FR-152)
- **Given** an audit log entry exists in the database
- **When** any party (including a DBA acting outside the application, or any API call including Administrator) attempts to UPDATE or DELETE that row
- **Then** the attempt fails: the database-level constraint or application guard rejects the mutation; no audit row is ever modified in normal operation.

**AC-019** (US-076, FR-152)
- **Given** the audit log implements a hash-chain (each entry's hash covers its data + the previous entry's hash)
- **When** the Auditor runs the integrity check tool (or Administrator triggers it)
- **Then** the check reports all entries valid; if any entry has been tampered with, the check reports the first broken link and the entry ID.

**AC-020** (US-077, FR-153)
- **Given** an Auditor searches the audit log with filters: entity_type="Topic", actor="user-42", date_range="2026-06-01..2026-06-30"
- **When** the search is executed
- **Then** paginated results show only matching entries; each result displays entity type, entity ID, action type, actor, and timestamp; a Member or Submitter attempting the same search receives HTTP 403.

---

## Section 6 — Voting Integrity (FR-070–FR-078)

**AC-021** (US-045, FR-070)
- **Given** a Secretary configures vote VOTE-2026-005 with eligible-voter list = {Alice, Bob, Carol}, options = {Approve, Reject}, quorum = 2, abstention = allowed
- **When** the vote is opened
- **Then** the configuration is locked; only Alice, Bob, and Carol can cast a ballot; the vote appears in each eligible voter's notification center with a deep link.

**AC-022** (US-046, FR-072)
- **Given** vote VOTE-2026-005 is Open and Alice has already cast her vote
- **When** Alice attempts to cast a second ballot
- **Then** the system rejects the second cast with an error ("You have already voted"); the first ballot is unchanged; the attempt is audited.

**AC-023** (US-046, FR-073)
- **Given** vote VOTE-2026-005 is Closed
- **When** the Auditor views the vote record
- **Then** each eligible voter's individual choice is visible (attributed, not anonymous); aggregate totals are also shown; no voter's name is masked; the tally is the same as visible to Chairman.

**AC-024** (US-047, FR-071)
- **Given** vote VOTE-2026-005 has quorum = 2 and only 1 eligible voter has cast a ballot
- **When** the Secretary attempts to close the vote
- **Then** the system rejects the close with an error ("Quorum not met: 1 of 2 required votes cast"); the vote remains Open.

**AC-025** (US-049, FR-075)
- **Given** vote VOTE-2026-005 is Closed and Ratified
- **When** any user (including Administrator or Chairman) attempts to modify any ballot record, the tally, or the chairman's recorded action via API
- **Then** the system returns an error ("Vote record is immutable after close"); no field is altered; the attempt is audited.

**AC-026** (US-051, FR-077)
- **Given** a vote is in Ratified state
- **When** any party attempts to transition it back to Closed, Open, or Configured
- **Then** the system rejects the transition; vote lifecycle is strictly forward-only.

---

## Section 7 — Decision Immutability & Supersession (FR-062–FR-067)

**AC-027** (US-055, FR-066)
- **Given** decision DECN-2026-003 is in Issued status
- **When** any user (including Secretary or Chairman) attempts to edit the outcome, rationale, alternatives, or conditions fields via API or UI
- **Then** the system returns an error ("Issued decisions are immutable; create a superseding decision"); no field is altered; the attempt is audited.

**AC-028** (US-055, FR-065)
- **Given** a Secretary creates new decision DECN-2026-004 that supersedes DECN-2026-003
- **When** DECN-2026-004 is Issued
- **Then** DECN-2026-003 is marked Superseded with a `SupersededByDecisionId` back-link to DECN-2026-004; both records remain readable; DECN-2026-003's content is unchanged.

**AC-029** (US-054, FR-067)
- **Given** Secretary attempts to mark decision DECN-2026-005 as Issued with no downstream artifact links
- **When** the submit is executed
- **Then** the system rejects with an error ("At least one downstream link (Action, Risk, or other artifact) is required before a decision can be Issued"); the decision remains in Draft.

---

## Section 8 — Topic Lifecycle Guards (FR-025, FR-029, FR-040, FR-042–FR-044)

**AC-030** (US-016, FR-025)
- **Given** a user attempts to submit a new topic without a required field (title, description, type, or affected stream)
- **When** the form is submitted
- **Then** the API returns HTTP 400 with field-level validation errors in the active locale (EN or AR); no topic record is created; the form highlights the missing fields.

**AC-031** (US-020, FR-029)
- **Given** a Secretary triages a submitted topic and selects Reject
- **When** the Reject action is submitted without a rejection rationale
- **Then** the system rejects the submit with an error ("Rejection reason is required"); the topic remains in Submitted or Triage status.

**AC-032** (US-020, FR-029)
- **Given** a Secretary provides a valid rejection rationale and submits
- **When** the rejection is processed
- **Then** the topic transitions to Rejected; the reason, actor, and timestamp are recorded as an immutable event in the topic's history; the submitter receives an in-app notification.

**AC-033** (US-021, FR-044)
- **Given** topic TOP-2026-010 has been Rejected with a recorded reason
- **When** any user (including Administrator) attempts to delete or modify the rejection event record
- **Then** the system rejects the mutation; the rejection event is immutable.

**AC-034** (US-032, FR-040)
- **Given** topic TOP-2026-012 is in Accepted status and a Member (who is not the Owner) attempts to edit the topic's description
- **When** the edit is submitted
- **Then** the system returns HTTP 403; only the Secretary may edit metadata fields after Acceptance; content fields are locked to prevent retroactive modification.

**AC-035** (US-022, FR-042)
- **Given** a topic is in Accepted status and the Owner has completed all required preparation materials
- **When** the Secretary marks the topic as Prepared
- **Then** the topic transitions to Prepared status; a `TopicPrepared` audit event is emitted; the topic becomes eligible for scheduling.

---

## Section 9 — MoM Versioning & Approval (FR-053, FR-054)

**AC-036** (US-042, FR-054)
- **Given** MoM MIN-2026-003 is in Published (approved and locked) status
- **When** the Secretary discovers an error and submits a correction
- **Then** the system creates a new version (MIN-2026-003 v2) as a superseding document; the prior version remains readable, immutable, and linked to the new version; no in-place edit occurs.

**AC-037** (US-042, FR-054)
- **Given** MoM MIN-2026-004 is in InReview status and the Chairman requests changes
- **When** the Chairman submits the change-request
- **Then** the MoM transitions back to Draft; the Secretary is notified; the review cycle restarts.

**AC-038** (US-043, FR-055)
- **Given** Chairman approves MoM MIN-2026-005
- **When** the approval is committed
- **Then** the MoM transitions to Published (immutable); an in-app notification is sent to all committee members with a deep link to the MoM; the `MoMApproved` audit event records the approving actor and timestamp.

---

## Section 10 — Localization: EN/AR & RTL (FR-003, FR-004, FR-005)

**AC-039** (US-005, FR-003)
- **Given** a user has an unsaved form with data in it and the interface is in English
- **When** they switch the locale to Arabic
- **Then** all UI labels, button text, validation messages, and navigation items render in Arabic; the form data is preserved exactly as entered; no unsaved data is lost during the locale switch.

**AC-040** (US-006, FR-004)
- **Given** a user selects Arabic as their locale
- **When** any page renders
- **Then** the `dir="rtl"` attribute is applied to the root element; all text flows right-to-left; navigation is mirrored; form inputs are right-aligned; table columns are ordered right-to-left; no UI element uses a hardcoded LTR layout.

**AC-041** (US-006, FR-004)
- **Given** the Arabic locale is active
- **When** a visual regression test captures every page in the application
- **Then** no LTR text flow artifacts are detected (no left-anchored text blocks, no misaligned icons, no cropped RTL text) on Chrome and Edge.

**AC-042** (US-007, FR-005)
- **Given** a user sets their theme preference to "dark"
- **When** they end their session and log in again
- **Then** the dark theme is active without requiring them to set it again.

---

## Section 11 — Accessibility: Keyboard Navigation & DnD Alternatives (FR-034, FR-047)

**AC-043** (US-027, FR-034)
- **Given** a user is on the backlog view and cannot or chooses not to use drag-and-drop
- **When** they select a topic and use the keyboard-accessible move-up/move-down controls
- **Then** the topic's priority ordinal changes correctly; the UI updates to reflect the new position; the change is persisted; no mouse interaction is required.

**AC-044** (US-034, FR-047)
- **Given** a user is building an agenda and is on the agenda-item reorder UI
- **When** they use the keyboard alternative (move-up/move-down controls or ARIA-described keyboard interaction)
- **Then** items reorder correctly without requiring drag-and-drop; the focus ring remains visible and moves to the moved item.

**AC-045** (FR-034, FR-047)
- **Given** any interactive element in the application (buttons, links, form controls, DnD alternatives, modals)
- **When** a keyboard-only user tabs through the page
- **Then** every interactive element receives a visible focus indicator (contrast ratio ≥ 3:1 against adjacent color per WCAG 2.2 AA success criterion 2.4.11); no element is keyboard-unreachable that is also reachable by mouse.

**AC-046** (FR-004, FR-034)
- **Given** any form field, label, button, or icon in the UI
- **When** inspected for WCAG 2.2 AA compliance
- **Then** every form control has a programmatically associated label; every icon-only button has an `aria-label`; the reading order in DOM matches the visual order in both LTR (EN) and RTL (AR) layouts; color contrast ratio ≥ 4.5:1 for normal text and 3:1 for large text.

---

## Section 12 — Unsaved-Work Guard (FR-015)

**AC-047** (US-009, FR-015)
- **Given** a user has made changes to a form (topic edit, action update, MoM draft) that have not been saved
- **When** they navigate to a different page (via browser back, nav menu, or route change)
- **Then** a confirmation prompt appears asking "You have unsaved changes. Leave without saving?"; if they confirm, navigation proceeds; if they cancel, they remain on the form with data intact.

**AC-048** (US-009, FR-015)
- **Given** a user is editing a form with unsaved changes
- **When** they attempt to close the browser tab
- **Then** the browser's built-in beforeunload dialog is triggered (standard browser behavior; no data is silently lost).

---

## Section 13 — File Upload Validation (FR-006, FR-027)

**AC-049** (US-018, FR-006, FR-027)
- **Given** a user attempts to attach a file to a topic
- **When** the selected file exceeds the maximum allowed size (configurable, default 50 MB) or has a disallowed MIME type
- **Then** the system rejects the upload with a clear error message in the active locale (EN or AR) naming the constraint violated; no partial file is stored; the topic record is unchanged.

**AC-050** (US-018, FR-006)
- **Given** a valid file is uploaded to a topic
- **When** the upload completes
- **Then** the file metadata (filename, size, MIME type, uploader ID, UTC timestamp, topic ID) is stored in SQL Server; the file content is stored in MinIO via the `IFileStore` abstraction; a `DocumentAttached` audit event is emitted.

---

## Section 14 — In-App Notification Delivery (FR-129–FR-132)

**AC-051** (US-073, FR-130)
- **Given** a Secretary publishes an agenda for meeting MTG-2026-010
- **When** the publish action is committed
- **Then** an in-app notification is created for every committee member within 5 seconds; each notification contains the meeting date, agenda title, and a deep link to the agenda view; the notification appears in the recipient's notification center.

**AC-052** (US-074, FR-131)
- **Given** an eligible voter receives an in-app notification that vote VOTE-2026-008 is Open
- **When** they click the notification
- **Then** they are navigated directly to the voting UI for that specific vote/topic; no additional navigation steps are required.

**AC-053** (US-073, FR-129)
- **Given** the Webex adapter is not yet active (v1 in-app only)
- **When** any notification is dispatched
- **Then** it is delivered via the in-app notification center channel exclusively; no email, no Webex message is attempted; no error is raised for the absence of other channels.

---

## Section 15 — Background Jobs & Reminders (FR-011, FR-083, FR-084)

**AC-054** (US-060, FR-083)
- **Given** action ACT-2026-020 has a due date of T+3 days and the reminder threshold is 3 days
- **When** the Hangfire reminder job runs (daily)
- **Then** the action owner receives an in-app notification reminding them of the upcoming due date; the notification contains the action title and due date.

**AC-055** (US-061, FR-084)
- **Given** action ACT-2026-021 is Overdue by more than 2 days (configurable threshold)
- **When** the Hangfire escalation job runs
- **Then** the Secretary and Chairman receive an in-app escalation notification with the action title, owner, and days overdue; the escalation is recorded in the audit log.

**AC-056** (FR-011)
- **Given** the ACMP application is running in Docker Compose
- **When** an Administrator opens the Hangfire dashboard
- **Then** they can see the job queue, job history, and any failed jobs; failed jobs are retried automatically per the configured retry policy; reminder and escalation job executions are listed with timestamps.

---

## Section 16 — Aging Indicator (FR-038)

**AC-057** (US-030, FR-038)
- **Given** topic TOP-2026-030 has Urgency=Critical and has been in Triage status for 4 days (exceeding the 3-day Critical SLA)
- **When** any user views the backlog
- **Then** the topic displays a visual aging badge (e.g., "Overdue 1d"); the Secretary has received an in-app notification that the SLA was breached; the badge updates daily.

---

## Section 17 — Member Directory & Provisioning (FR-016, FR-017, FR-020, FR-021)

**AC-058** (US-013, FR-020)
- **Given** Administrator deactivates user account for "Bob"
- **When** Bob attempts to log in
- **Then** Bob's Keycloak session is valid but ACMP rejects access (the ACMP account is deactivated); all of Bob's historical votes, authorship credits, and action assignments remain attributed to "Bob" in all records; no historical data is anonymized.

**AC-059** (US-014, FR-021)
- **Given** any authenticated user (any role) navigates to the Member Directory
- **When** the page loads
- **Then** a list of all active committee members is displayed with name, role, stream membership, and email; deactivated users do not appear; no authentication error is raised regardless of the viewer's role.

---

## Section 18 — Global Search & Traceability (FR-143–FR-147)

**AC-060** (US-078, FR-143, FR-144)
- **Given** a user enters a search query in the global search bar
- **When** the search executes
- **Then** results are returned within 3 seconds; results are grouped by artifact type (Topics, Decisions, MoMs); each result shows ID, title, a matched excerpt, status, and a deep link; clicking the deep link navigates to the artifact.

**AC-061** (US-079, FR-145)
- **Given** a user types an Arabic search query in the global search bar
- **When** the search executes
- **Then** results include Arabic-language content matches using the SQL Server Arabic word-breaker; English-language results matching the query are also returned if relevant; no character encoding errors occur.

**AC-062** (US-080, FR-146)
- **Given** a user views the detail page of any artifact (topic, decision, action, risk, dependency, MoM)
- **When** the traceability panel loads
- **Then** all upstream typed relationships (what produced this artifact) and downstream typed relationships (what this artifact produced or affects) are displayed with relationship type, target artifact ID, title, and a navigable link.

**AC-063** (US-081, FR-147)
- **Given** a Secretary creates a typed traceability edge from topic TOP-2026-040 (DerivedFrom) to decision DECN-2026-015
- **When** the edge is saved
- **Then** TOP-2026-040's traceability panel shows DECN-2026-015 as an upstream DerivedFrom link; DECN-2026-015's traceability panel shows TOP-2026-040 as a downstream DerivedFrom link; the edge creation is audited.

---

## Section 19 — Dashboards (FR-135–FR-137)

**AC-064** (US-082, FR-135)
- **Given** any authenticated committee member logs in
- **When** the committee dashboard loads
- **Then** the following data is displayed: backlog count by status and urgency, the next scheduled meeting with agenda link, open action counts by status (Open/InProgress/Blocked), overdue action count, and the titles/IDs of the last 5 issued decisions; all values reflect live data.

**AC-065** (US-083, FR-136)
- **Given** a Secretary logs in
- **When** their secretary dashboard loads
- **Then** it shows: count of topics in Triage awaiting review, count of MoMs awaiting approval, count of overdue actions beyond the escalation threshold, and the list of topics exceeding their urgency SLA with their aging day count.

**AC-066** (US-084, FR-137)
- **Given** the Chairman logs in
- **When** their chairman dashboard loads
- **Then** it shows: votes awaiting chairman approval (Closed, not yet Ratified), escalated risks, escalated actions, and topics that have been Deferred ≥2 times.

---

## Traceability

- Every Must-priority FR in `docs/07-functional-requirements.md` is covered by ≥1 AC (Keystone gate G-TRACE).
- AC-### → US-### per each criterion's parenthetical references.
- AC-### → FR-### per each criterion's parenthetical references.
- Hard governance rules covered: authorization (AC-005 to AC-011), SoD (AC-012 to AC-016), audit immutability + hash-chain (AC-017 to AC-020), voting integrity (AC-021 to AC-026), decision immutability + supersession (AC-027 to AC-029), topic lifecycle guards (AC-030 to AC-035), MoM versioning (AC-036 to AC-038), EN/AR + RTL (AC-039 to AC-042), accessibility + keyboard alternatives (AC-043 to AC-046), unsaved-work guard (AC-047 to AC-048), file upload validation (AC-049 to AC-050), in-app notification delivery (AC-051 to AC-053).
- SoD rules: SoD-1 (AC-012 to AC-013), SoD-2 (AC-014), SoD-3 (AC-015 to AC-016), SoD-5 (AC-007).
- Immutability rules: vote immutability (AC-025 to AC-026), decision immutability (AC-027 to AC-029), MoM immutability (AC-036 to AC-038), audit immutability (AC-017 to AC-019), topic rejection immutability (AC-033).
- Phase-gate link: all ACs here are PH-1 gates; PH-1 exit requires all M-priority ACs passed (per `docs/36-roadmap.md §PH-1 Acceptance Criteria`).
