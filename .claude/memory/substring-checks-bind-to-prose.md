---
name: substring-checks-bind-to-prose
description: "A check that locates its subject by substring can silently bind to prose ABOUT the subject — three instances in one day, each green for the wrong reason."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 0facad2f-a83f-45b0-a92d-7db892b90d5c
  modified: 2026-08-10T12:06:07.212Z
---

**Match on structure, not on substring.** Three instances on 2026-08-10, each of which passed —
or failed — for a reason unrelated to what it claimed to measure:

1. **`contrast.test.ts` graded the wrong palette for the file's entire life.** `block()` found rules
   with `css.indexOf('[data-theme="dark"]')`, and `tokens.css:4` documents that selector **in its own
   header comment**. indexOf matched the prose, took the next `{` — which opens `:root` — and returned
   the **light** palette. So `DARK = LIGHT ∪ LIGHT`, and the dark half of a WCAG AA gate compared
   light against light. Invisible because light passes: *a mis-pointed gate and a working one are the
   same colour* (DEF-035). Fixed by matching `selector + \s* + {` and **throwing** when absent.
2. **`seed-users.sh` turned an expired admin token into three different fake data failures** — "role
   did not stick", a false "no UPDATE_PASSWORD pending", then HTTP 401. Every check grepped a
   response **body**, so auth failure impersonated data failure. **When a check reads a body rather
   than a status, an auth failure looks like a data failure.**
3. **`smoke.sh`'s DNS check reported the resolver's IP as the host's** — `nslookup | awk '/^Address: /'`
   prints the DNS *server* first. It said `103.86.96.100` for a host that is `52.23.105.56`, and
   printed OK.

Related counting trap: **`git grep -c` counts LINES, not occurrences** (`ar.json:2` holds two on one
line — produced a 15-vs-16 disagreement). For completeness gates **assert ZERO**, never a count.

Same family as [[baselines-as-numbers-not-properties]] and
[[absence-claims-need-untruncated-search]] — all are checks that look rigorous and measure the wrong
thing. See also [[controls-must-detect-and-tell]].
