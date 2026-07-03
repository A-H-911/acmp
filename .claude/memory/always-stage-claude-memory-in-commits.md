---
name: always-stage-claude-memory-in-commits
description: Every ACMP repo commit must stage .claude/memory/* (repo-tracked) alongside code + docs — never leave memory updates uncommitted.
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 53ee53f1-ecf4-4fd3-bb4f-a4e363abafce
---

The `.claude/memory/` directory is **tracked inside the acmp repo** (it is the harness memory path, symlinked/mirrored into the repo root), so memory edits show up as normal working-tree changes on `git status`.

**Rule (user, 2026-07-03):** every commit must include that session's `.claude/memory/*` updates — the P-series plan memory (e.g. [[p10-risks-deps-traceability-plan]]) and `MEMORY.md`.

**Why:** it keeps the durable planning/decision log versioned in lockstep with the code it describes; P10e forgot to stage it, and PR1-P10f left a one-line post-merge memory edit uncommitted on `main`. Uncommitted memory drifts from the branch it belongs to.

**How to apply:**
- Update the relevant memory file + `MEMORY.md` **before** committing (same step as progress-log + acceptance-audit — see the standing "update docs before commit" instinct).
- Stage everything together: `git add -A` (catches `.claude/memory/`), or explicitly `git add .claude/memory docs adr src tests`.
- If a memory edit was made after a merge (e.g. recording the squash hash), fold it into the next branch's first commit rather than leaving it loose on `main`.

Related: [[p10-risks-deps-traceability-plan]], [[ci-gates-run-locally-pre-push]].
