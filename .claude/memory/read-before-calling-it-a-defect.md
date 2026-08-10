---
name: read-before-calling-it-a-defect
description: Never call something a defect until you have read the implementation that produces it — six wrong calls in one session, none caught by any gate, four of which would have broken deliberate behaviour.
metadata:
  type: feedback
---

On 2026-08-10/11 I asserted a defect **six times** from a pattern-match — a symptom that *looked
like* a known failure family — without reading the code that produced it. All six were wrong.
**Not one was caught by a gate.** Every one was caught by opening the file. **Four of them would
have shipped a change that broke behaviour someone had chosen on purpose.**

| I claimed | Reality | What settled it |
|---|---|---|
| Health banner is "green because it isn't looking" | `SystemHealth.tsx:55` — *"Overall banner reflects only what is monitored"*, deliberate | reading the aggregate |
| Audit noise fires on "every route change" | `AuthProvider`'s `useRef` guard holds — once per app **mount** | reading the caller |
| `/wiki` is missing the `<h1>` every register has | the design puts it in a **breadcrumb**; the wiki is *designed* without a page title | reading the `.dc.html` |
| `/actions`: "design and domain disagree" | `SourceKey` = *"snapshot for the Linked column"* — they **agree**; only a picker was missing | reading the domain |
| MinIO tile is dead "on prod and UAT" | `check` is optional; the API registers **2** checks, so **4 of 6** tiles are unmonitored **everywhere** | reading the catalog + `Program.cs` |
| The committee "+" is "an ENABLED control" | it already had `disabled`, and `.adm-add:disabled` already dimmed it | reading the JSX + CSS |

**Why it kept happening.** This repo has a rich, well-documented defect history — `DEF-023` health
probes green on an unreachable box, `DEF-030` an armed alert with no delivery path, `DEF-031` an
alarm with no action. That history makes pattern-matching feel like expertise. It is not: the same
*symptom* (a green banner, an inert button, a missing header) is produced by deliberate design at
least as often as by a bug.

**The rule.** Before writing "this is a defect" — in a plan, a register row, a PR, or a message —
open the file that produces the behaviour and read it. Comments in this codebase are unusually
load-bearing: five of the six above were settled by a comment the author had already written,
sitting one line from the thing I was about to "fix".

**How to apply.**
- A finding is a **question** until you have read the implementation. Write it as one.
- Quote the line that proves it. If you cannot quote one, you have a hypothesis, not a defect.
- When the record and the code disagree, read **both** before deciding which is wrong — see
  [[verify-mechanically-not-carefully]].
- Correct the register when you were wrong, rather than silently "fixing" it. `DEF-039` and
  `DEF-040` carry `CORRECTED` prefixes for this reason; a register that keeps only the conclusions
  is not a record.
- Related shapes: [[substring-checks-bind-to-prose]] (a check bound to the wrong subject) and
  [[baselines-as-numbers-not-properties]] (a check that passes for the wrong reason). This one is
  the human version — **the reasoning** bound to the wrong subject.
