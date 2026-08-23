---
name: controls-must-detect-and-tell
description: For every control ask THREE things separately — can it detect, can it tell, and does its subject ever occur. Five instances in this project; the "tell" half is always the one asserted in a comment rather than tested.
metadata:
  node_type: memory
  type: project
---

**A control has three independent failure modes, and only the first ever gets tested.**

1. **Can it DETECT?** — the logic works.
2. **Can it TELL?** — there is a live path from detection to a human.
3. **Does its SUBJECT ever occur?** — the thing it watches actually happens.

**Five instances in ACMP, all found the hard way (2026-08-07 → 09):**

| | control | what was broken |
|---|---|---|
| DEF-023 | health probes | green on a box nobody could log into |
| DEF-030 | budget notifications | state changed correctly; **every publish was denied** — the SNS topic never granted `budgets.amazonaws.com` |
| DEF-031 | CPU-credit alarm | `AlarmActions: []` — created that way, fired into the void |
| DEF-032 | backup failure | `crontab.example` and `backup.sh` both *asserted* alerting; cron redirects to a log and AL2023 has no MTA |
| OQ-068 | backup absence | failure alerting is blind to a backup that never runs — box stopped when idle |

**The pattern in the "tell" cases is identical: the missing half was asserted in a comment
that made it look verified.** "cron alerts on the non-zero exit", "is where an alert hooks in".
Nobody lies; everybody assumes. DEF-030 was found only because AWS emailed the account contact.

**How to apply:**
- Every alarm: check `AlarmActions` is non-empty. An alarm with no action is a dashboard widget.
- Every SNS topic a *service* publishes to: the default policy (`Principal {"AWS":"*"}` +
  `AWS:SourceOwner`) covers IAM principals only. Service principals need an explicit statement.
- Every "we alert on X" comment: find the code that sends. If there isn't any, the comment is the bug.
- **Force the transition and watch delivery** — `aws cloudwatch set-alarm-state` does this on demand.
  Confirm from the *topic's* metrics, not from the sender's own success line.
- Before trusting an alert-on-failure, ask when the thing last *ran*. See [[silent-controls-need-forced-tests]].
