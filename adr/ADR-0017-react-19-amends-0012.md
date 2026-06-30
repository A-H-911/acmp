# ADR-0017: Adopt React 19 (amends ADR-0012)

- Status: Accepted
- Date: 2026-06-25
- Deciders: Architecture Committee execution (secretary to ratify)
- Amends: ADR-0012 (React 18 + TypeScript + Vite frontend)
- Note: Renumbered from ADR-0015 on 2026-06-30 to resolve a number collision with ADR-0015 (Self-Hosted Keycloak). The renumber is administrative only; the decision and its 2026-06-25 date are unchanged.

## Context and Problem Statement

ADR-0012 specified **React 18**. The P1 scaffold installed **React 19** (the current stable major at scaffold time) without recording the deviation — a silent drift surfaced during P3 (frontend foundation). Code and a settled ADR disagree; CLAUDE.md requires we fix one or raise an ADR rather than let them drift. This ADR records the deviation and decides which way to resolve it.

## Decision Drivers

- React 19 is the current stable release; it is the default `create-vite` React template and what the toolchain (`@types/react@19`, `@vitejs/plugin-react@6`) is built against.
- React 19 is a near-superset of 18 for ACMP's usage; nothing in ADR-0012's rationale (RTL via logical CSS, `@dnd-kit`, `react-i18next`, Vite, light/dark tokens) depends on 18-specific behavior.
- React 19 simplifies a few patterns ACMP uses: `ref` as a plain prop (no `forwardRef`), and `use()` for context/promises — minor ergonomic wins, no architectural change.
- Downgrading to 18 now would mean pinning older `@types/react` and plugin versions and re-testing the working P1/P3 stack — churn with no benefit (guardrail 12: right-size, no busywork).
- Error boundaries still require a class component in React 19 (unchanged from 18) — already implemented as such.

## Considered Options

1. **Adopt React 19, amend ADR-0012** — keep the installed, working, fully-supported stack; record the version change. (chosen)
2. **Downgrade to React 18 to match ADR-0012 as written** — version churn, older type/plugin pins, re-test, no functional gain.

## Decision Outcome

Chosen option: **adopt React 19**. ADR-0012 remains in force in every other respect (TypeScript, Vite, `react-i18next` EN/AR, RTL via CSS logical properties + `dir`, light/dark tokens, `@dnd-kit` accessible DnD). Only the React major version is amended, 18 → 19.

### Consequences

- Good: stays on the current supported major; no downgrade churn; aligns with the installed `@types/react@19` / `@vitejs/plugin-react@6` toolchain; `forwardRef` no longer needed for ref-forwarding components.
- Trade-off: React 19 is newer, so a few third-party libraries may lag on peer-dep ranges — none encountered in P3 (`@tanstack/react-query`, `react-oidc-context`, `@dnd-kit`, `react-i18next`, `react-router-dom` all install clean against 19).
- Process: this is the corrective record for a P1 silent drift; future stack changes must raise the ADR *before* merging, not after.

## Validation

- P3 builds clean (`tsc -b && vite build`) and the test suite (Vitest + RTL, 21 tests) passes on React 19.
- No `forwardRef` or 18-only API in the codebase; error boundary remains a class component.

## Links / Notes

- Amends ADR-0012 (`adr/ADR-0012-react-typescript-frontend.md`).
- React 19 upgrade guide: https://react.dev/blog/2024/12/05/react-19
- Recorded in the P3 progress-log entry (`docs/_progress/progress-log.md`).
