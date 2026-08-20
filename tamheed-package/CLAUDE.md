
## Tamheed progress tracking
<!-- tamheed:note v4 -->

This project executes Tamheed package `tamheed-package` (under `C:\Users\ahammo\Repos\acmp`). **The package is the record — when code and package disagree, fix the code or record a scope change; never let them drift.** **Package data lives in the git working tree** (C31): uncommitted package writes are destroyed by `git reset --hard` / `git checkout` / `git stash` exactly like uncommitted source — commit the package `data/` before branch operations. The `tamheed` MCP server is provided by the installed tamheed plugin (no project-level .mcp.json entry needed). All package reads/writes go through the `tamheed` MCP tools; ready-made task prompts live in `tamheed-package/prompts/` — start with `tamheed-package/prompts/README.md`, the operator guide (which prompt for which situation, semi-auto vs fully-auto); the human review surface is `tamheed-package/review.html`.

### Recording obligations (mandatory — unrecorded work is drift)

| During execution, when… | Record BEFORE moving on |
|---|---|
| you find a defect | `entity_upsert` a `defect` row (`DEF-`, honest severity — open critical/high BLOCK readiness) — then fix it |
| you find needed work that is out of scope | `entity_upsert` a `deferred-work` row (`DW-`) with an activation trigger |
| you deviate from the approved plan in any way | a `scope-change` row (`SC-`) FIRST, `decision_ref` naming the deciding `DEC-`/`ADR-`, delta edges (`scope_adds`/`scope_modifies`/`scope_removes`) naming the affected rows — after approval, apply the row changes and set the `SC-` to Merged |
| you hit genuine ambiguity | an `open-question` row (`OQ-`, with owner + due_by) and `[NEEDS-CLARIFICATION: OQ-NNN]` at the exact spot — NEVER assume |
| execution teaches you something durable (a mistake's fix, a practice worth repeating) | `entity_upsert` a `lesson` row (`LL-`, born Proposed; kind improve\|sustain, statement + impacts) + a `learned_from` edge to the source — the OPERATOR confirms later; only Approved lessons bind |
| you finish a unit of work | `progress_update(...)` — event_type `work-done`, `subject_id`, your `actor` string, phase/slice ids |
| you believe a slice/wbs-item is complete | set its `lifecycle_status` to **Review** (done-claimed) — `Implemented` means VERIFIED and is readiness-guarded |
| you verify an acceptance criterion | `audit_record(...)` with evidence + `verified_by` + `verification_method` + `against_commit` — never Met without proof |
| you create a commit or PR | `work_bind(ref, entity_ids=[...])` |
| you declare a slice/phase/release done | `readiness_check(scope)` first — resolve every blocking failure, or ask the OPERATOR for a `WVR-` waiver (their words; you never author your own) — `"force": true` only on the operator's explicit words |

If you cannot record (lock held, package missing), STOP and tell the operator — do not proceed unrecorded.

### Lessons (operator-confirmed — these bind every session)

- **LL-009** [improve, pinned] Verifying that no pre-signed URL can reach a notification, batch 14 wrote a line-based grep for the record's constructor and got 13 construction sites across 7 files. Suspecting...
- **LL-008** [improve, pinned] LL-005 and its trap tell you to sweep the adr, decision and open-question registers before disposing of a capability or putting an option in front of the operator. Batch 14 did ...
- **LL-007** [improve, pinned] Before trusting a clean scan, prove the scanner actually looked at something. Inject a deliberate fault and watch the count move. A tool that exits 0 over an empty file set is i...
- **LL-006** [improve, pinned] Before classifying something as built, read the thing itself. Every proxy failed in one session, each time while I was correcting the previous proxy's failure: (1) the requireme...
- **LL-005** [improve, pinned] Before asking the operator to dispose of a capability — wire it, drop it, record it as intentional — search the requirement register for a row that already covers it. An option ...
- **LL-004** [improve, pinned] When cross-checking git against a Tamheed package, a commit whose entire content IS the package write can never contain a reference to its own sha: the MCP write happens FIRST, ...
- **LL-003** [improve, pinned] When something needs the OPERATOR — a decision that is theirs to make, or an action only they can perform — START THE INTERVIEW IMMEDIATELY. Do not report the blocker and wait t...
- **LL-002** [improve, pinned] When a decision belongs to the operator, run the interview EVERY time. Do not bank their earlier answers as a starting position, a default, or a shortcut for a later ceremony. A...
- **LL-001** [improve, pinned] When a repair payload has been generated from canonical source data, PASTE it into the tool call — never re-type or re-transcribe it. The hand is the untrusted transport: re-typ...

### Tool cheat-sheet (execution loop)

- `progress_update(entries=[{entry, event_type?, subject_id?, actor?, corrects?, phase_id?, slice_id?}])` — append TYPED progress (correct via a `correction` event, never edit)
- `audit_record(verdicts=[{ac_id, verdict: Met|Partial|Not-met|Pending, evidence?, verified_by?, verification_method?, against_commit?}])` — evidence ref = evidenced, not narrated
- `work_bind(ref, entity_ids=[...], note?)` — stamp a commit/PR onto entities
- `entity_query(type, id?, status?, columns?, limit?)` — rows + total
- `trace_query(entity_id, direction: out|in|both, relation?)` — typed links
- `entity_upsert(entities=[{type, id, ...}])` — FULL rows, even for updates
- `gate_run()` — mechanical gate verdict · `readiness_check(scope, id?)` — is it actually DONE (waivers honored, Review counts open)
- `export_html()` — refresh review.html · `server_info()` — version + root
<!-- /tamheed:note -->
