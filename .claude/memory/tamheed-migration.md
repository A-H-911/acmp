---
name: tamheed-migration
description: docs/ (Keystone v1) migrated to Tamheed v2 store at tamheed-package/ — full cutover 2026-07-21; docs/ FROZEN; all package writes via tamheed MCP tools
metadata:
  type: project
---

**Cutover 2026-07-21:** the Keystone v1 package `docs/` was migrated into the **Tamheed v2 relational store at `tamheed-package/`** (staged `package_migrate`, fidelity clean, **gates 7/7 `ready:true`**) and the operator ordered **full cutover** in the same run. `docs/` is now **FROZEN read-only reference** (banner in docs/README.md); AGENTS.md v2.0.0 + CLAUDE.md rewired.

**Why:** one system of record — the relational store enforces referential gates at write time (FKs/CHECKs), so registers can't drift the way markdown tables did.

**How to apply:**
- **All package reads/writes = `tamheed` MCP tools only** — `entity_query`/`trace_query`/`gate_run` to read; `audit_record` (AC verdicts + evidence), `progress_update`, `work_bind` (commit→FR/AC/SL), `entity_upsert` to write. NEVER hand-edit `tamheed-package/` or the frozen `docs/`.
- Phase-gate tracking = audit_record + progress_update + work_bind, then `gate_run` + `export_html`, then **commit the regenerated `tamheed-package/` (JSONL + review.html)** — replaces the old edit-4-markdown-registers protocol.
- `package_open` takes a single-writer lock (`data/.lock`, gitignored); **always `package_close`** (flushes canonical JSONL + releases). Stale lock after a crash = delete deliberately.
- Human review surface = `tamheed-package/review.html` (deterministic; commit it). The old validate_package.py flow is DEAD for this repo.
- Migration facts: 74 ACs + 74 AV verdicts (62/11/1 preserved), 33 ADRs, 58 OQs, 28 DECs, 23 DW (D-nn→DW-NNN), 218 reqs, 155 WBS, 1032 edges; 15-row patch fixed v1 statuses (`Resolved`/`Open`/`Monitoring`→Approved, `Closed`→Obsolete) + phase titles; originals in `custom_attributes.v1`.
- CI: `tamheed-package/**` + `handoff/**` added to paths-ignore in ci/e2e/security workflows (JSONL churn must not trigger builds). Root `/.mcp.json` is gitignored (machine-specific handoff_emit artifact).

See [[p19-release-readiness]] for the release state at freeze time.
