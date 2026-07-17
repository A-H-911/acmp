---
name: ci-gates-run-locally-pre-push
description: "Before pushing, run the FULL CI gate set locally — not just dotnet test"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 5fee7e9b-56ad-4868-ab8e-8b45a4158a32
---

`dotnet test` passing is NOT "CI green." The ACMP backend CI runs three separate gates that `dotnet test`
does not: (1) `dotnet format --verify-no-changes` (fails on encoding too — the `Write` tool creates files
WITHOUT the UTF-8 BOM the repo enforces → `error CHARSET`), (2) the per-file **≥95% coverage gate**
`node scripts/check-coverage.mjs .` (unions cobertura across all test projects; seam impls mocked in handler
tests + validator/not-found branches read via the pipeline are the usual <95% culprits), and (3) build.

**Why:** in P9a (PR #74) two CI cycles were burned — one on the coverage gate (`MeetingQuorumSource` fully
mocked, `ChangeBallot`/`RecuseVote` validators uncovered), one on file encoding (a new `Write`-created test
file lacked the BOM). Both were avoidable.

**How to apply:** before every `git push`, from repo root run:
`dotnet build acmp.sln -c Release` → `dotnet test acmp.sln -c Release --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings` → `node scripts/check-coverage.mjs .` (expect EXIT=0) → `dotnet format acmp.sln --verify-no-changes`.
After creating any file with `Write`, run `dotnet format acmp.sln` once to apply the BOM. See [[coverage-and-e2e-mandate]], [[p9-voting-plan]].

**⚠ Format the SOLUTION, never a project — scoping the check is how this bites a THIRD time (P16-B4, PR #141).**
`dotnet format <one>.csproj --verify-no-changes` exited 0 while CI's `dotnet format acmp.sln --verify-no-changes`
exited 2 with **7 `error CHARSET`** files. Reason: the scoped run only checks that project, so BOM-less files added
by *earlier commits on the same branch* (other projects) are never looked at — the gate passes locally and reds in
CI. Always `acmp.sln`. Two more traps seen the same session: **`--nologo` is NOT a valid `dotnet format` option**
(it prints help + exits 1, which reads like a real failure), and `dotnet format ... | tail` **swallows the exit
code** — redirect to a file and echo `$?` instead, or a failure looks green.
