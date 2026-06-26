---
artifact: design-parity-ledger
status: active
version: v1
updated: 2026-06-26
---

# ACMP Design-Parity Ledger

Per-component divergence between the implementation and the Claude Design "ACMP product context"
reference. Run via `tools/parity` (see its README). Each row is classified on two axes:

- **Visual**: `FIX` (match exactly) · `MATCH`
- **Behaviour**: `KEEP-GATED` (match look, restrict behaviour) · `INTENDED-DATA` (prototype demo
  data not copied) · `MATCH`

Scope = built surfaces only (Design System · Logo · Sign In · ACMP shell · Navigation & IA ·
Administration). Other design files become targets as P5+ lands.

---

## Topbar role control (Navigation & IA — `toggleRoleMenu`) — POC, fixed 2026-06-26

Reference: 210px bordered trigger (avatar + 2-line + chevron) → 280px `role="menu"` with a
"read-only from Keycloak" header and 8 `menuitemradio` rows (avatar + label + accent check).
Implementation before: `DevRoleSwitcher` = a 34px `.chip-btn` wrapping a **native `<select>`**.

| Element | Reference (markup) | Impl before | Impl after | Visual | Behaviour |
|---|---|---|---|---|---|
| trigger block-size | 40px | 34px | 40px | FIX→MATCH | — |
| trigger border | `--border-strong` | `--border` | `--border-strong` | FIX→MATCH | — |
| trigger radius | 9px | `--r-md` (8px) | `--control-radius` (9px) | FIX→MATCH | — |
| trigger min-inline-size | 210px | — | 210px | FIX→MATCH | — |
| trigger content | avatar + 2-line + chevron | "Role" + native select | avatar + 2-line + chevron | FIX→MATCH | — |
| open menu | custom 280px radio menu | native OS `<select>` popup | custom 280px radio menu | FIX→MATCH | — |
| menu header | "Roles are read-only from Keycloak" | — | same (EN+AR) | FIX→MATCH | — |
| items | `menuitemradio` + avatar + accent check | OS options | `menuitemradio` + avatar + check | FIX→MATCH | — |
| a11y | menu / menuitemradio / aria-checked | native select | menu / menuitemradio / aria-checked | FIX→MATCH | — |
| role switching | prototype switches | dev switches | dev switches | — | **KEEP-GATED** (dev-only; absent in prod, guardrail 4) |
| per-role sub + avatar colours | "Committee lead · power user" etc., role-tinted | — | trigger sub = "Preview role (dev only)"; primary avatars | — | **INTENDED-DATA** (prototype demo strings/colours not copied) |
| person identity ("Khalid A.") | demo person | "Dev User" | "Dev User" | — | **INTENDED-DATA** |

**Verdict:** all visual rows FIX→MATCH; behaviour stays dev-gated; prototype demo data intentionally
not copied. Verified: build/test/lint/i18n green + live screenshot (open menu, EN-light & AR-dark).
