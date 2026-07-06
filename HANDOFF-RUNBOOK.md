# ACMP — Handoff Runbook

**Purpose:** operator guide for driving the ACMP build with Claude Code, plus the record of the original planning-package handoff. Keep this at the repo root.

**Date:** 2026-06-25 · **Updated:** 2026-07-06 (paths remapped to the Keystone v1.0.0 package layout; Stages 1–4 are historical — the handoff is complete and the MVP has shipped through P12).

---

## What was handed off (historical)

Two packages, in order:

1. **Planning package** — the build spec: *what* to build and *how*. Originally landed as `/CLAUDE.md`, `/docs`, `/adr`, `/execution-handoff`, `/design-handoff`, `/.context`. Since the Keystone migration (PR #97) it lives as `/CLAUDE.md` + `/AGENTS.md` + the **Keystone package under `/docs`** (ADRs in `docs/adrs/`, handoff prompts in `docs/handoff/`); `/design-handoff` is retained as an archived record. This package is the authoritative source of truth for behavior.
2. **Design references** — the exact UI. The authoritative form is the **local `.dc.html` files** in `/ACMP product context/` at the repo root, read **directly with file tools** (INV-014); the [Usage Map](ACMP%20product%20context/ACMP%20Usage%20Map.dc.html) is the per-screen index. (The original Claude-Design-MCP import route is **superseded** — see Stage 4.)

> Role note: the committee-lead role is **Secretary of the Committee** / **أمين سر اللجنة** (canonical token: `Secretary`). Use the planning package's canonical role identifiers, not the prototype's internal keys.

---

## Stage 1 — Planning package into the repo ✅ (done 2026-06-25)

The package zip was extracted to the repo root and now lives in the Keystone layout described above. The hidden `.context\` folder is included — several ADRs reference `.context/brief-digest.md`.

Note: `README.md` exists at **both** the repo root and `docs\README.md` — intentional. `docs/README.md` is the canonical package entry; the root copy is the GitHub landing page.

## Stage 2 — Baseline commit ✅ (done 2026-06-25, commit `c487448`)

## Stage 3 — Hand off the planning package to Claude Code ✅ (done; use for fresh sessions)

For a **fresh session today**, send:

> Read `CLAUDE.md` (which imports `AGENTS.md`), then `docs/handoff/initial-prompt.md`, and follow it. State the current phase, the branch you are on, and the single next work item before touching anything.

Phase work then proceeds via the per-slice prompts in `docs/handoff/follow-up-prompts.md` — the shipped history and remaining slices are defined in the build-slice ladder in `docs/planning/roadmap.md`.

## Stage 4 — Design references

> **The design reference is the local `.dc.html` files** in `/ACMP product context/` — the agent reads them **directly with its file tools, not via the design MCP** (INV-014). The Usage Map (`ACMP product context/ACMP Usage Map.dc.html`) is the authoritative per-screen index. The Claude-Design-MCP import route (and its zip fallback) used at the original handoff is **superseded**; its instructions are archived in [`design-handoff/`](design-handoff/) should a design re-import ever be needed.

## Stage 5 — Drive the build

Work through the remaining per-slice prompts in `docs/handoff/follow-up-prompts.md` (ladder `P1–P19` in `docs/planning/roadmap.md`; P1–P12 shipped). Each slice self-enforces: unit + integration tests, the relevant `AC-###` (`docs/validation/acceptance-criteria.md`), authorization (`docs/domain/permission-role-matrix.md`) + audit events, no hardcoded strings (EN+AR) + RTL, progress-log + acceptance-audit updates, conventional commits. Confirm each slice before the next. If a step would break an invariant (`docs/requirements/invariant-register.md`), the agent is instructed to **stop and ask** — a violation requires a new ADR.

---

## Quick reference — key files

| File | Role |
| --- | --- |
| `/CLAUDE.md` → `/AGENTS.md` | Standing brief — read first, every session |
| `/docs/README.md` | Package entry: reading order, canonical reference (§A–§G), layout |
| `/docs/handoff/initial-prompt.md` | First message to Claude Code (resume orientation) |
| `/docs/handoff/follow-up-prompts.md` | Per-slice prompts (P13–P19) + situational prompts |
| `/docs/planning/roadmap.md` | Phases + exit criteria + the P1–P19 build-slice ladder |
| `/docs/requirements/invariant-register.md` | Binding constraints (INV-001…014) |
| `/docs/validation/acceptance-criteria.md` | `AC-###` acceptance criteria |
| `/docs/domain/permission-role-matrix.md` | Role × action × ABAC scope |
| `/ACMP product context/` | Local `.dc.html` design references + Usage Map (per-screen index) |
| `/design-handoff/` | **Archived** — original Claude-Design input package + prompts (superseded by direct-read) |
| `/VERIFICATION.md` | Planning-package verification record |

---

## Non-negotiables (carried from the package)

- Self-contained (CON-001): app-owned Hangfire on ACMP's SQL, self-hosted Seq + MinIO, **self-hosted Keycloak (ACMP-owned realm) + SQL Server bundled (ADR-0015)**, in-app notifications v1 — **no** dependency on org Hangfire/ELK/Seq/notification platform; **zero external runtime services** (Webex Phase 2 is the only external dependency).
- Audit immutability + hash-chain; votes/decisions/ADRs/published-minutes are superseded, never edited.
- Always-attributed voting with quorum + abstentions + chairman approval + conflict-of-interest recusal.
- Self-hosted Keycloak OIDC (ADR-0015); roles from claims; no self-registration.
- Bilingual EN/AR, full RTL, Gregorian dates, WCAG 2.2 AA.
- Tarseem = Phase 2 (JSON spec is source of truth); Keystone = optional; AI extraction = Phase 3.
- Any change to a settled decision requires a new ADR.
