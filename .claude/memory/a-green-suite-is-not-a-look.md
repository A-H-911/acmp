---
name: a-green-suite-is-not-a-look
description: "Testing-library queries resolve perfectly against unstyled markup, so a fully passing suite can describe a visibly broken screen — render the real components in a browser."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 01449c66-99ef-4bad-b978-5afc8ccf49ef
  modified: 2026-08-12T14:00:14.802Z
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

## ⚠ THE HARNESS MUST IMPORT ONLY WHAT THE ROUTE IMPORTS (FR-159, 2026-08-12)

The harness itself lied the first time. It imported `styles/administration.css` because the component
used `.adm-*` classes — and **only `AdministrationPage.tsx` imports that file**, so the same markup
rendered from a *meetings* dialog would have shipped completely unstyled. The screenshot looked
perfect. `DEF-047` again, one PR later, in a different disguise.

Two rules fall out, and they are cheap:

1. **A shared component owns its stylesheet.** `InvitedCredential` now imports
   `invited-credential.css`; those rules were deleted from `administration.css` so there is nothing
   left to drift. Reusing another feature's classes is a runtime dependency the type system, the
   bundler and every test are all blind to.
2. **Grep before you borrow a class.** `grep -rn "<stylesheet>.css" src` tells you which routes load
   it. If the answer is not "the one I'm on", the styles are not there.

## The same shape in behaviour, not just CSS (FR-159, 2026-08-12)

`Dialog` re-ran its focus-trap effect whenever `onClose` changed identity — an inline arrow, so every
render — and the cleanup restores focus to the pre-dialog element. Typing `nadia@vendor.example` into
a dialog field stored **`n`**. Invisible for a year because every previous dialog was a confirmation
with no text input, and invisible to tests because `userEvent.type` in jsdom does not lose focus the
way a real browser does. Fixed **in `Dialog`** (read `onClose` through a ref, depend on `[open]`), not
at the call site: every future caller with a field would have hit it.
