---
name: sl033-slice-findings
description: "Per-item durable findings from SL-033 (WBS-24.1 to 24.6) — six different ways a planning row misled, plus the defects only a browser or a mutation could see."
metadata: 
  node_type: memory
  type: project
  originSessionId: a89ceedc-ec7e-412e-b4f7-f43f3e627b6b
  modified: 2026-08-27T13:33:05.156Z
---

# `SL-033` — what each item taught (`WBS-24.1`…`24.6`)

⚠ **Live state is `tamheed-package/prompts/prm-next.md`, not this file.** This is the durable residue only.

## ⭐⭐ SIX ITEMS, SIX DIFFERENT WAYS A ROW MISLED — this is the pattern worth carrying

| item | how the row related to reality |
|---|---|
| `24.1` `DW-033` | the **WBS title's SUMMARY** of the row was wrong ("dense table … verified unbuilt" — the table had shipped); the `DW-` row's own text was accurate |
| `24.2` `DW-037` | the row carried **its own correction** and it HELD — distrust the summary, read the row |
| `24.3` `DW-039` | the row was really about a **source COMMENT**, not a feature |
| `24.4` `DW-068` | the **measurement was right and the prescribed remedy too small** |
| `24.5` `DW-036` | **ONE WORD** ("configurable") covering a subsystem the architecture had already specified |
| `24.6` `DW-035` | the row was a **FAITHFUL QUOTATION of a clause an ADR had already superseded** |

**Read each row's own text AND the code, then size it yourself.** Every item paid, in a different direction.

## ⭐⭐⭐ `24.6` — THE ONE NO REGISTER VIEW COULD SEE, AND HOW IT WAS CAUGHT ANYWAY

`WBS-24.6`, `DW-035` and `FR-154` all said the audit export is *"accessible only to Auditor and
Administrator"*. **`ADR-0027` (Approved) decides `{Auditor, Chairman, Secretary}` with Administrator
EXCLUDED on SoD grounds — and it names *exporting* explicitly.** Building to the row's own text would
have reversed an Approved ADR and broken control `C-INS-03`.

⚠⚠ **WHY NOTHING FLAGGED IT.** Every id in those three rows resolves. Every status is correct. `G-TRACE`
passes. The requirement register agrees with itself. **`count-prompt-ids.py` and the prose-status checker
both run clean straight over it**, because the fault is not an id, a status, or a broken link — it is a
correct citation of a superseded sentence.

⭐⭐ **THE DISCRIMINATOR, AND IT IS SHARPER THAN "SWEEP THE ADRs" (trap 32 / `LL-008` only got me into the
register): `ADR-0027` RECORDED ITS OWN FOLLOW-THROUGH.** Its text says `FR-151` and `FR-153` would carry
a pointer to it. Both do, as `relates_to` edges. **`FR-154` had neither the pointer nor the edge** — it
was Phase-2 when the ADR was written and was simply missed.
**→ When an ADR names the rows it will amend, check that list against every row that quotes it. The one
it missed is the one still asserting the superseded text.** Fixed by `DEC-081` d2 / `SC-036`.

## ⭐⭐ `24.6` — three more that generalise

- ⚠⚠ **A CONTROL CAN DECIDE ARCHITECTURE.** `SEC-311` `C-AUDIT-08` requires *every export to be an audited
  sensitive event carrying who, scope and volume*. **A client-built blob cannot audit itself**, so that
  one sentence forced a SERVER endpoint — and the Reports page's client-side CSV was the obvious in-repo
  precedent and the wrong answer. **Reading `src` would have produced the cheap answer with no signal.**
- ⛔ **NEVER apply `PageSize.Clamp` to an export.** `DEF-104` taught that every paged read must cap a
  caller-supplied page size. On a compliance artifact that habit *is* the defect — `DEF-103`'s silent
  truncation, on the worst possible surface, indistinguishable from *"those rows do not exist"*. The
  anti-regression test seeds past the cap and asserts the LAST row is present.
- ⚠ **`SEC-248` is now FALSE and is deliberately not edited.** It justified not building `C-INS-01`'s
  bulk-export alert with *"ACMP has no export feature … this signal activates with `D-07`, not before"*.
  **Its activation condition named a Phase-3 ITEM rather than a property of the system, so the row could
  not see its own condition being met from another direction.** Carried as `DW-087`.

## ⭐⭐ `24.5` ADDS A STEP THE OTHER FOUR DID NOT NEED

⚠⚠ **READING `src` TELLS YOU WHAT EXISTS AND NOTHING ABOUT WHAT WAS SPECIFIED.** `DW-036` said
"retention **configurability** only". I found no settings store in the code and recommended
appsettings — which `SEC-103` makes an architectural **divergence**. `SEC-080` names the home (*"the
Configuration table (16 §2.15) holds retention settings…"*) and `SEC-103` specifies its columns. It did
not exist. **Sweep the NARRATIVE documents by keyword (`LL-008`) before sizing, not just the code.**

That sweep also found three clauses no code-reading would surface: enforcement is **Phase 2**
(`SEC-089`), the period VALUES are an open question awaiting legal (`OQ-079`), and a retention config
change is a **privileged AUDITED action** (`SEC-077`).

⚠ **The obvious grep lies here:** `class Configuration` matches only EF entity-type configurations under
`Persistence/Configurations/`. Control with `class Stream`, which resolves to a real entity. That
collision is *why* the table's absence went unnoticed.

⭐ **Three things the codebase decided, which beat designing them:** `Policies.AdminConfig` already
existed and already admitted Administrator alone · `AuditDbContext` was already the shape a cross-cutting
store needs under `ADR-0001`, so BuildingBlocks was the answer not a choice · the type had to be
`ConfigurationSetting`, because `SharedKernelExtensions.cs` already imports
`Microsoft.Extensions.Configuration`.

⚠⚠ **A NEW `DbContext` MUST BE SUBSTITUTED IN THREE PLACES, NOT TWO** — DI, `MigrationRunner`, AND
`AcmpWebApplicationFactory`. Omitting the third fails by reaching for a REAL SQL Server, which reads like
a broken environment rather than a missing registration.

⛔ **`automaticPurgeEnabled` is a CONSTANT, not a setting.** A purge job would **violate**
`NFR-059`/`NFR-060`, not complete them. **v1 shipping no period is canon** (`SEC-080`).

## Defects no unit test could see

- **`24.1`** `.table-wrap`'s `overflow: hidden` clips popovers → a control in `Table`'s toolbar slot is wrong, use `.bk-bar`. `Menu`'s default `align="end"` put the panel **off-screen both ways** (x=−123 LTR, right edge 1345 vs a 1200px viewport RTL). **`align="start"` when the trigger sits at the inline-start.**
- **`24.3`** ⚠⚠ **`white-space: pre` PRESERVES WHITESPACE, NOT CHARACTER ORDER.** In Arabic the diff rendered `# Governance charter` as `Governance charter #` and moved full stops and `-` markers — bidi reordering of NEUTRAL characters. **Fix: `unicode-bidi: plaintext`.** Any future pre-formatted or code-like surface (log viewer, JSON preview, config panel) needs it, **and none will fail a test without it.**
- **`24.5`** ⭐⭐ **`24.3`'s bidi lesson PREDICTED ITS OWN RECURRENCE BY NAME** — it said *"a log
  viewer, a JSON preview, **a config panel**"*, and in Arabic `{"years":7}` rendered `{years":7"}`.
  ⚠ **Its fix does NOT transfer: `unicode-bidi: plaintext` takes direction from the first STRONG
  character and a JSON fragment has none.** Use `dir="ltr"` on code-like elements, and keep a worked
  example OUT of translated prose.
- **`24.4`** the mockups draw `٤٠٪` with **U+066A**, the Arabic percent sign; the app glued an ASCII `%` onto the digits. **THE SIGN IS PART OF THE NUMBER FORMAT, NOT A SUFFIX** — `style: 'percent'` makes Intl pick it per locale. ⚠ Intl then appends **U+061C** (Arabic Letter Mark), so `getByText('٨٧٪')` finds nothing and the failure looks exactly like the sign being wrong. Match on content.

## `24.4` — the i18n finding that generalises

⚠⚠ **`interpolation.format` IS A SILENT NO-OP.** i18next **overwrites** it with its own `Formatter` during init, and that Formatter returns the value untouched when no format is named (`if (!format) return value`). A formatter **module** plus `alwaysFormat: true` is what fires. Keying it off the **runtime type** is what makes it safe globally — a number localizes; an entity key, an ADR id and a pre-formatted date are strings and don't.

⭐ **The general class: A VALUE PRE-STRINGIFIED BY ITS PRODUCER IS INVISIBLE TO A TYPE-KEYED FORMATTER.** Three bypasses existed (`formatDuration` returning `String(ms)`, a pre-formatted read time, `Intl.RelativeTimeFormat`'s own digits — it needs the `ar-u-nu-arab` pin too).

⚠ Bare `ar` gives **Latin** digits under Node's ICU and Arabic-Indic in a browser. **Always pin `ar-u-nu-arab`**, or the test agrees with itself while the screen disagrees.

## ⭐⭐ Hollow passes — both caught by mutation, in tests written to prove a fix

- **`24.4`**: the relative-time assertion was written on **−2 hours**. Arabic has a **DUAL form**, so that renders `قبل ساعتين` with **no digit in it at all** — it passed with or without the numbering pin. **Pick a value that actually emits the thing you assert about.** (Five hours takes the plural.)
- See also [[a-green-suite-is-not-a-look]] and `LL-022`.

## Harness gotchas

- ⭐⭐ **A GREEN e2e JOB DOES NOT PROVE YOUR NEW TEST RAN — CHECK THE COUNT.** 86→88 for ONE added test, because `playwright.config.ts` runs `rtl-a11y.spec.ts` in **both** `chromium` and `msedge`.
- Playwright `getByRole` matches `name` as a **case-insensitive SUBSTRING** — `{name:'AR'}` hit four buttons. Use `exact: true`.
- **A comment about a SIBLING's state goes stale silently** — nothing compiles it.
- A test can pass **alone** and fail in the full suite: another file mounted the component without the new API mocked.
- **Widening SHARED test data changes every test that reads it** — scope the new fixture, don't relax the old assertion.
- ⚠⚠ **`jq` IS NOT INSTALLED on this machine.** A CI monitor built on it runs silently and reports nothing — **silence reads identically to "still running."** Use `gh`'s own `--jq`.

## ⚠ `scripts/number-render-scan.mjs` — committed, and wrong three times

It lists numbers rendered **bare in JSX** (the `t()` path needs no enumeration — it's closed centrally). It is **TRIAGE, not a coverage proof**, and says so in its own header. Its three failures are the reusable warning:

1. keyed on `count|total|length` → missed the entire **reports** family, whose numbers are named `s.value`, `c.value`, `card.kpi`
2. a `\b` word boundary → dropped **camelCase** (`findingCount`, `hiddenCount`)
3. required a trailing `<` or `{` → ran past **`{act.progressPct}%`**, a number with a literal suffix

It now carries a calibration for each, plus a minimum-file-set guard. See [[scan-must-prove-it-had-a-subject]] and `LL-015`.

⚠⚠ **I then used its output as a measurement of something else** — its candidate-**line** count became "24 sites" in a pushed commit message, where the real render-site count was **31** there and **37** at merge. **An instrument's output measures the thing the instrument counts and NOTHING ELSE; if you are about to state a different quantity, run a different command.**


## ⚠ Register + CI traps from `24.5`

- ⚠⚠ **`DOC-011`'s `OQ-DATA-*` labels were INVISIBLE to the register** — zero `OQ-` rows for retention
  against a control of 78 — while **three `Met` verdicts leaned on them being open**. Filed as `OQ-079`
  (periods) and `OQ-080` (legal hold). ⛔ **`SEC-080` asserts a legal hold overrides any future purge and
  NO HOLD MECHANISM EXISTS.** Build Phase-2 enforcement without it and that guarantee goes false
  **silently**. Answer `OQ-080` first.
- ⚠ **Approved ACs are IMMUTABLE — including against being marked superseded**, which is the very path
  the refusal message names. `AC-147`'s `superseded_by` is NULL by necessity; the operator accepted it.
  **Do not "repair" it.** Cross-references can only run *from* a new row *to* an old one.
- ⚠⚠ **CI's first run died at the FORMAT CHECK with Build and Test `skipped`** — that red said nothing
  about the migration (`DEF-106`). **`dotnet format --verify-no-changes` is a committed gate; run the
  gates that EXIST, not the ones you remember.** `dotnet ef` also writes migrations as **CRLF** and in a
  block namespace, both of which this repo rejects.
- ⭐ **Prove a new test RAN by the COUNT, not the colour:** Integration **64→68**, Api **368→376**.
  ⚠ The CI log carries only per-assembly summaries, so grepping for a test CLASS name returns zero — and
  my first such grep ran over a **zero-byte download**. A control term is what exposed it.
- ⚠ `userEvent.type` parses `{` as a keyboard descriptor — use `user.paste` for JSON.
