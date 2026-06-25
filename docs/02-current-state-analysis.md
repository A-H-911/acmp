# 02 — Current-State Analysis

**Purpose:** Precise AS-IS mapping of the Architecture Committee's process, artifacts, data model, and cadence — to establish the baseline ACMP must replace, and to identify what is working and must be preserved.

---

## 1. AS-IS Process Narrative

The committee runs on a weekly cadence. The Secretary maintains a shared text file that serves as both backlog and the primary record of topics. Before each meeting, the Secretary (often in consultation with the Chairman or Technical Directors) selects 1–N topics from the backlog for that session. Presenters prepare materials independently (slides, diagrams, documentation) and share them through ad-hoc channels.

The meeting itself is conducted in Webex (or in-person). A topic is presented, discussed, voted on (or decided by the Chairman directly), and then the outcome is verbally recorded and later written into the MoM. The Secretary compiles the MoM manually after the meeting, based on notes and memory. Decisions, actions, and deferred items are then reflected back into the text-file backlog by hand.

There is no formal ADR process. Follow-up actions are noted in the MoM but tracked informally — verbally confirmed at the next meeting. There is no structured way to track action completion, escalate overdue items, or link decisions to their downstream consequences.

---

## 2. AS-IS Process Step Table

| Step | Who | Input | Output | Where it lives today | Gap |
|---|---|---|---|---|---|
| **1. Topic submission** | Stream staff, committee members, Chairman/VP | Identified need, incident, innovation, regulatory requirement | Topic added to backlog | Text file (shared, no version control) | No formal intake form; no fields enforced; no ID; submitter not tracked; no status |
| **2. Backlog maintenance** | Secretary | Updated topic notes, statuses, decisions | Current backlog text file | Text file | Concurrency: one editor at a time; no history; no priority model; no aging signal |
| **3. Weekly topic selection** | Secretary (+ Chairman input) | Backlog text file | List of topics for this meeting's agenda | Verbal / ad-hoc | No documented selection criteria; no scheduling of presenter; no time-box allocation |
| **4. Presenter preparation** | Topic owner / assigned member | Topic context, research, diagrams | Slides, diagrams, documents | SharePoint / email / personal drives | No central attachment store; no link from topic to materials; no version tracking |
| **5. Meeting (presentation)** | Presenter, Members, Chairman | Prepared materials | Discussion, feedback | Webex / in-person | No in-system attendance record; no structured notes during meeting |
| **6. Voting / Chairman decision** | Eligible members, Chairman | Discussion outcome | Vote result + Chairman approval | Verbal / manual tally | No record of individual votes; no quorum check; no abstention tracking; no immutable record |
| **7. Decision and outcome recording** | Secretary (post-meeting) | Notes, memory | Decision entry in MoM | MoM document (Word/email) | Manual; not machine-readable; rationale rarely captured; conditions not formalized |
| **8. Action creation** | Secretary / Chairman | Decision, follow-up needs | Action items noted in MoM | MoM document | No owner tracking; no due dates enforced; no progress tracking; no escalation |
| **9. MoM preparation** | Secretary | Meeting notes, votes (recalled), decisions, actions | Minutes of Meeting document | Word document / email | Entirely manual; error-prone; time-delayed (often prepared 24–48h post-meeting) |
| **10. MoM approval / distribution** | Chairman (approval), Secretary (distribution) | Draft MoM | Approved MoM shared to members | Email distribution | No formal versioning; no acknowledgement tracking; no audit of changes between draft and approved |
| **11. Backlog update** | Secretary | MoM decisions, new topics, deferred items | Updated backlog | Text file | Manual; no link from backlog entry to MoM or decision record |
| **12. Action follow-up** | Secretary (chase), Action owners | Open actions from prior MoM | Verbal status update at next meeting | Next week's verbal report | No system tracking; no reminders; no escalation; completion not formally verified |
| **13. (Absent) ADR creation** | — | Approved architecture decision | Architecture Decision Record | Does not exist | No ADR practice; decision rationale permanently lost once meeting closes |

---

## 3. AS-IS Artifact Inventory

| Artifact | Exists today? | Format | Where it lives | What's lost or missing |
|---|---|---|---|---|
| **Topic backlog** | Yes | Unstructured text file | Shared drive (secretary-managed) | No IDs, no status model, no history, no search, no concurrent editing |
| **Agenda** | Informally | Verbal / ad-hoc note | Secretary's head / chat message | Not published; no time-boxes; no formal link to backlog topics |
| **Meeting materials** | Yes | Slides, diagrams (various formats) | Personal drives / email / SharePoint | Not linked to topics; no version control; scattered across individuals |
| **Voting record** | No | — | — | Entirely absent; vote counts and individual positions unrecorded |
| **Minutes of Meeting (MoM)** | Yes | Word document | Email distribution / SharePoint | Manual; delayed; rationale thin; not machine-readable; no formal versioning |
| **Decisions record** | Partial | MoM section | Inside MoM document | Not queryable; not linked to topic; no conditions/alternatives captured |
| **Action items** | Partial | MoM section | Inside MoM document | Not tracked system-side; no status updates; completion unverified |
| **ADRs** | No | — | — | Practice does not exist |
| **Risk register** | No | — | — | Practice does not exist in committee context |
| **Dependency map** | No | — | — | Not tracked; cross-stream dependencies surfaced only verbally |
| **Audit log** | No | — | — | No immutable record of any committee event |
| **Diagrams** | Ad hoc | Various (Visio/Draw.io/slides) | Personal drives | Not version-controlled; not linked to decisions or topics |
| **Research/rationale** | Ad hoc | Email / attached docs | Email threads / personal drives | Lost after decision; not linked to outcome |

---

## 4. Current Topic Fields

Based on digest §3 (current/needed fields observed in practice and elicited from the Secretary):

| Field | Status |
|---|---|
| Title | Present in text file |
| Description | Partial — often a short phrase only |
| Scope | Informal / absent |
| Type | Absent (no taxonomy enforced) |
| Source | Absent |
| Created / target / scheduled dates | Absent |
| Status | Informal (no canonical model) |
| Priority | Absent |
| Owner | Sometimes noted |
| Assignees | Absent |
| Affected streams | Sometimes noted |
| Affected systems/services | Sometimes noted |
| Dependencies | Absent |
| Risks | Absent |
| Notes / comments / feedback | Mixed in with description |
| Supporting research | Not linked — separate docs if they exist |
| Attachments / presentations / diagrams | Not linked — separate files |
| Decisions | Recorded in MoM, not in backlog entry |
| Voting results | Not recorded |
| Follow-up actions | In MoM, not linked to backlog |
| Due dates / progress | Absent |
| Related ADRs | Absent (ADRs don't exist) |
| Related architecture invariants | Absent |

---

## 5. Current Decision Outcomes in Practice

The committee uses an informal vocabulary for decisions. The canonical model (README §E) formalizes and extends what is currently applied verbally:

| Current informal usage | Canonical outcome (README §E) |
|---|---|
| "Approved" | `Approved` |
| "Approved with conditions" | `ConditionallyApproved` |
| "Rejected" | `Rejected` |
| "Need more information" | `MoreInfoRequired` |
| "Committee has feedback, revise and resubmit" | `FeedbackProvided` |
| "Needs more design work" | `DesignChangesRequired` |
| "Needs enhancements before re-review" | `EnhancementsRequired` |
| "Should be researched first" | `ResearchRequired` |
| "Defer to later" | `Deferred` |
| "Escalate outside committee" | `Escalated` |
| "Convert this to a different topic type or execution track" | `Converted` |

All of these outcomes occur today but none are recorded in a structured, queryable form.

---

## 6. Current Cadence

| Parameter | Current state | Under consideration |
|---|---|---|
| Frequency | Weekly | Bi-weekly (under active discussion) |
| Duration | [unverified — not specified in source material] | — |
| Topics per session | 1–N (unspecified upper bound) | — |
| Preparation lead time | Ad hoc | — |
| MoM turnaround | 24–48 h post-meeting [unverified] | — |

ACMP must support both weekly and bi-weekly cadence without configuration changes — meeting scheduling is flexible.

---

## 7. What Works Today (Preserve, Don't Break)

The current process has genuine strengths that ACMP must preserve, not paper over:

| Strength | Why it works | ACMP obligation |
|---|---|---|
| **Lightweight submission** | Anyone with a need can raise a topic informally. Low friction gets issues to the committee quickly. | Submission must remain low-friction — a short form, not a bureaucratic intake process. |
| **Chairman authority** | The VP/Chairman has clear final-approval authority, including override. This concentrates accountability and enables rapid decisions. | ACMP models chairman override explicitly as a distinct, auditable action — not collapsed into a majority vote. See ADR-0010. |
| **Voting as a legitimacy signal** | Even informal voting creates group buy-in and surfaces disagreement before a decision is finalized. | Voting must be easy, configurable, and produce an immutable record — not add friction. |
| **Weekly rhythm** | Regularity keeps the backlog moving and prevents topic stagnation. | Meeting scheduling and agenda building must support both weekly and bi-weekly cadences with carry-over handling. |
| **Oral culture / discussion quality** | Committee members are comfortable with verbal discussion and do not want a system that replaces judgment with process. | ACMP records and structures; it does not prescribe discussion. Structured fields capture what the committee decides, not how it deliberates. |

---

*Traceability: This document is the AS-IS baseline. It directly drives `docs/03-pain-points.md` (gap analysis) and `docs/07-functional-requirements.md` (TO-BE functional model). The canonical topic field list here informs `docs/11-domain-model.md`. Current decision outcomes map to README §E canonical status model. Process steps map to workflow definitions in `docs/13-workflows.md`.*
