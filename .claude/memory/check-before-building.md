---
name: check-before-building
description: "In this codebase a \"new\" feature is often already half-built — check the domain, the i18n keys and the .dc.html references before designing anything."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 4515f496-f3b7-46a8-8285-244b75d6b513
  modified: 2026-08-11T13:41:54.754Z
---

**Three times on 2026-08-11** I designed something the repo had already modelled. Each time,
reading first produced a smaller and better design than the one I had written down.

| I planned to build | It already existed |
|---|---|
| A separate "invite record" entity + table + migration (`ADR-0038`) | `MembershipStatus.Invited`, commented *"reserved for admin pre-registration ahead of first login"*, **and** `SyncFromClaims` already flipping such a record to Active on first login with the transition flagged for audit |
| An `invited` badge in the roster | `STATUS_TONE.Invited = 'info'` plus EN `"Invited"` and AR `"مدعو"` keys — the whole defect was a **backend filter**, and the SPA needed no change at all |
| `/session` "from scratch, content unspecified" | A complete bilingual `GUEST / PRESENTER SHELL` at `ACMP Navigation & IA.dc.html` 304–347 |

**Why it keeps happening.** The planning package and the code were built from the same brief, so
the code frequently anticipates a requirement the package still lists as unbuilt. An entity, an
enum value, an i18n key or a `.dc.html` block is often already sitting there.

**How to apply — before writing a design or an ADR:**

1. **Grep the domain enums and aggregates for the concept.** `MembershipStatus`, not just tables.
2. **Grep `i18n/locales/en.json` for the label.** If the string exists, the surface was designed.
3. **Grep `ACMP product context/*.dc.html` for the screen.** [[read-before-calling-it-a-defect]] is
   the same discipline pointed at defects; this is it pointed at features.
4. **Read the comments.** They are unusually load-bearing here and repeatedly state the intent
   ("reserved for…", "P4 produces Active/Disabled; the directory renders all three").

**When the code contradicts an Approved ADR, the code is often right.** `ADR-0038`'s objection to a
`CommitteeMember` row was reconciling an unknown `sub` — real for a blind pre-seed, absent when the
app itself creates the account and receives the id. Record the refinement as a `scope-change`
(`SC-003`) rather than either diverging silently or building the worse version out of deference to
the document. See [[verify-mechanically-not-carefully]].
