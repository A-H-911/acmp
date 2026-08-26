---
name: sl033-slice-findings
description: "Per-item durable findings from SL-033 (WBS-24.1 to 24.4) — four different ways a planning row misled, plus the defects only a browser or a mutation could see."
metadata: 
  node_type: memory
  type: project
  originSessionId: a89ceedc-ec7e-412e-b4f7-f43f3e627b6b
  modified: 2026-08-26T17:47:15.776Z
---

# `SL-033` — what each item taught (`WBS-24.1`…`24.4`)

⚠ **Live state is `tamheed-package/prompts/prm-next.md`, not this file.** This is the durable residue only.

## ⭐⭐ FOUR ITEMS, FOUR DIFFERENT WAYS A ROW MISLED — this is the pattern worth carrying

| item | how the row related to reality |
|---|---|
| `24.1` `DW-033` | the **WBS title's SUMMARY** of the row was wrong ("dense table … verified unbuilt" — the table had shipped); the `DW-` row's own text was accurate |
| `24.2` `DW-037` | the row carried **its own correction** and it HELD — distrust the summary, read the row |
| `24.3` `DW-039` | the row was really about a **source COMMENT**, not a feature |
| `24.4` `DW-068` | the **measurement was right and the prescribed remedy too small** |

**Read each row's own text AND the code, then size it yourself.** Every item paid, in a different direction.

## Defects no unit test could see

- **`24.1`** `.table-wrap`'s `overflow: hidden` clips popovers → a control in `Table`'s toolbar slot is wrong, use `.bk-bar`. `Menu`'s default `align="end"` put the panel **off-screen both ways** (x=−123 LTR, right edge 1345 vs a 1200px viewport RTL). **`align="start"` when the trigger sits at the inline-start.**
- **`24.3`** ⚠⚠ **`white-space: pre` PRESERVES WHITESPACE, NOT CHARACTER ORDER.** In Arabic the diff rendered `# Governance charter` as `Governance charter #` and moved full stops and `-` markers — bidi reordering of NEUTRAL characters. **Fix: `unicode-bidi: plaintext`.** Any future pre-formatted or code-like surface (log viewer, JSON preview, config panel) needs it, **and none will fail a test without it.**
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
