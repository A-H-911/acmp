---
name: git-push-hang-fix
description: "Git Credential Manager hangs on `git push` in this repo; `gh auth setup-git` fixes it permanently."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: f79e14d2-046a-4f4d-a818-420d4c0e3381
  modified: 2026-08-07T14:07:12.489Z
---

`git push` used to hang indefinitely (Git Credential Manager waiting on a GUI prompt that
never surfaces in this session). Running **`gh auth setup-git`** once switches the credential
helper to the `gh` CLI and pushes work normally from then on — confirmed 2026-08-07.

**Why:** the `gh` CLI already holds a valid token; GCM does not, and blocks rather than failing.

**How to apply:** if a push hangs, run `gh auth setup-git` and retry rather than asking the
operator to run `! git push`. Wrap the first retry in `timeout 90` so a regression fails fast
instead of blocking the session. See [[ph5-aws-deployment]].
