---
name: verify-mechanically-not-carefully
description: "When a tool replaces full rows or rewrites files, do not rely on careful typing — build a field-level diff against a known baseline and run it after every batch."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 0facad2f-a83f-45b0-a92d-7db892b90d5c
  modified: 2026-08-09T18:04:42.699Z
---

`entity_upsert` **replaces** full rows (NOT NULL rejects partial rows outright — it
refuses rather than damages, verified 2026-08-09 on AC-077). So every status change
means retyping long statement text, and a transcription slip is silent data damage of
the kind the v2.3 migration caused.

**Do not solve this by being careful. Solve it with a diff.** `tamheed-package/data/*.jsonl`
is flushed on **every write** (not only on `package_close`), so **git HEAD is a live
baseline**. `scratchpad/pkgdiff.py` reports every changed field per entity id; run it
after each batch and require that only the intended field moved. It caught nothing
across 8 slices + 12 ACs — which is the point: it converts "I think I typed it right"
into evidence.

**Two traps it also caught:**
- **Console encoding corrupts what you copy.** Plain `python -c` on Windows writes
  cp1252 and turns `—` / `→` into `?`. Always `PYTHONIOENCODING=utf-8`. Transcribing
  from mangled output would have silently rewritten requirement text.
- **`sed -i` under MSYS rewrote all 36 CRLF line endings** as a side effect of a
  two-line edit to the SSM env payload. Worse, my own "only the tag lines changed"
  check gave a **false pass** — a check that passed for the wrong reason, while
  checking for exactly that. Use a byte-level script (`scratchpad/repin.py`) that
  rebuilds the original from the edit and asserts byte equality.

**Why line endings mattered:** `/acmp/uat/env` is CRLF and the deployed stack works
with it (`tag_of` in the bootstrap does `tr -d '\r'`, so the author knew). Normalising
them would have been an untested change to a working deployment riding along with an
unrelated one. Preserve the format you found; don't "improve" it in passing.

Related: [[baselines-as-numbers-not-properties]], [[tamheed-data-repair]],
[[absence-claims-need-untruncated-search]].
