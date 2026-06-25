# ADR-0001: Modular Monolith as Macro-Architecture

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

ACMP is a focused, auditable, bilingual governance tool for a single Architecture Committee. Scale is on-prem, ≤20 total users, low traffic, single delivery team. A macro-architecture decision is required before module boundaries, inter-module communication rules, and deployment can be specified. The wrong choice here drives every downstream structural decision.

## Decision Drivers

- CON-001: self-contained deployment on a single on-prem VM via Docker Compose; no orchestration platform.
- Team size: one team; no organizational justification for separate deployment units.
- Operational complexity: the committee cannot carry the burden of service-mesh, distributed tracing across services, or independent release pipelines for ≤20 users.
- Strong domain boundaries are desirable (audit, voting, decisions, traceability are distinct contexts), but they do not require network isolation at this scale.
- The modular-monolith + Clean Architecture pattern is well-supported in .NET and Microsoft-endorsed. [See: https://learn.microsoft.com/en-us/shows/on-dotnet/on-dotnet-live-clean-architecture-vertical-slices-and-modular-monoliths-oh-my ; https://www.milanjovanovic.tech/blog/vertical-slice-architecture-dotnet]

## Considered Options

1. **Modular monolith** — single deployable; bounded contexts enforced via module folders + in-process contracts; no cross-module DB access.
2. **Microservices** — separate deployable per module (e.g., Meetings, Decisions, Notifications each as a service).
3. **Traditional layered monolith** — single deployable with no explicit module boundaries; shared data access throughout.

## Decision Outcome

Chosen option: "Modular monolith", because the scale (≤20 users, single team, on-prem VM) makes microservices operational overhead unjustifiable, and a layered monolith provides no enforced isolation and would couple unrelated domains (audit, voting, search) in ways that invite future pain. Modules map one-to-one with bounded contexts; they communicate only via in-process public contracts (interfaces / MediatR notifications) and never read each other's tables directly.

### Consequences

- Good: minimal operational overhead; single deployment unit (Docker Compose); one EF Core migration set; full-stack debugging without distributed tracing between services; well-understood pattern in .NET; can extract a module to a service later if usage changes.
- Bad / trade-off: no network-level isolation between modules (a bug in one module can crash all); if scale increases dramatically (>20×), decomposition requires non-trivial refactoring. Discipline is required to enforce module boundaries — violations are not caught by the compiler without a custom Roslyn analyzer or ArchUnit-equivalent.

## Validation

- Code review gate: no `using` across module namespaces except via public contract interfaces.
- Architecture fitness function (optional CI check): dependency rule violations fail the build. See `docs/32-devsecops-plan.md`.

## Links / Notes

- Microservices explicitly rejected for v1 — document the rejection so it is not re-opened without evidence.
- Module list: `Membership · Topics · Meetings · Decisions · Actions · Risks · Dependencies · Governance · Research · Knowledge · Diagrams · Notifications · Reporting · Search&Traceability · Audit&Records · Platform` (canonical in README §B).
- If team size or concurrent-user count grows beyond single-team/on-prem scope, revisit this ADR before adding inter-service communication.
- Related: ADR-0002 (backend stack), ADR-0013 (deployment), ADR-0003 (single datastore).
