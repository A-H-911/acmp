# 00 — Executive Summary

**Purpose:** One-page orientation for the Architecture Committee Management Platform (ACMP) — the problem, the product, what the MVP proves, settled decisions, phased approach, explicit non-scope, and the single governing success metric.

---

## The Problem

The Architecture Committee runs national-scale governance — reviewing, approving, and tracking every significant architecture decision across 5 delivery streams, dozens of services, and a native mobile platform used at national scale. The committee started in firefighting/startup mode during the COVID-era ramp-up, where lightweight coordination (a shared text file as a backlog, weekly verbal discussions, manually compiled Minutes of Meeting) was appropriate. That mode has now outgrown itself. The text-file backlog has no concurrency control, no version history, no ownership model, and no searchability. Decisions leave no durable trace: there is no formal record of who voted, what the rationale was, what conditions were attached, or what follow-up actions were created and whether they were ever completed. MoM preparation is manual and error-prone. When committee members rotate or streams add complexity, decision memory is lost entirely. The committee cannot answer basic governance questions — "Why did we approve this? What was the vote count? Is that action still open? Has this invariant been violated?" — without chasing text files and email threads. This is not a process immaturity; it is a structural absence of a system of record appropriate to the committee's authority and scope.

---

## Product Vision

ACMP is a **focused, auditable, bilingual (EN/AR) web platform that serves as the single system of record for the Architecture Committee** — from topic intake through backlog management, agenda building, meeting facilitation, voting, decision recording, ADR creation, action tracking, risk management, dependency mapping, and end-to-end traceability. It is *architecture governance tooling*, not generic project management. Every feature must serve the committee's governance function or the traceability that makes decisions defensible and knowledge durable.

---

## What the MVP Proves

The MVP validates the **core committee loop** end to end:

```
Topic Intake → Backlog → Agenda → Meeting → Voting → Decision → Action tracking
                 ↕                    ↕                ↕
             Traceability          MoM (auto-draft)   ADR (on approve)
```

MVP scope is deliberately narrow: the loop works correctly and auditably; votes and decisions are immutable; the audit trail is complete; MoM is generated from meeting records (not typed from scratch); basic dashboards surface backlog age, decision throughput, and open actions; the interface is fully bilingual (EN/AR, RTL) in both light and dark mode; and role-based access is enforced via OIDC/Keycloak with invitation-only onboarding. The MVP explicitly does not attempt the Research/Knowledge phase — that comes in Phase 3.

---

## Settled Decisions (Canonical Reference: README §A)

Each decision has an ADR. These are not reopenable in v1 without a new ADR superseding the existing one.

| Area | Decision | ADR |
|---|---|---|
| Macro-architecture | **Modular monolith** — single deployable, logically bounded modules. Microservices explicitly rejected for v1. | ADR-0001 |
| Backend | **.NET 8 (LTS), ASP.NET Core, REST.** Clean Architecture per module + vertical-slice handlers (MediatR). EF Core. | ADR-0002 |
| Primary datastore | **Microsoft SQL Server only** — transactional + reporting (columnstore) + search (FTS). No second DB in v1. | ADR-0003 |
| Identity | **OIDC → Keycloak** (strategic); adapter for internal auth during transition. **No self-registration** — invitation/provisioned only. | ADR-0004 |
| Notifications | **Channel-abstraction** (`INotificationChannel`). **v1 channel = in-app notification center only (no email in v1).** Webex adapter = Phase 2. Email deferred until an SMTP relay is available. No org notification platform dependency. | ADR-0005 |
| Diagrams | **Tarseem** as containerized render sidecar. JSON spec = version-controlled source of truth; artifacts are generated outputs. | ADR-0006 |
| Research/planning | **Keystone** as companion Claude Code workflow. Platform **imports** its structured artifacts; Keystone is not embedded as a service. | ADR-0007 |
| Traceability | **Typed directed relationship model** over a shared `Artifact` identity; graph traversal in SQL for impact analysis. | ADR-0008 |
| Audit/immutability | **Append-only audit log**; votes and issued decisions are **immutable** — event-recorded, never silently edited. | ADR-0009 |
| Voting | **Simple model, always attributed** (no anonymity in v1): eligible voters, quorum, abstentions; **chairman final approval/override** explicitly recorded. | ADR-0010 |
| Search | **SQL Server FTS** in v1; if search outgrows it, stand up the platform's own self-hosted search (e.g., an OpenSearch container) — app-owned, never the org's ELK. | ADR-0011 |
| Frontend | **React 18 + TypeScript + Vite.** `react-i18next` (EN/AR); RTL via logical CSS + `dir`; light/dark; accessible DnD (`@dnd-kit`). | ADR-0012 |

**Supporting infrastructure (app-owned, not org-shared):** App-owned Hangfire on ACMP's own SQL Server (background jobs: reminders, escalation, digest, diagram render); Serilog → self-hosted Seq (app-owned container; structured logs); OpenTelemetry traces; ASP.NET health checks; self-hosted MinIO via `IFileStore` abstraction (S3-compatible; production sizing is an ops detail).

---

## Phased Approach

| Phase | Name | Focus | Gate to next |
|---|---|---|---|
| **PH-0** | Discovery | This planning package. Finalize requirements, settle open questions, align stakeholders, complete ADRs. | Org sign-off on this package |
| **PH-1** | MVP Governance | Core committee loop: intake → backlog → agenda → meeting → MoM → voting → decision → action → audit + basic dashboards. EN/AR, light/dark, OIDC. | MVP in production; committee uses it for ≥4 consecutive weekly/bi-weekly meetings |
| **PH-2** | Governance Expansion | ADRs, Architecture Invariants, Risk management, Dependency graph, cross-stream traceability, Reporting/KPI dashboards, advanced notifications/escalation. | Measured adoption; open actions tracked to completion; audit queries satisfied |
| **PH-3** | Research & Knowledge | Research missions (Keystone import), Findings/Recommendations, Documentation/Wiki, Diagrams (Tarseem), Knowledge base, advanced search, Presenter/Guest flows. | Committee using knowledge module; Keystone packages being imported |

Each phase ships to production before the next begins. No phase is contingent on a future architecture change — the modular monolith accommodates all phases within the same deployable.

---

## What ACMP Will NOT Build

These are explicit non-scope items, not future possibilities — building them would distort the product's mission or duplicate existing systems:

- **Generic project/task management** (not a replacement for Jira, Planner, or any delivery tracker)
- **Microservices or distributed architecture** in v1 (ADR-0001 is settled)
- **A new diagram engine** — Tarseem exists and is adopted (ADR-0006)
- **A new research methodology or planning workflow** — Keystone is adopted as a companion (ADR-0007)
- **A Webex replacement or meeting platform** — Webex integration is an adapter, not the product
- **Public-facing or self-service user registration** — sensitive gov system; invitation/provisioned only (ADR-0004)
- **A second database** in v1 — SQL Server covers all v1 needs (ADR-0003)

---

## Single Governing Success Metric

> **The Architecture Committee can answer, from ACMP alone and without consulting any external file or email: "What did we decide on topic X, who voted how, what conditions were set, and are all follow-up actions resolved?"**

Operationally: time-to-answer this question drops from "hours / impossible" to **< 60 seconds** for any decision recorded since go-live.

---

*Traceability: This document summarizes `README.md §A–G`, `.context/brief-digest.md §1–4`, and feeds every downstream deliverable. Phase detail in `docs/36-roadmap.md`. Settled decisions detailed in `adr/ADR-0001` through `adr/ADR-0012`. Scope detail in `docs/06-scope-and-out-of-scope.md`.*
