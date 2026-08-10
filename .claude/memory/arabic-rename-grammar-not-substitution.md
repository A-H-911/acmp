---
name: arabic-rename-grammar-not-substitution
description: Renaming an Arabic term is a grammar transformation, not a string swap — the إضافة form changes the head noun, and definiteness must be preserved or the copy turns ungrammatical.
metadata:
  type: project
---

The 2026-08-10 rename (`DEC-032`, الهندسة المعمارية → **الهيكلة**) could not be done by
substitution, and neither could the next one.

**Why.** The product's Arabic used *adjective phrases* — `الثوابت المعمارية`, `قرار معماري`
("architectural invariants/decision"). The new term is a **noun**, so the phrase becomes an
**إضافة** (genitive construct): `ثوابت الهيكلة`. An إضافة **drops ال from the head noun**
(`الثوابت` → `ثوابت`). No find-and-replace can do that; it takes a rule that rewrites the
noun *and* the adjective together.

**The trap: definiteness.** An إضافة is made definite by its **second** term. The approved
plan collapsed both `الثابت المعماري` and `ثابت معماري` onto the definite `ثابت الهيكلة`.
That ships bad Arabic in two shapes, both of which existed in the `.dc.html` prose:

- after `كل` — `لكل قرار الهيكلة` ✗. `كل` takes an indefinite, and `قرار الهيكلة` is
  *already* an إضافة so it cannot be one. Correct: `لكل قرار هيكلة`.
- before a trailing indefinite adjective — `بثابت الهيكلة قائم` ✗, `سجل قرار الهيكلة مقترح` ✗.
  `قائم`/`مقترح` stop agreeing with a now-definite head.

**So: preserve definiteness.** Definite source → `الهيكلة`; indefinite source → bare `هيكلة`.
One rule, always grammatical, and it still reproduces every example the operator approved
(`الهيكلة` · `ثوابت الهيكلة` · `سجل قرارات الهيكلة` — all definite sources).

**How to apply.** Write ordered regex rules, dry-run them printing every distinct
*before → after* pair, and read the Arabic before applying. Then two mechanical checks:

1. **Idempotence** — `transform(transform(s)) == transform(s)`. This is the proof that no
   rule's *output* is re-matched by a later rule; without it "I reviewed the dry run" is only
   true for the first rule that fired.
2. **Assert ZERO**, never a count — see [[substring-checks-bind-to-prose]]. Here it caught the
   one site no rule could reach: `L('Architecture','معماري')`, a bare adjective used as a
   standalone label with no head noun. 94 → 1 would have read as success.

**Ordering is load-bearing.** `الهندسة المعمارية` must be consumed *before* the generic
noun+adjective rule, which would otherwise read `الهندسة` as the head noun and emit
`هندسة الهيكلة`.

**Never touch** `مهندس`/`المهندسون`/`للمهندسين` — "engineers", same root, different word.
Safe from a literal `الهندسة` replace by construction: after `ال` they have `م`, not `ه`.
Also excluded, and for a reason that is *not* "generated": `docs/`, `tamheed-package/`,
`.claude/memory/`, `handoff/` quote prose **as written at the time** — renaming them
falsifies the record. See [[commit-package-writes-before-git-ops]].

**Arabic + Windows:** always `PYTHONIOENCODING=utf-8`, and write files as **bytes**
(`open(p,'wb')`) so a rename cannot rewrite line endings or strip a `.cs` UTF-8 BOM as a side
effect — verify both byte-wise afterwards. See [[ci-gates-run-locally-pre-push]].
