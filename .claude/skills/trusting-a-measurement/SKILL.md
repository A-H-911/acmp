---
name: trusting-a-measurement
description: Use BEFORE trusting or reporting any measurement — a scan or grep returning zero, a green suite or CI run, a coverage or performance number, a detector/watchdog/healthcheck you built, a reproduction of a bug, a calibration, or any claim that something is absent, unused, fixed, or caused by X. Also use when a premise looks untestable, when two sources agree, and when a number you relied on turns out to be wrong.
---

# Trusting a measurement

A measurement can run, have a subject, return a true number, and still mean something
other than what you are about to claim. Every step below is a case where that happened
and the wrong claim was written down.

Work top to bottom. Each step is cheap — usually one command — and the ones near the
top kill the most confident errors.

---

## 1. Before running it: what would a NEGATIVE mean?

Decide this **before** the experiment, not after.

- If a negative result is equally consistent with *the hypothesis is wrong* and with
  *this instrument cannot see the difference*, the instrument has no power and its
  refutation is a coin flip you will read as a refutation. A suspected memory leak was
  measured by process **working set** on a 64 GB machine that never pressures the GC —
  the unchanged curve meant nothing. Forcing a full collection and reading retained
  managed bytes refuted one hypothesis at 345 vs 335 MB and confirmed another at 137 vs
  8 MB. Same experiment, same machine, same afternoon; only the instrument changed.
  (`LL-047`)
- If **no observation could prove your fix wrong**, it is a mitigation, not a fix.
  A remedy that reduces a *probability* buys an unfalsifiable position, and its own
  honest caveat is what makes it unfalsifiable: every recurrence gets attributed to the
  residual it disclosed. Prefer a remedy that removes the **condition** over one that
  changes the **odds** — three requests whose order in time was unconstrained became
  three requests issued *together*, which is not a smaller window but no window.
  Where only the odds are reachable, file it as a deferral and say so. (`LL-035`)

## 2. Did the instrument have a SUBJECT?

A tool exiting 0 over an empty file set is indistinguishable, at the exit code, from
one exiting 0 over a clean set — and the empty case is silent by construction.

- **Inject a deliberate fault, confirm the gate FAILS, then remove it.** Prefer the
  command CI runs over a hand-rolled one; CI's is the one whose subject is known-good.
  (`LL-013`)
- **A passing mutant is not a result.** A missing test, a mis-targeted file, a filtered
  test, a stale build and a genuinely uncovered branch all produce the identical
  observation — and it is the direction that *feels* like a completed check, because you
  did the work of writing the mutant. Count twice: prove the detector exists (watch the
  test count move), then require the failure count to move under the mutant. (`LL-013`)
- **Proving the spec RAN is not proving the assertion had anything to look at.** An
  accessibility sweep was proven to run by a real test-count delta, 86 → 88. The seed
  put every meeting one day outside the rendered month, so for weeks the assertion ran
  over an empty grid, and the chips it existed to check were the only thing capable of
  violating anything. It reported clean throughout; the violation was `serious`.
  (`LL-041`)
- **A trigger that never fires, one that always fires, and one whose silence you cannot
  read are the same fault.** The later two are worse because they produce output. If an
  empty artefact is produced identically by *it ran and saw nothing* and by *it never
  ran*, you have no instrument. (`LL-054`)

## 3. Is it the RIGHT subject?

Step 2 guards against no subject. This guards against the **wrong** one — where the
scan runs, the count moves, and the number is true of a set that excludes the answer.

- **State the denominator out loud and ask what it excludes**: which file types, which
  directories, which spellings, which quoting. Report it with the finding — *"twelve of
  fourteen paged reads"*, not *"twelve uncapped reads"*. (`LL-015`)
- **A zero is the most dangerous result**, because absence is what people act on. When a
  scan returns zero for something a record claims exists, treat the **scan** as the prime
  suspect before the record. (`LL-015`)
- **Choose a control spelled the same way as the term under test.** A control made of a
  different token proves only that the scan reached the corpus; it cannot prove your
  pattern matches the corpus's spelling. Allow the separators the corpus might use
  (`conflict[- ]of[- ]interest`), or grep the bare head-word and *read* what spelling
  comes back — the corpus's orthography is cheaper to observe than to predict. (`LL-033`)
- **Choose the control from OUTSIDE the scope you are searching.** A control picked from
  inside confirms that boundary; it cannot test it. A sweep of five registers was
  properly calibrated — the control returned 15 naming rows — and still returned a clean,
  evidenced, wrong answer, because progress entries were not in the swept scope and no
  control chosen from within could ever have revealed that. *Did the scan run* and *is
  this the right corpus* look identical from the passing side. (`LL-053`)
- **A HIT on the wrong spelling is worse than a zero and has no control at all.** A grep
  for `cal-grid` matched `mt-cal-grid` as a substring; a whole defect row was written
  about the wrong file, quoting its CSS accurately and reaching a conclusion about an
  element the failing test never touches. A zero prompts a second look; a hit does not.
  (`LL-043`)
- **A prescribed command is a claim to re-verify, not an authority to quote.** A
  committed instruction to *measure it with this command* keeps its authority long after
  the artifact it measures was rewritten underneath it. It does not error — it runs, has
  a subject, and returns a plausible wrong number. An instrument and its subject are one
  decision in two places and only one is exercised by CI. What catches it is refusing a
  number that disagrees with something you independently know. (`LL-046`)

## 4. Does the control prove COUPLING, or only FIRING?

This is the step most often skipped by people who did steps 2 and 3 properly.

- **A positive control proves the instrument fires. It cannot prove the trigger's
  SUBJECT is the fault's SYMPTOM.** A stall watchdog was mutation-checked twice, 10 of
  10 green, with an internal seam created specifically so the control could exist. Its
  trigger measured whether the *process was scheduled*. When the fault fired for real,
  drift stayed under 3 ms while eighteen requests each burned a 100-second ceiling — the
  trigger's quantity never left its healthy value, so no threshold on it could ever have
  fired. (`LL-055`)
- **Ask: what did I change in order to observe this, and does the environment this claim
  governs have it?** A build logger was calibrated against a throwaway image and proven
  to emit per-step output. It shipped; CI produced the identical silence — because the
  probe was run with `--logger "console;verbosity=detailed"` *precisely in order to see
  the output*, and that flag is the channel CI lacks. **The act of observing supplied the
  missing link, so the instrument passed its own test in the one environment where the
  fault could not occur.** Prefer a channel the target environment already uses. (`LL-060`)
- **One calibration licences one check.** A multi-check instrument sharing a parser, a
  loader and a key extractor does **not** share trustworthiness — each check embodies its
  own claim about what correct looks like. A harness with one designed control passing
  had three other checks each reporting a loud, plausible, wrong number. What killed each
  false positive was an **independent oracle**, not more care: no amount of re-reading
  your own code finds them, because the code did exactly what you wrote. (`LL-062`)

## 5. Two sources agree?

- **Ask what MECHANISM they share, not how different the tooling looks.** Two
  independently written scanners returned the same 13 sites across the same 7 files —
  and both were blind to the same 20% of the surface, because both keyed on the same
  token in the same position. Varying the parsing sophistication varied nothing that
  mattered. **Change the subject, not the sophistication**: prefer an instrument whose
  subject is an enumerable population (assert the count) over one whose subject is a
  pattern (whose denominator is whatever it happened to match). (`LL-009`)
- **Superficial difference is not independence.** A different runner, orchestrator or
  machine is not independence if both paths load the same artefact. (`LL-018`)
- **But the relationship is asymmetric.** Two non-independent observations are weak
  evidence about *where* a fault lives and **strong** evidence that a change to their
  shared component worked. Do not discount a repair's confirmation on independence
  grounds. (`LL-018`)

## 6. Reproductions, root causes, and held variables

- **A reproduction CONFIRMS; only an intervention EXPLAINS.** Recreating a failure on
  demand proves only that *something* in what you changed is sufficient. If the setup
  step altered more than one thing, it silently credits whichever one you were already
  watching. List everything the setup changed — not everything you intended it to
  change — then vary ONE thing and predict the outcome before running it. Treat the
  **fix** as the real experiment: a fix that fails under the true failing precondition
  falsifies the mechanism. (`LL-029`)
- **Record the variables you are HOLDING, not just the one you are varying.** An
  investigation varied command shape exhaustively across three sessions and never
  recorded which *paths* the commands read — and the paths were the cause. One
  controlled pair settled it in two commands. Cost of the missing variable: five
  published mechanisms, all wrong. (`LL-059`)
- **A root-path tool names A path, never THE cause.** Removing the named, plausible,
  project-owned suspect a `gcroot` path ran through changed nothing — 20 of 20 objects
  still rooted. The footer said what the path did not: *505 unique roots*. When
  retention is structural, no single removal ends the enumeration, and that inability to
  terminate is itself the tell. (`LL-048`)

## 7. Read the artifact, not the proxy

- **Classify from source, never from an attribute, a register row, or a filename.** In
  one session three proxies failed in turn, each while correcting the previous one's
  failure — and the sharpest case was a deliberate, well-commented, routed, *empty*
  shell whose own header said no bars are drawn. Every proxy said built; only the file
  said otherwise. Check both directions, and check the instrument can discriminate at
  all. (`LL-006`)
- **A layer no test can reach is not a gap in the tests; it is a property of the
  composition, and it gets worse as protection improves.** When an earlier layer refuses
  the whole population a later layer exists to refuse, the later one cannot be exercised
  from outside. Assert it at its own boundary, and give each layer a **distinguishable
  signature** — three tests all asserting 403 read as rigour while testing whichever
  layer runs first, and would keep passing if any one were deleted. (`LL-030`)
- **When a new instrument disagrees with an old one, both hypotheses predict the same
  observation** — *the new one miscounts* and *the old one was crediting things it should
  not have*. Shrink the disagreement to ONE artefact small enough to adjudicate by hand,
  get per-item output rather than a summary percentage, and read the disputed item
  yourself. (`LL-017`)

## 8. After the number is in your hand

- **A measurement that runs AFTER the action it should gate is a report, not a control.**
  In a chained command the number prints, is correct, and changes nothing — you read it
  after the push has already happened. Make the action **conditional** on the
  measurement, and the same number becomes a gate. (`LL-049`)
- **Correcting a number does not correct what was concluded from it.** Those conclusions
  stay where they were written, detached from the evidence, reading exactly like
  conclusions drawn from the corrected value. Fixing the measurement is half the repair;
  sweep for what was inferred from the old one and re-derive it, or mark it as resting on
  a superseded measurement. (`LL-020`)

---

## When a premise looks untestable

Before recording a premise as untestable, **list your instruments and ask whether every
one of them is an OUTPUT you are comparing against another output.** If so, the *producer*
of at least one output is a further instrument and is usually cheaper than either
comparison — source code, a schema, a generator, a migration. A premise carried for weeks
as unverifiable, by two separate records that each said the only test was forbidden, was
settled in one command by reading the code that produced the output. And when a record
states that something cannot be tested, treat that as a claim to check rather than a fact
to inherit. (`LL-063`)

## What this skill does NOT cover

It judges whether a measurement means what it appears to mean, and stops there.

- **Not** how to write or structure tests.
- **Not** how to read CI, git history, or build tooling.
- **Not** how to write to a planning register, quote records for a decision, or run an
  operator interview.

Those are separate concerns with their own lessons. Do not stretch anything here to
cover them.

---

*Distilled from `LL-006`, `LL-009`, `LL-013`, `LL-015`, `LL-017`, `LL-018`, `LL-020`,
`LL-029`, `LL-030`, `LL-033`, `LL-035`, `LL-041`, `LL-043`, `LL-046`, `LL-047`, `LL-048`,
`LL-049`, `LL-053`, `LL-054`, `LL-055`, `LL-059`, `LL-060`, `LL-062` in package
`tamheed-package` — those 23 are the promoted set. The lessons remain the evidence: each
carries the instance, the cost and the register rows involved. This file is the procedure.*

*`LL-063` is cited once above and is deliberately **not** promoted. Only half of it belongs
here; its other half is about re-transmitting text into a register, which is a different
concern and stays where a session writing to the package will meet it.*
