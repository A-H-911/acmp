---
name: commit-package-writes-before-git-ops
description: "tamheed-package/data is git-TRACKED — `git reset --hard`/`checkout` destroy uncommitted package writes. Commit package writes before any branch operation."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: f79e14d2-046a-4f4d-a818-420d4c0e3381
  modified: 2026-08-08T10:00:04.963Z
---

`tamheed-package/data/*.jsonl` is **git-tracked** (29 files). Tamheed writes canonical state
there, not to a private store — so **`git reset --hard`, `git checkout` and `git stash` destroy
uncommitted package writes exactly as they destroy uncommitted source.**

**Why:** on 2026-08-07 I ran `git reset --hard origin/main` repeatedly while merging PRs, and
lost three package sessions' writes that way. I misdiagnosed it as Tamheed silently dropping
writes and reusing ids — **that was wrong and is retracted** (PE-181, `findings_10.md` §B).
Tamheed behaved correctly: on each re-open it loaded canonical state as it actually stood and
allocated the next free id from it.

**How to apply:** treat `package_close` like a file edit, not like a database commit — **`git add
tamheed-package && git commit` immediately after**, before any branch operation. Batch writes
into one package session where you can. Verifying against `tamheed-package/csv/` after a close is
still worth doing, but for this reason, not because the tool is unreliable.

**One real Tamheed defect** (reported upstream in `findings_10.md` §A, not patched): `_next_id`
sorts ids as TEXT, so past 999 rows it returns `PE-1000` forever and `progress_update` /
`audit_record` / `work_bind` fail permanently. Only the unbounded tables are affected. This
package is at PE-181. See [[ph5-sl025-uat-live]].
