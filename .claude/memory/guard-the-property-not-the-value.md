---
name: guard-the-property-not-the-value
description: "A regression guard must assert agreement with the source of truth, not the absence of the stale value — and it must be run RED before you trust it green."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 4515f496-f3b7-46a8-8285-244b75d6b513
  modified: 2026-08-11T03:55:06.955Z
---

When adding a check that stops a fixed defect from returning, assert the **property**
(this doc agrees with the definition site) — never the **stale value** (this doc does
not say `$60`).

Day 3 (2026-08-11) added `scripts/check-runbook-drift.mjs`. The obvious version was a
grep asserting zero occurrences of `$60`. It parses `BUDGET_LIMIT_USD` out of
`deploy/aws/_common.sh` and compares instead.

**Why:** an assert-zero-`$60` check goes **permanently green the next time the budget
moves**, while the docs quietly rot at the new stale figure. It would pass for the wrong
reason forever, and no gate can see that. Third instance of this shape here, after
AV-117's count-based budget test and `contrast.test.ts` grading the light palette as
dark for its whole life. See [[baselines-as-numbers-not-properties]] and
[[substring-checks-bind-to-prose]].

**How to apply:**

1. **Find the definition site and parse it.** If there isn't one, the check is guessing.
2. **Exempt by PATH, never by phrase.** `_common.sh` legitimately says "the **old** $60
   budget" — that is amendment history at the definition site. Exempting it via the
   string `"old $60 budget"` would rebind the check to prose and break on any rewording.
3. **Match structure, not substring.** First draft flagged 6 legitimate cost estimates
   that merely mention the budget (`~$76/mo and blows the budget`). Tightened to the two
   shapes that actually assert a ceiling: `$N budget` and `budget is/of $N`.
4. **Expect the check to trip its own documentation.** The warning "Not
   `deploy/scripts/promote.sh`" was flagged by the rule banning that path. Fix: match the
   **invocable path**, so prose can name the script to warn about it while any
   copy-pasteable command is still caught.
5. **Write the guard BEFORE the fix and run it RED.** It found the sites mechanically —
   **five** stale budget sites where the approved plan listed four, plus one file claiming
   a "120%" budget action that does not exist. Then re-prove both rules fire against an
   injected violation and delete it. A gate that cannot go red is this project's
   recurring bug.

**Also:** fixing the number is not always fixing the defect. Raising the budget 60 → 100
invalidated the surrounding *reasoning* — "never leave two instances running" now
contradicts a design where prod is always-on and UAT starts for a session. No guard can
catch that; only reading can. See [[read-before-calling-it-a-defect]].
