# Claude Design — Input Package (Deliverable 52)

**Purpose:** everything Claude Design needs to design the ACMP UI as a coherent, usable system. It gives structure and constraints without over-prescribing visuals. Pair this with `claude-design-prompts.md` (the 10 layered prompts).

---

## 1. Product summary
ACMP is the single, auditable, bilingual (EN/AR) system of record for a government organization's **Architecture Committee**. It manages the committee's whole flow — topic intake → backlog → agenda → meeting → minutes → voting → decision → ADR → action → risk → dependency — with end-to-end **traceability**. It replaces a text-file backlog. Tone: **calm, serious, government-grade, information-dense** — not playful or marketing-led. Audience: busy senior technical leaders. Used on **desktop and tablet**, on-prem, ≤20 users.

## 2. Product goals
1. Make the weekly/bi-weekly committee loop fast and low-friction (minimal steps for common tasks).
2. Make every decision **traceable and auditable** — from the originating topic to the ADR and the follow-up action.
3. Present complex relationships **understandably**, not overwhelmingly.
4. Be fully usable in **Arabic (RTL)** and English, light and dark, and accessible (WCAG 2.2 AA).

## 3. User types & roles
- **Chairman (VP):** chairs meetings, final decision authority, override. Needs at-a-glance readiness + approvals.
- **Coordinator (committee lead — primary user):** runs the backlog, agenda, meetings, minutes, follow-up. Power user; needs efficiency and bulk actions.
- **Member (technical directors, senior engineers, iOS/Android SMEs):** own/contribute topics, vote, comment.
- **Reviewer:** reviews submissions/ADRs.
- **Submitter (stream requester):** submits topic requests; limited view.
- **Auditor:** read-only across records + audit trail.
- **Administrator:** user/role admin, templates, configuration.
- **Guest/Presenter:** time-boxed, presents a specific topic.
Per-topic capabilities overlay roles: **Owner, Assignee, Presenter**.

## 4. Primary user journeys (design these end-to-end)
1. **Submit a topic** (Submitter/Member): pick type → justification → affected streams/systems → attach docs/diagram → set urgency → submit.
2. **Triage the backlog** (Coordinator): review submissions → accept/return/reject → categorize, prioritize (drag or keyboard), tag streams, set dependencies/risks.
3. **Build & publish an agenda** (Coordinator): pick backlog topics → order (DnD) + time-box → assign presenters → publish → notify.
4. **Run a meeting & capture minutes** (Coordinator/Chairman): attendance → per-topic discussion notes → decisions → actions → review/approve minutes (versioned, immutable once published).
5. **Decide & vote** (Chairman/Members): open vote → cast (always attributed) → quorum check → record outcome + chairman approval → optionally convert to ADR.
6. **Track actions** (Owner/Coordinator): see my actions → update progress/evidence → get reminders → verify completion (verifier ≠ owner).
7. **Explore traceability** (any role): from a topic, navigate to its decision → ADR → actions → risks → diagrams, both directions; see impact of a change.

## 5. Information architecture
Top-level areas: **Home/Dashboard · Backlog · Agenda & Meetings · Decisions · ADRs & Governance (Invariants/Standards) · Actions · Risks · Dependencies · Research · Knowledge/Wiki · Diagrams · Reports · Admin.** Plus **global search** and a universal **artifact detail** pattern: every artifact (topic, meeting, decision, ADR, action, risk…) has a canonical detail page with a consistent **Relationships / Traceability panel**. Principle: *single artifact, many entry points; traceability-first navigation.*

## 6. Sitemap (summary — full inventory in `/docs/14`)
```
/                         Home / role dashboard
/backlog                  Backlog (list · table · kanban · calendar · timeline)
/topics/:id               Topic detail (+ relationships)
/topics/new               Submit topic
/agendas , /agendas/:id   Agenda list / builder
/meetings , /meetings/:id Meeting list / workspace
/meetings/:id/minutes     Minutes editor / viewer
/decisions , /decisions/:id
/adrs , /adrs/:id         ADR repository / detail
/governance/invariants    Invariants & standards
/actions , /actions/:id
/risks , /risks/:id
/dependencies , /dependencies/graph
/research , /research/:id  (Phase 3)
/wiki , /wiki/:slug        (Phase 3)
/diagrams/:id              (Phase 2)
/reports , /reports/:key
/admin/users , /admin/templates , /admin/system , /admin/notifications
/search?q=                 Global search
```
Role-gated routes are marked in `/docs/14`.

## 7. Navigation model
Primary left nav (role-filtered) + top bar (global search, language toggle EN/AR, theme toggle, notifications, profile). Breadcrumbs + deep links on detail pages. A consistent **"Related / Go to"** affordance on every artifact for traceability. **RTL mirrors the whole layout** (nav moves right, chevrons flip). Keep nav stable and shallow.

## 8. Module list
Membership · Topics/Backlog · Agenda & Meetings · Minutes · Decisions · Voting · Actions · Risks · Dependencies · ADRs · Invariants · Research · Knowledge/Wiki · Diagrams · Notifications · Reporting · Search & Traceability · Admin.

## 9. Page inventory
~50–90 pages; full table in `/docs/14`. The design-critical screens are in §10.

## 10. Screen descriptions (the ~12 most important)
1. **Home / role dashboard** — role-specific cards: for Coordinator (backlog health, agenda readiness, overdue actions, pending approvals); for Chairman (decisions awaiting approval, upcoming meeting, votes); for Member (my topics, my actions, my votes). Each card drills down.
2. **Backlog** — the workhorse. Multiple views: **list, table (dense, sortable, filterable, saved views), kanban (by status, DnD + keyboard), calendar, timeline**. Aging indicators, priority, owner avatars, stream tags, status chips, urgency. Bulk actions for Coordinator.
3. **Topic detail** — header (key `TOP-YYYY-###`, title EN/AR, type, urgency, status, owner); tabs/sections: description & justification, affected streams/systems, attachments & diagrams, discussion/comments, decisions, actions, risks, dependencies, history; and the **Relationships/Traceability panel**.
4. **Agenda builder** — two-pane: backlog topics (left) → agenda (right) with DnD ordering, per-item time-box, presenter assignment; total-time vs meeting-length indicator; publish + notify.
5. **Meeting workspace** — agenda items as the spine; attendance capture; per-item discussion notes; quick-create decisions/actions inline; live time tracking.
6. **Minutes editor** — structured MoM (attendees, per-topic notes, decisions, actions); rich text (EN/AR); versioning; review → approve; **read-only/immutable once published** with a clear locked state.
7. **Decision record** — outcome (from the canonical set), rationale, alternatives, conditions, approving authority, effective date, affected systems, links; "convert to ADR" action; supersede flow; immutable history.
8. **Voting panel** — eligible voters, options, live tally, quorum indicator, **attributed** votes (who voted what), abstentions, comments, chairman approval/override (recorded by name), close → locked.
9. **ADR detail** — MADR layout (context, drivers, options, decision, consequences), status badge, supersedes/superseded-by links, related topics/decisions/diagrams.
10. **Dependency / traceability view** — a relationship graph **and** an accessible list/breadcrumb alternative; upstream/downstream; blocked-work + cross-stream impact highlighting; filterable.
11. **Reports** — dashboard gallery with interactive filters, drill-down, export; executive/committee/stream/audit views.
12. **Admin** — users & role mapping (roles shown as **read-only from Keycloak claims**; membership/assignments editable here), templates, notification settings, system health.

## 11. Key components
Status chips (semantic colors per status model); filter bar + saved views; dense data tables; kanban cards + DnD (with keyboard alternative); calendar; timeline; **relationship panel** (list) + **relationship graph** (Phase 2); voting panel; MoM rich-text editor; diagram viewer (Tarseem SVG, Phase 2); notification center; forms with **autosave + unsaved-changes guard**; comments thread; attachment uploader; aging/urgency badges; breadcrumb; empty/loading/error/permission-denied states.

## 12. Forms & field requirements
Forms are frequent and sometimes long (topic submission, decision record, risk). Requirements: clear labels (EN/AR), inline validation with accessible error messages, required-field indication, sensible grouping/sections, **autosave + "you have unsaved changes" guard**, keyboard-complete, and review-before-submit on long forms. Field types include localized text, rich text, selects (type/urgency/status/stream), multi-select (affected systems, eligible voters), date (Gregorian), file upload, and relationship pickers.

## 13. Tables & filtering
Dense, scannable tables with column sort, multi-facet filters (status, type, stream, owner, urgency, date), **saved views**, pagination or virtualized scroll, row selection + bulk actions, and per-row quick actions. Tables must stay readable in RTL and dark mode.

## 14. Kanban & drag-and-drop
Kanban for backlog status and agenda ordering. DnD must have a **keyboard-accessible alternative** (move up/down/to-column via menu or keys) and clear focus + announcement for screen readers. Don't use DnD where precision/auditability matters more than convenience (e.g., voting) — see §22.

## 15. Timeline & calendar
Calendar view of meetings + target/scheduled dates; timeline view of a topic's lifecycle and of upcoming committee dates. Both filterable and RTL-aware.

## 16. Relationship & traceability visualization
The hardest UX problem: make traceability **understandable, not overwhelming**. Provide (a) a compact **relationships panel** on every artifact (grouped by relation type, both directions, "go to"), and (b) an optional **graph view** (Phase 2) for impact analysis with depth control and a list fallback. Keep default views shallow; expand on demand.

## 17. States (design all four for every screen)
- **Empty:** helpful first-run guidance + primary action (e.g., "No topics yet — submit the first").
- **Loading:** skeletons, not spinners, for content areas.
- **Error:** plain-language, actionable, with retry; never raw stack traces.
- **Permission-denied:** clear, non-alarming "you don't have access" with who to contact.

## 18. Arabic & RTL requirements
Full RTL mirroring of layout, navigation, icons (directional), tables, kanban, and DnD. **Consistent EN↔AR terminology** (a shared glossary — see `/docs/README §G`). Arabic typography with a proper Arabic font and correct shaping. Numerals and dates: **Gregorian**, localized formatting. Every screen must be designed and reviewed in both directions; AR is first-class, not an afterthought.

## 19. Light & dark mode
Both first-class via design tokens. Ensure status/semantic colors keep meaning and contrast in both. Sensitive, calm palette suitable for a government tool.

## 20. Accessibility (WCAG 2.2 AA)
Keyboard-complete (incl. DnD alternatives), visible focus, logical focus order, sufficient contrast, labeled controls, ARIA where needed, accessible error messaging, target sizes, no reliance on color alone for status. Screen-reader announcements for dynamic updates (vote tally, save, validation).

## 21. Responsive behavior
**Desktop-first, tablet-supported.** Layouts reflow gracefully to tablet (collapsible nav, stacked panels). Mobile phone is **not** an MVP target (`OQ` — confirm). Dense tables degrade to prioritized columns + detail drill-down on smaller widths.

## 22. Design constraints
Government, sensitive, internal. Serious and trustworthy; clarity over flair; high information density without clutter. No dark patterns. Auditability and precision beat convenience where they conflict (e.g., voting and decision records are explicit, confirmed actions — not casual DnD). Right-sized for ≤20 expert users.

## 23. Suggested design-system requirements
A small, coherent system: design tokens (color, spacing, typography incl. Arabic font, radius, elevation, motion), semantic **status color map** aligned to the canonical status models (`/docs/README §E`), a component library (buttons, inputs, selects, chips, tables, cards, tabs, dialogs, toasts, nav, breadcrumbs, kanban, calendar, timeline, relationship panel, voting panel, editor), light/dark theming, and full RTL support baked into every component. Document do/don't usage.

## 24. Realistic sample data (bilingual)
Use for mockups:
- **Topics:** `TOP-2026-014` — "Adopt Keycloak as the standard IdP across streams" / «اعتماد Keycloak كموفّر هوية موحّد عبر المسارات» — Type: ArchitectureDecision, Urgency: Urgent, Scope: Platform, Owner: T. Director (Identity), Status: Scheduled. · `TOP-2026-022` — "Standardize API pagination & error model" / «توحيد ترقيم الصفحات ونموذج الأخطاء في الواجهات» — Governance/Standardization, Normal, Accepted. · `TOP-2026-031` — "Spike: event streaming for notifications" / «دراسة: بثّ الأحداث للإشعارات» — ResearchDiscovery, Normal, Triage.
- **Meeting:** `MTG-2026-019` — Weekly Architecture Committee, 6 attendees, 4 agenda items, 90 min.
- **Decision:** `DECN-2026-008` — "Approve Keycloak adoption, conditionally" / «الموافقة على اعتماد Keycloak بشروط» — Outcome: ConditionallyApproved; vote 5 approve / 1 abstain; chairman approved.
- **Action:** `ACT-2026-040` — "Produce Keycloak migration ADR" — Owner: SME, Due in 2 weeks, In Progress 40%.
- **Risk:** `RSK-2026-006` — "Dual-running of internal auth + Keycloak during migration" — Probability: Medium, Impact: High, Status: Mitigating.
- **ADR:** `ADR-2026-003` — "Keycloak as standard IdP" — Status: Proposed.
Members: Chairman (VP), Coordinator, 3 Technical Directors, iOS SME, Android SME. Streams: Health, Permits, Payments, Identity, Notifications.

---
**Pair with:** `claude-design-prompts.md` (the 10 layered prompts). **Source of truth for IA/pages:** `/docs/14-information-architecture-sitemap.md`.
