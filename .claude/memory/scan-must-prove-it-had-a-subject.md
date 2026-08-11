---
name: scan-must-prove-it-had-a-subject
description: "A search that finds nothing must also report how much it searched — \"zero matches\" and \"the query examined nothing\" print identically."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 4515f496-f3b7-46a8-8285-244b75d6b513
  modified: 2026-08-11T08:31:36.377Z
---

When a clean result is the evidence, the check must emit **how much it actually
examined**, not only that it found nothing.

2026-08-11, verifying the `DEC-032` Arabic rename against production data: the scan
reported zero matching columns. That output is **indistinguishable** from a scan whose
schema filter matched no columns at all. Adding `SELECT COUNT(*) … columns_scanned` turned
it into admissible evidence — **351 columns across 83 tables**, zero matches.

**Why:** this is the same false-green family that has already cost this project twice —
`AV-117`'s count-based budget test, and `contrast.test.ts` grading the light palette as
dark for its entire life. See [[baselines-as-numbers-not-properties]],
[[substring-checks-bind-to-prose]] and [[guard-the-property-not-the-value]]. An absence
claim also needs an untruncated search ([[absence-claims-need-untruncated-search]]).

**How to apply:**

1. **Emit the denominator.** Rows scanned, files scanned, columns scanned. A zero
   numerator is only meaningful beside a non-zero denominator.
2. **Search the whole term family, not the literal you remember.** The first scan looked
   only for `الهندسة`; `DEC-032` renamed a family, so `الثابت المعماري` would have passed
   a check that was one step from being recorded as `Met`. Search the **root** (`معمار`).
3. **Widen to every store the change cannot reach.** Both `Acmp` **and** `keycloak` — group
   and realm display names are typed by humans and no code rename touches them.
4. **Qualify the schema.** Every ACMP module owns one (`membership.streams`); an
   unqualified name returns *Invalid object name*, not an empty result — which at least
   fails loudly. A wrong *filter* fails silently, which is the dangerous case.
5. **State what the clean result does NOT cover.** `streams` was empty, so its half was
   vacuously true; the real evidence was the sweep. If a stream-creation UI ever ships,
   the verdict does not extend to it.

**Mechanics:** build Arabic literals from `NCHAR(...)` code points so nothing depends on
JSON/SSM/console encoding, and return text as UTF-16LE hex rather than trusting rendered
glyphs. Wrap the container query in `sh -c '...'` or `$(cat /run/secrets/…)` is evaluated
on the **host** and fails as a misleading *Login failed for user sa*.
