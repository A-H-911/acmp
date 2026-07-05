# 10 — Permission & Role Matrix (Deliverable 13)

**Purpose:** The canonical authorization contract for ACMP — global RBAC roles, a per-action capability matrix, per-topic relationship capabilities, ABAC scoping rules, and the policy-based enforcement approach the execution agent must implement.

> Authoritative for authorization. Other docs (11 domain model, 12 lifecycles, 13 workflows) reference role names and cell values from here verbatim. Roles, modules, IDs and status models are taken from `../README.md` §C/§E unchanged.

---

## A. Authorization model overview

Authorization in ACMP is **two-layered**, combining RBAC and ABAC, per `README` §C (ADR-0004):

1. **Global RBAC role** — a coarse, organization-wide capability grant (`Chairman`, `Secretary`, …). Answers *"what kind of actions may this principal ever perform?"*
2. **ABAC scope + relationship** — fine-grained, contextual constraints (stream scope, topic confidentiality, per-topic Owner/Assignee/Presenter relationship, delegation window, segregation-of-duties). Answers *"on **this** topic/meeting/action, given **this** principal's relationship and scope, is the action permitted **now**?"*

**Effective permission = RBAC grant ∧ ABAC scope ∧ relationship overlay ∧ SoD constraint.** A `Deny` at any layer is final. This is deny-by-default: an action is permitted only if a policy explicitly allows it.

**Enforcement (ASP.NET Core policy-based authorization).** Each row action maps to a named **authorization policy** (e.g. `Policy.Topic.Triage`, `Policy.Vote.Cast`). Policies are composed of:
- **Role requirements** — `RolesAuthorizationRequirement` checking the principal's global role claim(s).
- **ABAC requirements** — custom `IAuthorizationRequirement` + `AuthorizationHandler<TRequirement, TResource>` resources (the handler receives the target aggregate, e.g. the `Topic`, and evaluates stream scope, confidentiality, relationship, delegation, SoD). Resource-based authorization (`IAuthorizationService.AuthorizeAsync(user, resource, policy)`) is used wherever the decision depends on the target instance.
- **Module boundary** — policies live in the owning module (`README` §B); the matrix below is the cross-module union.

Least-privilege defaults: a newly provisioned principal receives only `Member` or `Submitter` (whichever the invitation specifies); elevated roles (`Chairman`, `Secretary`, `Administrator`, `Auditor`) are explicitly assigned, time-bounded where appropriate, and audited (`AuditEvent`). No role implies another; there is no superuser bypass of immutability rules (votes/issued decisions, ADR-0009) — not even `Administrator`.

---

## B. Canonical global roles (RBAC)

Roles are exactly the eight defined in `README` §C. Each is a claim on the principal; a principal MAY hold multiple (subject to SoD, §E).

| Role | Code | Intent | Typical holders | Default scope |
|---|---|---|---|---|
| **Chairman** | `Chairman` | Highest committee authority; final decision approval/override; chairs meetings. Cannot bypass vote/decision immutability. | VP (committee chair). Usually exactly one active; a delegate may act when chair absent. | All streams (committee-wide) |
| **Secretary** | `Secretary` | Operational owner of the committee process: triage, backlog, agenda, scheduling, MoM stewardship, action orchestration. The day-to-day driver. | Lead secretary(s). | All streams |
| **Member** | `Member` | Voting committee member; participates in discussion, votes, may own/contribute to topics, raises risks/dependencies. | Technical directors, senior engineers, iOS/Android SMEs. | Own + assigned streams (configurable, see OQ-AUTH-001) |
| **Reviewer** | `Reviewer` | Reviews topics/ADRs/research and provides feedback/recommendations; non-voting unless also a Member. | Invited specialists, peer reviewers. | Scoped to assigned topics/streams |
| **Auditor** | `Auditor` | Read-only oversight across the system incl. the audit log; exports compliance reports. Never mutates governance data. | Quality/compliance, internal audit. | All streams, read-only |
| **Administrator** | `Administrator` | Platform administration: users, roles, templates, system config, retention jobs. **Not** a committee-content authority (cannot vote, decide, or override immutability). | Platform admins. | Platform-wide (config), not committee decisions |
| **Submitter** | `Submitter` | Stream requester: submits topic requests and tracks their own; minimal committee rights. | Stream business/technical staff outside the committee. | Own submissions only |
| **Guest/Presenter** | `Guest` | Time-boxed external/invited participant; presents on a specific topic/meeting; minimal, expiring access. | Invited presenters, specialists for one meeting. | Single topic/meeting, time-boxed |

**Notes.**
- `Guest/Presenter` is modelled as a single global role `Guest` whose *presenter* abilities are granted **only** via the per-topic `Presenter` relationship (§D). A guest with no presenter relationship is effectively a time-boxed read participant. Whether "Guest" and "Presenter" should be split into two distinct global roles is left to org configuration — see **OQ-AUTH-002**.
- `Member` voting eligibility is per-meeting/per-vote: a Member is an *eligible voter* only if included in the vote's eligible-voter set (ADR-0010); the global role grants the *capability* to vote, the vote configuration grants *eligibility*.

---

## C. Capability matrix (actions × global roles)

Cell legend: **A** = Allow · **AiO** = Allow-if-owner (permitted only when the principal holds the relevant per-topic relationship Owner/Assignee, or is the artifact's creator/assignee; see §D) · **D** = Deny.
"—" never appears; every cell is explicitly A / AiO / D.

ABAC scope (stream, confidentiality, delegation, SoD) further constrains every **A**/**AiO** per §E. Where a row is decided per-instance, the binding policy name is given.

| # | Action (policy) | Chairman | Secretary | Member | Reviewer | Auditor | Administrator | Submitter | Guest/Presenter |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Submit topic (`Topic.Submit`) | A | A | A | A | D | D | A | D |
| 2 | Triage / accept into backlog (`Topic.Triage`) | A | A | D | D | D | D | D | D |
| 3 | Edit backlog item / topic fields (`Topic.Edit`) | A | A | AiO | AiO | D | D | AiO | D |
| 4 | Prioritize backlog (reorder/priority) (`Backlog.Prioritize`) | A | A | D | D | D | D | D | D |
| 5 | Build / publish agenda (`Agenda.Publish`) | A | A | D | D | D | D | D | D |
| 6 | Schedule meeting (`Meeting.Schedule`) | A | A | D | D | D | D | D | D |
| 7 | Record attendance (`Attendance.Record`) | A | A | D | D | D | D | D | D |
| 8 | Capture minutes / discussion notes (`Minutes.Capture`) | A | A | D | D | D | D | D | D |
| 9 | Finalize / approve MoM (`Minutes.Approve`) | A | A | D | D | D | D | D | D |
| 10 | Open / close voting (`Vote.Manage`) | A | A | D | D | D | D | D | D |
| 11 | Cast vote (`Vote.Cast`) | A | D | A | D | D | D | D | D |
| 12 | Record decision outcome (`Decision.Record`) | A | A | D | D | D | D | D | D |
| 13 | Chairman approve / override decision (`Decision.ChairApprove`) | A | D | D | D | D | D | D | D |
| 14 | Create / assign action (`Action.Create`) | A | A | AiO | D | D | D | D | D |
| 15 | Verify action completion (`Action.Verify`) | A | A | AiO | D | D | D | D | D |
| 16 | Raise / mitigate risk (`Risk.Manage`) | A | A | AiO | AiO | D | D | D | D |
| 17 | Create dependency edge (`Dependency.Create`) | A | A | AiO | AiO | D | D | D | D |
| 18 | Create ADR (`Adr.Create`) | A | A | AiO | AiO | D | D | D | D |
| 19 | Approve ADR (`Adr.Approve`) | A | A | D | D | D | D | D | D |
| 20 | Supersede ADR (`Adr.Supersede`) | A | A | D | D | D | D | D | D |
| 21 | Create invariant (`Invariant.Create`) | A | A | AiO | AiO | D | D | D | D |
| 22 | Approve / activate invariant (`Invariant.Approve`) | A | A | D | D | D | D | D | D |
| 23 | Manage templates (`Template.Manage`) | A | A | D | D | D | A | D | D |
| 24 | Manage docs / wiki (`Document.Manage`) | A | A | AiO | AiO | D | D | D | D |
| 25 | Attach diagram (`Diagram.Attach`) | A | A | AiO | AiO | D | D | AiO | AiO |
| 26 | Run / import research (`Research.Manage`) | A | A | AiO | AiO | D | D | D | D |
| 27 | Manage users / roles (`Admin.Users`) | D | D | D | D | D | A | D | D |
| 28 | Delegate authority (`Auth.Delegate`) | A | A | D | D | D | D | D | D |
| 29 | Read audit log (`Audit.Read`) | A | A | D | D | A | D | D | D |
| 30 | Export reports (`Report.Export`) | A | A | A | A | A | D | AiO | D |
| 31 | Configure system (`Admin.Config`) | D | D | D | D | D | A | D | D |
| 32 | Create/deactivate traceability edge (`Traceability.Link`) | A | A | D | D | D | D | D | D |

**Reading the matrix — key intents.**
- **Process control** (rows 2, 4, 5, 6, 7, 8, 9, 10, 12) is `Secretary`+`Chairman` only. The Secretary runs the loop; the Chairman has parallel authority and chairs.
- **Vote casting** (11) is `Member`/`Chairman` only — `Secretary` *manages* the vote (10) but is **not** a default voter (SoD, §E: vote manager ≠ vote caster where they would be sole counter). The Chairman may cast a vote **and** approve (13), but cannot be the *sole* vote counter (SoD-3, §E).
- **Governance authoring** (18, 21, 24, 26) is `AiO` for Member/Reviewer — they may create/edit artifacts they own; **approval/activation** (19, 20, 22) is restricted to process controllers.
- **Administrator** is deliberately walled off from committee content: it holds only rows 23 (templates), 27, 31 (platform admin). It cannot vote, decide, approve governance, or read decision-specific content beyond what config administration requires. This separates *platform operation* from *committee authority*.
- **Auditor** is read-plus-export only (29, 30) and explicitly `D` on every mutating row.
- **Submitter** can submit (1), edit own submission (3), attach a diagram to own submission (25), and export own reports (30); everything else `D`.
- **Guest/Presenter** can only attach a diagram to the topic they present (25, via Presenter relationship); all process/governance actions `D`. Read access to the specific topic/meeting is granted by the Presenter/Guest scope, not a matrix row.
- **Traceability edges** (32, `Traceability.Link`) — creating/deactivating a typed relationship edge is `Secretary`+`Chairman` only in v1 (P10c). docs/domain/search-and-traceability.md §6.1 additionally envisions the topic **Owner** creating edges on their own topic (`AiO`); that overlay is **deferred** — there is no ABAC resource handler for arbitrary artifact types yet, and no edge-create UI (ASM-P10c-4). *Reading* the traceability panel is committee-wide (any authenticated role, incl. Auditor/Member), enforced only by endpoint authentication, not a matrix row.

---

## D. Per-topic capabilities (relationship-based ABAC overlay)

Per `README` §C, three **relationship capabilities** are attached to a (principal, topic) pair, not granted globally. They are the substrate for every `AiO` cell above. A relationship overlay can **only widen `AiO`→effective-Allow for the specific instance**; it never overrides a global `Deny` and never bypasses SoD or immutability.

| Relationship | Granted by | Confers (on that topic and its child artifacts) | Does **not** confer |
|---|---|---|---|
| **Owner** | Secretary/Chairman on accept; or original submitter promoted on acceptance | Edit the topic (3); create/assign actions (14) on it; raise risks/dependencies (16, 17); author ADR/invariant/doc/research/diagram (18, 21, 24, 25, 26) for it; request scheduling; respond to feedback. | Triage (2), prioritize (4), publish agenda (5), open/close vote (10), record decision (12), chair-approve (13), **verify actions they own** (SoD-1). |
| **Assignee / Contributor** | Owner or Secretary | Contribute edits to assigned sub-artifacts (3, AiO); update assigned actions' progress; add findings/recommendations; attach diagrams (25). | Reassign ownership; approve anything; verify own actions. |
| **Presenter** | Secretary/Owner for a specific agenda item/meeting | Read the topic + its artifacts for the meeting; attach/replace the presentation diagram (25); present during the meeting (no system mutation beyond presentation artifacts). | Vote (unless also Member + eligible), edit topic fields, create actions/decisions. Time-boxed to the meeting window. |

**Overlay precedence:** `effective = RBAC(role) ⟶ if AiO then require matching relationship ⟶ apply ABAC scope ⟶ apply SoD`. Concretely, a `Member` (RBAC=AiO on row 14) may create an action **only on a topic where they are Owner**, **within their stream scope**, and **may not later verify that same action** (SoD-1).

---

## E. ABAC scoping rules

These rules are evaluated by resource-based `AuthorizationHandler`s and constrain every Allow/Allow-if-owner cell. All are auditable; a denied attempt emits an `AuthEvent` (`README` §F audit).

### E.1 Stream scope (`StreamScopeRequirement`)
- Every Topic carries `AffectedStreams` (one or many) and an originating stream. A principal's reach is bounded by their **assigned streams** (claim set), except `Chairman`, `Secretary`, `Auditor`, `Administrator` who are committee-wide.
- A `Member`/`Reviewer`/`Submitter` may act (per their matrix cells) only on topics intersecting their assigned streams.
- **Default visibility of out-of-scope topics is an org policy decision** → **OQ-AUTH-001** (`stream-scope default visibility`): options are *(a) visible-but-read-only across all streams* vs *(b) hidden unless in scope or explicitly shared*. Default recommended: **(a) read-visible, write-scoped** for committee transparency; flagged for org confirmation.

### E.2 Topic confidentiality (`ConfidentialityRequirement`)
- A Topic has a `Confidentiality` facet (`Normal | Restricted`). `Restricted` topics (e.g. security findings, sensitive partner matters) are visible only to: committee `Chairman`/`Secretary`, explicitly granted Members/Reviewers, the topic's Owner/Assignees, and `Auditor` (read). They are **excluded** from default stream-scope visibility and from broad search results for non-grantees.
- Confidentiality narrows visibility **below** stream scope; it never widens it.

### E.3 Delegation / temporary assignment (`DelegationRequirement`)
- `Chairman` and `Secretary` may delegate authority (row 28) to a named principal for a **bounded window** (`ValidFrom`/`ValidTo`) and an explicit capability subset (e.g. "chair meeting MTG-2026-014", "approve MoM while secretary on leave"). Delegations are first-class records, audited, and auto-expire.
- A delegate exercises the delegated policy **only** within the window and scope; the handler checks an active `Delegation` grant. Delegation cannot transfer a capability the delegator lacks, and cannot transfer immutability-bypass (none exists).
- `Guest/Presenter` access is itself a time-boxed grant (expires at meeting end + grace), enforced by the same window mechanism.

### E.4 Segregation of duties (SoD requirements)
Enforced as hard guards; a violation is a `Deny` regardless of role.

| ID | Rule | Rationale | Enforcement point |
|---|---|---|---|
| **SoD-1** | An action's **verifier ≠ its owner/assignee**. (`Action.Verify` denied if principal is the action's owner or the assignee who marked it complete.) | Prevent self-certification of completion. | `ActionVerifyHandler` |
| **SoD-2** | The **MoM approver SHOULD differ from its sole author** where staffing allows; if Secretary both authored and approves, the act is allowed but flagged + audited for review (soft SoD). | Four-eyes on the record of decisions. | `MinutesApproveHandler` (warn+audit) |
| **SoD-3** | The **Chairman cannot be the sole vote-counter**. Vote tallying/closing (`Vote.Manage`) and the recorded count must involve a counter who is not the same principal performing the chairman override on that decision; at least the Secretary (or a second Member) co-attests the tally. | Prevent concentration of "cast + count + override" in one actor (ADR-0010). | `VoteCloseHandler` + `DecisionChairApproveHandler` |
| **SoD-4** | The **decision recorder SHOULD differ from the sole presenter/owner** of the topic being decided (conflict-of-interest); COI is declared and the affected member may be excluded from the eligible-voter set. | Conflict-of-interest handling (brief §4 voting). | `DecisionRecordHandler` + vote eligibility |
| **SoD-5** | `Administrator` is excluded from all committee-content authority (cannot self-grant `Chairman`/voting and act on content). Role-management (row 27) cannot be used to bypass §C denies for the same session's content actions. | Separate platform admin from committee governance. | Role policy composition |

### E.5 Immutability guard (cross-cutting, ADR-0009)
- **Votes** are immutable after `Closed` (`README` §E). No role, including `Chairman`/`Administrator`, may edit a closed vote's ballots or tally; corrections happen by a new vote, recorded as such.
- **Issued decisions** are immutable; a decision is **superseded** by a new decision (never edited). Same for **issued ADRs** (superseded/deprecated).
- This guard sits above RBAC/ABAC: even an Allow cell is rejected if it would mutate an immutable record. Handlers return a domain error, audited.

---

## F. Least-privilege defaults & policy approach (summary)

- **Default-deny.** Absence of an explicit Allow policy = denied.
- **Minimal onboarding role.** Provisioned users default to `Submitter` or `Member` per invitation (ADR-0004, no self-registration). Elevation is explicit, audited, and (for Chairman/Secretary-delegated authority) time-bounded.
- **Resource-based checks** for every per-instance decision (topic/meeting/action/vote), so stream scope, confidentiality, relationship, and SoD are evaluated against the real aggregate.
- **No immutability bypass** for any role.
- **Policies co-located by module** (`README` §B); cross-module composition produces the union matrix in §C.
- **Audit on deny.** Both grants and denials of sensitive actions emit `AuditEvent`/`AuthEvent`.

### Open-question (`OQ-`) candidates flagged here
| OQ ID | Question | Default recommendation |
|---|---|---|
| **OQ-AUTH-001** | Stream-scope **default visibility** of out-of-scope topics: read-visible-everywhere vs hidden-unless-scoped. | Read-visible, write-scoped (committee transparency); confirm with org. |
| **OQ-AUTH-002** | **Guest vs Presenter split**: one global `Guest` role + `Presenter` relationship (current model) vs two distinct global roles. | Keep single `Guest` role + `Presenter` relationship; revisit if external-presenter governance needs differ. |
| **OQ-AUTH-003** | Whether `Reviewer` may ever vote without also holding `Member`. | No (non-voting by default); confirm. |

---

## Traceability
Implements **Deliverable 13**. Roles/modules/IDs/status models from `../README.md` §B/§C/§E/§F; ADR-0004 (identity/onboarding), ADR-0009 (immutability), ADR-0010 (voting). Consumed by `11-domain-model.md` (per-entity *Permissions* rows reference these policies), `12-entity-lifecycles.md` (*Allowed role* columns), `13-workflows.md` (*Permissions* blocks). Concept disambiguation (principle/standard/policy/constraint/invariant/ADR) deferred to `22-standards-and-best-practices.md`. Open questions recorded for `42-open-decisions.md` (OQ-AUTH-001…003).
