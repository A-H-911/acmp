# ADR-0007: Keystone as Optional Companion (Not Embedded, Not a Hard Dependency)

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

Keystone (`github.com/A-H-911/keystone`) is a Claude Code plugin that turns a project description into an execution-ready planning and handoff package (research missions, findings, recommendations, requirements, risks, decisions, traceability matrix). The ACMP domain includes a Research module for `ResearchDiscovery` topics. The question is how tightly Keystone integrates with ACMP: embedded service, hard dependency, or optional enhancement.

## Decision Drivers

- Keystone is a Claude Code agent skill / plugin, not a running service — it has no HTTP API to call at runtime. It is a human-in-the-loop workflow tool (22 stages, approval gates), not a background process.
- The Research module must be fully functional for teams that do not use Keystone (manual entry of Research Missions, Findings, Recommendations is the baseline).
- Keystone is MIT-licensed (§5.2); its identifier scheme (`FR-/NFR-/CON-/ASM-/DEP-/OQ-/DEC-/ADR-/RISK-/HYP-/AC-/PH-`) and gate philosophy are worth adopting at the platform level regardless of whether any given research topic uses Keystone.
- Keystone's validator (`validate_package.py`) runs at authoring time (Python 3.9+, stdlib-only) — not a runtime dependency of the ACMP server.
- Embedding Keystone as a service would require wrapping a non-service tool in an API, maintaining that wrapper, and creating a runtime dependency on Claude Code/Anthropic — all unjustified for a governance platform.

## Considered Options

1. **Keystone OPTIONAL — companion Claude Code workflow; Research module standalone; import structured outputs; adopt ID scheme** — decoupled, addable by teams that choose it, no runtime dependency.
2. **Keystone embedded as a sidecar service** — wraps a human-in-the-loop tool in an API; creates an Anthropic/Claude Code runtime dependency for ACMP; technically inappropriate; rejected.
3. **Keystone as a hard dependency (Research module requires it)** — blocks Research module launch for teams not using Keystone; inappropriate given Keystone is a facilitated workflow, not a data service; rejected.
4. **Ignore Keystone entirely** — loses the value of its ID scheme and structured output format, which the planning package itself adopts. Rejected.

## Decision Outcome

Chosen option: "Keystone OPTIONAL — Research module fully standalone; import Keystone outputs; adopt ID scheme", because Keystone is a facilitated authoring workflow, not a runtime service, and the Research module must be independently usable. When a team runs a Keystone package for a `ResearchDiscovery` topic, ACMP imports the structured manifest (requirements, decisions, risks, acceptance criteria, traceability matrix) as first-class domain artifacts (Research Mission → Finding → Recommendation entities) and stores a reference/link to the Keystone package. ACMP adopts Keystone's identifier scheme and quality-gate philosophy platform-wide.

### Consequences

- Good: Research module launches on day one without any Keystone dependency; Keystone becomes a value-add for teams doing rigorous research discovery; imported artifacts are first-class (queryable, traceable, linked to topics); identifier scheme consistency across the platform and any Keystone packages produced; no Anthropic/Claude Code runtime coupling.
- Bad / trade-off: import mapping (Keystone output schema → ACMP domain entities) must be defined and maintained; if Keystone's output schema changes between versions, the import adapter needs updating. Teams using Keystone plus ACMP need a clear documented workflow (run Keystone → export manifest → import into ACMP topic) — this is a process gap, not a technical one.

## Validation

- Research module acceptance: create a Research Mission, add Findings and Recommendations, link to a `ResearchDiscovery` topic — all without Keystone. Feature complete standalone.
- Keystone import acceptance (Phase 2 or later): given a valid Keystone package manifest, ACMP parses and imports it; all FR/NFR/RISK/DEC/AC items appear as structured artifacts on the Research Mission; the reference link to the Keystone package is stored; Keystone IDs are preserved as `sourceId` on each imported artifact.
- Quality gate alignment: ACMP's own planning IDs (`FR-`, `NFR-`, `CON-`, `ASM-`, `ADR-`, `RISK-`, `HYP-`, `AC-`, `PH-`) match the scheme used in this planning package and in Keystone.

## Links / Notes

- Keystone repo: https://github.com/A-H-911/keystone (inspected 2026-06-24; MIT; v1.0).
- Keystone integration detail: §5.2 of `.context/brief-digest.md`.
- This ACMP planning package is itself a Keystone-style package — produced using the same methodology, identifier scheme, and gate philosophy.
- Keystone's 7 mechanical quality gates (G-IDS, G-DEC-STATUS, G-REQ-SRC, G-COMPLETE, G-TRACE, G-SET, G-PROGRESS) inform ACMP's own acceptance-criteria validation in `docs/40-acceptance-criteria.md`.
- Related: ADR-0001 (Research module as bounded context in modular monolith), ADR-0008 (imported Keystone artifacts participate in the traceability graph).
