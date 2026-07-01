---
name: user-prefers-simple-english
description: "The operator is a non-native English speaker — write explanations in simple, plain English."
metadata: 
  node_type: memory
  type: user
  originSessionId: 3ed5b626-2110-45aa-a9d8-b997346483a7
---

The operator is not a native English speaker. Write explanations in **simple, plain English**: short
sentences, common words, no jargon unless needed (then define it). When asking the operator to decide
something, state each option plainly and give a clear recommendation they can just say yes/no to.

**Preferred decision format** (operator confirmed they understand this one): for each choice, give four
short labelled lines —
- **Design:** what the reference shows
- **My version / Now:** what the current build does
- **My question:** the one decision, phrased simply
- **I suggest:** the recommended option

Point at what's on screen ("the white DISCUSSION NOTES box") instead of class names. Avoid dense tables
of jargon; the four-line shape above is what worked.

**Why:** the operator twice said "I can not understand your questions, explain and simplify"; the
four-line Design/Now/Question/Suggest shape was the one they could act on ("1: yes, 2: ...").

**Always name the design file.** Whenever an explanation references "the design", also name the exact
`.dc.html` reference file (e.g. the Actions register/detail = `ACMP Lists & Registers.dc.html`). The
operator asked for this so they can open the right file to check. Never say "the design" bare.

**How to apply:** prefer plain wording over terse caveman fragments when the goal is the operator
understanding a choice. Code/commits stay normal. See [[exact-design-fidelity-visual-loop]].
