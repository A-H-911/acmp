# ADR-0006: Tarseem as Containerized Render Sidecar (JSON Spec = Source of Truth)

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

ACMP topics, ADRs, and architectural decisions require architecture diagrams (C4, flowchart, sequence, deployment, ER, etc.) as first-class artifacts, not just file attachments. The question is whether to build a diagram engine, adopt a third-party SaaS (Lucidchart, draw.io cloud), or integrate an existing engine already in the organization's orbit.

## Decision Drivers

- Tarseem (`github.com/A-H-911/tarseem`) is a schema-driven Python diagram engine (Apache-2.0, v1.0.0, released 2026-06-17) that covers 11 diagram families including C4/architecture, flowchart, sequence, ER, state, deployment, and UML class. It is directly relevant to this platform's use cases.
- Tarseem is Arabic/RTL-first-class (HarfBuzz shaping + geometry mirror + bundled Cairo font) — matching the bilingual EN/AR requirement.
- Tarseem's agent surface (`tarseem.generate(spec) → JSON`, never raises on bad input, structured error objects with JSON Pointer paths) is designed for automated/agent-driven generation — well-suited to a background job.
- The JSON spec is deterministic and diffable; it can be stored in SQL/version-controlled and produces artifacts with an embedded spec hash for traceability.
- Building a diagram engine from scratch is out of scope and would produce an inferior result. Adopting a cloud SaaS violates the on-prem constraint and introduces external runtime dependencies.
- Tarseem is a CLI/library, not a service — it must be wrapped. Running it as a containerized sidecar with a thin internal HTTP wrapper (FastAPI) is the recommended integration model (§5.1).
- Phase-2 timing: Tarseem is not needed in Phase 1 (no diagram authoring feature in v1). The sidecar container is added in Phase 2.

## Considered Options

1. **Tarseem as containerized render sidecar (FastAPI wrapper); JSON spec = source of truth stored in SQL** — integrates the most capable available engine; spec is versionable and traceable; sidecar is isolated.
2. **Structurizr DSL + Structurizr Lite** — architecture-as-code for C4 diagrams only; does not cover 11 families; does not have the Tarseem agent surface for JSON-driven generation; would need to run alongside Tarseem anyway for other diagram types. Not adopted (Tarseem covers C4 natively via its architecture/C4 family).
3. **draw.io (diagrams.net) embedded** — editor only, not a generation engine; no JSON-spec-driven rendering; cannot be driven from a Hangfire worker. Rejected: not a render engine.
4. **Build a custom diagram engine** — years of effort, inferior result, out of scope.
5. **SaaS (Lucidchart, Miro, etc.)** — violates on-prem constraint; external runtime dependency; no EN/AR/RTL support guaranteed.

## Decision Outcome

Chosen option: "Tarseem as containerized render sidecar; JSON spec as the version-controlled source of truth", because Tarseem is the only available engine covering the required diagram families with Arabic/RTL support, a machine-readable agent surface, and a deterministic spec-hash-traceable output. The sidecar pattern keeps Tarseem's Python + Chromium dependency isolated from the .NET application container. The JSON spec stored in SQL is the canonical artifact; generated SVG/PNG/PDF/draw.io/PPTX are derived outputs attached via the relationship model.

### Consequences

- Good: no diagram engine to build; 11 diagram families available on day one of Phase 2; Arabic/RTL diagrams work without workarounds; spec-diff enables diagram history and change review; generated artifacts carry spec hash for traceability; sidecar isolation means Tarseem failures do not crash the .NET app; `tarseem doctor` provides a health-check endpoint.
- Bad / trade-off: adds a Python + Chromium container to the Docker Compose stack (image size, startup time, Chromium memory for raster); Tarseem is v1.0.0 (released 2026-06-17 [unverified for latest patch]) — adopting at v1.0 means being an early adopter; schema version lock at v1.0 means migration tooling (`tarseem migrate`) must be used if schema evolves. ACMP must implement a thin internal HTTP wrapper (not a public API) around `tarseem.generate()`.

## Validation

- Phase 2: integration test — submit a Tarseem JSON spec via the sidecar HTTP endpoint; receive SVG artifact with embedded spec hash; store artifact and verify hash matches the stored spec.
- `tarseem doctor` invoked as a Docker health check on the sidecar container.
- Render errors returned as structured JSON (`{code, path, message, hint}`) are surfaced in the ACMP UI as actionable spec-fix guidance, not opaque 500 errors.
- Arabic diagram rendering: validate RTL text flow and geometry mirror for at least one architecture/C4 and one flowchart diagram with AR labels.

## Links / Notes

- Tarseem repo: https://github.com/A-H-911/tarseem (inspected 2026-06-24; Apache-2.0; v1.0.0).
- Tarseem integration detail: §5.1 of `.context/brief-digest.md`.
- Exports: SVG (canonical), PNG, PDF, draw.io (mxGraph), PPTX — all derived from the JSON spec; stored as file attachments via `IFileStore` (MinIO).
- Diagram entities use runtime ID prefix `DGM-` and attach to Topics/ADRs/Decisions via the typed relationship model (ADR-0008).
- CLI path for Hangfire worker alternative: invoke `tarseem render <spec-file>` from a background job if the sidecar HTTP endpoint is unavailable.
- Related: ADR-0001 (Diagrams module in modular monolith), ADR-0003 (JSON spec stored in SQL Server), ADR-0008 (diagram ↔ topic/ADR relationships), ADR-0014 (Hangfire renders diagrams as background jobs).
