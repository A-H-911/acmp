# Claude Code Execution Package — ACMP (Deliverable 54)

**Purpose:** the single brief that tells the execution agent what to build, why, what is fixed, what is open, and how to work. It points into the planning package rather than duplicating it.

## 1. What to build (and why)
Build **ACMP**, the single system of record for the Architecture Committee. The committee today runs national-scale architecture governance on a **text-file backlog** with no traceability, no audit, weak follow-up, and lost decision rationale (see `/docs/02`, `/docs/03`). ACMP digitizes the committee's existing process — intake → backlog → agenda → meeting → minutes → voting → decision → ADR → action → risk → dependency — and makes it searchable, auditable, and fully traceable. **Digitize the process; don't reinvent it. Architecture governance, not project management.**

Primary user: the **Secretary** (you are building for them) plus the **Chairman**, **Members**, **SMEs**, **Submitters**, **Auditors**. Scale: on-prem, ≤20 users, low traffic — right-size everything.

## 2. Mandatory requirements (must hold)
- The **core governance loop** works end to end in PH-1: submit topic → triage → backlog → agenda → meeting → minutes → decision → action, with in-app notifications, audit history, and basic dashboards, in **EN + AR (RTL)** and light/dark.
- **Authorization** on every operation (role policy + ABAC), least privilege, segregation of duties.
- **Audit immutability:** every state change audited; votes/decisions/ADRs/published-minutes immutable (superseded, not edited); hash-chain votes/decisions/audit.
- **Always-attributed voting** with eligible voters, quorum, abstentions, and recorded chairman approval/override.
- **Self-contained** deployment (CON-001): app-owned Hangfire on ACMP SQL, self-hosted Seq + MinIO, in-app notifications; **self-hosted Keycloak (ADR-0015)** OIDC for identity (ACMP-owned realm; roles via claims); SQL Server only (also bundled) → zero external runtime services (Webex Phase 2 is the only external dependency).
- **Traceability** wired as you build (typed relationships; live traceability matrix).
- Non-functionals per `/docs/08` (right-sized: ≤20 users, 24×7/99.9% via simple redundancy + nightly backups, WCAG 2.2 AA, OWASP ASVS 5.0 L2).

## 3. What is resolved vs open
- **Resolved decisions:** `/docs/README.md §A` ("Resolved 2026-06-24") and `/docs/42-open-decisions.md §A` (26 items) — treat as fixed; changing one needs an ADR.
- **Open decisions:** `/docs/42-open-decisions.md §B` (37 `OQ-###`). Each has a **recommended default** — use it to stay unblocked, but never silently "decide" a flagged item; surface PH-0 blockers (CI system, registry, package mirror, TLS policy, MFA/session, Arabic FTS spike) for human confirmation.
- **Assumptions:** `/docs/41-raid.md` (`ASM-###`). Add new ones as you infer; never mask them.

## 4. Files & directories to create
Scaffold exactly per **`/docs/34-repository-structure.md`**: solution + modular-monolith host (`Acmp.Api`), `src/Modules/<Module>` (Domain/Application/Infrastructure per module), `src/BuildingBlocks` (shared kernel), `src/Acmp.Web` (React), `/tests/...`, `/deploy` (Dockerfiles, `docker-compose.yml`, env samples), `/scripts`, `/.github/workflows`, `/adr`, `/docs`. Architecture tests enforce module boundaries.

## 5. Architecture rules that must not be violated
See `/execution-handoff/agent-guardrails.md` and `/adr`. Core: modular monolith; module isolation (no cross-module DB; in-process contracts/MediatR only); Clean Architecture per module; approved stack only; self-contained (CON-001); no distributed infra without an ADR; no overengineering.

## 6. Coding standards
`/docs/34-repository-structure.md` + `/docs/31-testing-strategy.md` + `/CLAUDE.md`. C#: nullable, analyzers + `dotnet format`, async-all-the-way, FluentValidation in MediatR validation behavior, EF Core forward-only migrations, Problem Details error model, no logic in controllers. React/TS: strict, feature folders, TanStack Query, all strings in i18n, logical CSS for RTL, axe-clean. Conventional commits; small PRs.

## 7. Tests to implement
Per `/docs/31-testing-strategy.md`: unit/domain, application/handler, API integration (WebApplicationFactory), DB integration (Testcontainers), frontend component (Vitest + RTL), E2E (Playwright), **permission-matrix**, **workflow**, **audit-trail**, **voting-integrity**, **decision-history/immutability**, file-upload, localization, **Arabic/RTL**, accessibility (axe), security (ZAP baseline), migration, backup/restore, and mocked contract tests for Webex/Tarseem/Keystone. Every feature → tests + its `AC-###`. Architecture tests enforce boundaries.

## 8. Documentation to maintain
Per `/docs/35-documentation-plan.md`: keep `/adr` (MADR) current; OpenAPI/Swagger generated; per-module READMEs; runbooks; the progress log + acceptance audit; diagram specs (Tarseem JSON, Phase 2) versioned. Docs-as-code; no orphaned/stale docs (CI link-check + i18n-parity).

## 9. Acceptance criteria that must pass
`/docs/40-acceptance-criteria.md` (`AC-###`, Given/When/Then) + each phase's exit criteria in `/docs/36-roadmap.md`. Maintain the **acceptance audit** (every `AC-###` → Met/Partial/Not-met/Pending — Keystone gate G-PROGRESS). A requirement isn't done until it traces to ≥1 decision, ≥1 work item, and ≥1 test (gate G-TRACE).

## 10. How to track progress
Keep `/docs/_progress/progress-log.md` (per-phase, dated) and `/docs/_progress/acceptance-audit.md`. Use the implementation backlog order in `/docs/37-implementation-backlog.md`. Update both as you complete work items.

## 11. How to record ADRs & manage deviations
Any new architecture decision, or a change to a settled one, is a **MADR** file in `/adr` (lifecycle `Draft→Proposed→Approved→Superseded/Deprecated`; superseded-not-edited). A deviation from the plan is only legitimate **with an ADR** that states the driver, the option chosen, and the consequence; otherwise, conform to the plan. Surface deviations — never hide them.

## 12. Arabic/RTL validation
No hardcoded strings (CI i18n-parity check EN↔AR). Every screen tested in RTL (logical CSS + `dir`). Gregorian dates, localized formatting. Acceptance: `/docs/40` AC for EN/AR + RTL; design validation prompt in `/design-handoff/claude-design-prompts.md` (#9).

## 13. Permissions & auditability validation
Permission-matrix tests cover `/docs/10` (role × action, ABAC scope, SoD). Audit-trail tests assert every state change emits an `AuditEvent` and that votes/decisions/ADRs/minutes cannot be mutated (only superseded). Hash-chain integrity test for votes/decisions/audit (`/docs/26`).

## 14. Tarseem & Keystone integration (later phases)
- **Tarseem (Phase 2):** integrate as a containerized render sidecar via `IDiagramRenderer`; store the **JSON spec as the source of truth** (with spec hash) and artifacts in MinIO; never build a diagram engine. See `/docs/19` + ADR-0006.
- **Keystone (optional):** the Research module works standalone; if used, import structured artifacts via `IResearchImporter` and map to ResearchMission/Finding/Recommendation; adopt the ID scheme. Never embed or hard-depend. See `/docs/20` + ADR-0007.

## 15. Avoid unnecessary complexity
Right-size for ≤20 users on-prem. No microservices, brokers, K8s, second DB, or speculative abstraction. Prefer explicit domain concepts. If you're reaching for an enterprise pattern, justify it against the actual requirement or don't add it. When in doubt, build the simplest thing that satisfies the acceptance criteria — and write the test first.

---
**Build order:** PH-0 validation + scaffold → backend/frontend foundation → Identity & Permissions → Topics & Backlog → Agenda & Meetings → Minutes & Decisions → Actions → (PH-2) Voting UI, Risks, Dependencies, ADRs, Invariants, Templates, Reporting, Tarseem, Webex → (PH-3) Keystone, Research, Wiki, advanced traceability, AI extraction, email. Phase prompts: `/execution-handoff/phase-prompts.md`.
