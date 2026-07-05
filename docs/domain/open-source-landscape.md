# 21 — Open-Source & Commercial Landscape Analysis (Deliverable 29)

**Purpose:** Survey tools with functional overlap to ACMP, determine what to adopt, integrate, or learn from, and justify why no single existing product satisfies architecture-committee governance + traceability + EN/AR/RTL + on-prem gov constraints.

---

## 1. Scope of Survey

This survey covers four categories of tooling that have non-trivial overlap with one or more ACMP modules:

1. **ADR tooling** — lifecycle management of Architecture Decision Records
2. **Developer portals / software catalogs** — entity models, docs-as-code, plugin systems
3. **Architecture-as-code / diagramming** — structured diagram generation
4. **Board/committee/meeting governance SaaS** — meeting management, agenda, voting, minutes
5. **Decision-log / RFC-process tools** — lightweight decision tracking in software teams
6. **Wiki / knowledge-base platforms** — documentation and knowledge management

Evaluation criteria applied uniformly: functional overlap, stack fit, license, maturity, security posture, extensibility, deployment model, EN/AR/RTL support, government on-prem suitability, and verdict.

---

## 2. Evaluation Criteria Definitions

| Criterion | What is assessed |
|---|---|
| **Functional overlap** | Which ACMP modules/features the tool addresses |
| **Stack** | Language, runtime, framework |
| **License** | Open-source licence or proprietary |
| **Maturity** | Release history, maintenance cadence, community health |
| **Security posture** | Auth model, supply-chain risk, CVE history (where known) |
| **Extensibility** | Plugin/API surface for custom behaviour |
| **Deployment model** | Self-hosted / SaaS / hybrid |
| **RTL/bilingual** | Native Arabic/RTL or feasible to add |
| **Gov-suitability** | On-prem, data residency, sensitivity constraints |
| **Verdict** | See key below |

**Verdict key:**
- **Adopt** — use the tool as-is within ACMP's own deployment
- **Integrate** — call or embed the tool as a service/sidecar
- **Fork** — copy and substantially modify
- **Reuse-partially** — lift specific patterns, schemas, or components; do not run the tool
- **Learn-only** — study for product/UX ideas; do not include in any dependency
- **Reject** — not suitable; no value to extract

---

## 3. Category A — ADR Tooling

### 3.1 Log4brains (`thomvaill/log4brains`)

| Field | Detail |
|---|---|
| **Purpose** | Docs-as-code ADR management: Markdown files in a Git repo, static-site preview, MADR template, timeline, full-text search |
| **Functional overlap** | ADR lifecycle (Draft→Proposed→Accepted/Deprecated/Superseded), ADR repository browser, linking between ADRs |
| **Stack** | Node.js (TypeScript); static site output (React); Git as storage |
| **License** | MIT |
| **Maturity** | Active (2020–present); ~3 k GitHub stars [unverified]; MADR 3.x template default |
| **Security posture** | Static-site generator; no auth layer (assumes Git access control); low attack surface if not exposed |
| **Extensibility** | Custom templates; limited plugin surface |
| **Deployment model** | CLI tool + generated static site; no server component |
| **RTL/bilingual** | No native RTL; adding RTL to generated static site would require forking the frontend |
| **Gov-suitability** | Self-hosted static site; Git repo is the store — fits gov if Git is internal |
| **Reusable ideas** | MADR template structure; docs-as-code pattern; supersede/deprecate lifecycle vocabulary; single-ADR-per-file convention |
| **Limitations** | No auth, no committee workflow, no voting, no traceability to topics/decisions, no bilingual support, no in-app integration surface |
| **Verdict** | **Reuse-partially** — adopt the MADR template as the in-app ADR template (see §22); adopt docs-as-code philosophy; do not run Log4brains |

Source: https://github.com/thomvaill/log4brains · https://adr.github.io

---

### 3.2 adr-tools (`npryce/adr-tools`)

| Field | Detail |
|---|---|
| **Purpose** | Shell-script toolchain for creating/listing/superseding Nygard-format ADRs as Markdown files |
| **Functional overlap** | ADR create/supersede; templating |
| **Stack** | Bash; Markdown |
| **License** | MIT |
| **Maturity** | Stable but minimally maintained; Nygard format is the original ADR format |
| **Security posture** | Shell scripts; trivially auditable; no server |
| **Extensibility** | Shell; forkable |
| **Deployment model** | CLI only |
| **RTL/bilingual** | No |
| **Gov-suitability** | Trivially self-hosted; no data concerns |
| **Reusable ideas** | Nygard format vocabulary (Context/Decision/Status/Consequences) — useful conceptually; MADR is preferred for ACMP |
| **Limitations** | No UI, no workflow, no integration, no bilingual support, Bash-only |
| **Verdict** | **Learn-only** — useful for understanding the canonical ADR format; MADR is preferred |

Source: https://adr.github.io

---

### 3.3 dotnet-adr

| Field | Detail |
|---|---|
| **Purpose** | .NET global tool for creating and managing ADRs in the Nygard and MADR formats |
| **Functional overlap** | ADR creation; template selection |
| **Stack** | .NET (C#); CLI tool |
| **License** | MIT [unverified] |
| **Maturity** | Moderate; less widely adopted than Log4brains or adr-tools |
| **Security posture** | .NET global tool; installs via NuGet; supply-chain exposure normal for NuGet ecosystem |
| **Extensibility** | Limited |
| **Deployment model** | Developer workstation CLI |
| **RTL/bilingual** | No |
| **Gov-suitability** | Dev tool; not a deployed service |
| **Reusable ideas** | Demonstrates MADR implementation in .NET ecosystem; confirms template viability |
| **Limitations** | No web UI, no workflow, no server |
| **Verdict** | **Learn-only** — confirms MADR is feasible in .NET context; not adopted |

---

### 3.4 MADR (Markdown Any Decision Records)

| Field | Detail |
|---|---|
| **Purpose** | Template standard for structured ADRs: YAML front-matter + Markdown body; fields: title, status, deciders, date, consulted, informed, context, options, pros/cons, decision, consequences |
| **Functional overlap** | ADR template content model; directly maps to ACMP's in-app ADR entity |
| **Stack** | Template only (Markdown + YAML); language-agnostic |
| **License** | CC0 (public domain) |
| **Maturity** | Stable; v3.x; adopted by Log4brains, dotnet-adr, and the wider ADR community |
| **Security posture** | A template; no runtime risk |
| **Extensibility** | Fields are a baseline; ACMP extends with committee-specific fields (voting reference, related-topic, affected-systems) |
| **Deployment model** | N/A — template only |
| **RTL/bilingual** | Content is bilingual by authorship; template fields will have EN/AR labels in ACMP |
| **Gov-suitability** | Perfect — template stored as structured data in ACMP's DB |
| **Reusable ideas** | **All fields** — adopt MADR as the baseline ADR template for ACMP's in-app ADR entity |
| **Limitations** | Does not cover committee-specific fields; those are ACMP extensions |
| **Verdict** | **Reuse-partially** — adopt MADR field structure as the canonical ADR template; extend with ACMP fields |

Source: https://adr.github.io/madr/

---

## 4. Category B — Developer Portals / Software Catalogs

### 4.1 Backstage (`backstage/backstage`)

| Field | Detail |
|---|---|
| **Purpose** | Open-source developer portal (IDP): software catalog, TechDocs (docs-as-code Markdown), software templates (scaffolding), plugin system, 200+ community plugins |
| **Functional overlap** | Entity/catalog model (Service, Component, API, Group, User); docs-as-code (TechDocs = Markdown → static site); search; plugin marketplace analogy |
| **Stack** | React (frontend) + Node.js (backend); requires PostgreSQL or SQLite; optional search backends |
| **License** | Apache 2.0 (CNCF Incubating project) |
| **Maturity** | Very high — created at Spotify, 3,400+ adopting organizations [unverified], active CNCF ecosystem |
| **Security posture** | Auth is pluggable (OIDC, SAML, GitHub); large attack surface (many plugins, Node.js runtime); security varies by plugin; CVEs in dependency tree are common at this scale |
| **Extensibility** | Extremely high — plugin system is the product's core value |
| **Deployment model** | Self-hosted (Kubernetes recommended; Docker Compose possible for small installs) |
| **RTL/bilingual** | No native Arabic/RTL support; would require significant plugin and theme work |
| **Gov-suitability** | Self-hosted possible; Kubernetes complexity; data in PostgreSQL; plugin supply chain must be audited carefully for gov use |
| **Reusable ideas** | **Entity-catalog model** (owner, system, component, API — maps to ACMP's topic → system/service relationship); **docs-as-code pattern** (Markdown as authoritative source, rendered in-browser); **plugin architecture philosophy** (extensibility via well-defined interfaces) |
| **Limitations** | Dramatically over-engineered for a ≤20-user internal committee tool; requires Node.js + PostgreSQL backend; no Arabic/RTL; no committee workflow, no voting, no ADR lifecycle; plugin security audit burden; off-mission |
| **Verdict** | **Learn-only** — entity-catalog model and docs-as-code pattern are valuable conceptual inputs; do not adopt or deploy Backstage |

Source: https://backstage.io · https://github.com/backstage/backstage

---

## 5. Category C — Architecture-as-Code / Diagramming

### 5.1 Structurizr / C4 DSL

| Field | Detail |
|---|---|
| **Purpose** | Architecture-as-code: DSL to define C4-level models (System Context, Container, Component, Code, Dynamic, Deployment); renders to multiple diagram formats |
| **Functional overlap** | Diagram creation; C4-model vocabulary; architecture documentation |
| **Stack** | Java (Structurizr server); DSL is text; various renderers including PlantUML, Mermaid, Graphviz |
| **License** | Apache 2.0 (core); Structurizr cloud is SaaS |
| **Maturity** | Mature; C4 model is well-established; Structurizr Lite is self-hostable |
| **Security posture** | Self-hosted Java service; workspace files on disk; auth is basic in Lite edition |
| **Extensibility** | Limited unless forking the DSL parser |
| **Deployment model** | Self-hosted (Structurizr Lite) or SaaS; Docker available |
| **RTL/bilingual** | No native RTL/Arabic; diagram labels are plain text (could write Arabic but layout won't mirror) |
| **Gov-suitability** | Self-hostable via Docker |
| **Reusable ideas** | C4 vocabulary (Person, System, Container, Component) directly maps to Tarseem's `architecture/C4` diagram family — already integrated via ADR-0006 |
| **Limitations** | ACMP already has Tarseem for diagram rendering (ADR-0006 settled); Tarseem has native Arabic/RTL support that Structurizr lacks; adding a second diagram engine creates duplication |
| **Verdict** | **Learn-only** — C4 model vocabulary is already incorporated via Tarseem (ADR-0006); do not add Structurizr |

Source: https://c4model.com · https://structurizr.com

### 5.2 Tarseem (`A-H-911/tarseem`)

Already settled as **Integrate** (ADR-0006). See `docs/domain/tarseem-analysis.md` for full analysis. Not re-evaluated here.

---

## 6. Category D — Board / Committee / Meeting Governance SaaS

> **Policy note:** All products in this category are commercial SaaS with no self-hosted/on-prem offering. They cannot be deployed inside a sensitive government network. Evaluation is for **learn-only** product intelligence.

### 6.1 Comparative Summary Table

| Product | Core capability | Architecture-specific? | On-prem? | Arabic/RTL? | Gov-suitability | Verdict |
|---|---|---|---|---|---|---|
| **BoardEffect** | Board portal: meetings, minutes, voting, secure docs, director collaboration | No (generic board governance) | No (SaaS only) | Unknown [unverified] | Not self-hostable; US cloud | Learn-only |
| **OnBoard** | Board management: agenda, minutes, voting, action items, approvals | No (generic) | No | No [unverified] | Not self-hostable | Learn-only |
| **Diligent Boards** | Enterprise board governance: secure docs, voting, minutes, D&O questionnaires | No (generic board) | No (enterprise SaaS) | Limited [unverified] | Regulated-industry focus but cloud | Learn-only |
| **Decisions (for Teams)** | MS Teams-native meeting management: agenda, notes, actions, follow-up | No (general meetings) | No (Teams/cloud) | Via Teams locale | Not self-hostable; Teams-only | Learn-only |
| **Fellow** | Meeting productivity: agenda, notes, action items, 1:1s, manager tools | No (general meetings) | No (SaaS) | No [unverified] | Not self-hostable | Learn-only |
| **Hugo** | Meeting minutes, decisions, actions; integrates with calendar/video | No (general meetings) | No (SaaS) | Unknown [unverified] | Not self-hostable | Learn-only |

### 6.2 Transferable Patterns (learn-only)

Despite none of these tools being adoptable, they collectively confirm several product patterns worth incorporating:

- **Agenda-item time-boxing** — all board/meeting tools prominently feature time estimates per agenda item; confirms ACMP's agenda time-box requirement.
- **Action-item ownership + due-date tracking** — universal; confirms the ACMP Actions module design.
- **"One-click minutes"** — meeting notes compiled from agenda + decisions + actions; confirms the MoM auto-generation workflow.
- **Pre-meeting packet** — attach docs/slides before the meeting for review; confirms ACMP's pre-meeting preparation feature.
- **Voting record as immutable audit** — all enterprise board tools record votes as permanent audit trail; confirms ADR-0009 (immutability).
- **Role-based access to board materials** — members vs. directors vs. observers; confirms ACMP's role/stream-visibility model.

---

## 7. Category E — Decision-Log / RFC-Process Tools

### 7.1 GitHub Discussions / Pull Request–based RFC process

| Field | Detail |
|---|---|
| **Purpose** | Lightweight RFC/decision-log using GitHub PRs or Discussions: proposal → review comments → merge = decision |
| **Functional overlap** | Decision discussion, versioned history, lightweight review workflow |
| **Stack** | GitHub (SaaS) or self-hosted Gitea/Forgejo |
| **License** | Proprietary (GitHub); MIT (Gitea/Forgejo) |
| **Maturity** | Very high (GitHub); moderate (Gitea) |
| **Security posture** | GitHub: cloud; Gitea: self-hosted, simpler attack surface |
| **RTL/bilingual** | GitHub: partial locale support; no native RTL |
| **Gov-suitability** | Gitea self-hosted possible; not architecture-committee-specific |
| **Reusable ideas** | PR-based review process maps to ACMP's ADR proposal/review flow; Markdown-as-source |
| **Limitations** | No committee workflow, no voting, no formal agenda, no MoM, no traceability graph |
| **Verdict** | **Learn-only** — PR-review-as-decision-review is a useful pattern; ACMP's ADR lifecycle is richer |

### 7.2 Linear / Notion (decision tracking via pages/issues)

Many teams track decisions in Notion databases or Linear issues. These tools have broad functionality but no architecture-committee-specific workflow, no immutable vote records, no RTL, and are SaaS-only. **Learn-only** for UX patterns (Kanban backlog, status chips, timeline view).

---

## 8. Category F — Wiki / Knowledge-Base Platforms

### 8.1 Confluence (Atlassian)

| Field | Detail |
|---|---|
| **Purpose** | Enterprise wiki: pages, spaces, macros, templates, comments, versioning |
| **Functional overlap** | Knowledge base / documentation (ACMP Knowledge module); page versioning |
| **Stack** | Java (Server/Data Center); cloud SaaS |
| **License** | Proprietary |
| **Maturity** | High (industry-standard) |
| **Security posture** | Data Center edition is self-hosted; mature security model; admin complexity |
| **RTL/bilingual** | Partial RTL support in newer versions; not first-class |
| **Gov-suitability** | Data Center self-hostable; expensive licensing |
| **Reusable ideas** | Page-versioning UI; inline comments; "last edited by" audit trail |
| **Limitations** | Proprietary; licensing cost; not architecture-committee-specific; no traceability to decisions/ADRs; RTL secondary |
| **Verdict** | **Learn-only** — versioned-page UX pattern is useful; not adopted |

### 8.2 Outline (`outline/outline`)

| Field | Detail |
|---|---|
| **Purpose** | Open-source team wiki: nested docs, Markdown, comments, versioning, search, OIDC auth |
| **Functional overlap** | ACMP Knowledge module (Markdown docs, versioning, cross-links) |
| **Stack** | Node.js + React; PostgreSQL or SQLite |
| **License** | BSL 1.1 (not OSI-approved for commercial hosting, but self-hosted internal use is fine) |
| **Maturity** | High (~25 k stars [unverified]); active development |
| **Security posture** | Self-hosted; OIDC integration; auth-aware; generally clean security posture |
| **Extensibility** | Limited plugin surface |
| **Deployment model** | Self-hosted via Docker |
| **RTL/bilingual** | Community-contributed RTL CSS improvements exist but not first-class [unverified] |
| **Gov-suitability** | Self-hostable; requires PostgreSQL (not SQL Server) — **stack mismatch** |
| **Reusable ideas** | Nested-document tree; inline Markdown editor; cross-link syntax (`[[doc-name]]`); versioning history UX |
| **Limitations** | BSL licence has hosting restrictions (review for gov); PostgreSQL dependency conflicts with ACMP's SQL Server mandate (ADR-0003); RTL not first-class; adding it requires a second DB engine |
| **Verdict** | **Learn-only** — nested-doc UX and cross-link syntax are useful design inputs; not adopted (stack/licence mismatch) |

### 8.3 TipTap / ProseMirror (rich-text editor libraries)

| Field | Detail |
|---|---|
| **Purpose** | Headless rich-text / Markdown editor libraries for embedding in web apps |
| **Functional overlap** | ACMP's in-app Markdown editor for Meeting Notes, MoM, ADR body, Knowledge docs |
| **Stack** | JavaScript/TypeScript; framework-agnostic (TipTap has React adapter) |
| **License** | MIT (TipTap OSS core); Pro extensions are paid |
| **Maturity** | Very high; ProseMirror is the industry standard; TipTap is the most ergonomic React wrapper |
| **Security posture** | Client-side library; content sanitization must be applied server-side (OWASP ASVS V5) |
| **Extensibility** | Very high — extension system |
| **RTL/bilingual** | RTL text direction is supported in ProseMirror/TipTap (bidirectional text); requires `dir` attribute configuration |
| **Gov-suitability** | A client-side library — no deployment concern |
| **Reusable ideas** | **All** — this is a strong candidate for ACMP's embedded Markdown editor |
| **Limitations** | Pro extensions cost money; OSS core covers most needs; sanitization discipline needed |
| **Verdict** | **Adopt** — use TipTap (OSS core) or a comparable ProseMirror-based library as ACMP's embedded rich-text/Markdown editor for Meeting Notes, MoM, ADR body, and Knowledge docs. No full wiki platform needed. |

---

## 9. Cross-Cutting Patterns Worth Adopting

These patterns appear consistently across the surveyed tools and map cleanly onto ACMP's design. None require adopting the tools themselves.

| Pattern | Source tool(s) | How ACMP applies it |
|---|---|---|
| **Docs-as-code** | Backstage (TechDocs), Log4brains | ADRs and Architecture Invariants stored as Markdown in DB; version-controlled; rendered in-browser |
| **Entity catalog / typed entities** | Backstage | ACMP's `System/Service` catalog (governed entities); typed relationships (belongs-to, depends-on, relates-to) |
| **MADR ADR template** | Log4brains, MADR spec | Baseline ADR template in ACMP's template library; extended with committee-specific fields |
| **Immutable vote record** | BoardEffect, Diligent, Decisions | ADR-0009: votes + issued decisions immutable; append-only audit log |
| **Agenda time-boxing** | BoardEffect, OnBoard, Hugo | Agenda item has `estimated_minutes`; total vs allocated visible to Secretary |
| **Pre-meeting packet** | BoardEffect, Diligent | Topic `PreparedStatus` exposes attachments/slides for review before meeting |
| **Action-item + due-date from meeting** | Fellow, Hugo, OnBoard | ACMP Actions module: action created inline during meeting, owner/due/status tracked |
| **Supersede lifecycle for decisions** | adr-tools, Log4brains | ADR status `Superseded`; superseding ADR references the original |
| **ProseMirror/TipTap editor** | Outline, Notion | Embedded rich-text editor in ACMP for MoM, notes, ADR body |

---

## 10. Conclusion

No single open-source or commercial product covers the intersection of:
- Architecture-committee governance workflow (intake → backlog → agenda → meeting → vote → decision → ADR)
- Typed traceability graph (topic → decision → ADR → invariant → action → risk → dependency)
- EN/AR full RTL bilingual support (first-class, not a bolt-on)
- On-prem government deployment (sensitive network, no cloud dependency)
- SQL Server as the mandated datastore
- Self-contained, low-traffic (≤20 users), Docker Compose deployment

**Recommendation:** Build the thin domain core for architecture-committee governance. Integrate Tarseem (ADR-0006) for diagramming. Integrate Keycloak (ADR-0004) for identity. Treat Keystone as an optional companion capability (ADR-0007). Adopt patterns — docs-as-code, MADR template, entity-catalog model, immutable audit trail, agenda time-boxing — from the tools surveyed above. Adopt TipTap (OSS) as the embedded Markdown editor. Do not deploy Backstage, Structurizr, Outline, or any board SaaS.

---

## Traceability

- Deliverable 29 (`docs/domain/open-source-landscape.md`)
- Informs: `docs/domain/build-vs-buy-vs-integrate.md` (Deliverable 31), `docs/domain/standards-and-best-practices.md` (Deliverable 30)
- ADRs informed: ADR-0001, ADR-0002, ADR-0003, ADR-0006, ADR-0007
- Settled decisions confirmed: CON-001 (self-contained); Tarseem integrated (not Structurizr); Keystone optional; no Backstage; no board SaaS; MADR template adopted
- Related: `docs/domain/tarseem-analysis.md`, `docs/domain/keystone-analysis.md`
