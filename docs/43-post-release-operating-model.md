# 43 — Post-Release Operating Model (Deliverable 59)

**Purpose:** Defines how ACMP is operated, supported, and continuously improved after PH-1 launch — covering product ownership, support/maintenance cadence, the committee's day-to-day operating model *with* the platform, data governance, user/role administration, adoption and training, feedback loop, and the platform's own self-governance (dogfooding).

---

## 1. Product Ownership of ACMP

### 1.1 Who owns the product

**Product Owner (PO):** The lead Secretary. The Secretary is the domain authority, acts as PO for the backlog, prioritizes enhancement requests, and has final say on feature scope before it reaches the Architecture Committee for governance decisions.

**Technical Owner:** The Tech Lead who delivered PH-1. Responsible for architectural integrity, technical debt decisions, and escalating infrastructure concerns.

**Executive Sponsor:** The Chairman (VP). Signs off on major capability expansions (PH-2/PH-3) and any change to the platform's self-governance rules (see §8).

### 1.2 ACMP backlog ownership

- Enhancement requests and defect reports enter the ACMP product backlog, triaged by the Secretary (PO).
- Requests from committee members arrive via in-app feedback or direct communication to the Secretary; external requests (from stream owners who use ACMP as submitters) follow the same intake path.
- The PO grooms the backlog bi-weekly, prioritizing against the phased roadmap (`docs/36-roadmap.md`) and open decisions (`docs/42-open-decisions.md`).
- Any ACMP feature that changes committee governance rules (e.g., adding a new topic type, changing quorum rules) must itself go through the committee as a topic before implementation (§8 — dogfooding).

---

## 2. Support and Maintenance

### 2.1 Support tiers

| Tier | Scope | Handled By | Response SLA |
|---|---|---|---|
| T1 — User support | Login issues, navigation questions, role access, bilingual UI questions | Secretary | 1 business day |
| T2 — Application defects | Functional bugs, incorrect data, notification failures, search issues | Tech Lead + Backend Engineer | Sev-1: 4h; Sev-2: 1 business day; Sev-3: next sprint |
| T3 — Infrastructure/platform | Docker Compose failures, SQL Server, MinIO, Seq, self-hosted Keycloak (ACMP-owned — ADR-0015) issues | Tech Lead (+ org infra team for host VM only) | Sev-1: 2h; Sev-2: 4h |

**Severity definitions:**
- **Sev-1:** Platform inaccessible or data corruption/immutability breach detected; governance loop blocked.
- **Sev-2:** Major feature unavailable (e.g., voting broken, notifications not delivering) but workaround exists; affects a committee meeting in progress or within 24h.
- **Sev-3:** Non-critical defect; cosmetic issue; minor localization error; dashboard count mismatch.

### 2.2 Maintenance windows

- Scheduled maintenance (migrations, upgrades): Sundays 00:00–04:00 local time.
- Changes to production require: a tested migration script (EF Core), a verified backup from the prior evening, a rollback plan, and a change record in the ACMP committee backlog.
- No unplanned changes to production schema or Docker Compose config outside a maintenance window, unless Sev-1 in progress.

### 2.3 Monitoring and alerting

- **Serilog → Seq:** Structured log stream; error-rate alerts configured in Seq for HTTP 5xx spikes, authentication failures, and audit-log write failures.
- **ASP.NET health checks:** `/health/live` and `/health/ready` polled every 30 seconds by the Docker Compose healthcheck directive; alert on consecutive failures.
- **Hangfire dashboard:** Checked by the Secretary or Tech Lead on the first business day of each week; failed or stalled jobs are investigated and resolved.
- **Availability target:** 24×7 / 99.9% (per `README.md §A`); achieved via single-node deployment with nightly backup + restore tested monthly (no HA cluster; right-sized for ≤20 users).

---

## 3. Release Cadence and Change Management

### 3.1 Release cadence

| Phase | Release cadence | Branch strategy |
|---|---|---|
| PH-1 active development | 2-week sprints; sprint-end releases to staging; production monthly after secretary UAT | Feature branches → `main`; tag `v1.x.y` on production release |
| Post-PH-1 maintenance | Monthly patch releases for Sev-2/Sev-3 fixes; quarterly minor releases for enhancements | Same |
| PH-2 / PH-3 development | Parallel feature branches; merged to `main` only when AC-### criteria verified | Same |

### 3.2 Change management process

1. **Request:** Enhancement or defect reported to Secretary (PO).
2. **Triage:** Secretary adds item to ACMP product backlog with priority, phase, and FR/US reference if applicable.
3. **Governance gate (if scope-changing):** Any change that alters committee rules, data model, or user roles is submitted as a committee topic (dogfooding — §8).
4. **Development:** Tech Lead confirms feasibility; assigned engineer implements in a feature branch.
5. **Testing:** QA runs AC-### tests; security reviewer verifies no new ASVS L2 gaps introduced.
6. **Staging UAT:** Secretary confirms acceptance criteria met.
7. **Release:** Secretary signs off; release applied in maintenance window; release notes distributed via in-app notification to all users.
8. **Post-release monitoring:** 48h watch period; Sev-1/Sev-2 issues trigger immediate rollback per the runbook.

---

## 4. Committee Operating Model With the Tool

This section defines how the Architecture Committee uses ACMP as its primary governance system, replacing the text-file process.

### 4.1 Intake SLA (topic submission to triage decision)

| Urgency | SLA (submission → Triage decision) | Owner |
|---|---|---|
| Critical | ≤ 1 business day | Secretary + Chairman |
| Urgent | ≤ 3 business days | Secretary |
| Normal | ≤ 7 business days (or next weekly triage session) | Secretary |

- Critical topics trigger immediate Secretary notification; the Chairman is cc'd.
- If the SLA is missed, the aging indicator fires (FR-038, AC-057) and a notification escalates to the Chairman.

### 4.2 Agenda preparation cadence

**Weekly cadence (default):**
| Day | Activity |
|---|---|
| Tuesday | Secretary reviews Prepared topics and confirms candidates for Friday's meeting |
| Wednesday | Secretary builds and publishes the agenda in ACMP; committee receives in-app notification |
| Thursday | Presenters finalize their materials and attach to topic in ACMP |
| Friday | Meeting conducted; decisions, votes, and actions recorded in ACMP during/after |

**Bi-weekly option (if committee switches cadence):**
- Same rhythm but on alternating weeks; the Wednesday-to-meeting gap extends to 8 days.
- The secretary must publish the agenda at least 5 business days before the meeting when bi-weekly.

**Agenda rules enforced by the system:**
- Only topics in Prepared or Scheduled status are eligible for agenda inclusion.
- The system auto-suggests carry-over items from the prior meeting (FR-048).
- Total time-box sum must not exceed meeting duration (system warns but does not hard-block).

### 4.3 MoM turnaround

| Step | Responsible | Target turnaround |
|---|---|---|
| Draft MoM generated and completed by Secretary | Secretary | Within 24h of meeting end |
| Reviewer(s) annotate / approve | Reviewer/Chairman | Within 48h of draft submission |
| Chairman final approval | Chairman | Within 72h of meeting end |
| Published MoM distributed (in-app notification) | System | Immediately on approval |

- Secretary is responsible for completing the draft within 24h.
- If the Chairman approval SLA (72h) is missed, a Hangfire job sends an escalation notification.
- Corrections after publication create a new version (not an in-place edit) — see AC-036.

### 4.4 Decision publication and record

- All issued decisions are immediately visible in ACMP to all committee members upon the Chairman's ratification.
- The Secretary is responsible for ensuring downstream artifacts (Actions, Risks) are linked before Issuing the decision (FR-067, AC-029).
- The decision's conditions (for ConditionallyApproved outcomes) are tracked as separate `DecisionCondition` records; the Secretary monitors their completion and marks conditions Met when confirmed.

### 4.5 Action follow-up cadence

| Cadence | Activity |
|---|---|
| Daily (automated) | Hangfire jobs compute Overdue status; send reminders N days before due date (default 3d) |
| Weekly (meeting open) | Secretary reviews actions dashboard at the start of each meeting; overdue and blocked actions discussed |
| Per meeting | Action owners report progress verbally; Secretary updates status in ACMP during or immediately after |
| Bi-weekly | Secretary runs the actions dashboard export and presents open/overdue count trend to Chairman |

### 4.6 Secretary's weekly routine

| Day | ACMP Task |
|---|---|
| Monday | Open ACMP secretary dashboard; review triage queue; process new submissions; check aging indicators |
| Tuesday | Review Prepared topics for upcoming meeting; confirm carry-over items; draft agenda in ACMP |
| Wednesday | Publish agenda; notify committee (in-app auto-triggered); contact presenters for materials |
| Thursday | Final materials check (attachments on topics); confirm attendance list setup |
| Friday (meeting day) | Record attendance; capture live notes per agenda item; open/close votes; record decisions; create actions |
| Friday (post-meeting) | Begin MoM draft; link all decisions to downstream artifacts; update action status |
| Monday (following) | Follow up on outstanding MoM approval; check escalation queue; process any new submissions |

---

## 5. Data Governance and Retention Administration

### 5.1 Data governance roles

| Role | Data responsibility |
|---|---|
| Secretary | Owns the completeness and accuracy of topic records, MoMs, and action records |
| Chairman | Owns decision records (final authority); ensures all chairman actions are recorded by name |
| Administrator | Owns platform configuration, user accounts, and retention policy settings |
| Auditor | Oversight of audit log completeness; periodic review of immutability integrity |
| Tech Lead | Owns backup/restore cadence, migration integrity, and schema documentation |

### 5.2 Retention policy (v1)

Per `README.md §A` (resolved 2026-06-24): **keep all records; no automatic purge in v1; retention is configurable.**

- In v1, the default retention setting is "retain indefinitely."
- The Administrator can configure retention class periods via the admin UI (e.g., "audit log: 7 years"), but no automatic purge job runs in v1 — the setting is recorded for future implementation.
- Immutable records (votes, issued decisions, ADRs, published MoMs) cannot be purged by any retention job; they can only be archived (made read-only).
- Legal hold: if a legal or compliance hold is placed on a record, the Administrator marks it as "held" and the Hangfire retention job (when activated in a future phase) skips it.

### 5.3 Backup and restore

- **Frequency:** Nightly automated backup of SQL Server database and MinIO object store.
- **Retention:** Last 30 daily backups retained on-prem; off-site copy on another volume (org policy).
- **Testing:** Monthly restore test on a staging environment; Tech Lead documents the test result.
- **RTO target:** ≤ 4 hours (restore from backup to operational); data loss ≤ 24 hours (nightly backup RPO).
- Backup and restore procedure documented in the operational runbook (`execution-handoff/`).

### 5.4 Data quality responsibilities

- The Secretary is responsible for ensuring topics have complete required fields before progressing them through the lifecycle.
- The Secretary reviews and resolves any orphan artifacts (actions with no topic link, decisions with no outcome) on a monthly basis.
- The Administrator conducts a quarterly user account review: deactivate accounts for users who have left, verify stream assignments are current.

---

## 6. User and Role Administration (Keycloak Claims + ACMP Membership)

### 6.1 Role provisioning flow

1. New employee/contractor is provisioned in Keycloak by the org's identity team.
2. Org identity team adds the user to the correct Keycloak group/realm-role corresponding to their ACMP role (Chairman, Secretary, Member, etc.).
3. ACMP Administrator creates the ACMP user account (name, email, stream assignment) via the ACMP admin UI (FR-017).
4. On first login, Keycloak claims are consumed; ACMP assigns the mapped role; the user receives an in-app welcome notification.

**No self-registration:** Users cannot create their own ACMP accounts (FR-016, ADR-0004).

### 6.2 Role change process

1. A role change (e.g., Member → Secretary) is requested by the Chairman or existing Secretary to the org identity team.
2. The org identity team updates the Keycloak group/role assignment.
3. On the user's next login (or token refresh), ACMP picks up the updated claims and re-maps the role.
4. The ACMP Administrator may need to update stream assignments in ACMP if scope changes.
5. The role change is recorded in the audit log (`UserRoleMapped` event).

### 6.3 Deactivation process

1. When a committee member leaves, the Secretary notifies the ACMP Administrator.
2. Administrator deactivates the ACMP account (FR-020); the user can no longer log in.
3. Org identity team removes the user's Keycloak group membership.
4. Historical records (votes, authorship, action assignments) remain attributed to the deactivated user.
5. Any open actions owned by the deactivated user must be reassigned (Secretary manually reassigns via ACMP).

### 6.4 Guest/Presenter access (PH-2)

*Not available in PH-1.* In PH-2, Guest/Presenter accounts will be provisioned with a time-boxed Keycloak account (expiry = meeting date + grace period) by the Administrator, scoped to a single topic/meeting per FR-023.

---

## 7. Adoption and Training

### 7.1 Go-live rollout

**PH-1 go-live approach (phased, pilot-first):**
1. **Pilot run (week 1 post-deploy):** Secretary and one Member conduct a full end-to-end cycle on staging (topic submission → triage → agenda → meeting → vote → decision → action). Document any usability issues.
2. **Soft launch (week 2):** All committee members given access; first real committee meeting run on the platform in parallel with the text file (for the first meeting only, as a safety net).
3. **Hard cutover (week 3+):** Text file process retired; ACMP is the sole system of record.

### 7.2 Training materials (EN + AR)

| Material | Format | Language | Owner | Timing |
|---|---|---|---|---|
| Quick-start guide (role-based, 1 page per role) | PDF | EN + AR | Secretary | Before go-live |
| Walkthrough video: Secretary workflow | Screen recording | EN | Tech Lead | Before go-live |
| Walkthrough video: Member/voter workflow | Screen recording | AR (primary audience) | Tech Lead + AR reviewer | Before go-live |
| In-app guided tour (first login) | Interactive overlay | EN + AR | Frontend Engineer | At go-live |
| Q&A session (30 min, all committee) | Live session | EN + AR | Secretary | Week 1 |

Training materials stored in ACMP's Knowledge module (wiki, PH-2) or as shared links (PH-1).

### 7.3 Ongoing training

- New committee members receive a role-based orientation from the Secretary within their first week.
- When PH-2 features launch (ADRs, diagrams, Webex), a new training session is held.
- Bilingual training materials are updated within 2 weeks of any significant feature release.

---

## 8. Continuous Improvement Loop

### 8.1 Feedback intake

- **In-platform feedback:** A feedback link/button in the app header allows any user to submit a free-text comment; submitted to the Secretary's ACMP product backlog as a Submitter-role topic.
- **Post-meeting retro:** At the end of each committee meeting (monthly cadence), the last 3 minutes are reserved for brief platform feedback from committee members.
- **Quarterly review:** Secretary and Tech Lead review KPI metrics (action completion rate, topic-to-decision days, backlog aging, MoM turnaround time) and identify improvement areas.

### 8.2 Feedback → backlog → release loop

1. Feedback collected via in-platform form or verbal retro.
2. Secretary triages into: defect (→ T2 support), enhancement (→ product backlog), governance-rule change (→ committee topic per §8.3).
3. PO prioritizes backlog; Tech Lead estimates; items scheduled into sprint/release per §3.
4. Released change communicated to all users via in-app notification.
5. Post-release: Secretary confirms the improvement achieved its intent at the next quarterly review.

### 8.3 Platform self-governance (dogfooding)

**ACMP's own architecture and significant configuration changes are governed by the Architecture Committee using ACMP itself.**

This applies to:
- Adding a new module or major capability (e.g., introducing the Tarseem diagram sidecar for PH-2).
- Changing the canonical role model, status lifecycle, or permission matrix.
- Introducing an AI-assisted extraction feature (PH-3, OWASP LLM01 risk).
- Changing the technology stack (e.g., adding a second datastore, introducing a message broker).
- Modifying retention policy rules.

**Process:** The Secretary submits a topic in ACMP (type = `GovernanceStandardization` or `ArchitectureDecision`), the committee reviews it through the normal governance loop (triage → agenda → meeting → decision → ADR if warranted), and the resulting decision is the authority for implementation. The implementation team then builds to that decision. The platform is thus subject to the same governance discipline it enforces for the rest of the organization's architecture decisions.

---

## Traceability

- Implements **Deliverable 59** (Post-release governance & operating model).
- Operational cadences align with workflow definitions in `docs/13-workflows.md` (W1–W25).
- Data retention rules per `docs/26-audit-and-records-management.md` and `README.md §A` (resolved 2026-06-24).
- Role/admin flows per `docs/10-permission-role-matrix.md §B/F`; Keycloak identity per ADR-0004.
- Notification cadences per `docs/29-notification-strategy.md` and FR-083, FR-084.
- Self-governance (§8.3) principle reinforces `docs/05-product-vision-and-principles.md` principle 6 (auditable) and principle 8 (progressive delivery).
- Backup/restore and availability target per `README.md §A` (24×7/99.9%, right-sized via simple redundancy).
