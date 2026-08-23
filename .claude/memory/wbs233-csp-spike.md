---
name: wbs233-csp-spike
description: "The WBS-23.3/DW-038 PNG-export spike — the DOM-to-image technique is fine under the shipped CSP, but the pre-selected package is not, and registry metadata is what mis-picked it."
metadata: 
  node_type: memory
  type: project
  originSessionId: 8b74843f-a935-4e83-9b97-0d45ef38e4cb
  modified: 2026-08-21T13:57:36.737Z
---

Run 2026-08-21. The full record is `PE-578`; this is the durable shape.

## The answer

**The technique is not blocked. The package was.** DOM → `foreignObject` → data-URL SVG → canvas → PNG
runs clean under the shipped `style-src 'self'` (no `unsafe-inline`). A parent document's `style-src`
does **not** reach inside an SVG rendered as an `<img>`, `img-src 'self' data:` admits the data URL, and
the canvas is not tainted. **The "if it needs `unsafe-inline`, STOP and ask" condition never arose** —
`DW-022`'s recorded finding stands and the header ships untouched.

- ⛔ **`modern-screenshot` 4.7.0 throws on every card, both locales.** `embedWebFont` builds a scratch
  document with `implementation.createHTMLDocument()`, appends a `<style>` to it, and dereferences
  `.sheet` unguarded (`dist/index.mjs:1347-1350`, thrown at `:1381`). **The scratch document inherits the
  page's CSP**, so the sheet is null. `font: false` skips that path and every capture then succeeds — and
  **silently breaks the Arabic layout**: it omits `width`/`height` from the styles it copies, so the clone
  re-lays-out under the substituted font and the card title reflows into its subtitle. English is fine.
  **A crash traded for a one-locale visual defect is not an escape.**
- ✅ **`html-to-image` 1.11.13 works with fonts embedded and contributes zero CSP violations.** It inserts
  `@import` rules into the *real* stylesheet and appends its font `<style>` to the *detached clone*, so no
  CSP check ever fires. ⚠ It has a catch path that `insertRule`s into the app's **live** stylesheet when a
  sheet's `cssRules` read throws — inert today because every stylesheet here is same-origin, live the
  moment one is not.

## How it was proven, and the parts worth copying

- The CSP header was **extracted from `deploy/nginx/default.conf.template`** and envsubst-ed from
  `deploy/.env.example`, never re-typed ([[verify-mechanically-not-carefully]]). The **dev** topology was
  used deliberately: it is the stricter `frame-src` case, because in cloud `KEYCLOAK_ORIGIN` equals the
  site's own origin and in dev it does not.
- **Three negative controls gated the run** and all fired first: `setAttribute('style')` blocked
  (`style-src-attr`), an injected `<style>` blocked (`style-src-elem`), a CSSOM `setProperty` applied.
  Without them a clean capture only proves the server forgot the header — see
  [[a-green-control-can-be-blind]].
- Pixel assertions were **derived live from the rendered card**, not hardcoded: the exact zone colour read
  off the element via `getComputedStyle` found in quantity in the PNG, and a colour that cannot be present
  returning zero every time, so the searcher was proven able to say no ([[scan-must-prove-it-had-a-subject]]).
- The "fonts are embedded" claim used a **direct instrument**, not pixels: `getFontEmbedCSS` returns 48
  `@font-face` rules for en and 80 for ar, every `url()` a `data:` WOFF2. ⚠ Its first cut reported
  `dataUrlCount: 0` beside an 874,619-character string — incoherent on its face; the regex missed that the
  URLs are quoted. **A scanner you write can measure itself.**
- A pixel diff quantified what embedding changes — 1.60% of pixels en, 1.26% ar — with a **self-diff
  control at exactly 0.0000%**, so the diff can discriminate.

## Not claimed

Chromium only — **149 for the spike, 151 for the verification run, so the result reproduced across two
builds** — but no Firefox and no WebKit. No full-page multi-card export. No memory measurement of the
~1.63 MB ar font payload on the on-prem hardware. `AC-142` names all four exclusions in its own text.
(⚠ This section originally also said "no acceptance criterion was written", which was true of the SPIKE
and went stale the moment the build shipped in the same session. `AC-142` exists and is Met.)

## For the build session

Compute the font CSS **once** with `getFontEmbedCSS` and pass it via the `fontEmbedCSS` option, or every
capture re-embeds the whole payload. **Deliberately NOT done in the shipped build** — no measurement says
it hurts, and a cache would have to be keyed by locale (ar needs strictly more `@font-face` rules than
en). See [[check-before-building.md]] before starting.

## Shipped (2026-08-21)

PR #304 → `ada5fe2`. `AC-142` Met (`AV-220`), `WBS-23.3` Implemented, `DW-038` Done. Built on
`html-to-image` 1.11.13 per `DEC-069`.

- **The card's own controls had to be filtered out of the raster** — they sit *inside* the element being
  captured. Proven two independent ways: a unit test executing the extracted filter against the real DOM,
  and a browser run exporting the same card **with and without** the filter, where the unfiltered control
  shows both buttons in the picture. The negative control is the point — it proves the buttons are absent
  *because of* the filter, not because they never rendered.
- **`DEF-105` had to be cleared first.** `ADR-0022` clause 4 read *"Export = client-side CSV only in v1"*.
  `ADR-0044` supersedes **the clause only**; the ADR row stays Approved with `superseded_by` unset,
  because clauses 1/2/3/5 are load-bearing and `DEF-102` plus two source comments cite the id.
  ⚠ The operator's instruction said "supersede clause 4" — taking it literally would have superseded the
  ROW. **Put that correction back to them before applying it.**
- **`DW-075` came out of this:** `ToastProvider` is built, tested and mounted nowhere, so failure feedback
  here is inline in the card instead.
