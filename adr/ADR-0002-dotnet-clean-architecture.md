# ADR-0002: .NET 8 + ASP.NET Core + Clean Architecture + Vertical Slice + EF Core + REST

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

Given the modular-monolith decision (ADR-0001), the backend technology stack and intra-module structural pattern must be chosen. The platform handles sensitive governance data (votes, decisions, audit) and must be maintainable long-term by a single .NET team within an organization whose broader tech estate is .NET.

## Decision Drivers

- The organization's existing tech estate is .NET; team expertise and hiring are aligned.
- .NET 8 is the current LTS release (supported until November 2026 → November 2028 for LTS successor); organizational risk of a non-LTS release is unacceptable for a governance platform.
- Clean Architecture enforces the dependency rule (domain → application → infrastructure) so domain logic is not polluted by EF Core, HTTP, or Keycloak concerns — critical for testability of voting, decision, and audit logic.
- Vertical-slice handlers (MediatR) keep each use-case's request/response/handler/validator co-located, reducing cognitive load and minimizing risk of cross-use-case coupling inside a module.
- EF Core is the standard ORM for .NET/SQL Server; the team already knows it; migrations are first-class.
- REST is sufficient for the use cases (no real-time streaming in v1 beyond SignalR for notification push); GraphQL adds complexity without benefit at this scale.

## Considered Options

1. **.NET 8 + ASP.NET Core + Clean Architecture per module + vertical-slice (MediatR) + EF Core + REST** — current LTS, familiar, well-patterned.
2. **.NET 8 + Minimal APIs only (no Clean Architecture / MediatR)** — simpler scaffolding but less structure; module boundaries harder to enforce; domain logic risks mixing with infrastructure.
3. **Node.js / TypeScript backend** — diverges from org stack; no benefit for this use case; excludes EF Core migration tooling.
4. **.NET 9 (non-LTS)** — shorter support window; not appropriate for a governance platform requiring multi-year stability.

## Decision Outcome

Chosen option: ".NET 8 + ASP.NET Core + Clean Architecture per module + vertical-slice (MediatR) + EF Core + REST", because it aligns with org expertise, is LTS-stable, and provides layered testability (domain logic isolated from EF Core and HTTP) while keeping each use case self-contained. MediatR pipeline behaviors handle cross-cutting concerns (validation, logging, authorization, outbox) uniformly without repeating them in controllers.

### Consequences

- Good: domain and application layers are fully unit-testable without a running database; EF Core migrations are automated and reviewable; MediatR pipeline behaviors enforce consistent cross-cutting logic; REST is universally understood by API consumers; LTS lifecycle gives multi-year support stability.
- Bad / trade-off: MediatR adds an indirection layer that beginners find opaque; Clean Architecture enforces strict layer boundaries which produce more files per feature than a simple CRUD approach; EF Core's change tracker can be a footgun if developers mix tracked/untracked entities carelessly — requires code-review attention.

## Validation

- Each module must have a `Domain`, `Application`, `Infrastructure`, and `Presentation` (or `Api`) layer. Domain projects must not reference EF Core or ASP.NET packages.
- CI: build a dependency-direction check (no upward references). Test coverage gate on domain and application layers.
- See `docs/31-testing-strategy.md` for unit/integration/e2e test layers.

## Links / Notes

- Vertical-slice reference: https://www.milanjovanovic.tech/blog/vertical-slice-architecture-dotnet
- Modular monolith + Clean Architecture guidance: https://learn.microsoft.com/en-us/shows/on-dotnet/on-dotnet-live-clean-architecture-vertical-slices-and-modular-monoliths-oh-my
- Background jobs (Hangfire), observability (Serilog + OpenTelemetry + Seq), and object storage (MinIO) are cross-cutting infrastructure concerns configured in the Platform module — see ADR-0014.
- REST versioning strategy: URI-path versioning (`/api/v1/...`); breaking changes require a new version path.
- Related: ADR-0001 (modular monolith), ADR-0003 (SQL Server + EF Core), ADR-0014 (jobs + observability).
