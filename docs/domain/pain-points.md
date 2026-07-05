# 03 — Pain-Point Analysis

**Purpose:** Structured catalog of every identified governance failure mode — with severity, root cause, and the specific ACMP module/capability that addresses each — to ensure no pain point is left un-mapped to a product capability.

**Key:** MVP-critical pain points are marked **[MVP]**. All others are addressed in Phase 2 or 3.

---

## Pain-Point Table

### Theme A: Backlog & Data Integrity

| ID | Pain | Symptom / Evidence | Impact | Root Cause | ACMP Module / Capability |
|---|---|---|---|---|---|
| **PAIN-01** [MVP] | **Text-file backlog fragility and concurrency** | Two people editing the file simultaneously corrupt it; no version history; no recovery path. Secretary is the only safe editor. | **High** — data loss risk; secretary bottleneck | Single unstructured file with no locking, no history, no schema | **Topics module** — structured database-backed backlog; concurrent-safe; field-enforced schema |
| **PAIN-02** [MVP] | **No canonical topic ID** | Topics are referenced by title-substring in text; titles change; referencing across MoM and backlog is inconsistent | **Medium** — search/link failures; traceability gaps | No identifier model | Platform runtime ID scheme (`TOP-YYYY-###`); stable IDs from submission onward |
| **PAIN-03** [MVP] | **No enforced topic schema** | Fields missing at submission (type, source, affected streams, risks, dependencies, owner); committee wastes time in meeting extracting basic context | **High** — meeting efficiency loss; decisions made with incomplete info | No intake form or field requirements | **Topics / Backlog module** — structured submission form with required + optional fields; triage step validates completeness |
| **PAIN-04** [MVP] | **No aging or priority visibility** | Old topics sit in the backlog indefinitely with no signal; newest topics always feel more urgent | **Medium** — governance backlog debt; important topics deferred too long | No date-stamping, no priority model, no visual aging | **Backlog module** — age calculated from submission; priority field; aging highlights; DnD prioritization |

---

### Theme B: Traceability & Decision Memory

| ID | Pain | Symptom / Evidence | Impact | Root Cause | ACMP Module / Capability |
|---|---|---|---|---|---|
| **PAIN-05** [MVP] | **No traceability: topic → decision → action → ADR** | Cannot answer "Why did we decide X?" or "What happened after decision Y?" without manually searching emails and MoM documents | **Critical** — governance opacity; compliance exposure; repeated decisions | No relationship model between governance artifacts | **Decisions + Search & Traceability modules** — typed directed relationship graph; artifact IDs linked at creation |
| **PAIN-06** [MVP] | **Lost decision rationale** | Decisions are recorded as outcomes; alternatives considered, conditions, and reasoning are not captured | **High** — same decisions re-debated; context evaporates on team rotation | MoM records verdict, not deliberation | **Decisions module** — structured rationale field, alternatives field, conditions field; mandatory at decision issuance |
| **PAIN-07** | **No ADR practice** | Architecture decisions are not formally recorded as ADRs; invariants are not documented; the architecture is undocumented at the decision level | **High** — knowledge loss; inability to enforce constraints; architecture drift | Practice absent; no tooling | **Governance module** — ADR lifecycle (`Draft→Proposed→Approved→Superseded|Deprecated`); MADR-lite template; link from `DECN-` to `ADR-`; Architecture Invariants (`AIV-`) |
| **PAIN-08** | **Cross-stream dependency blindness** | Decision on Stream 2 has implications for Stream 4; this surfaces only in verbal discussion (or not at all) | **High** — integration failures; blocked work discovered late; rework | No structured dependency model | **Dependencies module** — typed dependency edges (`DPN-`); graph traversal; impact analysis |
| **PAIN-09** | **No architecture backlog discipline** | No way to see which topics are queued for which stream, which have unresolved dependencies, or which are blocking other work | **Medium** — stream coordination failures; wasted meeting time | Text file has no query capability | **Backlog module** — filter by stream, type, status, age, dependency status; blocked/blocking indicator |

---

### Theme C: Voting & Audit Integrity

| ID | Pain | Symptom / Evidence | Impact | Root Cause | ACMP Module / Capability |
|---|---|---|---|---|---|
| **PAIN-10** [MVP] | **No immutable vote record** | Individual votes are not recorded; only the outcome (if that) is noted; "who voted no and why" is unrecoverable | **Critical** — governance and compliance exposure; disputed decisions unresolvable | No voting system; verbal tally only | **Voting module** — eligible voters, quorum, abstentions; always attributed (anonymity out of scope for v1); immutable after close (`VOTE-`); ADR-0009/0010 |
| **PAIN-11** [MVP] | **Chairman override not formalized** | Chairman's final approval or override is not distinguished from regular votes in the MoM; authority is exercised but not audited | **High** — accountability gap; unclear decision authority in record | No formal override mechanism | **Voting module** — chairman approval/override recorded as explicit event with timestamp and actor; ADR-0010 |
| **PAIN-12** [MVP] | **No audit log** | No record of who changed what, when; no immutable history of committee events | **Critical** — inability to reconstruct history; compliance failure for a national-scale gov system | No append-only event log | **Audit & Records module** — append-only audit log for all state changes; votes and issued decisions immutable; ADR-0009 |
| **PAIN-13** | **No quorum enforcement** | Quorum for valid votes is undefined and unenforced; decisions made with low attendance may be challenged | **Medium** — decision legitimacy risk | No voting configuration model | **Voting module** — configurable quorum threshold; meeting marks vote invalid if quorum not reached |

---

### Theme D: Meeting & MoM

| ID | Pain | Symptom / Evidence | Impact | Root Cause | ACMP Module / Capability |
|---|---|---|---|---|---|
| **PAIN-14** [MVP] | **Manual MoM preparation** | Secretary writes MoM from memory/notes after the meeting; takes hours; error-prone; time-delayed (24–48h) [unverified] | **High** — secretary burden; information decay; errors introduced | No system to capture meeting events as they happen | **Meetings module** — attendance, real-time notes, vote results, decisions all recorded in-system; MoM auto-drafted from these records; human review before publish |
| **PAIN-15** [MVP] | **MoM is not machine-readable** | MoM is a Word document; cannot be queried, linked, or used as a structured data source | **High** — information silo; decisions buried in prose | Word format chosen for human readability only | **Meetings module** — MoM stored as structured record (Markdown + structured fields); human-readable and machine-readable; versioned |
| **PAIN-16** | **No carry-over mechanism** | Topics not completed in one meeting are noted informally; may slip through to next meeting or be forgotten | **Medium** — topic loss; meeting debt accumulates | No formal carry-over or scheduling model | **Agenda module** — explicit carry-over status; topics deferred automatically returned to scheduled state for next meeting |
| **PAIN-17** | **No attendance record** | Who was present, absent, or excused is not formally recorded; impacts quorum validation and accountability | **Medium** — quorum disputes; accountability gaps | No in-system meeting record | **Meetings module** — attendance tracking (present / absent / excused / late); linked to quorum calculation |

---

### Theme E: Action Tracking & Escalation

| ID | Pain | Symptom / Evidence | Impact | Root Cause | ACMP Module / Capability |
|---|---|---|---|---|---|
| **PAIN-18** [MVP] | **No action tracking system** | Actions from meetings live only in MoM; no owner is notified; no due date is enforced; completion is confirmed (or not) verbally at the next meeting | **Critical** — decisions not acted on; committee's authority undermined; national-scale consequences if deferred | No action management system | **Actions module** — `ACT-` entities; owner, due date, status (`Open→InProgress→Blocked→Completed→Verified`); reminders via Hangfire; ADR-0005 (Notification) |
| **PAIN-19** | **No escalation path** | Overdue actions have no formal escalation; Secretary manually chases; status unknown to committee until the next meeting | **High** — accountability gap; decisions stall in execution | No escalation model | **Actions module + Notifications module** — configurable escalation rules; `Overdue` derived status; escalation notification to secretary + chairman |
| **PAIN-20** | **Actions not linked to decisions** | An action created from a decision cannot be traced back to it; no way to confirm all conditions on a `ConditionallyApproved` decision have been resolved | **High** — governance loop incomplete; conditional approvals silently lapse | No relationship between decision and action entities | **Actions + Traceability modules** — `ACT-` linked to `DECN-` at creation; conditions on `ConditionallyApproved` mapped to actions; completion triggers governance closure |

---

### Theme F: Knowledge & Onboarding

| ID | Pain | Symptom / Evidence | Impact | Root Cause | ACMP Module / Capability |
|---|---|---|---|---|---|
| **PAIN-21** | **Knowledge loss on rotation** | When committee members or stream directors change, governance history, rationale, and context are not transferable | **High** — new members cannot understand past decisions; same debates recur | No searchable knowledge base; decisions not in a retrievable form | **Knowledge module (Phase 3)** — wiki/docs; ADR repository; search; traceability all make history discoverable |
| **PAIN-22** | **Onboarding/handover loss** | No structured onboarding package for new committee members; they rely on verbal briefing from the Secretary | **Medium** — ramp-up time; context dependency on Secretary | No documentation system | **Knowledge + Governance modules** — ADR repository, architecture invariants, and topic history serve as structured onboarding material |
| **PAIN-23** | **Secretary single point of failure** | All backlog knowledge, process knowledge, and governance history reside primarily with one person | **Critical** — if Secretary is unavailable, committee function degrades significantly | System-of-record is a text file controlled by one person | **ACMP as a whole** — all information in a shared, role-accessible system eliminates the personal knowledge monopoly |

---

### Theme G: Reporting & Metrics

| ID | Pain | Symptom / Evidence | Impact | Root Cause | ACMP Module / Capability |
|---|---|---|---|---|---|
| **PAIN-24** [MVP] | **No metrics or dashboards** | Committee has no visibility into: backlog age, decision throughput, open action count, topic resolution rate, SLA compliance | **High** — committee cannot self-assess; leadership has no governance health signal | No data collection; no reporting layer | **Reporting module** — dashboards for backlog health, decision throughput, action status, open risks, SLA compliance |
| **PAIN-25** | **No risk visibility** | Risks identified in committee discussions are noted in MoM but not tracked; no risk register | **High** — governance risk blindspot; risks not mitigated | No risk management practice | **Risks module** — `RSK-` entities; status (`Open→Mitigating→Closed`); owner; linked to topics/decisions |

---

### Theme H: Language & Accessibility

| ID | Pain | Symptom / Evidence | Impact | Root Cause | ACMP Module / Capability |
|---|---|---|---|---|---|
| **PAIN-26** [MVP] | **Bilingual inconsistency** | Committee operates in Arabic and English; MoM and materials are sometimes one language, sometimes mixed; terminology inconsistent across documents | **Medium** — professional/governance quality concern; confusion on bilingual terms | No bilingual tooling; manual document management | **Platform (Shared Kernel)** — EN/AR first-class via `react-i18next`; RTL layout; bilingual terminology fixed in glossary; ADR-0012 |

---

## Summary: MVP-Critical Pain Points

The following pain points block the core committee loop and must be addressed in Phase 1 (MVP):

`PAIN-01, PAIN-02, PAIN-03, PAIN-04, PAIN-05, PAIN-06, PAIN-10, PAIN-11, PAIN-12, PAIN-14, PAIN-15, PAIN-18, PAIN-23, PAIN-24, PAIN-26`

Remaining pain points (`PAIN-07, PAIN-08, PAIN-09, PAIN-13, PAIN-16, PAIN-17, PAIN-19, PAIN-20, PAIN-21, PAIN-22, PAIN-25`) are addressed in Phase 2 (governance expansion) or Phase 3 (research/knowledge) — they are real and tracked, not deferred indefinitely.

---

*Traceability: Pain points in this document directly drive `docs/requirements/functional.md` (each FR traces to ≥1 PAIN-##), `docs/planning/work-breakdown.md`, and `docs/domain/user-stories-mvp.md`. The MVP-critical set governs Phase 1 scope in `docs/planning/roadmap.md`. Root causes and the AS-IS process they stem from are documented in `docs/domain/current-state.md`.*
