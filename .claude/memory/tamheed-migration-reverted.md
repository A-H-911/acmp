---
name: tamheed-migration-reverted
description: "SUPERSEDED HISTORY — the 2026-07-21 migration (PR #153) was reverted 2026-07-22, then RE-RUN the same day on plugin 2.2.0 with full cutover; see tamheed-v2-cutover-live for current state"
metadata: 
  node_type: memory
  type: project
  originSessionId: 174359d8-3eae-475b-bb38-1f59599e103a
  modified: 2026-07-22T02:25:53.399Z
---

**Timeline of the Tamheed cutover (all 2026-07):**
1. **07-21** PR #153 migrated `docs/` → `tamheed-package/` on plugin **2.1.0** + full cutover.
2. **07-22 (morning)** Operator ordered a full revert (merged as #154, tree restored to `efabddb`).
   The run itself had succeeded — the revert was an evaluation choice. Feedback for the Tamheed
   owner was written to `findings_2.md` (repo root, git-excluded).
3. **07-22 (later)** Operator ordered a fresh re-run on plugin **2.2.0** ("forget the memory and
   findings_2.md, start from scratch, Full cutover"). 2.2.0 had fixed the findings (status_coerced
   + title_fallbacks ledgers, semantic STATUS_MAP, id-column title exclusion, plugin-hosted
   `.mcp.json` skip, stale-reference scan). The re-run completed with full cutover.

**How to apply:** current state lives in [[tamheed-v2-cutover-live]] — `tamheed-package/` is the
system of record; `docs/` is a frozen archive. Do not trust #153's or #154's descriptions of the
tracking protocol; both are superseded.
