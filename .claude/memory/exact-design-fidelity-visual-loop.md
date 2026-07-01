---
name: exact-design-fidelity-visual-loop
description: "For ACMP UI, \"from <file>.dc.html\" means pixel-exact to that file, verified visually — not nearest-token, not pixels-deferred."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 938e157b-a88b-4935-9be1-9f5aa2058528
---

When a task says build a screen "from `ACMP <name>.dc.html`", the operator means **pixel-exact** to that source: tokens, padding/spacing, anatomy, states, icons, RTL, AA — the SAME as the file, unless there is no backend for it or it needs an ADR (e.g. meeting Type+Mode = add backend granted; agenda new-vs-link radio = omit). Do NOT defer fidelity ("behavior now, pixels later") and do NOT substitute the nearest design token for an exact px value.

**Why:** building from second-hand summary maps + nearest-token substitution drifted from the design (lifecycle banner used pad 12/16, radius 8, gap 12 vs the `.dc.html`'s exact 13/15, 11, 11; meeting workspace grid gap 16 vs the design's 18). The operator rejected it: "you did not follow the UI exactly … not tested visually before."

**How to apply:** (1) Read the EXACT `.dc.html` markup/values — never rely on a summary. Use literal px where the mock does. (2) Stand up a visual screenshot-compare loop: render the `.dc.html` reference and the live app side-by-side per surface × EN/AR × light/dark × tablet/desktop, diff, fix to exact, repeat. Visual verification is part of "done," not deferred to a later VR phase. (3) IA/nav/gating from the operator's answers is accepted; the bar is the visuals. See [[phase-prompt-standard-footer]].
