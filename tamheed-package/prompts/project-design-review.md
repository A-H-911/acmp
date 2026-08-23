# Design review — ACMP `.dc.html` process

The project-specific half of a UI review. Invariant **INV-014** makes this a blocking check, not a
polish pass.

## Where the reference lives

Local `.dc.html` files under `ACMP product context/`. The **Usage Map** (`ACMP Usage Map.dc.html`) is
the authoritative per-screen index — start there to find which file covers the screen you changed.

⚠ **Read the `.dc.html` DIRECTLY with file tools — never through a design MCP server.** The file in
the repo is the reference; anything else is a second-hand summary, and building from summaries is how
this project drifted before (nearest-token substitution instead of the exact px value).

## What "matches" means

Confirm ALL of it, not the happy path:

- design tokens and **exact** values — use the literal px the mock uses, never the nearest token
- component anatomy and iconography
- **every state**: default, empty, loading, error, permission-denied
- full **right-to-left mirroring**, verified in a browser — not inferred from logical CSS properties
- **light and dark**
- copy in **both languages**
- contrast

## When there is no reference

Compose from the shared design system plus the IA spec, and **flag it explicitly as a no-reference
composition** in the code comment and the PR. Do not present it as fidelity.

⚠ A superseding decision (an ADR that overrides what the design shows) is legitimate — record it and
say so in the header comment, exactly as `RoleAssignmentPanel` and `StreamAssignmentPanel` do.

## The check that actually catches drift

A green test suite is not a look. Render the screen and compare it against the reference side by
side, EN and AR, light and dark, before calling it done. Property-level guards (asymmetric radii,
logical-property lint) catch what a screenshot diff misses; a screenshot catches what they cannot.
