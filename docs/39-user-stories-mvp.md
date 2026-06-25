# 39 — User Stories: PH-1 MVP (Deliverable 47)

**Purpose:** All `US-###` user stories in scope for PH-1 (the complete architecture governance loop), grouped by module/epic, each tied to FR-IDs from `docs/07-functional-requirements.md` and the canonical roles from `README.md §C`. Acceptance criteria are in `docs/40-acceptance-criteria.md`; AC-IDs cross-referenced per story.

**Conventions.**
- **Priority:** M = Must (PH-1 blocker); S = Should (expected by end of PH-1 but deferrable to PH-2 if needed)
- **Size:** XS / S / M / L / XL (relative story points; calibrate to team)
- **Role names** are canonical per `README.md §C`: Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest/Presenter; plus ABAC capabilities Owner, Assignee, Presenter.
- Every story maps to ≥1 FR (Keystone gate G-TRACE).

---

## EPIC-01 — Platform & Identity

### Login and Session Management

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-001 | As any user, I want to log in via the organization's Keycloak SSO (OIDC), so that I do not need a separate ACMP password and access is governed by my existing identity. | FR-001 | M | M |
| US-002 | As any user, I want my ACMP role (Chairman, Secretary, Member, etc.) to be derived automatically from my Keycloak group/realm-role claims, so that the platform enforces the correct permissions without manual role assignment in ACMP. | FR-002, FR-018 | M | M |
| US-003 | As any user, I want my session to expire after a configurable idle period and to be prompted to re-authenticate, so that unattended sessions do not remain open. | FR-001, FR-013 | M | S |
| US-004 | As any user, I want to see only the navigation items and action buttons my role is authorized to use, so that I am not confused by options I cannot execute. | FR-024 | M | M |

### Localization and Theming

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-005 | As any user, I want to switch the interface language between English and Arabic at any time without losing unsaved form data, so that I can work in my preferred language. | FR-003 | M | M |
| US-006 | As any user browsing in Arabic, I want the entire UI to render in full RTL layout (all text, icons, navigation, form controls, and data tables), so that the platform is comfortable and correct for Arabic readers. | FR-004 | M | L |
| US-007 | As any user, I want to toggle between light and dark themes, and have my preference remembered across sessions, so that I can work in comfortable lighting conditions. | FR-005 | M | S |

### Platform Reliability and Error Handling

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-008 | As any user, I want to see clear, actionable validation error messages in my active language (EN or AR) when I submit invalid data, so that I can correct mistakes efficiently. | FR-014 | M | S |
| US-009 | As any user, I want a confirmation prompt when I navigate away from a form with unsaved changes, so that I do not accidentally lose my work. | FR-015 | M | S |
| US-010 | As an Administrator, I want health check endpoints for liveness and readiness, so that the Docker Compose deployment can verify the application is running correctly. | FR-007 | M | S |

---

## EPIC-02 — Membership Management

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-011 | As an Administrator, I want to create a user account by providing name, email, and stream membership, so that the person can log in via Keycloak and access the system with appropriate scope. | FR-016, FR-017 | M | M |
| US-012 | As an Administrator, I want to assign a user to one or more streams, so that their topic access and contributions are correctly scoped to their area of responsibility. | FR-019 | M | M |
| US-013 | As an Administrator, I want to deactivate a user account, so that a departing team member can no longer log in while their historical votes, authorship, and actions remain intact and attributed. | FR-020 | M | M |
| US-014 | As any authenticated user, I want to view a committee member directory listing all active users with their name, role, stream membership, and email, so that I can identify who is on the committee and contact them. | FR-021 | M | S |
| US-015 | As a Secretary, I want to assign per-topic capabilities (Owner, Assignee, Presenter) to users independently of their global role, so that a Member can be made Presenter on a topic without a global role change. | FR-022 | M | M |

---

## EPIC-03 — Topic Intake & Triage

### Topic Submission

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-016 | As a Submitter, Member, Secretary, or Chairman, I want to submit a new topic with title, description, type, urgency, source, affected streams, and optional target date, so that it enters the committee's intake queue. | FR-025 | M | M |
| US-017 | As a topic submitter, I want the system to automatically assign my topic a canonical ID in the format `TOP-YYYY-###` when I submit it, so that I can reference and track it unambiguously. | FR-026 | M | S |
| US-018 | As a topic Owner or Secretary, I want to attach files (presentations, documents, diagrams, data sheets) to a topic, so that committee members can review supporting material before the meeting. | FR-027 | M | M |
| US-019 | As a topic Owner, I want to add free-text comments to a topic (timestamped and attributed), so that I can capture notes, updates, and clarifications in context without editing the core topic record. | FR-028 | M | S |

### Triage Workflow

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-020 | As a Secretary, I want to move a submitted topic into Triage, review its fields, and then accept it (assigning an Owner and confirming scope) or reject/defer it with a mandatory reason, so that only valid, well-scoped topics enter the active backlog. | FR-029 | M | M |
| US-021 | As a Secretary, I want rejection and deferral reasons to be recorded as immutable events in the topic's history, so that the triage decision is always auditable and not erasable. | FR-044 | M | S |
| US-022 | As a Secretary, I want to mark an accepted topic as Prepared once its presentation materials and supporting research are complete, so that the scheduling step only considers ready topics. | FR-042 | M | S |
| US-023 | As a Secretary, I want to schedule a prepared topic to a specific meeting, moving it to Scheduled status, so that it appears on that meeting's agenda. | FR-043 | M | S |

---

## EPIC-04 — Backlog Management

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-024 | As a Secretary or Chairman, I want to view the backlog as a sortable, filterable list showing title, type, urgency badge, status, owner, affected streams, and dates, so that I can quickly scan and manage the queue. | FR-031 | M | M |
| US-025 | As a Secretary or Chairman, I want to view the backlog as a dense table with user-configurable columns (show/hide, reorder), so that power users can surface the fields most relevant to their workflow. | FR-032 | M | M |
| US-026 | As a Secretary or Chairman, I want to view the backlog as a kanban board with status columns, where dragging a card between columns changes its status (with permission check), so that I can visually manage flow. | FR-033 | M | L |
| US-027 | As a Secretary or Chairman, I want a keyboard-accessible alternative to drag-and-drop for reordering topics (e.g., move-up/move-down controls), so that the interface is operable without a mouse and meets accessibility requirements. | FR-034 | M | M |
| US-028 | As a Secretary or Chairman, I want to view the backlog as a calendar view showing topics by their scheduled meeting date, so that I can see how topics are spread across upcoming meetings. | FR-035 | M | M |
| US-029 | As a Secretary or Chairman, I want to drag-and-drop topics to reprioritize them in the backlog, with priority stored as an explicit ordinal field, so that the committee's session order is always explicit. | FR-037 | M | M |
| US-030 | As a Secretary, I want to see aging indicators on topics that have remained in the same status beyond their urgency SLA (Critical: 3d, Urgent: 7d, Normal: 21d), so that stalled work is immediately visible and I receive a background notification. | FR-038 | M | M |
| US-031 | As any committee member, I want to view a topic's detail page showing all fields, status history (with actor and timestamp), linked artifacts, and a traceability graph excerpt, so that I have a complete picture of a topic's journey. | FR-039 | M | L |
| US-032 | As a topic Owner or Secretary, I want topic editing to be permitted in early statuses (Draft, Submitted, Triage) and restricted (Secretary-only metadata edits) after Acceptance, so that the record cannot be retroactively altered once the committee has engaged. | FR-040 | M | M |

---

## EPIC-05 — Agenda Build & Publish

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-033 | As a Secretary, I want to create an agenda for a scheduled meeting by selecting topics from the backlog, setting presentation order, assigning a time-box per item, and assigning a presenter, so that the meeting has a structured plan. | FR-046 | M | M |
| US-034 | As a Secretary, I want to reorder agenda items via drag-and-drop with a keyboard-accessible alternative, so that the session order can be adjusted easily and accessibly. | FR-047 | M | M |
| US-035 | As a Secretary, I want the system to automatically suggest carry-over items from the previous meeting's agenda that were not resolved, so that unfinished business is not silently dropped. | FR-048 | M | M |
| US-036 | As a Secretary, I want to publish the agenda to committee members, triggering an in-app notification with agenda content and material links, so that members can prepare before the meeting. | FR-049 | M | M |

---

## EPIC-06 — Meeting & Attendance

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-037 | As a Secretary, I want to create a meeting record with date, start/end time, meeting type (regular/extraordinary/emergency), mode (in-person/remote/hybrid), and a link to the published agenda, so that the meeting is formally recorded. | FR-050 | M | M |
| US-038 | As a Secretary, I want to mark each committee member as Present, Absent, or Remote for a given meeting, so that the attendance record is complete and can be used for quorum calculation. | FR-051 | M | M |
| US-039 | As a Secretary, I want to view a meeting calendar showing past and upcoming meetings with status badges, so that I can track the schedule at a glance. | FR-061 | M | S |

---

## EPIC-07 — Minutes Capture & Approval

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-040 | As a Secretary, I want to add live discussion notes per agenda item during a meeting (with auto-save on typing pause ≤2s), so that key points are captured in real time without interrupting the flow. | FR-052 | M | M |
| US-041 | As a Secretary, I want the system to generate a draft Minutes of Meeting (MoM) by compiling attendance, ordered agenda items with notes, decisions issued, and actions created into a structured Markdown document, so that I do not assemble it manually from scratch. | FR-053 | M | L |
| US-042 | As a Secretary, I want to submit the draft MoM for review and approval; a Reviewer or Chairman reviews it, and once the Chairman approves, the MoM is locked and immutable with its version history preserved, so that the official meeting record is tamper-evident. | FR-054 | M | L |
| US-043 | As a Chairman or Secretary, I want the system to send an in-app notification to all committee members when the MoM is approved and published, containing a link to the published MoM, so that everyone is informed promptly. | FR-055 | M | S |
| US-044 | As a Secretary, I want to attach a Webex meeting recording link or upload a recording file to the meeting record, so that future reference to the session is possible. | FR-056 | M | S |

---

## EPIC-08 — Voting

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-045 | As a Secretary, I want to configure a vote on a topic specifying the eligible voter list, voting options, quorum threshold, and whether abstentions are allowed, so that the formal ballot is correctly set up. | FR-070 | M | M |
| US-046 | As a Member or Chairman, I want to cast my attributed vote (one vote per session, no double voting), so that my position is on record. | FR-072, FR-073 | M | M |
| US-047 | As a Secretary, I want the system to prevent closing a vote unless the quorum threshold is met (eligible voters present ≥ quorum count), so that no decision is ratified without sufficient participation. | FR-071 | M | M |
| US-048 | As a Chairman, I want to record my final action on a closed vote — Confirm (ratify majority), Override (select different outcome with mandatory reason), or Abstain-from-override — with my action recorded explicitly and immutably, so that the committee's record reflects my authority decision. | FR-074 | M | M |
| US-049 | As an Auditor or Chairman, I want all vote records (each voter's choice, timestamp, chairman action, quorum check result) to be immutable and stored in the append-only audit log after vote close, so that the ballot cannot be altered retroactively. | FR-075 | M | M |
| US-050 | As an eligible voter, I want to receive an in-app notification when a vote is opened on a topic I am eligible for, so that I can cast my ballot without missing the window. | FR-078 | M | S |
| US-051 | As a Secretary, I want the vote status to follow the strict lifecycle Configured → Open → Closed → Ratified with no backward transitions, so that the ballot process is procedurally correct. | FR-077 | M | S |

---

## EPIC-09 — Decision Record

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-052 | As a Secretary or Chairman, I want to create a committee decision record linked to a topic, capturing outcome (from canonical list), rationale, conditions (if conditionally approved), alternatives considered, authority actor, and effective date, so that the decision is complete and traceable. | FR-062, FR-064 | M | M |
| US-053 | As a Secretary, I want the outcome field to enforce selection from the canonical list (Approved, ConditionallyApproved, Rejected, MoreInfoRequired, FeedbackProvided, EnhancementsRequired, DesignChangesRequired, ResearchRequired, Deferred, Escalated, Converted) with no free-text override, so that outcomes are consistently categorized. | FR-063 | M | S |
| US-054 | As a Chairman or Secretary, I want to link a decision to a downstream artifact (Action, Risk) before marking it Issued, with at least one downstream link required, so that every decision drives follow-through. | FR-067 | M | M |
| US-055 | As a Secretary, I want an issued decision to be immutable — corrections require creating a new superseding decision with a back-link to the prior — so that the record of governance cannot be silently altered. | FR-065, FR-066 | M | M |
| US-056 | As any committee member, I want to view a filterable decision history list (by outcome, topic, stream, date range, chairman), so that I can research past governance decisions efficiently. | FR-069 | M | M |

---

## EPIC-10 — Action Create, Assign, Track & Verify

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-057 | As a Secretary, Chairman, or topic Owner, I want to create an action item linked to a topic, decision, or risk with a mandatory owner and due date, so that every follow-through commitment has an accountable person and a deadline. | FR-079, FR-080 | M | M |
| US-058 | As an action Owner, I want to update my action's status (Open → InProgress → Blocked → Completed) and add timestamped progress notes, so that the committee can track implementation without chasing me in meetings. | FR-081 | M | M |
| US-059 | As any committee member, I want overdue actions (due date past, status not Completed/Verified/Cancelled) to be visually flagged automatically without any user action required, so that stalled work is always visible. | FR-082 | M | S |
| US-060 | As an action Owner, I want to receive an in-app reminder N days before my action is due (configurable, default 3 days), so that I can plan completion without missing the deadline. | FR-083 | M | M |
| US-061 | As a Secretary or Chairman, I want the system to send an escalation notification when an action is overdue beyond a configurable threshold (default 2 days past due), so that chronic delays are surfaced for intervention. | FR-084 | M | M |
| US-062 | As a Secretary or Chairman, I want to mark a completed action as Verified (recorded with actor and timestamp), and the verifier must not be the action's owner, so that self-certification of completion is prevented (SoD-1). | FR-085 | M | M |
| US-063 | As a Secretary, I want to cancel an action with a mandatory cancellation reason, and the cancelled action remains in the topic's history and is never deleted, so that the audit trail is complete. | FR-086 | M | S |
| US-064 | As any committee member, I want to view an actions dashboard showing all open, blocked, and overdue actions across all topics (filterable by owner, stream, topic, due date, status), so that I have a live view of implementation health. | FR-087 | M | M |
| US-065 | As any committee member, I want the topic detail page to show all actions linked to that topic with their current status, so that I can assess outstanding commitments without leaving the topic context. | FR-088 | M | S |

---

## EPIC-11 — Risks (Basic)

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-066 | As a Secretary, Chairman, or Member, I want to create a risk record linked to a topic or action, specifying likelihood, impact, owner, and an optional mitigation plan, so that risks are formally captured and visible. | FR-089 | M | M |
| US-067 | As a Secretary, I want the system to prevent closing a risk without a recorded mitigation plan or an explicit Accepted status with acceptance rationale, so that risks are not silently abandoned. | FR-090 | M | S |
| US-068 | As a risk Owner, I want to update my risk's status through the lifecycle (Open → Mitigating → Closed; or Accepted/Escalated), so that the committee can track mitigation progress. | FR-091 | M | S |
| US-069 | As any committee member, I want the topic detail page to show all risks linked to that topic with current status and a likelihood/impact indicator, so that risk exposure is visible in context. | FR-093 | M | S |

---

## EPIC-12 — Dependencies (Basic)

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-070 | As a Secretary, Chairman, or Member, I want to create a typed dependency edge (Blocks, DependsOn, RelatesTo) between two entities (topic, action, system), so that cross-topic and cross-stream blocking relationships are formally recorded. | FR-094 | M | M |
| US-071 | As any committee member, I want the system to visually flag dependency edges that cross stream boundaries, so that cross-stream dependencies are immediately visible and can be coordinated. | FR-095 | M | S |
| US-072 | As any committee member, I want the topic detail page to show inbound and outbound dependency edges, so that I understand what this topic blocks and what it depends on. | FR-098 | M | S |

---

## EPIC-13 — In-App Notifications

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-073 | As any user, I want to receive in-app notifications (in the notification center) for all key events: topic status change, agenda published, vote opened, vote closed, MoM published, action reminder, action overdue, action escalation, so that I am informed of events relevant to me without relying on email or Webex in v1. | FR-129, FR-130 | M | L |
| US-074 | As any user, I want each notification to contain a deep link directly to the relevant artifact (e.g., vote notification → voting UI for that topic), so that I can act on the notification in one click. | FR-131 | M | S |

---

## EPIC-14 — Audit Trail

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-075 | As an Auditor or Administrator, I want every state transition, field update, and user action on governed entities (topics, decisions, votes, actions, risks, dependencies, MoMs) to be recorded automatically in an append-only audit log, so that the complete history of committee activity is available for compliance review. | FR-150, FR-151 | M | L |
| US-076 | As an Auditor or Administrator, I want audit log entries to be immutable — no API, UI, or database-level process can update or delete an audit row in normal operation — so that the log is a tamper-evident record. | FR-152 | M | M |
| US-077 | As an Auditor or Administrator, I want to search the audit log by entity type, entity ID, actor, action type, and date range (paginated results), so that I can investigate specific events efficiently. | FR-153 | M | M |

---

## EPIC-15 — Search & Traceability

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-078 | As any committee member, I want a global search bar accessible from every page that searches across topics, decisions, and MoMs (by title, description, and content), with results grouped by artifact type showing ID, title, excerpt, status, and a deep link, so that I can find any record quickly. | FR-143, FR-144 | M | M |
| US-079 | As any committee member who works in Arabic, I want global search to correctly process Arabic queries (Arabic word-breaking), so that I get relevant results when I search in Arabic. | FR-145 | M | M |
| US-080 | As any committee member, I want every artifact (topic, decision, action, risk, dependency, MoM) to display a traceability panel showing all upstream and downstream typed relationships, so that I can navigate the governance chain from any point. | FR-146 | M | L |
| US-081 | As a Secretary, I want to create, read, and delete typed traceability edges between any two artifacts (DerivedFrom, Supersedes, Implements, Resolves, References, Blocks, DependsOn, RelatesTo), so that the governance lineage is explicitly modeled. | FR-147 | M | M |

---

## EPIC-16 — Dashboards

| US-ID | User Story | FR-IDs | Priority | Size |
|---|---|---|---|---|
| US-082 | As any authenticated committee member, I want a committee dashboard showing backlog summary by status/urgency, the next scheduled meeting with agenda link, open action counts by status, overdue action count, and the last 5 decisions issued, so that I get an immediate health snapshot on login. | FR-135 | M | M |
| US-083 | As a Secretary, I want my dashboard to show topics in triage queue, pending MoM approvals, overdue actions awaiting escalation, and aging topics beyond SLA, so that I can immediately identify what needs my attention. | FR-136 | M | M |
| US-084 | As a Chairman, I want my dashboard to show votes pending my approval, escalated risks, escalated actions, and topics deferred ≥2 times, so that I can focus on the items that require my authority. | FR-137 | M | M |

---

## Traceability

- All US-### → FR-### per table column above (Keystone gate G-TRACE: every Must-priority FR covered by ≥1 story).
- US-### → `docs/40-acceptance-criteria.md` (AC-###) for testable criteria.
- US-### → `docs/38-epics-and-features.md` (EPIC-## grouping mirrors this file).
- FR-IDs per `docs/07-functional-requirements.md`; roles per `README.md §C`; priority per `docs/36-roadmap.md §PH-1 Scope`.
- Stories scoped to **PH-1 MVP only**. PH-2/PH-3 stories (FR-023, FR-030, FR-036, FR-041, FR-045, FR-057–FR-060, FR-068, FR-076, FR-092, FR-095–FR-097, FR-099–FR-128, FR-133–FR-134, FR-138–FR-142, FR-148–FR-149, FR-154–FR-155) are out of scope for this document and belong in `docs/38-epics-and-features.md`.
