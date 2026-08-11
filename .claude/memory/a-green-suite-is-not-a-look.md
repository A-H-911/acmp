---
name: a-green-suite-is-not-a-look
description: "Testing-library queries resolve perfectly against unstyled markup, so a fully passing suite can describe a visibly broken screen — render the real components in a browser."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 01449c66-99ef-4bad-b978-5afc8ccf49ef
  modified: 2026-08-11T19:59:29.730Z
---

`DEF-047`: the invite panel shipped with both labels flush against the card border, unstyled browser
inputs inline beside them, and its one primary action rendered as plain text (it used `.adm-back`,
the borderless back-*link* style). **Eight tests passed.** They query by role and label, and those
resolve just as well on unstyled markup as on a designed screen.

Found by writing a throwaway Vite entry that mounts the **real** components inside a
`QueryClientProvider` and screenshotting it. The same pass caught, *before* shipping, that
`.adm-detail-card { overflow: hidden }` would have clipped the new role dropdown in half.

**Why:** every mechanical gate in this repo — vitest, coverage, axe, `check-i18n` — is blind to
layout. Nothing reads pixels. A no-reference composition (`INV-014`, no matching `.dc.html`) has no
reference to diff against either, so the browser *is* the only check that exists.

**How to apply:** for any new or restyled screen, `npx vite --port <n>` + a `_vis.tsx` that renders
the real components with a stub `fetch`, visit `?lang=en` and `?lang=ar`, screenshot both, delete
the harness. Look specifically for: padding on cards that carry none of their own, popovers inside
`overflow: hidden`, icons in headings with no `gap`, and raw English in the Arabic render. Related:
[[exact-design-fidelity-visual-loop]], [[web-visual-verify-cache-busting]].
