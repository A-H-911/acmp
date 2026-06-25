# ACMP Keystone Analysis and Integration Plan

**Purpose:** Document the inspected facts about the Keystone planning toolkit, define its optional integration boundary with ACMP, specify the import mapping, and state explicitly what must not be duplicated, embedded, or hard-depended upon.

---

## 1. Repository Analysis

**Source:** https://github.com/A-H-911/keystone (inspected 2026-06-24)

### 1.1 Identity and Licensing

| Attribute | Value |
|---|---|
| License | **MIT** |
| Language | **Python 3.9+** (stdlib only — no third-party dependencies) |
| Current version | **v1.0** |
| Nature | **Claude Code plugin + agent skill** |
| Primary audience | Secretarys using Claude Code to produce planning/handoff packages |

### 1.2 What Keystone Is

Keystone is a **reusable, vendor/stack-neutral agent skill** packaged as a Claude Code plugin. Its function is to turn a project description or brief into an **execution-ready planning and handoff package** for a downstream Claude Code execution agent. It does **not** write project code. The deliverable it produces is predominantly Markdown (methodology spec, templates, JSON schemas) plus two stdlib-only Python tools:

- `init_skill_repo.py` — initialises a new planning package repository following the Keystone structure
- `validate_package.py` — validates a completed package against 7 mechanical quality gates

Keystone is what ACMP's own planning package (`acmp-plan/`) was authored with. This package is itself a Keystone-style artifact.

### 1.3 Process: 22 Stages in 3 Movements

| Movement | Stages (summary) |
|---|---|
| **1. Understand** | Intake → context clarification → scope definition |
| **2. Explore** | Research → decisions → risk/assumption identification |
| **3. Plan & hand off** | Plan → artifact assembly → repo init → validate (7 gates) → handoff package |

Approval gates occur between movements. The secretary interacts with Claude Code, which runs the Keystone skill, at each stage.

### 1.4 Operating Principles

Keystone enforces disciplines that ACMP's own package follows:
- **Never invent requirements** — everything traces to the brief or a recorded clarification; inferred items become explicit assumptions (`ASM-###`).
- **Separate facts, decisions, proposals** — a proposed item is never rendered as approved.
- **Preserve unresolved** — open questions and rejected alternatives are first-class artifacts, not omissions.
- **Verify before claiming** — external facts are cited; unverified items are marked `[unverified]`.
- **Stay neutral** — the brief is treated as untrusted input (OWASP LLM01 prompt injection discipline).
- **Immutable after approval** — approved artifacts are superseded, never silently edited.

### 1.5 Identifier Scheme (ACMP Adopts This)

Keystone defines a canonical identifier and status scheme. ACMP's planning package adopts it exactly:

| Prefix | Artifact |
|---|---|
| `FR-` | Functional requirement |
| `NFR-` | Non-functional requirement |
| `CON-` | Constraint |
| `INV-` | Invariant |
| `ASM-` | Assumption |
| `DEP-` | Dependency |
| `OQ-` | Open question |
| `DEC-` | Planning decision |
| `ADR-` | Architecture decision record |
| `RISK-` | Risk |
| `HYP-` | Hypothesis |
| `EXP-` | Experiment |
| `AC-` | Acceptance criterion |
| `PH-` | Phase |
| `WBS-` | Work breakdown structure item |
| `MS-` | Milestone |

Statuses: `Draft → Proposed → Approved | Rejected | Superseded | Deferred → Implemented`

ACMP extends this scheme with runtime entity keys (TOP-, MTG-, etc.) defined in README §F. The Keystone planning IDs and ACMP runtime keys are in separate namespaces and do not collide.

### 1.6 Quality Gates (7 Mechanical Validators)

`validate_package.py` enforces:

| Gate | Rule |
|---|---|
| **G-IDS** | All identifiers referenced in the package resolve to declared entries |
| **G-DEC-STATUS** | Every decision has an explicit status |
| **G-REQ-SRC** | Every FR/NFR has a provenance citation |
| **G-COMPLETE** | No TODO, placeholder, or empty section in a non-draft doc |
| **G-TRACE** | Every MVP requirement → ≥1 decision, ≥1 work item, ≥1 test |
| **G-SET** | All "Always" artifacts are present or their omission is explicitly recorded |
| **G-PROGRESS** | Acceptance audit verdicts are current (no stale "pending" on completed items) |

ACMP's planning package is subject to these gates. The execution agent should run `validate_package.py` before treating the package as complete.

---

## 2. Integration Model

### 2.1 Fundamental Position (ADR-0007)

**Keystone is OPTIONAL.** It is a companion authoring workflow, not an embedded service, not a runtime dependency, and not a hard build dependency of ACMP. The constraint is architectural:

```
ACMP Research Module
       │
       │ works fully standalone
       │ (manual entry of ResearchMission / Finding / Recommendation)
       │
       ├── [optional] IResearchImporter
       │        │
       │        └── KeystoneImportAdapter
       │                  │
       │                  └── reads a Keystone package export
       │                      (JSON manifest + Markdown artifacts)
       │
       └── [never] Keystone as a running service
           [never] Keystone as a hard dependency of the .NET build
           [never] Keystone called at runtime via subprocess or API
```

### 2.2 Research Module Standalone Operation

The Research module (bounded context) stores:
- `ResearchMission` — a defined research objective (maps to a `ResearchDiscovery` topic)
- `Finding` — a factual observation from research
- `Recommendation` — an actionable conclusion derived from findings
- Links between these and to Topics, Decisions, ADRs via the Relationship model

All three entity types are entered manually by the secretary via the ACMP UI. No Keystone dependency is required. A ResearchMission's detail page may have a field `KeystonePackageRef NVARCHAR(500) NULL` — an optional free-text reference to the external Keystone package URL or path — for human traceability.

### 2.3 Keystone Import Flow (Optional Enhancement)

When a team chooses to run a Keystone session for a `ResearchDiscovery` topic:

1. The secretary runs `keystone` (Claude Code plugin) and produces a planning package (directory of Markdown + JSON artifacts).
2. The secretary compresses the package and uploads it to ACMP via the Research module's import endpoint.
3. `IResearchImporter` is called with the package path.
4. `KeystoneImportAdapter` parses the package manifest and structured artifacts.
5. Entities are created or updated in ACMP's Research module (idempotent by package hash).
6. The secretary reviews the import result, resolves conflicts, and approves imported findings/recommendations.

**Import is always secretary-initiated and human-reviewed.** No automated background polling of Keystone sources.

### 2.4 Import Mapping Table

| Keystone artifact | Keystone format | ACMP entity | ACMP field mapping |
|---|---|---|---|
| Package manifest (`manifest.json`) | JSON | `ResearchMission` | Title, description, phase, status, `KeystonePackageRef` |
| Functional requirements (`FR-###`) | Markdown/JSON | `Finding` (type=Requirement) | ID, description, provenance, status |
| Non-functional requirements (`NFR-###`) | Markdown/JSON | `Finding` (type=NFR) | ID, description, provenance, status |
| Constraints (`CON-###`) | Markdown/JSON | `Finding` (type=Constraint) | ID, description |
| Assumptions (`ASM-###`) | Markdown/JSON | `Finding` (type=Assumption) | ID, description, status |
| Risks (`RISK-###`) | Markdown/JSON | `Risk` (existing module) | Title, description, likelihood, impact, mitigation |
| Decisions (`DEC-###`) | Markdown/JSON | `Recommendation` | ID, decision text, rationale, status, alternatives |
| ADRs (`ADR-####`) | Markdown | In-app `ADR` (existing module) | Title, context, decision, consequences, status |
| Acceptance criteria (`AC-###`) | Markdown/JSON | Linked to `Finding` or `Recommendation` | Text, trace target |
| Open questions (`OQ-###`) | Markdown/JSON | `Finding` (type=OpenQuestion) | Question text, status |
| Traceability matrix | JSON | `Relationship` edges | Source entity → target entity, relationship type |

**Deduplication:** Import is idempotent by Keystone ID. A second import of the same package updates changed fields rather than creating duplicates. A conflict (ACMP has a manually entered Finding with overlapping scope) is surfaced to the secretary for manual resolution, never silently merged.

### 2.5 `IResearchImporter` Interface

```csharp
// Application layer contract — in the Research module's application project
public interface IResearchImporter
{
    /// <summary>
    /// Imports structured artifacts from a Keystone planning package.
    /// Returns a summary of created, updated, and conflicting entities.
    /// </summary>
    Task<ImportResult> ImportAsync(
        Stream packageStream,
        Guid researchMissionId,
        CancellationToken cancellationToken = default);
}

public record ImportResult(
    bool Success,
    int Created,
    int Updated,
    int Conflicts,
    IReadOnlyList<ImportConflict> ConflictDetails,
    IReadOnlyList<string> Errors);

public record ImportConflict(
    string KeystoneId,
    string ConflictDescription,
    ResolutionRequired Resolution);
```

In v1, `KeystoneImportAdapter` is not registered; the DI container registers a `NullResearchImporter` that returns a `ImportResult(Success: false, Errors: ["Keystone import not enabled"])`. The import UI shows a feature-disabled state.

### 2.6 What Keystone's Gate Philosophy ACMP Adopts

The following Keystone disciplines are applied to the ACMP platform itself (not as runtime software — as methodology):

| Keystone discipline | ACMP application |
|---|---|
| Every req has provenance | All FR/NFR in `docs/07` and `docs/08` cite the brief or a recorded decision |
| Separate facts from proposals | `[unverified]` markers; Recommendations labelled explicitly |
| Preserve unresolved | `docs/42-open-decisions.md` is a first-class deliverable, not a backlog note |
| Immutable after approval | Settled decisions in README §A are not re-opened; updated via new ADR |
| 7 quality gates | `validate_package.py` run on this package before handoff |
| Identifier scheme | FR/NFR/CON/ASM/DEP/OQ/DEC/ADR/RISK/AC/PH/EPIC/US/KPI used throughout |

---

## 3. What NOT to Do

- **Do not embed Keystone as a service.** It is a Claude Code plugin run by humans, not a microservice to call.
- **Do not hard-depend on Keystone** in the ACMP .NET build, Docker Compose, or CI pipeline.
- **Do not duplicate Keystone's methodology in-app.** The Research module stores findings and recommendations; it does not implement the 22-stage Keystone process internally.
- **Do not assume Keystone import is available in v1.** The Research module works fully without it.
- **Do not auto-run Keystone** on behalf of the user; it is an interactive, secretary-driven process.
- **Do not generalize the import to non-Keystone package formats.** The `IResearchImporter` interface allows future importers (e.g., a Confluence export adapter), but the first implementation targets Keystone packages specifically.

---

## 4. Note: This Package Is Itself a Keystone-Style Package

The ACMP planning package (`acmp-plan/`) was authored following the Keystone methodology: it uses Keystone's identifier scheme, operating principles, quality-gate philosophy, and immutability discipline. It is not claimed to have been produced by running the Keystone plugin (that would require Claude Code + the plugin at authoring time), but it is structurally and methodologically aligned so that:

1. The execution agent can recognize and navigate it using the same expectations Keystone sets.
2. Running `validate_package.py` against this package is meaningful (G-IDS, G-DEC-STATUS, G-COMPLETE, G-TRACE gates apply).
3. ACMP's own secretarys, who may later author Keystone packages for Research topics, work with familiar artifact patterns.

---

## 5. Phase Assignment

| Capability | Phase |
|---|---|
| Research module (manual ResearchMission / Finding / Recommendation entry) | **v1** |
| `KeystonePackageRef` free-text field on ResearchMission | **v1** |
| `IResearchImporter` interface + `NullResearchImporter` stub | **v1** |
| `KeystoneImportAdapter` full implementation | **Phase 2 (optional)** |
| Import UI (upload + conflict resolution) | **Phase 2 (optional)** |

---

**Traceability:** Deliverable 28. Implements ADR-0007 (Keystone optional; Research module standalone; IResearchImporter boundary). References `docs/17-integration-architecture.md` §2.5 (IResearchImporter integration point), `docs/11-domain-model.md` (Research module entities: ResearchMission, Finding, Recommendation), `docs/41-raid.md` (ASM items re: Keystone availability). Keystone repo: https://github.com/A-H-911/keystone. This ACMP package is itself Keystone-aligned: see README §A (identifier scheme) and `docs/42-open-decisions.md`.
