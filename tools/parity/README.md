# Design-parity harness

Detect and drive out divergence between the implemented UI and the **Claude Design
"ACMP product context"** reference, component by component, state by state.

## The instrument (hybrid — see ADR-less CHANGE-002 rationale)

| Source | Gives | Used for |
|---|---|---|
| `.dc.html` markup (design MCP) | exact reference CSS *values* + every `sc-if` state | authoritative numbers |
| Live design canvas (`claude.ai/design/p/<id>`) | rendered pixels + interaction + Tweaks props (role/dir/theme) + Present mode | reference screenshots, "click every component" |
| Live app (`vite dev`) | `getComputedStyle` + pixels + scripted state-driving | the implementation under test |

The reference render is a **cross-origin iframe** (`<id>.claudeusercontent.com`) — screenshot- and
click-able, but not `getComputedStyle`-able. So exact values come from markup; rendered truth from
live pixels.

## Procedure (per component × state × {light,dark} × {en,ar})

1. **Reference**: open the surface in the design canvas, set props via the Tweaks panel
   (`defaultRole`, `defaultDir`), enter Present mode, drive to the state (OS-level click/hover),
   screenshot the component region.
2. **App**: `vite dev`; set role (`sessionStorage 'acmp-dev-roles'`), `localStorage i18nextLng`,
   theme; drive to the same state; screenshot the matched node region (same pixel size) and dump
   `getComputedStyle`.
3. **Diff**:
   - CSS delta = app computed-style vs reference markup values → property-level FIX list.
   - Pixel delta = `node diff.mjs ref.png app.png out.png` → mismatch % + diff image
     (crop both to equal size; mask font-metric + mock-data regions).
4. **Classify** each delta on two axes and log to `docs/progress/design-parity-ledger.md`:
   - **Visual**: `FIX` (match exactly) · `MATCH`
   - **Behaviour**: `KEEP-GATED` (match look, restrict behaviour — e.g. dev-only) ·
     `INTENDED-DATA` (real IDs/content, prototype demo data not copied) · `MATCH`
5. **Fix** FIX items in dependency order (tokens→shared→shell→nav→screens), re-diff, converge.

## Setup

```
cd tools/parity && npm install        # pixelmatch + pngjs
node diff.mjs ref.png app.png out.png  # both PNGs must be identical dimensions
```

## Scope

Only surfaces with **built** UI are reconcilable now: Design System, Logo, Sign In, ACMP shell,
Navigation & IA, Administration. The other design files (Backlog, Agenda, Decisions/Voting/ADR,
Traceability, Dashboards, Research, Lists) are parity targets as their phases (P5+) land — register
the surface and run the same loop.
