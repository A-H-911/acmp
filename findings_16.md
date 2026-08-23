# findings_16 — Tamheed v3.2.1 acceptance pass

**Steps 1 and 2: clean.** The per-file template-acceptance path works in the field, and both
documented additions render.

**Step 3 is filed here because the drift verdict is still not settled** — and the reason is
methodological, mine, not the tool's.

---

## ✅ 1 — the per-file path, first real field use

`prompts/README.md` appeared in `diverged` (6 stock prompts, up from 5) because its template moved
on. Taken exactly as documented: **deleted that one file, re-emitted** →
`emitted: ["prompts/README.md"]`, the current guide came back, the five customised prompts stayed
untouched and still report `diverged`, warning count 6 → 5.

That closes the loop findings_14 asked for and findings_15 could only confirm on paper.

## ✅ 2 — both additions render, and the lock section is faithful

Header carries `(tamheed v3.2.1)`. The closing row —
*"Something project-specific | any other `.md` here — project prompts are operator-authored,
purpose-named; read the folder"* — fixes findings_15's README negative: a table-scanner is now routed
to the three `project-*.md` prompts.

The **"One session at a time"** section encodes the findings_13 §1 method faithfully, both
discriminators: identity (*"plausibly IS an agent session"*) **and** ordering (*"a process younger
than the lock cannot hold it"*).

⚠ Wording nit, not a defect: *"Only when both say stale, delete"* is stricter than the logic requires
— **either** discriminator failing already proves staleness (a `conhost.exe` that started after the
lock cannot be the holder, whatever the other check says). It errs toward **not** deleting, which is
the correct direction for a destructive action, so this is a phrasing observation only.

## ⚠ 3 — the drift verdict: STILL OPEN, and my instrument confounded it

I ran a **genuinely fresh primary session** this time — `claude -p`, Claude Code v2.1.229, not a
delegated agent — on a small real task (the flaky agenda-publish race) whose prompt said nothing
about recording, packages, or Tamheed. I released the package lock first and captured a baseline so
any write would be provable by diff, not by self-report.

**Result: no package writes.** defects 64 → 64, progress 311 → 311, edges 1052 → 1052.

**But that null is not evidence of a choice, because I blocked the path.** I ran with
`--permission-mode acceptEdits` (deliberately, since project rules forbid
`--dangerously-skip-permissions`). That mode accepts *file edits only*. The session reported that
`git checkout -b`, `git switch -c`, bare `git branch` and `tsc --noEmit` were **all rejected by the
permission layer**. An MCP `entity_upsert` would almost certainly have been rejected the same way —
and it never attempted one, so I have no evidence about whether the tools were even reachable.

**I cannot distinguish "it chose not to record" from "it could not."** Reporting the null as a
behavioural verdict would be exactly the error this series keeps catching: a measurement that cannot
tell the two states apart.

### What the run *does* prove

**Instruction transfer, in a second independent context.** Unprompted, it wrote:

> *"CLAUDE.md's recording table says a found defect gets a `DEF-` row before the fix, plus
> `progress_update`/`work_bind` on the commit. Does a flaky-spec repair cross that bar for you, or is
> it below the line? Your call — I didn't write to the package either way."*

It read the table, paraphrased it correctly, and **surfaced the obligation as an explicit decision
rather than silently skipping it** — including the genuinely ambiguous question of whether a
flaky-test repair is a "defect" at all. It also declined to commit to `main` when it could not branch,
and said so.

So across two independent contexts the note transfers **completely**. What remains unproven is only
autonomous *discharge*, and no headless run can prove it without a permission posture I am not willing
to use.

### How §6 could actually be closed

An **interactive** fresh session (a human pasting the task into a normal Claude Code window, where MCP
calls prompt and can be approved) is now the only instrument left. Everything cheaper is confounded:
a delegated agent defers to its parent (findings_15), and a headless run cannot reach the tools.

## Bonus — the fresh session corrected one of my own rows

`DEF-061` records the racing call as a **PUT**. It is a **POST**:
`POST /meetings/{id}/agenda/items/{topicId}/presenter` (`meetings.ts:264`, `MeetingsEndpoints.cs:96`).

It also established two things my row did not:

- **The UI cannot help.** `AgendaBuilder.tsx:216` enables Publish on `items.length` alone, so
  Playwright's auto-wait-for-enabled offers no protection — the barrier must be the response.
- **Root-cause sweep: one site only.** `scenario.ts` sets the presenter inside the same
  `addAgendaItem` POST body, so its later publish is atomic; `dnd-and-failures.spec.ts` only asserts
  the disabled state.

The fix is preserved on `fix/e2e-agenda-publish-race` (`2e91b9d`), and `DEF-061` has been corrected.
⚠ That commit is **not type-checked or run** — the same permission layer blocked both — so it needs
verification before merge.

---

## Summary

| Step | Verdict |
|---|---|
| 1 Per-file template acceptance | ✅ works in the field, first real use |
| 2 Table row + lock section | ✅ both render; lock method faithful · ⚠ "both say stale" is stricter than needed |
| 3 Drift verdict | ⚠ **still open** — transfer proven twice; discharge unprovable by any instrument I can run |
| Bonus | `DEF-061` corrected (PUT → POST) by the fresh session |
