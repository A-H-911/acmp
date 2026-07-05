---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Project Charter — ACMP

The Architecture Committee Management Platform (ACMP) charter: the problem, objectives, goals/non-goals, scope, success metrics, and stakeholders. Scope was locked with the Secretary on 2026-06-24; changes after lock require a recorded decision (`DEC-`/ADR). This charter defers to the registers under [requirements/](requirements/) and [decisions/](decisions/) for detail.

## Problem statement

The organization's Architecture Committee governs architecture decisions through a **text-file backlog and ad-hoc documents**. There is no single, auditable system of record: topics, agendas, meetings, minutes, votes, decisions, ADRs, actions, risks, and dependencies live in disconnected artifacts, with no traceability from a need to its decision to its follow-up, no immutability guarantees on votes/decisions, and no bilingual (EN/AR) support. This makes governance slow, hard to audit, and easy to drift. Full context: [domain/current-state](domain/) and [domain/pain-points](domain/).

## Objectives

- Be the **single system of record** for one Architecture Committee: topic intake → backlog → agenda → meeting → minutes → voting → decision → ADR → action → risk → dependency, with end-to-end traceability.
- Make the sensitive records **auditable and immutable** (votes, issued decisions, approved ADRs, published minutes; hash-chained).
- Be **bilingual and RTL-first** (EN + AR), accessible (AA), and usable by ≤20 committee members on-prem.
- Replace the text-file process without becoming generic project management — **architecture governance, not PM**.

## Goals and non-goals

### Goals
- A complete, live governance loop with attributed voting and chairman approval recorded by name.
- Policy-based authorization (role + per-topic ABAC) with segregation of duties.
- Self-contained on-prem deployment (all runtime deps bundled; zero external runtime services in v1).
- First-class traceability: every requirement → decision → work → test → risk → acceptance.

### Non-goals (anti-goals)
- Not generic project/sprint management (no boards, velocity, capacity planning).
- Not a diagram engine (Tarseem renders; the JSON spec is the source of truth).
- Not a research-methodology engine (Keystone is the optional companion).
- Not a meeting/video platform, mobile-native app, BPM engine, or public portal.

## Scope

### In scope
The PH-1 governance loop across Platform, Membership, Topics, Meetings, Decisions, Voting, Actions, Risks, Dependencies, Notifications (in-app), Reporting (role dashboards), Search & Traceability, and Audit & Records — the Must-priority functional requirements in [requirements/functional.md](requirements/functional.md). PH-2 adds ADRs, Invariants, Research/Knowledge, Diagrams, the Webex adapter, and expanded reporting. Phasing: [planning/roadmap.md](planning/roadmap.md).

### Out of scope
Generic PM (OOS-01), in-app diagram engine (OOS-02), research-methodology engine (OOS-03), video conferencing (OOS-04), mobile-native (OOS-05), public self-registration (OOS-06), real-time collaborative editing (OOS-07), BPM engine (OOS-08), IdP management (OOS-09), data-warehouse/BI (OOS-10), own email/SMS infra in v1 (OOS-11), external partner portals (OOS-12), Kubernetes/broker (OOS-13), custom search cluster (OOS-14), WebSocket infra (OOS-15). These are rejected for v1/v2 unless a future decision reverses them; deferred (not rejected) items are in [execution/deferred-work-register.md](execution/deferred-work-register.md).

## Success metrics (KPI-)

Governing metric: **traceability completeness** — every MVP requirement reaches a decision, a work item, a test, and an acceptance criterion (Keystone gate G-TRACE). The full catalogue is [domain/metrics-kpi-catalog](domain/) (`KPI-001…KPI-021`); representative targets:

| ID | Metric | Baseline | Target | Measurement | Status |
|---|---|---|---|---|---|
| KPI-001 | Topic-to-decision cycle time | text-file era: unmeasured | trend down; visible per stream | dashboard aggregate | Instrumented (P12) |
| KPI-002 | Action SLA adherence (% actions closed by due date) | unmeasured | ≥ committee-agreed threshold | actions dashboard | Instrumented |
| KPI-003 | Backlog age (topics aging past urgency SLA) | unmeasured | surfaced + declining | aging indicator | Instrumented |
| KPI-004 | Vote-to-ratification integrity (hash-chain verifiable) | n/a | 100% verifiable | Auditor chain check | Met (ADR-0009) |
| KPI-005 | Bilingual coverage (strings with EN+AR + RTL clean) | 0% | 100% before ship | i18n parity + RTL VR | Met |

## Stakeholders (STK-)

| ID | Stakeholder / role | Interest | Influence (H/M/L) |
|---|---|---|---|
| STK-001 | Committee Chairman | Final approval/override on decisions and votes; governance authority | H |
| STK-002 | Secretary of the Committee (أمين سر اللجنة) | Primary user / product owner; runs the committee lifecycle | H |
| STK-003 | Committee Members | Read all streams; vote; own/contribute to topics | M |
| STK-004 | Reviewer / Auditor | Independent verification; audit-chain integrity | M |
| STK-005 | Administrator | User provisioning (via Keycloak), Hangfire/ops config | M |
| STK-006 | Submitter / Guest-Presenter | Stream requests; time-boxed presentation access (PH-2) | L |

Full analysis: [domain/stakeholders](domain/).

## Constraints and assumptions (summary)

Hard constraints (`CON-001…015`) and assumptions (`ASM-001…016`) are in [requirements/constraint-register.md](requirements/constraint-register.md) and [decisions/assumption-register.md](decisions/assumption-register.md). Load-bearing: self-contained deployment (CON-001), mandated stack (CON-002), single committee (CON-010), no email in v1 (CON-006), attributed voting (CON-011), Gregorian dates (CON-012). The non-negotiable invariants are [requirements/invariant-register.md](requirements/invariant-register.md).

## Approval

Scope confirmed by the Secretary on 2026-06-24 and treated as settled canon (mirrored in the settled-decisions of [decisions/open-decision-register.md](decisions/open-decision-register.md)). This charter is `Approved`; material change requires a new `DEC-`/ADR.
