# 22 — Standards and Best Practices (Deliverable 30)

**Purpose:** Enumerate the formal standards, industry frameworks, common practices, and context-specific proposals that shape ACMP's design; distinguish which are mandatory, which are guidance, and which are proposals; provide the canonical concept-disambiguation section referenced by README §G.

---

## A. Concept Disambiguation (Single Source of Truth)

> README §G states: *"Distinctions between principle / standard / policy / constraint / invariant / decision / ADR are defined once in `docs/22-standards-and-best-practices.md` §'Concept disambiguation' and must not be duplicated."* All other documents cross-reference this section.

These seven concepts are distinct domain entities in ACMP. Conflating them produces ambiguous governance artefacts and breaks traceability. Each is defined below with: definition, example from ACMP context, lifecycle, where it lives in ACMP, and how it relates to adjacent concepts.

---

### A.1 Principle

**Definition:** A high-level, enduring belief about how the organization should build and operate its systems. A Principle is prescriptive in direction but not prescriptive in implementation. It does not expire and is rarely retired unless the organization's values fundamentally change.

**Example:** "Security is a first-class concern at every layer, not a bolt-on."

**Lifecycle:** `Draft → Proposed → Approved → (Revised | Retired)`. Revision produces a new version; the old version is archived. Principles are never silently edited once approved.

**Where in ACMP:** Stored as `Principle` entities in the Governance module. Displayed in the Architecture Invariants / Principles view. Cross-linked to Architecture Invariants that enforce them and to ADRs that were influenced by them.

**Relationship to adjacent concepts:**
- A Principle *motivates* one or more Standards, Policies, and Architecture Invariants.
- A Principle does **not** record *why a specific decision was made*; an ADR does that.
- A Principle is not a Constraint: a Principle is chosen; a Constraint is externally imposed.

---

### A.2 Standard

**Definition:** A precise, measurable, agreed-upon rule or specification that the organization mandates for its systems. A Standard is more specific than a Principle — it names the specific technology, format, version, or measurement that must be met.

**Example:** "All REST APIs must use OpenAPI 3.1 for contract documentation." / "All passwords must meet OWASP ASVS v5.0 L2 Section V2 requirements."

**Lifecycle:** `Draft → Proposed → Approved → (Superseded | Retired | Exempted)`. Standards can be superseded by a newer version. An exemption from a Standard requires an ADR recording the rationale.

**Where in ACMP:** Stored as `Standard` entities in the Governance module (same module as Principles and Invariants). Cross-linked to Architecture Invariants that enforce them and to ADRs that chose technologies in conformance with or in exemption from them.

**Relationship to adjacent concepts:**
- A Standard *operationalizes* a Principle into a specific rule.
- A Standard differs from a Policy: a Standard is technical (what/how to build); a Policy is organizational (who may do what, under what conditions).
- A Standard differs from an Architecture Invariant: a Standard is a rule; an Invariant is a property the architecture must *always exhibit*, enforced architecturally.
- A Standard violation typically triggers a review; an Invariant violation is a structural failure.

---

### A.3 Policy

**Definition:** An organizational rule governing *process, behaviour, or authority* — who may do what, under what conditions, and what consequences follow. Policies are typically owned by a function (security, legal, compliance) rather than the architecture committee.

**Example:** "All production deployments must be approved by two senior engineers." / "Sensitive data may not leave the organization's network perimeter."

**Lifecycle:** `Draft → Proposed → Approved → (Revised | Retired)`. Policies are versioned; older versions are archived.

**Where in ACMP:** ACMP is a *consumer* of organizational Policies (it implements them via its RBAC/ABAC model and audit controls) rather than the primary store of organizational Policies. ACMP's own process rules (who may vote, who may publish minutes) are modelled as RBAC/ABAC rules, not stored as free-form Policy entities. If the committee governs external Policies (e.g., "adopt this retention policy"), that is recorded as a Decision + ADR, and the Policy itself lives in the org's Policy Register.

**Relationship to adjacent concepts:**
- A Policy governs *people and process*; a Standard governs *technical artefacts*.
- A Policy may reference a Standard ("all new services must comply with Standard S-042").
- A Constraint is a specific restriction that cannot be changed by the committee; a Policy can be changed by the appropriate authority.

---

### A.4 Constraint

**Definition:** An externally imposed, non-negotiable restriction on what the architecture may or may not do. A Constraint is not chosen by the committee; it is received from regulation, law, contractual obligation, or mandated organizational infrastructure.

**Example:** CON-001 — "ACMP must not depend on the organization's shared runtime infrastructure." / "All data must remain on-premises within [country]'s borders (data residency law)."

**Lifecycle:** Constraints do not expire on a lifecycle; they persist until the external authority removes or relaxes them. They are recorded as `CON-###` in the planning package and `docs/41-raid.md`. If a Constraint is relaxed, the change is recorded in RAID and noted against the original CON reference.

**Where in ACMP:** `CON-###` identifiers in the planning package; constraints surface in ADRs as binding context (not as options that were weighed). In the running platform, constraints are implemented as architectural rules enforced in code (e.g., CON-001 is enforced by not importing org-infra packages).

**Relationship to adjacent concepts:**
- A Constraint is **non-negotiable**; a Standard or Policy can be exempted by authority.
- A Constraint does not record *how* it is satisfied — an ADR records the design decision that satisfies it.
- A Constraint is not a Principle: a Principle is chosen and aspirational; a Constraint is received and mandatory.

---

### A.5 Architecture Invariant

**Definition:** A structural property the architecture *must always exhibit*, regardless of feature evolution. An Invariant is enforced architecturally (usually in code, tests, or linting rules) and is derived from Principles, Standards, or Constraints. Violating an Invariant is a structural failure, not a review trigger.

**Example in ACMP:** "No module may read another module's tables directly — inter-module communication is via in-process public contracts only." / "Votes and issued decisions are append-only; no UPDATE or DELETE is permitted on those records."

**Lifecycle (`AIV-` in-app entity):** `Draft → Proposed → Active → (Retired | Superseded)`. Violations are tracked as separate `Violation` records against the Invariant (never as status changes on the Invariant itself).

**Where in ACMP:** The Governance module stores Architecture Invariants as first-class entities (`AIV-YYYY-###`). Each Invariant records: category (data, integration, security, process, etc.), scope (platform-wide, module-specific, service-specific), enforcement mechanism (automated test, CI gate, code review rule, architectural fitness function), exceptions policy, and open violations.

**Relationship to adjacent concepts:**
- An Invariant *enforces* a Principle or Standard structurally.
- An ADR may *introduce* an Invariant (e.g., ADR-0001 → Invariant "no cross-module DB access").
- An Invariant is not a Decision: a Decision is a point-in-time choice (which option to take); an Invariant is an ongoing property that must be preserved.
- An Architecture Invariant is the ACMP product's governance artefact for the *governed* tech estate, distinct from the planning package's `CON-###` identifiers (which constrain ACMP itself).

---

### A.6 Decision

**Definition:** A point-in-time, committee-ratified choice between evaluated options, with recorded rationale, alternatives considered, and vote/authority record. A Decision is the *outcome of committee deliberation* on an architecture or governance topic.

**Example:** "The committee decided (DECN-2026-012) to adopt Keycloak as the OIDC identity provider for all new platform services, conditional on a security review within 60 days."

**Lifecycle (`DECN-` in-app entity):** `Pending → Ratified → (Implemented | Deferred | Converted)`. A ratified Decision is immutable (ADR-0009). Superseding a Decision requires a new Decision.

**Where in ACMP:** Decisions module. Each Decision links to: the Topic that triggered it, the Vote that ratified it, the Chairman's approval record, any conditions, and the resulting ADR (if the decision warrants one).

**Relationship to adjacent concepts:**
- A Decision is more specific than a Principle: a Principle says "prefer open standards"; a Decision says "adopt Keycloak for OIDC."
- An ADR *documents* a Decision for posterity, providing context and rationale for future readers.
- A Decision becomes an Architecture Invariant if it establishes a structural rule that must be preserved going forward.
- A Decision is distinct from a Standard: a Standard is a standing rule; a Decision is a discrete choice event.

---

### A.7 ADR (Architecture Decision Record)

**Definition:** A structured document that captures the *context, options, trade-offs, chosen option, and consequences* of a significant architectural Decision, written so that future readers understand not just *what* was decided but *why*, and *what alternatives were rejected*. An ADR is the persistent, human-readable memory of a Decision.

**Example:** ADR-0001 — "Modular Monolith for v1. Context: ≤20 users, single team, time-to-value priority. Options: microservices (rejected — premature complexity), monolith (rejected — no modularity boundary), modular monolith (chosen). Consequences: modules communicate via in-process contracts; no cross-module DB access invariant."

**Lifecycle (`ADR-` in-app entity):** `Draft → Proposed → Approved → (Superseded | Deprecated)`. A Superseded ADR is never deleted; the superseding ADR references it. No in-place editing of Approved ADRs (append-only audit requirement).

**Where in ACMP:** Governance module. Template follows MADR format (see §B.4 below), extended with committee-specific fields (vote reference, related topic, affected systems, author, reviewers). ADRs are versioned Markdown documents with YAML front-matter stored as structured data.

**Relationship to adjacent concepts:**
- An ADR documents a **Decision** (one-to-one or many-to-one if one Decision produces multiple ADR concerns).
- An ADR may introduce or reference an **Architecture Invariant**.
- An ADR describes the decision's alignment with a **Standard** or the rationale for an **exemption**.
- An ADR is not a Principle: a Principle is aspirational and standing; an ADR is a specific, time-stamped, context-bound record.
- An ADR is not a Standard: a Standard is the rule; an ADR is the record of *choosing* to adopt that rule (or a specific implementation of it).

---

### A.8 Disambiguation Summary Table

| Concept | "What it is" | Lifecycle | Mutable? | ACMP entity |
|---|---|---|---|---|
| **Principle** | Enduring belief about how to build systems | Draft→Proposed→Approved→(Revised\|Retired) | Versioned (not silently edited) | `Principle` |
| **Standard** | Precise technical rule / specification | Draft→Proposed→Approved→(Superseded\|Retired\|Exempted) | Versioned; exemptions via ADR | `Standard` |
| **Policy** | Organizational process/authority rule | Draft→Proposed→Approved→(Revised\|Retired) | Versioned | External register (ACMP consumes) |
| **Constraint** | Non-negotiable external restriction | Persistent until relaxed | Not relaxed without authority change | `CON-###` (planning) |
| **Architecture Invariant** | Structural property always exhibited | Draft→Proposed→Active→(Retired\|Superseded) | No; violations tracked separately | `AIV-YYYY-###` |
| **Decision** | Point-in-time committee choice (ratified) | Pending→Ratified→(Implemented\|Deferred\|Converted) | **Immutable after ratification** | `DECN-YYYY-###` |
| **ADR** | Structured memory of a Decision with context + rationale | Draft→Proposed→Approved→(Superseded\|Deprecated) | **No in-place edit after Approved** | `ADR-YYYY-###` (in-app) |

---

## B. Formal Standards (Must / Should Comply)

Standards in this section represent formal bodies (ISO, IEC, IEEE, W3C, OWASP) whose outputs are either legally/contractually required or represent the minimum acceptable bar for a system of this sensitivity. Non-compliance requires an explicit ADR-recorded justification.

### B.1 ISO/IEC/IEEE 42010:2022 — Architecture Description

**Applicability:** The vocabulary and structural model of 42010 underpins ACMP's governance artefacts.
**Key concepts used:**
- **Architecture description** — a work product that expresses an architecture
- **Stakeholder** and **concern** — maps to ACMP's role model and the governed tech estate's constituency
- **Viewpoint** and **view** — ACMP's diagrams are views over the architecture; Tarseem renders them
- **Architecture decision** — the concept directly maps to ACMP's `Decision` and `ADR` entities

**Compliance posture:** ACMP adopts 42010's vocabulary (stakeholder, concern, viewpoint, view, architecture description, architecture decision) in its domain model and templates. ACMP is not itself the subject of a formal 42010-compliant architecture description, but its templates guide users to produce 42010-aligned content.

**Note:** 42010:2022 supersedes 42010:2011. Reference the 2022 edition.

Source: https://quality.arc42.org/standards/iso-42010 · https://iso.org/standard/50508.html

---

### B.2 OWASP ASVS 5.0 (Application Security Verification Standard)

**Target level: L2** — appropriate for a sensitive internal system handling committee deliberations, votes, and architectural decisions (not public-facing, not financial-critical, but high-sensitivity governance data).

ASVS 5.0 (May 2025) restructures verification into ~350 requirements across 17 chapters (modular, chapter-addressable). Key chapters for ACMP:

| ASVS Chapter | Applicability to ACMP |
|---|---|
| V1 — Encoding and Escaping | Input encoding for Markdown/HTML content in Meeting Notes, MoM, ADR body |
| V2 — Authentication | Delegated to Keycloak (OIDC); verify JWT validation, token expiry, PKCE |
| V3 — Session | JWT lifetime, refresh-token rotation, logout invalidation |
| V4 — Access Control | RBAC/ABAC enforcement; per-topic capability checks; no cross-module table reads |
| V5 — Validation, Sanitization | All user-supplied content (Markdown, JSON, file uploads); TipTap output sanitized server-side |
| V6 — Cryptography | MinIO encryption; Seq TLS; SQL Server TDE; secrets management |
| V7 — Error and Logging | Serilog + Seq; no PII in logs; structured audit trail |
| V8 — Data Protection | Classification of committee data; immutable vote/decision records |
| V9 — Secure Communication | TLS on all internal service communication; MinIO, Seq, Keycloak |
| V11 — Business Logic | Vote integrity; quorum validation; chairman override recorded |
| V13 — API | REST API design; OpenAPI docs; auth on all endpoints |
| V14 — Configuration | Externalized config; no secrets in code; container hardening |

**Compliance posture:** Target L2 across all 17 chapters. L3 is not required (not a public financial or safety-critical system). Security-control plan (`docs/25-security-controls.md`) maps ASVS L2 requirements to implementation controls.

Source: https://owasp.org/www-project-application-security-verification-standard/ · https://github.com/OWASP/ASVS

---

### B.3 OWASP Top 10 (2021)

**Applicability:** Baseline security checklist for web applications. Key items for ACMP:

| Top 10 Item | ACMP relevance |
|---|---|
| A01 — Broken Access Control | RBAC/ABAC; per-topic capability checks; stream visibility rules |
| A02 — Cryptographic Failures | TLS everywhere; MinIO encryption; no plaintext secrets |
| A03 — Injection | SQL injection prevention (EF Core parameterized queries); Markdown injection; file path traversal in MinIO |
| A04 — Insecure Design | Threat-modelled design; immutable audit; no anonymous voting |
| A05 — Security Misconfiguration | Container hardening; Seq/MinIO/Keycloak default credentials; externalized config |
| A06 — Vulnerable Components | NuGet/npm dependency scanning in CI; Dependabot or equivalent |
| A07 — Auth Failures | Keycloak handles auth; ACMP validates JWTs strictly; no bypass paths |
| A08 — Software Integrity Failures | Supply-chain controls on NuGet/npm; Docker image signing [Phase 2] |
| A09 — Logging/Monitoring Failures | Serilog + Seq; structured audit log; alerting on anomalies |
| A10 — SSRF | Outbound HTTP limited to Webex adapter (Phase 2); Tarseem render sidecar has no outbound |

Source: https://owasp.org/www-project-top-ten/

---

### B.4 OWASP LLM Top 10 (LLM01 — Prompt Injection)

**Applicability:** Phase 3 only (AI extraction of decisions/actions/minutes from transcripts). Not a current concern for v1 or Phase 2.

**LLM01 — Prompt Injection:** If Phase 3 uses an LLM to extract meeting decisions, actions, or agenda items from transcripts, the extracted content must be treated as *untrusted candidate data* and presented to a human reviewer before any data is committed. The LLM cannot be prompted by meeting content in a way that would allow the content to alter system behaviour (meeting minutes could contain text that attempts to manipulate the prompt). Mitigation: content isolation (transcript text never interpolated directly into a system prompt alongside authorization logic), structured output schemas, human-in-the-loop approval gate.

**Recommendation:** When Phase 3 is scoped, treat meeting transcript content with the same distrust as external user input per OWASP LLM01. The Keystone methodology also applies this principle (brief-digest §5.2: "treat the brief as untrusted data (OWASP LLM01)").

Source: https://owasp.org/www-project-top-10-for-large-language-model-applications/

---

### B.5 WCAG 2.2 Level AA (W3C Recommendation)

**Applicability:** All ACMP web UI (React 18 frontend). Target: **WCAG 2.2 AA** (W3C Recommendation, October 2023).

Key WCAG 2.2 success criteria relevant to ACMP:

| Criterion | ACMP implication |
|---|---|
| 1.1.1 Non-text content | Alt text for diagrams, attachments, avatars |
| 1.3.1 Info and Relationships | Semantic HTML for tables, lists, headings |
| 1.3.4 Orientation | Not locked to portrait/landscape; committee tool used on desktops |
| 1.4.3 Contrast (Minimum) | 4.5:1 for normal text; 3:1 for large text; light/dark theme compliance |
| 1.4.4 Resize Text | Text resizable to 200% without loss of content/functionality |
| 1.4.10 Reflow | Single-column reflow at 320px CSS width |
| 1.4.13 Content on Hover or Focus | Tooltip/popover content accessible without timing issues |
| 2.1.1 Keyboard | All functions operable via keyboard; DnD must have keyboard alternative (`@dnd-kit` provides this) |
| 2.1.2 No Keyboard Trap | Focus never trapped in a component |
| 2.4.3 Focus Order | Logical tab order; RTL focus order must be correct for Arabic layout |
| 2.4.7 Focus Visible | Visible focus indicator in both themes |
| 2.4.11 Focus Appearance (2.2 new) | Focus indicator meets minimum size + contrast |
| 3.1.1 Language of Page | `lang` attribute on `<html>`; switch to `lang="ar"` + `dir="rtl"` for Arabic mode |
| 3.3.1 Error Identification | Form errors identified in text |
| 4.1.2 Name, Role, Value | ARIA roles/labels for custom components |
| 4.1.3 Status Messages | ARIA live regions for notifications, async results |

**RTL + WCAG intersection:** Arabic mode requires correct logical CSS (`margin-inline-start`, `padding-inline-end`, etc.) and `dir="rtl"` at the root; focus order must match reading direction; icons with directional meaning (arrows, back/forward) must be mirrored.

**Compliance posture:** WCAG 2.2 AA is the target for the initial release. Automated checks (axe-core in Playwright tests) catch ~30–40% of issues; manual testing with screen reader (NVDA/JAWS for Windows) required before release. WCAG 2.2 AAA is optional and not required.

---

## C. Industry Frameworks (Adopt as Guidance)

Frameworks in this section are not formal standards bodies but are widely adopted, evidence-based, and applicable to ACMP. Compliance is encouraged but each point of non-compliance should have a recorded rationale.

### C.1 arc42 (Architecture Documentation Template)

**Applicability:** `docs/15-architecture.md` is structured along arc42's 12 sections.

arc42's 12 sections and their ACMP mapping:

| arc42 Section | ACMP coverage |
|---|---|
| 1. Introduction & Goals | `docs/05-product-vision-and-principles.md`, `docs/07-functional-requirements.md` |
| 2. Constraints | `docs/41-raid.md` (CON-###) |
| 3. Context & Scope | `docs/01-organization-and-problem.md`, context diagram in `docs/15` |
| 4. Solution Strategy | `docs/15-architecture.md` §solution-strategy |
| 5. Building Block View | Module definitions in `docs/15-architecture.md` |
| 6. Runtime View | Workflow diagrams in `docs/13-workflows.md` |
| 7. Deployment View | `docs/33-containerization-and-deployment.md` |
| 8. Cross-Cutting Concepts | `docs/22` (this doc), `docs/15-architecture.md` §cross-cutting |
| 9. Architecture Decisions | `adr/` directory |
| 10. Quality Requirements | `docs/08-non-functional-requirements.md` |
| 11. Risks & Technical Debt | `docs/41-raid.md` (RISK-###) |
| 12. Glossary | README §G |

Source: https://arc42.org

---

### C.2 C4 Model (Context, Container, Component, Code)

**Applicability:** ACMP's architectural diagrams use C4 levels. Tarseem's `architecture/C4` diagram family renders them.

| C4 Level | Used in ACMP |
|---|---|
| L1 — System Context | ACMP as a box; external systems (Keycloak, Webex, Tarseem sidecar, MinIO, Seq) as surrounding boxes |
| L2 — Container | ACMP web app, ACMP API, SQL Server, MinIO, Seq, Tarseem sidecar, Keycloak |
| L3 — Component | Modules within ACMP API (Topics, Meetings, Decisions, ADRs, etc.) |
| L4 — Code | Not diagrammed (code structure documented in code itself) |

C4 also defines Dynamic and Deployment diagram types. Tarseem's `deployment` and `sequence` families cover those needs.

Source: https://c4model.com

---

### C.3 .NET Modular Monolith + Clean Architecture + Vertical Slice

**Applicability:** ADR-0001 (modular monolith), ADR-0002 (.NET 8, Clean Architecture, vertical slice with MediatR). This is the authoritative .NET community pattern for medium-complexity applications where microservices would be premature.

**Structure pattern per module:**
```
Modules/
  <ModuleName>/
    Domain/          (entities, value objects, domain events)
    Application/     (commands, queries, handlers — vertical slices via MediatR)
    Infrastructure/  (EF Core, repositories, external adapters)
    Contracts/       (public interfaces for cross-module communication)
```

**Invariants enforced by this pattern (Architecture Invariants):**
- A module's `Infrastructure` layer is private; only `Contracts` is public.
- No module imports another module's `Domain` or `Infrastructure` types.
- Cross-module calls go through the `Contracts` interface or MediatR (in-process).
- No shared DB context across modules; each module owns its EF Core `DbContext`.

**Community resources:**
- Milan Jovanović (milanjovanovic.tech) — vertical slice + modular monolith in .NET
- Ardalis/Steve Smith — Clean Architecture for .NET
- Microsoft On-.NET — "Clean Architecture, Vertical Slices, and Modular Monoliths"

Source: https://www.milanjovanovic.tech/blog/vertical-slice-architecture-dotnet · https://learn.microsoft.com/en-us/shows/on-dotnet/on-dotnet-live-clean-architecture-vertical-slices-and-modular-monoliths-oh-my

---

### C.4 Records Management and Auditability

**Applicability:** ACMP governs sensitive organizational decisions. Records must be kept reliably, durably, and in a tamper-evident manner.

**Applicable principles (general records-management practice):**
- **Authenticity:** records are what they claim to be; prevent unauthorized alteration (append-only audit log, immutable vote/decision records — ADR-0009).
- **Reliability:** the full record of a decision is captured, including alternatives and dissenting votes.
- **Integrity:** records have not been altered or corrupted (database integrity constraints; audit-log hash chaining if required).
- **Usability:** records are retrievable and interpretable (search, traceability graph, export).
- **Retention:** records are kept for the period required by the authority (configurable retention in v1; no auto-purge — resolved decision §0.14).

**ACMP implementation:** Append-only `AuditEvent` table; immutability enforced by EF Core interceptors preventing UPDATE/DELETE on `Vote`, `Decision`, `ADR` (post-Approved status), `MeetingMinutes` (post-Published); Hangfire scheduled retention jobs (configured, not executing in v1); MinIO with versioning enabled for file attachments.

---

## D. Common Practices (Apply by Default)

These practices are widely accepted as baseline for professional software development. Apply unless there is a specific reason not to (which would be documented in an ADR or open decision).

### D.1 REST API Design

- **Versioning:** URI versioning (`/api/v1/`) for the initial API; breaking changes increment the version.
- **Resource naming:** plural nouns (`/topics`, `/meetings`, `/adrs`); no verbs in resource paths.
- **HTTP methods:** `GET` (read), `POST` (create), `PUT`/`PATCH` (update), `DELETE` (soft-delete where retention applies).
- **Status codes:** standard semantics; `201 Created` with `Location` header on create; `204 No Content` on delete; `422 Unprocessable Entity` for validation failures; `409 Conflict` for concurrency.
- **Pagination:** cursor-based preferred for large lists; offset acceptable for predictable small sets (≤20 users).
- **OpenAPI 3.1:** all endpoints documented; Swagger UI available in non-production environments.
- **Auth:** Bearer JWT on all endpoints; no unauthenticated endpoints (health check excepted).

### D.2 Containerization and Deployment

- Docker multi-stage builds (build image separate from runtime image); minimal runtime base image.
- Docker Compose for orchestration (ADR — no Kubernetes in v1).
- Externalized configuration via environment variables and Docker secrets; no secrets baked into images.
- Health check endpoints (`/health` liveness, `/health/ready` readiness) on all services.
- Named Docker volumes for persistent data (SQL Server data, MinIO data, Seq data).
- `docker compose up -d` should be the only command needed to start the stack.
- Database migrations run at startup (`EF Core Migrate` on app start) with idempotent migration scripts.
- Backup/restore procedures defined for SQL Server volume and MinIO volume (nightly, tested).

### D.3 Observability

- **Logging:** Serilog with structured JSON output; sinks to Seq (self-hosted) and console. Log levels: `Debug` (dev), `Information` (prod minimum), `Warning`, `Error`, `Fatal`. No PII in log fields.
- **Tracing:** OpenTelemetry with OTLP exporter; traces exported to Seq (or a compatible OTLP receiver). Trace context propagated across async boundaries (Hangfire jobs must inherit trace context).
- **Metrics:** ASP.NET Core built-in metrics (`Microsoft.AspNetCore.Diagnostics.HealthChecks`); custom metrics for committee-specific KPIs (topics processed, decisions per meeting, action completion rate) — see `docs/28-metrics-and-kpi-catalog.md`.
- **Health checks:** liveness (is the process alive?), readiness (can the process serve traffic?), dependency (can it reach SQL Server / Seq / MinIO / Keycloak?).

### D.4 Testing Strategy (Summary; full detail in `docs/31-testing-strategy.md`)

- **Unit tests:** domain logic, application command/query handlers (xUnit + Moq).
- **Integration tests:** API endpoint tests against a real SQL Server (Testcontainers for SQL Server in CI), covering auth, RBAC, and data flow.
- **Contract tests:** Keycloak OIDC token validation; Tarseem render API contract.
- **UI/E2E tests:** Playwright; critical paths (topic submission, vote flow, ADR publish, MoM approve).
- **Accessibility tests:** axe-core in Playwright for WCAG 2.2 AA; manual screen-reader testing before release.
- **Security tests:** OWASP ZAP scan in CI (DAST); Semgrep/Trivy for SAST + container scan.

### D.5 Arabic / RTL Web Practices

The following practices are applied throughout the ACMP frontend. They are not guidelines; they are implementation requirements for first-class bilingual support.

| Practice | Detail |
|---|---|
| **Logical CSS properties** | Use `margin-inline-start` not `margin-left`; `padding-inline-end` not `padding-right`; `inset-inline-*` for absolute positioning |
| **`dir` attribute** | Set `dir="rtl"` at `<html>` level when Arabic mode is active (via `react-i18next` language switch); component-level `dir` overrides where needed |
| **Bidirectional text** | Use Unicode bidi controls for mixed-direction content (e.g., Arabic paragraph with embedded English code); CSS `unicode-bidi: isolate` for isolated segments |
| **Font stack** | Primary Arabic web font (e.g., IBM Plex Arabic, Noto Kufi Arabic, or a government-approved Arabic font); fallback to system Arabic fonts; Latin font separate |
| **Icon mirroring** | Directional icons (arrows, back/forward, send) use CSS `[dir="rtl"] .icon { transform: scaleX(-1) }` or SVG mirroring; non-directional icons (calendar, bell) not mirrored |
| **Number formatting** | `Intl.NumberFormat` with `ar-SA` or `ar` locale; Eastern Arabic numerals vs. Western depends on org preference (default Western Arabic numerals in technical context) |
| **Date formatting** | Gregorian only (resolved decision §0.15); formatted via `Intl.DateTimeFormat` with `ar` locale |
| **DnD accessibility** | `@dnd-kit` chosen specifically for keyboard-accessible drag-and-drop, compatible with RTL layouts |
| **Playwright RTL tests** | Test critical flows with Arabic locale active; verify layout does not break |

Source: MDN Web Docs — CSS Logical Properties; W3C Internationalization Working Group RTL guidance [unverified — verify specific W3C doc when writing a11y spec]

---

## E. Optional Recommendations

These practices are advisable but not required for the initial release. They should be revisited when the platform matures or specific needs arise.

| Practice | Rationale | Trigger to adopt |
|---|---|---|
| **OpenSearch self-hosted** | Richer full-text search (fuzzy, autocomplete, aggregations) than SQL Server FTS | SQL Server FTS demonstrably insufficient; traffic/volume outgrows it (ADR-0011) |
| **Fitness functions (ArchUnit equivalent)** | Automated enforcement of architecture invariants in CI | Module boundaries start being violated in practice |
| **Feature flags** | Gradual feature rollout; A/B testing | Team grows; multiple concurrent features in flight |
| **SBOM (Software Bill of Materials)** | Supply-chain transparency; required by some gov contracts | Gov contract requires it; or a critical CVE affects a dependency |
| **Docker image signing (Cosign)** | Image integrity in deployment pipeline | Security policy mandates it |
| **Semantic versioning + Changelog** | Clear communication of API/breaking changes | More than one consuming client |
| **OpenTelemetry Collector sidecar** | Decouple app from telemetry backend | Seq replaced or supplemented with another OTLP receiver |
| **Rate limiting (ASP.NET Core built-in)** | Protect API from abuse | Usage pattern suggests risk; or Webex adapter added (Phase 2) |
| **WCAG 2.2 AAA** | Higher accessibility bar | Accessibility audit reveals user need; or gov procurement requires it |
| **Hijri calendar support** | Some users may prefer Hijri dates | Org decides to add Hijri support (currently resolved: Gregorian only) |

---

## F. Context-Specific Proposals

These proposals are specific to ACMP's domain and are not covered by general standards. They are **proposed** (not approved until recorded in an ADR or acceptance criterion).

### F.1 Decision Audit Hash Chain (Proposal)

**Proposal:** Each `Decision` and `Vote` record stores a SHA-256 hash of its content + the hash of the previous record in the sequence (chain). This creates a tamper-evident audit trail that can be verified without external trust.

**Rationale:** While append-only DB constraints prevent modification, a hash chain makes tampering detectable even if the DB admin circumvents the constraints.

**Status:** Proposed. Not required for v1 (append-only + DB audit is sufficient). Raise as `OQ-` if the organization's legal/compliance function requires it.

### F.2 MADR-Extended ADR Template (Proposal → Approved via ADR-0009 + §A.7)

**Proposal:** The in-app ADR template extends MADR 3.x with:
- `committee_decision_ref` — link to the `DECN-YYYY-###` that triggered this ADR
- `affected_systems` — list of governed systems/services this ADR applies to
- `vote_ref` — link to the `VOTE-###` record
- `reviewers` — list of Keycloak user IDs who reviewed the draft
- `related_invariants` — list of `AIV-` entities introduced or affected
- `related_principles` — list of Principle entities this ADR aligns with

**Status:** Proposed. Template stored in ACMP's Template module. No ADR required (template content; implementation detail).

### F.3 Architecture Invariant Fitness Function Gates (Proposal)

**Proposal:** For key invariants (especially "no cross-module table access"), implement automated ArchUnit-style tests (or a custom Roslyn analyzer) that fail CI if violated.

**Status:** Proposed for Phase 2. Phase 1 relies on code review. Raise as a Phase 2 implementation task.

### F.4 Bilingual Terminology Glossary as Governed Artefact (Proposal)

**Proposal:** The EN↔AR terminology mapping for ACMP's domain (Architecture Committee, Backlog, ADR, Invariant, Vote, Quorum, etc.) is stored as a governed artefact in the Knowledge module, with versioning and approval. Changes to bilingual terminology require Secretary approval.

**Rationale:** Consistent bilingual terminology across UI labels, notifications, reports, and exported documents is a governance quality concern, not just a translation task. It needs a workflow.

**Status:** Proposed. Design handoff (`design-handoff/`) covers the initial EN↔AR term list. The governed-artefact workflow is a post-v1 enhancement.

---

## Traceability

- Deliverable 30 (`docs/22-standards-and-best-practices.md`)
- **§A (Concept Disambiguation) is referenced by README §G** — single source of truth; do not duplicate in other documents
- Informs: `docs/24-security-threat-model.md`, `docs/25-security-controls.md`, `docs/26-audit-and-records-management.md`, `docs/31-testing-strategy.md`, `docs/32-devsecops-plan.md`, `docs/33-containerization-and-deployment.md`, design handoff
- ADRs: ADR-0001 (modular monolith → §C.3), ADR-0002 (.NET → §C.3), ADR-0009 (immutability → §B.2, §B.4 ASVS V8, §D.4 records), ADR-0012 (React/RTL → §D.5)
- Settled decisions confirmed: OWASP ASVS L2 target; WCAG 2.2 AA target; arc42 doc structure; C4 diagrams via Tarseem; MADR template extended; Gregorian only; AI extraction Phase 3 only (LLM01 applies)
- Open decisions raised: None new (existing OQs cover AI extraction scope)
