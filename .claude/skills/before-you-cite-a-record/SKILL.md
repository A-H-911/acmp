---
name: before-you-cite-a-record
description: >-
  Use before asserting what a requirement, decision, ADR or register row says; before offering the
  operator an option; before calling a status, count or figure stale; and before measuring anything a
  requirement specifies. More generally: use whenever you are about to write or act on a claim ABOUT
  a record you have not just read — including a claim that a record says nothing, which no keyword
  sweep can find.
---

# Before you cite a record

**Read the record. Not your memory of it, not a summary of it, not a tool's header describing it.**

The failure this prevents is not misreading. It is *not reading* — asserting what a row says from a
recollection that was once true, or from prose that describes the row rather than being it. The
assertion is usually plausible, often nearly right, and nothing mechanical can catch it: every id
resolves, every gate stays green, and the claim travels attached to real work.

---

## When this fires

- You are about to write "the requirement says…", "no decision covers…", "nothing specifies…"
- You are about to put an option in front of the operator
- You are about to call a status, a count or a figure stale, wrong or lagging
- You are about to measure something a requirement specifies — **the conditions are in the
  requirement's own text, and they bound what a valid measurement looks like**
- A committed tool told you what has already been decided, in a header, a docstring or a refusal

---

## The procedure

**1. Name the record that would have to exist for your claim to be true.**
A `DEC-`, an `ADR-`, a `PE-`, a requirement id. If you cannot name one, your claim is unsourced —
say so explicitly rather than letting it set the scope of the work.

**2. Go and read it — the field, not a description of it, and not a search over it.**
Fetch the row and read the column you are making a claim about. A substring search across a row can
match text preserved in `custom_attributes` (historical wording is often kept there on purpose), so
a search reports the old text as present and reads as though an amendment failed. *The field is the
instrument; the search over the row is not.*

**3. Sweep with two keys, not one.**
- **By identifier** — finds rows that name the thing you changed.
- **By keyword and by shape** — finds rows that discuss it without ever citing its id. An id-only
  sweep is a scan with no subject when the two rows never name each other.

**4. Sweep the surfaces your default sweep skips.**
- **Closed rows.** `Fixed`, `Won't-do` and `Obsolete` defects, and closed deferred-work rows, are an
  *unindexed decision store*: operator rulings get recorded inside them where no decision-register
  sweep will ever find them.
- **Progress entries.** The reason a status is what it is often lives only as prose in an
  append-only log that no register view surfaces.
- **The ADR's own text.** When an ADR names the rows it will amend, that list *is* the instrument:
  a row on the list without the pointer is still asserting superseded text, and a row quoting the
  same subject but absent from the list was never considered at all.

**5. For an absence-claim, sweep on the shape of a denial.**
"Nothing does X" has no keyword to search for. Key on the shape instead — `no `, `none`, `nothing`,
`names no`, `not scheduled`, `deliberately`, `standing`, `exists` — and read what each hit **denies**,
asking whether your work just filled that gap. A sweep keyed on what changed finds sentences about
the change; only this second sweep finds sentences asserting that nothing did.

**6. Read the hits; do not count them.**
Tense is invisible to a regex. *"Needed an operator decision"* followed by *"DECIDED"* is history,
not an open question.

---

## Before you call something stale

Ask **"what would this be if the ruling had been obeyed perfectly?"** If that equals what you
measured, the row is right and the drift is in your reading.

- **A status may be load-bearing rather than lagging.** Uniformity across a natural group is the
  signature of a decision. Check whether any store constraint keys on that status — immutability, a
  `CHECK`, a trigger, a foreign key — before "repairing" it.
- **A fix-forward ruling deliberately freezes a historical figure** while the total grows, so the
  row's number stays correct while the ratio it appears in goes wrong.
- **Weight by reversibility.** An irreversible flip made on an unverified cause cannot be undone.

---

## Before you measure

A requirement's own text carries the conditions a valid measurement must satisfy — the network, the
scale, the cache state, the subject. **Read them first, and check the mechanism it names actually
exists in the code.** If the named trigger, mechanism or event is absent, stop: you have found a
defect, and the measurement you were about to take would either be meaningless or would quietly
time a different event and produce a number that passes.

Measuring outside the stated conditions produces a figure that supports neither a pass nor a fail.
That is a worse outcome than no measurement, because it looks like one.

---

## What this skill does NOT cover

- **Whether a number you gathered means what you think** — that is `trusting-a-measurement`. This
  skill is about records already written, not evidence being produced.
- **Git and package sequencing** — commit ordering, branch points, `work_bind` flush timing.
- **How to WRITE a record** — which row type, what a defect must contain. Those obligations are
  tool-owned and live in the package's own instructions.
- **Operator interview conduct** — how to put a decision in front of someone. This skill stops at
  the homework that must precede the question.

---

*Distilled from `LL-005`, `LL-008`, `LL-025`, `LL-038`, `LL-040`, `LL-050`, `LL-066`, `LL-071` and
`LL-074` in package `tamheed-package`. The lessons remain the evidence and the worked instances;
this file is the procedure.*
