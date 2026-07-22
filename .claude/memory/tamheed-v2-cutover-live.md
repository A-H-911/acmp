---
name: tamheed-v2-cutover-live
description: "LIVE 2026-07-22 — ACMP planning record = Tamheed v2 store at tamheed-package/ (plugin 2.2.0); docs/ is a frozen archive; track progress via the tamheed MCP tools, never markdown edits"
metadata: 
  node_type: memory
  type: project
  originSessionId: 174359d8-3eae-475b-bb38-1f59599e103a
  modified: 2026-07-22T02:26:19.444Z
---

**The Tamheed v2 cutover is LIVE (2026-07-22, plugin 2.2.0, branch `feat/tamheed-v2-migration`).**
`tamheed-package/` (canonical JSONL under `data/`, 25 tables) is the system of record; the old
Keystone v1 tree `docs/` carries a freeze banner and is read-only history.

**Migration facts (verified, not narrated):**
- Fidelity clean: `identifier_gaps: []`; count_deltas = the five stale-manifest ones only
  (OQ 48→58, DEC 27→28, ADR 23→33, AC 66→74, WBS 20→155 — disk wins, manifest was frozen 2026-07-06).
- `gate_run` 7/7 pass, `ready: true`; audit split **62 Met / 11 Partial / 1 Pending** (73 evidenced /
  1 narrated — the Pending AC), byte-identical to the v1 acceptance audit.
- Operator status_map: `Instrumented`/`Instrumented (P12)`/`Met`/`Met (ADR-0009)` → **Implemented**
  (5 KPI rows), `Living` → **Approved** (DOC-052). Patch: PH-0..3 `lifecycle_status` → **Approved**
  (roadmap phase table has no Status column → silent Draft default otherwise). Semantic defaults
  accepted: Resolved→Implemented, Open/Monitoring/active→Approved, Closed→Obsolete.
- Only unmapped: `adrs/README.md` (no ADR id) — preserved as narrative `other`.

**How to work now:**
- All package writes via the `tamheed` MCP tools: `audit_record` (verdict + evidence ref),
  `progress_update`, `work_bind`, `entity_upsert`; verdict/state via `gate_run`, `entity_query`
  (returns `total` — default limit 100 truncates big families), `trace_query`. Refresh the human
  surface with `export_html` → `tamheed-package/review.html`.
- **Never hand-edit `tamheed-package/` files.** `data/.lock` is transient (gitignored, released on
  `package_close`). No root `.mcp.json` exists or is needed — the plugin registers the server
  (2.2.0 detects plugin-hosted installs and skips emitting it).
- Kickoff/task prompts: `handoff/prm-00{1,2,3}-*.md` + `tamheed-package/prompts/` (orient-resume,
  progress-sync, integrity-check, generate-report, slice-review).
- CI: `tamheed-package/**` added to paths-ignore in ci/e2e/security workflows — JSONL write-backs
  don't trigger CI. The old markdown tracking protocol (acceptance-audit.md / progress-log.md /
  status-report.md edits + keystone validator) is **dead** — do not follow instincts/memories that
  demand it.
- Old-protocol memories ([[p19-release-readiness]] etc.) remain valid for *product* facts (what
  shipped, gotchas) but their *tracking instructions* are superseded by this note.
