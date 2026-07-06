# 35 — Documentation Plan (Deliverable 43)

**Purpose:** Define what documentation lives where, in what format, owned by whom, updated on what trigger — so the planning package, the engineering codebase, and the in-app committee knowledge base remain consistent, non-redundant, and never go stale.

> Stack: docs-as-code in VCS; MADR ADRs in `docs/adrs/`; Swagger/OpenAPI auto-generated; per-module READMEs; arc42 architecture doc; EN/AR user guides; in-app wiki (Markdown, Knowledge module) for committee knowledge. Tarseem diagram JSON specs versioned in repo.

---

## 1. The Two Documentation Realms

| Realm | What lives here | Primary audience |
|---|---|---|
| **Repo docs** (`/docs`, `/adr`, module READMEs, runbooks) | Engineering: architecture decisions, planning package, deployment runbooks, API reference, contribution guide, per-module design notes | Engineers, Claude Code agent, DevSecOps |
| **In-app wiki** (Knowledge module, `Document` + `Template` aggregates) | Committee knowledge: governance standards, principles, templates for topics/ADRs/minutes, how-to guides for committee roles, glossary, decision templates | Committee members, Secretary, Chairman, Submitter |

**Hard boundary:** engineering implementation details (ADRs, architecture diagrams, migration guides, CI runbooks) belong in the **repo**, not the in-app wiki. Committee process guides, terminology, and reusable templates belong in the **in-app wiki**, not the repo. Crossing this boundary causes duplication and version drift.

---

## 2. Documentation Inventory Table

| Doc type | Location | Format | Owner | Update trigger |
|---|---|---|---|---|
| **Planning package** (Deliverables 1–59) | the foldered registers under `/docs` (see `docs/README.md` §Package layout) | Markdown | Lead secretary | Requirement / decision change; phase gate |
| **arc42 architecture doc** | `/docs/arc42/` (generated from `/docs/domain/architecture-detail.md`) | Markdown | Lead engineer | Architecture decision change (ADR update) |
| **ADRs** | `/docs/adrs/adr-NNNN-*.md` | MADR Markdown | Decision proposer + lead | New architectural decision; superseding |
| **Per-module README** | `/src/Modules/<Module>/README.md` | Markdown | Module lead | Module interface change; new features |
| **Shared Kernel README** | `/src/BuildingBlocks/Acmp.Shared/README.md` | Markdown | Lead engineer | Shared contract / interface change |
| **API reference (OpenAPI)** | `/docs/api/openapi.json` + served via Swagger UI at `/swagger` | OpenAPI 3.1 JSON | Auto-generated (CI) | Any endpoint change (auto on build) |
| **Tarseem diagram JSON specs** | `/docs/diagrams/*.tarseem.json` | Tarseem JSON (schema v1.0) | Author of diagram | Architecture / topology change |
| **Deployment runbook** | `/docs/runbooks/` + `/deploy/README.md` | Markdown | DevSecOps lead | Infrastructure or compose topology change |
| **User guide (EN)** | `/docs/user-guide/en/user-guide.md` | Markdown | Secretary | Feature release; UX change |
| **User guide (AR)** | `/docs/user-guide/ar/دليل-المستخدم.md` | Markdown (Arabic) | Secretary (AR-fluent reviewer) | Same as EN guide; translated after EN update |
| **In-app wiki: governance standards** | Knowledge module (DB) | Markdown (in-app editor) | Chairman / Secretary | Committee process change |
| **In-app wiki: committee templates** | Knowledge module (DB) | Markdown | Secretary | Template revision |
| **In-app wiki: glossary** | Knowledge module (DB) | Markdown | Secretary | Term addition / clarification |
| **In-app wiki: how-to guides** | Knowledge module (DB) | Markdown | Secretary | Platform release with new features |
| **Contribution guide** | `/CONTRIBUTING.md` (root) | Markdown | Lead engineer | Team / branching / PR convention change |
| **CLAUDE.md (agent context)** | `/CLAUDE.md` (root) | Markdown | Lead engineer | Module additions; guardrail changes; new conventions |
| **Security exemptions** | `/docs/security-exemptions.md` | Markdown | Security owner | Any new CVE exemption granted |
| **Release notes** | `/docs/releases/<version>.md` | Markdown (auto from git log) | CI + lead | Each release tag |

---

## 3. ADR Process (MADR in Repo)

### 3.1 Template (MADR-lite)

```markdown
# ADR-NNNN: <Title>

**Status:** Proposed | Accepted | Deprecated | Superseded by ADR-NNNN
**Deciders:** <names or roles>
**Date:** YYYY-MM-DD

## Context
<What is the problem? What forces are in tension? Constraints?>

## Decision
<What is the chosen solution?>

## Consequences
**Positive:** …
**Negative / trade-offs:** …
**Risks:** …

## Options considered
| Option | Pros | Cons |
|---|---|---|
| A | … | … |
| B | … | … |

## Validation
<How will we know this decision was correct?>

## References
<Links to planning docs, NFR IDs, ADR IDs>
```

Source: adr.github.io/madr — adapted to a shorter "MADR-lite" form suited to an on-prem, single-team project. Full MADR template available at adr.github.io/madr if more detail is needed.

### 3.2 Numbering and lifecycle

| Step | Action |
|---|---|
| **Create** | Create `docs/adrs/adr-NNNN-<kebab-title>.md` following the conventions in `docs/adrs/README.md`; set status `Proposed` |
| **Propose** | Open a PR; link to relevant FR/NFR/OQ in planning package |
| **Accept** | PR merged; status → `Accepted`; date updated |
| **Supersede** | New ADR references old one: `Superseded by ADR-MMMM`; old ADR updated to `Superseded by ADR-MMMM` status — **original text preserved** (immutable after Accepted, per ADR-0009 philosophy) |
| **Deprecate** | Decision no longer relevant (e.g., Phase 3 deferral); set status `Deprecated`; add reason |

**Numbering:** sequential 4-digit, starting at 0001. The 12 settled ADRs (ADR-0001 … ADR-0012) are already accepted; new ones start at 0013.

**Rule:** never delete an ADR file. Superseded/deprecated ADRs remain in the repo — they are part of the decision history. This mirrors the audit/immutability principle (ADR-0009).

### 3.3 In-app ADRs vs repo ADRs

| Type | Location | Tool |
|---|---|---|
| **Repo ADRs** (engineering decisions about the ACMP platform itself) | `/docs/adrs/adr-NNNN-*.md` | MADR Markdown in VCS |
| **In-app ADRs** (committee governance decisions about org architecture, recorded in ACMP) | Knowledge module / Governance module (entities with `ADR-` prefix per README §F) | The ACMP application itself |

These are **distinct** — do not conflate. Repo ADRs govern how ACMP is built; in-app ADRs are the committee's governance output that ACMP exists to produce.

---

## 4. Diagram Specs — Versioned in Repo

Per ADR-0006, the **Tarseem JSON spec is the source of truth** for all architecture diagrams. Generated artifacts (SVG/PNG/PDF/drawio) are derived outputs.

```
docs/diagrams/
├── c4-system-context.tarseem.json      ← C4 Level 1 (system context)
├── c4-container.tarseem.json           ← C4 Level 2 (container topology)
├── c4-modules.tarseem.json             ← C4 Level 3 (module map)
├── data-model.tarseem.json             ← ER diagram
├── topic-lifecycle.tarseem.json        ← State diagram (topic FSM)
├── vote-sequence.tarseem.json          ← Sequence diagram (voting flow)
└── generated/                          ← .gitignored; regenerated by CI or Tarseem sidecar
    ├── c4-system-context.svg
    └── …
```

**Diff-ability:** Tarseem JSON is structured and diffable; a changed diagram = a changed spec file = a normal PR review. SVG artifacts are not committed (binary-equivalent churn in git).

**Regeneration:** on merge to main, CI (or Tarseem sidecar in Phase 2) regenerates `generated/` artifacts and publishes them to the docs site or attaches them to the release.

---

## 5. Per-Module README Convention

Each module's `README.md` (at `src/Modules/<Module>/README.md`) must contain:

1. **One-line purpose** of the module.
2. **Aggregate roots** and their key behaviors.
3. **Public contracts** (what other modules may call).
4. **Domain events** published.
5. **Domain events** subscribed to (and handlers).
6. **SQL schema name** + link to EF migration folder.
7. **Key business rules** (invariants enforced in domain).
8. **Authorization scope**: which roles can trigger which commands.
9. **Known limitations / open decisions** referencing `OQ-` or ADR IDs.

This README is read by the Claude Code agent when working in the module and by engineers during code review. It must be updated on any interface change — enforced by `CODEOWNERS` (module lead is owner).

---

## 6. API Documentation (OpenAPI / Swagger)

**Auto-generation:** Swashbuckle (or Scalar [unverified: .NET 8 compatibility]) generates OpenAPI 3.1 spec from XML doc comments on endpoint/DTO types.

**Requirements:**
- Every endpoint has a summary (EN) and, where space permits, an Arabic description in the doc comment.
- Every DTO property has an XML doc comment.
- Security schemes (Bearer JWT) documented in the spec.
- The spec is exported during CI (`scripts/update-openapi.sh`) and committed to `/docs/api/openapi.json`.
- Swagger UI served at `/swagger` in dev and staging; **disabled in prod** (or restricted to Administrator role) [unverified: Swashbuckle prod-disable pattern].

**Versioning:** API is unversioned in v1 (single version). If breaking changes are needed in a future phase, a URL path version (`/api/v2/`) strategy is adopted — not header versioning. Document this in an ADR if/when it arises (`OQ-` candidate).

---

## 7. Preventing Stale / Orphaned Docs

| Risk | Mitigation |
|---|---|
| Planning doc out of date after decision change | Doc-change PR required alongside any ADR update; `CODEOWNERS` enforces lead review |
| Module README drifts from code | Module README is in CODEOWNERS scope of module lead; PR template checklist: "Module README updated?" |
| User guide EN/AR diverges | AR guide update is part of Definition of Done for any user-visible feature (see `docs/execution/definition-of-done.md`) |
| ADR superseded but original not updated | ADR process step: PR that creates new ADR must update old ADR's status header |
| OpenAPI spec out of date | CI regenerates and commits diff; a stale spec = a CI diff = a visible PR |
| Diagram spec (Tarseem JSON) out of date | Spec is in VCS; topology change → PR modifies spec → CI re-renders artifacts |
| In-app wiki stale | No automated gate; quarterly review cadence (see §8); Secretary is owner |
| Broken cross-doc links | `scripts/check-links.sh` (lychee [unverified] or markdown-link-check) runs in CI weekly; broken links = warning (not block in PH-1; block in PH-2) |
| i18n keys stale | `scripts/check-i18n.sh` in CI: EN/AR key parity enforced (block on divergence) |

---

## 8. Review Cadence and Ownership

| Doc class | Review cadence | Owner | Review trigger (also) |
|---|---|---|---|
| Planning package | Phase gate | Lead secretary | Major scope/decision change |
| ADRs | On creation / supersession | Proposer + lead | Always PR-reviewed |
| Architecture doc (arc42) | Phase gate + ADR acceptance | Lead engineer | Any ADR acceptance |
| Per-module READMEs | Sprint boundary | Module lead | Module interface change |
| API docs (OpenAPI) | Continuous (auto) | CI | Any endpoint change |
| Runbooks | Phase gate | DevSecOps lead | Infrastructure change |
| User guides (EN + AR) | Per release | Secretary | Any user-visible feature |
| In-app wiki | Quarterly | Secretary | Committee process change |
| CLAUDE.md | Phase gate + module addition | Lead engineer | New module, new guardrail |
| Security exemptions | 90-day expiry | Security owner | CVE exemption creation |

---

## 9. Onboarding Documentation

New engineers need, in this reading order:

1. `README.md` (root) — what ACMP is, quick-start commands.
2. `CLAUDE.md` — project map, conventions, guardrails (Claude Code agent also reads this).
3. `docs/domain/architecture-detail.md` — macro-architecture, module boundaries.
4. `docs/adrs/` — why key decisions were made.
5. `src/Modules/<assigned-module>/README.md` — module-specific context.
6. `CONTRIBUTING.md` — branching, PR rules, code style.
7. `docs/domain/deployment.md` — how to run the stack locally.

New committee users need (in-app, EN or AR):
1. In-app wiki → "Getting started" guide (created by Secretary before go-live).
2. In-app wiki → Glossary.
3. In-app wiki → Role guide for their specific role.

---

## 10. Planning Package ↔ Repo Mapping

The planning package (this package, `acmp-plan/`) is not the application repo. On execution, the relevant parts are transferred:

| Planning package artefact | Destination in application repo |
|---|---|
| `docs/domain/architecture-detail.md` | → `docs/arc42/` (restructured per arc42) |
| `docs/adrs/adr-0001 … ADR-0012` | → `docs/adrs/adr-0001 … ADR-0012` (copied verbatim, reviewed) |
| `docs/domain/repository-structure.md` | → Scaffolded by Claude Code agent as the actual repo |
| `docs/domain/deployment.md` | → `deploy/README.md` + Dockerfiles + Compose files |
| `docs/validation/test-strategy.md` | → `docs/testing-strategy.md` + encoded as test patterns in code |
| `docs/domain/documentation-plan.md` | → `docs/documentation-plan.md` + triggers CLAUDE.md and per-module README templates |
| `docs/handoff/` prompts | → Orient and drive Claude Code sessions (in-repo since the Keystone migration; the original `execution-handoff/` bootstrap is retired) |
| `design-handoff/` | → Submitted to Claude Design; outputs stored in `docs/design/` |
| `docs/validation/acceptance-criteria.md` | → Encoded as test `DisplayName` tags in test projects |
| `docs/requirements/functional.md` | → Referenced as `FR-###` in backlog/issue tracker |

---

## Traceability

Links: `docs/domain/repository-structure.md` (directory layout implementing this plan) · `docs/domain/devsecops-plan.md` (CI link-check and i18n-check stages) · `docs/domain/keystone-analysis.md` (Keystone gate G-COMPLETE, G-TRACE — applied to planning docs) · `docs/domain/tarseem-analysis.md` (diagram JSON spec conventions) · `docs/adrs/adr-0006-tarseem-diagram-engine.md` · `../README.md` §A (ADR-0009 immutability philosophy applied to ADR lifecycle).
