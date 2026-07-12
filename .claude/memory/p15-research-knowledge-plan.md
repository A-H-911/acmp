---
name: p15-research-knowledge-plan
description: P15 Research & Knowledge — GO-gated 8-slice plan; Keystone import DEFERRED (D-05); global search + convert IN scope per operator; OQ-034/INV-002 live for search.
metadata: 
  node_type: memory
  type: project
  originSessionId: a67f04de-cda2-4d8f-aca8-f9541bc16a18
---

P15 (PH-2 remainder) = two new modules **Research** (schema `research`) + **Knowledge** (schema `knowledge`) + a **global search** surface. PH-2 = **AC-less**, traces to FRs. Prompt locked in [[keystone-package-migration]]'s follow-up-prompts.md P15 section. Plan-first, GO-gated (same discipline as P10/P11).

**Entities.** Research: `ResearchMission` (RMS-, aggregate) → child `Finding` (FND-), `Recommendation` (REC-). Knowledge: `Document` (DOC-, wiki, Draft→Published→Archived, **immutable version snapshot per save**), `Template` (TPL-, TargetType Topic/ADR/MoM/Document/Action, Active→Deprecated, versioned). Permissions: `Document.Manage` (AiO=Owner), `Template.Manage` (Sec/Chair/Admin).

**Operator scope decisions (2026-07-12):**
1. **Keystone import (FR-112) DEFERRED out of P15 → future** (D-05, no ADR — DEC-016/ADR-0007 optional-companion decision intact; committed PR #106). FR-111 keeps the package-ref *field* only; NO `IResearchImporter`. FR-114 delivered **partial** (package-ref + manually-linked artifacts; NO import-status/imported-items widget).
2. **Global search built NOW** (operator override of my index-only rec) → **AC-060 + AC-061 enter P15 scope** (were Pending). ⚠ **OQ-034 (Arabic word-breaker recall spike) + INV-002 become live**: default = SQL Server FTS (ADR-0011); if recall <80% the fallback is app-owned OpenSearch behind `ISearchIndex` — but **OpenSearch = 2nd datastore → INV-002 needs an ADR**. Run the spike at P15f; STOP + raise ADR before adding any datastore.
3. **Manual Finding/Recommendation entry** = yes (create dialogs are a no-reference composition, guardrail #14 — design frames them as imported).
4. **Convert flows IN P15** (operator override of my defer rec): Mission→Topic + Recommendation→Topic/Decision (W16), cross-module write seam (reuse P11e `ITraceabilityWriter` precedent).
5. **Versioning (FR-117)** = immutable `DocumentVersion` snapshot table + client-side markdown diff.

**Slice ladder (8):** P15a Research backend (convert-capable aggregates, CRUD, audit, topic-link, RBAC) → P15b Research UI (register+detail+manual create dialogs, `isResearch` design) → P15c Research convert (W16) → P15d Knowledge backend (Document versioned + Template) → P15e Knowledge UI (wiki list + versioned page + diff + Template mgmt, `isWiki`+template design) → P15f Search backend (SQL FTS `ISearchIndex`, OQ-034 spike FIRST, AC-061) → P15g Search UI (global grouped results, AC-060) → P15h Template wiring (picker→pre-fill at create-time, FR-120).

**FR coverage:** FR-111/113/114(partial)/115 (Research) · FR-116/117/118/119/120 (Knowledge) · AC-060/061 (search). FR-112 deferred.

**Reuse (ponytail):** module scaffold (Governance/Dependencies pattern = Domain/Application/Infrastructure/Api), register+detail+create-dialog FE (P10b Risks/P11d Invariants), `MarkdownEditor` (DV-04), `TraceabilityPanel`+`ITraceabilityWriter` (P10e/P11e), version-preserving pattern (Minutes/ADR), audit sink, RBAC policy pattern.

**Design (INV-014):** `/ACMP product context/ACMP Research & Knowledge.dc.html` (576 lines; `isResearch` missions register+detail, `isWiki` list+serif page, template). Read directly at each UI slice.

**Status:** plan GO'd by operator; **P15a in progress** on branch `feat/P15a-research-backend`. Docs-refresh + P15-defer landed as PR #106. See [[p11-adrs-invariants-plan]] for the module-slice precedent.
