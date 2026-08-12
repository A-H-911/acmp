---
name: verify-mechanically-not-carefully
description: "When a tool replaces full rows or rewrites files, do not rely on careful typing — build a field-level diff against a known baseline and run it after every batch."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 0facad2f-a83f-45b0-a92d-7db892b90d5c
  modified: 2026-08-12T17:24:17.041Z
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

**Why line endings mattered:** at the time, `/acmp/uat/env` was CRLF and the deployed
stack worked with it (`tag_of` in the bootstrap does `tr -d '\r'`, so the author knew).
Normalising them would have been an untested change to a working deployment riding
along with an unrelated one. Preserve the format you found; don't "improve" it in passing.

⚠ **CORRECTED 2026-08-12 — the CRLF half of that is no longer true, and believing it
would make you reintroduce CRLF.** `aws ssm get-parameter-history --name /acmp/uat/env`
shows **v9 (2026-08-09 20:59) onward are LF** (`CR=0`); only versions at or before the
note's own writing were CRLF. So the *deployed* format is LF and has been for days.
The rule survives, the fact did not — which is the whole reason to re-measure the format
you are "preserving" instead of quoting a note about it. It cost one `get-parameter-history`
call to find out.

⚠ **And measure it with something that cannot silently no-op.** `grep -c $'\r'` lost its
quoting in one shell layer, degraded to `grep -c ''`, and returned the **line count** for
every file — reporting CRLF for `gen-secrets.sh`, which CI runs green every day. **The tell
was that the measurement indicted known-good code.** `tr -cd '\r' | wc -c` cannot degrade
that way. A measurement that convicts something already proven innocent is measuring itself.

Related: [[baselines-as-numbers-not-properties]], [[tamheed-data-repair]],
[[absence-claims-need-untruncated-search]].
