---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Glossary — ACMP

The committee terms ACMP is built around, one line each (from `README.md` §G). The precise distinctions between *principle / standard / policy / constraint / invariant / decision / ADR* are defined once in [governance.md](governance.md) § "Concept disambiguation" and are not repeated here. EN↔AR term pairing is maintained in the design and i18n resources, not in this file.

| Term | Definition |
|---|---|
| **Architecture Committee** | The single governing body ACMP serves — it reviews architecture topics and issues authoritative, traceable decisions for the organization's tech estate. |
| **Backlog** | The ordered set of intake Topics awaiting triage, prioritization, and scheduling onto an agenda. |
| **Topic** (`TOP-YYYY-###`) | A unit of architecture work the committee considers — from intake through backlog, agenda, deliberation, to decision. |
| **Agenda** (`AGN-YYYY-###`) | The ordered list of Topics scheduled for a specific Meeting. |
| **Meeting** (`MTG-YYYY-###`) | A convened committee session in which agenda Topics are deliberated, votes are cast, and decisions are made. |
| **Minutes (MoM)** (`MIN-YYYY-###`) | The Minutes of a Meeting — the durable record of attendance, discussion, votes, and outcomes; immutable once published (superseded, never edited). |
| **Decision** (`DECN-YYYY-###`) | A point-in-time, committee-ratified choice between evaluated options, with recorded rationale and vote/authority record; immutable after ratification. |
| **Vote** (`VOTE-…`) | An attributed ballot on a Decision — eligible voters, quorum, abstentions, and the chairman's approval or override, all recorded by name. |
| **Quorum** | The minimum number of eligible voters that must be present for a Vote to be valid. |
| **Action** (`ACT-…`) | A tracked follow-up task assigned as a result of a Decision or discussion, with an owner and due date. |
| **Risk** (`RSK-…`) | An identified threat to an architecture outcome, with likelihood, impact, and a mitigation plan, tracked in the register. |
| **Dependency** (`DPN-…`) | A directed relationship where one item (Topic, System, Decision) relies on another; an edge in the traceability graph. |
| **ADR (Architecture Decision Record)** (`ADR-…`) | A structured record of a significant architectural Decision — its context, options, chosen option, and consequences — so future readers understand what was decided and why. |
| **Architecture Invariant** (`AIV-YYYY-###`) | A structural property the architecture must always exhibit, enforced architecturally; violating it is a structural failure, not a review trigger. |
| **Principle** | A high-level, enduring belief about how the organization should build and operate its systems — prescriptive in direction, not in implementation. |
| **Standard** | A precise, measurable, mandated rule or specification (naming a technology, format, version, or measurement) that systems must meet. |
| **Stream** | A thematic classification of Topics (an architecture domain or workstream) used for scoping, filtering, and reporting. |
| **System/Service** | A governed system or service in the organization's tech estate that the committee's Topics, Decisions, and Invariants apply to. |
| **Research Mission** (`RMS-…`) | A scoped investigation the committee commissions to gather evidence before a Decision, producing Findings and Recommendations. |
| **Finding** (`FND-…`) | A factual result produced by a Research Mission — evidence that informs a subsequent Recommendation or Decision. |
| **Recommendation** (`REC-…`) | A proposed course of action derived from Research Findings, presented to the committee for a Decision. |
| **Traceability** | End-to-end linkage across artefacts (Topic → Decision → ADR → Action → Risk → Dependency) so any governance outcome can be traced to its origin and consequences. |
