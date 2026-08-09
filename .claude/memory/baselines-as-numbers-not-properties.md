---
name: baselines-as-numbers-not-properties
description: A verification test that depends on a baseline must record it as a NUMBER AND TIMESTAMP, never as a property like "zero" or "empty" — properties get falsified by your own later work.
metadata:
  node_type: feedback
---

When a recorded test says "closes when metric X is non-zero **against its zero baseline**", it has
embedded an assumption — *that nothing else will ever write there*. Assumptions decay. Record
**"six as of 2026-08-09T14:05Z"** instead: that stays true no matter what happens next.

**Why:** AV-114 and AV-116 both said AC-085 closes when `NumberOfMessagesPublished` on
`acmp-budget-alerts` goes non-zero. Sound when written. Then fixing DEF-031 and DEF-032 required
publishing to that same topic — **six** messages, all mine. The metric was already non-zero, so the
recorded instruction would have reported the criterion **Met** when nothing had ever been delivered.
**A check that passes for the wrong reason**, manufactured while fixing two others.

**No gate can catch this.** Tamheed's gates check row-level integrity, not whether a verdict's stated
test still discriminates. It was caught by taking stock, not by any control.

**The root cause was a deliberate trade, not carelessness.** DEF-031 and DEF-032 each reused
`acmp-budget-alerts` rather than creating a topic, because it already carried a *confirmed* email
subscription and a new topic costs a confirmation click. The price: one topic now carries three
signal types, so its metrics can no longer attribute a publish to a source. **A cheap setup paid for
in verifiability** — the same currency the criterion was denominated in.

**How to apply:**
- Baseline = number + UTC timestamp. Never "zero", "empty", "none".
- Before reusing a shared channel (topic, log, table, metric) for a second purpose, ask what
  *verification* currently depends on that channel being single-purpose.
- Prefer a discriminator that cannot be polluted — here, the email's **sender and subject**
  (`budgets@costalerts.amazonaws.com` vs an alarm name vs `ACMP backup FAILED on …`).
- When you invalidate your own recorded test, fix the *record*, not just the situation.

Related: [[controls-must-detect-and-tell]], [[absence-claims-need-untruncated-search]].
