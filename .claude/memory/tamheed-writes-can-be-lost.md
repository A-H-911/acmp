---
name: tamheed-writes-can-be-lost
description: "Tamheed package writes can silently vanish and reuse ids — always verify against tamheed-package/csv/ before believing a tool's return value."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: f79e14d2-046a-4f4d-a818-420d4c0e3381
  modified: 2026-08-07T18:02:05.206Z
---

**Tamheed MCP writes are not trustworthy on their own.** On 2026-08-07, two `package_open` →
write → `package_close` cycles reported `ok: true` for every call and then **lost everything**:
two defect rows, two audit verdicts and a progress entry were absent from
`tamheed-package/csv/` afterwards.

Worse, the id allocator re-issued ids it had already handed out (`PE-178` three times, `AV-103`
twice), so a later write **silently overwrote an earlier narrative**. Nothing errored.

**Why:** the allocator derives the next id from canonical state at `package_open`. If a prior
session's writes never reached canonical storage, the next session reuses those ids and
clobbers whatever did land. Likely related to [[tamheed-stale-lock-pid-reuse]].

**How to apply:** after `export_html` + `package_close`, **re-read `tamheed-package/csv/`
directly** (`grep -c "^DEF-026," tamheed-package/csv/defects.csv`) and confirm every row you
wrote is present with the value you wrote. `ok: true` and a returned id prove nothing. Prefer
one session with all writes batched over several small sessions — each open/close is an
opportunity to lose the lot. See [[ph5-sl025-uat-live]].
