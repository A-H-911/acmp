# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`.**

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else.

```
server_info()                                # expect tamheed 4.4.2, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                                   # 7/7 is the NORM again (tamheed >= 4.4.2). A red gate is a
                                             # REAL finding - read its failure list, it names the token.
readiness_check("package")                   # ⚠ ASK IT. Do NOT expect either answer - this line said
                                             # "EXPECT ready:FALSE - DELIBERATELY" for days and that
                                             # became false when DEC-097 d1 closed DEF-108 (the
                                             # FORTY-THIRD). Advisories failing is NORMAL and is not a
                                             # task (see §1); a BLOCKING failure names its row and is
                                             # a real finding. ⛔ Never "fix" a blocking failure by
                                             # softening a defect's severity or converting it - that
                                             # was offered on DEF-108 twice and declined both times,
                                             # and its eventual close was an evidenced disposition,
                                             # not a re-grading.
git status --porcelain -uall                 # expect a CLEAN TREE
git rev-parse --abbrev-ref HEAD              # ⚠ ASK IT, never assume — read the conditional below
gh pr list --state open                      # ⚠ NO `--limit` (`PE-599`: a cap at ten is what hid the two
                                             # PRs that became `DW-080`). A Dependabot queue MOVES, so
                                             # any count of it in prose is stale by construction.
gh run list --branch main --limit 5          # ⚠ poll `status` to `completed` BEFORE reading `conclusion`
                                             # (trap 23). ⚠ For a `pull_request` event GitHub evaluates
                                             # paths-ignore against the WHOLE PR diff, so on an open PR
                                             # even a package-only push re-runs everything and CANCELS
                                             # the in-flight run (trap 38). Newest sha wins.
                                             # ⛔⛔ A MERGE-COMMIT RUN IS ITS OWN RUN AND CAN DISAGREE
                                             # WITH THE PR RUN OVER BYTE-IDENTICAL CODE - twice here
                                             # now (LL-036, pinned). `gh pr checks` shows only the PR
                                             # one. This command is what found `main` red on
                                             # c273294c after #331 merged "with ten checks green".
                                             # ⭐ Cite the RUN ID in anything you record, never a
                                             # colour: an id cannot be attributed to the wrong tree.
docker info                                  # ⚠ ASK IT — do not expect either answer. The daemon is
                                             # frequently DOWN here, and while it is,
                                             # `Acmp.Integration.Tests` cannot run: the ONLY place
                                             # FREETEXT executes and the only real SQL Server. The
                                             # operator started it on 2026-08-28, and a daemon that was
                                             # up in one session says nothing about this one.
                                             # ⚠ It gates the coverage GATE, not every coverage
                                             # question — see the THIRTY-FOURTH before concluding that
                                             # something "needs Docker".
```

⚠⚠ **WHICH BRANCH YOU ARE ON IS THE FIRST THING TO ESTABLISH, AND THIS FILE DELIBERATELY DOES NOT SAY.**
It said *"expect clean; you are on `main`, everything is merged"* for a long time, and it was true every
time until `DW-080` phase A left a branch open across a session boundary — the TWENTY-THIRD's shape, the
kickoff block describing an old world. **The command above is the answer; a sentence here is not.**
⛔ **Do not replace it with a new snapshot** — a statement of where the work sits has a half-life of one
merge, and writing one is what produced the THIRTY-SECOND.

**THE DURABLE FACTS, WHICH ARE NOT A WORKING STATE.** `DW-080` **phase A is MERGED** — PR `#320`, squashed
to `df8d7c3a` on 2026-08-28, all ten checks green, verified into `origin/main` **by content** rather than
by ancestry (trap 25). The solution targets `net10.0`, `SearchProvidersFtsTests` passes, and `#128`/`#134`
were **closed as superseded** by it, on the `#135`→`#308` precedent.
**`DW-080` PHASE B IS ALSO MERGED — PR `#325` → `1d7cb04b` on 2026-08-29**, ten checks green including the
e2e leg. The api and worker run on `aspnet:10.0-noble-chiseled-extra`.
**`SL-033`'s BUILD IS FINISHED: `WBS-24.8` MERGED as PR `#323` → `24738d4b` on 2026-08-29**, ten checks
green, verified into `origin/main` by content. Every item `WBS-24.1`–`WBS-24.8` has now been built.
**`SL-034` EXISTS** (`DEC-089` d2) and holds `WBS-25.1` (phase B, merged) and `WBS-25.2` (`DW-079`).
**`SL-035` EXISTS** (`DEC-094` d5 / `SC-038`, 2026-08-29) and holds `WBS-26.1`–`WBS-26.5`, the six rows
that interview activated. ⚠ **Every one of those six activations OVERRODE the agent's recommendation to
carry** — the rows record it as an override, so do not read the activation as agreement about HOW. And
`NFR-064` was created there because **no requirement covered segregation of duties at all**.
⚠ **The two open Dependabot PRs `#318`/`#319` ARE explained — `DEC-083` d3 left them alone deliberately**,
so seeing them is not the unexplained state the warning below is about. A Dependabot queue moves; run the
command rather than trusting this sentence's pair of numbers.
**2026-08-30 MERGED THREE MORE PRs, ALL SQUASHED TO `main`, EACH WITH TEN CHECKS GREEN *ON ITS PR RUN*:** `#329` → `472b84d1`
(`scripts/gen-record-slate.mjs`, the general record-slate generator — the three earlier generators are each
welded to one register, so a cross-register interview had none), `#330` → `42e948b8` (SoD-4 as
warn-and-audit over topic owner AND agenda presenter, plus `NFR-064`'s evidence for all five SoD rules,
which upgraded SoD-2's audit assertion from a mock to a ROW), and `#331` → `c273294c` (C-INS-01's two
anomaly signals, including `Topics.RestrictedTopicAccessed` — read-auditing that did not exist at all).
⚠ `#330`'s second CI run went red on `DEF-108` and was re-run by `DEC-097` d2 AFTER the disposition; see
the annotation on `DEC-077` d3.
**2026-08-31 MERGED TWO MORE, EACH TEN CHECKS GREEN ON BOTH ITS PR RUN AND ITS MERGE-COMMIT RUN — CITED BY
RUN ID, WHICH IS THE POINT** (`LL-036`): `#332` → `4529fd01` (`DEF-122`'s fix — the two C-API-03 rate-limit
tests now issue their requests CONCURRENTLY and assert the COUNT of throttled responses, so a fixed window
cannot roll mid-sequence), and `#333` → `05f2bbd5` (`WBS-26.3` — `ci.yml`'s `compose` job now really BUILDS
`api`, `web` and `worker` and asserts size ≤ 500 MB plus TWO base assertions; main CI run `33392572509`).
**`NFR-054` reached `Implemented` for the first time** and `DW-090` is `Done`.
**2026-09-01 MERGED FOUR MORE.** `#334` → `99ef8453` (`DEF-124` — two assertions pinning `AC-157`'s
placement clause, each proven to fail against a mutant); `#335` → `906d8fb3` (`DEF-125`, the object-store
probe, **and** `DEF-126`, the calendar `target-size` floor — merge-commit run `33458419757`, all four jobs
success); `#336` → `487bc7ea` (the a11y route sweep, 18 routes newly covered); `#337` → `b515fc64`
(`WBS-26.4` — `check-container-health.sh` and its ten forced cases). `ADR-0045` is `Approved`;
`LL-041`–`LL-044` are `Approved`; `SC-043` moved `NFR-005` and `NFR-038` to `Deferred`.
⭐⭐ **THE a11y SWEEP NOW COVERS EVERY STATIC AUTHENTICATED ROUTE, NOT THE FIVE IT HAPPENED TO HAVE** —
`#336`, on the operator's instruction after `DEF-126`. **The gap was structural, not an oversight:**
`DW-071`'s trigger fires *"whenever a NEW route ships"*, so every route that shipped **before that trigger
was written** was outside it by construction — which is how `/meetings` had no accessibility test at all.
⚠ **THE AUDIT'S RESULT IS WORTH ITS NUMBER**: 18 routes newly swept in both locales, **96 passed / 4
failed**, and the four are two sweep tests × two browser projects — **exactly ONE offending route**,
`/notifications`. Everything else was clean, including `/admin/users` behind its own administrator login.
⭐ **One of its two elements violated only for SHORT messages** (`.notif-row-msg`: one line ≈ 18 px, a
wrapped one already cleared 24) — **a conditional failure a sweep over real data finds and a reading of
the stylesheet does not.** ⛔ **Parameterised routes are still unswept** (`topics/:key`, `meetings/:key`
and its six children, and ten more): they need seeded entities and a key each, and the spec says so in its
own comment rather than letting the list read as complete.
⛔⛔ **AND `#334`'s MERGE-COMMIT RUN WENT RED WHILE ITS PR RUN WAS GREEN — `LL-036`'s THIRD INSTANCE HERE,
ON A DIFFERENT TEST AGAIN.** PR head `75a34334` passed CI `33449435775`; the squash-merge `99ef8453` failed
CI `33450264169` on `MinioFileStoreTests`, with `git diff … -- src/ tests/` **EMPTY**. **That is `DEF-125`,
not `DEF-121`** — different test, assembly and signature. ⭐ Unlike `DEF-108` and `DEF-121` its cause WAS
diagnosed, in the SDK's source: `Minio.MinioClient.ParseErrorNoContent` dereferences `response.Exception`
with no null check at tags **6.0.5 AND 7.0.0**, so a body-less 5xx null-derefs where a 404 never reaches
the line. **Upgrading does not fix it.**
⛔⛔ **AND `#331`'s TEN GREEN CHECKS WERE ITS *PR* RUN. ITS MERGE-COMMIT RUN ON `main` WENT RED, AND THIS
FILE SAID OTHERWISE FOR A WHOLE SESSION — THE FORTY-FOURTH's FIRST MEMBER.** PR head `6e8d4b96` passed CI
`33297195353` at 06:43:15Z; the squash-merge `c273294c` failed CI `33297623382` at 06:45:13Z on
`SearchProvidersFtsTests`, with `git diff … -- src/ tests/` **EMPTY** between them. Filed as `DEF-121`.
⭐⭐ **A PR-HEAD RUN AND A MERGE-COMMIT RUN ARE DIFFERENT RUNS OVER IDENTICAL CODE AND `gh pr checks` SHOWS
ONLY THE FIRST** — now `LL-036`, Approved and pinned. **Cite the run id, never a colour**: a run id cannot
be attributed to the wrong tree. The same conflation put a false clause inside a `Met` verdict (`AV-235`,
corrected by appending `AV-236`), which no gate can see because a verdict's evidence is free text and
nothing compares it to the runs it names.
⚠ **That is a repository fact and NOT a slice status** — a merged row still owes the operator a verdict,
and which rows are at `Review` is deliberately not written here. ⛔ **DO NOT NAME A SLICE ID IN THIS
PARAGRAPH.** Two sentences here named `SL-033`, and that was the FORTY-SECOND: a command with an id in it
rots the moment a later slice opens, and pointing a fresh session at a FINISHED slice is worse than
pointing at none. **`entity_query("slice", status="Approved")` names the live slice or slices without
naming one**, and `readiness_check(scope="slice", id=…)` then takes the id it gives you.
⛔ **THAT LAST SENTENCE NAMES THE ITEM AND NOT ITS POSITION, DELIBERATELY.** It used to read *"phase B …
is §6 item 1 now"*, which `DEC-085` d3 falsified an hour later — **the THIRTY-SIXTH**. A list is
renumbered exactly when work completes, which is the event that makes someone read this file, so a
pointer at a slot is wrong precisely when it is read. **Never write `item N` here; name the row.**
⚠ **Everything above is a repository fact; none of it is a status.** For what is actually open, resolve the
live slice first (`entity_query("slice", status="Approved")`) and read ITS readiness — **not a slice this
file names.** ⚠⚠ **MEASURED, because this is the FORTY-SECOND's sharpest half:** the sentence that stood
here sent you to `SL-033`, whose `wbs-done` **passes with zero entities**, so a fresh session would have
concluded nothing was open while five `WBS-26.x` items sat in another slice. **A pointer at a finished
slice does not fail loudly — it returns a clean answer about the wrong subject** (`LL-015`).
⚠ **If a branch or an open PR exists that this paragraph does not explain, stop and ask the operator** —
that is a state nothing here describes.

⚠ **Never assume CI has seen your tree** — `git rev-list --left-right --count @{u}...HEAD`, right-hand
number. ⛔ **Do not write into this file what you have or have not pushed** (the THIRTY-SECOND).

⚠ **WHAT THE COUNTER COUNTS, so it stays meaningful:** a statement is tallied when it was true, became
false, and **reached a commit** — where a fresh session could have read it. Wording caught and fixed
before it was committed is not counted; nor is annotating a historical record whose outcome later
happened. Otherwise the number would drift into a log of every edit and stop meaning anything, which is
the failure it exists to warn about.

⚠ **Do not trust any tally written into a prompt, including this one.** Read the live numbers. This
file has carried a stale statement **forty-six** times, and **fourteen** wrong assertions have escaped into
commit messages, which cannot be amended. **SEVERAL were written and then invalidated within
the SAME session** by the very work that session was doing — do not read the count above as anything but a
reason to distrust every number here, including that one: on 2026-08-19 it said
`readiness ready:TRUE` and `gate 7/7` hours after `DEF-093` made both false; its requirement tally
went stale the moment batch 13 recorded two verdicts; and on 2026-08-20 §1's *"`assumptions-current`
reports `indeterminate` because 0 of 17 rows carry a `validation_date`"* was made false by the
disposition session that was reading it. A prompt that restates a number is a prompt that
will lie to you. **Point at the live check, do not quote it.** If you find this section wrong again,
**fix it in the same session and bump this count.**

⚠⚠⚠ **THE FORTY-SIXTH IS THE FORTY-SECOND'S OWN FIX APPLIED TO TWO SITES AND MISSED AT NINE — AND EVERY
ONE OF THE NINE WAS A COMMAND A FRESH SESSION WOULD HAVE RUN.** The FORTY-SECOND found two sentences in the
kickoff block pointing at `SL-033` for *what is actually open*, measured that **`SL-033`'s `wbs-done` passes
with zero entities**, and fixed both. **It did not sweep §6.** Nine further sites carried the literal command
``readiness_check(scope="slice", id="SL-033")`` — at lines 395, 1459, 1588, 1614, 1636, 1651, 1767, 2096 and
2203 — each introduced as *\"the live answer\"*, *\"the answer\"*, or *\"names every row still open\"*.
`SL-033` has been `Implemented` since 2026-08-30. **Every one of them hands a fresh session a clean verdict
about a finished slice**, which is the failure mode the FORTY-SECOND had just finished measuring.
⭐⭐ **THIS IS THE TWENTY-THIRD'S LESSON AT SCALE: A CORRECTION APPLIED TO SOME ARTIFACTS AND NOT THEIR
SIBLINGS LEAVES THE SURVIVORS AS THE ONES THE NEXT SESSION READS** — and here the survivors outnumbered the
fixed copies four to one. ⚠ **The fix went where the error was REPORTED, not where the pattern LIVED.** The
FORTY-SECOND was found by reading the kickoff block, so the kickoff block is what got swept.
⛔ **All nine are now the no-slice form** — `entity_query("slice", status="Approved")` then
`readiness_check` on the id it returns. **Never write a slice id into a command in this file**; the id is
what rots, and the query that resolves it never does.
⚠ **Found by a full read at session close, not by any instrument.** `count-prompt-ids.py` resolves ids and
statuses and would report `SL-033: Implemented` — **correctly**, because the id is real and its status is
right. Nothing mechanical here can see that a CORRECT id inside a CORRECT command points at the wrong
SUBJECT. ⚠ **Counted once, family of nine, on the FIFTEENTH's and TWENTY-EIGHTH's precedent**, and counted
separately from the FORTY-SECOND because these survived that pass and rode into every commit since.
⚠ **Not an escape** — no commit message carries the command.

⚠⚠⚠ **THE FORTY-FIFTH IS A LIVE INSTRUCTION TO BUILD WORK THAT A SCOPE CHANGE HAD ALREADY WITHDRAWN — AND
IT SAT IN THE NUMBERED LIST, THE ONE PLACE THIS FILE SAYS THE NEXT ACTION LIVES.** The list read *"⚠
`WBS-26.4`'s `service_healthy` switch is the risky one: `/readyz` reaches SQL Server, Hangfire and object
storage, so strict ordering can turn a slow cold boot into a failed stack. Prove the failure mode both
ways."* Every clause of that is TRUE about the risk and it is now the wrong thing to do: `SC-042`
re-scoped the item because `DW-091`'s premise was withdrawn, and **a fresh session obeying the sentence
would have reversed an explicit operator decision** — the one `DEF-079` records, where `service_healthy`
was declared, its consequence measured, put back to the operator, and dropped by their choice.
⭐⭐ **THE SHAPE, AND IT IS NOT THE FORTY-FOURTH'S: A SENTENCE CAN BE FACTUALLY TRUE AND STILL BE A STALE
INSTRUCTION.** Nothing in it was ever false — the risk is real, the mechanism is real, `/readyz` really
does reach three subsystems. What changed is that the ACTION it recommends was withdrawn. **A staleness
checker that asks *is this claim still true?* returns YES and passes it.** ⭐ **The only safe form for an
item's deliverable is the ITEM'S OWN ROW plus its `SC-`** — deleted here rather than refreshed, which is
the EIGHTH fix's standing remedy.
⚠⚠ **IT IS ALSO THE THIRTY-SIXTH'S FAMILY IN A NEW COSTUME.** That one was a pointer at a list POSITION;
this is a pointer at an item's CONTENT. Both rot for the same reason — **the numbered list is rewritten
exactly when work completes, and a description of an item ages against the item.** Name the row; never
summarise it here.
⚠ **NOT AN ESCAPE**: the phrase was grepped across the log and no commit message asserts it. ⚠ **Counted
once.** ⭐ **What found it: sweeping for the identifiers this session moved and then reading what the hits
CONCLUDE rather than what they assert** — the FORTY-THIRD's own remedy, on its third consecutive payday.
⛔ **AND THE THREE INSTRUMENTS WERE CLEAN AGAIN.** `count-prompt-ids.py` resolves ids and statuses; every id
here was correct. The prose-status checker wants a `(Status)` form; this is an imperative. The
`— your verdict` / ``is at `Review` `` greps are scoped to a different fault class. **Four consecutive
findings that no instrument this file owns can see.**

⚠⚠⚠ **THE FORTY-FOURTH IS A CI COLOUR ATTRIBUTED TO THE WRONG RUN, AND IT LEFT `main` RED AND UNRECORDED
FOR A WHOLE SESSION.** This file said the 2026-08-30 merges landed *"with ten checks green"*. True of each
PR run; **false of `#331`'s merge-commit run**, which failed on `SearchProvidersFtsTests` two minutes after
the byte-identical PR head passed. A fresh session pasting the kickoff block would have read `main` as
green while `gh run list` said otherwise — and the previous session, having written exactly that sentence,
made three package commits without ever running it (`DEC-077` d2's rule failing on its first test after
`DEF-108`'s hold lifted). Filed as `DEF-121`, now carrying `DEC-089`'s end-condition structure via
`DEC-103` d1.
⭐⭐ **THE TRANSFERABLE POINT IS NOW A PINNED LESSON, `LL-036`: A PR-HEAD RUN AND A MERGE-COMMIT RUN ARE
DIFFERENT RUNS OVER IDENTICAL CODE, AND `gh pr checks` SHOWS ONLY THE FIRST.** *CI was green* is not a fact
until you say WHICH RUN. **Cite the run id** — a run id cannot be attributed to the wrong tree, a colour
can. On this repository the two runs have now disagreed twice (`DEF-108` occurrence 4, and `DEF-121`).
⚠⚠ **IT IS A FAMILY OF THREE UNDER ONE ORDINAL, on the FIFTEENTH's and TWENTY-EIGHTH's precedent — one
fault class, one pass.** (a) the kickoff block's *"ten checks green"*; (b) **`AV-235`'s EVIDENCE FIELD**,
which asserted *"CI on the merged tree: frontend, backend and compose all success, plus E2E and Security"*
— the backend job on that tree FAILED and E2E does not run on `push` at all, so no E2E result for it
exists; corrected by appending `AV-236`, verdict unchanged because the criterion's four backing tests are
in `Acmp.Api.Tests`, which passed 423 of 423 on that same failing run; and (c) **§6's `DW-090` block**,
*"NO AC WAS WRITTEN AND THAT IS DELIBERATE … no such check exists"*, false in every clause once `WBS-26.3`
built the check.
⚠⚠⚠ **THE `AV-235` MEMBER IS THE ONE WITH NO INSTRUMENT AT ALL, AND THAT IS WHY IT MATTERS MOST.** A
verdict's evidence is FREE TEXT and **nothing in the store compares an evidence claim to the runs it
names** — `G-PROGRESS`, `acs-met`, `count-prompt-ids.py` and the prose-status checker all pass over it,
because every identifier resolved and the verdict itself was correct. **A false fact inside a `Met` verdict
is invisible by construction.** The only defence is the citation rule above.
⚠ **THE FOURTEENTH ESCAPE IS SEPARATE AND IS MINE, FOUND BY THIS SWEEP:** I wrote that `ci.yml`'s `publish`
job was gated off *"because it reported `skipped`, so the variable is unset"* — into `DW-090`, `DEC-102`
d2, `PE-725` and **three commit messages** (`c4905fb8`, `8fbb3182`, `c615c3f2`) plus PR `#333`'s body.
Measured with a control: `AWS_ROLE_ARN` has been set since **2026-08-04** and `publish` runs on every push
to `main`; it was skipped that once because `backend` FAILED and it `needs:` it. **`skipped` conflates *the
`if:` was false* with *a needed job did not succeed*.** `PE-731` is the correction of record; `LL-039`
generalises it. ⛔ **It does NOT reopen `DEC-102` d2** — `compose` runs on every PR and `publish` does not,
and a push-to-main-only variant was explicitly declined because a red would then arrive AFTER merge.
⭐ **WHAT CAUGHT IT: `publish` CHANGED COLOUR.** Nothing mechanical could have — the false half was a CAUSE
attached to a true observation (`LL-020`).
⚠ **A SECOND, DIFFERENT CLASS RODE ALONG IN THE SAME PASS AND IS FOLDED IN RATHER THAN NUMBERED SEPARATELY**
— the FORTY-THIRD's own precedent, and `LL-016`'s rule that an ordinal is the thing no check can see, so do
not multiply them. §6's `DEC-077` d3 block read *"IT WAS OVERRIDDEN EXACTLY ONCE"*; `DEC-100` d2 made it
twice within a day. **A count inside a rule block, where the rule survives and the number rots** — replaced
with the register rather than with a fresher figure, which is the EIGHTH fix's standing remedy.
⭐⭐ **WHAT THIS PASS PROVES ABOUT THE SWEEP ITSELF: THREE OF THE FOUR MEMBERS WERE FOUND BY *READING*, NOT
BY GREPPING.** `count-prompt-ids.py` ran clean (347 of 348; the one unresolved is `DEF-082`, which
`DEF-101` already records). The `— your verdict` and ``is at `Review` `` greps returned only historical
quotes. The `item [0-9]` grep returned nine hits, all inside the error log. **Every instrument this file
owns said it was clean, and it was carrying four false statements** — three about what FOLLOWS from a run's
colour, one a count in a rule. `LL-009` inside this file's own tooling, for the fourth time.

⚠⚠⚠ **THE FORTY-THIRD IS A ROW CLOSING AND TAKING NINE STATEMENTS WITH IT — NONE OF WHICH NAMED ITS
STATUS.** `DEC-097` d1 closed `DEF-108` as environmental, and everything that had REASONED FROM its being
open went false at once. The kickoff block's *"⚠ EXPECT ready:FALSE - DELIBERATELY"* is the dangerous one:
a fresh session pasting it would read the package's true, evidenced `ready:TRUE` as a fault and go looking
for something to repair — or, worse, suspect the status had been manufactured. Also: *"`DEF-108` is
`Open`/high and holds `readiness_check` at `ready:FALSE`"*, *"`SL-033` IS DELIBERATELY NOT CLOSED"*,
*"the one rule still failing is `defects-closed`"*, *"clause three was offered to the operator and NOT
taken"* (it was taken), *"it stays `Open`/high with readiness deliberately `FALSE`"*, `SL-035`'s *"five open
items"*, *"Two `deferred-work` rows remain `Activated`"* (five), and the `START HERE` heading's own
*"READINESS IS `FALSE`, BOTH ON PURPOSE"*.
⭐⭐ **THE TRANSFERABLE POINT, AND IT IS NOT THE FORTY-SECOND'S:** that one was about claims describing what
REMAINS. **These describe what FOLLOWS FROM a row's state, and not one of them mentions the row's status,**
so neither the prose-status checker nor `count-prompt-ids.py` could ever see them — every id resolved and no
`(Status)` form appeared anywhere. **When a row is dispositioned, grep for its IDENTIFIER and then read what
the hits CONCLUDE, not what they assert.** A status is a column; a consequence is a sentence.
⚠ **A SECOND, OLDER CLASS RODE ALONG AND IS FOLDED IN RATHER THAN NUMBERED SEPARATELY** (`LL-016`: an
ordinal is what no check can see, so do not multiply them): §1's candidate-rule answer and §6's `NFR-018`
bullet, both of which the FORTY-SECOND had already corrected once, went false AGAIN when `NFR-064` and
`NFR-065` reached `Implemented`. ⭐ **A SENTENCE THAT HAS BEEN WRONG IN BOTH DIRECTIONS IS NOT A SENTENCE TO
REFRESH — BOTH ARE NOW DELETED IN FAVOUR OF THE COMMAND**, which is the EIGHTH fix's rule finally applied to
them rather than a third answer being written.
⚠ **Counted once, family of nine, on the FIFTEENTH's and TWENTY-EIGHTH's precedent** — one fault class,
one pass, one ordinal. ⚠ **THE THIRTEENTH ESCAPE IS SEPARATE AND IS A COMMIT MESSAGE:** `3ed9440b` says the
Api suite went to **421**; it is **419**. That figure was never measured — after adding one test I re-ran
only a three-test filter and wrote the total from belief — and the same line contradicted itself, claiming
both *"418 → 421"* and *"(+4, +1 presenter)"*. `PE-711` records it; the PR body was corrected before merge,
the commit message cannot be.

⚠⚠⚠ **THE FORTY-SECOND IS WHAT THE NINTH FIX'S FULL READ FOUND AFTER FOUR TARGETED SWEEPS IN ONE SESSION
HAD ALL RUN CLEAN — NINE SITES, EIGHT DISTINCT CLAIMS, ONE PASS.** The FORTIETH's entry closed by saying
it was *"its own best argument for the NINTH fix's stronger standard — read the WHOLE file before handing
it on, rather than the parts you think you touched."* This is that argument being tested, and the standard
paying: **every one of these survived the phrase grep, the touched-identifier sweep, `count-prompt-ids.py`
and the prose-status checker.**
- **Two in the KICKOFF BLOCK**, both naming `SL-033` as the slice to read for *"what is actually open"*.
  ⚠⚠ **MEASURED, AND THIS IS THE SHARPEST HALF: `SL-033`'s `wbs-done` PASSES WITH ZERO ENTITIES.** A fresh
  session obeying it would have concluded nothing was open while five `WBS-26.x` items sat in `SL-035`.
  **A pointer at a finished slice does not fail loudly — it returns a clean answer about the wrong
  subject** (`LL-015`). ⭐ **The fix is to name NO slice: `entity_query("slice", status="Approved")`.**
- **Two POSITIONAL pointers**, the THIRTY-SIXTH's own fault class surviving in the file that documents it:
  §6's *"context, not the next action (that is above)"* — it is 150 lines BELOW — and `SL-032`'s heading
  *"the live slice is the block ABOVE this one"*, where the block above is the `DEC-075` interview and the
  live slice has changed three times since.
- **A tally-and-position claim**, *"Two are now done and three are numbered below"*: measured, `DW-078`,
  `DW-079`, `DW-080`, `DW-082`, `DW-084` and `DW-085` are **all `Done`** and none is numbered anywhere.
- **§1's requirement-programme END CONDITION**, and it is the most consequential: *"expect TWO, both
  externally blocked … that is the operator's stated end condition, and it is met."* `DEC-094` d6 created
  `NFR-064` — `Approved`, Must, non-functional, **not externally blocked**, scheduled as `WBS-26.1`. **The
  end condition is no longer met**, and a fresh session would have believed the programme finished.
  ⚠ **DATED 2026-08-29 AND NOT A CURRENT STATE: `NFR-064` REACHED `Implemented` ON 2026-08-30.** Annotated
  rather than rewritten, because this entry is a record of what that pass FOUND; but it is written in the
  present tense, which §1 already records as the thing that makes a historical sentence dangerous. **Run the
  candidate rule; do not read a set from here.** Not re-counted — an outcome arriving is not a statement
  going stale (the `DW-084` precedent).
- **`NFR-018` described as *"the only remaining requirement real work could close"*** — `NFR-064` also is,
  and unlike `NFR-018` it is not blocked on a third party. ⚠ **DATED: `NFR-064` and `NFR-065` are both
  `Implemented` as of 2026-08-30, so this correction is itself historical — see the FORTY-THIRD.**
- **`"every phase is closed"`, in TWO places, beside a suggestion to run `release-close-out.md`.** `PH-3`
  and `PH-7` are `Approved`; **`PH-7` is the LIVE phase holding `SL-035`.** ⚠⚠ **AND §1 SAYS SO EXPLICITLY
  TWENTY SCREENS EARLIER** — *"`PH-3` stays `Approved` ON PURPOSE"*, *"`PH-7` because it is the live
  phase"*. **The file contradicted itself and the wrong half sat next to the release ceremony**, which is
  the FIFTEENTH's *a file that contradicts itself hands the reader the choice*. ⭐ **I wrote one of the two
  copies today, propagating the older one's phrasing** — the sibling-copy failure running FORWARD.
⭐⭐ **THE TRANSFERABLE POINT: A TARGETED SWEEP CAN ONLY FIND CLAIMS ABOUT THE THING YOU CHANGED, AND MOST
OF THESE ARE CLAIMS ABOUT WHAT REMAINS.** *"The only remaining requirement"*, *"the end condition is met"*,
*"every phase is closed"*, *"three are numbered below"* — none names a row I touched, so no
touched-identifier grep could reach them; they went false because something was ADDED elsewhere.
**Grepping what you changed finds claims about it; only reading the whole file finds claims about what is
left.** ⚠ **Counted once, family of eight, on the TWENTY-EIGHTH's precedent.** ⚠ **Not an escape** — the
log was grepped for each phrase and no commit message asserts any of them.

⚠⚠⚠ **THE FORTY-FIRST IS AN UNMEASURED ENUMERATION IN THE ONE PLACE THIS FILE SAYS THE NEXT ACTION LIVES,
AND ITS INSTRUMENT DID NOT UNDERCOUNT — IT DELETED ROWS.** The `START HERE` list read *"`DW-088`,
`DW-090`, `DW-091`, `DW-092`, `DW-093` are all `Open` and **none has been put to the operator**"*. Wrong in
BOTH directions: `DEC-089` **d4 had already ruled on `DW-088`** (*"stays Open as filed"*) and **d3 on
`DW-089`**, while `DW-086` and `DW-087` had never been ruled on and were left out. The true set was SIX.
⚠⚠ **THE CAUSE IS A REGEX OVER JSONL AND IT IS WORTH MORE THAN THE INSTANCE.** The selection came from
`grep -oE '"id":"DW-…"[^}]*"lifecycle_status":"[A-Za-z]+"'` — and `[^}]*` stops at the FIRST closing brace,
so any row whose `custom_attributes` contain a nested object is silently dropped. `DW-086` and `DW-088`
both do. **It did not undercount; it removed those rows from consideration entirely** — `PE-599`'s shape,
where a truncated instrument deletes the evidence that would have changed the answer rather than reporting
a smaller number. ⭐⭐ **PARSE THE JSON. NEVER REGEX A JSONL ROW** — every row in this store carries a
free-text `custom_attributes` blob, so the failure is latent in every such grep, not peculiar to this one.
⚠ **"None has been put to the operator" was also never measured** — it is a claim about the whole decision
register, and the THIRTEENTH's rule is that an unmeasured assertion is counted whether or not it happens
to come out right. This one did not.
⭐ **What actually caught it: being asked to run the interview.** The `LL-005` pre-interview sweep - which
exists because the `NFR-023` question went to the operator before its sweep - is what read `DEC-089` and
found d3 and d4. **A list nobody acts on is never checked; the act of using it is the test.**
⚠ **Counted, not an escape**: `910c02df` and `652ab23f` carry the list, and no commit message repeats it.

⚠⚠⚠ **THE FORTIETH IS THE SWEEP THAT FIXED THE THIRTY-NINTH BEING SCOPED TO THE THIRTY-NINTH — SO IT
COULD ONLY EVER FIND THAT ONE FAULT CLASS, AND IT PUSHED THREE OTHERS UNCHANGED.** Having named the
inline-status class, I grepped this file for its exact wording (`— your verdict`, ``is at `Review` ``), ran
`count-prompt-ids.py`, ran the prose-status checker, got a clean board, and committed. **SEVEN live stale
statements about rows that same session had moved rode out in `910c02df` — the count itself grew from three to seven as the key widened:**
- **the `DW-080` phase-A block's *"`DW-080` itself stays `Activated`"*** — I had set that row to `Done`
  twenty minutes earlier;
- **§6's *"it is still not a `WBS-` row in any slice"*** — it is `WBS-25.1`, built and closed;
- **§6's heading *"`SL-033` IS THE LIVE SLICE"*** — `SL-034` had been created, executed and closed after it,
  and the NINTH fix already established that a session acts on a HEADING over its own body;
- **`DEC-072` d1's *"`DW-066` IS NOW `Activated`"***; and, worst-placed of all, **two bullets under a
  heading reading *"Open, and the operator's alone"*** — `DW-066` and `DW-079`, neither open nor theirs;
- **`DEC-072` d4's *"(`DW-079`, `Activated`)"*** — in the `(Status)` form the prose checker exists to catch,
  and missed anyway because the id sits INSIDE the parentheses.
⚠⚠ **THE TWO UNDER THE *Open* HEADING ARE THE ONES THAT WOULD HAVE COST A SESSION**: the others merely say something
false, while a stale entry under an *Open* heading **actively recruits someone to do finished work** — the
TWENTY-EIGHTH's *"a do-not-rebuild entry naming a file that HAS been built"* running in the opposite
direction. ⭐⭐ **AND THE COUNT ITSELF IS THE LESSON: three found, then five, then six, then SEVEN — each time by
WIDENING the key rather than by re-reading.** A sweep stopped at its first clean-looking result reports
the scope it happened to have, not the state of the file (`LL-015`).
⚠⚠ **ALL THREE INSTRUMENTS RAN CLEAN, AND FOR THREE DIFFERENT REASONS — `LL-009` INSIDE THIS FILE'S OWN
TOOLING, AGAIN.** The phrase grep was scoped to one class's literal wording. The prose-status checker needs
the `(Status)` form, and *"stays `Activated`"* is a sentence, not a parenthesis. `count-prompt-ids.py`
resolves ids and statuses and has never been able to see a prose claim. **A clean board from three blind
instruments reads exactly like a clean file.**
⚠⚠⚠ **AND THE PROSE-STATUS CHECKER HAS A FOURTH BLINDNESS, FOUND HERE, WHICH MATTERS BECAUSE IT LOOKS LIKE
THE ONE CASE IT WAS BUILT FOR.** `DEC-072` d4 read *"(`DW-079`, `Activated`)"* — a status in parentheses
beside an id, exactly the shape the check exists to catch — and it was missed, because its regex expects
`` `ID` `` **followed by** `(Status)`, while here the id sits **inside** the parentheses with the status.
**The three known blindnesses were about the claim not being in the `(Status)` form at all; this one IS in
that form and still slips.** ⭐ **So a clean run of that checker says less than its own documentation
implies — widen the pattern, or treat it as one input rather than the answer.**
⭐⭐ **THE REMEDY IS THE TWENTY-EIGHTH'S, WHICH ALREADY EXISTED AND WHICH I DID NOT RUN: after changing
anything, GREP THIS FILE FOR THE IDENTIFIERS AND FILE NAMES YOU TOUCHED — not for the fault class you just
named.** `DW-080`, `DW-079`, `DW-066`, `WBS-25`, `SL-034` would have surfaced all three in one command.
**Grepping for a known-bad phrase can only find the fault you already understand; grepping for what you
CHANGED finds the ones you do not.**
⚠ **Counted once, family of SEVEN, on the FIFTEENTH's and TWENTY-EIGHTH's precedent** — one fault class
(*a claim about a row this session moved*), one pass, one ordinal. ⚠ **Not an escape**: `910c02df`'s
message asserts none of the seven. ⭐ **It is its own best argument for the NINTH fix's stronger standard —
read the WHOLE file before handing it on, rather than the parts you think you touched.**

⚠⚠⚠ **THE THIRTY-NINTH IS THE THIRTY-SEVENTH'S EXACT FAULT CLASS, REINTRODUCED *IN THE SAME COMMIT THAT
DOCUMENTED IT*, TWENTY LINES FROM A PARAGRAPH FORBIDDING IT.** The `WBS-25.1` block ended *"⚠ The row is
at `Review` — your verdict."* `DEC-092` promoted it to `Implemented` and a fresh session would have gone
looking for a verdict already given. The THIRTY-SEVENTH — three `WBS-24.x` blocks reading *"⚠ `Review` —
your verdict"* — was written up in commit `d8131b56`, and **that same commit introduced this one.**
⚠⚠ **A RULE DOES NOT PROTECT THE SENTENCE BEING WRITTEN WHILE THE RULE IS BEING WRITTEN.** The remedy was
stated, the ordinal was assigned, the register name was given — and the very next merge-block drafted in
that pass carried a fresh inline status. The THIRTY-FIRST already recorded a remedy failing to reach the
section it was about; this is the same shape at a distance of twenty lines instead of eleven hundred.
⭐⭐ **THE ONLY FORM THAT DOES NOT ROT IS THE COMMAND.** Not a fresher status, not a status with a date
attached — `readiness_check(scope="slice", id="SL-034")`. ⭐ **AND THE MECHANICAL HABIT THAT CATCHES IT:
after writing ANY merge block here, grep this file for `— your verdict` and for `is at \`Review\`` before
committing.** Neither is an id and neither is a status in the `(Status)` form the prose checker looks for,
so both mechanical passes run clean straight over it — the THIRTY-FIRST's three blindnesses, again.
⚠⚠ **IT IS A FAMILY OF TWO, ONE ORDINAL, ON THE FIFTEENTH'S AND TWENTY-EIGHTH'S PRECEDENT.** The grep that
found the first immediately found a second: `WBS-24.2`'s block still read *"⚠ `Review`, not `Implemented`
— your verdict"* about a row promoted on 2026-08-26. **So the THIRTY-SEVENTH's own remedy was applied to
three blocks and missed a fourth**, which is the TWENTY-THIRD's lesson — a correction applied to some
artifacts and not their siblings leaves the survivor as the one the next session reads.
⚠ **Counted once**: both were true when written, became false, and sit in commits on `main` where a fresh
session could read them. Found by grepping for the FILE's own phrases rather than for ids, which is the
sweep the THIRTY-SEVENTH prescribed and the only one that has ever caught this class.

⚠⚠⚠ **THE THIRTY-EIGHTH IS A NEGATIVE STATEMENT ABOUT WORK — *"`DW-080` phase B has not started"* — AND
IT WAS FALSIFIED BY THE VERY SESSION THAT WROTE IT, THEN PUSHED.** Phase B merged as `#325` → `1d7cb04b`,
and the next package commit (`20f0b61c`) carried the sentence unchanged. A fresh session reading `main`
would have been told to start work that was already on `main`.
⚠⚠ **A "HAS NOT STARTED" IS A STATUS WEARING A REPOSITORY FACT'S CLOTHES, AND THAT IS WHY IT SAT IN THE
ONE PARAGRAPH HEADED *THE DURABLE FACTS, WHICH ARE NOT A WORKING STATE*.** *"`#320` merged to `df8d7c3a`"*
is durable — it stays true forever. *"Phase B has not started"* is the exact opposite: it is a claim about
the present that the next merge destroys, and it had been sitting in the block whose whole purpose is to
exclude such claims. **The heading did not protect it, because the sentence LOOKS like its neighbours.**
⭐⭐ **THE RULE: A NEGATIVE IS NOT A FACT, IT IS A STATUS WITH NO TIMESTAMP.** Record what HAPPENED, never
what has not. If a fresh session needs to know something is outstanding, the numbered list under
`START HERE` says so and `readiness_check` proves it — both of which move on their own.
⚠ **IT IS THE THIRTY-SECOND'S FAMILY** (never record your own working state) reappearing in the negative
voice, which is the disguise that got it past two rewrites of this very block in one session.

⚠⚠⚠ **THE THIRTY-SEVENTH IS A LIFECYCLE STATUS WRITTEN INLINE IN PROSE — THE ONE THING THIS FILE ALREADY
FORBIDS ITSELF, IN A SECTION THAT SAYS SO TWO SCREENS BELOW.** Three per-item blocks ended *"⚠ `Review` —
your verdict"* (`WBS-24.1`, `24.3`, `24.4`). **All three rows have since been promoted to `Implemented`**,
so a fresh session reading any of them was told a verdict was owed on work the operator had already
settled — and §6's own standing paragraph says in bold that *which rows are at `Review` is not written
here on purpose*. **The file contradicted itself, and the contradiction is the part that shipped.**
⚠⚠ **IT IS THE THIRTY-FIRST'S FAULT CLASS ESCAPING ITS ORIGINAL SCOPE.** That entry ruled *do not write a
LESSON's lifecycle status here*; the same reasoning applies verbatim to a `WBS-` row and nobody carried it
across. **A rule written about one register is not a rule about one register** — the reason (a status is a
column, and prose cannot be kept in step with it) never mentioned lessons at all.
⭐⭐ **ONE ORDINAL FOR THREE INSTANCES, under the FIFTEENTH's and TWENTY-EIGHTH's family precedent: one
fault class, found in one pass.** Replaced with the command rather than a fresher status, which is the only
form that cannot rot — `entity_query("slice", status="Approved")` then `readiness_check(scope="slice", id=<that id>)`.
⚠ **FOUND BY THE FILE-NAME SWEEP, NOT BY AN ID PASS**: grepping `prm-next.md` for the artefacts I had
touched surfaced it, while every identifier in those sentences was correct. A status is not an id.

⚠⚠⚠ **THE THIRTY-SIXTH IS A POINTER AT A LIST POSITION — AND A POSITION IS THE ONE THING GUARANTEED TO
ROT.** The kickoff block said *"Phase B has not started, and it is §6 item 1 now"*, and §6's `DW-080`
bullet said *"phase B is the rest of the row, and it is item 1 below"*. `DEC-085` d3 then made
**`WBS-24.8`** item 1 and both went false. **A family of two, one fault class, one ordinal** — the
FIFTEENTH's and TWENTY-EIGHTH's precedent.
⚠⚠ **THE STRUCTURAL POINT IS SHARPER THAN *DO NOT QUOTE A COUNT*: A LIST IS RENUMBERED EXACTLY WHEN WORK
COMPLETES, AND WORK COMPLETING IS THE EVENT THAT MAKES SOMEBODY READ THIS FILE.** So a cross-reference to
a slot is not merely fragile, it is guaranteed to be wrong at the precise moment it is read.
⭐⭐ **NAME THE ITEM, NEVER THE SLOT.** *"`DW-080` phase B, below"* survives every renumbering; *"item 1"*
survives none. The same applies to *"the next action"*, which this file has already had to annotate as
history three times (`DW-084`, `DW-078`, and the TWENTY-FIFTH).
⚠ **IT IS `LL-016`'s NEIGHBOUR, NOT `LL-016` ITSELF, AND THE DIFFERENCE IS WHY NO CHECK CAUGHT IT.** That
lesson is about a sequence being INTERNALLY wrong — a list numbered 1, 3, 4. This is an EXTERNAL reference
INTO a sequence, and the list was perfectly numbered both times, so reading it back as a sequence — which
I did, and which passed — cannot see it. **Grep for `item [0-9]` as well as reading the list.**
⚠⚠ **A THIRD INSTANCE, FOUND IN THE SAME PASS AND FOLDED IN RATHER THAN GIVEN ITS OWN ORDINAL, BECAUSE IT
IS THE SAME FAULT CLASS: A POINTER WHOSE REFERENT MOVES.** §6's heading read *"`SL-033` IS THE LIVE SLICE.
**START AT `WBS-24.1`**. (state as of 2026-08-23)"* — and `WBS-24.1` has been `Implemented` since
2026-08-24, with everything through `WBS-24.7` following it. **It pointed an arriving session at finished
work for five days.** ⭐ **THE `(state as of …)` DID NOT SAVE IT: an IMPERATIVE with a date attached is
still an imperative**, precisely as the THIRTY-THIRD found that a count with a disclaimer attached is
still a count — and the NINTH fix had already found that a session ACTS ON A HEADING even when the body
below it says otherwise. ⭐⭐ **THE STANDING FIX FOR ALL THREE: the next action lives in exactly ONE place,
the numbered list under `START HERE`. Every other section says what HAPPENED. If a second place in this
file tells you where to begin, that place is wrong** — this is the third time it has been.
⚠ **THE TWELFTH ESCAPE:** commit `98eafe54`'s message reads *"phase B is item 1, `DW-079` item 2,
`DEF-114` item 3"* — all three now wrong, and `DEF-114` has since left the list entirely. ⭐ `0ef92e37` is
NOT counted: it QUOTES the claim while correcting it, which the TWENTY-FIFTH's rule makes a dated record
rather than a live instruction.
⚠ **Both instances were written by the session that renumbered the list an hour later** — the TWENTY-
FIRST's shape yet again: every pointer I put into a file I am still editing is a hostage to the rest of
my own session.

⚠⚠⚠ **THE THIRTY-FIFTH IS A MECHANISM I CALLED *PROVEN BY REPRODUCTION*, AND THE REPRODUCTION WAS
CONFOUNDED. IT IS ALSO THE ELEVENTH ESCAPE — SAME FAULT, THREE PUSHED COMMITS.** §6's `DEF-114` note said
the guard fails because *"Testcontainers does not rebuild an image that already EXISTS"*, and — worse —
that the rival explanation, Docker's layer cache, had been *"measured and DISCARDED"* on a 33s identical
rebuild against a 31s one-byte-different one. **Both are false and the confident one was the elimination.**
⚠⚠ **THE SETUP STEP CHANGED TWO THINGS.** Pre-creating the named image also **warmed the `RUN sleep 30`
layer**, and the cache is keyed on the INSTRUCTION, not the tag: warm, that build returns in ~**2 seconds**
under a brand-new tag. The 33s figure was taken minutes after the daemon started, against a COLD cache — a
true number about a state that no longer held (`LL-015`), used to rule a hypothesis OUT.
⭐⭐ **A REPRODUCTION CONFIRMS; ONLY AN INTERVENTION THAT ISOLATES ONE VARIABLE EXPLAINS.** It reproduced
perfectly every time, which is exactly why it felt like proof — a confounded setup reproduces *more*
reliably, because it changes more. **What actually caught it was THE FIX FAILING:** a per-run TAG was
applied against the true precondition and the test still failed. Filed as `LL-029`.
⚠ **The real fix is the INSTRUCTION, not the tag** — `RUN echo {Guid} && sleep 30`. ⚠⚠ **And CI cannot
adjudicate this one**: a fresh runner has a cold cache, so the test passed there throughout. **Do not cite
the CI run as validation** — that is the exact inverse of `DEF-113`, where CI was the only instrument that
could see the fault.
⚠ **Counted twice, and the criterion is stated so it can be argued with.** The statement count moves
because the claim was false and reached commits. The escape count moves once, not three times, for one
fault class found in one pass across `df8d7c3a`, `98eafe54` and `0ef92e37` — the FIFTEENTH's and
TWENTY-EIGHTH's family precedent. ⭐ **`DEC-084` d4's parenthetical also described the fix as "a per-run
name" and is wrong; its RULING — fix it now, in its own PR — is untouched.**

⚠⚠⚠ **THE THIRTY-FOURTH IS A "NEEDS" THAT WAS NEVER MEASURED — A CLAIM ABOUT EVERY METHOD, WRITTEN
BECAUSE TWO METHODS HAD BEEN THOUGHT OF. IT IS THE TWENTY-SEVENTH'S SHAPE EXACTLY.** §6 item 1 read
*"⚠⚠ **THIS NEEDS DOCKER.** … Ask the operator to start Docker rather than iterating through CI"*, and
`DEF-113`'s own row offered *"two honest routes"*. **A third existed and cost ten minutes.**
`Acmp.Application.Tests` needs no container, and running it ALONE reproduced CI's `23/25` EXACTLY — and a
subset of CI's tests can only cover a SUBSET of CI's lines, so an equal count forces the two sets
identical. **The two uncovered line numbers were obtained with the daemon down and without spending a CI
cycle.**
⚠⚠ **THE TRUE SENTENCE BESIDE IT IS WHAT MADE IT CREDIBLE, AND THAT IS THE TRAP.** *"A local run will name
MORE files than CI does and none of that extra list is real"* is CORRECT — about the **gate**. The fault
was generalising an instrument's unusability at ONE scope into unusability at EVERY scope: the gate over
461 files is worthless without `Acmp.Integration.Tests`, while ONE file's line list is provable.
**`AV-159`'s shape in reverse — *I searched for X and found none* ruling out the family — which is also how
the TWENTY-SEVENTH went wrong.**
⭐⭐ **REPLACED WITH A MEASUREMENT RATHER THAN A DELETION, because the prohibition was pointing at something
real.** With the daemon up, the FULL local pipeline run exactly as CI runs it reproduces CI's gate output
exactly — 461 files, 99.60% global, exit 0. **The durable fact is therefore: the local gate is a faithful
model of CI's WHEN `Acmp.Integration.Tests` can run, and only then.** ⚠ That same run found `DEF-114`.
⚠ **Counted**: it reached three commits, and *"needs"* is a claim about every method — the THIRTEENTH's
rule is that an unmeasured assertion is counted whether or not it happens to come out right.

⚠⚠ **THE TENTH ESCAPE IS TWO FALSE CLAIMS IN ONE PUSHED COMMIT MESSAGE (`1b79677f`), TAKEN AS ONE ORDINAL
UNDER THE FAMILY PRECEDENT** (the FIFTEENTH's and TWENTY-EIGHTH's; the criterion is stated so it can be
argued with — one commit, one pass, one fault class). **Both are the word `only`, and neither was
measured:**
- *"`Acmp.Application.Tests` is the ONLY project whose tests reach this file"* — `Acmp.Api.Tests` reaches
  it too and covers lines 42, 43, 44 and 63. **The conclusion never needed it**: the subset argument
  requires only that the local tests are a SUBSET of CI's, which is trivially true.
- *"`StopAsync` … the only deterministic join"* — **it is not a join at all.** It cancels the stopping
  token BEFORE awaiting, and .NET 10 dispatches the body carrying that token, so a loaded runner can
  cancel the work before it ever starts. **CI rejected the fix built on it.**
⭐⭐ **THE RULE, AND IT IS `LL-028` (merged from `LL-023` + `LL-027` by `DEC-084` d2): A VALID ARGUMENT
RESTING ON A WEAK PREMISE IS WEAKENED, NOT
STRENGTHENED, BY A STRONGER PREMISE YOU DID NOT CHECK.** The unnecessary sentence was the only false one,
and it was the one that read as rigorous. **Treat every `only`, `never` and `cannot` in your own draft as
a measurement request: run the command, or strike the word.** ⚠ It fired a THIRD time inside the hour — a
confident *"Docker's layer cache"* diagnosis of `DEF-114`, measured and discarded **before** it was filed
(identical rebuild 33s, one-byte-different 31s, so the cache is not involved at all).

⚠⚠⚠ **THE THIRTY-THIRD IS A NUMBER INSIDE A SENTENCE THAT TELLS YOU NOT TO TRUST THE NUMBER, AND THAT IS
THE WHOLE FINDING.** §6 read *"`defects-minor` therefore names SIX rows, not five — measure it, do not
count from this sentence."* Filing `DEF-113` made it seven, and it sat in three commits that way.
⚠⚠ **THE HEDGE DOES NOT NEUTRALISE THE FIGURE — A READER TAKES THE NUMBER AND SKIPS THE DISCLAIMER**, which
is the one part of the sentence that costs nothing to obey. This is the EIGHTH fix's rule applied only
halfway: it stopped short of DELETING the count and supplied both a command and an answer, and the answer
is what gets read. ⭐ **A count with a disclaimer attached is still a count. Give the command or give
nothing.** ⚠ Counted rather than waved through on the strength of its own hedge: it was true, became
false, and reached a commit, which is this section's test and the only one it has.

⚠⚠⚠ **THE THIRTY-SECOND HAS THE SHORTEST LIFE OF ANY ENTRY HERE: ABOUT NINETY SECONDS, AND I FALSIFIED IT
MYSELF BY DOING THE OBVIOUS NEXT THING.** The kickoff block — rewritten in the very pass that was fixing
the kickoff block — said *"⚠ one commit on it is not yet pushed"*. It was written, committed, and then I
pushed, which is the only action anyone would take next. **It was also wrong at the instant it was
committed**, because two commits were unpushed at that moment, not one.
⚠⚠ **THIS IS THE TWENTY-FIRST'S LESSON AND I HAD JUST RE-STATED IT IN THIS SAME SESSION** — *"every number
I put into a file I am still editing is a hostage to the rest of my own session"* — and an hour earlier I
had removed a verdict ROSTER for exactly this reason, writing that *"a sentence naming WHICH items are
outstanding is falsified by the very work the reader is doing"*. Then I wrote a sentence naming which
commits were outstanding.
⭐⭐ **THE GENERAL FORM, WHICH IS WORTH MORE THAN THE INSTANCE: NEVER RECORD YOUR OWN WORKING STATE IN A
DURABLE ARTIFACT.** Branch, PR number and "which row is at `Review`" are facts about the repository that
outlive the session; "what I have pushed so far" is a fact about the ten minutes you are in. The line
already CARRIED the command that answers it — `git rev-list --left-right --count` — and I put a stale
answer in front of the working instrument anyway. **Delete the answer, keep the command.**

⚠⚠⚠ **THE THIRTY-FIRST IS THE THIRTIETH'S SHAPE, ABOUT A DIFFERENT LESSON, WRITTEN INTO THIS FILE
*BELOW* THE PARAGRAPH THAT FORBIDS IT.** §6 said *"ONE LESSON AWAITS YOUR INTERVIEW: `LL-024` … so
`lessons-confirmed` therefore fails again, ON PURPOSE — do not clear it by approving a sentence the
operator has not read."* `LL-024` is **Approved and pinned** and `lessons-confirmed` **passes**. A fresh
session would have gone looking for an advisory that is already green and an interview that already
happened — a stale *instruction*, not a stale number.
⚠⚠ **THE REMEDY FOR THIS EXACT FAULT IS WRITTEN ELEVEN HUNDRED LINES ABOVE IT** — *"never put a lesson's
lifecycle status inline in this file"* — and it was written for the THIRTIETH, about `LL-023`, one
session earlier. **A rule stated in the preamble did not reach the section the preamble is about.**
⚠⚠ **BOTH COMMITTED CHECKS RAN CLEAN OVER IT, AND FOR DIFFERENT REASONS, WHICH IS THE STRUCTURAL POINT.**
`count-prompt-ids.py` resolved `LL-024` and read its status as `Approved` correctly — it compares the id
to the register, and the register was right. The prose-status checker missed it because the claim is
never in the `(Status)` form its regex looks for: *"awaits your interview"* and *"fails again, ON
PURPOSE"* are **sentences about a status**, not a status. ⭐ **Two instruments agreeing the file is clean,
both blind to the same thing — `LL-009` inside this file's own tooling.**
⚠⚠ **IT IS A FAMILY OF TWO, COUNTED AS ONE ORDINAL.** Grepping this file for `lessons-confirmed` — the
sweep that found the first — immediately found the second: §6's `DW-084` block still read *"Filed as
`LL-022` (Proposed — the operator's interview is owed; `lessons-confirmed` fails on it ON PURPOSE … Do
not approve it unread, and do not 'fix' the advisory.)"*, **one hundred lines below a line reading
`LL-022` IS APPROVED AND PINNED**. Same fault, same section, different lesson, found in one pass — so it
takes one ordinal, following the FIFTEENTH's and TWENTY-EIGHTH's precedent of a family under one number
rather than an inflated count (`LL-016`: an ordinal is the thing no check can see, so do not multiply
them). ⭐ **The prose-status checker missed this one too, and for a THIRD reason** — its `(Proposed)`
pattern allows 24 characters before the closing paren and this one runs for ninety. **Three different
blindnesses, one fault class.**
⚠ **Neither is an escape.** The last commit (`4aee2d6`) fixed the `LL-024` statement in the MEMORY INDEX
and not here, and its message says so, so the only commit-message hit is a correction. ⭐⭐ **THAT IS THE
TRANSFERABLE HALF: a correction applied to one artifact and not its sibling is how the surviving copy
becomes the one the next session reads.** It is the TWENTY-THIRD's lesson — *apply a decision to EVERY
artifact in the same batch* — firing on a correction rather than on a decision.
⭐⭐ **THE STANDING FIX, because patching three instances of one fault is not a fix: WHEN YOU GREP THIS
FILE FOR A ROW YOU TOUCHED, GREP FOR THE ADVISORY AND THE REGISTER NAME TOO.** `lessons-confirmed` found
both of these in one command, where neither id-based pass found either. **An advisory's name is where the
instructions built on a status live**, and instructions are the half that costs a session.

⚠⚠ **THE NINTH ESCAPE (2026-08-26, `WBS-24.4`) IS A COUNT THAT WAS NEVER MEASURED, AND IT IS NOT IN THIS
FILE — IT IS IN A PUSHED COMMIT MESSAGE.** Commit `7173eb7` says *"24 sites become `<Num value={…} />`"*.
That figure is the **scanner's candidate-LINE count minus its false positives**, used as if it were the
number of render sites. **They are different quantities and I never measured the second.** Measured with
the pattern stated — occurrences of `<Num `/`<Pct `/`<Bytes ` in `src/**/*.tsx` excluding tests and the
library that defines them — it is **31** at that commit and **37** at merge, across **19** files. Two
lines carry a ternary with BOTH a `<Pct>` and a `<Num>`, and one line renders two `<Num>`, which is how
lines and instances diverge.
⚠⚠ **THIS IS `LL-015` COMMITTED BY THE SESSION THAT WAS QUOTING `LL-015` ABOUT ITS OWN SCANNER.** In the
same hour I wrote into `AC-147` that the scanner is *triage, not a coverage proof* — and then used its
output as a measurement of something else entirely. ⭐ **THE RULE: an instrument's output measures the
thing the instrument counts, and NOTHING ELSE. If you are about to state a different quantity, run a
different command.** A correction is posted on PR `#314`; the commit message stands, which is why the
escape count moves rather than the statement count.

⚠⚠ **THE THIRTIETH IS AN EXACT REPEAT OF A SHAPE THIS SECTION ALREADY DOCUMENTS, WHICH IS WHY IT MATTERS
MORE THAN ITS SIZE.** An entry here read *"`LL-023` (Proposed)"* — true when written, false about an hour
later when `DEC-079` d1 approved and pinned it, and committed that way. **A fresh session would have run
a confirmation ceremony that had already happened** — the twelfth's shape, a stale INSTRUCTION rather
than a stale number. And the FIFTEENTH recorded exactly this, about `LL-011`, in this same file:
*"`LL-011` (Proposed — needs the operator's confirmation interview)"* sitting below a line saying it was
Approved and pinned.
⚠⚠ **THE MECHANICAL PASS CANNOT SEE IT, AND THAT IS THE STRUCTURAL POINT.** `count-prompt-ids.py` reports
each cited row's status from the JSONL — it read `LL:Approved` correctly — but the word *"(Proposed)"*
sitting in the PROSE beside the id is not a status, it is a sentence. **The checker agrees with the
register while the paragraph disagrees with both.**
⭐ **THE REMEDY IS TO STOP WRITING IT: never put a lesson's lifecycle status inline in this file.** Cite
the id and let `entity_query("lesson", status="Proposed")` answer — a status written in prose has a
half-life measured in one operator interview.

⚠ **THE TWENTY-NINTH IS THE NEXT-ACTION LIST NUMBERED 1, 3, 4.** Item 2 vanished when the list was
collapsed after `WBS-24.3` closed, and it reached two commits that way. **A SEQUENCE IN PROSE IS
INVISIBLE TO EVERY MECHANICAL CHECK** (`LL-016`) — the id-and-status pass ran clean over it at 246 of
247, because an ordinal is neither an id nor a status. Same shape as the FIFTEENTH, where two entries in
this very section were both numbered `THIRTEENTH`. ⭐ **When you renumber a list here, read it back as a
sequence rather than checking the item you changed.**

⚠⚠ **THE TWENTY-EIGHTH IS A FAMILY OF FIVE, ALL FALSIFIED BY THE SESSION'S OWN WORK, AND THE FILE WAS
COMMITTED TWICE WITH THEM.** Building `WBS-24.1`–`24.3` made these live instructions wrong, and the
pre-handoff read is what caught them — not a later session tripping over them:
- **§2's *"`Timeline.tsx` and `Calendar.tsx` EXIST AND ARE DELIBERATE EMPTY SHELLS"*** — under a **do NOT
  rebuild** heading. `Calendar.tsx` was filled that same day. ⚠⚠ **A "do not rebuild" entry that names a
  file which HAS since been built is the most expensive kind of stale: it argues against the very work
  that was just done.**
- **§2's sub-note** — *"`DW-037` is `Activated`, `FR-035` is back to `Approved`, filling that shell is the
  second item of the live slice"*. All three were true that morning and none survived the afternoon.
- **§4 trap 1c's copy of the same claim** — the second instance of one sentence living in two places,
  which is how one gets fixed and the other does not.
- **§4 trap 1c's *"`FR-032` is now `Approved` with `DW-033` `Activated`"*** — both had moved on.
- **§6's *"It is the single missing member of a family that otherwise shipped"*** — `FR-032` was the
  missing member and is now `Implemented`.
⭐ **AND TWO INSTRUCTION-SHAPED GAPS THAT WERE NOT FALSE, ONLY SILENT, WHICH IS WORSE FOR A READER:** the
`WBS-24` order table carried no status, so nothing said three of its eight rows were finished; and the
axe-obligation note still said THREE rows owe a route when `WBS-24.2`'s was discharged — a fresh session
would have added the calendar sweep a second time. Both now say so.
⚠ **Not an escape: no commit MESSAGE repeated any of them** — checked by grepping the log, where the only
hit is a 2026-08-20 commit CORRECTING the same sentence. The count stays at eight.
⭐⭐ **THE PATTERN ACROSS 25–28 IS ONE THING: THE FILE DESCRIBES A CODEBASE THAT THE SESSION IS CHANGING
UNDERNEATH IT.** Every item built falsifies some sentence here, and none of them is an id or a status, so
the mechanical pass runs clean over all of it — 245 of 246 today. **After building anything, grep this
file for the FILE NAMES and REQUIREMENT IDS you touched, not just for the row you closed.**

⚠⚠⚠ **THE TWENTY-SEVENTH IS THE WORST KIND IN THIS FILE'S HISTORY, BECAUSE IT REACHED THE OPERATOR'S
DECISION SLATE.** This file, `DW-085`'s row, `DEC-078` and commit `33994aa` all said the FTS image build
*"cannot meet this project's prove-by-forcing bar without committing a deliberately-hanging Dockerfile"* —
and that its standard of proof would therefore be LOWER than `DW-084`'s. **It is false and it was never
true.** A Dockerfile written to a TEMP DIRECTORY at run time forces the build perfectly well, ships
nothing, and is unambiguous test scaffolding; the forced test passed on the first attempt at exactly its
bound. **The objection is to OWNING a hanging Dockerfile in `deploy/`, and the objection was allowed to
swallow the technique** — `AV-159`'s shape running in reverse: one method being unacceptable was
generalised into no method existing.
⚠⚠ **IT IS NOT A STALE STATEMENT, IT IS AN UNMEASURED ONE** — the THIRTEENTH's shape. *"Cannot"* is a
claim about EVERY method and it is the cheapest sentence to write and the dearest to justify.
⚠⚠ **AND IT WAS ON THE DOCKET THE OPERATOR READ WHILE DECIDING**, where it partly justified the
recommendation to carry `DW-085`. They overrode it and activated the row anyway, so the false premise did
not change the outcome — **that is luck, not process.** ⭐ **A false constraint in a decision slate is
worse than the same sentence in a note: a note is read later by someone with the register open, where
checking is cheap; a slate is read AT THE MOMENT OF DECIDING, by the one person who cannot check it.**
⭐⭐ **THE STRUCTURAL GAP IS `LL-023` (Approved and PINNED, `DEC-079` d1 — it binds): `LL-011`'s generator guarantees the QUOTED records — and
on that docket fifteen fields were verified byte-identical, the verifier itself calibrated against two
injected faults — but NOTHING guarantees the agent's connective prose, and the connective prose is what
frames the question.** Worse, the verification machinery makes the surrounding prose read as MORE
trustworthy, because everything beside it is provably exact. `PE-631` corrects it; `33994aa` is pushed,
hence eight escapes.

⚠⚠⚠ **THE TWENTY-SIXTH IS INSIDE THE TWENTY-FIFTH, WRITTEN IN THE SAME EDIT, AND IT ESCAPED INTO A
PUSHED COMMIT MESSAGE — THE SIXTEENTH'S SHAPE EXACTLY.** The entry below originally closed *"the FOURTH
time this file has carried a wrong INSTRUCTION rather than a wrong number."* **Nothing measured that.**
Measured now, with the criterion stated so anyone can disagree with it — a preamble entry, headline
ordinal or named sub-item, whose stale thing THIS FILE ITSELF labels an instruction — the priors are the
tenth, the twelfth, the fifteenth's `LL-011` item and the twentieth. **Four priors, so mine is the FIFTH.**
"Fourth" is reachable only by counting headline ordinals and silently dropping the fifteenth's sub-item,
a criterion I never stated and did not have in mind.
⚠⚠ **AN ORDINAL IS THE ONE THING `LL-016` NAMES AS INVISIBLE TO EVERY MECHANICAL CHECK, AND I WROTE ONE
WITHOUT AN INSTRUMENT INSIDE THE ENTRY DOCUMENTING A STALE STATEMENT** — while the id-and-status pass over
the same edit ran clean at 232 of 233. ⭐ **THE FIX IS THE EIGHTH'S RULE EXTENDED FROM NUMBERS TO ORDINALS:
do not re-count, NAME THE LIST.** A list of four named entries cannot go stale by one; a count can, and
did. ⚠ **Both tallies moved** — the statement count because the THIRTEENTH's precedent is that an
unmeasured assertion is counted whether or not it happens to come out right, and the escape count because
`33994aa` is pushed and a commit message cannot be amended. **Softening my own tally is the move this
register has declined four times; it is not available to me either.**

⚠⚠ **THE TWENTY-FIFTH (2026-08-25, after `DW-084`) IS A STALE *INSTRUCTION* IN §6's `DEC-072` d1 BLOCK,
AND IT SURVIVED THE PASS THAT REWROTE THE FILE AROUND IT.** The paragraph reads *"ORDERING IS DECIDED
(`DEC-073`): THE SWEEP RUNS BEFORE `SL-033` STARTS, SO THIS — NOT `WBS-24.1` — IS THE NEXT ACTION."*
`DW-078` closed two days later, so a fresh session reading that block would go looking for a Dependabot
queue that is already swept. **Every id in it resolves and `DW-078` genuinely reads `Done`, so the
id-and-status pass runs clean straight over it** — the twelfth's shape. **Prior stale INSTRUCTIONS, NAMED
rather than counted, because a list cannot drift the way an ordinal can: the tenth's *"read this
requirement carefully"*, the twelfth, the fifteenth's `LL-011` item, and the twentieth.**
⭐⭐ **WHAT IS NEW, AND IT IS A DISTINCTION THE COUNTER NEEDED: THE IDENTICAL SENTENCE IS FINE IN ONE PLACE
AND WRONG IN THE OTHER.** Commit `ae23b03`'s subject says *"the sweep runs first, so `DW-078` is the next
action, not `WBS-24.1`"* — and that is NOT counted as an escape, because a commit message is a dated
record of what was decided when it was decided, and its outcome later happened. This file is a LIVE
instruction surface a fresh session acts on. **Same words, different status: what makes prose stale is
not the sentence, it is whether the artifact claims to describe NOW.** Annotated in place rather than
deleted, because the `DEC-073` reasoning — attribution, one cause per failure — still binds `DW-080`.
⚠ **`DW-084`'s own completion is deliberately NOT counted**; see the ✅ note in §6 for why.

⚠ **THE TWENTY-FOURTH IS THE NEXT-ACTION BLOCK, ONE HOUR OLD, AND IT IS *TWO* FAULTS IN ONE
PARAGRAPH.** It said *"all FOUR activated streams are in scope … and the ORDER IS NOW THEIRS: `DEC-076`
d3 accepted it"* — but `DEC-077` d5 had already amended that order by inserting `DW-084` at the front, so
(a) the live sequence is **five** rows, not four, and (b) crediting the order solely to `DEC-076` d3 sends
a reader to a decision that does not contain `DW-084`. ⚠ **A COUNT AND AN ATTRIBUTION FAIL DIFFERENTLY:
the count merely disagrees with the list beneath it, but the wrong attribution sends someone to the wrong
record and they find nothing amiss there.** Also restructured: the numbered list opened with a ✅ DONE
item, so the one thing a fresh session most needs was the *second* thing it read.

⚠ **THE TWENTY-THIRD (2026-08-25, later still) IS THE KICKOFF BLOCK ITSELF.** It said
*"`readiness_check` — expect `ready:TRUE` … `ready:FALSE` = a real blocker, go read it, never soften
it"*. `DEC-077` d1 then made `FALSE` the CORRECT state, deliberately. A fresh session pasting the block
would have read the package's intended condition as a fault and gone looking for something to repair.
⚠⚠ **THREE OF THE LAST FOUR ARE THE SAME SHAPE — A DECISION OUTRUNNING THE PROSE THAT DESCRIBED THE OLD
STATE — WHICH LOCATES THE RISK WINDOW PRECISELY: it is the gap between the operator ruling and the
write-up finishing.** Apply a decision to EVERY artifact in the same batch, or the artifact you skipped
is the one the next session reads.

⚠ **THE TWENTY-SECOND (2026-08-25, later) IS A DECISION OUTRUNNING THE PROSE THAT DESCRIBED THE OLD
STATE.** §6 called `DW-080` *"unscheduled"* — true until `DEC-076` d3 ordered it third, hours earlier in
the same session. ⭐ **Both 21 and 22 were caught by the pre-handoff end-to-end read rather than by a
later session tripping over them, which is the ninth fix's method paying for itself twice in one pass.**
⚠ **Neither was an id and neither was a status; the mechanical pass ran clean over both.**

⚠⚠ **THE TWENTY-FIRST (2026-08-25, later) IS THE PREAMBLE'S OWN WARNING FIRING ON THE SESSION THAT WROTE
IT: A NUMBER INVALIDATED, INSIDE ONE SESSION, BY THAT SESSION'S OWN LATER WORK.** §6 said the memory index
was *"compacted to 155"*. True when written. I then added to that same file twice more in the same
session and it ended at **178** — against a measured **200-line hard cap**. ⚠⚠ **AND THE STALENESS IS NOT
THE ARITHMETIC, IT IS THE HEADROOM: `155` reads as comfortable and `178` is nearly out of room**, which is
exactly the decision the next session has to make before adding a line. Replaced with the command.
⭐ **Every number I put into a file I am still editing is a hostage to the rest of my own session** —
the preamble has warned about this since the first fix, and it caught its author anyway.

⚠⚠ **THE NINETEENTH (2026-08-25) IS THE SAME MEASUREMENT WRITTEN TWICE, IN ONE COMMIT, WITH TWO
DIFFERENT NUMBERS — AND THE HALF THAT WAS WRONG IS UNKNOWABLE.** §6 said the resume pass covered **206**
cited identifiers; `PE-612`, written by the same commit `c043ed6`, said **217**. Nothing reconciles them,
because **the regex that produced either number was never recorded** — so the disagreement is not merely
unresolved, it is unresolvable from the artifacts. Measured fresh today over 18 id families: **221 before
this paragraph was written and 223 after it** — because the file is its own subject and citing `PE-612`
and `PE-613` here moved the number. That adjudicates nothing about 206-vs-217, since a different pattern is
a different question; it does demonstrate the hazard in one line. ⚠ **Both figures are HISTORY, stamped
2026-08-25 — the live number is whatever the script prints today, and it has risen since.** ⚠⚠ **A NUMBER WITHOUT ITS
INSTRUMENT IS NOT A MEASUREMENT, IT IS A CLAIM** — two of them from one pass simply expose what was always
true of the other seventeen. The fix is the eighth's rule taken one step further: not just *replace the
number with a command*, but **commit the command** (`scripts/count-prompt-ids.py`), so the next reader can
argue with the instrument. ⭐ **Found in the first ten minutes of a fresh session, by reading `PE-612`
against the file it describes — the two disagreed on their own shared subject.** `PE-613` records it.

⚠⚠ **THE EIGHTEENTH IS THE COUNTER ITSELF FAILING IN A NEW WAY: I CORRECTED THE STATEMENT AND FORGOT TO
COUNT IT.** This file carried *"`#135` mssql 2025 — the image will not boot … PRODUCTION MUST NOT MOVE TO
SQL SERVER 2025 EITHER."* Every clause was wrong — `ldd` reports nothing missing in EITHER image, and the
real cause was our own `Dockerfile.sqlserver` pinning a 22.04 package repo under a 24.04 base. `PE-606`
withdrew it and §6's bullet was rewritten — **but the counter above stayed at seventeen and no ordinal was
written**, so the file recorded the correction and lost the count of it. It had also escaped into commit
messages `6069618` and `e8e22f3`, which is why the escape count moves to six.
⚠⚠ **A CORRECTION THAT DOES NOT UPDATE THE TALLY OF CORRECTIONS IS HALF A CORRECTION.** Found by grepping
for `EIGHTEENTH` and getting zero — not by reading. **When you fix a statement here, bump the count in the
same edit; they are one action.**

⚠⚠ **THE SEVENTEENTH IS THE WORST OF THE SESSION: A CAP I SET MYSELF, IN THE SAME COMMAND, READ BACK AS
IF IT WERE THE REGISTER — AND IT COST A JUDGEMENT, NOT A COUNT.** `gh pr list --limit 10` returned exactly
ten rows and I wrote **thirteen**; no output anywhere showed a total. `DW-078` then carried a headline of
thirteen over an enumeration of ten — **`DEF-104`'s exact shape, reproduced in the session that memorialised
`DEF-104`**, and `LL-015` in its purest form. Measured with no cap: **twelve**.
⚠⚠ **THE TWO ROWS THE CAP HID WERE THE TWO THAT MATTERED.** #128 and #134 (`dotnet/sdk` and
`dotnet/aspnet` 8.0→10.0) both edit `deploy/Dockerfile.backend` — the `FROM` lines at 16, 31 and 51 that
`DW-066` is entirely about. Earlier in the same session I read `DW-066`'s trigger (*"whenever a base-image
bump is being made anyway"*) and concluded it had **not** fired, reasoning that the open docker rows touched
node, nginx and mssql. That reasoning came wholly from the truncated list. **A TRUNCATED INSTRUMENT DOES NOT
UNDERCOUNT — IT DELETES THE EVIDENCE THAT WOULD HAVE CHANGED YOUR ANSWER, from the end of the list you are
least likely to think about.** The classification was wrong too: **three** routine and **nine** majors, not
seven and three, with #261 filed routine while #137 — *the same version family* — was filed major.
`PE-599` corrects it; commit `b1eb81a` cannot be amended, hence five escapes.

⚠⚠ **THE SIXTEENTH ESCAPED INTO A COMMIT MESSAGE — AND IT SAT INSIDE THE SENTENCE BRAGGING THAT THE
MECHANICAL PASS HAD RUN CLEAN.** The fifteenth's own write said those 190 identifiers spanned *"nine
families"*. Nothing measured that. The phrase was carried whole out of the TENTH's sentence in this file
(*"68 across nine families"*) with only the figure in front of it refreshed; measured, the answer is
**fifteen**. ⚠⚠ **A PHRASE CAN GO STALE WITHOUT ITS NUMBER CHANGING** — the wrong half travelled attached
to a number I had genuinely just measured, which is what made it invisible. And an id-and-status pass
cannot check a clause about an id-and-status pass. Caught by a review pass after the push: `e33c636`'s
message cannot be amended, so the escape count above is now four; `PE-595` corrects `PE-593`.

⚠⚠ **THE FIFTEENTH (2026-08-23, later) IS THIS SECTION FAILING AT ITS OWN JOB: THE ORDINALS COLLIDED.**
Two findings, added by two different commits (`74b2801` and `21ba170`), were **both** labelled
`THE THIRTEENTH` — so the count above read **thirteen** where it should have read fourteen, and the
sequence had no fourteenth in it at all. The `a2066ba` unmeasured-assertion finding keeps `THIRTEENTH`; the 2026-08-23 end-to-end pass is renumbered
`FOURTEENTH`; this is the fifteenth. **The section that counts wrong statements contained one, in its own
numbering** — and no gate, and no id-and-status pass, can see an ordinal. The same re-read found **five**
more, and every one of them is prose:
- **A requirement-register row count** in §1 — stale the moment `SC-031` added `FR-164`. It is a `wc -l` now.
- **`DEF-104` "Open, low … twelve paged reads"** in the build-standing block, a hundred lines below a block
  already reading *"`DEF-104` is `Fixed`"*. ⚠⚠ **A FILE THAT CONTRADICTS ITSELF HANDS THE READER THE
  CHOICE, AND THE STALE HALF WAS THE ONE WEARING THE DETAIL** — it is the half that reads researched.
- **`LL-011` "(Proposed — needs the operator's confirmation interview)"**, below a line reading
  *"`LL-011` Approved and PINNED"*. A fresh session would have run a ceremony that already happened:
  the twelfth's shape, a stale INSTRUCTION rather than a stale number.
- **"the advisory listing the same 53"** — that is a `readiness_check` field, not a fact about the register.
- **"Nine rows correctly carry no date"** — 17 assumptions, 12 dated, so it is **five**. ⚠⚠ **THE
  ARITHMETIC NEVER CLOSED, IN A FILE NOTHING CHECKS THE PROSE ARITHMETIC OF.**

⚠ The mechanical pass ran clean again: **190** distinct identifiers, every one
resolving except `DEF-082` — the KNOWN gap (`DEF-101`). **That is three passes running where the
id-and-status check found nothing and the prose carried everything. Read the prose; it is the hard half.**

⚠⚠ **THE FOURTEENTH (2026-08-23) WAS FOUND BY THIS FILE BEING RIGHT AND ME BEING WRONG, WHICH IS NEW.**
Preparing the resume, the end-to-end re-read surfaced **five** stale statements at once — *"every phase is
`Implemented` except `PH-3`"* (`PH-7` is also `Approved`), *"155 of 164 `wbs_items`"* (right numerator,
stale denominator), *"Ten lessons … `LL-001`…`LL-010`"*, *"all 155 have `slice_id` NULL"* and *"28 closed
slices"*. Each is now a COMMAND rather than a number, per the eighth fix's rule.
⚠⚠ **AND THE SIXTH FINDING WAS THE OTHER DIRECTION: this file said TWO of `SL-032`'s rows were mis-sized
and I had been writing THREE all session.** The file was right. I had conflated its TRUE sentence — *"that
habit paid three times in this slice"*, two catches plus one confirmation — with a false count of wrong
sizings, and propagated mine into **six** artifacts including `SL-033`'s standing warning. `PE-592`
corrects it. **A count of what an instrument DID is not a count of what it FOUND**, and the mechanical
id-and-status pass ran clean over all 170 identifiers while this sat in the prose.

⚠⚠ **THE THIRD ESCAPE INTO A COMMIT MESSAGE (2026-08-21) WAS A ROW LYING ABOUT ITSELF.** `SL-032`'s
rewritten objective said *"THE SLICE OBJECTIVE BELOW REPEATED THEM VERBATIM"* and *"the wrong text is left
in place rather than edited away"* — and `entity_upsert` **replaces full rows**, so that very write had
deleted the text it claimed to preserve. Commit `0e75755`'s message repeats it and is pushed.
**THE SHAPE, and it is not a stale number: an intention stated in the same breath as an operation that
contradicts it, where the OPERATION is what takes effect.** No gate sees it — both halves are well-formed
prose in a valid row. `PE-585` corrects it; the original objective survives at
`0e75755^:tamheed-package/data/slices.jsonl` and is pointed at rather than re-typed (`LL-001`).
**Before writing that you preserved something, check the tool you used can preserve it.**

⚠ **THE TWELFTH WAS AN INSTRUCTION, NOT A NUMBER, AND IT WOULD HAVE COST A DEPENDENCY.** §6's
`▶▶ DO THIS FIRST` block told a fresh session to run a spike and, three lines down, *"THE DEPENDENCY
EVALUATION IS DONE — do not redo it"* over a table marking `modern-screenshot` as the pick. The spike
ran on 2026-08-21 and that package **cannot run under this app's CSP at all**. Every number in the table
was correct; the *instruction built on them* was wrong, and a fresh session obeying it would have
installed a package that throws on every card. The block was **replaced, not annotated** — see §6.
**This is the tenth fix's lesson repeating: an id-and-status verifier cannot see a stale INSTRUCTION.**

⚠⚠ **THE THIRTEENTH IS THE MOST EMBARRASSING, BECAUSE IT HAPPENED IN THE SESSION WHOSE OWN FINDINGS ARE
ABOUT THIS.** Commit `a2066ba` asserts *"readiness ready:TRUE"* about the state after twelve activations.
Only `gate_run()` was run after those writes; the last `readiness_check` preceded every one of them. The
assertion was later verified true — **and it was still unmeasured when written.** ⚠ **An assertion that
happens to be correct is still an unmeasured assertion**, and "it came out right" is the reasoning that
lets the practice rot. It was caught by a review pass, not by me. Same breath, or don't write it.

⚠⚠ **THE ELEVENTH DID NOT REACH THIS FILE — IT REACHED A COMMIT MESSAGE, WHICH CANNOT BE AMENDED.**
Commit `46821d6` states the deferred-work triage split as *"out of scope 11 / blocked on a stack 17"*. The real
split is **10 and 18**. The script had always said so; the command that ran it piped through `tail`, so those two
lines were never on screen, and I wrote the counts from recollection. It was caught only because preparing this
file starts by re-measuring everything it asserts. **THE MECHANICAL RULE: a number entering a durable artifact
must come from output visible in the same breath — not from what you believe the output said.**

⚠ **THE TENTH (batch 14) IS THE MOST INSTRUCTIVE, BECAUSE THE MECHANICAL CHECK PASSED.** After reconciling
this file I extracted every governance id it references — 68 across nine families — and verified all 68 live:
zero dangling, zero status mismatches. **The file still contained three wrong statements**, because none of
them is an id or a status: a `⚠ read this requirement carefully` instruction for a requirement that batch had
just closed, and two prose counts ("the first subtraction alone yields twenty", "five of the original
twenty"). ⚠⚠ **An id-and-status verifier cannot see a stale INSTRUCTION or a PROSE NUMBER — and a prose
number is exactly what this section is about.** The fix was to delete every intermediate count and leave the
three-step rule. **When you re-verify this file, read the prose; the mechanical pass is the easy half.**

⚠⚠ **BEFORE YOU HAND THIS FILE ON, RUN BOTH CHECKS — the mechanical one is committed, the second is not
and is worth ten lines of your time.** `python scripts/count-prompt-ids.py` resolves every cited id and
prints each row's real status. It does NOT see a status written in PROSE, which is how the THIRTIETH
happened, so also run this — it compares an inline `(Status)` beside an id against the register:

```python
import io, re, json
s = io.open('tamheed-package/prompts/prm-next.md', encoding='utf-8').read()
rows = {}
for f in ('lessons','deferred_work','defects','requirements','wbs_items','acceptance_criteria'):
    for l in open(f'tamheed-package/data/{f}.jsonl', encoding='utf-8'):
        r = json.loads(l); rows[r['id']] = r.get('lifecycle_status')
for m in re.finditer(r'`(LL|DW|DEF|FR|NFR|WBS|AC)-[0-9.]+`[^.\n]{0,24}?\((Proposed|Activated|Approved|Open|Done|Fixed|Implemented|Review|Deferred)\)', s):
    ident = m.group(0).split('`')[1]; claimed = m.group(2); actual = rows.get(ident)
    if actual and claimed != actual:
        print(f'line {s[:m.start()].count(chr(10))+1}: {ident} prose says ({claimed}) register says {actual}')
```

⚠ **It cannot tell a QUOTATION of a corrected error from the error itself** — the entry documenting the
thirtieth quotes *"`LL-023` (Proposed)"* and the check flags it every time. That is trap 2's shape in a
tool you just wrote: **read the hit before believing it.**

⚠ The eighth fix (batch 13) stopped patching the numbers and **deleted them**, replacing each with the
command that measures it. A tally you can re-type is a tally that will go stale again; a command cannot.

⚠ **The NINTH fix was different and is worth copying.** It was not found by tripping over a wrong number
mid-task — it was found by **reading this file end to end** before handing it to a fresh session, which
surfaced **nine** wrong statements at once: a stale tool version, four row states that had since changed,
three tallies, and a **section heading that contradicted its own body** (`SC-021` was labelled "needs you,
Proposed" three lines above a paragraph saying it was Merged). A fresh session would have acted on the
heading. **Before you hand this file on, re-read the WHOLE thing and re-verify every row state it asserts
against the live register** — batch 13's pass checked 19 of them mechanically and found zero mismatches
only AFTER the nine fixes.

---

## §1 — The state the 2026-08-20 disposition session began from (⚠ superseded in part — see §6)

> ⚠⚠ **SEVERAL LATER SESSIONS CHANGED THE SHAPE OF v1 AND BUILT AGAINST IT. THIS SECTION IS THE STATE
> THEY BEGAN FROM, NOT THE STATE YOU ARE IN.** Rows were activated, requirements returned to `Approved`,
> one of the two blind controls was fixed, `SL-032` was built and closed, and `SL-033` now holds eight
> scheduled rows. **Go to §6 FIRST — the numbered list under `START HERE` is the ONLY place that says
> what to do next — before acting on anything in §1, §2 or §4.** ⚠ Every phase statement below predates
> `PH-7`.

**THE BUILD LADDER IS FINISHED AND SO IS THE REGISTER PROGRAMME.** `P1`–`P19` shipped long ago; the
`DW-029` acceptance-criterion programme that replaced it ran **twenty batches** and was accepted by the
operator on 2026-08-20. **`SL-031` is `Implemented` and `PH-6` is closed.** ⚠ **Do not
re-quote a phase tally from here** — `entity_query("phase")` is the live answer. What is durable is the
REASON two phases sit at `Approved`: `PH-3` on purpose (below), and `PH-7` because it is the live phase.

⚠ **`PH-3` stays `Approved` ON PURPOSE — do not "repair" it.** `WBS-20.4` is the email adapter against a
hard constraint (`DEC-055`), and closing it is the manufactured-status move `DEF-010` records.
⚠ `SL-014` is `Deferred` (`P14`/Tarseem, `DEC-028`) and is off the ladder. Do not start it.

⛔ **HISTORICAL — DO NOT READ THIS AS NOW.** The sentence here was *"You are on `main`, clean, everything
merged, CI green. No feature branch is open."* It was true of the 2026-08-20 session this section
describes, and it is **false as of 2026-08-27**: `DW-080` phase A left `feat/dw-080-phase-a-net10` open
with PR `#320`. ⚠ **It is annotated rather than deleted because §1 is a dated record**, but it was written
in the present tense and in the SECOND PERSON, which is what made it dangerous — *"you are on `main`"*
reads as an instruction about now no matter what the section heading says. ⭐ **The transferable half: a
historical section survives going stale only if its sentences do not address the reader directly.**
`git rev-parse --abbrev-ref HEAD` is the answer, and the kickoff block at the top now asks for it.
⚠ **DELIBERATELY NOT COUNTED IN THE TALLY, and the reasoning is recorded so nobody thinks it was missed.**
§1's heading already says in bold that it is *"the state they began from, NOT the state you are in"*, so
the artifact does not claim to describe NOW — the TWENTY-FIFTH's own test. The TWENTY-THIRD was counted
because the KICKOFF block claims exactly that and has no such heading. **Counting this one too would
inflate the ordinal against `LL-016`'s warning**, and an ordinal is the one thing no mechanical check can
see. The fix is applied either way; only the number is withheld.

### Measure, do not trust — the three commands that replace every tally

⚠ **`entity_query("requirement", ...)` OVERFLOWS THE TOOL'S TOKEN LIMIT** — the whole register is tens
of KB even with `columns` set, because `columns` does not actually narrow the payload. (A row count sat
in this sentence and went stale the moment `SC-031` added `FR-164`. `wc -l` is the answer.) **Count from the canonical
JSONL instead**, which is also what trap 13 already tells you to do when building any payload:

```
tamheed-package/data/requirements.jsonl     # wc -l for the total; count by lifecycle_status/kind/priority
tamheed-package/data/deferred_work.jsonl    # the DW register: count by lifecycle_status / severity
entity_query("defect", status="Open")       # small enough to query directly
gate_run() / readiness_check("package")     # the live verdicts — never quote a remembered one
```

⚠ **No count is written into this file on purpose.** Ten stale tallies, and an eleventh that reached an
unamendable commit message, are why. The one structural fact worth stating is a *shape*, not a number:
**the requirement register is now mostly `Implemented` or carried by a `DW-` row, and what remains
`Approved` is either externally blocked or covered by deferred work.**

### THE CANDIDATE RULE — and it was BROKEN until batch 20, so read this before running it

The rule for "which Must-priority non-functional requirements could still be closed by reading code":

1. the `Approved` **Must**-priority **non-functional** ids;
2. **minus** every id named in a `deferred-work` row's **TITLE** — i.e. the row is *about* it;
3. **minus** the ops/runtime group `PE-485` names (`NFR-015 017 044 052 062`), which no `DW-` row covers
   but which is separately blocked on a running stack.

⚠⚠ **Step 2 used to read *"named anywhere in a deferred-work row"*, and that CONFLATES A MENTION WITH A
COVERAGE.** Writing `DW-074` with the phrase *"the `NFR-018` ASVS assessment"* in its activation trigger
made `NFR-018` vanish from the candidate set though nothing about it had changed. **The loose rule
silently shrinks the worklist every time a row cross-references a requirement — so the better the
register's prose gets at linking things, the more requirements quietly disappear, and a
well-cross-referenced register would eventually report itself finished.**

⛔⛔ **NO ANSWER IS WRITTEN HERE ANY MORE, AND THE REASON IS THAT THIS SENTENCE HAS NOW BEEN WRONG IN BOTH
DIRECTIONS.** It first read *"expect TWO, both externally blocked … the end condition is met"*. `DEC-094` d6
then created `NFR-064` and made that false (the FORTY-SECOND). `WBS-26.1` and `WBS-26.2` then carried
`NFR-064` AND `NFR-065` to `Implemented`, which made the CORRECTION false in turn — the FORTY-THIRD.
⭐⭐ **A COUNT THAT HAS BEEN WRONG IN BOTH DIRECTIONS IS NOT A COUNT TO REFRESH, IT IS A COUNT TO DELETE**
(the EIGHTH fix's rule, and the THIRTY-THIRD's: give the command or give nothing). **RUN THE RULE.** ⚠ Its
step 2 is a substring PROXY and cannot make the judgement the rule needs — *does the row COVER the
requirement, or merely NAME it?* — so treat its output as triage (`LL-006`).
⚠ **`NFR-018` is externally blocked and still real**: it needs an external OWASP ASVS 5.0 Level 2
assessment, and the evidence pack `DOC-070` is an INPUT to it, never a substitute.
⛔ **THIS SENTENCE USED TO NAME A SECOND ONE, `NFR-038`, AND `SC-043` REMOVED IT FROM THE SET ON
2026-09-01.** `NFR-038` and `NFR-005` are both Tarseem, whose `P14`/`SL-014` `DEC-028` deferred
**indefinitely** with no automatic trigger, so `OOS-02` puts the capability out of scope entirely — the
same reasoning `SC-026` applied to `NFR-026` under `DEC-012`. Both are now `Deferred` and **neither enters
the candidate set at all**, because step 1 selects `Approved` rows. ⚠ Nothing had ever ruled on them: a
calibrated sweep of every register (control `NFR-054`: 55 rows including 11 decisions) returned **ZERO**
decisions and **ZERO** ADRs for either. **Run the rule; do not read a set from here.**
⚠⚠ **RUN THE RULE; DO NOT READ AN ANSWER FROM HERE — AND KNOW THAT STEP 2 IS A PROXY.** Mechanised as
written, step 2's *"named in a `deferred-work` row's TITLE"* is a substring test, and it removes BOTH
`NFR-018` (named in `DW-079`'s title) and `NFR-064` (named in `DW-093`'s), leaving `NFR-038` alone.
⚠ **THAT WORKED EXAMPLE IS DATED 2026-08-29 AND ITS ARITHMETIC HAS MOVED TWICE** — `NFR-064` reached
`Implemented` on 2026-08-30, and `SC-043` moved `NFR-038` to `Deferred` on 2026-09-01, so **neither enters
the candidate set now and the example's *\"leaving `NFR-038` alone\"* is false**. It is kept because the
POINT survives its own numbers: step 2 removes
a requirement whose covering row merely NAMES it. The
stated answer has always needed the judgement the script cannot make — *does the row COVER the
requirement, or merely name it?* `DW-079` explicitly does NOT close `NFR-018`. **Treat the rule's output
as triage, not as the worklist** (`LL-006`).

### The advisories — none is a task, and two are BLIND

`defects-minor` (carried deliberately — **read the live list, it grew on 2026-08-20**),
`deferred-work-reviewed` (grows on purpose), `acs-slice-bound` (`AC-109`–`AC-112`, accepted by `DEC-058 d3`),
and `assumptions-current`, which **now fails on purpose** — see below.

⚠⚠ **ONE OF THE TWO BLIND CONTROLS IS FIXED; THE OTHER IS NOT.** ✅ `assumptions-current` no longer reports
`indeterminate` — twelve rows were dated on 2026-08-20 and it now **fails**, naming the genuinely overdue
one. ⚠ **The field is a FUTURE re-validation DUE date**, so more will go red as dates pass: **that is the
control working, and clearing a date to restore the amber would be re-blinding it.** ⚠⚠ **ONE OF THE TWO BLIND SLICE RULES IS NO LONGER BLIND, AND THAT IS NEW.**
Slice-scope `wbs-done` now WORKS for any slice whose items carry a `slice_id` — `SL-032`'s ran 5→4→3→2→0
and adjudicated its exit, the first time that rule has ever done so (`DEC-068` d2's fix-forward rule for
`DEF-087`). It stays vacuous for the OLDER slices, whose items have none. ⛔ Slice-scope `defects-closed`
IS still effectively blind: almost no defect row carries `found_in`. ⚠ **Count the NULL-`slice_id` items
from `data/wbs_items.jsonl`; do not re-quote a number from here** — the one that used to sit in this
sentence had the right numerator and a stale denominator, which is exactly what `LL-015` is about. **A rule that cannot fail is not a green light**; this is the shape this project keeps finding in
instruments, living inside the package's own controls. See §6.

### The mechanical guarantee, and the token rule

`gate_run()` returns **7/7** and that is the norm (tamheed ≥ 4.4.2). **A red gate is a REAL finding —
read its failure list, it names the token.** ⚠ **Journal text is EXEMPT** (`progress_entries.entry`,
`audit_verdicts.evidence`), so a progress note may quote marker tokens freely. **Every live ENTITY row is
still screened** — `title`, `statement`, `description` — so there, name the concept or backtick the token.

**The Approved+pinned lessons bind every session via the tool-owned note in `tamheed-package/CLAUDE.md`.**
⚠ **COUNT THEM, NEVER QUOTE A NUMBER** — this sentence carried "Ten … (`LL-001`…`LL-010`)" long after it
stopped being true, and the range form is worse than the count because superseded rows leave gaps.
`entity_query("lesson", status="Approved")` is the answer. The three below earned themselves within days of
being written and are repeated because they keep firing. ⚠ **Three more were added and pinned since and are
NOT restated here** — read them from the register: `LL-013` (a mutation check has two subjects; a passing
mutant proves nothing), `LL-014` (registry metadata cannot rank correctness; "the technique works here"
never transfers to "this package works here") and **`LL-015` (a scan's SCOPE is part of its answer — a
scanner that runs, has a subject, and returns a TRUE number about the WRONG SET reads exactly like a
finding, and `LL-013`'s fault-injection CONFIRMS it rather than catching it)**. ⚠ **`LL-016` is the newest
and it is about THIS FILE**: a sequence in prose is invisible to every mechanical check, and **a phrase can
go stale without its number changing** — the wrong half rides in attached to a figure you *did* just measure,
which is what makes it invisible.

- **`LL-008` — sweep the registers by KEYWORD as well as by identifier.** An id-only sweep returned **zero
  hits for six of seven** candidates and would have concluded the registers were silent; the keyword sweep
  found `ADR-0035`, ratified, replacing the storage technology `NFR-027` still mandated by name.
  **Neither row contained the other's id.** It then fired twice more in three batches.
- **`LL-009` — two instruments agreeing is ONE instrument when they share a mechanism.** Two independently
  written scanners returned an identical 13-sites/7-files answer and were **both blind to the same two
  files** (C# target-typed `new(`). Corroboration requires independence, not repetition.
- **`LL-010` — when a requirement says X is the single source for Y, check X EXISTS before measuring Y.**
  `NFR-039`'s glossary is a **circular pointer between two English documents**, so its measurable clause
  is *undecidable*, not merely unverified.

### What this session landed (batches 14–21) — reference, do not redo

- **Requirements closed:** `NFR-027` (`AC-135`), `NFR-050` (`AC-136`), `NFR-034` (`AC-137`),
  `NFR-028` (`AC-138`), `NFR-023` (`AC-139`). **`NFR-026` moved to `Deferred`** by `SC-026`.
- **Defects fixed:** `DEF-095` (#298), `DEF-097` (#299), `DEF-098`, `DEF-099` (#300).
- **New deferred work:** `DW-067`…`DW-074`.
- **Scope changes merged:** `SC-025` (NFR-027 → the configured object store, per `ADR-0035`),
  `SC-026` (NFR-026 → Deferred), `SC-027` (NFR-023's `[unverified]` marker removed after `DEC-065`).
- ⚠⚠ **`DEF-099` IS THE ONE TO READ IF YOU READ ONLY ONE.** OTLP traces had **never reached Seq in any
  environment**: the endpoint was the bare `/ingest/otlp` and `OTEL_EXPORTER_OTLP_PROTOCOL` was set
  nowhere, so the exporter's gRPC default posted to a path Seq 404s. The worker separately had **no
  `OTEL_` variables at all**. **The discriminator is the transferable part:** under shipped config, 8
  `/readyz` calls grew Seq's event count 679→739 while the DB span count stayed at **exactly 72**. Same
  host, same port, same container — log path delivering, trace path not. **Verify any observability change
  by sending traffic and asserting spans MOVE, never by a clean start-up.**
- ⚠ **The operator declined to relax a requirement TWICE** — `SC-024` (`NFR-054`'s minimal-base clause →
  `DW-066`) and `DEC-066` (`NFR-019` kept, gap → `DW-074`). **The register is being kept as a statement of
  the target, not of the status quo. Do not offer a narrowing as the easy path.**

## §2 — Things already built that get re-proposed. Do NOT rebuild.

- **`SL-030` — Confidentiality ABAC for Restricted topics, with egress redaction** (#295 → `1a52dba`),
  all mutation-proven; `AV-192` is the evidence. ⚠ **`MoveTopicPriority` is deliberately UNFILTERED** —
  `BacklogPrioritize` admits Chairman/Secretary only, both committee-wide readers, and filtering would
  renumber the column as if hidden topics were absent. ⚠ **`SC-021` is Merged but `WBS-22.3` still reads
  "notification bodies"** — the SC row and its `scope_modifies` edges ARE the correction. Do not "tidy" it.
- **`SL-029` — FR-030 topic conversion with provenance** (`bcc3d00`, `b065a29`).
- ⚠ **`Timeline.tsx` IS AN HONEST EMPTY SHELL — routed, well-commented, drawing nothing, and its own
  header says so.** `FR-036`/`DW-001` stay deferred, because topics still carry no planned SPAN.
  ⚠ **`Calendar.tsx` WAS ONE AND IS NOT ANY MORE (2026-08-26, `WBS-24.2` → `65c158c`).** It renders real
  meeting markers; `DW-037` is `Done` and `FR-035` is `Implemented`. **Do not rebuild it, and do not go
  looking for a scheduled date on the Topics read model** — there is none: `Topic.Schedule` raises an
  event with zero consumers, and the view reads `MeetingSummary.scheduledStart` + `AgendaItem.topicId`.
  ⭐ **The durable half of this bullet is unchanged: A FILE EXISTING IS NOT EVIDENCE IT WAS BUILT.**
  Requirement ids in source comments are **positive-only** evidence — one such citation was a *deferral*
  note, which is the whole reason `DW-039` existed.
- **Real accessibility instruments already exist and are easy to miss:** `axe-core` is a dependency, and
  there is a jsdom axe test over 5 surfaces, a **live Playwright axe sweep in BOTH locales** with the full
  `wcag22aa` tag set, and a token-contrast test computing real WCAG luminance over 20 pairs in two
  palettes. **Check what exists before believing a "needs a browser" label.**
- **Real app-side inactivity detection exists** (`useIdleSignOut.ts`, 30 minutes, wiring mutation-tested).

## §3 — The two registers that will mislead you

- **Requirement status measures whether anyone WROTE an acceptance criterion, not whether the thing
  was built.** A requirement advances only via the AC auto-advance trigger, so one with no AC can
  never leave `Approved` however well it shipped. ⚠ **The three counts MOVE EVERY BATCH and are
  deliberately absent from this file — measure them (§1 gives the commands), never quote a written one.**
  Since 2026-08-18 the three labels finally denote different things.
- ⚠ **The `mvp` / Phase attributes record the ORIGINAL scoping and the ladder outgrew them.** Of the
  53 `mvp=0` requirements once described as "deliberately not built", **about 30 are built and
  shipped**. `SC-020` reclassified only the **24 verified absent from source**. See `LL-006`.
  ⚠ **`Deferred` is no longer a one-way label.** Four of `SC-020`'s 24 have since come back to
  `Approved` — `FR-035` via `SC-028`, and `FR-032`, `FR-154`, `FR-155` via `SC-029` — because their
  deferred-work rows were activated and **a requirement labelled not-in-v1 beside `Activated` work is a
  contradiction nothing in the package can see.** Measure the Deferred set; do not assume it only grows.
- **`DEF-012` is Won't-fix** (`DEC-055`): the one mechanical rule that would "fix" `v_backlog` closes
  `WBS-20.4`, the email adapter, against a hard constraint.

## §4 — Traps. Every one has cost real time here.

### A — Before you call something broken, or built

1. **Read the implementation.** Rows read as pre-checked; they are not. `DEF-062/061/065/071/041`
   and `AV-159` were each wrong in my own hand. ⚠ `AV-159`'s shape: **"I searched for X and found
   none" rules out X, never the family.**
1b. ⚠⚠ **AN ABSENCE IS ONLY EVIDENCE IF THE INSTRUMENT IS PROVEN PRESENT.** A parked branch once
   handed over *"both positive tests fail with an empty audit table"* — every word false. The helper
   read a NULL column and its two `NotContain` controls passed **vacuously**.
1c. ⚠⚠ **`LL-006` — A PROXY IS NOT THE ARTIFACT, AND IT FAILED FOUR TIMES IN ONE SESSION, EACH TIME
   INSIDE THE CORRECTION OF THE LAST.** A register attribute, then a register row, then a **filename**,
   then a filename again. ⚠ **`Timeline.tsx` IS AN HONEST EMPTY SHELL** — present,
   routed, well-commented, drawing nothing; its own header says so. (⚠ Status note, 2026-08-26:
   `Calendar.tsx` was the other example and is now BUILT — `DW-037` `Done`, `FR-035` `Implemented`.
   The LESSON is about a file's existence proving nothing, not about either file's current state.)
   Check **both** directions: the sweep also found `FR-032` unbuilt inside the "presumed built" group —
   and `FR-032` is now `Implemented` with `DW-033` `Done`, so that example has moved twice.
   Requirement ids cited in source comments are strong evidence of being BUILT, but the instrument is
   **positive-only** and one citation was a **deferral note** (`InvariantStatus.cs:7`).
2. **A measurement that indicts known-good code is measuring itself.** ⚠ Fired again 2026-08-18:
   `grep -c "Convert/3"` returned 2 and nearly read a deleted allowlist entry as surviving — the hits
   were the **comment explaining the deletion**. Match the quoted entry, not the substring.
3. **The LSP diagnostics panel is stale constantly** — it fired repeatedly again in batch 13, including a
   full screen of errors for `Topic.IsRestricted` and friends that had been merged and green for days, and
   twice for `vr-*.tsx` files that do not exist and were never tracked. **Build before believing it**; the
   build is the arbiter and it took ten seconds to settle every one.

### B — What your tests structurally cannot see

4. **THE PROVIDER YOU TEST ON DECIDES WHAT CAN PASS.** `DEF-066`: assigning a stream had **never**
   worked on a real database under four green suites. `Acmp.Application.Tests` and `Acmp.Api.Tests`
   are **EF InMemory**; only `Acmp.Integration.Tests` is real SQL Server, and
   `SearchProvidersFtsTests` is the **only** place any `FREETEXT` branch executes.
5. **JSDOM DOES NOT RENDER.** If a change is visual, **look at it**: a throwaway page importing only
   `main.tsx`'s stylesheets and the real components, served over **http**. ⚠ Last session this found
   **three** defects no suite could: a standalone `.sub-card` collapsing to 43px in RTL, an icon
   forcing a label onto a second line, and `aria-pressed={undefined}` **omitting the attribute**,
   silently demoting a toggle to a plain button. ⚠ A portaled `Dialog` escapes a wrapper `div`, so
   set `dir` on **`<html>`** as the app does — otherwise the RTL reading is a harness artifact.
5b. ⚠ **AN UNDEFINED CSS *CLASS* IS AS SILENT AS AN UNDEFINED CUSTOM PROPERTY.** `.sub-cards-2` does
   not exist (only `.sub-cards`, already 2-column, and `.sub-cards-3`). Grep `styles/` for a class and
   `tokens.css` for a variable **before** writing either (`DW-031`).
6. **Coverage catches unread state but never an uncalled method** — `DW-026`, fired seven times.
7. **A requirement typed to a RESOURCE is invisible wherever that resource type is absent**
   (`DEF-068`). ⚠ This fired again on `SL-030`: adding `ConfidentialityRequirement` to a policy broke
   **every** `Topic.Edit` cell until the test's `StubTopic` implemented the new contract. **A policy
   may join a `*Scoped` set only if EVERY call site passes a matching aggregate** — which is why
   `TopicTriage` is excluded (endpoint-level on `/close`, `/reopen`, `/reactivate`, `/convert`).

### C — Proving things

9. **The test must fail without the change.** Mutation-check every guard and name which test fails.
10. **A mutation nothing catches is a decision nobody recorded.**
11. **A HOLLOW PASS IS WORSE THAN AN `indeterminate`.** ⚠ Slice-scope `defects-closed` is
   `indeterminate` — almost no defect row carries `found_in` (`DEF-087`). The **superset** answers only
   part: package-scope `defects-closed` passing rules out open **critical/high** and says nothing about
   medium/low, which live in the `defects-minor` advisory. Read both.
12. **A green exit code can come from a run that checked nothing.** Confirm the mutant **COMPILED**
   — a mutation run that silently failed to apply reports a clean pass.

### D — Writing to the package

13. **Build payloads from `data/*.jsonl`, never from `entity_query` output** — v4 needs FULL rows and
   the store holds **truncated** ones.
13b. ⚠ **PROVEN 2026-08-18: OMITTING A NULLABLE FIELD *PRESERVES* IT; NOT NULL FIELDS ARE REQUIRED.**
   `{type, id, lifecycle_status}` alone is refused (`NOT NULL constraint failed: requirements.kind`),
   but sending only the NOT NULL columns preserves `statement`, `priority`, `rationale`,
   `verification_method`, `custom_attributes` **byte-identically**. A status-only requirement update
   needs just `id, kind, title, mvp, lifecycle_status, source_kind, source_span, introduced_in`.
14. ⚠ **A GENERATED payload must be PASTED, not RE-TYPED** (`LL-001`). Where composition is
   unavoidable, **hash a pre-image and assert byte-identity after** — done twice last session over
   24 requirements and 4 WBS items, both identical.
14b. ⚠⚠ **APPROVING A LESSON IS GUARDED IN TWO WAYS THE OTHER REGISTERS DO NOT HAVE, AND BOTH ARE
   FRIENDS.** (a) *Approval is not an edit* — `entity_upsert` refuses unless the content you send is
   **byte-identical** to the stored row (*"content drifted on [...]; send the stored content
   byte-identical, or supersede first"*). (b) *Attribution lands WITH approval* — it refuses again
   without `confirmed_by`, which **can never be added later**. ⭐ Together they make trap 14's dangerous
   middle SAFE here: re-typing a long field in order to PRESERVE it fails LOUDLY instead of silently
   enshrining a corrupted lesson that is immutable from that moment. **Elsewhere there is no such guard —
   hash-and-verify, or rewrite openly.**
14c. ⚠ **A `deferred-work` row's `title` is NOT NULL, so closing one needs a full row.** Rewriting the
   title openly (the row's state really did change) beats re-typing 4000 characters to keep them
   identical; `activation_trigger` is nullable and **preserved by omission** — verified, 988 chars intact.
15. **Omitting `custom_attributes` PRESERVES it; sending it REPLACES the whole blob.**
16. **Run `gate_run()` AFTER writing.** ⚠ Creating an AC ahead of its build turns `G-PROGRESS` RED —
   record a verdict in the same session (`Pending`, or `Partial` when part is genuinely done).
16b. ⚠ **`G-TRACE` WANTS THREE LEGS WHERE THE ADVISORY WANTS ONE.** A new `mvp=1` requirement must
   link to a **decision-or-ADR**, a **wbs-item-or-slice**, AND a **test**. Wiring two clears
   `requirements_unwired` while `G-TRACE` stays red, which reads like the fix not working.
16c. ⚠ **`acs-met` COUNTS BY `retired_in`, AND IGNORES `lifecycle_status` ENTIRELY.** A `Deferred` AC
   still counts; only retirement removes one. So **writing ACs for unbuilt work holds readiness false
   forever** — this is why `DW-029` cannot be executed as one bulk pass.
16d. ⚠ **SLICE-SCOPE `wbs-done` IS VACUOUS FOR EVERY SLICE WHOSE ITEMS PREDATE `DEC-068` d2**
   (`DEF-087`): their `wbs_items` carry `slice_id` NULL, so the rule returns zero rows for them and
   **breaks the obvious AC→slice derivation**. ✅ It is NOT vacuous any more for slices built since —
   `SL-032` proved it adjudicates. **New WBS rows must set `slice_id`**; that is the whole fix.
16e. ⚠ **`RELATION_RULES` REFUSE PLAUSIBLE EDGES.** `lesson --learned_from--> deferred-work` is
   rejected (allowed targets: decision, defect, progress-entry, risk, slice, wbs-item). The error is
   the only documentation, and **the whole batch rolls back** — re-send the entire corrected batch.
   ⚠ **FIRED AGAIN 2026-08-30 ON A SECOND SHAPE: `scope-change --scope_adds--> test` IS REFUSED** (allowed
   targets include requirement, wbs-item, slice, acceptance-criterion — not `test`). Use `relates_to`. The
   batch that carried it rolled back six VALID edges with it, so the cost of the refusal is the whole write.
16f. ⚠⚠ **A NEW `mvp=1` REQUIREMENT NEEDS A `test` ROW TO EXIST BEFORE `G-TRACE` GOES GREEN** (trap 16b's
   third leg). If no suite covers it yet, create the row as **`Proposed`** with verdict `Pending` and say IN
   the row that it is PLANNED AND NOT WRITTEN — seven rows already sit at `Proposed`, so this is the register's
   own idiom, not an invention. ⛔ It must never read as evidence that a test exists.
16g. ✅ **`slice.objective` IS NULLABLE AND PRESERVED BY OMISSION** — verified 2026-08-30 when `SL-033` closed:
   a status-only upsert carrying `id`, `title`, `phase_id`, `sort_order`, `introduced_in` and
   `lifecycle_status` left its 3329-character objective byte-length-identical. **Closing a slice does not
   require re-transmitting its objective**, which is worth knowing before you re-type one (`LL-001`).
17. ⚠ `progress_entries` is **append-only** — correct via a `correction` event, never an edit.
17b. ⚠⚠ **BRANCH TOPOLOGY IS PACKAGE TOPOLOGY (C31).** The package lives in the git working tree. Do
   package writes on the branch you will merge, and **merge `main` in FIRST** when the branch predates
   a package-only commit. Commit `tamheed-package/data` immediately after every write batch.

### D2 — Batch 13's additions

32. ⚠⚠ **SWEEP `adrs`, `decisions` AND `open_questions` — NOT JUST `requirements`.** `LL-005` is usually
   read as "check the requirement register". Batch 13 did exactly that, thoroughly, and still offered the
   operator a build that would have reversed **`ADR-0037`'s decision clause**, whose own consequences name
   the fix as deferred under **`OQ-061`**. What caught it was reading `docker-compose.cloud.yml` before
   writing code and seeing a bare ADR id in a COMMENT. **Sweep all three registers before writing a `DW-`
   row or putting an option in front of the operator.**
33. ⚠⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY.** The `NFR-021` census read
   as a **38-command server-side-validation hole** — the shape of a critical security finding. All four
   commands that actually carry scalar input are guarded **in the domain** instead (`SetTimebox` clamps
   5..120, `MoveItem` bounds-checks the index, `RecordActualMinutes` floors at 0, `DeleteRecording` uses its
   string only as an EF equality predicate). Filing off the census alone would have been wrong.
34. ⚠⚠ **COVERAGE MUST BE UNIONED ACROSS PER-PROJECT REPORTS, NEVER SUMMED.** A file appears in several
   projects' cobertura reports, unexecuted in most. Summing reported **20%** for a file the gate correctly
   scored **100%**. `check-coverage.mjs` says it unions; believe it over your own ad-hoc script. Trap 2's
   exact shape — the measurement indicting known-good code was the broken thing.
35. ⚠⚠ **HANGFIRE'S `JobStorage.Current` AND `GlobalJobFilters` ARE PROCESS-GLOBAL, and a second
   `BackgroundJobServer` in one process does not reliably pick up work.** A filter test that ran a real
   in-memory server passed ALONE and reported **zero spans** in the full suite — which reads exactly like a
   broken filter. If you need a real Hangfire server, expect that; a test green alone and red in the suite is
   worse than no test. `WebexJobDeadLetterTests` already owns the serialized collection.
36. ⚠ **HANGFIRE NEVER HANDS A FILTER THE JOB'S OWN EXCEPTION** — it wraps it in a `JobPerformanceException`
   whose message is the same fixed sentence every time. Record `InnerException`, or every failed job gets an
   identical, useless error tag.
37. ⚠ **A PACKAGE THAT DOES THE JOB MAY NOT BE SHIPPABLE.** `OpenTelemetry.Instrumentation.Hangfire` has
   never had a stable release (`1.17.0-beta.1`). Check for a stable version BEFORE designing around a
   package, and treat "prerelease dependency in a production image" as the operator's call.
38. ⚠ **DO NOT PUSH TO A BRANCH WITH CI IN FLIGHT.** For `pull_request` events GitHub evaluates
   `paths-ignore` against the WHOLE PR diff, so once a PR touches `src/` even a package-only commit re-runs
   everything and cancels the in-flight e2e. Batch 13 cost itself a full ~20-minute e2e cycle this way.

### E — Environment

18. **`.cs` files need a UTF-8 BOM and LF**; the Write tool adds neither. ⚠ `git status --porcelain`
   collapses untracked directories — use `-uall`.
18b. ⚠ `dotnet ef migrations add` rewrites the model snapshot as **CRLF** against this LF repo.
   Normalise the migration, its Designer and the snapshot to **BOM + LF** before building.
19. ⚠ `gh pr create --body` and `git commit -m` with backticks break under PowerShell — use
   `--body-file` / `-F <file>`. ⚠ A `git commit -F -` **heredoc on stdin** can trip the
   no-verify guard hook; write the message to a file instead.
   ⚠⚠ **BUT NOT TO A FIXED PATH — THIS TRAP AS WRITTEN IS WHAT PRODUCED `LL-021`.** `git add … && cat >
   /tmp/c4.txt <<EOF … && git commit -F /tmp/c4.txt`: the `git add` failed, so the heredoc **never ran**,
   and `git commit -F` found a **leftover `/tmp/c4.txt` from a session five days earlier** and used it.
   Correct files, a message describing a different feature on a different branch, and **no error
   anywhere**. ⭐ **An `&&` chain fails CLOSED for the command that breaks and OPEN for any later command
   that reads a file the chain was supposed to write.** Use `/tmp/name-$$.txt`, `rm` it after, and read
   back `git log -1 --format=%s`.
22. ⚠ The coverage gate is **per-file ≥95%**, and the line a new feature most often misses is the
   **validator**. `rm -rf tests/*/TestResults` before trusting a local run (`DEF-069`).
22c. ⚠⚠ **`npm run build` IS ONLY AN ARBITER WHEN `node_modules` MATCHES THE BRANCH'S LOCKFILE, AND A
   CHECKOUT DOES NOT CHANGE `node_modules`.** Diagnosing `DEF-106` I checked out `main`, ran the build,
   saw the same 16 errors and concluded main was broken too — while the tree on disk was still the
   *other* branch's install. **That experiment was invalid and its conclusion was published before it was
   caught.** After `npm ci` on each side: main passes (231 packages), the branch fails (169), and the
   branch source against MAIN's tree **passes** — identical source, opposite verdicts. **`npm ci` before
   you believe a build, and say which tree you built against.**
22b. ⚠⚠ **THE SPA TYPECHECK HAS TWO ENTRY POINTS AND ONE OF THEM CHECKS NOTHING.**
   `npx tsc --noEmit -p tsconfig.json` **exits 0 over a tree that does not compile**, because
   `src/Acmp.Web/tsconfig.json` is solution-style: `"files": []` plus project references. A clean scan
   with no subject (`DEF-091`). `vitest` will not catch it either — it transpiles per file and never
   typechecks, so 1241 tests passed over 13 real type errors for ten commits. **Use `npm run build`
   (`tsc -b && vite build`, exactly what CI runs) or `-p tsconfig.app.json`, and prove your checker has
   a subject by injecting a deliberate error and watching the count move.**
23. ⚠ **`gh pr checks --watch` AND `gh run watch` BOTH REPORT SUCCESS ON UNFINISHED RUNS.** Poll the
   `status` field until `completed`, then read `conclusion`; treat a 503 as **unknown**, never success.
   ⚠ Fired again 2026-08-25 as a **TLS handshake timeout** from `gh pr checks` — same rule, different
   shape: ANY transport failure is unknown. Re-read before acting; it succeeded on the retry.
23b. ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND, NOT YOUR GATE'S.** Fired again 2026-08-18:
   reading it bare caught a real `IMPORTS` failure `dotnet format` would have hidden behind `tail`.
   Redirect to a file and read `$?` on the bare command.
24. ⚠ **NEVER `git checkout -- .` WITH UNCOMMITTED WORK.** Commit first, then mutate against the commit.
25. ⚠⚠ **`gh pr merge --delete-branch` CAN MERGE REMOTELY AND STILL FAIL LOCALLY.** It did again on
   #293: squash-merged, then aborted with *"Not possible to fast-forward"*, leaving the tree on a
   pre-feature `main` that **looks exactly like lost work**. Check `gh pr view --json state` first,
   verify the content is in `origin/main` **by content, not ancestry**, then `git reset --hard`.
26. **A log tail is the wrong instrument for a failure whose distance you do not know** (`DEF-074`).
27. ⚠ **NOTHING A LATER SESSION MUST READ MAY LIVE IN THE SCRATCHPAD.** Repository or package, never
   scratchpad. ⭐ **It fired and was discharged on 2026-08-25:** the `DW-082` triage script had lived only
   in a scratchpad and was LOST, so the worklist had to be re-derived. Three instruments are now committed
   instead — `src/Acmp.Web/scripts/coverage-triage.mjs`, `scripts/count-prompt-ids.py` and
   `scripts/gen-lesson-docket.mjs`. **If you build an instrument worth trusting once, commit it.**
27b. ⚠ **A PLUGIN RELOAD ORPHANS THE PACKAGE LOCK.** `/reload-plugins` kills the MCP server process while
   `data/.lock` still names its pid, so the next `package_open` refuses with "another writer owns this
   package". **Do not just delete it.** The operator guide's two discriminators decide: an **identity**
   failure (the named pid is not running) OR an **ordering** failure (the process started AFTER the lock's
   `taken_at`). Either one proves staleness. On Windows check with PowerShell — `Get-Process -Id <pid>` —
   because Git Bash mangles `tasklist /FI` into a path. Then remove it deliberately and say why.
28. ⚠ Delete throwaway visual-verify harnesses before committing — `vr-out/` is gitignored but a
   `vr-*.tsx` in `src/` is not, and it would ship.
29. ⚠⚠ **A SCRIPT'S PATHS MUST NOT BE cwd-RELATIVE — CI RUNS IT FROM SOMEWHERE ELSE.** `ci.yml`'s
   frontend job sets `working-directory: src/Acmp.Web`, so a scanner rooted at `'src/Acmp.Web/src'`
   resolves to `src/Acmp.Web/src/Acmp.Web/src` and either crashes or, far worse, **reports a clean tree
   over ZERO files**. Resolve from the script's own location (`fileURLToPath(import.meta.url)`), and
   **run the script the way CI runs it**, not only from the repo root. Same family as trap 22b.
29b. ⚠⚠ **`ConfigurationSetting.Create` NORMALISES EVERY KEY WITH `Trim().ToLowerInvariant()`, SO A READER
   MATCHING A camelCase STRING FINDS NOTHING AND FALLS BACK TO ITS DEFAULT — SILENTLY.** Cost a cycle on
   `WBS-26.2`: `anomaly.bulkExport.rowCount` stored as `anomaly.bulkexport.rowcount`, both thresholds
   defaulted, and the failure presented as *a threshold that cannot be configured* — `DEF-110`'s shape
   arriving through the back door on the very control built to avoid it. ⭐ It is `LL-033`'s family: a lookup
   whose spelling does not match the corpus's returns a confident nothing, and the fallback made it quiet.
   **Lower-case every configuration key at the point you declare it.**
30. ⚠⚠ **A SCANNER YOU WRITE CAN MEASURE ITSELF.** The first hardcoded-string scanner used
   `/>([^<>{}]*)</` and matched straight through TypeScript: `day >= 1 && dayNum <` read as a JSX text
   node. It reported FIVE findings and **all five were the bug** — acting on the count would have meant
   "fixing" five pieces of correct code. Before trusting a new instrument, inject a KNOWN-POSITIVE and
   confirm it is found, then confirm the tree is clean without it.
31. ⚠ **A GATE WITH NO SUBJECT MUST FAIL, NOT PASS.** Write the guard INTO the tool: refuse to report
   a clean result over an implausibly small file set. `scripts/check-hardcoded-strings.mjs` exits
   non-zero below 50 components for exactly this reason. Same lesson as `DEF-078`'s healthcheck that
   evaluated zero checks and `.gitleaks.toml`'s allowlist that exempted every markdown file.

## §5 — Definition of done

Unit + integration tests, each guard proven by **forcing its refusal** and verified to fail without
the change · flip AC verdicts via `audit_record` with evidence, `verified_by`, `verification_method`
and `against_commit`, and say plainly when something is ANALYSIS rather than measurement ·
authorization enforced server-side, `AuditEvent`s asserted as **ROWS** · no hardcoded strings, EN + AR
together, RTL verified · **if it is visual, look at it** · no secrets · `progress_update` +
`work_bind`, then `gate_run()` and `export_html()`; **commit `tamheed-package/data` immediately** ·
conventional commits, small and reviewable · branch → PR → green CI → squash-merge ·
**register every finding as a Tamheed row AS YOU GO — including findings against your own work.**

⚠ Before offering the operator a disposition for a capability, **sweep `adrs`, `decisions` AND
`open_questions` as well as `requirements`** (`LL-005`, and trap 32 — sweeping only the requirement register
is what nearly reversed an Approved ADR). ⚠ Scope changes, waivers and `force` are the operator's alone, and the interview
runs **every** time (`LL-002`) — plan approval is not scope-change approval.

### ⚠ Rules the `DW-029` programme paid for — they outlived it, and they override habit

The programme is closed, but these were expensive and they generalise to any verification work here.

- **Never leave a Pending or Partial acceptance criterion.** `acs-met` counts by `retired_in` and ignores
  `lifecycle_status`, so an AC written ahead of its evidence holds package readiness false **forever**.
  Write the AC and record its verdict in the SAME batch, or do not write the AC.
- **A part-verified requirement gets a `DW-` row, not an AC.** If you cannot evidence the WHOLE criterion
  today, a deferred-work row with a real activation trigger is the honest instrument — and say so IN the
  row, so the next session does not "helpfully" add the missing criterion.
- **Say what you are NOT claiming.** Every verdict this session names clauses it deliberately does not
  cover. A verdict that quietly covers less than its criterion is the failure the programme existed to end.
- ⚠ **COUNT THE REQUIREMENT'S CLAUSES BEFORE YOU COUNT YOUR FINDINGS.** Twice in three batches, strong
  evidence covered two-thirds of a three-part requirement and nearly carried a `Met`: `NFR-037` (31
  locale-aware date sites — and **exactly two** `Intl.NumberFormat` sites, while the text says *"date,
  time, **and number**"*) and `NFR-033` (a genuinely good contrast gate covering **one of two thresholds
  and none of four states**). **Evidence that is strong is not evidence that is complete.**
  ⚠ **HISTORICAL AS OF 2026-08-26 — the `NFR-037` half is CLOSED** (`WBS-24.4`, `AC-147` `Met`, the
  requirement `Implemented`), so *"exactly two `Intl.NumberFormat` sites"* describes the codebase BEFORE
  that commit and is no longer a live measurement. **The lesson is untouched and is why the row existed
  at all**; this is a dated annotation, not a correction, so the tally above does not move (`DW-084`'s
  precedent: an outcome arriving is not a statement going stale). ⚠ **Do not re-derive the date-side
  figure from this sentence either** — measured 2026-08-26 with the pattern stated, `new
  Intl.DateTimeFormat(` outside tests returns **30**, not 31; `AC-147` records why the two disagree and
  says plainly that they were not reconciled.
- ⚠ **A surface that DOES NOT EXIST can be excluded BY NAME in a `Met` verdict; a surface that EXISTS but
  is unproven CANNOT.** That single line decided every borderline call this session.
- ⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY.** An `NFR-021` census read as a
  38-command validation hole; all four commands carrying scalar input were guarded in the domain instead.
  An `NFR-039` census produced **76** Arabic divergences — but **Arabic adjectives agree in gender**, so 55
  were morphologically correct and the remaining 21 were candidates, not defects.
- ⚠ **Before offering the operator a disposition, sweep `adrs`, `decisions` AND `open_questions` — by
  KEYWORD as well as by id** (`LL-005`, `LL-008`). ⚠ And **sweep BEFORE the interview, not after**: the
  `NFR-023` question went to the operator before its sweep, and the sweep would have changed the question —
  the register already held two answers nobody had connected to it.

✅ **BRANCHING IS SETTLED — stop asking. ⚠ AMENDED 2026-08-25 (`DEC-077` d2); READ THE AMENDMENT.**
Operator decision, 2026-08-20: **split by content.** Package, prompt and memory writes go **straight to
`main`**; **anything touching CODE goes branch → PR → green CI → squash-merge.** PRs #296/#297 are the
original examples.

⚠⚠ **THE AMENDMENT, AND IT EXISTS BECAUSE THE ORIGINAL RULE'S JUSTIFICATION WAS NARROWER THAN THE RULE.**
The allowance rests on both workflows path-ignoring `tamheed-package/**` and `.claude/**` — *"so they
cannot redden anything"*. **But `scripts/*.py` and `scripts/*.mjs` are NOT path-ignored.** A commit that
is package-and-prose PLUS one instrument runs the **full** pipeline, and on 2026-08-25 exactly such a
commit (`6bdaac4`) left `main` **red** while its author reported the state as clean (`DEF-108`).
**a. `scripts/**` IS CARVED OUT — it goes branch → PR → green CI, like any other code.**
**b. AFTER *ANY* DIRECT PUSH TO `main`, POLL CI TO COMPLETION** — `status` until `completed`, then read
`conclusion` — **whatever the commit touched.** Not a judgement call about whether it "could" redden.
⛔ **DO NOT PROPOSE ADDING `scripts/**` TO THE PATH-IGNORE. It was offered and REJECTED**: several
`scripts/*.mjs` **are** the CI gates (`check-coverage`, `check-i18n`, `check-vulns`,
`check-hardcoded-strings`), so ignoring that path would mean **a change to a gate never runs the gate.**

## §6 — THE CARRIED LIST. This section IS the list; nothing is carried in conversation.

**Reconcile this section whenever you close one — a list nobody maintains is worse than no list.**

### ⚠ WHAT THE 2026-08-20 DISPOSITION SESSION DID — context, never the next action

⛔ **THIS HEADING SAID *"(that is above)"* AND THE NEXT ACTION IS BELOW IT — the FORTY-SECOND.** The
numbered list under `START HERE` sits further down this same section. **Never point at a direction; name
the anchor:** the next action is the numbered list under `START HERE`, and nowhere else.

**Item 1 of the old list — the deferred-work disposition — IS DONE, and it went differently than the
bucket table predicted.** The whole slate is a published artifact carrying the full canonical text of every
record it cites; the register carries `PE-556`, `PE-558`, `DEC-067`, `SC-028` and `SC-029`.

⚠ **The batch-21 bucket table that used to sit here was DELETED, not updated.** Re-deriving the grouping
produced a different split on **twelve** of its ids, because the right axis is **who adjudicates**, not
where the work lives — the demand-triggered rows are adjudicable *immediately*, by the operator, and
belong nowhere near a bucket labelled "no". Re-derive again if you need it; do not restore a table.

**WHAT THE OPERATOR DECIDED, and the two that matter most are counter-intuitive:**
- ⚠⚠ **ALL TWELVE demand-triggered rows are `Activated`** (`DEC-067` / `SC-029`) — `DW-028 032 033 035 036
  038 039 040 061 063 068 069`. **This was AGAINST my recommendation to carry them**, and the rows record
  it as an override, not agreement. **v1 is materially larger than it was.** Three requirements returned
  `Deferred`→`Approved` in the same delta (`FR-032`, `FR-154`, `FR-155`) — mandatory, not tidying, see below.
- **`DW-037` was already activated a day earlier and nobody applied it** — `DEC-064` d2, Approved
  2026-08-19, reads *"DW-037 is ACTIVATED… It becomes real work"*, and the row still read `Open`. `SC-028`
  applied it and returned `FR-035` to `Approved`.

⚠⚠ **THE STRUCTURAL LESSON, and it will fire again: A REQUIREMENT'S `lifecycle_status` AND ITS `DW-` ROW'S
`lifecycle_status` ARE UNRELATED COLUMNS AND NOTHING COMPARES THEM.** No gate, no readiness rule, no view.
So a requirement reading `Deferred` (= not in v1) beside an `Activated` row for the same work is invisible
and simply sits. Found once, then found three more times in the same session by checking BEFORE writing.
**Whenever you activate a `DW-` row, check its requirement's status in the same breath.**

⚠ **`deferred-work-reviewed` CANNOT GO GREEN FROM REVIEWING** — read the predicate, don't assume it: it
selects `Open` **`Activated`** and `Scheduled` alike, with no "reviewed" field. Only *closing* removes a
row. Activating twelve did not remove a single row from it. **A review's deliverable is the recorded
judgement, not a colour change**, and the advisory staying red afterwards is correct.

**Item 2 — the blind controls — is HALF done.**
- ✅ **`assumptions-current` now DISCRIMINATES.** Twelve rows dated; it moved `indeterminate` → `fail`,
  naming **`ASM-011`** alone — the urgency SLA thresholds whose own text says they must be validated with
  the committee after the PH-1 pilot, which shipped long ago. ⚠ **The field is a FUTURE re-validation DUE
  date** (read the rule's SQL, not its description), so more will go red as dates pass. **That is the
  control working — do NOT clear dates to restore the amber.** The rest correctly carry no date — **count
  them from `data/assumptions.jsonl`.** The number that sat here said nine, against a 17-row register with
  twelve dated; it never closed, and **nothing anywhere checks the prose arithmetic in this file.**
- ⛔ **`DEF-087` IS UNTOUCHED and still needs its own clean context.** Its own row warns the obvious
  mechanical fix closes `WBS-20.4`, the email adapter, against a hard constraint — the `DEF-012`/`DEC-055`
  trap. **Do not fold it into a broad session.**

**ALSO SETTLED IN THE SAME INTERVIEW — all three flagged items were dispositioned, not carried:**
1. ✅ **`ASM-001` → `Superseded`.** Its statement is struck through and reads *"RESOLVED — FALSE
   (`ADR-0015`)"*; it had been sitting at `Approved` — a settled question wearing a live assumption's status.
2. ✅ **`DEF-092` widened AND the assumption half REPAIRED.** ⚠⚠ **I told the operator "four" and it was
   EIGHT** (`ASM-001 004 006 008 011 014 015 016`). **I measured inside the twelve rows I happened to be
   editing instead of across the register — measuring inside the set you are already holding is not
   measuring the register.** All eight titles restored from their statements after verifying the
   precondition (every statement starts with its truncated title; 8 undamaged rows already have
   title == statement). ⚠ `ASM-017` deliberately untouched — its 73-char title is real prose, and a
   mechanical "title differs from statement" rule would have destroyed it. **The requirement half is
   unchanged:** `NFR-006`, `FR-032`, `FR-153` have title AND statement cut, so no intact source exists.
3. ✅ **`DW-029` → `Done`**, under `DEC-064` d1's precedent (closed because the work SHIPPED). ⚠ Its
   trigger now states what Done does NOT mean: criteria were never written for all 162, it ran to the
   operator's end condition, and **its structural point still stands** — requirement status measures
   whether anyone WROTE a criterion. Making that authoritative needs a NEW row, not reopening this one.
4. ✅ **`LL-011` Approved and PINNED**; `lessons-confirmed` passes. ⚠ **`LL-007` AND `LL-012` ARE NOW
   `Superseded` BY `LL-013`** (operator, 2026-08-21: merge them). The store REFUSES an in-place edit of an
   approved lesson — *"approved/promoted lessons are immutable: supersede, never edit"* — so a merge is
   always a new row superseding the old ones. **Count the binding lessons, do not quote a number.**
   ✅ **`DEF-082` carried, NOT reconstructed** (operator's call): a plausible reconstruction from
   second-hand narrative reads exactly like a record. `DEF-101` documents the honest gap.
5. ✅ **Tiers one and three CONFIRMED by the operator — all 41 rows carried.** With the twelve activated,
   **every open row carries a recorded human judgement for the first time.**

### ▶ HISTORY — `SL-033`: HOW IT WAS SCHEDULED, AND WHAT EACH ITEM TAUGHT

⛔ **THIS HEADING READ *"`SL-033` IS THE LIVE SLICE"* AND THAT WAS THE FORTIETH's THIRD INSTANCE.** `SL-034`
was created, executed and CLOSED after it, so the heading named a slice that had been superseded as the
live one — and the NINTH fix already found that a session **acts on a heading even when the body below it
says otherwise**. ⚠ This is the THIRD time this one heading has gone stale: it also read *"START AT
`WBS-24.1`"* (the THIRTY-SIXTH's third instance). **Nothing in this section is the next action; the
numbered list under `START HERE` is.**

⛔ **THIS HEADING USED TO READ “START AT `WBS-24.1`” AND THAT IS THE THIRTY-SIXTH'S THIRD INSTANCE.**
`WBS-24.1` has been `Implemented` since 2026-08-24 and everything through `WBS-24.7` has followed it,
so the heading pointed an arriving session at finished work. The `(state as of …)` it carried did not
save it — an IMPERATIVE with a date attached is still an imperative, exactly as the THIRTY-THIRD found
that a count with a disclaimer attached is still a count, and the NINTH fix found a session would act
on a HEADING that its own body contradicted. ⭐ **The next action lives in ONE place, the numbered list
under `START HERE`; every other section describes what happened, not what to do next.**

**`SL-032` is `Implemented`** — the operator's verdict on the slice review (`PE-586`, applied `PE-588`).
**`DEF-104` is `Fixed`** (`PE-589`). **`SL-033` was created by `DEC-071`** and holds eight rows the
operator scheduled in one slice. ⚠ **Measure, do not trust this list** —
`entity_query("slice", status="Approved")` then `readiness_check(scope="slice", id=<that id>)` and `entity_query("wbs-item")`.

✅ **THE SWEEP IS DONE AND SO IS `DW-082` (2026-08-25).** ⚠ **Measure, do not
trust this block** — `entity_query("deferred-work", status="Activated")`, `gh pr list --state open`, and a
full `npm run test:cov` are the live answers.
**`DW-078` is `Done`: EIGHT of ten landed** — `#255` `#257` `#259` `#256` `#258` `#260` `#139` plus
**`#308`**, which supersedes `#135`. `main` was green after each. ⚠ **`typescript` on `main` is now
`~7.0.2`** (a compiler major landed). ⚠⚠ **`main` IS NOW BRANCH-PROTECTED** (`DEF-105`, Fixed): 9 required
checks, `strict=true` so branches must be up to date, `enforce_admins=false` so package writes still go
direct per the 2026-08-20 rule. **Every push to `main` re-stales every open PR** — do package pushes
between merge cycles, never during one.
- ✅ **`#135` IS CLOSED, SUPERSEDED BY THE MERGED `#308`** — and my first diagnosis of it was WRONG (`PE-606`). I recorded
  *"the image will not boot … production must not move to SQL Server 2025"*. **Withdrawn.** `ldd
  /opt/mssql/bin/sqlservr` reports **nothing missing in either image**: 2022 is Ubuntu 22.04 with
  `liblber-2.5.so.0`, 2025 is 24.04 with `liblber.so.2` (OpenLDAP 2.5→2.6, soname changed).
  ⚠⚠ **`deploy/Dockerfile.sqlserver` HARDCODES the `ubuntu/22.04/mssql-server-2022` package repo**, and
  `#135` changes only the `FROM` — so a 22.04 FTS package lands on a 24.04 base. **A PINNED BASE IMAGE
  AND A PINNED PACKAGE REPO ARE ONE DECISION IN TWO PLACES, AND ONLY ONE IS AUTOMATED.** Dependabot
  cannot see the coupling, so `#135` can never go green on its own.
  ⚠⚠ **HOW THE WRONG DIAGNOSIS SURVIVED — `LL-009`, walked into after quoting it the same day:**
  Testcontainers AND compose failed identically and I read that as independent corroboration. **Both
  build the same Dockerfile.** Two instruments agreeing is ONE instrument when they share a mechanism.
- ✅✅ **`DW-082` IS `Done` AND `#307` IS MERGED (`8432a1d`, 2026-08-25).** All THIRTY-TWO files pass
  `ADR-0016` — **at `8432a1d`** `npm run test:cov` exited 0 over 158 files / 1365 tests at 99.19% lines,
  zero files under 95, and `npm run build` exited 0. ⚠ **Those are the numbers AT THE MERGE, not now:**
  `SL-033` adds components and tests, so re-run the gate rather than reading them forward. CI, Security and E2E were all green before the merge. **The
  threshold was never touched.** ✅ **`#261` and `#137` are both CLOSED** — `#137` as superseded
  (`DEC-076` d2), its 4.1.10 bump having landed as 4.1.11 via `#307`. ⛔ **HISTORICAL — this sentence read
  *"the only open PRs are `#128` and `#134`"* and it is the SURVIVING TWIN of a claim corrected further
  down this same section** (see *DO NOT RE-QUOTE AN OPEN-PR LIST*): two more PRs appeared within a day,
  and `#128`/`#134` are now closed as superseded by `DW-080` phase A. **One copy was fixed and this one
  was not — the THIRTY-FIRST's shape, found by grepping for `#128` rather than for the row.** The durable
  half is only that `DEC-074` carved that pair out to `DW-080`; `gh pr list --state open` is the count.
  ⭐ The experiment that decided it (`PE-608`) is worth keeping: on `ErrorBoundary.tsx` **both** providers
  record the SAME uncalled inline `onClick` and differ only in WHERE they count it — v3 has no zero-hit
  statements so the line is credited; v4 records the statement at zero hits, which propagates into lines.
  `ADR-0016` gates on **lines only**, so **v3 rendered every untested inline handler INVISIBLE to it.**
  ⭐⭐ **EXECUTION PROVED IT HARDER THAN THE EXPERIMENT DID: FOUR of the closed files had NO TEST FILE AT
  ALL, and v3 scored every one of them ≥95%.** The old provider was not merely lenient — it reported a
  passing grade on components nobody had ever tested.
  ⚠⚠ **NEVER LOWER THE THRESHOLD** — the number would now hide real untested code, which is worse than
  the artefact it was first mistaken for, and this row's execution is the evidence.
  ⚠ **`scripts/coverage-triage.mjs` is COMMITTED** (`src/Acmp.Web/scripts/`) — run it after
  `npm run test:cov -- --coverage.reporter=json --coverage.reporter=json-summary --coverage.reporter=text`.
  It prints each uncovered line's **source text** so a cause is confirmed rather than assumed, and refuses
  to report unless three calibrations pass. ⭐ **Calibration A caught a real defect in it on its first
  run**: attributing a statement to every line of its SPAN reported `TopBar` at **100%** against a gate
  saying 82%, because a covered multi-line JSX element swallows the uncovered inline arrow nested inside
  it — it would have reported ZERO failing files against a gate listing eighteen.
⚠ **`DW-080` (the .NET 8→10 migration + `DW-066`'s base move) was never in the sweep and does NOT block
`SL-033`.** ⚠⚠ **IT IS NO LONGER "UNSCHEDULED" — `DEC-076` d3 ORDERED IT THIRD**, after `SL-033` and
before `DW-079`. ⛔ **The sentence that followed — *"it is still not a `WBS-` row in any slice"* — was the
FORTIETH's second instance.** `DEC-089` d2 made it `WBS-25.1` under `SL-034`, and both it and `DW-079`
(`WBS-25.2`) have since been built and closed. **That was the TWENTY-SECOND** — caught by a pre-handoff
read, not by a later session tripping over it. ⭐ **The durable half is the DISTINCTION, which is what the
TWENTY-SECOND was actually about: *ordered* and *scheduled as a `WBS-` row* are different states, and
conflating them is what made the original wording wrong.**
⭐ **WHAT THE METHOD BOUGHT, since it cost hours:** verifying each PR against CURRENT `main` made every
failure attributable to ONE change. A batch merge would have shown `#135` later as an unrelated-looking
flake, and `#307` as a coverage regression across twenty files with no cause.

▶▶▶ **START HERE — BUT READ THE TWO STANDING RULES BELOW FIRST. ⛔ THIS HEADING USED TO ASSERT `main`
IS GREEN AND READINESS IS `FALSE`; BOTH ARE STATES, NOT RULES, AND THE SECOND WENT FALSE — the
FORTY-THIRD. `gh run list` and `readiness_check("package")` are the answers.**

⛔⛔ **A RED FROM `SearchProvidersFtsTests` IS REAL. STOP AND INVESTIGATE — DO NOT RE-RUN IT** (`DEC-077`
d3). That test is the ONLY place any `FREETEXT` branch executes against real SQL Server, which makes it
the one test whose silent loss would be least visible and most expensive. ⚠ **This overrode the agent's
recommendation** (re-run once and log it); the operator took the stricter reading, as they have four
times now on a control that went red.
⚠⚠ **IT HAS BEEN OVERRIDDEN TWICE, AND THE RULE ITSELF IS UNCHANGED — READ THE DECISION REGISTER FOR THE
CURRENT COUNT RATHER THAN THIS SENTENCE.** ⛔ It read *"EXACTLY ONCE"* and that went false within a day,
which is the FORTY-FOURTH's folded member: **a count inside a rule block, where the rule is what survives
and the number is what rots.** (1) `DEC-097` d2 re-ran a failed job on PR `#330` AFTER `DEF-108` had been
dispositioned — the agent recommended an administrator merge instead. (2) `DEC-100` d2 re-ran the failed
backend job on `main` for `DEF-121` — the agent recommended against on evidential grounds, since a green
already existed on byte-identical code. **In both, the reasoning-against is preserved in the decision.**
**`DEC-077` d3 as written is unconditional and contains no exception for a dispositioned cause**, so both
were recorded as overrides on their face. ⛔ **Do not read either as precedent: the response to a red here
is still stop and investigate.**
⚠⚠⚠ **`DEC-100`'s RATIONALE FLAGS THE THING TO WATCH, AND IT IS NOW ONE STEP CLOSER: a rule overridden
twice in two days while remaining unconditional in its own text is a rule being repealed by attrition
rather than by decision.** The agent offered *re-run AND amend d3 so it stops being unconditional-in-name-
only*; that was declined, so d3 stands as written. **A THIRD override should reopen whether the rule still
says what the practice does.**
⭐⭐ **AND THE SECOND OVERRIDE VINDICATED THE OPERATOR AGAINST THE AGENT, WHICH IS WHY IT IS WORTH READING
RATHER THAN COUNTING.** The agent argued a re-run could produce no fact the register did not already hold.
**It found `DEF-122`** — a rate-limit test whose timing assumption had held for hundreds of runs. A re-run
is not a second sample of the question you are asking; it is a fresh sample of every OTHER question the
suite asks (`LL-037`, Approved).
✅ **`DEF-108` IS CLOSED — `Won't-fix`, accepted as ENVIRONMENTAL by `DEC-097` d1 (2026-08-30), under
clause (3) of the end condition `DEC-089` d1 gave it.** It had been carried `Open`/high since `DEC-077` d1
specifically so that nothing could be declared done while it stood, and that hold is now released.
⚠⚠ **WHAT SETTLED IT WAS A DIFFERENT KIND OF EVIDENCE, NOT A FOURTH TALLY MARK.** Occurrences 1–3 supported
only *no backend code changed in this commit* — a claim about a diff. Occurrence 4 fired on a tree whose
`src/` and `tests/` were **byte-identical** to one that had passed CI completely ninety minutes earlier
(`git diff` empty), with the crash signature reproducing frame-for-frame including offsets. Identical code,
opposite verdicts. ⛔ **THE TRIGGER IS STILL NOT DIAGNOSED and the closure does not rest on one** — clause
(1) is explicitly NOT satisfied. ⛔⛔ **CLOSING IT REPEALED NOTHING: a backend integration failure is still
never called flaky on the strength of one more green, and a further red from that test is a NEW defect to
investigate on its own evidence — file one, do not append to the closed row, and do not cite the closure as
a precedent.**

**THE ORDER IS THE OPERATOR'S, AND IT IS THE PRODUCT OF TWO DECISIONS — read both before reordering.**
`DEC-075` d2 put the four activated streams in scope (they answered *"all"*); `DEC-076` d3 accepted the
agent's proposed sequence for them, closing the separation `DEC-071` d3 required between what they
DECIDED and what the agent INFERRED; **`DEC-077` d5 then AMENDED it, inserting `DW-084` at the front.**
⚠ **`DW-084` IS NOT ONE OF `DEC-075` d2's FOUR STREAMS — it was created by `DEC-077`**, so the work
spanned five rows. ⛔ **A TALLY-AND-POSITION CLAIM STOOD HERE — *"Two are now done and three are numbered
below"* — AND IT WAS THE FORTY-SECOND.** Measured: `DW-078`, `DW-079`, `DW-080`, `DW-082`, `DW-084` and
`DW-085` are **all `Done`**, and none of them is numbered anywhere. **The sequence is a decision and it is
now HISTORY; what is live is the numbered list under `START HERE`.**

⚠⚠ **THE ORDER WAS AMENDED AGAIN ON 2026-08-26 BY `DEC-078` d2 + `SC-034`, WHICH INSERTED `DW-085` AT
THE FRONT** — exactly as `DEC-077` d5 inserted `DW-084`. **That was an OPERATOR OVERRIDE**: the agent
recommended carrying `DW-085` Open because its trigger has not fired, and the operator activated it. The
reasoning-against is preserved in `DEC-078`; do not read the activation as the agent agreeing.

✅✅ **`DW-085` IS DONE (PR `#310` → `590ac03`)** — the FTS image build is bounded at 8 minutes and the
failure names the build, the bound, and the fact that there is no container log to attach. ⚠ **The two
budgets were chosen TOGETHER and should not be tuned apart: 8 (build) + 10 (start) = 18 minutes, leaving
the backend job room under its own `timeout-minutes: 25` to fail, report and finish.**
⭐ **From its calibration, and it sharpens `LL-022` rather than contradicting it:** the BUILD path throws
`OperationCanceledException`, not `TimeoutException` — so there the exception-TYPE assertion genuinely
discriminates, where on the container path it was INHERITED and would have passed vacuously. **Check what
the framework already does PER CALL; the answer differs inside one library.**

✅✅ **`WBS-24.1` IS DONE-CLAIMED (PR `#311` → `f968703`)** — user-configurable backlog columns.
`AC-144` is `Met` (`AV-222`) and **`FR-032` auto-advanced to `Implemented`**. ⚠ The row was done-claimed on merge and
**no status is written here on purpose** — `entity_query("slice", status="Approved")` then `readiness_check(scope="slice", id=<that id>)` is the
live answer, and the verdict is YOURS via `prompts/slice-review.md`.
⚠⚠ **ITS SIZING WAS WRONG IN THE CHEAP DIRECTION AND THE SAME TRAP IS LIVE FOR THE OTHER SEVEN.** The
title said *"dense table … verified unbuilt"*; **the table had already shipped** and only the
CONFIGURATION was missing. `DW-033`'s own text was accurate and narrow — **the WBS title's SUMMARY of it
was the wrong part** (`DEC-064` d2's shape). **Read each row's own text, not the WBS summary of it.**
⭐ **Two defects no unit test could see:** `.table-wrap`'s `overflow: hidden` would have clipped the
popover (found by reading CSS), and the panel rendered OFF-SCREEN in **both** directions until
`align="start"` (found by looking at it — x=-123 in LTR, right edge 1345 vs a 1200px viewport in RTL).
⚠ **`DW-085`'s and `DW-084`'s guards ran clean on a real runner** inside a green Integration suite.
⚠⚠ **NEW: `DEF-109`** — `Acmp.Api.Tests` ran **20m35s / 17 failed** between two normal runs (3m18s and
2m37s, 368/368), all 100-second `HttpClient` timeouts across TWELVE unrelated classes, on a backend tree
byte-identical to both. **The mitigation cannot be credited: the run BEFORE it was also green.**
⛔ **Do not re-run a red into silence** — append a second occurrence to `DEF-109` instead.
⚠ **`DEF-110` EXISTS AND THIS FILE NEVER CARRIED IT** (added 2026-08-26, after the resume was written).
`DEC-079` d2 carried it Open at medium — *record it, change nothing*, the `DEF-102` disposition for the
`DEF-102` shape: the topic urgency SLA thresholds are a hardcoded `switch` (3/7/21) while `ASM-011` and
`OQ-035`'s recorded resolution both promise the committee will adjust them **via configuration**. ⚠ **So
`assumptions-current` naming `ASM-011` is not merely an overdue date — its remediation path does not
exist.** ⛔ Do not "fix" the advisory by re-dating the assumption; that was the wrong question and
`DEF-110` records why. ⚠ **`defects-minor` therefore grew** — `readiness_check("package")` is the list;
no count is written here, and the reason is the THIRTY-THIRD below.

✅✅ **`WBS-24.2` IS DONE-CLAIMED (PR `#312` → `65c158c`)** — the calendar shows real scheduled meetings.
`AC-145` `Met` (`AV-223`), **`FR-035` auto-advanced to `Implemented`**, `DW-037` `Done`. ⛔ A lifecycle
status stood here too — the THIRTY-NINTH's **second instance**, found in the same pass and folded into one
ordinal. `entity_query("slice", status="Approved")` then `readiness_check(scope="slice", id=<that id>)` is the answer. **What `DEC-079` d3 established is
the RULE and it is what survives: done-claimed is `Review`, and `Implemented` is the operator's verdict,
adjudicated per item.**
⭐ **`DW-037`'s own correction was load-bearing and it HELD** — the scheduled date really is not on the
Topics side. **That is the opposite of `WBS-24.1`, where the WBS title's SUMMARY of a row was the
misleading part. Read each row's own text; distrust the summary.**
⚠⚠ **A SIZING FACT NEITHER ROW ANTICIPATED: `/meetings` carries NO topic ids** — only `/meetings/{key}`
does. So the grid shows a per-meeting COUNT from one request and titles on selection. **`DW-086` records
the residual and says explicitly: do NOT fan `useMeetingDetail` across the month — that is `DEF-104`'s
N+1 shape.**
⭐⭐ **THE AXE SWEEP WAS PROVEN TO RUN, NOT INFERRED FROM A GREEN JOB:** the e2e count moved **86 → 88**,
which is **+2 for ONE added test** because `playwright.config.ts` runs `rtl-a11y.spec.ts` in **both**
`chromium` and `msedge`. **A green e2e job that never reached a new test leaves the count unchanged —
check the count, not the colour.**
⚠ **Two stale things found:** `Backlog.tsx`'s comment calling Kanban/Calendar/Timeline all "coming soon"
(only Timeline is), and a test that **passed alone and failed in the full suite** — `Backlog.test.tsx`
mounts the calendar and mocked only the topics API. **A comment about a SIBLING's state goes stale
silently; nothing compiles it.**
⚠ **Harness gotcha:** Playwright's `getByRole` matches `name` as a **case-insensitive SUBSTRING**, so
`{name:'AR'}` also hit "Regul**ar**", "Extraordin**ar**y" and "**Ar**chitecture board". Use `exact: true`.

✅✅ **`WBS-24.3` IS DONE-CLAIMED (PR `#313` → `a794daa`)** — wiki version compare. `AC-146` `Met`
(`AV-224`), **`FR-117` → `Implemented`**, `DW-039` `Done`. ⚠ **No status is written here** — `entity_query("slice", status="Approved")` then `readiness_check(scope="slice", id=<that id>)` is the live answer.
⭐ **The row was really about a SOURCE COMMENT, not a diff.** *"Diff is deferred to P14 — viewable
satisfies FR-117"* was a requirement-satisfaction judgement living where no register view could see it,
untrue as written (`FR-117` says viewable **AND** diffable), and pointed at a phase `DEC-028` deferred
INDEFINITELY. **A judgement about whether a requirement is satisfied does not belong in a code comment.**
⭐⭐ **A DEFECT NO TEST COULD SEE, FOUND IN ARABIC: the diff rendered `# Governance charter` as
`Governance charter #`**, moved trailing full stops to line-fronts and pushed `-` markers to line-ends —
bidi reordering of NEUTRAL characters in an RTL paragraph. Fixed with `unicode-bidi: plaintext`.
⚠⚠ **`white-space: pre` PRESERVES WHITESPACE, NOT CHARACTER ORDER.** Any future pre-formatted or
code-like surface — a log viewer, a JSON preview, a config panel — needs the same treatment, **and none
of them will fail a test if it is missing.**
⚠ **Widening SHARED test data to serve a new test is a change to every test that reads it** — it broke a
pre-existing exact-text assertion here; the fix was to scope the new fixture, not relax the old test.

✅✅ **`WBS-24.4` IS DONE-CLAIMED (PR `#314` → `58052b4`)** — locale-appropriate NUMBER formatting.
`AC-147` `Met` (`AV-225`), **`NFR-037` → `Implemented`**, `DW-068` `Done`. ⚠ **No status is written here** — `entity_query("slice", status="Approved")` then `readiness_check(scope="slice", id=<that id>)` is the live answer.
⚠⚠ **ITS ROW WAS RIGHT ABOUT THE PROBLEM AND TOO SMALL ABOUT THE REMEDY — a FOURTH distinct way a row
can mislead, after `24.1`'s wrong title-summary, `24.2`'s correction that held, and `24.3` being about a
source comment.** `DW-068`'s census was exact (two `Intl.NumberFormat` sites in 347 files) but its
prescription — *"extract one shared hook and route rendered numbers through it"* — reaches only ONE of the
two ways a number gets on screen. The other is a value handed to `t()`, ~50 of them, which no per-site
hook covers and no future author would remember.
⭐⭐ **THE FINDING TO CARRY: `interpolation.format` IS A SILENT NO-OP FOR THIS.** i18next OVERWRITES it
with its own `Formatter` during init, and that Formatter returns the value untouched when no format is
named. A formatter **module** plus `alwaysFormat` is what fires; both halves are mutation-proven. Keying
it off the **runtime type** is what makes it safe globally — a number localizes, an entity key and an
ADR id are strings and do not. ⚠ **The general class: A VALUE PRE-STRINGIFIED BY ITS PRODUCER IS INVISIBLE
TO A TYPE-KEYED FORMATTER** — three such bypasses existed and were closed.
⭐ **A HOLLOW PASS IN A TEST WRITTEN TO PROVE THE FIX.** The relative-time assertion was written on **−2
hours**; Arabic's **dual form** renders that with **no digit in it at all**, so it passed with or without
the numbering pin. Rebuilt on five hours. **Pick a value that actually emits the thing you assert about.**
⚠⚠ **AN `INV-014` MISS THE FIRST COMMIT SHIPPED:** the mockups draw `٤٠٪` with **U+066A**, and the app
glued an ASCII `%` onto the digits. **THE SIGN IS PART OF THE NUMBER FORMAT, NOT A SUFFIX** — `style:
'percent'` makes Intl choose it per locale. ⚠ Intl then appends **U+061C** (Arabic Letter Mark) after the
sign, so `getByText('٨٧٪')` finds nothing and the failure looks exactly like the sign being wrong.
⚠ **`scripts/number-render-scan.mjs` is committed and was WRONG THREE TIMES** — it missed the reports
family (`s.value`, `card.kpi`), then camelCase (`findingCount`), then a number carrying a literal suffix
(`{act.progressPct}%`). It now carries a calibration for each. **It is TRIAGE, not a coverage proof**, and
`AC-147` says so instead of leaning on it.
⚠ **Environment, so the gap between push and CI is not misread later:** GitHub Actions was in a
**critical outage** (incident opened 15:11Z) and no workflow fired for ~30 minutes. Backend job on the
merge run was **6m41s** with `SearchProvidersFtsTests` green — neither `DEF-109`'s signature nor
`DEC-077` d3 fired. ⚠⚠ **`jq` IS NOT INSTALLED ON THIS MACHINE.** A CI monitor built on it ran silently
and would have reported nothing at all; **silence reads identically to "still running."** Use `gh`'s own
`--jq`.

✅✅ **`WBS-24.5` IS DONE-CLAIMED (PR `#316` → `76c2dde`, 2026-08-27)** — configurable retention.
`AC-149`/`AC-150`/`AC-151` `Met` (`AV-227`/`228`/`229`), **`FR-155`, `NFR-059` AND `NFR-060` all →
`Implemented`**, `DW-036` `Done`. ✅ **`Implemented` — the operator's verdict, 2026-08-27.**
⚠⚠ **IT WAS RESIZED `S`→`L` BEFORE ANY CODE (`DEC-080` / `SC-035`), AND THE RESIZE IS THE STORY.** The
row said *"retention CONFIGURABILITY only"*. `SEC-080` names the home — *"the Configuration table
(`16` §2.15) holds retention settings for legal/compliance to set later"* — and `SEC-103` specifies it.
**It did not exist.** ⚠ Verified against a CONTROL, because the obvious grep lies: `class Configuration`
matches only EF entity-type configurations under `Persistence/Configurations/`, while `class Stream`
resolves to a real entity. **That collision is why its absence went unnoticed for months.**
⭐⭐ **THE AGENT'S FIRST RECOMMENDATION (appsettings) WAS WRONG AND WAS WITHDRAWN BEFORE THE OPERATOR
RULED. READING `src` TELLS YOU WHAT EXISTS AND NOTHING ABOUT WHAT WAS SPECIFIED** — the keyword sweep of
the NARRATIVE documents (`LL-008`) found the mechanism AND three clauses that bound the build and that no
code-reading would surface: enforcement is **Phase 2** (`SEC-089`), the period VALUES are an open
question awaiting legal (`OQ-DATA-004`), and a retention config change is a **privileged AUDITED action**
(`SEC-077`).
⛔ **A PURGE JOB WOULD *VIOLATE* `NFR-059`/`NFR-060`, NOT COMPLETE THEM.** `automaticPurgeEnabled` is a
CONSTANT reported as a fact, never a setting, so nothing can switch on a purge that does not exist. **v1
ships NO periods and that is canon** (`SEC-080`) — an AC here must evidence the MECHANISM, never a value.
⭐ **Three things the codebase decided, which beat designing them:** `Policies.AdminConfig` already
existed and already admitted Administrator alone; `AuditDbContext` was already the shape a cross-cutting
store needs under `ADR-0001`, so BuildingBlocks was the answer not a choice; and the type had to be
`ConfigurationSetting` because `SharedKernelExtensions.cs` already imports
`Microsoft.Extensions.Configuration`.
⚠⚠ **A NEW `DbContext` MUST BE SUBSTITUTED IN THREE PLACES, NOT TWO** — DI, `MigrationRunner`, AND
`AcmpWebApplicationFactory`. Omitting the third fails by reaching for a REAL SQL Server, which reads like
a broken environment rather than a missing registration.
⭐⭐ **`WBS-24.3`'s BIDI LESSON PREDICTED ITS OWN RECURRENCE BY NAME AND WAS RIGHT.** It named *"a log
viewer, a JSON preview, **a config panel**"*; in Arabic `{"years":7}` rendered `{years":7"}`. ⚠ **Its fix
does NOT transfer: `unicode-bidi: plaintext` takes direction from the first STRONG character and a JSON
fragment has none** — use `dir="ltr"` on code-like elements.
⚠⚠ **CI'S FIRST RUN FAILED AT THE *FORMAT CHECK* WITH BUILD AND TEST `skipped`** — so that red said
NOTHING about the migration (`DEF-106`'s lesson). **I had run `dotnet build` and the suites but never
`dotnet format --verify-no-changes`, which is a committed gate. RUN THE GATES THAT EXIST, NOT THE ONES
YOU REMEMBER.**
⭐ **The new tests are proven to have RUN BY THE COUNT, not the colour:** Integration **64→68** (+4) and
`Acmp.Api.Tests` **368→376** (+8). ⚠ A grep of the CI log for the test CLASS names returns zero — the log
carries only per-assembly summaries — and **my first such grep ran over a ZERO-BYTE download** and
returned a confident zero. A control term is what exposed it.

⚠⚠ **NEW AND LOAD-BEARING: `OQ-079` AND `OQ-080` (2026-08-27).** `DOC-011` carried two open questions
as **doc-local labels only** — `OQ-DATA-004` (retention periods per record class) and `OQ-DATA-003` (the
legal-hold workflow). A sweep of the `open_questions` register returned **ZERO** rows against a control
of **78**, so both were invisible to `open-questions-resolved`, `open-questions-overdue` and every
readiness view — `DEF-101`'s shape, a prose-only identifier. ⚠⚠ **Except these were LOAD-BEARING:
`AC-149`, `AC-150` and `AC-151` all state their boundaries by leaning on those questions being open, so
THE STATED SCOPE OF THREE `Met` VERDICTS RESTED ON QUESTIONS THE REGISTER COULD NOT SEE.** Filed as
`OQ-079` and `OQ-080`. ⚠ **The cross-reference runs ONE WAY only and that is a store constraint, not an
oversight** — approved ACs are immutable and refuse edits, so the new rows cite the criteria and the
criteria cannot cite back.
⛔⛔ **THE TRAP INSIDE `OQ-080`, FOR WHOEVER TOUCHES PHASE 2.** `SEC-080` ASSERTS that a legal hold
overrides any future retention or purge — and **no hold mechanism exists in the product**: no flag, no
place/release path, no audit of either. It is harmless today only because nothing purges. **Build
enforcement without it and `SEC-080`'s guarantee becomes false SILENTLY**, since nothing enforces it and
no test asserts it. **Answer `OQ-080` FIRST, not alongside.**
⚠ **`AC-147`'s DANGLING SUPERSESSION IS A RULED-ON STATE, NOT DRIFT — DO NOT "REPAIR" IT.** `AC-148`
supersedes it in substance, but `AC-147`'s `superseded_by` stays NULL because the store refuses to edit
an approved AC **at all — including to mark it superseded, which is the very path its own refusal message
names**. The operator accepted this on 2026-08-27; `DEF-111` and `AC-148`'s text are the record. `NFR-037`
therefore carries two active `Met` criteria, one containing a withdrawn exclusion.

⚠ **`LL-025` CAME OUT OF `WBS-24.6` (2026-08-27):** a plan row can be a FAITHFUL quotation of a clause an
ADR has already superseded, and **when an ADR names the rows it undertook to amend, that list is the
instrument.** ⛔ **No lesson's lifecycle status is written here any more** — that was the THIRTY-FIRST,
twice. **`tamheed-package/CLAUDE.md` is the tool-owned list of what actually binds**, rebuilt by
`handoff_emit`; `readiness_check`'s `lessons-confirmed` row says whether an interview is outstanding.
⚠⚠ **AND READING THAT NOTE IS NOT OPTIONAL AFTER AN APPROVAL — `DEF-107` IS A LESSON THAT WAS APPROVED,
PINNED AND OPERATOR-ATTRIBUTED WHILE BINDING NOTHING FOR TWO DAYS**, because only `handoff_emit` rebuilds
the note and `lessons-confirmed` goes green the instant a row is approved, propagated or not. **Run
`handoff_emit(target_dir=<repo root>)` in the SAME batch as any approval and commit the rebuilt note.**

⚠ **`LL-024`: generated code loses its escapes silently, so prove it RUNS rather than reading it.**
It cost four cycles in one session, then fired a fifth and a sixth time. ⛔ **Do NOT write a lesson's
lifecycle status here** — this paragraph asserted one and it was **the THIRTY-FIRST**; `entity_query
("lesson", status="Proposed")` and `readiness_check`'s `lessons-confirmed` row are the live answer, and
whether that advisory is red is a fact to READ, never one to carry in prose.

⚠⚠ **READ THE ROW'S OWN TEXT AND THE NARRATIVE DOCUMENTS BEFORE SIZING ANY REMAINING ITEM** — that habit
has now paid on all SIX built items, in a DIFFERENT direction each time: `24.1`'s WBS title summarised its
row wrongly; `24.2`'s row carried its own correction and it held; `24.3` turned out to be about a source
COMMENT rather than a feature; `24.4`'s measurement was right while its prescribed remedy was too small;
`24.5` was ONE WORD ("configurable") covering a subsystem the architecture had already specified; and
`24.6`'s row **quoted its requirement FAITHFULLY while that requirement's clause had been superseded by an
ADR the row never mentions.** ⭐⭐ **`24.5` and `24.6` each added a STEP to the habit: read the NARRATIVE
documents by keyword, because reading `src` tells you what exists and nothing about what was specified;
and read the ADR/DECISION registers, because a row can be a faithful quotation of a dead clause.**
⚠ **Measure, do not trust this list** — `entity_query("slice", status="Approved")` then `readiness_check(scope="slice", id=<that id>)` and
`entity_query("wbs-item")` are the live answer.

✅✅ **`DW-080` PHASE A IS DONE — PR `#320` → `df8d7c3a`, 2026-08-28, ten of ten checks green.** The
solution targets `net10.0`, `SearchProvidersFtsTests` passes (Arabic `FREETEXT` against real SQL Server
works on .NET 10 — the single most load-bearing check in the migration), the coverage gate reports 461
files at **99.60%** with zero files under 95%, and `#128`/`#134` are **closed as superseded**.
⛔ **A LIFECYCLE STATUS STOOD HERE — *"`DW-080` itself stays `Activated`"* — AND IT WAS THE FORTIETH.**
Both phases shipped and the row is closed; `entity_query("deferred-work", id="DW-080")` is the answer.
⛔ This sentence has now been wrong twice in two different ways: it also used to end *"and it is item 1
below"*, which is the THIRTY-SIXTH. **A sentence that has rotted twice is a sentence that should carry no
state at all.**

✅ **`DEF-113` IS `Fixed`, AND IT COST TWO ATTEMPTS. Read it if you read one row from this slice.** The
two uncredited lines were a genuinely UNEXECUTED path, not the coverage-attribution artefact the file's
own history predicted — so `LL-017` resolved toward *the old instrument was over-crediting*, settled by a
**documented .NET 10 breaking change** (*BackgroundService runs all of ExecuteAsync as a Task*) rather
than by argument. ⭐⭐ **The test passed on both runtimes, and only the per-file coverage floor ever
noticed** — a NEGATIVE assertion is satisfied by the empty run, so 1167 green tests could not tell *ran
and correctly did nothing* from *never ran*. **That is why `[ExcludeFromCodeCoverage]` and lowering
`ADR-0016`'s 95% were the two worst available moves: both delete the only instrument that discriminated.**
The generalisation is `LL-026`.
⛔⛔ **AND THE FIRST FIX WAS ITSELF RACY — `StopAsync` IS NOT A JOIN.** It cancels the stopping token
BEFORE awaiting, and .NET 10 dispatches the body carrying that token, so a loaded runner can cancel the
work before it starts. It passed locally at 25/25 and CI rejected it. **`ExecuteTask` is the body:
awaiting it involves no cancellation, so it is deterministic BY CONSTRUCTION rather than by timing.**
⭐ **A fix for a race that passes locally because the race went your way is the defect class reproducing
inside its own remedy** — and the local machine wins that race every time, so only CI can see it.

   ⭐⭐ **FOUR FINDINGS FROM PHASE A THAT OUTLIVE IT:**
   - **`.0` OF A MAJOR IS THE LEAST-TESTED BUILD OF THAT MAJOR.** Phase A pinned every first-party
     package to `10.0.0` with eleven patches out — and had ALREADY found that
     `System.Security.Cryptography.Xml` `10.0.0` was itself vulnerable and needed `10.0.11`. The lesson
     was written into `Directory.Build.props` and twenty other packages were left on `.0` anyway.
   - **A PINNED BASE IMAGE AND A PINNED SDK ARE ONE DECISION IN TWO PLACES** (`LL-019`, with the distro
     release swapped for an SDK band). `global.json` was pinned to the LOCAL SDK and the CONTAINER was
     what broke; `rollForward: latestPatch` cannot cross a feature band. **The image is the authority,
     not the developer's box** — and no local gate models this, only building the image does.
   - **A MIGRATION'S VERDICT COMES FROM EXECUTING, NEVER FROM BUILDING.** The solution built clean in
     Release and `dotnet format` passed, then 355 of 392 API tests failed at RUNTIME — twice, for two
     unrelated causes (Swashbuckle's `TypeLoadException`, and EF 10 refusing two providers in one
     service provider). `DW-080`'s row predicted exactly this class.
   - **EF 10 REFUSES WHAT EF 8 TOLERATED, AND THE OLD GRAPH WAS NEVER LEGAL** (`DEF-112`, Fixed): one
     owned value-object instance shared across three navigations. `LL-017` resolved in the rarer
     direction — the OLD instrument was lying.
⚠⚠ **THE ORDER BELOW IS `DEC-084` (2026-08-28), AND IT SETTLED A REAL AMBIGUITY RATHER THAN RESTATING
ONE.** `DEC-076` d3 / `DEC-077` d5 / `DEC-078` d2 all read *`SL-033` → `DW-080` alone → `DW-079`*, while
`DEC-083` d1 said *"phase A … then phase B"* with `WBS-24.7` and `WBS-24.8` still open and said nothing
about them — **silent, not overriding.** The operator ruled: **finish `SL-033` first.**
⛔ **THIS FILE BRIEFLY SAID "PHASE B IS ITEM 1" AND THAT WAS THE AGENT'S INFERENCE, NEVER A RULING** — it
was disclosed as an inference on the slate (`DEC-071` d3) and `DEC-084` d3 overrode it. **Do not restore
it.** ⭐ Docker being up is **not** a reason to reorder: it is one operator action away at any time, so it
is not a scarce window — that refutation is what moved the recommendation.

✅ **`DEF-114` IS FIXED (`DEC-084` d4) — one line, and it is the Dockerfile INSTRUCTION, not the tag.**
⭐ **The deciding argument for doing it first was the INSTRUMENT, not the code: the local backend gate has
been shown to reproduce CI exactly (461 files, 99.60%), and `DEF-114` was the single thing making that
false — 65/68 locally against CI's 66/68.** ⚠⚠ **Its first diagnosis was wrong and the row is worth
reading for that reason alone** — see the THIRTY-FIFTH and `LL-029`. ⚠ **CI cannot validate this fix**: a
fresh runner has a cold cache, so the test passed there throughout. The local before/after under the true
precondition is the evidence.
✅ **`WBS-24.7` IS `Implemented`** — the operator's verdict, `DEC-085` d1. PR `#322` → `8884f4b9`, ten
checks green; `AC-153` `Met` (`AV-231`), `NFR-010` → `Implemented`, `DW-063` `Done`.
⭐⭐ **THE HABIT PAID A SEVENTH TIME, AND IN TWO DIRECTIONS AT ONCE.** `DW-063` offered a fork — *"a
create-stream command … **or** a settings-seeded reconciler"* — and neither was the answer. The NARRATIVE
sweep found the architecture had already decided the shape (`SEC-178` specifies screen 85 as *"Stream
list, edit inline, add stream button"*; `ACMP Administration.dc.html` gives the section the primary action
*Add stream*), and the CODE showed the feature was **half built** — a read-only Streams tab already
shipped, so only the write path was missing. `WBS-24.5`'s lesson and `WBS-24.1`'s shape in one row.
⚠ **`NFR-010`'s `≥20` target is STILL NOT PROVEN** and the promotion does not claim it: the evidence is at
**TEN**, the number the requirement's own verification note names. `AC-153` says so in its own text, and
`DEC-085` d1 records that the operator declined to spin the gap out into its own `DW-` row.

✅✅ **`WBS-24.8` IS `Implemented` (`DEC-087` d1) — PR `#323` → `24738d4b`, ten checks green.** `SL-033`'s
BUILD IS COMPLETE: `WBS-24` and all eight children are `Implemented`, every bound criterion is `Met`, and
slice-scope `wbs-done` and `acs-met` both PASS. `FR-165` added by `SC-037` under `DEC-086` d4;
`AC-154`/`AC-155` `Met` (`AV-232`/`AV-233`); `DW-028` `Done`.
✅ **`DEF-108` HAD A CHECKABLE END CONDITION AND IT WAS USED — the row is CLOSED (`Won't-fix`, `DEC-097`
d1, clause 3). The clause structure below is kept because it is the METHOD, not because the row is open.** It is chased when the
cause is diagnosed, OR the failure recurs under `DW-084`/`DW-085`'s bounded presentation with a log naming
a cause, OR the operator explicitly disposes of it. ⛔ **ACCUMULATED GREENS SATISFY NO CLAUSE, BY DESIGN** —
an end condition greens could satisfy would silently repeal the rule that a backend integration failure is
not called flaky on one more green. ⚠ Clause two waits on a FAILURE, so quiet time moves it no closer.
✅✅ **`SL-033` IS CLOSED — `Implemented`, `DEC-098` d2 (2026-08-30).** `DEC-088` had held it open on ONE
condition and one only — *it stays open until `DEF-108`'s cause is settled* — and `DEC-097` d1 settled it.
⭐ **THE REASON IT CLOSED IS THAT THE HOLD EXPIRED ON ITS OWN TERMS, NOT THAT THE SLICE LOOKED FINISHED** —
it had looked finished since `WBS-24.8` merged. Leaving it open once the condition was met would have been
a hold with no stated reason, which is the failure `DEC-089`'s end condition was written to prevent.
⚠ `DEF-109` stays open at medium, carried deliberately; the `defects-minor` advisory naming it is the
control working and was never a reason to hold the slice. ⚠ The operator's FIRST answer at `DEC-087` was
*"close it now"* and they reversed it once the collision with their own `DEC-077` d1 was put in front of
them — a waiver and `force: true` are theirs alone, so neither was inferred (`LL-002`).
⭐ **Greens did not discharge `DEF-108` and never could have** — its closure rests on four recorded
occurrences and two full crash dumps, not on a green, and that distinction is the whole reason the row was
allowed to close. The backend ran green twice more here with
`SearchProvidersFtsTests` passing and a full local run had Integration 69/2-skipped — offered as fact and
explicitly not as a closer, because that row conditions on the CAUSE being chased and its own text records
the rule that a backend integration failure is not called flaky on one more green.
⭐⭐ **THE SHAPE FOLLOWS FROM ONE FACT AND IT GENERALISES: `/session`'s SECURITY WAS AN *ABSENCE*.** Its own
source says there is no parameter naming a meeting, a topic or a person, *"which is a stronger guarantee
than checking that they did not"*. This item required exactly that parameter, so `DEC-086` d1 **isolated**
it — its own query (no `Guest` in `AllowedRoles`), its own endpoint group **outside** the guest path
allowlist, its own guarded route — leaving `GetMySessionQuery` untouched. **When an item asks you to add
the thing whose absence IS the guarantee, isolate it; do not make the shared path conditional.**
⚠⚠⚠ **THE FINDING TO CARRY, AND IT IS ABOUT TESTING DEFENCE IN DEPTH: A LAYER'S CORRECTNESS CAN BE
STRUCTURALLY INVISIBLE TO ANY TEST THAT ENTERS THROUGH THE FRONT DOOR.** I wrote that adding `Guest` to the
preview query's `AllowedRoles` was *"the single mutation"* that opens the door. **Measured false by applying
it: all ten API tests stay GREEN**, because the path gate intercepts guests first — the very population that
layer exists to refuse. Only a unit-level test against `AuthorizationBehavior` catches it. ⭐ **Each refusal
is therefore pinned to a DISTINGUISHABLE SIGNATURE** (a `guest_scope` header vs. an `Authorization.Forbidden`
row), because three tests all asserting `403` read as rigour while testing whichever layer runs first.
⚠⚠ **A REFACTOR CAN PUSH A FILE UNDER THE PER-FILE COVERAGE FLOOR WITHOUT ADDING ONE UNTESTED LINE.**
`GetMySession.cs` went 191 → ~155 lines when the shell moved into a shared composer: **covered** code left,
the same three early returns stayed untested, and their share crossed `ADR-0016`'s 5% budget. **The
numerator never moved; the denominator did.** Do not read such a failure as *"the new code is untested"*.
⚠ **AND I HAD NOT RUN THE GATE** — `dotnet test` per project, never `check-coverage.mjs`. That is
`WBS-24.5`'s *run the gates that exist, not the ones you remember*, repeated three days later. With Docker
up the local gate reproduced CI **exactly** (466 files, 99.56%, same four files) — the THIRTY-FOURTH again.
⚠ **`DEF-115` (Fixed here) came from LOOKING, which no gate can do:** `/session` rendered `10:40–10:55 ·
١٥ دقيقة` — two digit systems on one line, because only the number path carried `WBS-24.4`'s
`ar-u-nu-arab` pin. **It is also an `INV-014` divergence** — `DEC-037` quotes the reference as
`١٠:٤٠–١٠:٥٥` — so the page had disagreed with its own `.dc.html` since `FR-159` shipped.
⚠ **`DW-088` is NEW and was filed BEFORE the question it bore on reached the operator**, so the fork was
not prejudged: `TopicDetail`'s download button is hardcoded `disabled`, and **no principal but a guest
presenter can open a topic attachment anywhere in the product** — an inverted capability gradient nothing
in the package states as intended.
⚠⚠ **`DEF-116` (Fixed, `#324` → `3f93ba66`) — THE SLICE REVIEW COULD NOT BE HELD UNTIL AN INSTRUMENT WAS
FIXED.** `gen-slice-review-slate.mjs` derived an item's criterion by regex over its own title and aborted
unless **exactly one** matched. `WBS-24.8` names two; **`WBS-24.4` names two and `WBS-24.5` names three**,
so the multi-criterion path had never executed since the generator landed in `#315`. That generator **is**
how `LL-011` is discharged, so an item it cannot render is one whose review would have to be hand-built.
⚠ **A QUIETER GAP CAME WITH IT AND IS STILL UNENFORCED:** every earlier item's WBS row was amended after
the build to NAME the criteria it satisfied — `24.8`'s never was. **Nothing enforces that amendment and no
gate sees its absence**, so an item can be merged, verdicted and left permanently unreviewable. When you
finish an item, make its row a completion record naming its `AC-` ids.
✅ **`LL-030` and `LL-031` are Approved + PINNED (`DEC-087` d2/d3) and `handoff_emit` ran in the SAME
batch**, so they bind now rather than in two days (`DEF-107`'s failure mode).

⭐ **THE COUNTER IS NOT BUMPED FOR ANY OF THIS, and the reasoning is recorded so it does not read as an
oversight.** *"`SL-033` has ONE item left"* and *"`WBS-24.8` still owes its route"* were TRUE and became
false because the work COMPLETED — that is an outcome arriving, which this section's own scope excludes
(the `DW-084` precedent). Both are corrected above.

✅✅ **`WBS-25.1` (`DW-080` PHASE B) IS MERGED — PR `#325` → `1d7cb04b`, ten checks green INCLUDING the
e2e leg the row demanded.** `DEC-090` d1 chose **`aspnet:10.0-noble-chiseled-extra`**; d2 replaced the
shell healthcheck with `Acmp.HealthProbe`. Same source, two bases: **326 MB → 258 MB (−21%)**, application
CVEs **75 → 11 (−85%)**, zero criticals either side. ⛔ A lifecycle status stood here and it was the
THIRTY-NINTH; `readiness_check(scope="slice", id="SL-034")` is the answer that cannot go stale.
⭐⭐ **THE SPIKE FALSIFIED WHAT THIS FILE PREDICTED, AND THE CORRECTION IS THE VALUABLE PART.** A no-ICU
base does **NOT** throw at runtime here. It starts, exits 0, and silently enters invariant mode, throwing
only when a non-invariant culture is **touched** — and ACMP's API touches none (all 10 `CultureInfo` uses
are `InvariantCulture`; `RequestLocalization`/`IStringLocalizer`/`.resx`/`new CultureInfo` are **zero
across 693 files**). Plain chiseled would have looked perfectly healthy while arming a trap.
⭐⭐ **THE REAL DISCRIMINATOR IS THE ICU *VERSION*, NOT musl.** alpine-extra ships ICU 78.1 against
chiseled-extra's 74.2, and CLDR moved `ar-SA`'s default calendar in between — the same binary renders a
Hijri date on one and a Gregorian one on the other, **no exception, no log line, Arabic only**. ⛔ So the
`-extra` suffix is load-bearing and a digest bump that changes the ICU major needs the Arabic render
checked, not just a green suite.
⚠ **`LCID 1025` IS A SQL SERVER CONCERN, NOT A .NET ONE** — every reference is an EF migration creating a
full-text index with `LANGUAGE 1025`, executed inside `sqlserver-fts`, an image `NFR-054` excludes.
⚠⚠ **A GREEN e2e DOES NOT PROVE THE HEALTHCHECK, AND CHECKING THAT FOUND `DW-091`:** both api dependents
use `service_started`, not `service_healthy`, so **nothing consumes the healthcheck's verdict** —
`DEF-078`/`DEF-079`'s shape a third time. The probe itself
was then forced both ways through Docker's own plumbing (200 → healthy, 503 → unhealthy with the reason in
`docker inspect`).
⛔⛔ **THE CLAUSE *"with a compose comment asserting the opposite"* STOOD HERE AND IS WITHDRAWN — IT WAS A
MISATTRIBUTED QUOTATION.** The sentence `DW-091` quoted sits in the `api` service's OWN `depends_on` block,
attached to `minio-init`; the actual dependents each carry the OPPOSITE comment, saying `service_started`
is deliberate and naming its cost, at four sites across both topologies. **And `DEF-079` — cited above as
the precedent — is the row that RECORDS the decision**: `service_healthy` was declared, its total-outage
consequence measured, put back to the operator, and dropped by their choice (`fd98515`).
⛔ **WHAT SURVIVED WAS NARROWED AGAIN ON 2026-09-01 AND THIS SENTENCE IS THE SIBLING COPY OF THE ONE THE
ROW ITSELF CARRIED.** It read *"the observation is still true: nothing consumes the healthcheck's verdict"*.
**Measured (`PE-753`): BOTH boot paths consume it** — `up.sh:24`'s `docker compose up -d --wait` blocks
until every service declaring a healthcheck reports healthy, and `08-bootstrap-box.sh:221`'s `wait_healthy`
runs over six services and **exits 1**. What is genuinely unconsumed is the **STEADY STATE**: after boot
nothing observes a health TRANSITION, by the operator's own choice at `DEF-079`. `SC-042` re-scoped
`WBS-26.4` to close THAT without restoring the interlock, and `WBS-26.4` built it. ⛔ Restoring
`service_healthy` still needs a decision superseding `DEF-079`'s, not a scope change.
⭐ **A PREMISE CAN SURVIVE ONE CORRECTION AND STILL BE WRONG** — this row was resized by a sweep twice.
⛔ **THIS BLOCK SAID *"NO AC WAS WRITTEN AND THAT IS DELIBERATE (`DW-090`) … no such check exists"* AND
THAT IS NOW FALSE IN EVERY CLAUSE — `WBS-26.3` BUILT THE CHECK.** It is kept as the reason the AC was
withheld for so long, which was correct at the time: `NFR-054`'s own verification names a CI check —
`docker image inspect`, assert ≤ 500 MB, base layer verified — and until PR `#333` none existed in any of
the three workflow files, so any criterion would have claimed a CI-verified property nothing verified.
⭐ A size check ALONE would have passed on Debian at 326 MB, reporting compliance with a *minimal-base*
clause while sitting on the base it excludes — which is why the base half is the half that matters.
✅ **`entity_query("requirement", id="NFR-054")` is the live answer**; the check itself is
`scripts/check-image-contract.mjs`, wired into `ci.yml`'s `compose` job behind a real
`docker compose build api web worker`.
⚠ **`DW-066`'s recorded 257 MB is stale by a runtime major** — measured on .NET 8; .NET 10 made it 326 MB.

✅✅ **`WBS-25.2` (`DW-079`) IS `Implemented` (`DEC-093`) — PR `#327` → `58b900b0`, ten checks green, and
`SL-034` IS CLOSED (`Implemented`, the operator's verdict, no waiver and no force).** It produced
**`DOC-070`**, `tamheed-package/docs/asvs-l2-evidence-pack.md` — the **first narrative document in this
package to live outside `docs/`**, per `DEC-091` d3, because all 69 prior `DOC-` rows point into the
frozen archive and nothing had established where post-freeze prose goes.
⛔ **`NFR-018` DID NOT MOVE AND MUST NOT.** It stays `Approved` with **no acceptance criterion**: only an
external assessor's report can evidence it, and an AC written from the pack would hold package readiness
false permanently (trap 16c). `DW-079` stays the honest instrument for the distance between the two.
⭐⭐ **THE TRAP THE PACK HAD TO REFUSE, and it is one copy-paste away from anyone who assembles a pack from
the existing catalogue:** `docs/domain/security-controls.md` §20 concludes *"L2 is met across all
applicable chapters"* — precisely the **self-assertion of conformance `DW-079` forbids**, in a document
that would be handed to a paid third party. The pack declines it explicitly and says why.
⭐ **Its structural claims are mechanical, not remembered** — chapters, titles and level counts come from
the authoritative OWASP machine-readable release. **ASVS levels are CUMULATIVE**, so *"Level 2"* means the
253 requirements at **L1+L2**, not the 183 tagged L2; getting that wrong is silent. §20's
`[unverified titles]` marker is **discharged** (17/17, 0 mismatched), calibrated first by injecting ASVS
4.0's *"Malicious Code"* for V5 — a clean result from an uncalibrated checker proves nothing.
⚠⚠ **THE CONTROL→CHAPTER MAP NEEDED TWO KEYS AND THIS IS `LL-009` PAYING AGAIN:** the per-control ASVS
column and each section heading's own `V`-suffix **disagree on 41 of 72 controls**, so a single key would
have silently dropped mappings. The union is what the pack carries.
⚠ **`scripts/check-asvs-pack-paths.mjs` is committed and wired into `ci.yml`'s `compose` job.** It asserts
every cited path **exists** — never that it is sufficient — and **fails closed below 12 citations** (trap
31). ⭐ It is positioned deliberately: `ci.yml` path-ignores markdown **and** `tamheed-package`, so editing
the pack never runs it; it fires when **SOURCE moves**, which is how the pack actually goes stale.

⚠⚠ **THE BUILD LADDER HAS NOTHING SCHEDULED ON IT. Every item below is an OPERATOR act or an interview,
not code** — measure before believing that: `entity_query("deferred-work", status="Activated")` and
`readiness_check("package")` are the live answers, and the sentence you are reading is not.

⛔ **THE DISPOSITION ITEM THAT STOOD HERE IS DONE — `DEC-094` / `SC-038`, 2026-08-29 — AND ITS ROW LIST WAS
THE FORTY-FIRST.** It named `DW-088, DW-090, DW-091, DW-092, DW-093` and was wrong in BOTH directions:
`DEC-089` **d4 had already ruled on `DW-088`** and **d3 on `DW-089`**, while `DW-086` and `DW-087` had
never been ruled on and were omitted. An unmeasured enumeration, in the one place this file says the next
action lives. ⚠ **The instrument that produced it dropped rows silently**: a regex over
`deferred_work.jsonl` spanning to the first `}` loses any row whose `custom_attributes` contain a brace —
`PE-599`'s shape, where a truncated instrument does not undercount but DELETES the evidence that would
have changed the answer. **Parse the JSON; never regex a JSONL row.**

1. ▶▶▶ **FINISH THE LIVE SLICE, IN `DEC-094` d5's ORDER — security, then deployment/CI, then the calendar
   projection.** ⛔ **DO NOT NAME WHICH ITEMS ARE LEFT HERE, AND DO NOT NAME THE SLICE EITHER.** Two
   sentences rotted by naming items, and **nine commands rotted by naming a slice** (the FORTY-SIXTH) —
   a list is renumbered, and a slice closes, precisely when work completes, which is the event that makes
   somebody read this. **`entity_query("slice", status="Approved")` resolves the live slice; then
   `readiness_check(scope="slice", id=<that id>)`'s `wbs-done` names every open row, and
   `entity_query("wbs-item")` gives their statuses.**
   ⚠ **The order above is now mostly HISTORY and is kept for its reasoning, not as a schedule** — the
   security and deployment/CI items are built. Read the rows, not this sentence.
   `DEC-094` activated six rows and **every one overrode the agent's recommendation to carry**; the rows
   record it as an override, so do not read the activation as agreement about HOW.
   ⚠⚠ **EVERY REMAINING ITEM GETS A PRE-BUILD SWEEP BEFORE ANY CODE, AND ON THIS SLICE IT HAS PAID ON
   EVERY ITEM BUILT SO FAR — IN A DIFFERENT DIRECTION EACH TIME, AND ON ONE ITEM TWICE.** ⭐ **`WBS-26.4`
   was resized by a sweep, then resized AGAIN by the sweep before its build**: the first found `DW-091`'s
   premise to be a misattributed quotation and stopped the item; the second found that `SC-042`'s
   surviving clause — *\"nothing consumes the healthcheck's verdict\"* — was **also false**, because
   `up.sh`'s `up -d --wait` and `08-bootstrap-box.sh`'s `wait_healthy` both consume it at BOOT. What was
   genuinely unconsumed was the STEADY STATE, and that is what got built. **A premise can survive one
   correction and still be wrong.** ⛔ No count is written here; the
   sweep's value is the habit, and a tally of it goes stale on the next build. `WBS-26.1`'s sweep found
   `FR-076` already specified SoD-4's COI half, softly, at Phase 2 — building it would have contradicted a
   requirement's own words. `WBS-26.2`'s found that its bound requirement `NFR-046` covered neither of its
   signals, AND that `DW-092`'s central data premise was false: nothing recorded Restricted-topic reads at
   all, so the signal was undetectable by any mechanism. **`WBS-26.3`'s found its row's CENSUS exact and
   its PRESCRIPTION too small in three ways at once** — two images where the requirement names three, one
   base digest where the three images have two different bases, and *"after the compose build in CI"* when
   **no CI job built an image at all**. That is `WBS-24.4`'s shape: the measurement was right and the
   remedy was sized to it wrongly. **All of them were invisible to every id-and-status pass, because every
   identifier resolved and every status was correct** (`LL-025`).
   ⚠⚠ **AND `WBS-26.3` ADDED A WARNING ABOUT THE SWEEP ITSELF: I INVENTED A CAUSE FROM A JOB STATUS.** I
   recorded that `ci.yml`'s `publish` job was gated off *"because it reported `skipped`, so the variable is
   unset"*. Measured later: `AWS_ROLE_ARN` has been set since **2026-08-04** and `publish` runs on every
   push to `main`; it was skipped that once because `backend` **failed** and it `needs:` it. **`skipped`
   conflates *the `if:` was false* with *a needed job did not succeed*, and the API prints the same word
   for both** (`LL-039`, Proposed). The `needs:` line was ONE LINE above the `if:` and I did not read it.
   ⚠⚠⚠ **AND THE FOURTH SWEEP DID NOT RESIZE AN ITEM — IT STOPPED ONE, WHICH IS THE STRONGEST RESULT THE
   HABIT HAS PRODUCED.** `WBS-26.4`'s sweep found `DW-091`'s central claim to be a **misattributed
   quotation**: the sentence it quoted sits in the `api` service's OWN `depends_on` block arguing for
   `minio-init: service_completed_successfully`, not in either dependent's. And `DEF-079` — the row
   `DW-091` cites as its PRECEDENT — turned out to **record the decision the item proposed to reverse**:
   `service_healthy` was declared, its total-outage consequence was measured, it was put back to the
   operator, and they chose to keep the signal and drop the gate. **No code was written; `SC-042`
   re-scoped the item.** ⭐ Read `LL-040`: a CLOSED row is an unindexed decision store, so a sweep of the
   decision, ADR and open-question registers cannot see a ruling that lives in a `Fixed` defect.
   ⛔⛔ **A LINE STOOD HERE READING *\"`WBS-26.4`'s `service_healthy` switch is the risky one … prove the
   failure mode both ways\"* AND IT IS DELETED, NOT REFRESHED — THE FORTY-FIFTH.** It was a live
   INSTRUCTION to build work that `SC-042` withdrew, and a fresh session obeying it would have reversed an
   explicit operator decision. **The item's deliverable is now in its own row and in `SC-042`; read those,
   and never a description of it here.**
   ⛔ **`WBS-26.5` must NOT fan `useMeetingDetail` across the month** — `DEF-104`'s N+1 shape; the answer
   is a per-meeting agenda projection. ⚠ **Its row also carries a RIDER** (`DEC-105` d2): a one-line
   comment correction at `src/Acmp.Api/Endpoints/MeetingsEndpoints.cs` travels with its PR, and the row
   says what to do if the item is never built. Read the row, not this sentence.
   ⚠⚠ **A SECOND REGISTER FINDING CAME OUT OF THE SAME ROUND AND IS CARRIED `Open` AT `medium` BY
   `DEC-106` d4: `DEF-123`.** ⛔ **AND IT WAS WIDENED ON 2026-09-01 — IT IS NOT THE TEST LEG, IT IS ALL
   THREE.** For `NFR-001`–`NFR-063`, contiguous, **63 of 65**, every leg `G-TRACE` requires is the same
   blanket: `implements ← WBS-1` (*Platform Foundation*, a `PH-1` item), `derives_from → ADR-0012 +
   ADR-0016`, `tests ← TEST-041 + TEST-045`. Only `NFR-064`/`NFR-065` are wired individually. **The FR
   register is the control and is NOT like this**, which is what makes it a finding rather than a house
   style. ⭐ **The `derives_from` leg needs no judgement call where the test leg did:** `NFR-054`
   (container images ≤ 500 MB from a minimal base) is recorded as **deriving from the React frontend
   ADR**, as are `NFR-052` and `NFR-018`.
   ⛔ **No edge has been touched and none should be** — `DEC-104` d1 authorised an investigation, not a
   repair; `DEC-106` d4 then carried it deliberately, because deleting the blanket reddens 63 requirements
   on three legs at once, re-wiring is 189 edges nobody has scoped, and fixing only the three
   phantom-capability rows leaves 60 (`LL-035`). `G-TRACE` and `G-REL` PASS and are right to:
   **`G-REL` validates an edge's shape; no rule validates its claim.**
2. **THE OPERATOR OWES A PER-ITEM VERDICT ON ANY ROW AT `Review`.** Done-claimed is `Review`;
   `Implemented` is theirs alone, adjudicated per item against a GENERATED slate
   (`node scripts/gen-slice-review-slate.mjs <the live slice id>`), never a summary — `LL-011`. ⛔ **Which rows are at
   `Review` is deliberately not written here**; the command above prints them. ⭐ The per-item mechanism
   has discriminated twice and is not ceremony: a slice-level verdict would have carried `WBS-24.4`
   through on its neighbours' strength on both occasions.
3. **`release-close-out.md` has never been run.** ⛔ **AND IT IS NOT DUE — *"every phase is closed"* stood
   here and was the FORTY-SECOND.** Measured: **`PH-3` and `PH-7` are `Approved`, not `Implemented`** —
   `PH-3` deliberately (§1: do not "repair" it), and **`PH-7` because it is the LIVE phase, holding the
   open slice.** ⛔ **A COUNT OF ITS OPEN ITEMS STOOD HERE AND IS DELETED, NOT REFRESHED** — it went stale
   the moment an item closed, which is the FORTY-THIRD's cheapest member. `readiness_check` on the live slice is the count. Closing out a release while a slice is open is exactly what the deleted
   sentence would have invited. It sits in the prompt library, production is live, and it is the ceremony
   that would formally end v1. ⚠ It is the operator's to start.

⚠ **SEVERAL `deferred-work` rows are `Activated` and every one is correctly so — do not "tidy" any.**
⛔ **A COUNT STOOD HERE ("Two") AND IS DELETED: `entity_query("deferred-work", status="Activated")` is
the answer, and the number moves every time a slice item ships.** ⚠⚠ **A `DW-` ROW DOES NOT CLOSE
ITSELF WHEN ITS `WBS-` ITEM SHIPS, AND NOTHING COMPARES THEM** — three rows sat `Activated` after their
items were built and merged, and the omission was found only by measuring the register while preparing
this file. **When you done-claim an item, close its `DW-` row in the same batch**, as every `WBS-24.x`
item did.
`DW-069`, the bilingual glossary, is live work that **cannot be closed by a reader of code**: its own row
says adjudicating Arabic divergences needs an Arabic-reading stakeholder, and `NFR-039`'s second clause
stays *undecidable* until the artifact exists. `DW-071`'s new-route clause was discharged by `SL-033`;
its SECOND clause is release sign-off, parked by `DEC-071` d4.

⚠ **WHAT `DEF-114` ACTUALLY IS, AND WHY IT WAS KEPT OUT OF `#320` — the reasoning binds the next bundling
temptation.** `DW-085`'s forced-build guard is not hermetic: **Docker caches `RUN sleep 30` like any other
layer, and the cache is keyed on the INSTRUCTION, not the image tag.** Once warm, the deliberately-hanging
build returns in ~2 seconds, the 10-second budget never expires, and the timeout never fires. ⚠ **CI never
sees it — a fresh runner has a COLD CACHE — so it reports a false red only to DEVELOPERS**, which is
backwards for a guard whose whole purpose is that this path cannot be proven any other way. **The fix is a
per-run INSTRUCTION (`RUN echo {Guid} && sleep 30`); a per-run TAG fixes nothing and was tried first.**
⛔ **This paragraph previously named the image-exists mechanism and claimed the layer cache had been
measured and DISCARDED. Both were wrong — see the THIRTY-FIFTH, and `LL-029`.**
It stayed out of `#320` because `DEC-073`'s attribution rule is one change, one cause, and `#320` was
already green.

⚠⚠ **DO NOT RE-QUOTE AN OPEN-PR LIST FROM ANYWHERE IN THIS FILE.** A line here said *"the only two open
PRs, `#128` and `#134`"*, re-verified 2026-08-26 — and two more appeared within a day. **A Dependabot
queue is a moving target, so any count of it is stale by construction**; `gh pr list --state open` with
**no `--limit`** is the answer (`PE-599`: a cap at ten is what hid `#128`/`#134` in the first place, which
is the entire reason `DW-080` exists as its own row). ⚠ `#318` and `#319` are covered by NO prior decision
and `DEC-083` d3 deliberately LEFT THEM ALONE — stretching an old *"sweep everything"* over work nobody
has seen is the failure that created this row. ✅ **`#128`/`#134` are CLOSED as superseded by phase A**
(the `#135`→`#308` precedent), discharged 2026-08-28 — **so do not read any list above as live; run the
command.**

⚠⚠ **THE RULE, NOT THE ROSTER: A ROW AT `Review` IS DONE-CLAIMED WORK AWAITING THE OPERATOR'S VERDICT, AND
IT IS ALWAYS MERGED. `Review` counts as OPEN in `readiness_check`, so slice-scope `wbs-done` naming such a
row is the rule working, not a fault to repair — and it is NEVER a reason to rebuild anything.**
⛔ **Which rows are at `Review` is not written here on purpose** — `entity_query("slice", status="Approved")` then `readiness_check` on the id it returns is the only
answer that cannot go stale, and this block has now been wrong in BOTH
directions within one session. It said *"no verdict is owed"* while `WBS-24.6` sat at `Review`, then said
*"one is owed — `WBS-24.6`"* about ninety minutes before the operator promoted it (`DEC-082` d1). Both were
true when written; both were falsified by the session's own next action, and both were caught before a
commit, so the tally above does not move.
⭐⭐ **THAT IS THE GENERAL SHAPE AND IT IS WHY THE ROSTER IS GONE: a sentence naming WHICH items are
outstanding is falsified by the very work the reader is doing, so its half-life is one unit of work.** A
sentence naming the RULE survives, because the rule is what does not change. ⚠ **Both verdicts of
2026-08-27 (`WBS-24.4` at the second asking, `WBS-24.6`) were taken against a GENERATED slate carrying each
criterion's own text** — `scripts/gen-slice-review-slate.mjs`, `LL-011`/`LL-023` discharged mechanically
rather than remembered. ⭐ **And `WBS-24.4` is why you ask:** it had been declined twice with no reason
recorded the second time, and asking produced a promotion, where every available inference was wrong.
⭐⭐ **WHY IT MATTERS AS A METHOD AND NOT AS A STATUS: THE ROW HAD BEEN DECLINED TWICE AND THE SECOND
DECLINE RECORDED NO REASON.** The first had one — `AC-147` carried an exclusion the definition of done
forbids — and that cause (`DEF-111`) was Fixed and merged at `2b0da29`, with `AC-148` carrying the
corrected criterion and its own `Met` verdict. The second, on 2026-08-27, said nothing at all. **The
session ASKED instead of inferring (`LL-003`), and the answer was a promotion** — so every inference
available from the evidence (a concern not yet stated, work still owed, something to rebuild) would have
been WRONG about merged, finished code. ⚠ **AN ABSENT REASON IS NOT EVIDENCE OF A REASON.** It reads like
a signal because a decline usually carries one, and that is exactly what makes it worth one question.
⭐⭐ **THE PER-ITEM MECHANISM HAS DISCRIMINATED TWICE NOW, WHICH IS WHY IT IS NOT CEREMONY.** A
slice-level verdict would have carried `24.4` through on its neighbours' strength on both occasions.
`DEC-071` d3 put eight rows in one slice over the agent's objection and this was the recorded mitigation;
it has earned itself.
⭐⭐ **THE PER-ITEM MECHANISM HAS NOW DISCRIMINATED, WHICH IS WHY IT IS NOT CEREMONY.** At the 2026-08-26
review the operator promoted `24.2` and `24.3` and WITHHELD `24.4`, because its criterion carried an
exclusion the definition of done forbids (`DEF-111`, since Fixed). **A slice-level verdict would have
carried `24.4` through on its neighbours' strength.** `DEC-071` d3 put eight rows in one slice over the
agent's objection and the recorded mitigation was exactly this; the mitigation earned itself.

✅ **`LL-022` IS APPROVED AND PINNED** (`DEC-078` d1) and `lessons-confirmed` passes again. ⭐ **The
approval path has a guard worth knowing before you meet it: omitting a field is NOT preservation there.**
An ordinary update preserves nullable fields by omission (trap 13b), but an approving upsert refused a
minimal payload outright — *"content drifted on ['category','context',…]; send the stored content
byte-identical"* — so every content field must be resent. It fails LOUDLY, which is what makes `LL-001`'s
dangerous middle survivable here; generate the payload from the JSONL and read it back rather than
trusting what you believe you wrote.

✅ **`DW-082`, the handler tests, is DONE** and is deliberately not numbered — it led so that `SL-033`'s
new components would land under a gate that can SEE their inline handlers, and they now do.

✅✅ **`DW-084` IS DONE (PR `#309` → `eb09342`, 2026-08-25) AND IS NO LONGER THE NEXT ACTION.**
⚠⚠ **THIS IS NOT A STALE STATEMENT AND THE COUNTER IS DELIBERATELY NOT BUMPED FOR IT.** The block said
*"`DW-084` is the next action"*; that was true, and the work being DONE is its outcome arriving, not the
claim turning out wrong. The counter's own scope excludes *"annotating a historical record whose outcome
later happened"* — **and keeping that boundary is what stops the tally degrading into a log of every
edit.** What follows is what the row bought, kept because it binds later work:
- `ContainerStartup.StartOrFailFastAsync` bounds every container start in `Acmp.Integration.Tests` at
  **10 minutes** and, on expiry, throws a `TimeoutException` naming the container, the bound, and the
  tail of the container's **own** startup log. ⚠ **THREE call sites, not the one the row named** —
  `SqlBackstopFixture`, `SearchProvidersFtsTests`, `MinioFileStoreTests` all carried the identical
  unbounded call; guarding only the reported path would have left the siblings able to hang the same way.
- ⚠ **The bound is generous ON PURPOSE.** It covers pull + create + boot + wait, and a bound tight
  enough to fire on a slow-but-healthy start would **manufacture exactly the red `DEC-077` d3 turns into
  a mandatory operator stop.** Do not "tighten" it. A whole green backend job is ~9 minutes.
- ⭐⭐ **THE FINDING WORTH CARRYING, and it is a hollow pass caught before it shipped.** Mutating the
  wrapper away showed Testcontainers **already** throws `TimeoutException`, message *"The operation has
  timed out."* — so `ThrowAsync<TimeoutException>` **alone would have passed vacuously**, and the two
  message assertions are the only part of that test doing work. It also settles why a bound alone could
  never have satisfied the row: `TestcontainersSettings.WaitStrategyTimeout` stops the hang and still
  names no container, no bound and no log. **Filed as `LL-022`** — which now BINDS; `DEC-078` d1 approved
  and pinned it. ⛔ **This sentence used to carry its then-status and an instruction built on it, and that
  is the THIRTY-FIRST's second half** (see the preamble): a fresh session was told an interview was owed
  and an advisory was red on purpose, months after both had been settled.
- ⚠ **IT DID NOT CLOSE `DEF-108`** — it changed how the failure presents. ⛔ The clause that followed here
  said the row *"stays `Open`/high with readiness deliberately `FALSE`"*, and that was true for five days
  and is now false: `DEC-097` d1 closed it as environmental. **That is the FORTY-THIRD.** The durable half
  is unchanged and is why the row is still worth reading: bounding a hang changes a failure's PRESENTATION
  and never its cause.
- ⚠ **NEW: `DW-085`** — `_image.CreateAsync()`, the 3.62 GB FTS image **build**, is still unbounded and
  is now the only unbounded await left on that path. Left out of scope deliberately: `DEC-077` d4 scoped
  the decision to container **startup**, all four `DEF-108` data points name startup, and it could not
  meet the prove-by-forcing bar without committing a deliberately-hanging Dockerfile.
  ⛔ **THAT LAST CLAUSE IS WITHDRAWN — see the TWENTY-SEVENTH above.** It was forced, with a temp-directory
  Dockerfile, to `DW-084`'s own standard.
⚠⚠ **THE TWENTIETH, AND IT WAS A STALE *INSTRUCTION* THAT WOULD HAVE COST AN OPERATOR INTERVIEW.** This
block said *"`TopBar` is NOT [a handler fix] — `DevRoleSwitcher.tsx` is in the coverage `exclude` list but
its call site is not … handler tests will not fix that file; it needs its own decision."* **Measured false**
(`PE-614`): `TopBar`'s uncovered lines are 62, 63, 92, 108, 131, 138, 160 — search submit, search
`onChange`, language toggle, notification toggle, `NotificationCenter` `onClose`, profile backdrop. **All
seven are inline handlers.** The `lazy()` declaration on 20–21 records **three hits**, and the
`{DevRoleSwitcher && …}` call site on 99–101 has **no statement starting on it**, so it never enters the
line metric at all. The exclusion charges `TopBar` nothing. ⚠⚠ **WHERE IT CAME FROM: version one of the
triage script misread the ASCII table's compressed `92-138` as a 47-line range; version two corrected the
NUMBER to seven and NOBODY RE-DERIVED THE CAUSE INFERRED FROM THE WRONG NUMBER.** It then travelled into
`PE-611`, into this block, and into the memory index as a standing warning. **A CORRECTED MEASUREMENT DOES
NOT CORRECT THE INFERENCE SOMEBODY BUILT ON THE OLD ONE** — the twelfth's lesson running backwards.
✅ **`DW-082` IS FINISHED and the remainder was uniform after all** — every uncovered line in all 32
files was a handler, a dismissal, an error arm or a drag path; not one needed a decision. **Do not re-add
a "needs a decision" label to any file without reading its lines**: `scripts/coverage-triage.mjs` prints
each one's source text for exactly that. The branch `chore/vitest-4-pair` is merged and deleted.

▶ **HISTORY — `WBS-24.1` SHIPPED (`f968703`) AND ITS REQUIREMENT IS `Implemented`.** Kept because the
sweep below is the method, not because the work is pending. ⚠⚠ **AND THE ROW'S SIZING WAS WRONG: the
dense TABLE had already shipped; only the CONFIGURATION was missing.** `DW-033`'s own text was accurate
and narrow — the WBS title's SUMMARY of it was the wrong part.
`WBS-24.1` / `DW-033` / `FR-032` — the backlog as a dense table with **user-configurable
columns** (show/hide, reorder). **Re-verified unbuilt 2026-08-23**: `columnPrefs`, `visibleColumns`,
`columnConfig` and `ColumnPicker` return **zero** across the 339 `.ts*` files of `src/Acmp.Web`, and the
sweep is proven to have had a subject — the control term `Backlog` returns 305 in the same pass
(`LL-013`). It WAS the single missing member of a family that otherwise
shipped — `Backlog.tsx` (`FR-031`), `Kanban.tsx` (`FR-033`), `Calendar.tsx` (`FR-035`), `Timeline.tsx`
(`FR-036`). ⚠ **Of those, only `Timeline.tsx` is still a shell today.**

**THE ORDER (`DEC-071` d1), smallest and most contained first, riskiest LAST:**

⚠ **The status column is a convenience and it CAN go stale — `entity_query("wbs-item")` is the live
answer, and `entity_query("slice", status="Approved")` then `readiness_check(scope="slice", id=<that id>)` names every row still open.**

| # | row | what | status (2026-08-27) |
|---|---|---|---|
| `WBS-24.1` | `DW-033` / `FR-032` | configurable backlog columns | ✅ `Implemented` |
| `WBS-24.2` | `DW-037` / `FR-035` | the calendar view · axe route **DISCHARGED** | ✅ `Implemented` |
| `WBS-24.3` | `DW-039` / `FR-117` | the wiki version **diff** half | ✅ `Implemented` |
| `WBS-24.4` | `DW-068` / `NFR-037` | **number** formatting (the date half already holds) | ✅ `Implemented` |
| `WBS-24.5` | `DW-036` / `FR-155`, `NFR-059`, `NFR-060` | the `Configuration` store (**resized S→L**, `DEC-080`) | ✅ `Implemented` |
| `WBS-24.6` | `DW-035` / `FR-154` | audit-log export, **`{Auditor, Chairman, Secretary}`** (`ADR-0027`) · axe route **DISCHARGED** | ✅ `Implemented` |
| `WBS-24.7` | `DW-063` / `NFR-010` | configuration-driven stream count | ✅ `Implemented` |
| `WBS-24.8` | `DW-028` / `FR-165` | the `/session` presenter preview — **LAST, on purpose** · axe route **DISCHARGED** | merged `#323` |

⚠⚠ **THE ROWS MARKED `+ axe route` CARRY A SECOND OBLIGATION (`DEC-072` d2, `SC-032`): each adds its
route to the live axe sweep in `e2e/rtl-a11y.spec.ts` IN THE SAME BATCH THAT BUILDS IT, and says so in its
own acceptance criterion.**
✅ **`WBS-24.2`'s IS DISCHARGED — do NOT add the calendar again.** `AC-145` carries it, and it was PROVEN
to run rather than inferred from a green job: the e2e count moved **86 → 88**, which is **+2 for one added
test** because `playwright.config.ts` runs `rtl-a11y.spec.ts` in **both** `chromium` and `msedge`.
✅ **`WBS-24.6`'s IS DISCHARGED TOO — do NOT add `/audit` again.** `AC-152` carries it; **88 → 90**, and the
`88` was **re-measured from PR `#316`'s own run** rather than quoted from this file.
⭐⭐ **THE THIRD STEP THAT CHECK NEEDED, learned here: the obvious confirmation is BLIND.** Grepping the e2e
log for the new test's NAME returns zero — **and so does a grep for an EXISTING test's name**, because the
log carries only per-test dots and a summary. **The control is the only thing that separates "my test did
not run" from "this log never contains test names."** `WBS-24.5` recorded this exact shape about the
BACKEND log; it repeated on the e2e log one item later. **Always grep a known-present term first.**
⭐ **And scan the surface in the state that has the new UI in it.** `/audit`'s export is a `Menu`, and a
closed `Menu` renders only its trigger — sweeping on load would have scored the panel, its `role="menu"`
labelling and its `target-size` without ever rendering them. A true zero over the wrong set (`LL-015`).
✅ **`WBS-24.8`'s IS DISCHARGED — do NOT add `/session/preview` again.** `AC-155` carries it; `playwright --list` moved **92 → 94**, +2 for one test across two browsers. ⚠ **The baseline was RE-MEASURED and was 92, while this file recorded 90** — the rule is to measure, and it paid the first time it was applied after being written. `DW-071` is **`Activated`** because its FIRST trigger clause — *"whenever a new
route ships — that is the moment the ratio gets worse, and the moment it is cheapest to add the route to the
sweep"* — fired against exactly these three surfaces. ⚠ **The row had been read as fully parked and it never
was**: `DEC-071` d4 parked its SECOND clause (release sign-off) and nobody had read the first. **The summary
over a row had dropped half of what the row said** — `DEC-064` d2's failure inverted.
⚠⚠ **DO NOT RE-QUOTE A COVERAGE RATIO FROM HERE; MEASURE IT** — `grep -oE "page\.goto\('[^']+'\)"
`e2e/rtl-a11y.spec.ts` | sort -u`. This sentence used to read *"the sweep today visits three of fifty-two
routes"*, and it was counting SURFACES: the kanban and the calendar are views inside `/backlog`, not routes.
⛔⛔ **THE COUNT THAT SAT HERE IS DELETED, AND ITS DELETION IS THE THIRTY-THIRD'S RULING FINALLY APPLIED IN
FULL.** This sentence read *"Measured 2026-08-27 the sweep visits three distinct ROUTES (`/backlog`,
`/backlog/submit`, `/audit`)…"* — a command AND an answer, and the answer is the half that gets read.
`WBS-24.8` added `/session/preview` and the figure went stale the moment the route shipped, which is exactly
when someone next reads this file. **A count with a disclaimer attached is still a count: give the command or
give nothing.** ⭐ The durable half is the *reason* the old figure misled — it was counting SURFACES while
its own sentence said ROUTES, so the kanban and the calendar (views inside `/backlog`) inflated it.
**`LL-016`'s *a phrase can go stale without its number changing*, on a number never measured in the unit it
named.** ⭐ **Not tallied**: it was true when written and was falsified by work COMPLETING, which this
section's scope excludes (the `DW-084` precedent) — but the half-applied fix is why it could go stale at all.
⚠ This does NOT activate `DW-041` or `DW-067`: both name release sign-off ALONE, which stays unscheduled.

⚠⚠ **READ THE CODE BEFORE BELIEVING ANY OF THESE EIGHT ROWS' SIZING.** **TWO** of `SL-032`'s four said
"blocked on nothing" and were wrong — `DW-040` and `DW-038`; `DW-061`'s and `DW-032`'s sizings HELD.
⚠ The number in this sentence read "three" for one day and was **my own prose error**, propagated into six
artifacts before a sweep caught it (`PE-592`) — conflated with the TRUE statement that reading the code
*paid* three times, two catches plus one confirmation. **The habit is what matters, and it is unchanged.** **Two of these eight already carry corrections in their own
text:** `DW-037` says the scheduled date is **NOT** on the Topics side — `Topic.Schedule` does not persist
the meeting id, it raises an event with **zero consumers**, so the calendar **must** read
`MeetingDetailDto.ScheduledStart` + `AgendaItemDto.TopicId` from the **Meetings** API; and `DW-063` says
`Stream.Create` **still has no caller**, so adding a sixth stream is a migration and a deployment.

▶ **HISTORY — `WBS-24.8` (`DW-028`) SHIPPED (`#323` → `24738d4b`). Kept because the reasoning is the method, not because the work is pending.** It was the one to slow down for: It adds a targeting parameter to the `/session`
read path **and authorization on that parameter** — a second authorization path over content scoped to
somebody else, which its own row names as the shape that produced `DEF-052` and `DEF-056`. **Treat the
refusal as the feature and prove it by forcing it.** `navModel.ts`'s ACCESS map grants `session` to GUEST
ONLY and that restraint holds (`DEF-053` deliberately left it alone). A guest is bounded by a TIME WINDOW,
so the targeting parameter must never become the way a guest reads somebody else's slot.

### ▶▶ THE SECOND INTERVIEW OF 2026-08-23 (`DEC-072`, applied by `SC-032`) — FOUR MORE DISPOSITIONS

⚠ **Run after the resume above was prepared and pushed. TWO of the four OVERRODE the recommendation; both
are recorded as overrides, reasoning-against preserved, per the `DEC-071` d3 precedent.**

- **d1 — THE WHOLE DEPENDABOT QUEUE IS SWEPT, MAJORS INCLUDED** (`DW-078`, `Activated`). **OVERRIDE** — the
  recommendation was to merge the routine set and carry the majors. ⚠⚠ **THE COUNT THAT SAT HERE WAS
  THIRTEEN AND IT WAS NEVER MEASURED** — `gh pr list` had `--limit 10` on it (`PE-599`). It is **twelve**,
  oldest **2026-07-16**, and the split is **three routine / nine majors**, not seven and three.
  ✅ **BOTH QUESTIONS THAT CREATED WERE PUT BACK TO THE OPERATOR AND ANSWERED (`DEC-074`, `SC-033`):**
  **#128/#134 (`dotnet` 8.0→10.0) ARE CARVED OUT to `DW-080`** — the solution targets `net8.0`, so that pair
  is a RUNTIME MIGRATION, not a dependency bump, and `DEC-072` d1's "everything" was answered over a
  description that never contained it. **The sweep is therefore TEN PRs, not twelve**, and `DW-080` does
  **not** block `SL-033`. **And `DW-066` WAS BOUND TO `DW-080`** (a status stood here and it was the
  FORTIETH's fourth instance; both rows are closed now) — #134 edits the api and
  worker `FROM` lines (16/31/51, verified with `gh pr view --json files`), which is its trigger *verbatim*,
  so the alpine/distroless move happens in the SAME change as one base-image decision. ⚠ **`DW-066` must not
  be scheduled separately** — doing the base move apart from the `FROM` edit spends the expensive part
  (full e2e leg, Arabic FREETEXT end to end) twice. ⚠ **musl is the risk, not the edit.**
  ⚠⚠ **IT IS NOT AN `NFR-051` BREACH AND MUST NOT BE FILED AS ONE** — that requirement is `Implemented` and
  says Dependabot shall be **configured to ALERT**, which it is; thirteen open alerts are it WORKING. Nothing
  in the register obliges anyone to *act*, so the gap is uncovered rather than violated.
  ⚠⚠ **THE `mssql` BUMP CAN DESTROY DATA** — fresh-volume isolated project only, per "HOW TO RUN A STACK
  HERE" below. The shape is part of what was authorised: **a dedicated batch, full e2e leg per risky bump**,
  majors verified individually and never as a block.
  ⚠⚠ **ORDERING WAS DECIDED (`DEC-073`, same interview): THE SWEEP RAN BEFORE `SL-033` STARTED.**
  ⛔ **HISTORY — `DW-078` IS `Done`, SO THIS IS NO LONGER THE NEXT ACTION AND `WBS-24.1` NO LONGER WAITS
  ON IT.** The sentence here read *"so THIS — not `WBS-24.1` — IS THE NEXT ACTION"* for two days after the
  queue was swept: **the TWENTY-FIFTH.** The REASONING below is what still binds, and it binds `DW-080`:
  a TypeScript major and a SQL Server major landing under eight items in flight give any later failure two
  candidate causes, and every `SL-033` item should be built against the versions it will ship on. The
  accepted cost is that the live slice pauses for a batch.
- **d2 — `DW-071`'s new-route clause HAS FIRED**, so it is `Activated` and three `SL-033` items carry the
  axe-route obligation. See the table above; that is where it lives, not here.
- **d3 — `LL-016` is Approved and PINNED** in one step, the operator having read the exact statement.
- **d4 — THE `NFR-018` ASVS EVIDENCE PACK IS PREPARED NOW** (`DW-079` — **built and closed 2026-08-29 as
  `WBS-25.2`**; a status stood here and was the FORTIETH's seventh instance). **OVERRIDE** — the
  recommendation was to leave it, externally blocked with no trigger fired. ⚠⚠ **IT DOES NOT CLOSE
  `NFR-018`, AND NO ACCEPTANCE CRITERION MAY BE WRITTEN FROM IT** — only an external assessor's report can
  evidence that requirement, and an AC ahead of the report holds readiness false forever (trap 16c). The
  pack must carry the KNOWN GAPS too (`DEF-100`, `DW-074`: two of three internal hops are plaintext), or it
  is worse than no pack.

⚠ **Nothing `DEC-071` settled that morning was re-raised.** ⭐ **Two store facts proven by experiment, not
assumed:** `scope_adds` → a **deferred-work** target is ACCEPTED (every prior one pointed at an AC, a
requirement or a slice), and `deferred_work.source_kind` is a CHECK over `brief|clarification|code|inferred`
— anything else rolls back the whole batch.

### ▶▶ WHAT 2026-08-25 ADDED — read this before trusting any instrument here

- ✅ **`DEF-106` FIXED, AND MY FIRST DIAGNOSIS OF IT WAS WRONG AND IS WITHDRAWN.** I filed it as *"the
  local build reports 16 errors while CI compiles the same commit cleanly, so trap 22b's arbiter is
  unusable on this machine"*. ⚠⚠ **THE EXPERIMENT BEHIND THAT WAS INVALID: I checked out `main`, ran
  `npm run build`, saw the same errors, and concluded main was broken too — but `node_modules` still held
  the BRANCH's install. A CHECKOUT CHANGES THE SOURCE; IT DOES NOT CHANGE `node_modules`.**
  ⭐ **What adjudicated it was the e2e job failing with the SAME sixteen errors, in Linux, in CI**, inside
  the web image's own `npm run build` — which killed the local-environment theory outright. A three-way
  split then located it: main source + main's tree passes, branch source + branch's tree fails, branch
  source + MAIN's tree **passes**. Identical source, opposite verdicts: the variable was never the machine.
  **Real cause:** `tsconfig.app.json` sets `types: ["vite/client"]` and includes all of `src`, and four
  test files there read the repo from disk. Node types were never declared — they arrived transitively,
  because vitest 3 re-exported types from `vite`, whose `dist/node/index.d.ts` carries a
  `/// <reference types="node" />`. vitest 4 dropped the chain. **The typecheck had been compiling by
  ACCIDENT since before that branch.** Fixed by declaring `types: ["vite/client", "node"]`.
  ⚠⚠ **AND CI's `frontend` JOB LOOKED FINE THROUGHOUT, because it fails at the coverage-gate step, which
  runs BEFORE the build step. A JOB THAT STOPS EARLY CANNOT VOUCH FOR THE STEPS IT NEVER REACHED.**
- ⚠⚠ **A COMMIT LANDED CARRYING ANOTHER SESSION'S MESSAGE.** `git add … && cat > /tmp/c4.txt <<EOF …
  && git commit -F /tmp/c4.txt` — the `git add` failed, so the heredoc never ran, and `git commit -F`
  found a **leftover `/tmp/c4.txt` from 2026-08-19** and used it. Correct files, a message describing a
  different feature on a different branch, no error anywhere. ⭐ **An `&&` chain fails CLOSED for the
  command that breaks and OPEN for any later command that reads a file the chain was supposed to write.**
  Amended before pushing. **Write generated inputs to `/tmp/name-$$.txt` and `rm` them**, and read back
  `git log -1 --format=%s`. (`LL-021`, Proposed.)
- ⚠⚠ **THE MEMORY INDEX HAD BEEN SILENTLY TRUNCATED AND NOBODY KNEW.** The tooling reports a **200-LINE**
  read limit; the file was at 275, so its *"Standing rules & gotchas"* section — the most durable content
  in it — **was already invisible**. Compacted, with the detail moved to `dw082-sweep-and-vitest4.md`.
  ⚠⚠ **DO NOT READ A LINE COUNT HERE — `wc -l` IT.** The figure that sat in this sentence was true when
  written and false two edits later, in the same session (the TWENTY-FIRST). **Check the headroom before
  adding to that file**, because the cap is a hard 200 and the tail is dropped SILENTLY.
  ⭐ The old note there had DISPROVEN a ~17KB *byte* ceiling and concluded the limit was unknown.
  **A LIMIT YOU HAVE DISPROVEN IN ONE UNIT IS NOT A LIMIT YOU HAVE DISPROVEN** — it was measuring the
  wrong dimension.
- ✅✅ **THE LESSONS INTERVIEW IS DONE (`DEC-076`, 2026-08-25): `LL-017`…`LL-021` ARE ALL APPROVED AND
  PINNED**, every sentence taken as written, none refined. `lessons-confirmed` is GREEN for the first time
  in weeks. ⭐ **The form is what made it possible and it is now reusable:** the slate was a generated
  docket carrying the FULL canonical text of every field of every row, printed from `data/lessons.jsonl`
  and verified block-by-block as byte-identical before publishing — `LL-011` discharged mechanically
  rather than remembered. `scripts/gen-lesson-docket.mjs` is committed; **use it for the next one.**
  ⭐ **Two store controls, both new to this session:** approval is NOT an edit — `entity_upsert` refuses
  unless the content you send is byte-identical to the stored row — and **attribution lands WITH approval**
  (`confirmed_by` can never be added later). Together they make `LL-001`'s dangerous middle safe *here*:
  re-typing a long field in order to preserve it fails LOUDLY instead of silently enshrining a corrupted
  lesson that is immutable from that moment. Elsewhere, hash-and-verify is still the only substitute.
- ⚠⚠ **`DEF-107`: APPROVING AND PINNING A LESSON DOES NOT MAKE IT BIND.** The note every session loads is
  rebuilt ONLY by `handoff_emit`, and nothing compares it against the pinned set. **`LL-016` was Approved,
  pinned and operator-attributed on 2026-08-23 and was still absent from the note on 2026-08-25** — so for
  two days the register said it binds every session and no session saw it. Found by accident: the emit
  added SIX lines, not five. ⚠ **`lessons-confirmed` cannot catch this** — it counts `Proposed` rows, so it
  goes green the instant a lesson is approved, propagated or not. **THE FIX IS PROTOCOL: run
  `handoff_emit(target_dir=<repo root>)` in the SAME batch as any approval and commit the rebuilt note.**
  Same shape as `DEC-064` d2's *"DW-037 is ACTIVATED"* beside a row still reading `Open`.

### ▶▶ THE THIRD INTERVIEW OF 2026-08-23 (`DEC-075`) — AND `DEF-105`, WHICH IT FIXED

- **d1 — `DW-082` CLOSES BY WRITING THE MISSING HANDLER TESTS**, then merging `#307`. The only route
  ending with BOTH a current vitest and a true 95%. ⚠⚠ **Lowering the threshold stays forbidden and is
  now MORE so** — the number would hide real untested code rather than an artefact.
- **d2 — ALL FOUR ACTIVATED STREAMS ARE IN SCOPE** (the operator answered *"all"*). Order = agent's
  recommendation only; see the next-action block above.
- **d3 — SIX LEAKED `vite` PROCESSES KILLED, AND `DW-083` FILES THE GAP THAT LET THEM LEAK.** They had
  been running out of `src/Acmp.Web/node_modules` for up to THIRTEEN DAYS on ports 5199/5201/5233/5241/
  4173/8124, holding `rolldown-binding.win32-x64-msvc.node` open — which is why `npm ci` failed here with
  `EPERM`/`EBUSY`, and why an interrupted `npm ci` gutted `node_modules` from 171 packages to 35.
  ⚠⚠ **TRAP 28 GUARDS THE FILES AND MISSES THE PROCESSES**: a `vr-*.tsx` in `src/` would ship, so it is
  checked — but the same visual-verify loop also starts a long-lived dev server and nothing stops it. **A
  file-level check runs perfectly clean over a machine carrying six leaked servers.** ⚠ **If `npm ci`
  ever fails here with `EPERM`/`EBUSY` on a file under `node_modules`, enumerate node processes and match
  their command lines against the repo path BEFORE debugging npm** — and note `npm ci` makes it WORSE,
  because it unlinks `node_modules` before discovering it cannot finish.
- **d4 — `LL-017`, `LL-018`, `LL-019` FILED AS `Proposed`, NOT Approved.** The operator selected the
  FINDINGS; approved lessons are IMMUTABLE here and selecting a finding is not approving a sentence they
  have not read. ⚠ **`lessons-confirmed` therefore FAILS on purpose** — that advisory is doing its job,
  do not "fix" it by approving them unread.
  ✅ **RESOLVED 2026-08-25 by `DEC-076` d1: all three (plus `LL-020`, `LL-021`) are now Approved and
  PINNED, and `lessons-confirmed` PASSES.** The interview finally happened because the slate carried the
  full text; d4's reasoning was right and is kept as the record of why it waited. `LL-017`: when an instrument changes and the numbers get
  worse, *"the new one is broken"* and *"the old one was lying"* predict IDENTICAL evidence — only
  adjudicating one hand-countable artefact separates them. `LL-018`: two runners over one shared artefact
  are ONE instrument for LOCATING a fault and TWO for VERIFYING its repair. `LL-019`: a pinned base image
  and a pinned package repo are one decision in two places, only one automated.

✅ **`DEF-105` IS FIXED AND IT WAS A REAL GAP: `INV-013` NAMED `branch protection` AS ITS ENFORCEMENT AND
NONE EXISTED.** Three endpoints agreed — classic protection 404, `rulesets` empty, `rules/branches/main`
empty — while `SEC-120`, `SEC-126`, `SEC-172`, `SEC-581` and the invariant's own enforcement column all
asserted it as fact. `SEC-126` said *"cannot merge/deploy without passing"*, which was flatly false.
⚠⚠ **THE OBSERVABLE THAT GAVE IT AWAY WAS A STATUS THAT NEVER APPEARED**: not one PR ever reported
`BEHIND`, which GitHub emits only when a repo requires branches to be up to date. **An absence produces no
output to be suspicious of.** After the fix all twelve open PRs flipped to `BEHIND` at once — the control
went from never emitting that status to emitting it on every row, which also proves they had ALL been
behind while six reported `CLEAN`. ⚠ **`publish` is deliberately NOT a required check** (it reports
SKIPPED on PRs, and a required check that never reports hangs a PR forever). ⚠ **A package-only PR would
skip 7 of the 9 required checks and hang**, because `e2e.yml` and `security.yml` path-ignore
`tamheed-package/**` while `ci.yml`'s PR trigger ignores only `*.md`. Acceptable — package writes go
direct — but read this before debugging a stuck package PR. ⚠ **Residual: `DW-081`** — `SEC-120` mandates
*"Minimum 1 approver"* and the config sets **0**, because GitHub forbids self-approval and this is a
one-human team: the clause is UNSATISFIABLE, not merely unmet.

⚠⚠ **WHAT THIS RESUME PASS ACTUALLY VERIFIED, stated so the next session does not over-trust it.**
The MECHANICAL pass ran over every cited identifier — all resolve except `DEF-082`, the
known gap `DEF-101` records. ⚠⚠ **THE COUNT THAT SAT IN THIS SENTENCE READ `206` AND ITS OWN PROGRESS
ENTRY READ `217` — ONE PASS, ONE COMMIT (`c043ed6`), TWO NUMBERS.** That is the NINETEENTH (`PE-613`).
Neither is recoverable, because the regex that produced them was never written down. **Measure it —
`scripts/count-prompt-ids.py`, committed for exactly this reason, prints the distinct-id and family
counts and names the pattern it used**, so the next reader can disagree with the instrument instead of
with a number. It now RESOLVES every cited id against the JSONL too: **all resolve except `DEF-082`.**

✅ **`PE-612`'s DECLARATION IS DISCHARGED: §2–§5 WERE READ THIS PASS**, which is why traps 19, 22b, 23 and
27 changed. ⚠⚠ **AND ONE OF THEM WAS NOT MERELY STALE — IT WAS THE CAUSE.** Trap 19 said *"write the
message to a file instead"*, and following it into a FIXED path is precisely how a commit took a stale
`/tmp` file from another session as its message. **A trap can be actively wrong, not just out of date;
read them as claims, not as scripture.**

⚠ **WHAT WAS *NOT* DONE, so nobody inherits an unearned assurance:** the traps were read and corrected
where this session had evidence, but they were **not independently re-verified against the code** — a
trap that has quietly stopped being true, and that nothing this session happened to touch, would have
survived. §1's candidate rule and §3's register claims were likewise read, not re-measured.

⚠ **ONE SLICE OF EIGHT IS AN OPERATOR OVERRIDE (`DEC-071` d3), not a judgement this file endorses.** The
recommendation was three slices, because `DEC-068` d1 justified a single slice from the rows being *small
with their machinery already in place* — untrue of a create-stream command, of every number in the SPA, and
of an authorization surface. The mitigation is per-item: **each row gets its OWN acceptance criterion
recorded in the batch that produces its evidence**, so the exit is adjudicated per item, never in aggregate.

⚠ **`DW-069` IS DELIBERATELY NOT IN THIS SLICE** (`DEC-071` d2). The operator said "all" to the nine and
"leave it, not now" to the glossary; the narrower answer governs. **Consequence:** `NFR-039` stays
unmeetable — its clause two is *undecidable*, not merely unverified — and `DW-076` (the `TopicSource`
picker) stays blocked, because the nine Arabic source labels have no canonical source.

⚠ **STILL NOT SCHEDULED, and this is deliberate (`DEC-071` d4, holding `DEC-068` d3):** the v1
release close-out. **`DW-041` (WCAG manual pass), `DW-067` (Firefox/WebKit matrix) and `DW-071` (alt-text
route coverage) therefore do NOT fire** — their triggers name "before release sign-off". `DEF-087` stays
carried and **open** rather than Won't-fix (`DEC-071` d5), so the historical blindness stays visible.

---

**WHAT `SL-032` TAUGHT, kept because it binds future work.** `PE-578` the CSP spike, `PE-579` its build,
`PE-583` `WBS-23.4`, `PE-585` a correction, `PE-586` the review, `PE-589` `DEF-104`.

### ▶ WHAT `SL-032` DID — HISTORY, and it is CLOSED.

⛔ **THIS HEADING SAID *"the live slice is the block ABOVE this one"* AND IT WAS THE FORTY-SECOND.** The
block above is the `DEC-075` interview, and the live slice has changed three times since. **Never point at
a block; the live slice is whatever `entity_query("slice", status="Approved")` returns, and the next
action is the numbered list under `START HERE`.**

**`PH-7` → `SL-032`**, four small activated rows in `DEC-068` d1's order. **Measure the statuses; do not
trust this list** — `readiness_check(scope="slice", id="SL-032")`.

- ✅ **`WBS-23.1` / `DW-061`** — mobile notice. PR #301 → `78b5ca2`, `AC-140` Met (`AV-218`).
- ✅ **`WBS-23.2` / `DW-040`** — drag-to-reprioritize. PR #302 → `145d9bf`, `AC-141` Met (`AV-219`).
- ✅ **`WBS-23.3` / `DW-038`** — PNG chart export. PR #304 → `ada5fe2`, `AC-142` Met (`AV-220`).
  Needed `ADR-0044` + `SC-030` first; see the block above. `DW-038` is `Done`.
- ✅ **`WBS-23.4` / `DW-032`** — triage reclassification. PR #305, `AC-143` (`FR-164`). ⚠ **Its sizing was
  RIGHT** — the domain method really was the only missing piece — **but the REGISTER was not**: the
  pre-build keyword sweep found **no requirement anywhere** covering reclassification, and `DEC-070` +
  `SC-031` created `FR-164` before the build could be recorded. **Read `PE-583`.**
  ⚠⚠ **CLOSING `23.4` DOES NOT MAKE `SL-032` READY BY ITSELF.** Slice-scope `wbs-done` also names
  **`WBS-23`, the PARENT** — check with `readiness_check(scope="slice", id="SL-032")`. Precedent from
  `WBS-21` and `WBS-22`: the parent goes `Implemented` once every child is. ⚠ **The SLICE's own
  `Implemented` is the OPERATOR's verdict, not yours** — done-claimed is `Review`, and the ceremony is
  `prompts/slice-review.md`.

⭐⭐ **`DEF-087`'s fix-forward rule WORKED END TO END.** Slice-scope `wbs-done` named **five** items when
`SL-032` was created and ran **5→4→3→2→0** as each leaf and then the parent `WBS-23` closed — the first
slice exit that rule has ever been able to adjudicate rather than pass over an empty set. It still returns
zero rows for the older slices, whose items carry no `slice_id`. **Keep new work items carrying
`slice_id`** — that is the entire fix, and `SL-033`'s items already do.

⚠⚠ **TWO ROWS IN THIS SLICE HAD WRONG SIZING, AND BOTH SAID "only the gesture / blocked on nothing".**
`DW-040` needed a backend change (the operation was a SWAP — indistinguishable from a move at ±1, wrong
for any longer drag) *and* a new addressing mode (the kanban renders the **filtered, sorted, page-
truncated** backlog, so a client-computed position addresses a different sequence — it sends the
TARGET'S IDENTITY and the server resolves both ends; **a test exists whose whole purpose is to fail if
anyone "simplifies" that back to a delta**). `DW-038`'s is above. **Read the code before believing a
row's sizing** — that habit paid three times in this slice.

⚠ **Both acceptance criteria name what they do NOT cover, deliberately.** `AC-140` covers `NFR-063`'s
NOTICE clause only, not *no-broken-layout* across 52 routes. `AC-141` does not claim every topic is
reachable by the gesture. Do not read either as wider than it says.

⚠ **Watch the backend applock test.** The first CI run on #301 failed `AuditAtomicityTests.
Concurrent_commands_all_commit_without_forking_the_audit_chain` with *"audit-chain applock not
acquired"*; the re-run passed on **identical backend code**. Two observations of one commit range
disagreeing with itself. **If it recurs, file a defect** — the audit chain is hash-linked and therefore
inherently serialising, which `DW-053` names as the shape where throughput surprises you. **Do not
label it flaky on the strength of one more green.**

⚠ **New defects from this slice:** `DEF-103` **Fixed** (the kanban rendered a silent 25-row prefix;
now `KANBAN_PAGE_SIZE = 500` plus an actionable notice — the residual ceiling is named, not hidden).
`DEF-104` **Fixed** (PR #306 → `bdbd8b6`, `PE-589`) — **eleven** paged reads accepted an unbounded caller
page size and **two** already capped it, so the correct pattern existed in-repo and was simply not applied.
⚠⚠ **THE ROW'S OWN COUNT SAID TWELVE WHILE ITS OWN ENUMERATION LISTED ELEVEN**, and two sweeps on
different keys agree on eleven — **neither was complete alone**: the identifier sweep (`PageSize`) returned
**ten**, blind to the audit endpoint and `GetDecisions`, which page with locals named `size` and `n`.
⭐ The fix is **one** shared `PageSize.Clamp` (`Acmp.Shared/Application/Pagination/PageSize.cs`, `Max = 500`,
called from eleven files), 500 because that is the largest page the SPA itself requests — **copying
`GetNotifications`' 50 would have broken reports and the kanban.** ⚠ `GetDecisions` with a NULL limit still
does **no** `Take`: capping where no cap existed is `DEF-103`'s silent-truncation shape.
⚠ **This paragraph used to predict "twelve modules plus tests is a slice of its own", and it was wrong**
in both halves — eleven, and one shared helper closed them all in one PR.

### Open, and the operator's alone

- ⛔ **`DW-066` IS NOT OPEN AND IS NOT THE OPERATOR'S — IT IS CLOSED, AND THIS BULLET SAT UNDER A HEADING
  READING *"Open"*.** It shipped inside `DW-080` phase B exactly as `DEC-074` d2 required, in the same
  change as the `FROM`-line edit. **That was the FORTIETH's fifth instance**, and the worst-placed of them:
  the other four merely said the wrong thing, while this one said it under a heading asserting the row was
  outstanding. ⭐ **The durable half survived the outcome and is worth keeping: the RISK WAS NEVER THE
  EDIT** — two `FROM` lines against SQL Server native interop and Arabic culture-aware work. ⚠⚠ **And the
  spike proved the risk was not even musl** — it is the ICU **version**: `alpine-extra` ships ICU 78.1
  against `chiseled-extra`'s 74.2, and CLDR moved `ar-SA`'s default calendar between them, so the same
  binary renders Hijri on one and Gregorian on the other, **with no exception and no log line, in Arabic
  only.** A green unit suite proves nothing here, which is why the row demanded a full e2e leg.
- **`DW-074` + `DEF-100`** — `NFR-019` mandates TLS on three internal hops; app↔Keycloak and nginx↔api run
  plaintext on the Docker network. **The operator KEPT the requirement rather than narrowing it**
  (`DEC-066`), so `NFR-019` stays `Approved` and correctly has **no AC**, and `DEF-100` stays **open
  deliberately**. ⚠ Not a config edit: service-to-service TLS needs a certificate story, and the public
  certbot flow does not extend to services addressing each other by compose name.
- **`NFR-018`** — ⛔ **THIS BULLET HAS CARRIED A WRONG CLAIM TWICE AND NOW CARRIES NONE.** It first said
  *"the only remaining requirement real work could close"*; `NFR-064` falsified that (the FORTY-SECOND);
  then `NFR-064` and `NFR-065` both reached `Implemented`, falsifying the correction (the FORTY-THIRD).
  **Run §1's candidate rule rather than reading any answer from here.**
  `NFR-018` needs an **external OWASP ASVS 5.0 Level 2 assessment**. Preparable, not closable. ✅ **The evidence pack is now SCHEDULED work, not
  a suggestion** — and it is **BUILT**: `DW-079` closed as `WBS-25.2` (`DEC-093`), and the pack is
  `DOC-070` at `tamheed-package/docs/asvs-l2-evidence-pack.md`. *(A status stood here too — the FORTIETH's
  sixth instance.)* ⛔ **`NFR-018` ITSELF DID NOT MOVE AND MUST NOT**: the pack is an INPUT to an
  assessment, never the assessment, and no acceptance criterion may be written from it (trap 16c).
  Commissioning the assessment itself remains the operator's act alone.
- **The running-stack group** — `DW-065` (span PARENTING across modules, still unobserved), the ops group
  (`NFR-015 017 044 052 062`, `PE-485`), and much of `DW-043`…`DW-060`, several of which are measured FROM
  trace data. ⚠ **`DEF-099` is fixed, so traces now actually arrive** — that blocker is gone.
- **`release-close-out.md`** exists in the prompt library and has never been run. ⛔ **NOT DUE, and the
  phrase *"with every phase closed"* that stood here was the FORTY-SECOND**: `PH-3` and `PH-7` are
  `Approved`, and `PH-7` is the LIVE phase holding `SL-035`. It is the ceremony that would formally end
  v1 — **after** the open slice closes, not while it is open.

### ⚠ HOW TO RUN A STACK HERE — batch 17 proved this and the next stack batch should copy it exactly

`docker ps` showing no ACMP containers does **not** mean it is safe. **Five populated volumes exist**, and
`scripts/dev-up.sh` is `up -d --build` — the documented breaker, because SQL Server keeps the VOLUME's
original SA password and ignores the env value, so a recreate fails its healthcheck and the recorded
recovery is `down -v`, destroying all five.

1. Use an **isolated compose project on FRESH volumes**, same compose and env file, so the config under
   test is the shipped one while the dev stack's data is **structurally unreachable rather than avoided**.
2. **Tag an existing CI image** to the name compose expects, or it rebuilds the 3.62 GB FTS image.
3. Bring up `sqlserver` + `seq` **alone** and confirm healthy **before** the api.
4. Tear down `down -v` (yours, not the dev stack's) and **remove the tag**, so a later `up` cannot silently
   reuse a stale image. Then verify all five dev volumes are still there.

### ⚠⚠ TWO THINGS FROM THE FIRST `SL-032` BUILD — both cost a CI cycle, both generalise

**1. COMMITTING TO `main` IS NOT PUBLISHING TO `main`.** The branching rule says package writes go
straight to `main`, and I followed it faithfully for **ten commits** — and never pushed. So `main` was
ten ahead locally, the feature branch inherited all ten, and `gh pr merge --squash` folded **every
package commit plus the web change into ONE commit** titled after the web feature. No content was lost
(verified by stat); ten commit messages were. ⚠ **After a package commit on `main`, PUSH.** Check with
`git rev-list --left-right --count HEAD...origin/main` before branching — a non-zero left number means
the next branch will carry work that is not its own.

⚠ Trap 25 also fired on that merge exactly as written: merged **remotely**, aborted **locally**,
working tree left looking like pre-feature `main`. The documented recovery worked — verify the content
is in `origin/main` **by content, not ancestry**, then reconcile. ⚠ And back up `tamheed-package/data`
first: C31 means uncommitted package writes die to `reset --hard`, and there were some.

**2. ⚠⚠ ARABIC MORPHOLOGY BITES TEST ASSERTIONS, NOT JUST UI COPY — AND THE FAILURE MESSAGE HIDES IT.**
A Playwright assertion looked for a literal Arabic substring. CI reported it **missing from a received
string that visibly appears to contain it**. It genuinely was absent: the preposition `لـ` absorbs the
alef of the definite article `ال`, so the phrase renders contracted and the standalone spelling occurs
nowhere. `DEC-032` already records this rule — **Arabic morphology is a RULE, not a string
substitution** — but records it about *renaming UI copy*. A test assertion is a nastier venue, because
the diff output makes it look like a tooling fault. **Never assert a hand-picked Arabic fragment.
Assert the PROPERTY** — a run of `[؀-ۿ]` proves the bundle resolved, survives every
rewording, and pairs with a key-echo check. Verify any such regex discriminates: it must match AR, and
**not** match EN, and **not** match the literal key path.

### ⚠ THE LESSON THAT CHANGED HOW YOU WRITE FOR THE OPERATOR — and it BINDS

**`LL-011` (Approved and PINNED — the confirmation interview is DONE; this is not a proposal): AN
IDENTIFIER IS A POINTER, NOT A
REFERENCE.** The first version of the disposition slate cited ~40 records by id alone and asked the
operator to rule on them. **They refused the interview on exactly that ground.** An id is an index into a
store the reader may not have open, so citing one hands the retrieval work to the person the artifact
exists to serve, at the moment they are deciding. Ids *read* as precision and are genuinely traceable —
for the agent, which can resolve every one in a tool call. The asymmetry is invisible from this side.
**Any artifact the operator reads to DECIDE must carry each cited record's own text where it cites it**,
quoted from the JSONL by a generator, never paraphrased and never re-typed. The test is mechanical: could
a reader who has never opened this package adjudicate every question using only the artifact?

⭐ **The remediation found a defect no gate can see.** Making the generator resolve every identifier in
every quoted record and fail loudly on the unresolvable turned up **`DEF-082`, which does not exist** —
yet two defect rows and a progress entry cite it as a real, diagnosed, fixed defect and restate its root
causes. The register runs 1–100 with **82 the only gap**. `G-IDS` passes because it checks foreign keys
and the entity index, **not identifiers embedded in prose**. Filed as `DEF-101`; the row is NOT
reconstructed, because writing a defect after the fact from second-hand narrative is close enough to
manufacturing a status that it is the operator's call. **Closing the reference graph over an artifact is a
cheap instrument no gate provides, and it found this on its first run.**

### Closed 2026-08-20 (later session) — do not re-carry these

The deferred-work disposition (`PE-556`, `PE-558`) · `DW-037`'s unapplied activation (`SC-028`) · the
`assumptions-current` blind control (12 dates, `PE-557`) · the twelve demand rows (`DEC-067`, `SC-029`).
**New rows to know:** `DEF-101` (missing `DEF-082`), `DEF-102` (`NFR-013` mandates a columnstore that
`ADR-0022` removed — operator chose *record it, change nothing*; the cluster also includes `DEC-020`,
`ADR-0003` and `OQ-040`, which all still assume it), `LL-011`, `SC-028`, `SC-029`, `DEC-067`.

⚠ **`DW-052`'s premise is WRONG and its "closeable today" half is not closeable** — the upload options
carry **50 MB** and **2 GB**, not `NFR-011`'s 100 MB, and nothing overrides either. An AC over the
validators would verify the wrong numbers.
⚠ **`DW-037`'s data claim was half wrong**: `Topic.Schedule` does **not** persist the meeting id — it
raises `TopicScheduledEvent`, which has **zero consumers**, and `Topic` has no such column. The calendar
work is genuinely unblocked but **must read from the Meetings API** (`MeetingDetailDto.ScheduledStart` +
`AgendaItemDto.TopicId`), never from the Topics API the row names.

### Closed earlier 2026-08-20 — do not re-carry these

`DEF-093` · `DEF-095` (#298) · `DEF-097` (#299) · `DEF-098` (by reconciliation, `SC-025`) · `DEF-099`
(#300 — the OTLP export defect) · `DW-026`, `DW-027`, `DW-059`, `DW-062`, `DW-064` · `OQ-074` ·
`NFR-023`, `NFR-026`, `NFR-027`, `NFR-028`, `NFR-034`, `NFR-050` · the `SL-031` programme itself and the
`PH-6` phase gate · risk owners (0 of 23 lack one) · the three customized prompts' hand-merge (nothing to
merge) · `prompts/README.md`'s stale version (refreshed via `handoff_emit(refresh_stock=true)` — it was
classified **stale-stock**, so the TOOL fixed it; a hand edit would have opted it out of every future
refresh, permanently and silently).

Report the state and your plan before writing, then proceed.

=====
