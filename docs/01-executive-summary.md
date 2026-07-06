---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Executive Summary — ACMP

## In one paragraph

ACMP is a focused, auditable, bilingual (EN/AR) web platform that is the **single system of record for one Architecture Committee** — topic intake → backlog → agenda → meeting → minutes → voting → decision → ADR → action → risk → dependency, with end-to-end traceability. It replaces a text-file backlog and is deliberately **architecture governance, not generic project management**. It runs **on-prem, self-contained, for ≤20 users**. The MVP (PH-1) governance loop is **built and live through P12**; PH-2/PH-3 add diagrams, Webex, research/knowledge, and AI-assisted extraction.

## Problem and objective

The committee governs architecture through disconnected text files and ad-hoc documents: no single record, no traceability from need → decision → follow-up, no immutability on votes/decisions, no bilingual support. The objective is one auditable, immutable-where-it-matters, RTL-first system of record that makes governance fast, traceable, and tamper-evident.

## Recommendation

Build a **modular monolith** on .NET 8 / ASP.NET Core / EF Core with **SQL Server as the single datastore**, a **React + TypeScript + Vite** RTL-first frontend, **self-hosted Keycloak (OIDC)** for identity, and **self-hosted Hangfire / Seq / MinIO** — all **bundled in one Docker Compose stack** with zero external runtime services in v1 (Webex is the only external dependency, deferred to Phase 2). Diagrams via the **Tarseem** sidecar (Phase 2); research via the **optional Keystone** companion. Rationale: [architecture/architecture.md](architecture/architecture.md); options weighed in [architecture/technology-comparison.md](architecture/technology-comparison.md); decisions in [adrs/](adrs/).

## Why this over the alternatives

- **Modular monolith, not microservices** — right-sized for ≤20 users on-prem; distribution is pure cost here (ADR-0001).
- **SQL Server only, not polyglot** — transactional + columnstore reporting + full-text search from one engine; no second DB to run or back up (ADR-0003).
- **Self-hosted bundled Keycloak, not federate-to-org-IdP** — the org has no IdP; ACMP owns its realm and ships it (ADR-0015).
- **SQL FTS first, not a search cluster** — sufficient at scale; OpenSearch/Meilisearch only if it outgrows FTS (ADR-0011).

## Scope at a glance

| | Summary |
|---|---|
| MVP delivers (PH-1) | The full governance loop: intake→triage→agenda→meeting→vote→decision→action, RBAC+ABAC, in-app notifications, EN/AR+RTL, hash-chained audit, role dashboards, FTS, typed traceability, risks + basic dependencies |
| Full target adds (PH-2/3) | ADRs + Invariants, Research/Keystone import, Knowledge wiki, Tarseem diagrams + dependency graph, Webex adapter, impact analysis, expanded reporting, AI-assisted extraction (human-gated), email channel |
| Explicitly out of scope | Generic PM, in-app diagram engine, BPM engine, mobile-native, public self-registration, real-time collab editing, Kubernetes/broker, custom search cluster, external partner portal (see [charter](00-charter.md) §Out of scope) |

## Plan and effort

Four phases: **PH-0** discovery/validation (done), **PH-1** MVP governance (done, delivered incrementally through P12), **PH-2** governance expansion (substantially delivered — ADRs/Invariants/traceability/reporting shipped; Webex/Tarseem/Knowledge remain), **PH-3** research & knowledge (not started). Details + gates: [planning/roadmap.md](planning/roadmap.md).

## Top risks

RTL effort underestimated (RISK-002), committee adoption (RISK-007), scope creep to generic PM (RISK-006), Tarseem early maturity (RISK-005), Webex Assistant enablement (RISK-008), AI-extraction privacy (RISK-012). Full register: [risks/risk-register.md](risks/risk-register.md).

## What we are asking for

Continued execution of the remaining PH-2 backlog and hardening **under this package's Keystone governance** — every change traced (requirement → decision → work → test → acceptance), every settled decision respected, every invariant enforced. Start point: [handoff/initial-prompt.md](handoff/initial-prompt.md).
