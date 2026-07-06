# 09 — Topic Taxonomy

**Purpose:** Defines the canonical Topic Type taxonomy, topic attributes, lifecycle mapping, and rationale for its scope — including a devil's-advocate challenge and guidance on optional subtypes as tags. Covers Deliverable 12.

---

## Design Constraints

1. The taxonomy must be **small enough to be unambiguous** — committee members and submitters must categorize a topic correctly without consulting a manual.
2. Type drives: the **required template** used on creation, the **default triage workflow**, and the **SLA urgency thresholds** applied.
3. Urgency is **not a type** — it is a handling-speed attribute that is orthogonal to the kind of work. See Devil's Advocate §D.
4. Subtypes and fine-grained classifications are implemented as **tags**, not types, to avoid taxonomy bloat. See §F.

---

## A. Canonical Topic Types (4)

### Type 1 — ResearchDiscovery (RD)

**Definition:** A topic that requires investigation, analysis, or evidence gathering before a decision can be reached. The committee does not have sufficient information to decide at intake time. The expected output is a Research Mission (RMS-…) → Keystone package → imported Findings/Recommendations → subsequent ArchitectureDecision topic.

**Examples:**
- "Evaluate caching strategies for the API gateway layer" (technology evaluation)
- "Assess migration risk from the embedded auth service to Keycloak"
- "Investigate performance degradation patterns observed in Stream 3 services"
- "Explore options for real-time event streaming to the mobile app"

**Typical lifecycle:**
`Draft → Submitted → Triage → Accepted → Prepared (research conducted via Keystone) → Scheduled → InCommittee → Decided (outcome: ResearchRequired or Approved [findings presented]) → Closed`

Or, if research completes and leads to a new decision: `Decided (Converted) → spawns ArchitectureDecision topic`

**Required template sections:**
1. Problem statement: what question needs answering?
2. Context and background
3. Constraints on the answer (budget, time, technology boundaries)
4. Evaluation criteria (how we will decide between options)
5. Research scope (what is in/out of the research)
6. Expected deliverable (Research Mission reference, Keystone package)
7. Affected streams and systems
8. Timeline / deadline for research completion

---

### Type 2 — ArchitectureDecision (AD)

**Definition:** A topic on which the committee has sufficient information to deliberate and vote, resulting in a formal committee decision (DECN-…) and typically an ADR. The expected output is a decided outcome from the canonical list, a rationale record, and — for significant decisions — an in-app ADR.

**Examples:**
- "Adopt event sourcing for the Stream 2 payments service"
- "Approve the proposed API versioning strategy"
- "Select the technology for the national one-stop app's notification infrastructure"
- "Decide on the decomposition strategy for the monolithic embedded service"

**Typical lifecycle:**
`Draft → Submitted → Triage → Accepted → Prepared (decision brief, presentation ready) → Scheduled → InCommittee → Decided → Closed`

If a decision is ConditionallyApproved: `Decided → conditions tracked as Actions → re-presented as new AD topic once conditions met`

**Required template sections:**
1. Decision statement: what exactly is being decided?
2. Context and drivers
3. Options considered (≥2)
4. For each option: pros, cons, assumptions
5. Recommendation (with rationale)
6. Consequences if approved / rejected
7. Dependencies and affected streams
8. Supporting research or evidence (link to RMS-… or prior findings)
9. Diagrams (link to DGM-…)
10. Related ADRs and invariants

---

### Type 3 — EnhancementInnovation (EI)

**Definition:** A topic proposing a new capability, feature, integration, or innovative change to the governed technical landscape that does not primarily originate from compliance, governance, or a known deficiency — it is driven by improvement, opportunity, or strategic direction. May need research (converted to RD) or may be decided directly.

**Examples:**
- "Introduce an AI-powered fraud detection module for the payments stream"
- "Propose a cross-stream unified search API for the one-stop app"
- "Evaluate and propose adopting a reactive mobile architecture for Stream 1"
- "Introduce performance profiling tooling across all backend services"

**Typical lifecycle:**
`Draft → Submitted → Triage → Accepted (or converted to RD if underdeveloped) → Prepared → Scheduled → InCommittee → Decided → Closed`

Enhancement topics may be converted to ResearchDiscovery if insufficient information is available, or may be directly decided if the proposal is well-formed.

**Required template sections:**
1. Innovation proposal: what is being proposed and why?
2. Current state and pain point or opportunity addressed
3. Expected outcomes and success criteria
4. Technology or approach overview
5. Feasibility assessment (is this achievable within current constraints?)
6. Risks and trade-offs
7. Affected streams and systems
8. Estimated effort and dependencies (high level only — implementation detail is out of scope for governance)
9. Related topics, ADRs, or research

---

### Type 4 — GovernanceStandardization (GS)

**Definition:** A topic that establishes, updates, or retires a governance artifact: an Architecture Invariant (AIV-…), an organization-wide standard, a policy, a principle, or a process rule that applies broadly across streams. The expected output is an active invariant record, an updated standard, or a retired/superseded governance artifact.

**Examples:**
- "Establish the API naming and versioning standard for all stream APIs"
- "Ratify the mobile app accessibility compliance invariant"
- "Retire the deprecated service-to-service synchronous call standard"
- "Establish the data residency policy for gov-sector integrations"
- "Define the mandatory security review gate for embedded services"

**Typical lifecycle:**
`Draft → Submitted → Triage → Accepted → Prepared (standard or invariant draft circulated) → Scheduled → InCommittee → Decided → Closed`

Post-close: the approved governance artifact (AIV-…, standard, policy) is published in the Knowledge module and linked to the decision.

**Required template sections:**
1. Governance artifact type (invariant / standard / policy / principle)
2. Scope (single-stream / multi-stream / platform / org-wide)
3. Statement of the proposed governance rule
4. Rationale
5. Compliance criteria: how will adherence be measured?
6. Exceptions process (if any)
7. Supersedes (link to prior governance artifact if this is a revision)
8. Effective date
9. Affected streams and systems

---

## B. Topic Attributes

Attributes are stored as fields on the Topic entity; they do not create sub-types. Each attribute is independent.

### B.1 — Urgency (3 levels)

| Value | Definition | Aging SLA (time in same status) | Default reminder cadence |
|---|---|---|---|
| `Normal` | Standard committee process; no time pressure beyond the regular meeting cadence. | SLA threshold: 21 days | Weekly digest |
| `Urgent` | Requires committee attention within the current or immediately following meeting cycle; business impact if delayed. | SLA threshold: 7 days | Daily notification to Secretary |
| `Critical` | Requires emergency committee session or earliest possible slot; blocking critical production work, security incident, regulatory deadline. | SLA threshold: 3 days | Immediate alert to Chairman + Secretary |

**Urgency is not a type** — see Devil's Advocate §D for the rationale.

Urgency may be changed by Secretary or Chairman at any point in the topic lifecycle; each change is recorded in the audit log with the actor and reason.

### B.2 — Scope (4 levels)

Scope determines how broadly the topic's decision will affect the org's technical landscape. Scope drives the required reviewer set and the notification audience.

| Value | Definition |
|---|---|
| `SingleStream` | Affects one stream only; other streams may be informed but are not voting stakeholders. |
| `MultiStream` | Affects ≥2 streams; all affected stream directors are voting-eligible (or at minimum must be consulted). |
| `Platform` | Affects the shared platform layer (API gateway, identity, notification platform, observability, DevSecOps pipeline); all streams are indirectly affected. |
| `OrgWide` | Affects the entire organization's technical direction, external partners, or regulatory standing; requires chairman final approval and broad committee quorum. |

### B.3 — Source (submitter channel)

Source records how the topic entered the committee's awareness. It is informational, not a workflow driver.

| Value | Definition |
|---|---|
| `CommitteeMember` | Proposed by a sitting committee member (Chairman, Secretary, Member, technical director). |
| `StreamRequest` | Originated from a stream business or technical director as a formal request to the committee. |
| `UrgentOrgNeed` | Escalated from org leadership (VP, CEO, GM) as a priority need. |
| `OperationalIncident` | Triggered by a production incident, outage, or operational finding that requires governance response. |
| `SecurityFinding` | Originated from a security audit, penetration test, CVE, or threat-model finding. |
| `Modernization` | Proactive modernization initiative (tech debt remediation, platform upgrade, decommission). |
| `InnovationInitiative` | Strategic innovation or R&D initiative proposed by a stream or the committee. |
| `CrossStreamProblem` | Conflict, integration issue, or shared concern surfaced by ≥2 streams independently. |
| `Regulatory` | Driven by a regulatory requirement, government directive, or external compliance obligation. |
| `External` | Submitted by an external partner, embedded service vendor, or government integration party (via internal liaison). |

---

## C. Type-to-Status Lifecycle Mapping

All types share the same canonical status model (README §E). The table below shows which statuses are routinely reached per type and notable divergences.

| Status | RD | AD | EI | GS | Notes |
|---|---|---|---|---|---|
| Draft | ✓ | ✓ | ✓ | ✓ | |
| Submitted | ✓ | ✓ | ✓ | ✓ | |
| Triage | ✓ | ✓ | ✓ | ✓ | |
| Accepted | ✓ | ✓ | ✓ | ✓ | |
| Prepared | ✓ (RMS-… packaged) | ✓ (decision brief) | ✓ (proposal packaged) | ✓ (standard circulated) | |
| Scheduled | ✓ | ✓ | ✓ | ✓ | |
| InCommittee | ✓ | ✓ | ✓ | ✓ | |
| Decided | ✓ | ✓ | ✓ | ✓ | |
| Closed | ✓ | ✓ | ✓ | ✓ | |
| Rejected | ✓ | ✓ | ✓ | ✓ | |
| Deferred | ✓ | ✓ | ✓ | ✓ | |
| Converted | ✓ (→AD) | rare | ✓ (→RD or AD) | rare | RD converts to AD after research; EI converts to RD if underdeveloped |
| Reopened | ✓ | ✓ | ✓ | ✓ | |

---

## D. Mapping from Brief Categories to This Model

The brief (digest §4, §3) describes backlog sources and topic categories without a formal taxonomy. This table maps those informal descriptions to the canonical type model.

| Brief Category / Source | Canonical Type | Rationale |
|---|---|---|
| Architecture evaluation / technology selection | ResearchDiscovery → ArchitectureDecision | Evaluation requires research before decision |
| Cross-stream design conflicts | ArchitectureDecision (scope=MultiStream) | Sufficient information usually exists; needs committee decision |
| Platform standards (API, security, data) | GovernanceStandardization | Output is a standard or invariant |
| Modernization / tech debt | EnhancementInnovation (tag: TechDebtRemediation) | Improvement-driven; uses tag not type |
| Innovation / new features | EnhancementInnovation | Opportunity-driven |
| Security findings → architectural response | GovernanceStandardization or ArchitectureDecision | If it produces a policy/invariant → GS; if it produces a specific design decision → AD |
| Operational incidents requiring architectural response | ArchitectureDecision (source=OperationalIncident) | Enough information to decide; source attribute captures origin |
| Research/discovery (undecided direction) | ResearchDiscovery | By definition |
| Regulatory/compliance requirements | GovernanceStandardization (source=Regulatory) | Output is a policy or invariant |
| External partner integration decisions | ArchitectureDecision (scope=Platform or OrgWide) | Specific decision about how to integrate |

---

## E. Devil's Advocate: Challenging the Taxonomy

### Challenge 1 — Why not more types? "Urgent" feels like a type, not an attribute.

**Argument for "Urgent" as a type:** Committee members intuitively say "we have an urgent topic." Having a separate type would make urgency visible in type-based filters and template selection. Boards and governance bodies sometimes use "Emergency" as a distinct agenda item type.

**Counter-argument (adopted position):** Urgency is a *handling speed*, not a *kind of work*. An "Urgent ArchitectureDecision" and a "Normal ArchitectureDecision" share the same template, the same triage workflow, the same downstream artifacts (ADR, Decision), and the same module routing. The only differences are: SLA threshold, notification cadence, and meeting priority. These are fully captured by the Urgency *attribute*. Making Urgency a type would require either:
- 12 types (4 base × 3 urgency levels) — unusable taxonomy, or
- A hybrid taxonomy where some types are "kinds of work" and some are "handling speeds" — conceptually incoherent.

Moreover, urgency changes. A Normal topic can become Critical when a regulatory deadline is announced. Reclassification by attribute change is trivial; reclassification by type change is a semantic event requiring a Converted status.

**Verdict:** Urgency as an attribute is correct. Urgency SLAs drive Hangfire job schedules, notification templates, and backlog aging indicators — all implemented without type proliferation.

---

### Challenge 2 — Why not split ResearchDiscovery into "EvaluationResearch" and "InvestigativeResearch"?

**Argument:** Technology evaluations (comparing options) feel different from incident investigations (diagnosing problems).

**Counter-argument:** Both produce a Research Mission → Keystone package → Findings/Recommendations → subsequent ArchitectureDecision. The template, lifecycle, and downstream artifacts are identical. The difference is in the *content* of the research, not in how the committee handles it. Tag with `TechnologyEvaluation` or `IncidentInvestigation` for filtering and reporting — do not create two types.

**Verdict:** Single ResearchDiscovery type; subtypes as tags (see §F).

---

### Challenge 3 — Why not a "TechDebtRemediation" type?

**Argument:** Tech debt topics have a distinct urgency/impact profile and deserve their own queue.

**Counter-argument:** Tech debt topics are a subset of EnhancementInnovation — they propose improvements to existing capabilities. The committee's decision process is identical: evaluate the proposal, assess risk, vote, decide, assign actions. Distinguishing tech debt from enhancement creates two types that share template, workflow, and outcomes. The distinction is editorial (helps with reporting and filtering) and is well-served by a tag.

**Verdict:** Tag: `TechDebtRemediation` on an EnhancementInnovation topic. No separate type.

---

### Challenge 4 — Is 4 types enough, or is this under-taxonomized?

**Argument:** Large governance bodies (ISO, IETF, enterprise architecture boards) use richer taxonomies with 10–20 artifact types.

**Counter-argument:** Those taxonomies evolved over decades for large multi-committee bodies with formal standards bodies, external publication requirements, and regulatory mandates. This committee has 1 weekly meeting, 5 streams, ~50 members, and a secretary who currently manages everything in a text file. The primary failure mode is *over-complexity*, not under-classification. The 4-type taxonomy was also confirmed as the intended design by the README (§D), which explicitly states it is "kept deliberately small." A taxonomy that committee members memorize in 5 minutes is more valuable than one that requires a reference guide.

**Verdict:** 4 types is correct for this scale and maturity level. Revisit after 12 months of usage data to determine if tags are consistently overloaded and a 5th type is warranted.

---

## F. Optional Subtypes as Tags (Not Types)

Tags are free-text labels assignable by the topic owner or Secretary; they appear as filterable chips on the backlog. They do not affect lifecycle, template selection, notification routing, or SLA. They provide editorial categorization for search and reporting.

**Recommended standard tag vocabulary (enforced by Secretary; not a hard constraint):**

| Tag | Used With | Purpose |
|---|---|---|
| `TechnologyEvaluation` | ResearchDiscovery | Evaluation of a specific technology, library, or platform option |
| `IncidentInvestigation` | ResearchDiscovery | Architectural investigation triggered by an operational incident |
| `SecurityArch` | ArchitectureDecision, GovernanceStandardization | Security-specific architectural decision or control standard |
| `APIDesign` | ArchitectureDecision, GovernanceStandardization | API contract, versioning, or naming standard |
| `DataArchitecture` | ArchitectureDecision, GovernanceStandardization | Data model, retention, or ownership decisions |
| `TechDebtRemediation` | EnhancementInnovation | Proposal to address accumulated technical debt |
| `MobileArch` | Any | Topics primarily affecting iOS/Android native layers |
| `IntegrationPattern` | ArchitectureDecision | Service-to-service integration, event, or messaging pattern decisions |
| `CrossStreamDesign` | ArchitectureDecision | Scope=MultiStream topics requiring cross-stream coordination |
| `RegCompliance` | GovernanceStandardization | Regulatory or government-mandate-driven governance artifacts |
| `ExternalPartner` | Any | Topics involving embedded service partners or gov integrations |

Tags are not hardcoded in the domain model. The system supports free-text tags; the above vocabulary is a recommended controlled list enforced by coordination convention, not by schema constraint. Tags are indexed in SQL FTS and available as backlog filters.

**Why tags are not types:**
- Tags can be combined (a topic can be both `TechDebtRemediation` and `SecurityArch`)
- Tags can be added or removed without a status transition
- Tags do not affect the committee's decision process, template, or SLA
- Tags serve editorial/reporting needs only

---

## G. Summary Reference Table

| Attribute | Domain | Values | Affects | Enforced |
|---|---|---|---|---|
| **Type** | Canonical | ResearchDiscovery, ArchitectureDecision, EnhancementInnovation, GovernanceStandardization | Template, triage workflow, downstream artifact type | Required; system-validated |
| **Urgency** | Canonical | Normal, Urgent, Critical | SLA threshold, notification cadence, aging indicator | Required; default Normal |
| **Scope** | Canonical | SingleStream, MultiStream, Platform, OrgWide | Required reviewer/voter set, notification audience | Required |
| **Source** | Canonical | 10 values (§B.3) | Reporting/filtering, submitter attribution | Required |
| **Tags** | Editorial | Free-text (recommended vocabulary §F) | Filtering, search, reporting only | Optional; no system enforcement |

---

## Traceability

- Type taxonomy → `README.md §D` (canonical source; this document expands it)
- Urgency SLA thresholds → `docs/requirements/functional.md` FR-038 (aging indicator)
- Type → template mapping → `docs/requirements/functional.md` FR-041 (template selection on creation)
- Scope attribute → `docs/domain/permission-role-matrix.md` (scope drives eligible voter set and notification audience)
- Source attribute → digest §3 (backlog sources; mapped here to canonical values)
- Tags → `docs/requirements/functional.md` FR-031–032 (backlog list/table views with tag filter)
- Status lifecycle → `README.md §E` (canonical; this document adds per-type notes on which statuses are typical)
- Mapping from brief categories → digest §4 (functional scope initial list)
