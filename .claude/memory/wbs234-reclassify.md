---
name: wbs234-reclassify
description: WBS-23.4 shipped triage reclassification — the DW row's sizing was right, but no requirement existed for the capability, and one of my own assertions failed its mutation test.
metadata:
  type: project
---

PR #305. `FR-164` / `AC-143` / `TEST-053`, `DEC-070` + `SC-031`. Full record in `PE-583`.

## The row was accurate — the register was not

`DW-032` said `Topic.Reclassify` exists, is correct, and is reachable from no production code. **True** —
as was `DW-061`'s. ⚠ Two of `SL-032`'s four sizings were wrong (`DW-040`, `DW-038`), not three; the
"three" was my own prose error, corrected in `PE-592`. What the row could not tell me was that
**no requirement anywhere covered the capability**. Found by the `LL-005`/`LL-008` sweep run *by keyword*
before writing code — an identifier sweep returns nothing, because the requirement register simply had no
row to find. Precedent for the fix: `DEF-086` → `FR-163`.

⚠ **`G-TRACE` wants THREE legs for a new `mvp=1` requirement.** Decision + wbs-item cleared the
`requirements_unwired` advisory while `G-TRACE` stayed red — which reads exactly like the fix not
working. `TEST-053` was the missing leg.

## Design decisions worth keeping

- **Reclassify is NOT the edit path.** `PUT /api/topics/{id}` lets the *submitter* edit their own
  pre-Accept topic with **no policy check**, so folding type into `UpdateTopicCommand` would have made
  classification self-service. `EditTopic.tsx` had already drawn this line in its own header: *"no type
  picker — type is reclassification, not an edit"*. **Read the component headers; they carry decisions.**
- **The status guard stays in the aggregate** (`DEF-059`'s rule), and the no-op check runs *before* the
  domain call — otherwise re-submitting a past-Triage topic's own classification would be a 409 for a
  request that asks for nothing to change.
- **Removing the reachability allowlist entry IS the deliverable.** A compiling mutant that stops calling
  `Reclassify` turns the Architecture suite red. That is what the allowlist file's own comment says the
  removals are for.

## Two findings about my own instruments

- ⚠⚠ **An assertion of mine failed its mutation test.** The test fixture's `source` was
  `CommitteeMember` — exactly the literal a "hardcode the source" mutant would use — so the assertion
  could not fail. **A fixture value that coincides with the obvious wrong implementation proves nothing.**
  Changed to a value nothing else in the file produces. See [[baselines-as-numbers-not-properties]].
- ⭐ **A visual defect was settled by a CONTROL, not by judgement.** The new dialog's first label sits
  flush against the description. Rather than fix it locally or change shared CSS on a hunch, I rendered
  the **shipped** convert dialog in the same harness: identical spacing. So the new dialog *matches* the
  app, and changing it alone would have made it the inconsistent one. `DW-077` carries the shared gap
  (~20 dialogs, since `<Field>` renders `.field`). **Compare against shipped UI before changing a shared
  stylesheet.**

## Deliberately not built

`DW-076` — the **source** picker. `TopicSource` has nine values, no surface has ever shown or offered one,
and there are **zero** bilingual labels for it anywhere. Authoring nine Arabic governance terms against a
canonical glossary that `DW-069` says does not exist is what `NFR-039` forbids. The endpoint accepts a
corrected source; the SPA sends the topic's existing one back unchanged, and `AC-143` says so in its text.
