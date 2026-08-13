# findings_15 — Tamheed v3.2.0 acceptance pass

**Verdict: all three findings_14 negatives are fixed, verified.** The carried-over curation is done and
the drift test finally ran. Two things below are negative, and one of them is a defect in *my* work
that this pass surfaced.

---

## ✅ Negative 1 fixed — the note self-updates, force-free

`handoff_emit` with **no force**: `written: ["CLAUDE.md"]`, `diverged: []` — no `tamheed:note` entry.
The note span rebuilt (2611 → 2713 chars) and now points at `prompts/README.md`.

**Byte-for-byte claim verified properly**: I snapshotted `CLAUDE.md` *before* the emit and compared
everything outside the markers — identical, `sha 573f904437c4f6ab` both sides. The five customised
stock prompts were untouched and still report as `diverged`.

## ✅ Negative 2 addressed — the per-file path is named

New warning: *"…to accept the current template for ONE file, delete it and re-emit; `force=True`
overwrites ALL diverged stock files"*. That is the escape hatch that was missing.

It still cannot distinguish operator customisation from a template update — and now **says so**
("your customisation or a template update (indistinguishable without history)"). Naming the limit is
the right resolution; inferring it would need history the tool does not have.

## ✅ Negative 3 fixed — indeterminate is a real status

Slice `defects-closed` → `status: "indeterminate"` with `discriminating: false` and *"0 of 63 defects
rows have `found_in` set"*. `ready` unchanged.

`risks-discharged` correctly stays a loud **`fail`** — it lists 21 real entities, so amber would be
wrong there. The distinction the tool draws (indeterminate = nothing to show *and* cannot measure;
fail = actual entities) is the right one.

## ✅ Curation done — `converted_prompts` and `restated_content` both empty

Extracted the project-specific halves into three purpose-named prompts and retired the converted files:

| New prompt | Carries |
|---|---|
| `project-deferred-work-cautions.md` | `DW-`/`D-` identity crosswalk; `DEC-028`'s indefinite `SL-014` deferral |
| `project-invariant-audit.md` | the load-bearing invariant subset; **the gates-don't-measure-fidelity caveat** |
| `project-design-review.md` | the `.dc.html` review process (INV-014) |

`prm-001/002/003` deleted — their generic halves are covered by the stock library, and prm-002's state
paragraphs were factually stale (it claimed *62 Met / 11 Partial / 1 Pending* against a live 87/6/0).
`prm-next.md`'s flagged tally was replaced with a **pointer to the live form** rather than a fresher
number that would rot identically. Verified `DOC-069` really is the repair-history document before
citing it.

## §4 — The drift verdict, with its caveat stated first

⚠ **This was a delegated agent, not a genuinely fresh Claude Code session.** I cannot start one from
inside a session. That distinction turns out to matter, so read the result with it in mind.

The task prompt said **nothing** about recording, packages, Tamheed, defects or progress. Pure
engineering: "the Streams tab copy is stale, make it show the real streams."

**What the note demonstrably reached.** Unprompted, the agent:

- cited **INV-014** and declared its work a *"partial-fidelity composition"* where the design reference
  had five columns and the endpoint could source one;
- cited **DEC-043** for the wildcard's distinguishable-without-colour treatment, and **ADR-0016** for
  the coverage gate;
- identified a new defect and said *"Worth a `DEF-`"*;
- closed with: *"**Package writes outstanding** — `progress_update` and `work_bind(0dff8ec, …)` for this
  change, plus the `DEF-` above."*

It named every obligation in the table, in the table's own vocabulary, having been told none of it.
**The note reaches a fresh context.**

**What it did not do: record.** It listed the obligations and then deferred them — *"I left those to
you rather than writing to the package unilaterally."* The table says *record BEFORE moving on*; it
knew, and chose not to.

⚠ **I do not think that is evidence the note fails.** Declining to write to a shared store is exactly
what a *delegated* agent should do — it cannot know whether its parent holds the lock, and it did not.
A primary session has no such reason to defer. So the honest reading is: **instruction transfer is
proven; autonomous discharge is not, and this instrument cannot prove it.** A real fresh session is
still the only way to close §6, and it is now the *only* thing standing between the obligations table
and a clean verdict.

## ⚠ Negative — `prompts/README.md` indexes the library, not the folder

Good guide overall: the situation→file table is the question an operator actually has, and it
correctly names the two powers that are always the operator's (scope changes, `force`).

Two gaps:

1. **Project prompts are invisible to a table-scanner.** The table lists only the 14 stock files. My
   three new `project-*.md` prompts do not appear anywhere a reader looks first, so the folder's most
   project-specific content is the least discoverable. A closing row — *"Anything else in this folder
   is a project prompt — read the folder"* — would fix it.
2. **No mention of the single-writer lock.** Prompts are described as paste-ready for a session, but
   two sessions pasting concurrently hit a lock the guide never warns about. Stale locks have blocked
   `package_open` on **every** plugin reload in this series.

## ⚠ Negative — `DEF-064`, and it is mine

The delegated agent found, while doing something else, that **seven `ar.json` values were mojibake** —
`admin.streams.*` and `admin.invite.streams*`, from PRs #264 and #265. **Both mine, both merged.** The
Arabic UI of the two stream panels I built rendered garbage.

Root cause is a technique error: I wrote the Arabic as `\uXXXX` escapes and materialised them with
`.encode().decode('unicode_escape')` — UTF-8 bytes reinterpreted as Latin-1. Identity for ASCII, so the
**English half of every identical edit was perfect**, which is exactly why it hid.

⚠ **The verification I ran could not have caught it.** After both PRs I checked i18n and reported
*"parity OK"* — true, and meaningless: `check-i18n.mjs` compares **key sets, not values**. A mojibake
value has the right key. This is the memory rule *"i18n parity ≠ completeness"* biting the person who
cited it earlier in the same session. No existing gate can see it — not `tsc`, not the unit tests, not
coverage.

**Fixed** in this pass: all seven rewritten as literal UTF-8, mojibake scan returns 0, `check-i18n.mjs`
exit 0 at 1868 keys.

One correction to the agent's report: it concluded the content was *unrecoverable* and needed
re-authoring by an Arabic reader. It was right that mechanical reversal fails and right to check git
history — but the intended strings were still recoverable from the authoring calls earlier in the same
session, so the repair restores what was meant. **A correct diagnosis of the corruption is not a
correct diagnosis of what is recoverable.**

**Worth building:** a value-level guard flagging any `ar.json` value containing Latin-1-supplement
characters (`Ø`, `Ù`, `Û`) or `U+FFFD`. ~5 lines beside `check-i18n.mjs`, and it would have caught this
at the introducing commit.

---

## Summary

| Step | Verdict |
|---|---|
| 1 Note self-update, force-free | ✅ verified byte-for-byte against a pre-emit snapshot |
| 2 Indeterminate ambers | ✅ slice amber; package `risks-discharged` correctly still a loud fail |
| 3 Curation + README | ✅ curated, both surfaces empty · ⚠ README indexes the library, not the folder |
| 4 Drift verdict | ⚠ **instruction transfer proven; autonomous discharge unproven** — instrument was a delegated agent, not a fresh session |
| 5 This file | filed (2 negatives, 1 of them `DEF-064`, mine) |
