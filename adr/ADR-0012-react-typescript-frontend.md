# ADR-0012: React 18 + TypeScript + Vite Frontend

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

> **Amended by [ADR-0015](ADR-0015-react-19-amends-0012.md) (2026-06-25):** the React major version is **19**, not 18. Every other decision in this ADR (TypeScript, Vite, `react-i18next` EN/AR, RTL via CSS logical properties + `dir`, light/dark tokens, `@dnd-kit`) remains in force.

## Context and Problem Statement

ACMP requires a web frontend that is bilingual (EN/AR), supports RTL layout, is accessible, and provides rich interactions (drag-and-drop backlog/agenda prioritization, kanban views, real-time notification updates). The technology stack must be compatible with the .NET/REST backend (ADR-0002), supportable by the existing team, and deployable as a static asset bundle served by the ACMP server or a separate nginx container.

## Decision Drivers

- The organization's existing frontend work is primarily in native mobile (iOS/Android) and .NET backends; the browser tech choice is free of legacy constraint — pick the most productive stack for this specific tool.
- EN/AR bilingual support with full RTL is a first-class requirement, not an afterthought. The chosen tooling must support RTL without hacks.
- Drag-and-drop is required for backlog prioritization, agenda ordering, and kanban board — must be keyboard-accessible for a11y (WCAG 2.2 AA).
- Light/dark mode is required.
- `react-i18next` is the most widely-used React i18n library; RTL support via CSS logical properties (`margin-inline-start`, `padding-inline-end`) and `dir` attribute on the root element is framework-agnostic and the correct approach.
- `@dnd-kit` provides accessible, framework-integrated drag-and-drop with keyboard navigation — preferred over `react-beautiful-dnd` (no longer actively maintained) and HTML5 drag API (poor accessibility).
- Vite provides fast development builds and HMR without webpack configuration overhead; it is the React ecosystem's current standard toolchain for SPAs.
- React 18 is stable with concurrent features; Next.js (SSR) adds complexity without benefit for this fully authenticated internal tool.

## Considered Options

1. **React 18 + TypeScript + Vite; react-i18next (EN/AR); RTL via logical CSS + `dir`; light/dark design tokens; `@dnd-kit`** — productive, well-supported, correct i18n/RTL approach.
2. **Angular** — heavier framework; team not familiar with it; no advantage over React for this use case.
3. **Vue 3 + Vite** — viable but team preference and ecosystem alignment favour React.
4. **Next.js (SSR/RSC)** — adds server-side rendering complexity for an authenticated SPA with no SEO requirement; overkill; harder to deploy as a static bundle.
5. **react-beautiful-dnd** — no longer actively maintained; replaced by `@dnd-kit` as the community standard.

## Decision Outcome

Chosen option: "React 18 + TypeScript + Vite + react-i18next + RTL logical CSS + `@dnd-kit`", because it is the most productive and well-supported stack for the requirements, the RTL approach (CSS logical properties + `dir` attribute) is framework-agnostic and correct for a bilingual app, and `@dnd-kit` provides the accessible DnD needed for keyboard users without the maintenance risk of unmaintained libraries.

### Consequences

- Good: fast development iteration with Vite HMR; strong TypeScript ecosystem; `react-i18next` with `dir="rtl"` on the root element and CSS logical properties handles EN/AR layout flip without per-component hacks; `@dnd-kit` keyboard-accessibility satisfies WCAG 2.2 AA DnD requirements; React 18 concurrent features allow prioritised rendering (notifications update without blocking the UI); light/dark tokens via CSS custom properties are trivial to implement.
- Bad / trade-off: bundle size must be monitored (React + Vite is smaller than Angular but larger than a vanilla approach — acceptable for an internal tool); TypeScript type discipline requires upfront investment in shared type definitions (API response DTOs); RTL testing requires explicit test runs with `dir="rtl"` — easy to miss RTL layout bugs without a dedicated RTL test pass.

## Validation

- RTL rendering test: switch language to Arabic, verify all margin/padding/text-align/flex-direction properties use CSS logical equivalents; verify no hard-coded `left`/`right` CSS values remain (linted via Stylelint rule).
- DnD accessibility: keyboard navigation for backlog reorder, agenda order, and kanban card move — all operable without a mouse per WCAG 2.2 AA.
- i18n completeness: CI gate — all translation keys present in both `en.json` and `ar.json`; missing keys fail the build.
- Light/dark: visual regression tests (Playwright) run in both modes on key screens.
- Performance: Lighthouse score ≥ 85 (performance), ≥ 90 (accessibility) on the main dashboard and topic list pages.

## Links / Notes

- `@dnd-kit` docs: https://dndkit.com — keyboard accessibility is first-class (roving tabIndex, ARIA live regions for announcements).
- `react-i18next` docs: https://react.i18next.com — supports namespace separation, lazy loading of translations.
- RTL CSS logical properties: https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_logical_properties_and_values — replace `margin-left` with `margin-inline-start` etc.
- Design tokens (light/dark, spacing, typography, colour) defined in `design-handoff/`; implemented as CSS custom properties; no hard-coded hex values in component CSS.
- Static build artifact (`dist/`) served by nginx or the ACMP ASP.NET Core app in production; no Node.js runtime in the production container.
- Related: ADR-0001 (frontend is a separate module/project in the monorepo), ADR-0002 (REST API consumed by React frontend), ADR-0004 (Keycloak OIDC PKCE flow initiated from the SPA).
