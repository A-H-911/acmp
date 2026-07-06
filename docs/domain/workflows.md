# 13 — Workflow Definitions (Deliverable 16)

**Purpose:** The 25 canonical committee workflows — for each: trigger, participants, preconditions, main steps, exceptions, validation, permissions, notifications, audit events, exit criteria, related artifacts — consistent with the lifecycles (doc 12) and the permission matrix (doc 10).

> Roles/policies reference `docs/domain/permission-role-matrix.md`; entity states reference `docs/domain/entity-lifecycles.md` (`[→ §n]`); entities reference `docs/domain/domain-model.md`. IDs/status models from `../README.md`. Every workflow's mutating step emits an `AuditEvent` (ADR-0009); notifications go via `INotificationChannel` adapters (ADR-0005). "Permissions" lists the gating policies; ABAC scope + SoD (doc 10 §E) always apply.

**Workflow index.** W1 Submit topic · W2 Review/accept into backlog · W3 Prioritize backlog · W4 Prepare a topic · W5 Schedule a topic/meeting · W6 Build/publish agenda · W7 Conduct meeting · W8 Record attendance · W9 Capture discussion · W10 Record/approve MoM · W11 Open/complete voting · W12 Issue a decision · W13 Create follow-up actions · W14 Track implementation · W15 Record risks/mitigations · W16 Convert research→execution topic · W17 Convert decision→ADR · W18 Create/approve invariant · W19 Handle urgent topics · W20 Defer/reject topic · W21 Supersede decision/ADR · W22 Review overdue actions · W23 Close a topic · W24 Reopen a topic · W25 Archive committee records.

---

### W1 — Submit topic
- **Trigger:** A requester/member raises a new topic (backlog source: member, stream request, urgent need, incident, security finding, modernization, innovation, cross-stream, regulatory).
- **Participants:** Submitter / Member / Reviewer / Secretary / Chairman.
- **Preconditions:** Authenticated, provisioned principal (no self-registration, ADR-0004).
- **Main steps:** 1) Open "New topic". 2) Enter title, description, type (1 of 4), source, affected streams/systems, urgency, confidentiality. 3) Optionally attach materials/diagrams. 4) Save `Draft`. 5) Submit → `Topic.Submitted` `[→ §1]`.
- **Alt/exception:** Save as `Draft` and submit later; validation failure returns to form; duplicate-suspected warning (non-blocking) suggests existing topic.
- **Validation:** Required: title, description, type, ≥1 affected stream. Urgency ∈ {Normal,Urgent,Critical}. Confidentiality default `Normal`.
- **Permissions:** `Topic.Submit` (matrix row 1).
- **Notifications:** Secretary notified of new submission; Submitter receives confirmation.
- **Audit:** `TopicSubmitted` (actor, topic id).
- **Exit criteria:** Topic in `Submitted` (or `Draft` if saved).
- **Related artifacts:** `Topic`, `Attachment`, `Diagram`.

### W2 — Review / accept into backlog
- **Trigger:** A `Submitted` topic awaits triage.
- **Participants:** Secretary (primary), Chairman.
- **Preconditions:** Topic `Submitted`.
- **Main steps:** 1) Secretary picks up → `Triage` `[→ §1]`. 2) Validate type/scope/affected streams; deduplicate; set/adjust priority and owner candidate. 3) Decide: accept → `Accepted` (enters backlog view); or reject/defer (W20). 4) On accept, assign/confirm Owner (Owner relationship).
- **Alt/exception:** Needs more info → request from submitter (stays `Triage`); merge duplicate into existing topic; reclassify type.
- **Validation:** Valid `TopicType`; non-duplicate; owner assignable within stream scope.
- **Permissions:** `Topic.Triage` (row 2); owner assignment by Secretary/Chairman.
- **Notifications:** Submitter notified of acceptance/owner; assigned Owner notified.
- **Audit:** `TopicTriaged`, `TopicAccepted`.
- **Exit criteria:** Topic `Accepted` with Owner, on the backlog.
- **Related artifacts:** `Topic`, `CommitteeMembership` (owner eligibility).

### W3 — Prioritize backlog
- **Trigger:** Periodic grooming or new high-urgency arrival.
- **Participants:** Secretary (primary), Chairman.
- **Preconditions:** ≥1 topic in backlog states (`Submitted/Triage/Accepted/Prepared/Scheduled`).
- **Main steps:** 1) Open backlog (list/table/kanban/calendar/timeline view — `Backlog` is a view over `Topic`). 2) Reorder via accessible drag-and-drop or set `Priority`. 3) Apply filters (stream/type/urgency/aging). 4) Persist ordering on `Topic.Priority`.
- **Alt/exception:** Bulk reprioritize; urgency-driven auto-surfacing (W19); save a shared/personal view.
- **Validation:** Priority is a total/partial order on the active backlog; aging computed from `CreatedAt`.
- **Permissions:** `Backlog.Prioritize` (row 4).
- **Notifications:** None by default (optional digest of backlog changes).
- **Audit:** priority changes audited on `Topic`.
- **Exit criteria:** Updated, ordered backlog.
- **Related artifacts:** `Topic` (Priority), `Backlog` (saved view).

### W4 — Prepare a topic
- **Trigger:** An `Accepted` topic is selected for upcoming presentation.
- **Participants:** Owner (primary), Assignees, Secretary.
- **Preconditions:** Topic `Accepted`; Owner assigned.
- **Main steps:** 1) Owner completes preparation: refine description/scope, attach research (`ResearchMission`/`Finding`), diagrams, supporting docs. 2) Identify affected systems, dependencies, risks. 3) Complete preparation checklist. 4) Mark `Prepared` `[→ §1]`.
- **Alt/exception:** Preparation reveals need for research → spawn `ResearchMission` (and possibly revert to `Triage`); add dependencies/risks (W15).
- **Validation:** Required preparation fields/materials present; presenter candidate identified.
- **Permissions:** `Topic.Edit` (row 3, AiO=Owner); `Diagram.Attach`/`Document.Manage`/`Research.Manage` (AiO).
- **Notifications:** Secretary notified topic is `Prepared`.
- **Audit:** `TopicPrepared`; artifact attachments audited.
- **Exit criteria:** Topic `Prepared`, ready to schedule.
- **Related artifacts:** `Diagram`, `Document`, `ResearchMission`, `Risk`, `Dependency`.

### W5 — Schedule a topic / meeting
- **Trigger:** Prepared topics need a session; cadence (weekly/bi-weekly) due.
- **Participants:** Secretary (primary), Chairman.
- **Preconditions:** ≥1 `Prepared` topic; a `Meeting` exists or is created.
- **Main steps:** 1) Create/select `Meeting` (date/time, chair) → `Meeting.Scheduled` `[→ §5]`. 2) Optionally create the external conference (Webex via adapter, ADR-0005) capturing `JoinUrl`/`ExternalConferenceId`. 3) Place prepared topic(s) onto the meeting's agenda (W6) → topic `Scheduled` `[→ §1]`.
- **Alt/exception:** Reschedule/cancel meeting (notify participants); urgent topic forces an off-cadence meeting (W19); Webex unavailable → manual location/URL.
- **Validation:** No double-booking of chair; meeting within cadence policy or justified; urgency SLA respected.
- **Permissions:** `Meeting.Schedule` (row 6).
- **Notifications:** Participants/presenters invited (meeting scheduled); calendar/Webex invite via adapter.
- **Audit:** `MeetingScheduled`, `TopicScheduled` (and `MeetingCancelled` if applicable).
- **Exit criteria:** Meeting `Scheduled`; topic(s) `Scheduled` on its agenda.
- **Related artifacts:** `Meeting`, `Agenda`, `AgendaItem`, `Notification`.

### W6 — Build / publish agenda
- **Trigger:** A scheduled meeting needs its agenda finalized.
- **Participants:** Secretary (primary), Chairman.
- **Preconditions:** `Meeting` `Scheduled`; ≥1 `Prepared` topic.
- **Main steps:** 1) Add `AgendaItem`s (topic, order, time-box, presenter). 2) Sequence via DnD; assign presenters (Presenter relationship). 3) Review total time vs meeting length. 4) Publish → `Agenda.Published` `[→ §1 Scheduled]`. 5) Carry-over unfinished items from prior agenda if applicable.
- **Alt/exception:** Re-publish (versioned) after change; lock agenda at meeting start; carry-over creates `AgendaItem` with `CarryOverFromAgendaId`.
- **Validation:** Each item has time-box + presenter; sum(time-box) ≤ meeting duration (warn if over); no duplicate topic on one agenda.
- **Permissions:** `Agenda.Publish` (row 5).
- **Notifications:** Participants/presenters notified agenda published; presenters of their slot.
- **Audit:** `AgendaPublished`, agenda reorder, presenter assignment.
- **Exit criteria:** `Agenda` `Published`; topics `Scheduled`.
- **Related artifacts:** `Agenda`, `AgendaItem`, `Topic`, `Presenter` relationship.

### W7 — Conduct meeting
- **Trigger:** Scheduled meeting time reached.
- **Participants:** Chairman (chairs), Secretary (runs system), Members, Presenters, Guests.
- **Preconditions:** `Meeting` `Scheduled`; `Agenda` `Published`.
- **Main steps:** 1) Start meeting → `Meeting.InProgress`; agenda `Locked` `[→ §5]`. 2) Record attendance (W8). 3) Per agenda item: topic → `InCommittee` `[→ §1]`; present; capture discussion (W9); run voting if needed (W11); issue decision (W12); create actions (W13). 4) Set each `AgendaItem.Outcome`. 5) Conclude → `Meeting.Held`.
- **Alt/exception:** Quorum not met → defer voting items (W20) or proceed for discussion only; over-time topics carried over (W6 next meeting); recording/transcript captured (optional, W9).
- **Validation:** Quorum checked before any vote opens (ADR-0010); only agenda topics decided.
- **Permissions:** chair/secretary process control (rows 7–12); `Vote.Cast` for members.
- **Notifications:** Meeting-start reminder; post-meeting summary triggers (MoM, actions).
- **Audit:** `MeetingStarted`, per-item events, `MeetingHeld`.
- **Exit criteria:** Meeting `Held`; per-topic outcomes recorded.
- **Related artifacts:** `Meeting`, `Attendance`, `Discussion`, `Vote`, `Decision`, `Action`, `Recording`, `Transcript`.

### W8 — Record attendance
- **Trigger:** Meeting in progress; quorum determination needed.
- **Participants:** Secretary (primary), Chairman.
- **Preconditions:** `Meeting` `InProgress`; participant list from invites/`CommitteeMembership`.
- **Main steps:** 1) Mark each participant Present/Absent/Excused/Late and voting-eligibility. 2) Capture join/leave times (auto from Webex participants webhook if integrated). 3) System computes quorum status against the committee quorum policy.
- **Alt/exception:** Late arrivals update eligibility mid-meeting; Webex participant data imported as candidate, confirmed by Secretary; COI declarations recorded (affecting vote eligibility, SoD-4).
- **Validation:** Eligible-present count vs quorum rule; one attendance record per participant.
- **Permissions:** `Attendance.Record` (row 7).
- **Notifications:** None (internal); quorum-not-met may alert chair.
- **Audit:** `AttendanceRecorded` (quorum-relevant, audited).
- **Exit criteria:** Attendance + quorum status established for the meeting.
- **Related artifacts:** `Attendance`, `Meeting`, `Vote` (quorum input).

### W9 — Capture discussion
- **Trigger:** A topic is under discussion in-meeting.
- **Participants:** Secretary/secretary (notes), Chairman; optional transcript pipeline.
- **Preconditions:** Topic `InCommittee`; `Meeting` `InProgress`.
- **Main steps:** 1) Capture human notes as `Discussion` (per topic). 2) Optionally ingest `Recording`/`Transcript` (Webex snippets, ADR-0005) — **candidate** content. 3) Human reviews candidate excerpts; approves selected items into `Discussion` (principle 5). 4) Link discussion to topic + (later) MoM.
- **Alt/exception:** No recording (Webex Assistant off — cannot be enabled programmatically) → human notes only; AI-extracted candidate actions flagged for W13 review.
- **Validation:** Candidate transcript items require explicit human approval before becoming part of the record; language (EN/AR/mixed) captured.
- **Permissions:** `Minutes.Capture` (row 8).
- **Notifications:** None by default.
- **Audit:** `DiscussionCaptured`; candidate-promotion audited.
- **Exit criteria:** Discussion captured for the topic; candidates resolved (approved/discarded).
- **Related artifacts:** `Discussion`, `Recording`, `Transcript`, candidate `Action`.

### W10 — Record / approve MoM
- **Trigger:** Meeting concluded (`Held`); official record required.
- **Participants:** Secretary (author), Chairman (approver).
- **Preconditions:** `Meeting` `Held`; discussions/decisions/actions captured.
- **Main steps:** 1) Create MoM `Draft` `[→ §6]` (from `Template`), auto-aggregating attendance, decisions, actions, discussion summary. 2) Edit/curate; ensure candidate transcript content human-reviewed. 3) Submit → `InReview`. 4) Approver reviews → `Approved` (SoD-2: approver ≠ sole author where staffing allows). 5) Publish → `Published`; distribute.
- **Alt/exception:** Corrections after publish → new `Version` supersedes prior `[→ §6]`; reviewer requests changes → back to `Draft`.
- **Validation:** Required sections complete (attendance, decisions, actions, summary); all referenced decisions `Issued`.
- **Permissions:** `Minutes.Capture` (row 8), `Minutes.Approve` (row 9, SoD-2).
- **Notifications:** Reviewers on `InReview`; all participants on `Published`; owners on linked actions.
- **Audit:** `MoMDrafted/InReview/Approved/Published` (approval high-importance); `MoMSuperseded` on new version.
- **Exit criteria:** MoM `Published` (immutable version).
- **Related artifacts:** `MinutesOfMeeting`, `Template`, `Decision`, `Action`, `Attendance`.

### W11 — Open / complete voting
- **Trigger:** A topic requires a formal committee vote.
- **Participants:** Secretary/Chairman (manage), Members (cast), Chairman (cast + ratify).
- **Preconditions:** Topic `InCommittee`; quorum met (W8); eligible-voter set defined.
- **Main steps:** 1) Configure `Vote` (options, eligible voters, quorum rule, abstain) → `Configured` `[→ §4]`. 2) Apply COI exclusions (SoD-4). 3) Open → `Open`. 4) Members cast ballots (`Vote.Cast`, one per voter; **always attributed — anonymity out of scope for v1**). 5) Close → `Closed`; tally computed and **frozen** (SoD-3: counter ≠ sole chair-overrider). 6) Tally feeds the `Decision` (W12); chair ratifies → `Ratified`.
- **Alt/exception:** Quorum lost mid-vote → close as inconclusive, defer (W20); contested/erroneous vote → **new** Vote (closed one is immutable).
- **Validation:** Voter ∈ eligible set; one ballot/voter; quorum of cast met or timeout; immutability after `Closed` (ADR-0009/0010).
- **Permissions:** `Vote.Manage` (row 10), `Vote.Cast` (row 11). SoD-3 enforced.
- **Notifications:** Eligible voters on `VoteOpened`; chair/secretary on `VoteClosed`.
- **Audit:** `VoteConfigured/Opened`, `BallotCast` (anonymized if applicable), `VoteClosed` (tally frozen, high), `VoteRatified`.
- **Exit criteria:** `Vote` `Closed`/`Ratified` with immutable tally.
- **Related artifacts:** `Vote`, `Decision`, `Attendance` (quorum).

### W12 — Issue a decision
- **Trigger:** Committee reaches an outcome on a topic (post-vote or by chair authority).
- **Participants:** Secretary (record), Chairman (approve/override).
- **Preconditions:** Topic `InCommittee`; vote `Closed` (if vote-driven).
- **Main steps:** 1) Record `Decision` `Draft` `[→ §3]` with outcome (`README` §E set), rationale, alternatives, conditions (if `ConditionallyApproved`). 2) Link `Vote`. 3) Chairman approves or **overrides** (override + justification recorded, SoD-3/4) → `Issued`. 4) Topic → `Decided` `[→ §1]`. 5) Create follow-up actions (W13); add `DecisionCondition`s.
- **Alt/exception:** Outcome `MoreInfoRequired/ResearchRequired/Deferred` → topic returns/defers (W16/W20); chair override against the vote is explicitly recorded; `Converted` outcome → W16/W17.
- **Validation:** Outcome ∈ canonical set; rationale present; chair approval recorded; conditions captured for conditional approval; SoD-4 (decider ≠ sole conflicted owner).
- **Permissions:** `Decision.Record` (row 12), `Decision.ChairApprove` (row 13).
- **Notifications:** Topic owner/stakeholders on `DecisionIssued`; action owners on created actions.
- **Audit:** `DecisionDrafted`, `DecisionIssued` (high; override flagged), `TopicDecided`.
- **Exit criteria:** `Decision` `Issued` (immutable); topic `Decided`.
- **Related artifacts:** `Decision`, `DecisionCondition`, `Vote`, `Action`, `ADR` (if converted).

### W13 — Create follow-up actions
- **Trigger:** A decision/condition/meeting requires follow-up work.
- **Participants:** Secretary/Chairman/Owner.
- **Preconditions:** Source exists (`Decision`/`DecisionCondition`/`Meeting`/`Topic`/`Risk`).
- **Main steps:** 1) Create `Action` `Open` `[→ §7]` with owner, due date, priority, source link. 2) Assign assignees. 3) Link to source (decision/condition) via `Relationship`. 4) Promote any reviewed candidate actions from the transcript (W9).
- **Alt/exception:** Bulk-create from decision conditions; reassign owner; candidate action rejected (discarded).
- **Validation:** Owner set; due date sane; SoD-1 noted (creator/owner ≠ eventual verifier).
- **Permissions:** `Action.Create` (row 14, AiO=Owner).
- **Notifications:** Action owner/assignees on assignment.
- **Audit:** `ActionCreated`.
- **Exit criteria:** Action(s) `Open` linked to source.
- **Related artifacts:** `Action`, `DecisionCondition`, `Relationship`.

### W14 — Track implementation
- **Trigger:** Open actions in flight; progress/verification needed.
- **Participants:** Owner/Assignee (progress), Secretary/Chairman/verifier (verify).
- **Preconditions:** `Action` in `Open/InProgress/Blocked`.
- **Main steps:** 1) Assignee starts → `InProgress` `[→ §7]`; logs `ProgressUpdate`s. 2) If blocked → `Blocked` (+ reason/`Dependency`); unblock → `InProgress`. 3) Complete → `Completed` (evidence). 4) Independent verifier verifies → `Verified` (**SoD-1: verifier ≠ owner/completer**). 5) Update linked `DecisionCondition` to `Met`.
- **Alt/exception:** Cancel action (reason) → `Cancelled`; overdue handling (W22); reopen a verified action only via new action.
- **Validation:** Progress 0–100; completion evidence; SoD-1 hard guard on verify.
- **Permissions:** `Action.Create`/edit (AiO) for progress; `Action.Verify` (row 15, SoD-1) for verification.
- **Notifications:** Status changes to owner/secretary; due-soon/overdue reminders (Hangfire).
- **Audit:** `ActionStarted/Blocked/Completed`, `ActionVerified` (high).
- **Exit criteria:** Action `Verified` (or `Cancelled`); related condition `Met`.
- **Related artifacts:** `Action`, `ProgressUpdate`, `Dependency`, `DecisionCondition`.

### W15 — Record risks / mitigations
- **Trigger:** A risk is identified (during preparation, meeting, or implementation).
- **Participants:** Owner/Member/Reviewer (raise), Secretary/Chairman (accept/escalate).
- **Preconditions:** Subject exists (`Topic`/`Decision`/`System`/`ADR`).
- **Main steps:** 1) Raise `Risk` `Open` `[→ §10]` (likelihood/impact → severity, subject). 2) Plan `Mitigation`(s); begin → `Mitigating`. 3) Optionally link mitigations to `Action`s (W13). 4) Close → `Closed` when mitigated/no longer applicable; or `Accepted`/`Escalated` with rationale/authority.
- **Alt/exception:** Risk acceptance by chair (recorded); escalation to higher authority; risk reopened by raising a new one.
- **Validation:** Likelihood/impact set; acceptance/escalation requires authority + rationale.
- **Permissions:** `Risk.Manage` (row 16, AiO=Owner); accept/escalate Chairman/Secretary.
- **Notifications:** Owner/stakeholders on raise/escalate; `RiskEscalated` to target authority.
- **Audit:** `RiskRaised/Mitigating/Closed`, `RiskAccepted/Escalated` (high).
- **Exit criteria:** Risk `Closed`/`Accepted` (terminal) or actively `Mitigating`.
- **Related artifacts:** `Risk`, `Mitigation`, `Action`.

### W16 — Convert research → execution topic
- **Trigger:** A `ResearchMission` completes (or a `ResearchRequired` decision), yielding direction to execute.
- **Participants:** Owner/Secretary, Chairman.
- **Preconditions:** `ResearchMission` `Completed` with verified `Finding`/`Recommendation` (or decision outcome `ResearchRequired/Converted`).
- **Main steps:** 1) Review findings/recommendations (imported from Keystone, ADR-0007; human-verified, principle 5). 2) Create a new execution `Topic` (type `ArchitectureDecision`/`EnhancementInnovation`) seeded from the recommendation. 3) Link successor topic to mission/source topic via `Relationship` (`ConvertsTo`/`DerivesFrom`). 4) Mark source topic `Converted` `[→ §1]` if it was a research topic.
- **Alt/exception:** Recommendation rejected → no conversion (recorded); mission cancelled.
- **Validation:** Findings `IsVerified`; successor topic has owner + scope.
- **Permissions:** `Research.Manage` (row 26), `Topic.Submit`/`Topic.Triage` for the successor.
- **Notifications:** New topic owner notified; stakeholders of conversion.
- **Audit:** `ResearchCompleted`, `TopicConverted`, `RelationshipCreated`.
- **Exit criteria:** Execution `Topic` created and linked; research topic `Converted`.
- **Related artifacts:** `ResearchMission`, `Finding`, `Recommendation`, `Topic`, `Relationship`.

### W17 — Convert decision → ADR
- **Trigger:** An issued `Decision` represents a durable architecture decision warranting an ADR.
- **Participants:** Owner/Secretary (author), Chairman/Secretary (approve).
- **Preconditions:** `Decision` `Issued` with outcome `Approved`/`ConditionallyApproved`/`Converted`.
- **Main steps:** 1) Create `ADR` `Draft` `[→ §8]` (MADR-lite `Template`) from the decision (context, decision, consequences, options). 2) Set `SourceDecisionId`; link via `Relationship` (`DerivesFrom`). 3) Propose → `Proposed`. 4) Approve → `Approved`. 5) Reference any governing `Invariant`s.
- **Alt/exception:** Changes requested → back to `Draft`; later supersession via W21.
- **Validation:** Required MADR-lite sections; linked decision `Issued`; approval recorded.
- **Permissions:** `Adr.Create` (row 18, AiO), `Adr.Approve` (row 19).
- **Notifications:** Reviewers on `Proposed`; stakeholders on `Approved`.
- **Audit:** `AdrDrafted/Proposed/Approved` (approval high).
- **Exit criteria:** `ADR` `Approved` (immutable), linked to its source decision.
- **Related artifacts:** `ADR`, `Decision`, `Template`, `Invariant`, `Relationship`.

### W18 — Create / approve invariant
- **Trigger:** A governing architecture rule (principle/standard/policy/constraint) is needed.
- **Participants:** Owner/Secretary (author), Chairman/Secretary (approve/activate).
- **Preconditions:** Rationale + scope identified.
- **Main steps:** 1) Create `Invariant` `Draft` `[→ §9]` (`Kind`, `Category`, statement, scope, exceptions policy). 2) Propose → `Proposed`. 3) Committee/chair approves → `Active` (in force). 4) Link to related `ADR`/`Decision`/affected `System`s.
- **Alt/exception:** Retire/supersede later (W21); violations recorded separately as `Risk`/`Action`/`AuditEvent` (not state changes).
- **Validation:** `Kind` ∈ {Principle,Standard,Policy,Constraint}; scope valid; rationale present.
- **Permissions:** `Invariant.Create` (row 21, AiO), `Invariant.Approve` (row 22).
- **Notifications:** Affected stream owners on activation.
- **Audit:** `InvariantDrafted/Proposed/Activated` (activation high).
- **Exit criteria:** `Invariant` `Active`.
- **Related artifacts:** `Invariant`, `ADR`, `Relationship`, (violations → `Risk`/`AuditEvent`).

### W19 — Handle urgent topics
- **Trigger:** A `Critical`/`Urgent` topic (incident, security finding, regulatory) needs expedited handling.
- **Participants:** Secretary/Chairman (primary), Owner.
- **Preconditions:** Topic `Urgency` ∈ {Urgent,Critical}.
- **Main steps:** 1) Fast-track triage (W2) bypassing normal grooming. 2) Auto-surface to top of backlog (priority boost, W3). 3) Schedule into next/earliest meeting or an **off-cadence** meeting (W5). 4) Expedited preparation (W4) with reduced lead time. 5) Decide (W12) under tightened SLA.
- **Alt/exception:** Confidential urgent topic (security finding) → `Restricted` confidentiality (doc 10 §E.2); chair may convene emergency session; async chair decision then ratified in next meeting.
- **Validation:** Urgency justified; SLA timers (per `TopicType.DefaultSlaByUrgency`) tracked; escalation on SLA breach.
- **Permissions:** standard process policies; emergency convening by Chairman/Secretary.
- **Notifications:** Immediate alerts to chair/secretary/owner; SLA-breach escalations (Hangfire).
- **Audit:** urgency handling + off-cadence scheduling audited; SLA events recorded.
- **Exit criteria:** Urgent topic decided within SLA (or escalated).
- **Related artifacts:** `Topic` (Urgency, Confidentiality), `Meeting`, `Notification`, `Decision`.

### W20 — Defer / reject topic
- **Trigger:** A topic should not proceed now (defer) or at all (reject).
- **Participants:** Secretary/Chairman.
- **Preconditions:** Topic in `Submitted/Triage/Accepted/Scheduled/InCommittee` (defer) or `Submitted/Triage` (reject).
- **Main steps:** 1) Select defer or reject. 2) Record reason (`DeferReason`/`RejectionReason`). 3) Defer → `Deferred` (+ optional revisit date) `[→ §1]`; reject → `Rejected`. 4) Notify submitter/owner.
- **Alt/exception:** Deferred topic reactivated later → `Triage`; rejected topic reopened (W24) with justification; defer in-meeting (`deferInMeeting`).
- **Validation:** Reason mandatory; reject only from early states (post-decision uses supersede/close instead).
- **Permissions:** `Topic.Triage` (row 2) covers reject/defer authority (Secretary/Chairman).
- **Notifications:** Submitter/owner notified with reason.
- **Audit:** `TopicDeferred`/`TopicRejected` (reason recorded).
- **Exit criteria:** Topic `Deferred` (revisitable) or `Rejected` (terminal unless reopened).
- **Related artifacts:** `Topic`, `Notification`.

### W21 — Supersede decision / ADR
- **Trigger:** A new decision/ADR replaces a prior one (circumstances changed). **Edit is not allowed — supersede only** (ADR-0009).
- **Participants:** Secretary (record/author), Chairman (approve).
- **Preconditions:** Prior `Decision` `Issued` / prior `ADR` `Approved`.
- **Main steps (decision):** 1) Issue a **new** `Decision` (W12) for the topic. 2) On issuance, set prior `SupersededByDecisionId`; prior → `Superseded` `[→ §3]`. **Main steps (ADR):** 1) Author a **new** `ADR` (W17); approve. 2) Set prior `SupersededByAdrId`; prior → `Superseded` (or `Deprecated`) `[→ §8]`.
- **Alt/exception:** Deprecate an ADR without a replacement (→ `Deprecated`); supersede an `Invariant` (W18 successor → prior `Superseded`/`Retired`).
- **Validation:** Successor reaches `Issued`/`Approved` before the prior is superseded; supersession reason recorded; **no in-place edit of the immutable prior**.
- **Permissions:** `Decision.Record`+`Decision.ChairApprove` (rows 12/13) / `Adr.Supersede` (row 20).
- **Notifications:** Stakeholders of the superseded artifact notified of the replacement.
- **Audit:** `DecisionSuperseded` / `AdrSuperseded`/`AdrDeprecated` (high; immutable chain).
- **Exit criteria:** Prior `Superseded`/`Deprecated`; successor active; supersession link recorded.
- **Related artifacts:** `Decision`, `ADR`, `Invariant`, `Relationship` (`Supersedes`).

### W22 — Review overdue actions
- **Trigger:** Scheduled job detects actions past due (or a periodic review).
- **Participants:** Secretary (primary), Chairman, action Owners.
- **Preconditions:** Actions with `DueDate < now` in `Open/InProgress/Blocked` → derived `Overdue` `[→ §7]`.
- **Main steps:** 1) Hangfire job flags overdue actions (`ActionOverdue`). 2) Secretary reviews overdue list/dashboard. 3) Escalate to owners/chair; revise due date, reassign, unblock, or cancel. 4) Track resolution.
- **Alt/exception:** Repeated overdue → escalation to Chairman; blocked-by-dependency surfaced for cross-stream resolution.
- **Validation:** `Overdue` is derived (not a stored status); escalation thresholds per `29`.
- **Permissions:** `Action.Create`/edit (AiO) to revise; Secretary/Chairman oversight; reporting via `Report.Export`.
- **Notifications:** Owners on overdue; escalation to chair; reminder/digest cadence (Hangfire).
- **Audit:** `ActionOverdue`, due-date revisions, reassignments.
- **Exit criteria:** Overdue actions re-planned, reassigned, completed, or cancelled.
- **Related artifacts:** `Action`, `ProgressUpdate`, `Notification`, dashboards.

### W23 — Close a topic
- **Trigger:** A decided topic's follow-through is complete.
- **Participants:** Secretary/Chairman.
- **Preconditions:** Topic `Decided`; blocking actions/conditions resolved (or waived).
- **Main steps:** 1) Verify all linked `DecisionCondition`s `Met`/`Waived` and blocking `Action`s `Verified`/`Cancelled`. 2) Close → `Closed` `[→ §1]`. 3) Capture closure summary.
- **Alt/exception:** Outstanding items → cannot close (or close with explicit waiver + justification); converted topics close via `Converted` instead.
- **Validation:** No open blocking conditions/actions unless explicitly waived (audited).
- **Permissions:** `Topic.Triage`/process authority (Secretary/Chairman).
- **Notifications:** Owner/stakeholders on closure.
- **Audit:** `TopicClosed` (waivers recorded).
- **Exit criteria:** Topic `Closed`.
- **Related artifacts:** `Topic`, `DecisionCondition`, `Action`.

### W24 — Reopen a topic
- **Trigger:** A `Closed`/`Rejected` topic must be revisited (new information, changed circumstances).
- **Participants:** Secretary/Chairman.
- **Preconditions:** Topic `Closed` or `Rejected` (not `Converted`).
- **Main steps:** 1) Provide reopen justification. 2) Reopen → `Reopened` `[→ §1]`. 3) Re-triage → `Triage`; re-enter the loop.
- **Alt/exception:** Converted topics are **not** reopened (work continues on the successor) — raise a new topic instead.
- **Validation:** Justification mandatory; original not `Converted`.
- **Permissions:** `Topic.Triage` (Secretary/Chairman).
- **Notifications:** Owner/stakeholders on reopen.
- **Audit:** `TopicReopened` (justification recorded).
- **Exit criteria:** Topic `Reopened` → `Triage`, back in backlog.
- **Related artifacts:** `Topic`, prior `Decision` (context).

### W25 — Archive committee records
- **Trigger:** Records-management/retention milestone, or committee period closeout; a `Committee` is archived.
- **Participants:** Secretary/Administrator; Auditor (oversight).
- **Preconditions:** Records eligible per retention policy (`26`); committee/period concluded.
- **Main steps:** 1) Identify records due for archival (closed topics, held meetings, published MoMs, issued decisions/votes, audit events). 2) Apply archival (immutable snapshot; move to archive tier; preserve trace graph). 3) Archive `Committee` → `Archived` `[→ doc 11]` if period closed. 4) Enforce retention classes (incl. longest-class `AuditEvent`).
- **Alt/exception:** Legal hold overrides scheduled purge; recordings/media may follow a shorter media-retention class (`OQ`); archived records remain readable/exportable to Auditor.
- **Validation:** Immutable records preserved (votes/decisions/MoM/audit never altered/deleted within policy); retention windows per `26`.
- **Permissions:** `Admin.Config` (Administrator) for archival jobs; `Audit.Read`/`Report.Export` (Auditor/Secretary) for retrieval; **no delete of immutable records** by any role within policy.
- **Notifications:** Secretary/Auditor on archival completion; alerts on legal-hold conflicts.
- **Audit:** archival actions audited; the audit log itself is retained at the longest class.
- **Exit criteria:** Records archived per policy; trace integrity preserved; committee `Archived` if applicable.
- **Related artifacts:** `Committee`, `Topic`, `Meeting`, `MinutesOfMeeting`, `Decision`, `Vote`, `AuditEvent`, `Attachment`/`Recording`.

---

## Cross-workflow consistency notes
- **Loop spine:** W1→W2→W3→W4→W5/W6→W7(→W8,W9,W11,W12,W13)→W10 is the core intake→decision→action governance loop (principle 8). W14/W15/W22 sustain follow-through; W16–W18/W21 manage governance artifacts; W19/W20/W23/W24/W25 handle exceptions and lifecycle ends.
- **Immutability touchpoints:** W11 (vote), W12 (decision), W10 (MoM), W17/W21 (ADR) all obey supersede-not-edit (doc 12 §12; ADR-0009).
- **SoD touchpoints:** W14 (SoD-1 verify≠owner), W10 (SoD-2 approver≠author), W11/W12 (SoD-3 counter≠chair-overrider; SoD-4 decider≠conflicted owner) — enforced per doc 10 §E.4.
- **Human-review touchpoints:** W9/W10 (transcript candidates), W16 (research findings) require human approval before record entry (principle 5).

## Traceability
Implements **Deliverable 16**. Roles/policies from `docs/domain/permission-role-matrix.md`; states/transitions from `docs/domain/entity-lifecycles.md`; entities from `docs/domain/domain-model.md`; IDs/status/principles from `../README.md`. Settled decisions: ADR-0004 (onboarding), ADR-0005 (notifications/Webex adapter), ADR-0007 (Keystone research), ADR-0009/0010 (immutability/voting). Notification details in `docs/domain/notification-strategy.md`; retention/archival in `docs/domain/audit-and-records.md`; SLA/urgency in `docs/domain/topic-taxonomy.md`; Webex constraints in `docs/domain/webex-feasibility.md`.
