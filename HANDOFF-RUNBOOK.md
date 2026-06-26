# ACMP — Handoff Runbook

**Purpose:** step-by-step guide for handing the ACMP **planning package** and the **Claude Design UI** to Claude Code, then driving the build. Keep this at the repo root.

**Date:** 2026-06-25

---

## What gets handed off

Two packages, in order:

1. **Planning package** (this folder) — the build spec: *what* to build and *how*. It lands in the repo as `/CLAUDE.md`, `/docs`, `/adr`, `/execution-handoff`, `/design-handoff`, `/.context`. This is the authoritative source of truth.
2. **Claude Design prototype** — the exact UI (11 screens). Imported into Claude Code via the **Claude Design MCP** (Route A) or a downloaded **zip bundle** (Route B). The design defines visuals only; behavior comes from the planning package.

> Role note: the committee-lead role is **Secretary of the Committee** / **أمين سر اللجنة** (canonical token: `Secretary`). Use the planning package's canonical role identifiers, not the prototype's internal keys.

---

## 0. Prerequisites

- GitHub repo created and cloned locally — e.g. `https://github.com/A-H-911/acmp` at `C:\Users\ahammo\repos\acmp`.
- Claude Code installed and launched in the repo.
- Claude Design access (Pro / Max / Team / Enterprise; on **Enterprise it is off by default** — an admin must enable it in organization settings).

---

## Stage 1 — Put the planning package into the repo

Extract the package zip so its **contents** sit at the repo root (not in a nested folder):

```powershell
Expand-Archive -Path "$HOME\Downloads\ACMP-planning-package.zip" -DestinationPath "C:\Users\ahammo\repos\acmp" -Force
cd C:\Users\ahammo\repos\acmp
Get-ChildItem -Force
```

You should see `CLAUDE.md`, `docs\`, `adr\`, `execution-handoff\`, `design-handoff\`, `README.md`, `VERIFICATION.md`, and the hidden `.context\`.

Notes:
- `README.md` exists at **both** the repo root and `docs\README.md` — intentional. `CLAUDE.md` and the docs reference `/docs/README.md` as the canonical reference; the root copy is the GitHub landing page.
- Include the hidden `.context\` folder — several ADRs reference `.context/brief-digest.md`.

---

## Stage 2 — Baseline commit

```powershell
git add .
git commit -m "docs: ACMP planning package (handoff baseline)"
git push
```

---

## Stage 3 — Hand off Package 1 (planning) to Claude Code

Send this as the **first message** to the running Claude Code session:

> Read `CLAUDE.md`, then `execution-handoff/initial-prompt.md`, and follow it. Summarize back the stack, the module list, the build order, and the guardrails — then give me your PH-0 validation plan. Do **not** scaffold or write code until I confirm.

What to expect:
1. It reads the canon (`README` / `docs` / `adr` / guardrails) and reports its understanding + a PH-0 plan.
2. You confirm or correct.
3. It runs **PH-0 validation** and scaffolds the repo (solution, modular monolith, React app shell, `docker-compose` with keycloak/sqlserver/seq/minio — Keycloak self-hosted with a realm bootstrap under `deploy/keycloak/` per ADR-0015, one reference module), keeping a **progress log** and **acceptance audit**.
4. Then proceed phase-by-phase via `execution-handoff/phase-prompts.md` — **core loop first** (Topics → Meetings → Decisions → Actions).

---

## Stage 4 — Hand off Package 2 (Claude Design UI)

> **Updated approach (2026-06-25):** the design reference is now the **local `.dc.html` files** in the `/ACMP product context/` folder at the repo root — the agent reads them **directly** with its file tools. The claude_design MCP route below is **superseded** for this build (keep only as an optional fallback).

Best brought in when Claude Code reaches the **frontend foundation (P3)** — backend-first still applies.

### Route A — Claude Design MCP (one paste once connected)

See "Adding the Claude Design MCP" below. Once connected, paste the design handoff prompt (also below).

### Route B — Zip fallback (no connector, no login, no plan gating)

In Claude Design: **Share → Send to → Claude Code → tick "Download zip instead"**. Save the bundle into the repo, then tell Claude Code:

> Implement the UI to match the design bundle at `<path>`, following the planning package.

---

## Adding the Claude Design MCP (manual)

Run in a terminal on your machine (PowerShell is fine).

**1. Add the server:**

```powershell
claude mcp add --transport http claude-design https://api.anthropic.com/v1/design/mcp --scope user
```

- `claude-design` is the local name you'll see in `/mcp`.
- `--scope user` = available in all projects; use `--scope local` for this repo only.
- If a `claude` session was already running, exit and relaunch it so the new server loads (config is written to `~/.claude.json`).

**2. Authenticate (OAuth):**

```powershell
claude mcp login claude-design
```

or inside the Claude Code session: `/mcp` → pick `claude-design` → complete the browser login.

**3. Verify:**

```powershell
claude mcp list
```

Want `claude-design` showing **connected** with a tool count. Inside the session, `/mcp` shows the same.

**4. Use it:** paste the design handoff prompt (below).

### If OAuth won't complete (likely gotcha)

Some Anthropic-hosted connectors don't support local OAuth from Claude Code — the identity provider only accepts the redirect URL that **claude.ai** registered. If `/mcp` login errors:

1. Add the connector at **claude.ai → Settings → Connectors** (or `claude.ai/customize/connectors`) and authenticate there.
2. Log into Claude Code with the **same Claude.ai account** (`/login`, confirm with `/status`).
3. It then appears in Claude Code automatically (`/mcp`).

On Team/Enterprise, only an admin can add connectors there (relevant for a government org).

### The design handoff prompt

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/4e8eaa4d-2834-4b9c-a667-c01eebd63fa1?file=ACMP+Navigation+%26+IA.dc.html

Implement: the ENTIRE ACMP project — all 11 design files (app shell, design system, navigation & IA,
dashboards & reports, backlog & topic, agenda & meeting, decision/voting & ADR, traceability & dependencies,
lists & registers, administration, research & knowledge), not just the open file. Treat the ACMP planning
package as the authoritative source of truth for behavior, data model, permissions, workflows, and phasing —
read /CLAUDE.md, /docs/README.md, and /execution-handoff/initial-prompt.md first and follow its phase order;
the design defines the exact UI/visuals only. Stack: .NET 8 / ASP.NET Core / EF Core / SQL Server + React 18
/ TS / Vite, modular monolith, Keycloak OIDC. Bilingual EN/AR with full RTL, light/dark, WCAG 2.2 AA. The
committee-lead role is "Secretary of the Committee" / "أمين سر اللجنة"; canonical role token is Secretary. Do
not start from the prototype's internal role keys — use the planning package's canonical identifiers.
```

---

## Stage 5 — Drive the build

Work top-to-bottom through `execution-handoff/phase-prompts.md` (P1–P19). Each phase self-enforces: unit + integration tests, the relevant `AC-###` (`docs/40`), authorization (`docs/10`) + audit events, no hardcoded strings (EN+AR) + RTL, progress-log + acceptance-audit updates, conventional commits. Confirm each phase before the next. If a step would break a guardrail (`execution-handoff/agent-guardrails.md`), the agent is instructed to **stop and ask**.

---

## Quick reference — key files

| File | Role |
| --- | --- |
| `/CLAUDE.md` | Standing brief — read first, every session |
| `/docs/README.md` | Canonical reference: roles, modules, entities, IDs, statuses, settled decisions |
| `/execution-handoff/initial-prompt.md` | First message to Claude Code |
| `/execution-handoff/phase-prompts.md` | Build slices P1–P19 |
| `/execution-handoff/agent-guardrails.md` | Binding constraints |
| `/execution-handoff/claude-code-execution-package.md` | What to build / what's resolved vs open / how to track progress |
| `/docs/40-acceptance-criteria.md` | `AC-###` acceptance criteria |
| `/docs/36-roadmap.md` | Phases + exit criteria |
| `/docs/10-permission-role-matrix.md` | Role × action × ABAC scope |
| `/design-handoff/` | Claude Design input package + the 10 design prompts |
| `/VERIFICATION.md` | Planning-package verification record |

---

## Non-negotiables (carried from the package)

- Self-contained (CON-001): app-owned Hangfire on ACMP's SQL, self-hosted Seq + MinIO, **self-hosted Keycloak (ACMP-owned realm) + SQL Server bundled (ADR-0015)**, in-app notifications v1 — **no** dependency on org Hangfire/ELK/Seq/notification platform; **zero external runtime services** (Webex Phase 2 is the only external dependency).
- Audit immutability + hash-chain; votes/decisions/ADRs/published-minutes are superseded, never edited.
- Always-attributed voting with quorum + abstentions + chairman approval + conflict-of-interest recusal.
- Self-hosted 