# findings_17 — the v4 migration and the first register-liveness sweep

Written 2026-08-14, after migrating the store to v4 (tamheed 4.1.0) and working the advisory
ambers. Scope rule applied throughout: **merely unpopulated → populate or carry deliberately;
looks WRONG → recorded here.**

---

## A. Rows that are WRONG, not merely unpopulated

### A1. `DW-027`'s activation trigger has FIRED — the row is stale-Open

The row says *"`Stream.IsWildcard` SHIPS WITH NO PRODUCTION READER, DELIBERATELY."* That is no
longer true and has not been for some time: `UserStreamProvider` reads the column and returns
`IsUnrestricted` from it, `StreamScopeRequirement` consumes that, and as of PR #275 the
reconciliation command *grants* that stream to the rows it creates. Three production readers.

**Not closed here on purpose** — the liveness prompt is explicit that activation is a scope
decision for the operator, not the sweeper. It needs their words, then the replan-deferred flow.

### A2. `DW-026`'s trigger has fired AGAIN — a fifth instance, from this very session

`DW-026` proposes an architecture test that fails when a public aggregate method has no production
caller. This session found another one: `CommitteeMember.SetVotingEligibility` had existed since P4
with **no caller at all** until PR #276 wired it. The row's own case for existing is now stronger
than when it was raised, and coverage still cannot catch this class — coverage sees unread state,
never an uncalled method.

### A3. ⚠ My own memory mislabelled `DW-026`

The memory index asserted *"The wildcard IS read now (`DW-026`)"*. Wrong row: `DW-026` is the
architecture-test guard; the wildcard-reader subject is `DW-027`. The claim was true, the citation
was not — and a wrong citation in an index is worse than no citation, because it is followed.
Corrected in memory in the same pass that found it.

### A4. Risk titles are still truncated at exactly 200 characters

`RISK-001`…`RISK-012` all have `title` of length exactly 200, cut mid-word (`…or schema corre`,
`…EN↔AR locale switching`). This is residual **v2.3 migration damage**, not a v4 regression — and
unlike the 78 `requirements.statement` rows, **this one is fully recoverable**: the complete text
(236–395 chars) is sitting in `custom_attributes.v1.Risk` on every affected row.

Recovery recipe, should the operator want it run:
`title := custom_attributes.v1.Risk`, full-row `entity_upsert`, omitting `custom_attributes` so the
v1 blob survives (verified: omitting it preserves the blob in v4, same as v3).

⚠ Do **not** rebuild these rows from `entity_query` output — see C1.

### A5. The v4 migration dropped six milestone statuses and stashed them nowhere

`MS-001`…`MS-006` each lost `Approved`. Every other lossy change in the rewrite was stashed
(`risks` M/H/L → `custom_attributes.v3_*`) or renamed; this one is a silent drop. It survives only
in `data-v3-backup/` and in git at `967e75d`. Whether milestones should carry a status at all in v4
is a design question for the plugin, but the data loss is worth knowing about.

---

## B. Ambers that are NOISE — the register is healthier than the count suggests

### B1. `open-questions-resolved` lists 72 rows; roughly one is genuinely open

Status tally across all 76 OQs: **48 `Deferred`** (deliberately carried), **26 `Approved`**,
1 `Implemented`, **1 `Proposed`**. Spot-checked `OQ-070`: it carries a long, evidenced `resolution`
and is `Approved` — it is *answered* — yet it still counts toward the amber, because `resolved_by`
is null on every row (an artefact of the v2.3 import, which had no such column).

So this amber is measuring **bookkeeping, not open questions**. The one genuinely unanswered
question is **`OQ-074`** — *"What does 'Chairman/Secretary may preview /session' actually mean —
their own slot, or a chosen presenter's?"* — status `Proposed`, no owner, no due date. That one
needs operator words.

`resolved_by` was **not** back-filled: who resolved a question is a fact about a person, and
guessing it is exactly the manufactured-status failure this project recorded as `DEF-010`.

### B2. `risks-discharged` (blocking) cannot discriminate and is not evidence of anything

0 of 23 rows have `discharged_by` set, so the rule lists every open risk by construction. It says so
itself. Populating `discharged_by` is the fix; it is not that 21 risks are unmanaged.

---

## C. Traps discovered while doing the sweep

### C1. ⚠ `entity_upsert` demands FULL rows and `entity_query` returns text as stored — so a
round-trip through a *truncated* row silently re-commits the truncation

v4 rejects a partial update outright: *"NOT NULL constraint failed: risks.title — the row exists;
entity_upsert requires FULL rows even for updates (INSERT evaluates NOT NULL before conflict
resolution)."* That is a good, loud rule. The hazard is what you feed it: the risk titles LOOK
truncated in query output because they **are** truncated in the store, and a careless repair that
rebuilds a row from what it just read will faithfully re-write the damage and call it a fix.

**Read the JSONL, not the query, when a field may be damaged.**

### C2. Omitting `custom_attributes` on an update still preserves it in v4 — verified, not assumed

The recorded G1 hazard (sending `custom_attributes` REPLACES the whole blob, destroying `v1`) still
holds, and so does its converse. Verified mechanically: upserted `RISK-002` with title/state/owner/
strategy only, then re-read the JSONL — `v1.Owner` and the 355-char `v1.Risk` were intact.

### C3. `probability`/`impact` are NULL on all 23 risks after the migration

The v3 M/H/L values are stashed safely, but the columns await the v4 scale, which this session did
not establish. Consequence: even with owners now populated, `risk-liveness` still cannot evaluate
*"high-probability/high-impact"* — it can only check for a missing owner. Setting a scale I have not
confirmed would be inventing data, so the columns were left null.

---

## D. What the sweep actually changed

- `owner` + `response_strategy` populated on **11 risks** (`RISK-001`…`010`, `012`), every value
  **recovered from `custom_attributes.v1.Owner`** rather than assigned. `accept` where v1 said
  Accepted, `mitigate` where v1 carried a Mitigation.
- **`RISK-013`…`RISK-024` deliberately left ownerless** — the later AWS/deployment rows have no v1
  blob and no recorded owner anywhere. They are all operator-domain (instance sizing, credit mode,
  EBS, DNS, IAM, billing), so the operator is the plausible owner, but that is their call to make,
  not mine to fabricate.

## E. Left for the operator

| item | why it needs their words |
|---|---|
| `DW-027` close | activation is a scope decision |
| `DW-026` build-or-carry | it has now fired 5 times |
| `OQ-074` answer | genuinely unanswered |
| owner for `RISK-013`…`024` | no recorded value exists to recover |
| risk title recovery (A4) | a data repair beyond the amber sweep |
| `acs-slice-bound` — 20 ACs | binding is a planning judgement; several are Approved and immutable, so some need superseding rather than editing |
| the v4 risk scale (C3) | needs the plugin's definition before the columns can be filled |
