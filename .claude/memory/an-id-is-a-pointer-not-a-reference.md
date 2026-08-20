---
name: an-id-is-a-pointer-not-a-reference
description: "Operator correction — any artifact they read to DECIDE must carry each cited record's full text inline, never just its identifier"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 27ae9c8d-e210-441b-bef2-7a6d51f7bb65
  modified: 2026-08-20T20:35:53.858Z
---

**The operator refused an interview because it cited ~40 package records by identifier alone.** Their
words: *"do not just mention DW-x and expecting me to go and check it by my self... when you are
refereing to tamheed entity of any type, LL, DW, SC ... etc. do not just use it is Id to referenc it, you
have to get it is details so I can read it fully and make a decesion."* Recorded in the package as
`LL-011` (Proposed) + `PE-555`.

**Why:** an identifier is an index into a store the reader may not have open. Citing one hands the
retrieval work to the person the artifact exists to serve, at the exact moment they are deciding — so the
outcome is either a decision made on my summary rather than on the record, or no decision at all. It is
easy to get wrong because ids *read* as precision, and they are genuinely traceable **for me**, since I
have the store open and resolve each in one tool call. The asymmetry is invisible from my side: I had
already read all 53 rows, so the ids felt like references. To them they were homework. Same family as
[[verify-mechanically-not-carefully]] and `LL-001` — the difference is direction: `LL-001` governs how a
generated payload reaches the STORE, this governs how store content reaches the OPERATOR.

**How to apply:** for anything they read to decide — a slate, an interview, a disposition list, a report
— quote each cited record's operative fields inline (a `DW-` row's activation trigger, a `DEC-`'s decision
clause, an ADR's decision, a requirement's statement), **generated from `data/*.jsonl` at build time**,
never paraphrased and never re-typed. Collapsing it behind a disclosure is fine; being one lookup away is
not. The test is mechanical: *could a reader who has never opened this package adjudicate every question
using only the artifact?* Scope note — this is about operator-facing artifacts, not progress entries or
verdict evidence, where a bare id is the correct compact reference.

⭐ **The remediation is worth reusing on its own.** Having the generator resolve every identifier in every
quoted record and **fail loudly on the unresolvable** found `DEF-082`, a defect two defect rows and a
progress entry cite as real, diagnosed and fixed — and which **does not exist**; the register runs 1–100
with 82 the only gap. `G-IDS` passes because it checks foreign keys and the entity index, **not
identifiers in prose**. Filed as `DEF-101`. **Closing the reference graph over an artifact is a cheap
instrument no gate provides, and it caught this on its first run.** See
[[a-green-control-can-be-blind]] and [[scan-must-prove-it-had-a-subject]].
