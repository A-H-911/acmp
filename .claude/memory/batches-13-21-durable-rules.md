# Batches 13–21 and the DW-029 close-out — durable rules

> Moved out of MEMORY.md on 2026-08-26 to keep the index under its 200-line silent-truncation cap.
> Nothing here was deleted; the index carries a one-line pointer. Live state is `prm-next.md`.

## ★ 2026-08-20 · the DW-029 programme closed — durable rules only

- ⚠⚠ **A MENTION IS NOT A COVERAGE** — matching a requirement "named anywhere in a DW row" made one
  vanish from a worklist. Match on TITLE.
- ⚠⚠ **`DEF-099`: OTLP traces had NEVER reached Seq in ANY environment.** ⭐ **Reusable discriminator:**
  events grew 679→739 while DB spans sat at **exactly 72** — same host/port/container. **Verify
  observability by sending traffic and asserting spans MOVE, never by a clean boot.**
- ⚠ **COUNT THE REQUIREMENT'S CLAUSES BEFORE YOU COUNT YOUR FINDINGS** — twice, strong evidence covered
  two-thirds of a three-part requirement and nearly carried a `Met`.
- ⚠ **The operator declined to relax a requirement TWICE.** The register states the **TARGET**, not the
  status quo — never offer a narrowing as the easy path.
- ⚠⚠ **A number entering a durable artifact must come from output visible in the same breath.**
- ⚠ **`entity_query("requirement", ...)` OVERFLOWS the token limit**; count from the JSONL.
- ⚠ **Running a stack: `docker ps` empty does NOT mean safe** — 5 populated volumes; recipe in
  `prm-next.md` §6 "HOW TO RUN A STACK HERE".
- ⚠⚠ **`entity_upsert` REPLACES FULL ROWS** — NOT NULL on UPDATE: `defects.title/severity`,
  `scope_changes.decision_ref/description/iteration`, `slices.title/objective/phase_id`, `phases.title`;
  nullable preserved by omission. **Before writing that you PRESERVED something, check the tool can.**
  A `SL-032` objective claimed to leave text "in place" in the very write that deleted it (`PE-585`).
  ⚠ **CHECKs:** `verified_by IN (human|agent|ci)`, `verification_method IN (auto-test|manual|inspection)`,
  `progress.event_type` is a fixed set (no `decision-made`). ⚠ **Approved lessons are IMMUTABLE.**

## ★ batches 13–15 — durable rules only (fuller record in `prm-next.md`)

- ⭐⭐ **Surface that DOES NOT EXIST can be excluded BY NAME in a `Met` verdict; surface that EXISTS but is
  unproven CANNOT.** That line decided every borderline call.
- ⚠⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY** (`LL-006`) — an `NFR-021` census
  read as a 38-command validation hole; all four commands carrying scalar input are guarded in the domain.
- ⚠ **Never leave a Pending/Partial AC** — `acs-met` counts by `retired_in` and ignores `lifecycle_status`, so
  an AC ahead of its evidence holds readiness false **forever**. **A part-verified requirement gets a `DW-` row.**
- ⚠ **`tsc --noEmit -p tsconfig.json` in `src/Acmp.Web` EXITS 0 OVER ZERO FILES** (`DEF-091`); `vitest` does
  NOT typecheck — use `npm run build`. ⚠ **`DEF-096`: `NFR-054`'s 500 MB cap is UNSATISFIABLE** (fts 3.62 GB);
  operator REJECTED relaxing minimal-base → `DW-066`. **Do not change a base image.**
- ⚠⚠ **Hangfire's `JobStorage.Current` + `GlobalJobFilters` are PROCESS-GLOBAL**; it never hands a filter the
  job's own exception — record `InnerException`.
- ⚠ **Coverage must be UNIONED, never summed.** ⚠ **Never push to a branch with CI in flight.**
  ⚠ `Timeline.tsx` is an honest SHELL; `Calendar.tsx`'s work is `Activated` (`DW-037`).
- ⭐ [**Store mechanics proven by experiment**](package-mechanics-proven-2026-08-18.md) — `G-TRACE` needs 3 legs.
- **Still open, needing a stack or scanner:** `NFR-018` DAST+pentest, `NFR-019` TLS scan, `NFR-052`, the ops
  group (`NFR-015 017 044 052 062`, `PE-485`), and much of `DW-043`…`DW-060` measured from trace data.
- ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND** — redirect to a file, read `$?` on the bare command.
- **PRODUCTION IS DEPLOYED AND RECONCILED**; `/readyz` 200 on all four. `RISK-007` clock started 2026-08-17.
