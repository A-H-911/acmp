# 14 — Information Architecture, Sitemap & Page Inventory

**Purpose:** Define how ACMP is organized, how users navigate it, and enumerate every page/screen with its route, components, roles, and required states. Feeds Deliverables 17, 18, and 19. Primary consumer: Claude Design handoff (`design-handoff/`).

---

## 1. Information Architecture

### 1.1 Product Organization

ACMP is structured around **the committee governance lifecycle**, not around data entities. Navigation follows the natural flow of committee work — from intake through backlog, meeting, decision, and follow-up — rather than generic CRUD admin patterns.

Top-level areas and their grouping rationale:

| Area | Modules Served | Grouping Rationale |
|---|---|---|
| **Home / Dashboard** | Reporting, cross-cutting | Role-tailored entry point; single view of what needs action today |
| **Backlog** | Topics | The working inventory of all committee topics — primary daily destination for Secretary and Chairman |
| **Agenda & Meetings** | Meetings | Preparation for and record of every committee session; includes the live-meeting mode and MoM |
| **Decisions & ADRs** | Decisions, Governance (ADRs) | Issued governance outcomes, their rationale, voting record, and formalized decision records; grouped because an ADR is the long-form artifact of a decision |
| **Governance** | Governance (Architecture Invariants) | Standing rules the org's systems must never violate; separate from individual topic decisions |
| **Actions** | Actions | Cross-topic, cross-meeting follow-up items; treated as a first-class module because they cut across all topics |
| **Risks** | Risks | Risk register surfaced at both topic and committee level |
| **Dependencies** | Dependencies | Dependency graph — cross-stream and cross-entity blocking relationships |
| **Research** | Research, Knowledge | Keystone-imported research missions + wiki/knowledge base + templates; grouped as "knowledge assets" |
| **Diagrams** | Diagrams | Tarseem-rendered architecture diagrams stored as versioned JSON specs |
| **Reports** | Reporting | Dashboards and tabular reports beyond the role dashboards on Home |
| **Admin** | Membership, Platform | User management, role assignment, system configuration, notification settings, Hangfire job monitor |

### 1.2 Navigation Principles

**Single artifact, many entry points.** Every ACMP artifact (topic, decision, action, risk, ADR, etc.) has exactly one canonical detail page at a stable, deep-linkable route. The same artifact is reachable from multiple contexts: from the backlog, from a meeting's agenda, from a decision's traceability panel, from a notification deep link, and from global search results. Route design enforces this: `/topics/TOP-2026-042` is always valid regardless of which path led there.

**Traceability-first navigation.** Every detail page carries a Relationships / Traceability panel listing all typed upstream and downstream artifact links. Each link in that panel is a deep link to the related artifact's canonical page. This means a user can navigate the governance chain — from a wiki page → research mission → topic → decision → ADR → invariant — without ever returning to a list view. This is the primary navigation idiom for governance traceability.

**Role-filtered nav.** The primary navigation sidebar is rendered server-side per role; unauthorized modules do not appear. Unauthorized actions within an authorized page are hidden (FR-024). The user is never shown a permission-denied wall for a nav item — the item simply does not exist for their role.

**Global search.** A persistent search bar in the top chrome is accessible from every page. It returns grouped results across all artifact types (topics, decisions, ADRs, MoM, wiki, diagrams). It is the universal fallback entry point.

**Artifact-detail pattern.** Every module's main-list page leads to a detail page. The detail page layout is consistent across modules:
1. Header: canonical ID + title + status chip + urgency badge (where applicable)
2. Metadata section: structured fields (creator, dates, stream, type, etc.)
3. Content section: Markdown-rendered body or structured data
4. Actions panel: context-sensitive actions the current user can take
5. Relationships / Traceability panel: upstream + downstream typed links (expandable, with "go to related" deep links)
6. History / Audit trail section: immutable timeline of state transitions

---

## 2. Navigation Model

### 2.1 Primary Navigation (Role-Based)

The primary navigation is a collapsible left sidebar on desktop (persistent) and a bottom-nav / hamburger drawer on tablet. Items are ordered by frequency of use for the primary role. Roles that cannot access a module never see that nav item.

| Nav Item | Chairman | Secretary | Member | Reviewer | Auditor | Administrator | Submitter | Guest/Presenter |
|---|---|---|---|---|---|---|---|---|
| Home / Dashboard | Y | Y | Y | Y | Y | Y | Y | Y |
| Backlog | Y | Y | Y | Y (read) | Y (read) | — | Y (own only) | Y (own topic only) |
| Agenda & Meetings | Y | Y | Y | Y (read) | Y (read) | — | — | Y (own meeting) |
| Decisions & ADRs | Y | Y | Y | Y (read) | Y (read) | — | Y (read, own topic) | — |
| Governance | Y | Y | Y | Y (read) | Y (read) | — | — | — |
| Actions | Y | Y | Y | — | Y (read) | — | — | — |
| Risks | Y | Y | Y | — | Y (read) | — | — | — |
| Dependencies | Y | Y | Y | — | Y (read) | — | — | — |
| Research | Y | Y | Y | Y (read) | Y (read) | — | — | — |
| Diagrams | Y | Y | Y | Y (read) | Y (read) | — | — | — |
| Reports | Y | Y | Y (limited) | — | Y | — | — | — |
| Admin | — | Y (members) | — | — | — | Y (full) | — | — |
| Global Search | Y | Y | Y | Y | Y | Y | Y | Y |
| Notifications Bell | Y | Y | Y | Y | Y | Y | Y | Y |
| User Profile / Preferences | Y | Y | Y | Y | Y | Y | Y | Y |

Notes:
- `Y (read)` = nav item visible; all sub-pages load in read-only mode
- `Y (own only)` = nav item visible; filtered to artifacts the user owns or submitted
- `—` = nav item hidden entirely

### 2.2 Secondary / Contextual Navigation

| Context | Secondary nav pattern |
|---|---|
| Backlog | Sub-tabs: List · Table · Kanban · Calendar · Timeline (Phase 2) |
| Topic detail | Sub-tabs: Overview · Comments · Attachments · Votes · Traceability · History |
| Meeting detail | Sub-tabs: Agenda · Attendance · Notes · MoM · Recording |
| Decision detail | Sub-tabs: Details · Alternatives · Voting Record · Traceability · History |
| ADR detail | Sub-tabs: Content · Links · History |
| Governance / Invariants | Sub-tabs: Active · Retired · Violations |
| Reports | Sub-tabs: Committee Dashboard · Secretary Dashboard · Chairman Dashboard · Stream Reports · Decision History · Action Trends · KPI Health |
| Admin | Sub-tabs: Users · Roles · Streams · Templates · Notification Settings · Job Monitor · System |
| Research | Sub-tabs: Research Missions · Findings · Recommendations · Wiki / Knowledge · Templates |

### 2.3 Breadcrumb Strategy

Every page displays a breadcrumb trail reflecting the canonical hierarchy. Breadcrumbs are deep-linkable at every level.

Examples:
- `Backlog > TOP-2026-042 — API Gateway Migration`
- `Agenda & Meetings > MTG-2026-018 > Minutes > MIN-2026-018`
- `Decisions & ADRs > DECN-2026-007 > Traceability`
- `Admin > Users > Ahmed Hassan`

Breadcrumb behavior:
- Each segment is a link; clicking any ancestor navigates to that level.
- On mobile/tablet: breadcrumb collapses to show only the immediate parent + current page title.
- Deep-linked pages (arrived via notification or search) reconstruct the breadcrumb from the route parameters; no broken breadcrumbs.

### 2.4 Artifact-Relationship "Go to Related" Pattern

Within the Relationships / Traceability panel on any detail page, each linked artifact is rendered as a compact card:

```
[ ICON ] DECN-2026-007 — Approve API Gateway Technology Choice     [Approved]
         Relationship: DerivedFrom · Committee Decision
         → Go to Decision  [link]
```

Relationship types displayed: `DerivedFrom`, `Supersedes`, `Implements`, `Resolves`, `References`, `Blocks`, `DependsOn`, `RelatesTo`. The panel shows immediate (1-hop) links by default; an "Expand impact analysis" control loads the transitive graph (Phase 2, up to depth 3).

### 2.5 EN/AR + RTL Nav Mirroring

- All nav elements are implemented using CSS logical properties (`inline-start`, `inline-end`, `margin-inline-start`, etc.) — ADR-0012.
- When locale switches to Arabic, the entire layout mirrors: the sidebar shifts to the right edge, breadcrumbs read right-to-left, icon-text order flips, and directional icons (chevrons, arrows) are mirrored via `transform: scaleX(-1)` or the `dir="rtl"` cascade.
- Navigation labels are translated via `react-i18next`; all labels must have EN and AR keys. No hard-coded EN strings in components.
- The locale switcher (EN / AR) is always visible in the top chrome, regardless of current page, and does not trigger an unsaved-changes navigation away (locale switches in-place; React re-renders with new locale; unsaved form data is preserved in state).

### 2.6 Light / Dark Note

The top chrome provides a theme toggle (light / dark) persisted in user preferences (stored in user profile; falls back to OS preference). The toggle is visible from every page. All color tokens are CSS custom properties; components never hard-code color values. See ADR-0012.

---

## 3. Sitemap

Indented tree. Route parameters in `:param` notation. Role-gated routes annotated with `[ROLES]` using role initials: **Ch**=Chairman, **Co**=Secretary, **M**=Member, **R**=Reviewer, **Au**=Auditor, **Ad**=Administrator, **Su**=Submitter, **G**=Guest/Presenter. A node without annotation is accessible to all authenticated users.

```
/
├── /login                              Auth entry point (OIDC redirect; not a page users linger on)
├── /logout
├── /auth/callback                      OIDC callback handler
│
├── /dashboard                          Home / Role Dashboard (default landing)
│   └── (role-specific widget composition; no sub-routes — role is in the widget config)
│
├── /backlog                            Backlog list (default: List view)
│   ├── ?view=list                      List view (default)
│   ├── ?view=table                     Dense table view
│   ├── ?view=kanban                    Kanban view
│   ├── ?view=calendar                  Calendar view
│   └── ?view=timeline                  Timeline/Gantt-lite view [Phase 2]
│
├── /topics
│   ├── /new                            Submit new topic  [Ch, Co, M, Su]
│   └── /:topicId                       Topic detail (canonical page for TOP-YYYY-###)
│       ├── /                           → Overview tab (default)
│       ├── /comments                   Comments tab
│       ├── /attachments                Attachments tab
│       ├── /votes                      Votes tab (shows all vote sessions on this topic)
│       ├── /traceability               Traceability/Relationships tab
│       └── /history                    Audit history tab
│
├── /meetings                           Meeting list / calendar
│   ├── /new                            Create meeting  [Co]
│   └── /:meetingId                     Meeting detail (MTG-YYYY-###)
│       ├── /                           → Overview tab
│       ├── /agenda                     Agenda builder/viewer (AGN-YYYY-###)
│       │   └── /edit                   Agenda editor (DnD, time-box, presenter)  [Co]
│       ├── /attendance                 Attendance tracker  [Co]
│       ├── /notes                      Live meeting notes per item  [Co]
│       ├── /minutes                    Minutes of Meeting detail (MIN-YYYY-###)
│       │   ├── /edit                   MoM editor  [Co]
│       │   └── /versions               MoM version history
│       ├── /recording                  Recording & transcript page  [Ch, Co, Au]
│       └── /history                    Meeting audit history  [Ch, Co, Au]
│
├── /decisions                          Decision list
│   ├── /new                            Create decision  [Ch, Co]
│   └── /:decisionId                    Decision detail (DECN-YYYY-###)
│       ├── /                           → Details tab
│       ├── /alternatives               Alternatives considered tab
│       ├── /voting-record              Voting record tab  [Ch, Co, Au, M (if voter)]
│       ├── /traceability               Traceability panel tab
│       └── /history                    Decision audit history  [Ch, Co, Au]
│
├── /votes
│   ├── /new                            Configure vote session  [Ch, Co]
│   └── /:voteId                        Vote session detail (VOTE-YYYY-###)
│       ├── /                           → Vote status + ballot UI (eligible voters see ballot)
│       ├── /results                    Results (visible after close)  [Ch, Co, Au, M if voter]
│       └── /audit                      Immutable vote audit trail  [Ch, Au]
│
├── /adrs                               ADR repository list
│   ├── /new                            Create ADR  [Ch, Co]
│   └── /:adrId                         ADR detail (in-app ADR-…)
│       ├── /                           → Content tab
│       ├── /links                      Related artifacts tab
│       └── /history                    ADR version history  [Ch, Co, Au]
│
├── /governance                         Governance home (invariants overview)
│   ├── /invariants                     Architecture invariants list
│   │   ├── /new                        Create invariant  [Ch, Co]
│   │   └── /:invariantId               Invariant detail (AIV-…)
│   │       ├── /                       → Detail tab
│   │       ├── /violations             Violations list for this invariant
│   │       ├── /exceptions             Exception requests  [Phase 3, Ch, Co]
│   │       └── /history               Invariant history  [Ch, Co, Au]
│   └── /violations                     Global violation list (all invariants)  [Ch, Co, Au]
│
├── /actions                            Action list (cross-topic)
│   ├── /new                            Create action  [Ch, Co, M]
│   └── /:actionId                      Action detail (ACT-…)
│       ├── /                           → Detail + progress notes
│       └── /history                    Action audit history  [Ch, Co, Au]
│
├── /risks                              Risk register list
│   ├── /new                            Create risk  [Ch, Co, M]
│   └── /:riskId                        Risk detail (RSK-…)
│       ├── /                           → Detail + mitigation
│       └── /history                    Risk audit history  [Ch, Co, Au]
│
├── /dependencies                       Dependency list + global graph view
│   ├── /new                            Create dependency edge  [Ch, Co, M]
│   ├── /:dependencyId                  Dependency edge detail (DPN-…)
│   └── /graph                          Full dependency graph viewer (Tarseem-rendered)  [Phase 2]
│
├── /research                           Research & Knowledge hub
│   ├── /missions                       Research mission list
│   │   ├── /new                        Create research mission  [Co, Ch]
│   │   └── /:missionId                 Research mission detail (RMS-…)
│   │       ├── /                       → Overview + Keystone package ref
│   │       ├── /findings               Imported findings (FND-…)
│   │       ├── /recommendations        Imported recommendations (REC-…)
│   │       ├── /risks                  Imported risks
│   │       └── /traceability           Research mission traceability panel
│   ├── /findings                       Global findings list (FND-…)
│   │   └── /:findingId                 Finding detail
│   └── /recommendations                Global recommendations list (REC-…)
│       └── /:recommendationId          Recommendation detail
│
├── /knowledge                          Wiki / Knowledge base
│   ├── /                               Wiki home / category index
│   ├── /new                            Create wiki page  [Co, Ch]
│   ├── /templates                      Template management
│   │   ├── /new                        Create template  [Co, Ch]
│   │   └── /:templateId                Template detail / editor  [Co, Ch]
│   └── /:docId                         Wiki page detail (DOC-…)
│       ├── /                           → Rendered content
│       ├── /edit                       Edit wiki page  [Co, Ch]
│       └── /history                    Wiki page version history
│
├── /diagrams                           Diagram list
│   ├── /new                            Create diagram (spec editor)  [Co, M]
│   └── /:diagramId                     Diagram detail (DGM-…)
│       ├── /                           → Diagram viewer (SVG) + export controls
│       ├── /spec                       JSON spec editor  [Co, M]
│       ├── /versions                   Diagram version history (spec diff)
│       └── /render-log                 Tarseem render log + error report
│
├── /reports                            Reports & dashboards
│   ├── /committee                      Committee dashboard (all roles)
│   ├── /secretary                    Secretary dashboard  [Co]
│   ├── /chairman                       Chairman dashboard  [Ch]
│   ├── /executive                      Executive / governance health  [Ch, Ad (read)]
│   ├── /stream/:streamId               Per-stream report  [Ch, Co, M (own stream)]
│   ├── /decisions                      Decision history report  [Ch, Co, Au]
│   ├── /actions                        Action completion trends  [Ch, Co]
│   └── /kpi                            KPI health dashboard  [Phase 3, Ch, Co, Ad]
│
├── /search                             Global search results page
│   └── ?q=:query&type=:type            Filtered search results
│
├── /notifications                      Notification center (inbox)
│   └── /preferences                    Per-user notification preferences  [all roles]
│
├── /profile                            User profile + preferences (own user)
│   ├── /settings                       Theme, locale, notification preferences
│   └── /sessions                       Active session management  [Phase 2]
│
└── /admin                              Admin area  [Co (members only), Ad (full)]
    ├── /users                          User list
    │   ├── /new                        Invite / provision user  [Ad]
    │   └── /:userId                    User detail / role & stream assignment  [Ad]
    ├── /roles                          Role definitions (read-only reference)  [Ad]
    ├── /streams                        Stream configuration  [Ad]
    ├── /notifications                  Global notification settings + adapter config  [Ad]
    ├── /jobs                           Hangfire job monitor  [Ad]
    │   └── /:jobId                     Job detail / retry  [Ad]
    ├── /audit                          Audit log browser  [Au, Ad]
    │   └── /export                     Audit log export  [Au, Ad]
    └── /system                         System health + integration status  [Ad]
```

---

## 4. Page & Screen Inventory

Comprehensive table of all screens. "Key components" names component families, not code symbols. "Required states" are the distinct UI states each screen must handle.

**Abbreviation key — Primary Roles:** Ch=Chairman, Co=Secretary, M=Member, R=Reviewer, Au=Auditor, Ad=Administrator, Su=Submitter, G=Guest/Presenter. ALL = all authenticated roles.

| # | Page | Route | Purpose | Primary Module | Key Components | Primary Roles | Required States |
|---|---|---|---|---|---|---|---|
| 1 | **Login / Auth Entry** | `/login` | OIDC redirect initiation | Platform | OIDC redirect button, locale selector, logo | ALL (unauthenticated) | Default; OIDC error; session-expired redirect |
| 2 | **Auth Callback** | `/auth/callback` | Process OIDC response, establish session | Platform | Spinner, error boundary | ALL (system) | Loading; error (token exchange fail); redirect-to-dashboard |
| 3 | **Home / Role Dashboard** | `/dashboard` | Role-appropriate summary of what needs attention today | Reporting | Role dashboard widget grid: Backlog Status widget, Meeting Readiness widget, Open Actions widget, Recent Decisions widget, Aging Topics widget, Open Votes widget (role-filtered composition) | ALL | Loading (skeleton widgets); empty (new org, no data); populated; permission-denied (if misconfigured) |
| 4 | **Backlog — List View** | `/backlog?view=list` | Browse all topics as a ranked list with filter/sort | Topics | Search + filter bar (type, urgency, status, stream, owner, date range), sort controls, saved-views dropdown, topic list rows (ID badge, title, type chip, urgency badge, status chip, owner avatar, stream tags, aging indicator), pagination / infinite scroll, "New Topic" FAB | Ch, Co, M, R (read), Au (read), Su (own) | Loading; empty state (no topics yet); filtered-empty (no results); error; permission-denied |
| 5 | **Backlog — Table View** | `/backlog?view=table` | Dense data-grid view of all topics | Topics | Column-configurable data table (show/hide columns, reorder), inline sort, row actions menu, column filter chips, bulk-select (Co/Ch: bulk status change), "New Topic" FAB | Ch, Co, M, R (read), Au (read) | Loading; empty; filtered-empty; error |
| 6 | **Backlog — Kanban View** | `/backlog?view=kanban` | Status-column board for visual backlog management and DnD prioritization/status change | Topics | Kanban board (status columns), draggable topic cards, DnD priority reorder (within column), DnD status change (between columns, with permission check), keyboard move-up/move-down controls (a11y alternative), quick-filter chips, swimlane by urgency (optional toggle) | Ch, Co (DnD), M (read+limited DnD) | Loading; empty; filtered-empty; permission-denied on drag |
| 7 | **Backlog — Calendar View** | `/backlog?view=calendar` | Topics plotted by scheduled meeting date | Topics | Month/week calendar grid, topic event chips (color = urgency), click-to-detail, date-range navigation, "Unscheduled" sidebar list | Ch, Co, M, R (read) | Loading; empty; no-scheduled-topics |
| 8 | **Backlog — Timeline View** | `/backlog?view=timeline` | Gantt-lite date bars per topic | Topics | Horizontal timeline, topic bars (created→target→scheduled→decided), zoom/pan controls, urgency color coding, click-to-detail | Ch, Co, M | Loading; empty; Phase 2 availability gate |
| 9 | **Submit New Topic** | `/topics/new` | Multi-step topic creation form | Topics | Stepper form (fields: title, description Markdown editor, topic type selector, urgency selector, source field, scope, affected streams multi-select, affected systems free-text, target date picker, attachments drag-drop upload), template selector (pre-fills description), autosave draft indicator, unsaved-changes guard, preview pane | Ch, Co, M, Su | Empty form; pre-filled from template; autosaved draft loaded; validation errors; submit-in-progress; submit-success (redirects to topic detail) |
| 10 | **Topic Detail — Overview** | `/topics/:topicId` | Single source of truth for one topic; all fields + linked artifacts | Topics | Topic header (ID, title, status chip, urgency badge), metadata panel (type, source, dates, owner, assignees, affected streams, affected systems), description (Markdown rendered), inline edit button (if permitted), Relationships panel (upstream + downstream typed links), per-topic actions panel (triage, accept, reject, defer, schedule, prepare actions per role) | Ch, Co, M, R, Au, Su (own), G (own) | Loading; not-found; permission-denied; read-only (status=Issued); edit mode |
| 11 | **Topic Detail — Comments** | `/topics/:topicId/comments` | Immutable comment thread on a topic | Topics | Comment list (avatar, author, timestamp, Markdown body), comment composer (Markdown editor, submit), no-edit/no-delete indicator | Ch, Co, M, R, Su (own), G (own) | Loading; empty; new comment posting; error |
| 12 | **Topic Detail — Attachments** | `/topics/:topicId/attachments` | File attachments linked to topic | Topics | Attachment list (filename, size, MIME type, uploader, upload date, download link), upload dropzone (with size/type validation), delete button (Owner/Co only), virus-scan status badge | Ch, Co, M, R, Su (own) | Loading; empty; uploading; scan-pending; error |
| 13 | **Topic Detail — Votes** | `/topics/:topicId/votes` | All vote sessions associated with this topic | Topics + Decisions | Vote session cards (status chip, eligible voter count, quorum, dates), "Configure Vote" button (Co/Ch), link to full vote detail | Ch, Co, M (if eligible voter), Au | Loading; no-votes-yet; vote-open (ballot visible to eligible voter); vote-closed |
| 14 | **Topic Detail — Traceability** | `/topics/:topicId/traceability` | Full relationship graph for this topic | Search & Traceability | Relationship panel (typed link cards with "go to related"), impact analysis trigger (Phase 2), traceability matrix export (Phase 3), relationship type filter chips, add-relationship control (Co/Ch) | Ch, Co, M, R, Au | Loading; no-links; populated; add-link modal open |
| 15 | **Topic Detail — History** | `/topics/:topicId/history` | Immutable audit timeline for this topic | Audit & Records | Chronological event timeline (actor avatar, action, field-change diff, timestamp), filter by action type, export to CSV (Au/Ad) | Ch, Co, Au | Loading; empty (new topic); populated |
| 16 | **Meeting List** | `/meetings` | Calendar and list of all committee meetings | Meetings | Month/week calendar view (meeting chips), upcoming meetings list, past meetings list, "New Meeting" button (Co), status badges (Scheduled / In Progress / Completed / Cancelled) | Ch, Co, M, R, Au | Loading; empty; calendar month/week toggle |
| 17 | **Create Meeting** | `/meetings/new` | Create a meeting record with metadata | Meetings | Form (date picker, time range, meeting type selector, mode selector, agenda link/create), validation, unsaved-changes guard | Co | Empty form; validation errors; submit-in-progress; success |
| 18 | **Meeting Detail — Overview** | `/meetings/:meetingId` | Meeting record summary + quick links to sub-pages | Meetings | Meeting header (ID, date, type, mode, status chip), metadata panel (attendance summary, agenda status, MoM status), quick-action links to sub-pages, linked topics list | Ch, Co, M, R, Au | Loading; not-found; pre-meeting; in-progress; completed |
| 19 | **Meeting — Agenda (View)** | `/meetings/:meetingId/agenda` | Published agenda for a meeting | Meetings | Ordered agenda item list (topic ID, title, presenter, time-box, status), carry-over badge, publish status indicator, download/share link | Ch, Co, M, R, Su (notified), G (own item) | Loading; agenda-not-created; agenda-draft; agenda-published |
| 20 | **Meeting — Agenda (Edit)** | `/meetings/:meetingId/agenda/edit` | Build and manage agenda; DnD reorder | Meetings | Drag-and-drop ordered agenda builder, item card (topic selector from scheduled topics, presenter selector, time-box input), "Add Item" button, carry-over auto-suggestions banner, keyboard reorder alternative (move-up/down buttons), publish button, unsaved-changes guard | Co | Empty; draft-in-progress; carry-overs-suggested; publish-confirm modal; published (read-only redirect) |
| 21 | **Meeting — Attendance** | `/meetings/:meetingId/attendance` | Record member attendance for quorum purposes | Meetings | Member list with Present / Absent / Remote toggle per member, quorum indicator (present count vs. required), timestamps | Co | Loading; pre-meeting (editable); locked (meeting completed) |
| 22 | **Meeting — Live Notes** | `/meetings/:meetingId/notes` | Capture and autosave per-item notes during meeting | Meetings | Agenda item list with per-item Markdown note editor, autosave indicator (debounce ≤2s), collaborative edit indicator (Phase 2), unsaved-draft banner | Co | Loading; empty; editing + autosaving; connection-lost (offline save queue) |
| 23 | **Meeting — MoM (View)** | `/meetings/:meetingId/minutes` | Rendered Minutes of Meeting | Meetings | Structured MoM renderer (attendance, agenda items + notes, decisions, actions), MoM status badge (Draft/Under Review/Approved), approval action button (Chairman), annotate/comment (Reviewer), download PDF/MD | Ch, Co, M, R, Au | Loading; not-generated-yet; draft; under-review; approved (read-only); |
| 24 | **Meeting — MoM (Edit)** | `/meetings/:meetingId/minutes/edit` | Edit draft MoM before review | Meetings | Markdown editor (pre-filled from generated draft), section structure (attendance, items, decisions, actions), autosave, unsaved-changes guard, "Submit for Review" button | Co | Loading draft; editing; autosaving; submit-for-review confirm |
| 25 | **Meeting — MoM Versions** | `/meetings/:meetingId/minutes/versions` | Historical MoM versions | Meetings | Version list (version #, actor, timestamp, status), side-by-side Markdown diff viewer | Ch, Co, Au | Loading; single-version; multi-version with diff |
| 26 | **Meeting — Recording** | `/meetings/:meetingId/recording` | Recording link + transcript viewer | Meetings | Recording metadata (title, duration, play URL, download URL, Webex source indicator), transcript viewer (speaker-attributed snippets, keyword highlight), AI-candidate extraction panel (Phase 3: proposed actions/decisions for Co approval), transcript search input | Ch, Co, Au | Loading; no-recording-yet; recording-pending-retrieval; recording-available; transcript-unavailable (Webex Assistant not enabled); transcript-available |
| 27 | **Decision List** | `/decisions` | All committee decisions | Decisions | Filter bar (outcome, stream, date range, chairman), decision list rows (ID, topic link, outcome chip, effective date, issuing chairman), export to CSV (Au/Ad), "New Decision" button (Ch/Co) | Ch, Co, M, R, Au | Loading; empty; filtered-empty |
| 28 | **Create / Issue Decision** | `/decisions/new` | Record a committee decision on a topic | Decisions | Form (topic link selector, outcome selector from canonical list, rationale Markdown editor, conditions text, alternatives list editor, authority actor selector, effective date picker), downstream artifact link requirement indicator, unsaved-changes guard | Ch, Co | Empty; pre-filled from vote result; validation-errors; submit-confirm (immutability warning) |
| 29 | **Decision Detail — Details** | `/decisions/:decisionId` | Canonical decision record | Decisions | Decision header (ID, outcome chip, topic link, effective date), rationale (Markdown rendered), conditions (if ConditionallyApproved), authority actor, downstream links list, "Create ADR from Decision" button (Ch, Phase 2) | Ch, Co, M, R, Au | Loading; not-found; active; superseded (with back-link) |
| 30 | **Decision Detail — Alternatives** | `/decisions/:decisionId/alternatives` | Alternatives considered in the decision | Decisions | Alternatives table (name, reason not chosen), add-alternative form (locked after Issued) | Ch, Co, M, R, Au | Loading; empty; populated; locked |
| 31 | **Decision Detail — Voting Record** | `/decisions/:decisionId/voting-record` | Full immutable vote record linked to this decision | Decisions + Voting | Vote session summary (eligible voters, quorum status, aggregate results), individual voter results (**always attributed in v1 — anonymity out of scope per ADR-0010**), chairman action (Confirm/Override/Abstain), immutability timestamp | Ch, Co, Au, M (if voter) | Loading; no-vote-linked; attributed-results-view |
| 32 | **Decision Detail — History** | `/decisions/:decisionId/history` | Audit trail for the decision record | Audit & Records | Event timeline (issued, superseded, linked artifacts), immutable | Ch, Co, Au | Loading; populated |
| 33 | **Vote Session — Ballot / Status** | `/votes/:voteId` | Vote casting (eligible voters) and status display | Decisions | Vote status chip, topic link, voter eligibility indicator, ballot card (voting options, conflict-of-interest toggle), cast-vote button, aggregate result (open vote: count only; closed: full), quorum meter, time remaining indicator | Ch, Co, M (if eligible), Au | Loading; configured-not-open; open+eligible (ballot active); open+ineligible (result view only); closed; ratified; error-double-vote |
| 34 | **Vote Session — Results** | `/votes/:voteId/results` | Aggregate and individual results post-close | Decisions | Results summary bar chart, voter list (**always attributed in v1 — anonymity out of scope**), chairman action record, export | Ch, Co, Au, M (if voter) | Loading; vote-not-closed; results-attributed |
| 35 | **Vote Audit Trail** | `/votes/:voteId/audit` | Immutable append-only vote audit log | Audit & Records | Chronological event list (each ballot cast, timestamp, quorum check, chairman action), export to CSV | Ch, Au | Loading; populated |
| 36 | **ADR List** | `/adrs` | ADR repository — searchable list | Governance | Search input (FTS), filter chips (status, stream, author), ADR list rows (ADR-ID, title, status chip, date, author), "New ADR" button (Ch/Co) | Ch, Co, M, R, Au | Loading; empty-repo; populated; filtered-empty |
| 37 | **Create ADR** | `/adrs/new` | Create an ADR using MADR-lite template | Governance | MADR-lite template form (title, status selector, context Markdown editor, decision Markdown editor, consequences Markdown editor, alternatives list editor, date, linked decision selector, linked invariant selector), unsaved-changes guard | Ch, Co | Empty; pre-filled from decision (Phase 2); validation errors |
| 38 | **ADR Detail — Content** | `/adrs/:adrId` | Rendered ADR content | Governance | MADR-format Markdown renderer, status lifecycle chip (Draft/Proposed/Approved/Superseded/Deprecated), superseded-by/supersedes links, "Download MD" button, approve / propose / supersede action buttons (role-gated) | Ch, Co, M, R, Au | Loading; draft; proposed; approved; superseded (with forward link) |
| 39 | **ADR Detail — Links** | `/adrs/:adrId/links` | Traceability links from/to this ADR | Governance | Relationship panel (typed link cards: linked topic, decision, invariant, diagram), add-link control (Co/Ch) | Ch, Co, M, R, Au | Loading; no-links; populated |
| 40 | **ADR Version History** | `/adrs/:adrId/history` | Version history of ADR spec edits | Governance | Version list, Markdown diff viewer | Ch, Co, Au | Loading; single-version; multi-version |
| 41 | **Governance Home — Invariants List** | `/governance/invariants` | Active and retired architecture invariants | Governance | Filter tabs (Active / Retired / Draft), invariant list rows (AIV-ID, category badge, scope badge, statement excerpt, violation count badge), "New Invariant" button (Ch/Co) | Ch, Co, M, R, Au | Loading; empty; populated |
| 42 | **Create Invariant** | `/governance/invariants/new` | Create an architecture invariant | Governance | Form (category selector, scope selector, statement Markdown editor, rationale Markdown editor, owner selector), unsaved-changes guard | Ch, Co | Empty; validation errors |
| 43 | **Invariant Detail** | `/governance/invariants/:invariantId` | Invariant detail + violations | Governance | Invariant header (AIV-ID, category, scope, status chip), statement (Markdown rendered), rationale, owner, violation count, lifecycle action buttons (propose, approve, retire — Ch/Co), linked ADRs and decisions | Ch, Co, M, R, Au | Loading; draft; proposed; active; retired |
| 44 | **Invariant Violations List** | `/governance/invariants/:invariantId/violations` | All recorded violations of this invariant | Governance | Violations table (description, discovering entity link, severity chip, remediation link, created date), "Record Violation" button (Co/Ch) | Ch, Co, M, Au | Loading; no-violations; populated |
| 45 | **Global Violations List** | `/governance/violations` | All violations across all invariants | Governance | Filter bar (invariant, severity, stream, status), violations table (AIV-ID link, severity, status, topic/action link), summary chip counts | Ch, Co, Au | Loading; empty; populated |
| 46 | **Actions List** | `/actions` | Cross-topic action registry | Actions | Filter bar (owner, stream, topic, status, due date range), status chip filter tabs (All / Open / InProgress / Blocked / Overdue / Completed / Verified), action list rows (ACT-ID, title, owner avatar, topic link, due date, status chip, overdue badge), "New Action" button (Ch/Co/M) | Ch, Co, M, Au | Loading; empty; filtered-empty; overdue-highlighted |
| 47 | **Create Action** | `/actions/new` | Create a follow-up action item | Actions | Form (title, description, linked entity selector (topic/decision/risk), owner selector, due date picker, priority selector), unsaved-changes guard | Ch, Co, M | Empty; pre-filled from MoM or decision; validation errors |
| 48 | **Action Detail** | `/actions/:actionId` | Single action item with progress history | Actions | Action header (ACT-ID, title, status chip, overdue badge), metadata (owner, due date, priority, linked entity), progress notes timeline (timestamped free-text), update-status controls, verification button (Co/Ch), cancel button (Co/Ch), Relationships panel | Ch, Co, M (own), Au | Loading; open; in-progress; blocked; overdue; completed; verified; cancelled |
| 49 | **Risks List** | `/risks` | Cross-topic risk register | Risks | Filter bar (likelihood, impact, status, stream, owner), risk matrix heatmap thumbnail (optional Phase 2), risk list rows (RSK-ID, title, likelihood/impact chips, status chip, owner, linked topic), "New Risk" button (Ch/Co/M) | Ch, Co, M, Au | Loading; empty; populated |
| 50 | **Create Risk** | `/risks/new` | Create a risk record | Risks | Form (title, description, likelihood selector, impact selector, owner selector, linked topic selector, mitigation plan Markdown editor — optional at creation), unsaved-changes guard | Ch, Co, M | Empty; validation errors |
| 51 | **Risk Detail** | `/risks/:riskId` | Single risk record + mitigation + lifecycle | Risks | Risk header (RSK-ID, title, likelihood+impact matrix tile, status chip), description, owner, mitigation plan (Markdown rendered/editable), escalate button (Co/Ch), accept button (Ch), close button (Co/Ch, requires mitigation or accepted status), Relationships panel, history | Ch, Co, M (own), Au | Loading; open; mitigating; accepted; escalated; closed |
| 52 | **Dependencies List** | `/dependencies` | All dependency edges | Dependencies | Filter bar (type, stream, status), dependency list rows (DPN-ID, from-entity, relationship type badge, to-entity, cross-stream badge), "New Dependency" button | Ch, Co, M, Au | Loading; empty; populated |
| 53 | **Dependency Detail** | `/dependencies/:dependencyId` | Single dependency edge | Dependencies | DPN-ID, from-entity link, relationship type, to-entity link, cross-stream indicator, created-by, notes | Ch, Co, M, Au | Loading; not-found |
| 54 | **Dependency Graph** | `/dependencies/graph` | Full interactive dependency graph (Tarseem-rendered) | Dependencies | Tarseem SVG viewer (interactive: click node → go to entity), layout selector (horizontal/vertical), export controls (SVG, PNG, PDF) — Phase 2 | Ch, Co, M | Loading; render-pending; render-error (Tarseem structured error); rendered |
| 55 | **Research Missions List** | `/research/missions` | All Keystone research missions | Research | Filter bar (status, linked topic), mission list rows (RMS-ID, title, topic link, Keystone package ref, import status, status chip), "New Mission" button (Co/Ch) | Ch, Co, M, R, Au | Loading; empty; populated |
| 56 | **Create Research Mission** | `/research/missions/new` | Create a research mission and link Keystone package | Research | Form (title, description, linked topic selector, Keystone package URL/path, status selector), unsaved-changes guard | Co, Ch | Empty; validation errors |
| 57 | **Research Mission Detail** | `/research/missions/:missionId` | Mission overview + imported artifacts | Research | Mission header (RMS-ID, title, status chip, Keystone ref link), import status badge, tabs to Findings / Recommendations / Risks, trigger-import button (Co/Ch), Relationships panel | Ch, Co, M, R, Au | Loading; not-imported; import-in-progress; import-complete; import-error |
| 58 | **Finding Detail** | `/research/findings/:findingId` | Individual imported finding | Research | FND-ID, source research mission link, finding statement (Markdown), status, linked actions/recommendations, Relationships panel | Ch, Co, M, R, Au | Loading; not-found |
| 59 | **Recommendation Detail** | `/research/recommendations/:recommendationId` | Individual imported recommendation | Research | REC-ID, source mission link, statement, status, linked decisions/actions, Relationships panel | Ch, Co, M, R, Au | Loading; not-found |
| 60 | **Wiki / Knowledge Home** | `/knowledge` | Category index for the knowledge base | Knowledge | Category cards (count of docs), recent articles list, search input (FTS within knowledge), "New Article" button (Co/Ch) | ALL | Loading; empty-wiki; populated |
| 61 | **Wiki Page Detail** | `/knowledge/:docId` | Rendered wiki page | Knowledge | Markdown rendered content, category breadcrumb, last-edited metadata, edit button (Co/Ch), version history link, cross-links to ACMP artifacts (rendered as artifact chips), Relationships panel | ALL | Loading; not-found; read-only; editing (edit mode overlay) |
| 62 | **Wiki Page Edit** | `/knowledge/:docId/edit` | Markdown editor for wiki page | Knowledge | Split-pane Markdown editor + live preview, artifact cross-link inserter, autosave indicator, unsaved-changes guard, "Save Version" button | Co, Ch | Empty draft; editing; autosaving; version-save confirm |
| 63 | **Template List** | `/knowledge/templates` | All artifact templates | Knowledge | Filter by type (Topic / MoM / ADR / Research Mission), template list rows (TPL-ID, name, type, last-edited), "New Template" button (Co/Ch) | Co, Ch, M (read) | Loading; empty; populated |
| 64 | **Template Detail / Edit** | `/knowledge/templates/:templateId` | Edit a Markdown template | Knowledge | Markdown template editor with placeholder markers, type selector, set-as-default toggle, unsaved-changes guard | Co, Ch | Empty; editing; default-badge |
| 65 | **Diagrams List** | `/diagrams` | All Tarseem diagrams | Diagrams | Diagram gallery (DGM-ID, thumbnail SVG, title, type badge, last-rendered, linked entity chip), filter by type/linked entity, "New Diagram" button (Co/M) | Ch, Co, M, R, Au | Loading; empty; gallery; list toggle |
| 66 | **Create Diagram** | `/diagrams/new` | Submit Tarseem JSON spec to create a diagram | Diagrams | JSON spec editor (Monaco/CodeMirror, with Tarseem schema validation hints), family/type selector, linked entity selector (topic/ADR/decision), "Validate & Queue Render" button | Co, M | Empty editor; schema-validation-errors; queued; render-pending |
| 67 | **Diagram Detail — Viewer** | `/diagrams/:diagramId` | Rendered SVG viewer + export | Diagrams | Tarseem SVG viewer (zoom/pan, fullscreen), export buttons (SVG, PNG, PDF, draw.io, PPTX), render metadata (engine version, spec hash), linked entity chip, re-render button (Co/M), version selector | Ch, Co, M, R, Au | Loading; render-pending; render-error (structured error display); rendered |
| 68 | **Diagram — Spec Editor** | `/diagrams/:diagramId/spec` | Edit diagram JSON spec | Diagrams | JSON editor, current spec displayed, Tarseem structured validation output, "Save & Re-render" button, spec diff from previous version | Co, M | Loading; editing; validation-errors; save-queued |
| 69 | **Diagram Version History** | `/diagrams/:diagramId/versions` | Spec diff history across versions | Diagrams | Version list (version #, render timestamp, spec hash, engine version), spec diff viewer | Ch, Co, M, Au | Loading; single-version; multi-version |
| 70 | **Reports — Committee Dashboard** | `/reports/committee` | Shared committee health view | Reporting | Backlog-by-status donut chart, topic-by-urgency bar chart, upcoming-meetings widget, recent-decisions list, open-actions count, overdue-actions count | ALL | Loading; no-data; populated |
| 71 | **Reports — Secretary Dashboard** | `/reports/secretary` | Secretary operational view | Reporting | Triage queue list, pending-MoM-approvals list, aging-topics-beyond-SLA list, overdue-escalation queue, notification-delivery-failures widget | Co | Loading; all-clear; items-requiring-action |
| 72 | **Reports — Chairman Dashboard** | `/reports/chairman` | Chairman decision-action view | Reporting | Votes-pending-chairman-approval list, escalated-risks list, escalated-actions list, deferred-twice-topics list | Ch | Loading; all-clear; items-requiring-action |
| 73 | **Reports — Executive Dashboard** | `/reports/executive` | High-level governance health for VP / sponsors | Reporting | Governance-health KPI tiles (topic throughput, decision rate, action SLA %, open risks by severity), trend charts, link to full reports | Ch, Ad (read) | Loading; populated |
| 74 | **Reports — Per-Stream** | `/reports/stream/:streamId` | All artifacts affecting one stream | Reporting | Stream selector, topics table (with status), decisions table (with outcome), actions table (with status), risks table — all filterable by date range and status, export to CSV per table | Ch, Co, M (own stream), Au | Loading; no-stream-data; populated |
| 75 | **Reports — Decision History** | `/reports/decisions` | Full decision history tabular report | Reporting | Filter bar (outcome, stream, chairman, date range), decision history table (ID, topic, outcome, date, chairman, stream), export to CSV | Ch, Co, Au | Loading; empty; populated |
| 76 | **Reports — Action Trends** | `/reports/actions` | Action completion trend chart | Reporting | Weekly/monthly toggle, line chart (open vs. closed vs. overdue over time), SLA compliance % stat, export PNG | Ch, Co | Loading; insufficient-data; populated |
| 77 | **Reports — KPI Health** | `/reports/kpi` | KPI and governance health indicators | Reporting | KPI tiles (avg topic-to-decision days by type, action SLA compliance %, backlog age distribution, vote-to-ratification time), sparklines, threshold indicators — Phase 3 | Ch, Co, Ad | Loading; Phase-3-gate; populated |
| 78 | **Global Search Results** | `/search?q=:query&type=:type` | Search results across all artifact types | Search & Traceability | Search input (persistent, updates results live), result type tab filter (All / Topics / Decisions / ADRs / MoM / Wiki / Diagrams), grouped result list (ID chip, title, excerpt, status chip, deep link), no-results state | ALL | Loading; no-results; populated; search-error |
| 79 | **Notification Center** | `/notifications` | User's notification inbox | Notifications | Notification list (icon, summary, artifact deep link, timestamp, read/unread indicator), mark-all-read button, filter (unread / type), infinite scroll | ALL | Loading; empty-inbox; populated; all-read |
| 80 | **Notification Preferences** | `/notifications/preferences` | Per-user per-event-type notification opt-in/out | Notifications | Event-type list with toggle per channel (**v1: in-app only**; Webex toggle = Phase 2; Email = deferred), save button | ALL | Loading; saving; saved |
| 81 | **User Profile** | `/profile` | Own user's profile + preferences | Platform/Membership | Avatar, name, email, role badges, stream memberships (read-only), locale selector (EN/AR), theme selector (light/dark), link to notification preferences | ALL | Loading; editing; saved |
| 82 | **Admin — User List** | `/admin/users` | All provisioned users | Membership | Search + filter (role, stream, status), user list rows (name, email, role badge, stream tags, active/deactivated chip), "Invite User" button (Ad) | Ad, Co (read) | Loading; empty; populated |
| 83 | **Admin — Invite / Create User** | `/admin/users/new` | Provision a new user record (global roles supplied by Keycloak; ACMP Admin manages committee membership and stream assignment only) | Membership | Form (name, email, stream multi-select; **global role sourced from Keycloak group/realm-role claims — not set here**), send-invitation button, unsaved-changes guard | Ad | Empty; validation errors; invite-sent |
| 84 | **Admin — User Detail** | `/admin/users/:userId` | View a user's role (from Keycloak) and manage stream assignment | Membership | User header (name, email, avatar), role badge (read-only, from Keycloak claims), stream assignment checkboxes (ACMP-managed), deactivate button, active-sessions list (Phase 2), activity summary | Ad | Loading; active; deactivated |
| 85 | **Admin — Streams** | `/admin/streams` | Stream configuration (names, slugs) | Platform | Stream list, edit inline, add stream button | Ad | Loading; populated |
| 86 | **Admin — Notification Settings** | `/admin/notifications` | Global adapter config + delivery failure log | Notifications | **v1: in-app notification center only (no external adapter needed).** Webex adapter config (tokens, webhook URL — Phase 2); Email adapter (deferred); no org notification platform (CON-001). Delivery failure log table | Ad | Loading; config-valid; config-invalid; failure-log populated |
| 87 | **Admin — Hangfire Job Monitor** | `/admin/jobs` | Background job queue status | Platform | Job queue summary (pending, processing, succeeded, failed counts), job list (type, status, scheduled time, next-run, retry count), retry and delete buttons (Ad) | Ad | Loading; all-healthy; failures-present |
| 88 | **Admin — Audit Log Browser** | `/admin/audit` | Searchable audit log | Audit & Records | Filter bar (entity type, entity ID, actor user, action type, date range), audit log table (entity, action, actor, timestamp, before/after state JSON viewer), pagination, export button | Au, Ad | Loading; empty (no filter applied); populated; export-in-progress |
| 89 | **Admin — System Health** | `/admin/system` | Health check + integration status dashboard | Platform | ASP.NET health check status tiles (liveness, readiness, DB, storage, Tarseem sidecar, Webex adapter, Hangfire), last-checked timestamp, error detail expander | Ad | Loading; all-healthy; degraded; critical |
| 90 | **Not Found (404)** | `*` | Catch-all for invalid routes | Platform | 404 illustration, "Go to Dashboard" link, locale-appropriate message | ALL | — |
| 91 | **Permission Denied (403)** | Inline state on any detail page | When a route is valid but the user lacks access | Platform | 403 message, role context hint, "Go to Dashboard" link | ALL | — |
| 92 | **Error Boundary** | Inline on all pages | Unhandled UI rendering errors | Platform | Error summary (user-friendly, no technical details), retry button, "Go to Dashboard" link | ALL | — |

---

## 5. Key Cross-Cutting UI Patterns

| Pattern | Pages / Contexts | Description |
|---|---|---|
| **List / Table with Filter, Sort, Saved Views** | All list pages (backlog, decisions, actions, risks, ADRs, etc.) | Standard filter bar (chips for enum fields, date-range pickers for dates, text search), sort by any column, ability to name and save a current filter configuration as a "saved view" per user. Filter state is reflected in the URL query string for shareability. |
| **Kanban + DnD (with keyboard-accessible alternative)** | `/backlog?view=kanban`, `/meetings/:meetingId/agenda/edit` | Column-based board using `@dnd-kit` (ADR-0012). Drag a card to reorder within column (changes priority ordinal) or move between columns (changes status, with role+permission check before drop). Keyboard alternative: each card has "Move up / Move down" controls and a "Move to column →" menu, ARIA-labeled, fully keyboard navigable. Drag-and-drop is an enhancement; the keyboard path is functionally complete. |
| **Calendar & Timeline** | `/backlog?view=calendar`, `/backlog?view=timeline`, `/meetings` | Calendar: monthly and weekly layout; topics/meetings appear as chips on their scheduled date; click opens detail. Timeline: horizontal date bars per entity; pan/zoom via mouse drag or keyboard. Both views degrade gracefully to a list on narrow viewports. |
| **Traceability / Relationship Visualization (graph + breadcrumb list)** | Traceability tab on every detail page, `/dependencies/graph` | Primary mode: compact relationship panel (typed link cards, 1-hop) always visible on detail pages. Secondary mode: "Expand impact analysis" loads a Tarseem-rendered dependency SVG (interactive, zoom/pan) — Phase 2. Breadcrumb trail reconstructs the navigation path. Export to CSV for traceability matrix — Phase 3. Relationship types shown as color-coded badges. |
| **Forms with Autosave / Unsaved-Guard** | All creation and edit forms (`/topics/new`, `/meetings/:id/notes`, `/knowledge/:id/edit`, etc.) | Autosave: triggered on typing pause (debounce ≤2s for live-note pages; on-field-blur for other forms); persisted to a server-side draft record. Autosave indicator: "Saved N seconds ago" / "Saving…" / "Save failed — retrying". Unsaved-changes guard: if a user navigates away with a dirty form and no recent autosave, a confirmation modal appears ("You have unsaved changes — Leave? / Stay?"). Locale and theme changes never trigger this guard. |
| **Status Chips** | All list and detail pages | Status is always displayed as a visually distinct chip: color-coded by status class (Draft=gray, Active/Open=blue, Blocked=amber, Overdue=red, Completed=green, Superseded=muted, Cancelled=muted). Status chips include an icon (where semantic: ⚠ overdue, ✓ completed, ⏸ blocked). Both the color and label change (not color alone) to meet WCAG AA non-text-contrast. Locale-switch renders the chip label in the active locale. |
| **Bilingual / RTL Behavior** | All pages, all components | All string content served via `react-i18next`. No hard-coded EN strings in JSX. CSS logical properties used throughout (no `left`, `right` except inside `dir=ltr` scope rules). When `dir="rtl"`: sidebar flips to right, breadcrumb reads RTL, table columns read RTL, DnD drag direction mirrors, icon directional semantics flip (chevrons, arrows via `transform: scaleX(-1)` or icon variants). Arabic number formatting applied where appropriate. Font stack: Latin default, Arabic/HarfBuzz-compatible stack for AR locale. |
| **Responsive Breakpoints (Desktop + Tablet)** | All pages | Desktop (≥1024px): full sidebar, multi-column layouts, detail page with side-by-side metadata + content. Tablet (≥768px): collapsed sidebar (icon-only, expandable on tap), single-column detail layout, bottom sheet for secondary panels. Mobile (<768px) is a documented stretch goal, not an MVP requirement [ASM-based; validate with org]. Calendar and timeline views fall back to a list on tablet. Kanban board scrolls horizontally on tablet. |
| **Artifact-Detail Pattern (consistent layout across all modules)** | All detail pages | Header band (ID, title, status chip, urgency badge, key metadata); tabbed sub-navigation (Overview, Comments/Content, Attachments, Traceability, History); Actions sidebar panel (role-gated action buttons); Relationships/Traceability panel (typed link cards, always 1-hop visible, expand to graph); History/Audit timeline (at bottom or own tab). This consistent structure means a user who learns the Topic detail page immediately understands the Decision, ADR, and Action detail pages. |
| **Voting Panel** | `/votes/:voteId`, `/topics/:topicId/votes` | Ballot card (voting options as large touch-friendly radio buttons), conflict-of-interest toggle (SR-labeled), submit confirmation modal, quorum meter (progress bar), aggregate results bar chart (anonymous or attributed per config), chairman action panel (Confirm / Override with mandatory reason / Abstain-from-override). Panel is inaccessible to non-eligible voters; shows aggregate view instead. |
| **MoM Editor (structured Markdown with autosave)** | `/meetings/:meetingId/minutes/edit` | Split-pane: left = Markdown editor; right = live preview. Sections are structured (attendance block auto-inserted from attendance record, agenda items pre-structured from agenda, decisions and actions linkable as artifact chips). Autosave. Section anchors allow deep-linking to specific MoM sections (e.g., `/meetings/MTG-2026-018/minutes#decisions`). |
| **Diagram Viewer (Tarseem SVG)** | `/diagrams/:diagramId`, `/dependencies/graph` | SVG rendered from Tarseem sidecar. Pan (click-drag), zoom (scroll / pinch on tablet), fullscreen mode, export buttons. Render-pending state: spinner with estimated wait (Hangfire job). Render-error state: structured error list (Tarseem `{code, path, message, hint}` per error). Interactive mode (Phase 2): clickable nodes navigate to linked ACMP artifact detail pages. |
| **Notification Deep Links** | All notification events | Every notification (Webex card or email) includes a direct URL to the relevant artifact detail page at its canonical route. The deep link is stable and bookmarkable. If the user is not authenticated when following the link, they are redirected to login then returned to the original target URL. |

---

## Traceability

- **Deliverables:** 17 (Information Architecture, §1–§2), 18 (Sitemap, §3), 19 (Page & Screen Inventory, §4–§5).
- **Feeds directly:** `design-handoff/claude-design-input-package.md`, `design-handoff/claude-design-prompts.md`.
- **Depends on (canonical inputs):**
  - `../README.md §B` (canonical modules), `§C` (canonical roles), `§E` (status models), `§F` (entity ID scheme)
  - `docs/requirements/functional.md` (FR-### referenced inline via component/feature requirements)
  - `docs/domain/stakeholders.md` (role access postures)
  - `docs/domain/permission-role-matrix.md` (full permission detail; this doc summarizes nav access only)
  - `docs/adrs/adr-0006` (Tarseem integration), `ADR-0007` (Keystone), `ADR-0008` (traceability model), `ADR-0009` (immutability), `ADR-0010` (voting), `ADR-0011` (search), `ADR-0012` (frontend stack, RTL, DnD, i18n)
- **Downstream consumers:**
  - `docs/domain/architecture-detail.md` (module boundaries reflected in route groupings)
  - `docs/domain/search-and-traceability.md` (global search + traceability patterns formalized here)
  - `docs/domain/reporting-dashboards.md` (dashboard pages 70–77 are specified here; that doc provides widget detail)
  - `docs/planning/work-breakdown.md` and `docs/domain/user-stories-mvp.md` (user stories map to pages in this inventory)
- **Open decisions touching this doc:** `OQ-###` (mobile breakpoint as MVP vs. Phase 2 — validate with org); `OQ-###` (collaborative real-time MoM editing — Phase 2 assumption).
- **Assumption:** `ASM-###` — desktop + tablet are the required form factors for MVP; mobile is a stretch goal. Validate with committee.
