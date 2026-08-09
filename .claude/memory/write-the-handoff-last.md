---
name: write-the-handoff-last
description: A handoff written from a snapshot goes stale the moment the snapshot moves — write it after the session's final verdict, or re-read it before pushing.
metadata:
  node_type: memory
  type: feedback
---

Write `handoff/RESUME-*.md` **last**, after the session's final package write. If it was written
earlier, re-read it before pushing and reconcile it against the actual final state.

**Why:** on 2026-08-09 I rewrote `RESUME-ph5-closeout.md` a few commits *before* AV-112 landed. It
went to origin claiming "Nine acceptance criteria are Met; three are Partial" and carrying a whole
section instructing the next session to go and close **AC-084 — which was already Met**. A handoff
that dispatches someone to redo finished work is worse than no handoff, and it is exactly the
package-vs-reality drift `AGENTS.md` forbids.

The same day, `RESUME-ph5-sl025.md` was found still opening with "Resume PH-5 on ACMP" and no
superseded marker, three days after being replaced. Two stale handoffs in one session.

**How to apply:**
- Sequence the close-out as: final verdicts → `gate_run` → `export_html` → **then** the handoff →
  commit everything together.
- When a handoff is superseded, **stamp the old file immediately** with a `⛔ SUPERSEDED — do NOT
  paste this` banner naming its replacement. Do not rely on the new file's "supersedes X" line —
  nobody reads the new file when they opened the old one.
- Quote live numbers (AC counts, gate totals, evidence counts) only from a query made *after* the
  last write, never from memory of earlier in the session.
- Related discipline: [[absence-claims-need-untruncated-search]] — both are cases of a claim
  outliving the evidence that justified it.
