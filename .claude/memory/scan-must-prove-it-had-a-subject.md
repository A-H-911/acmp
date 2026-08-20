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

## 2026-08-19 — the strongest instance yet: a typechecker with zero inputs (`LL-007`, `DEF-091`)

`npx tsc --noEmit -p tsconfig.json` inside `src/Acmp.Web` **exits 0 while checking nothing.** That
file is solution-style — `"files": []` plus three `references` — so tsc resolves zero inputs and
succeeds. It blessed a tree carrying **13 type errors** that had been failing `npm run build` for
**ten commits** on `feat/sl-030-confidentiality`.

⚠ **`vitest` cannot cover for it.** It transpiles per file and never typechecks, so **1241 passing
tests** certified code that would not compile. Two green instruments, zero real coverage.

**Use `npm run build` (`tsc -b && vite build` — exactly what CI runs) or `-p tsconfig.app.json`.**

**How to prove any gate has a subject:** inject a deliberate fault, confirm the tool FAILS, remove it.
Thirty seconds. Doing that here is what separated "my change broke this" from "this was already
broken" — I also stashed and re-measured, getting 13 both with and without my changes.

⚠ **Honest about the discovery:** I did not catch this by applying the principle. vitest failed on a
change tsc had just approved, and I went looking. Had the two agreed, the false green would have
shipped inside a PR body asserting a typecheck that never ran.

Same family, same session: `grep -c $''` reported CRs on a pure-LF file — and a control file with
exactly one CR line reported **two**. The instrument was degraded to `grep -c ''`.

## 2026-08-20 — two instruments AGREEING is not corroboration when they share a blind spot (batch 14)

Verifying `NFR-027`'s *"URL never embedded in logs or notifications"* clause, three scanners in a row:

1. line-based `grep -rn "NotificationMessage("` → **13 sites, 7 files.**
2. a multi-line-aware C# argument parser → **the same 13 sites, the same 7 files.**

Two independent instruments, identical answer. That reads exactly like corroboration. Both were
**blind to the same two builder files**: `ActionNotifications.cs` and `RiskNotifications.cs` use C#
**target-typed `new(`**, where the type is inferred from the return type — so the string
`NotificationMessage(` never appears at the construction site at all.

3. The version that worked **inverted the question**: scan every builder *file* for absolute URLs,
   storage reachability and non-relative link helpers. It found **all 10 files, 19 link expressions**,
   and flagged a synthetic control carrying a signed S3 URL.

**The transferable bit:** when two instruments agree, ask whether they share a *mechanism* — both of
mine keyed on the type name at the call site. Agreement between two greps is one grep. Prefer an
instrument whose subject is the **file set** (countable, assertable) over one whose subject is a
**syntactic pattern** (silently narrowable).

⚠ Same session, the migration census for `NFR-050`: version one searched five EF operation names and
reported **1 hit and ZERO `AlterColumn` across 47 migrations**. Zero `AlterColumn` in a schema that
size is not a clean result, it is an **implausible** one — that implausibility, not a failure, is what
prompted widening to twelve operations plus raw `Sql(...)` bodies, which found a `DropIndex` the first
version had missed. **Interrogate a clean result that is *too* clean.**

⚠ And the inverse, same session: a scanner that *refuses* to report. The migration scanner asserts it
parsed an `Up()` in every file and fails otherwise, so a parse failure can never masquerade as clean —
the [[a-green-control-can-be-blind]] pattern written into the tool rather than remembered.
