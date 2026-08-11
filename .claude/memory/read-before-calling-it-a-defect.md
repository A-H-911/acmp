---
name: read-before-calling-it-a-defect
description: "Never call something a defect until you have read the implementation that produces it — seven wrong calls in one session, none caught by any gate, and the seventh reached the package before anyone noticed."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 4515f496-f3b7-46a8-8285-244b75d6b513
  modified: 2026-08-11T08:47:03.311Z
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
| **⚠ #7** `/session` is "a route **no navigation links to**", with no design reference | `navModel.ts` sets `ACCESS.session = { guest: 'full' }`, and FR-024 **hides an area's nav item from any role without access** — nav *does* link it, for Guests. A full bilingual `GUEST / PRESENTER SHELL` exists at `ACMP Navigation & IA.dc.html` **304–347** | reading `navModel.ts` + the IA design |

⚠ **#7 is the one that got through.** The first six were caught before shipping. This one reached
**two package rows and a plan file**, and needed a supersession (`DEC-037`) to repair — `DEC-036`
stays unedited, because `AGENTS.md` makes Approved rows final and supersession, not editing, is the
sanctioned fix. **The missed trigger is specific and reusable: in an app that hides navigation BY
ROLE BY DESIGN, "nothing links to it" can never be established by walking the app as one role.**
Before any "nothing references X" claim, grep the routing/nav *model*, not the rendered UI — and see
[[absence-claims-need-untruncated-search]], the same error in its search-tool form.

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
