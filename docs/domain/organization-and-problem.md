# 01 — Organization & Problem

**Purpose:** Establish the organizational context, the governed tech estate (at governance level only), the structural failure of current architecture governance, and what "good" looks like so ACMP has a grounded target.

---

## 1. Organization Overview

**Type:** Large national government organization.
**Mission:** A nationwide one-stop digital platform for government and private-sector services.
**Origin:** COVID-19 emergency initiative — permits, tracking, vaccination records. Started in startup/firefighting mode; now scaling to permanent national infrastructure.

### 1.1 Delivery Model

| Layer | Entities |
|---|---|
| Executive | CEO → VP |
| Management | General Managers: Technology, Business, Delivery, Quality, Operations |
| Stream leadership | Stream Directors: Technical, Delivery, Business (per stream) |
| Engineering | iOS team, Android team, .NET teams, QA/QC; specialists per stream |

- **5 streams**, each a business/technical domain with multiple services.
- Each service may span: backend APIs, background workers, databases, native mobile modules, embedded-service modules, external/gov integrations, observability components.
- Delivery is **Agile**; streams operate semi-independently but share platform infrastructure.

### 1.2 Tech Estate (Governed Environment — Not ACMP's Stack)

The following is the environment the Architecture Committee *governs*. ACMP does not replicate or replace any of it; it governs decisions *about* it.

| Category | Technology |
|---|---|
| Native apps | iOS + Android (native modules; embedded-service lifecycle) |
| Backend | .NET; microservices |
| API gateway | **Apigee** |
| Identity | Internal auth service (migrating to **Keycloak**) |
| Caching | **Redis** |
| Databases | Many **MS SQL Server** clusters; limited **PostgreSQL** |
| Observability | **ELK + Seq** |
| Background jobs | **Hangfire** |
| Messaging | **RabbitMQ + Kafka** |
| Notifications | Centralized platform (Email / SMS / Firebase) |
| Network | Private networks / VPNs for gov + private-sector integration |
| Pipelines | DevSecOps pipelines (org-standard) |
| Embedded services | Embedded web server inside mobile app; external-partner lifecycle |

**Implication for ACMP (CON-001 — self-contained, MANDATORY):** ACMP is **self-hosted and does NOT depend on the org's shared runtime infrastructure** — no org Hangfire, no org ELK/Seq, no org notification platform. It bundles its own background processing, observability, and notification channels. Per **ADR-0015**, all runtime dependencies are bundled — including **self-hosted Keycloak** (ACMP-owned realm, OIDC SSO) and **SQL Server** (app-owned instance, the mandated datastore) — so v1 has **zero external runtime services**; the only external dependency is **Webex** (Phase 2). ACMP's footprint must stay small — it is **low-traffic, internal, high-sensitivity**.

---

## 2. The Architecture Committee

### 2.1 Composition

| Role | Who |
|---|---|
| Chairman | VP (highest authority; final approval/override on votes) |
| Secretary | The primary operational owner of committee logistics and backlog |
| Members | All Technical Stream Directors; selected senior engineers |
| Subject-matter experts | iOS SME, Android SME; invited specialists as needed |
| Stream submitters | Technical/Delivery/Business stream staff submitting topics |

### 2.2 Current Cadence and Scope

- **Cadence:** weekly today; the committee is actively considering moving to bi-weekly.
- **Scope of governance:** any significant architecture decision affecting one or more streams, shared platforms, mobile apps, backend services, infra, security, external partners, gov integrations, or org-wide principles.
- **Decision authority:** voting by members + **chairman final approval**; chairman's authority supersedes a pure vote majority where necessary.
- **Input sources:** committee members, stream business/technical requests, urgent org needs, operational incidents, security findings, modernization initiatives, innovation proposals, cross-stream dependency problems, regulatory/external requirements.

---

## 3. The Core Problem: Architecture Governance Has Outgrown Its Startup Process

The committee's governance model — a text-file backlog, verbal weekly discussions, manual MoM — was adequate when the org was small, problems were bounded, and the same small group had full context. It is no longer adequate on any of those dimensions.

### 3.1 Why Architecture Governance Specifically Is Failing

| Failure mode | Concrete expression |
|---|---|
| **Scale** | 5 streams, dozens of services, hundreds of architecture decisions per year. A text file cannot track the volume, relationships, or cross-stream impacts. |
| **Cross-stream dependency blindness** | No structured way to record that `Decision A` on Stream 2 blocks or conditions `Topic B` on Stream 4. Dependencies are surfaced only in verbal discussion — or missed entirely. |
| **Security sensitivity** | Some committee decisions are security-critical (auth model, data residency, encryption choice, external integration policy). These require stricter traceability, access control, and audit trail than a text file can provide. |
| **No decision memory** | Once a decision is made, the rationale, alternatives, conditions, and context evaporate. MoM is manual; ADRs do not exist as a practice. A year later, no one knows *why* a choice was made — or even exactly *what* was decided. |
| **No action accountability** | Follow-up actions assigned during meetings have no system tracking their status, owner, due date, or escalation path. Completion is confirmed (or forgotten) verbally at the next meeting. |
| **No audit trail for votes** | Who voted what, when, with what authority, is not recorded in a defensible or queryable form. This is a governance and compliance exposure. |
| **No backlog discipline** | The text-file backlog has no aging visibility, no priority model, no traceability to stream source or urgency, and no capacity to handle parallel edits without corruption. |
| **Secretary bottleneck** | The Secretary manually maintains the backlog, prepares agendas, compiles MoM, chases action owners, and runs the governance calendar. This is a single-point-of-failure — knowledge and process both reside in one person. |
| **Knowledge loss on rotation** | When committee members or stream directors change, governance history is not transferable. The next person starts from zero. |

### 3.2 Why This Matters at National Scale

The platform governed by this committee is a **national-scale government system**. Poor architecture decisions — or good decisions poorly recorded — create technical debt, security gaps, integration failures, and regulatory exposure that affect the national user base. The committee's authority is commensurate with that responsibility; its tooling must be too.

---

## 4. Why Now

Three converging pressures make the status quo untenable:

1. **Platform maturity:** The org is exiting startup mode. National-scale operations require defensible audit trails, not informal records.
2. **Governance complexity:** 5 streams + embedded-service lifecycle + external integrations + a mobile platform means cross-stream architecture decisions are increasing in frequency and consequence.
3. **Institutional risk:** The Secretary is a single point of failure. If that person is unavailable, committee function degrades significantly. A system of record eliminates that dependency.

---

## 5. What Good Looks Like

A successful governance system for this committee has these properties:

| Property | Description |
|---|---|
| **Single system of record** | Every topic, decision, vote, action, and ADR lives in one place — no information scattered across files, emails, or chat. |
| **End-to-end traceability** | A topic can be traced forward to its decision, action, and ADR; an ADR can be traced back to the meeting and vote that produced it. |
| **Immutable audit trail** | Votes and issued decisions cannot be silently altered. The record of what was decided, by whom, and under what conditions is permanent and queryable. |
| **Action accountability** | Every action has an owner, a due date, a status, and an escalation path. The committee starts each meeting knowing the state of all open actions from prior meetings. |
| **Decision memory** | Rationale, alternatives considered, and conditions attached to a decision are captured at the point of decision — not reconstructed later from memory. |
| **Bilingual operation** | The committee operates in both Arabic and English. The platform must serve both languages with equal fidelity, including RTL layout for Arabic. |
| **Secretary-independent continuity** | Any committee member should be able to retrieve the full governance history without relying on the Secretary's personal knowledge. |
| **Low friction for committee members** | The tool must not add burden to the committee. Members submit topics, attend meetings, vote, and see their actions — all in minimal steps. |

---

*Traceability: Feeds `docs/domain/current-state.md` (as-is process), `docs/domain/pain-points.md` (structured failure analysis), `docs/domain/stakeholders.md` (who is affected), and `docs/domain/product-vision-and-principles.md` (the to-be state). Org/tech-estate detail is intentionally light here — governed environment detail lives in stream-level architecture docs, not in this platform's planning package.*
