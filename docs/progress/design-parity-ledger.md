---
artifact: design-parity-ledger
status: active
version: v1
updated: 2026-06-26
---

# ACMP Design-Parity Ledger

Per-component divergence between the implementation and the Claude Design "ACMP product context"
reference. Run via `tools/parity` (see its README). Each row is classified on two axes:

- **Visual**: `FIX` (match exactly) · `MATCH`
- **Behaviour**: `KEEP-GATED` (match look, restrict behaviour) · `INTENDED-DATA` (prototype demo
  data not copied) · `MATCH`

Scope = built surfaces only (Design System · Logo · Sign In · ACMP shell · Navigation & IA ·
Administration). Other design files become targets as P5+ lands.

---

## Agenda builder + Meeting workspace (Agenda & Meeting `.dc.html`) — P6 review, fixed 2026-06-27

Reference rendered + computed-style inspected (EN-light, AR-RTL-dark) against the implementation
(`features/meetings/*` + `meetings.css`). Color tokens already matched exactly; the divergences were
structural (radius/size/state/copy). All rows below reconciled to **FIX→MATCH**.

| # | Element | Reference | Impl before | Impl after | Visual |
|---|---|---|---|---|---|
| 1 | Pool "Add" button | 24px accent-tint pill (`--accent` border, `--primary-tint` bg, 7px) | shared `btn-sm` secondary (32px) | `.mt-pool-add` restyle on shared `<Button>` (24px accent pill) | FIX→MATCH |
| 2 | Time-budget bar | green/amber/red fill + **hatched buffer** segment; "Fits/Tight fit/Over by N" | green→red only, no buffer, no tight tier | 3-tier fill + buffer span + Fits/Tight/Over copy | FIX→MATCH |
| 3 | Agenda-spine done item | solid `--st-success-dot` + #fff check; done title `--text-3`; active title `--accent` | light `--st-success-bg`; titles unstyled | solid dot + #fff; `.active`→accent, `.done`→muted | FIX→MATCH |
| 4 | Attendance avatar | present `--primary`+#fff + green dot; absent `--sunken` | static `--primary-tint`, no dot | present/absent variance + `.mt-avatar-dot` | FIX→MATCH |
| 5 | Attendance summary | "N of M present · K needed" | dropped "· K needed" | quorum-needed threaded back in (EN+AR) | FIX→MATCH |
| 6 | Drop-zone idle bg | `transparent` | `var(--subtle)` | `transparent` | FIX→MATCH |
| 7 | Card radius | containers 12px · item/budget 11px | every card `--r-lg` (10px) | containers `--r-xl`(12) · item/budget 11px | FIX→MATCH |
| 8 | `.mt-key` (topic ref) | 10.5px / weight 400 | 12px / 500 | 10.5px / 400 | FIX→MATCH |
| 9 | Card paddings | budget 14×16 · item 13×14 · heads 12-13×14-15 | snapped to `--sp` grid (12/16) | explicit ref px | FIX→MATCH |
| 10 | Copy (EN+AR) | "Ready to schedule" · aria "Backlog pool" · "Search topics…" · "Drag to reorder · ↑↓ keys" · "Time-box" · "Items & total time" · "…notify attendees" | paraphrased | aligned verbatim to reference | FIX→MATCH |

**Intended (not drift), kept:** StatusChip DS §08 = 24/9/12 (md), §09 = 22/8/11.5 (sm) — DS-canon, overrides the dc's 23 (code tiebreaker: `StatusChip.tsx:12`, `styles/components.css:48,52`);
RTE→textarea, Pause/Preview + Decision/Action/Vote disabled stubs (P7-P9), notify-group checkboxes→
single honest line, presenter-cycle→Select, mm:ss→minutes, mock→real data; move/step/tool buttons
24-28px (reference is also sub-44px — faithful). Meetings list + ScheduleMeetingDialog have no design
reference (behavior scaffolding).

**Verified:** web 182/182 · meetings/notif 46/46 · tsc + oxlint clean · i18n parity 415 keys ·
reference render computed-value confirmed · **live browser pass done** — populated agenda-builder +
live meeting-workspace rendered against stubbed `/api` fixtures (Playwright route interception, dev-auth
context) in EN-light + AR-RTL-dark; all 10 rows visually confirmed to MATCH (buffer hatch + "Fits",
"Ready to schedule", accent "Add" pill, solid-green done spine, accent active title, present/absent
avatars + dot, "N of M present · K needed", rounded cards, RTL fully mirrored). No standing caveat.

---

## Topbar role control (Navigation & IA — `toggleRoleMenu`) — POC, fixed 2026-06-26

Reference: 210px bordered trigger (avatar + 2-line + chevron) → 280px `role="menu"` with a
"read-only from Keycloak" header and 8 `menuitemradio` rows (avatar + label + accent check).
Implementation before: `DevRoleSwitcher` = a 34px `.chip-btn` wrapping a **native `<select>`**.

| Element | Reference (markup) | Impl before | Impl after | Visual | Behaviour |
|---|---|---|---|---|---|
| trigger block-size | 40px | 34px | 40px | FIX→MATCH | — |
| trigger border | `--border-strong` | `--border` | `--border-strong` | FIX→MATCH | — |
| trigger radius | 9px | `--r-md` (8px) | `--control-radius` (9px) | FIX→MATCH | — |
| trigger min-inline-size | 210px | — | 210px | FIX→MATCH | — |
| trigger content | avatar + 2-line + chevron | "Role" + native select | avatar + 2-line + chevron | FIX→MATCH | — |
| open menu | custom 280px radio menu | native OS `<select>` popup | custom 280px radio menu | FIX→MATCH | — |
| menu header | "Roles are read-only from Keycloak" | — | same (EN+AR) | FIX→MATCH | — |
| items | `menuitemradio` + avatar + accent check | OS options | `menuitemradio` + avatar + check | FIX→MATCH | — |
| a11y | menu / menuitemradio / aria-checked | native select | menu / menuitemradio / aria-checked | FIX→MATCH | — |
| role switching | prototype switches | dev switches | dev switches | — | **KEEP-GATED** (dev-only; absent in prod, guardrail 4) |
| per-role sub + avatar colours | "Committee lead · power user" etc., role-tinted | — | trigger sub = "Preview role (dev only)"; primary avatars | — | **INTENDED-DATA** (prototype demo strings/colours not copied) |
| person identity ("Khalid A.") | demo person | "Dev User" | "Dev User" | — | **INTENDED-DATA** |

**Verdict:** all visual rows FIX→MATCH; behaviour stays dev-gated; prototype demo data intentionally
not copied. Verified: build/test/lint/i18n green + live screenshot (open menu, EN-light & AR-dark).
