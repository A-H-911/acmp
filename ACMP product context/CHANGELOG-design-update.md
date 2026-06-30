---
artifact: ACMP design update — engineer brief
status: active
version: v2
updated: 2026-06-30
purpose: Brief engineers on what changed in the ACMP design after the forensic review, and on the decisions taken for every open question.
---

# ACMP Design Update — CHANGELOG

Driven by `rebuild-findings.md` §10. Two things shipped:
1. **`ACMP Usage Map.dc.html`** — a new, dedicated source-of-truth index (sections A–H + a Decisions log). This is the file engineers consult per phase and per flow. EN/AR, full RTL, light/dark.
2. **`ACMP Design System.dc.html`** — the off-scale literals are now published as **named tokens**, and the canonical chip, scroll model, scrollbar and breadcrumb are settled in one place.

The Usage Map is authoritative. Where any older `.dc.html` disagrees with it, the Usage Map wins.

---

## Decisions on the open questions

| # | Question | Decision |
|---|---|---|
| 1 | **Meeting-page ownership (BLOCKER, RD-08)** | **`ACMP Meetings`** owns the detail **shell** — list · schedule · overview/lifecycle · recording · route-level permission-denied. **`ACMP Agenda & Meeting`** owns the **content** — agenda builder/viewer · conduct workspace (attendance + notes + actual-time/outcome) · minutes/MoM. The split is documented in Usage Map §A. |
| 2 | **Meeting sub-tab IA (BLOCKER, NV-08)** | Canonical, addressable sub-tabs = **Agenda · Attendance · Notes · Minutes · Recording**. The live "conduct" workspace is the runtime composition of Attendance + Notes while `inprogress`; each remains deep-linkable. |
| 3 | **Notification surfaces (RD-09)** | Two owned surfaces only: **bell popover** (`ACMP.dc.html` shell) + **full inbox** (`ACMP System States` → `/notifications`). v1 is **in-app only**, **Load-more** (not infinite scroll). **No preferences page ships in v1.** Webex channel = Phase 2. |
| 4 | **Canonical StatusChip (DV-01, AM-01, DI-06)** | Locked to DS canon (code is the tiebreaker): **md = 24 / 9 / 12**, **sm = 22 / 8 / 11.5**, chip radius **6**. The old **23** is retired. Now published as named tokens. |
| 5 | **Off-scale tokens (LP-02)** | Published in the Design System: `--control-radius:9`, `--chip-radius:6`, `--chip-h/px/fs-md|sm`, `--header-h:60`, `--sidebar-w:244`, `--row-min:44`, `--field-gap:16`, `--page-max:72rem`, `--agenda-*`. Screens consume the variable, never a literal. |
| 6 | **Rich text (DV-04, HD-06)** | **Markdown** is the single model — stored as Markdown, rendered to safe HTML, identical across **Submit · Meeting notes · MoM** via one shared editor. Published minutes lock to read-only (immutable). |
| 7 | **Nav IA (NV-02…06)** | Groups: **Committee · Governance · Knowledge · Insights · System** (already in `ACMP Navigation & IA`). Decisions and "ADRs & Invariants" sit under their groups; **Audit** is under **System** + a per-page History tab (not a standalone top-level). Unbuilt areas **show their existing designs**; only true Phase-2 surfaces show a designed "Phase 2" state — never a generic placeholder. |
| 8 | **Blessed build deviations** | Absorbed into the design (no future false "drift"): topic key on backlog/kanban/agenda rows · Agenda-status chip column on the meetings list · Load-more not infinite scroll · presenter = accessible `Select` · whole-row title-link navigation · single-day meeting with separate date + time fields · agenda publish = one "notifies attendees" line · agenda pool label = **"Prepared"** (not "Scheduled"). |
| 9 | **Curated-out elements** | No self-registration, no in-app role assignment. The user menu states **"roles are read-only from Keycloak"**. Provisioning = a deep-link to the Keycloak admin console only. The engineering-only **dev role-switcher does NOT appear** in the design. |
| 10 | **Actual-time / outcome (DV-16)** | A design-faithful actual-time + outcome control is **re-added** to the conduct workspace (backend `RecordActualTime` already wired). Live elapsed timer = mm:ss / h:mm:ss; timeboxes = whole minutes (DV-03). |
| 11 | **Scroll & header (AM-02, LP-04)** | Header height locked at **60px**; sidebar **244px**. Document scrolls; TopBar + SideNav sticky; **no inner overflow container**; one custom scrollbar (11px). |
| 12 | **Breadcrumb (NV-07, LP-01)** | **Lifted into the shell layout** — rendered on **every** page (incl. dashboard & former placeholders), 12px gap below. Rule: `Home › Area › Record › Sub-tab`. Chevrons flip in RTL. |

---

## What changed, per file

- **`ACMP Usage Map.dc.html`** — **NEW.** Sections: 00 Decisions · A File ownership · B Screen & state index (32 screens) · C Per-phase build map (P3–P19) · D Per-flow map (7 flows) · E Component index · F Data-binding notes · G Navigation/IA spec · H Interaction notes. Bilingual EN/AR, full RTL, light/dark.
- **`ACMP Design System.dc.html`** — Added named off-scale tokens to the base theme block; added a **"Named tokens & system rules"** section (canonical StatusChip md/sm, off-scale token table, breadcrumb rule, scroll model + scrollbar + field rhythm). No component visuals changed — only the literals got names.
- **`ACMP Navigation & IA.dc.html`** — Confirmed authoritative as-is: groups, "Agenda & Meetings" label, Governance group, Audit under System, and "roles read-only from Keycloak" in the role menu already match the decisions. No change required.

---

## De-duplications / removals

- **Meeting overlap** → resolved by the ownership split above (Usage Map §A). `ACMP Meetings` owns route-level permission-denied; the `denied` state inside `ACMP Agenda & Meeting` is the **minutes edit-access** denial (a legitimate sub-state of `minutes`, not a second route gate) — re-scope its enum to `minutes/edit-denied` when convenient.
- **Notifications** → collapsed from 3 references to 2 owned surfaces (bell + inbox); docs/14 p.79/80 are reference only; preferences dropped from v1.
- **Registers vs detail (RD-10)** → confirmed intentional: `ACMP Lists & Registers` = list/register views; `ACMP Decision, Voting & ADR` = detail. No contradiction.
- **Admin `notif` vs user inbox** → clarified: Administration's "Notification settings" is org-level config (distinct from the user's `/notifications` inbox).

---

## Data contracts to fill the honest-empty surfaces (Usage Map §F)

Calendar/Timeline (topic `scheduledDate`/event series + span) · Backlog Stream filter (stream registry, BL-024) · Owner filter (`/api/members`) · Votes tab (vote records: voter/choice/quorum/COI/chairman approval) · Assignments column (assignee counts) · Agenda readiness (per-topic flags) · Quorum indicator (eligible + present counts) · Recording `ready` (Webex player/transcript, Phase 2).

---

## Out of scope here (doc-integrity items from the findings — flag to Tech Lead)

These are **not** design changes; they live in code/docs: ADR-0015 number collision (DI-01) · OQ-043 RowVersion (DI-02) · OQ-041 gap (DI-04) · OQ-034 search engine OpenSearch vs Meilisearch (DI-08) · ledger StatusChip mis-record (DI-06).
