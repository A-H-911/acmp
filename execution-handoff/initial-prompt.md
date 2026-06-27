# Initial Claude Code Prompt — ACMP

> Copy-paste this as the **first message** to the Claude Code agent that will build ACMP. It assumes the planning package (this whole `acmp-plan/` folder) has been placed at the repository root under `/docs` (and `/adr`, `/execution-handoff`, `/design-handoff`, `/CLAUDE.md`).

---

You are the execution agent for **ACMP — the Architecture Committee Management Platform**: a focused, auditable, bilingual (EN/AR) web platform that is the single system of record for a government organization's Architecture Committee (topic intake → backlog → agenda → meeting → minutes → voting → decision → ADR → action → risk → dependency, with full traceability). It is **architecture governance, not generic project management**, for a **single committee, on-prem, low-traffic, ≤20 users**.

**Before you write any code, do this:**
1. Read `/CLAUDE.md` (your standing brief), then `/docs/README.md` (the canonical reference: roles, modules, entities, IDs, status models, and the settled decisions in §A + the "Resolved 2026-06-24" callout).
2. Read `/execution-handoff/agent-guardrails.md` and treat every guardrail as binding.
3. Read `/execution-handoff/claude-code-execution-package.md` (what to build, mandatory requirements, what's resolved vs open, how to track progress).
4. Skim, for the current phase: `/docs/15-architecture.md`, `/docs/11-domain-model.md`, `/docs/13-workflows.md`, `/docs/10-permission-role-matrix.md`, `/docs/34-repository-structure.md`, and the relevant `/adr/*`.
5. **Do not generate or modify code until you have inspected these artifacts.** Then summarize back: the stack, the module list, the build order, and the guardrails — so we confirm alignment.

**Hard constraints (full list in agent-guardrails.md):** modular monolith; .NET 8 / ASP.NET Core / EF Core / SQL Server; React 18 + TS + Vite; **self-hosted Keycloak (ADR-0015)** OIDC with roles from claims; self-contained (no org Hangfire/ELK/Seq/notification platform) — app-owned Hangfire on ACMP's SQL, self-hosted Seq + MinIO, **self-hosted Keycloak (ACMP-owned realm) + SQL Server bundled → zero external runtime services**; in-app notifications in v1 (Webex Phase 2 — the only external dependency; no email v1); Tarseem Phase 2 (JSON spec = source of truth); Keystone optional; audit immutability + always-attributed voting; EN/AR + full RTL; no overengineering. **Any change to a settled decision requires a new ADR.**

**Your first deliverable — PH-0 + repository foundation (do not skip PH-0):**
1. **PH-0 validation:** Produce a short `/docs/_progress/ph0-validation.md` that (a) confirms your understanding of the domain, modules, roles, and the core loop; (b) lists the `OQ-###` items from `/docs/42-open-decisions.md` that block PH-0/PH-1 and applies the recommended default for each (note where you need human confirmation); (c) verifies the local toolchain (.NET 8 SDK, Node, Docker) and that you can stand up the bundled, self-hosted Keycloak (ACMP-owned realm — ADR-0015) + SQL Server + Seq + MinIO via Compose (zero external runtime services).
2. **Scaffold the repository** exactly per `/docs/34-repository-structure.md`: the solution, the modular-monolith host, one vertical-slice module end-to-end as the reference (recommend **Membership** or **Topics**), the React app shell with i18n (EN/AR) + RTL + light/dark, the `docker-compose.yml` (api, web, keycloak, sqlserver, seq, minio — with the Keycloak realm bootstrap under `deploy/keycloak/` per ADR-0015), EF Core migration setup, the MediatR pipeline behaviors (validation, authorization, audit, logging), `IFileStore`/`INotificationChannel`/`ICurrentUser` abstractions, health checks, structured logging to Seq, and the CI workflow skeleton per `/docs/32-devsecops-plan.md`.
3. Establish the **progress log** (`/docs/_progress/progress-log.md`) and the **acceptance audit** (`/docs/_progress/acceptance-audit.md`, every `AC-###` → verdict) — keep them updated as you build (Keystone gate G-PROGRESS).

**Then proceed phase-by-phase** using `/execution-handoff/phase-prompts.md`, starting with the backend + frontend foundation and Identity & Permissions, then Topics & Backlog (the core loop first).

**Rules of engagement while building:**
- One module/feature at a time; small reviewable commits (conventional commits).
- Every feature ships with tests and meets its `AC-###`. No feature is "done" otherwise (`/docs/44-definition-of-done.md`).
- Enforce authorization and emit audit events from day one — never "add later".
- No hardcoded user-facing strings; EN + AR; verify RTL on every screen.
- Record any new architecture decision as a MADR file in `/adr`. Record inferred facts as `ASM-###`; never silently resolve a flagged `OQ-###`.
- If a task seems to require breaking a guardrail, **stop and ask** — don't work around it.

Start by reading the artifacts in step 1–4 and reporting your understanding and PH-0 plan. Do not scaffold until that summary is confirmed.
