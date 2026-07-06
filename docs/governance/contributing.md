---
status: Approved
version: 1.1.0
updated: 2026-07-06
owner: lead-secretary
---

# Contributing — ACMP

How work lands in ACMP: one branch per unit of work, a reviewed and CI-green PR into `main`, squash-merge, delete branch, sync. `main` stays green and deployable at all times. Full sources: `docs/governance/contributing.md` (workflow) and `docs/domain/devsecops-plan.md` (CI gates).

## Git workflow

`main` is protected — always green, always deployable. **No direct commits or pushes to `main`.**

**One branch per unit of work:**

- Phases: `feat/P{n}-<slug>` (e.g. `feat/P5-topics-backlog`).
- Change-slices / fixes: `fix/<slug>` or `chore/<slug>` (e.g. `chore/keycloak-self-host`).

**The sequence for every phase or slice:**

```bash
# 1. start from a fresh main
git switch main
git pull --ff-only

# 2. branch
git switch -c feat/P{n}-<slug>

# 3. build the phase in small conventional commits

# 4. run the FULL CI suite LOCALLY and get it green BEFORE pushing (never push red code)
#    e.g. dotnet build && dotnet test && dotnet format --verify-no-changes \
#         && npm run lint && npm test   (+ i18n parity + architecture/boundary tests)

# 5. push (only after local CI is green) + open the PR
git push -u origin feat/P{n}-<slug>
gh pr create --base main --head feat/P{n}-<slug> \
  --title "P{n}: <summary>" \
  --body "Implements P{n} (<module>). AC: <AC-###>."

# 6. monitor remote CI until green
gh pr checks --watch          # if red: fix on the branch, push, re-watch until green

# 7. after Review = GO and CI green: squash-merge + delete branch
gh pr merge --squash --delete-branch

# 8. sync local main for the next phase
git switch main
git pull --ff-only
```

**CI is the gate twice:** get it green *locally before pushing* (never push red code), then *monitor remote CI* after pushing until green. Never request a merge while CI is red.

**Merge only after** the phase Review returns **GO** *and* CI is green. Squash-merge (linear history), delete the branch, then branch the next unit of work from a clean `main`.

**Roles:** the execution agent (Claude Code) branches, commits, pushes, opens the PR, and reports CI status — but must not merge until the human's Review returns GO and CI is green. The human (Secretary / Tech Lead) runs the Review and merges (or authorizes the merge). If `gh` is unavailable, the agent pushes the branch and the human opens/merges the PR in the web UI.

## CI gates

Every PR into `main` must pass these required checks before merge. All run on a self-hosted runner (`docs/domain/devsecops-plan.md` §3 has the full stage table).

| Gate | Command / tool | Blocks PR when |
|---|---|---|
| **Format** | `dotnet format --verify-no-changes`; Prettier `--check` | Any formatting diff |
| **Lint** | `dotnet build /warnaserror` (Roslyn analyzers, nullable); ESLint (strict TS); oxlint | Any error |
| **Build** | `dotnet build -c Release`; `vite build` | Build fails |
| **Backend coverage** | xUnit + Coverlet, per-file coverage (`scripts/check-coverage.mjs`) | Coverage below threshold — **≥95% per file** (ADR-0016) |
| **Frontend tests** | Vitest + React Testing Library; jest-axe inline a11y | Test failure |
| **i18n parity** | `scripts/check-i18n.sh` | EN↔AR key set diverges (missing AR key) |
| **Architecture / boundary** | ArchUnit-style module-boundary tests | A module reads another module's tables or imports its internals |
| **SAST** | CodeQL (C# + JS/TS), `security-extended` | High/Critical finding |
| **Dependency scan** | `dotnet list package --vulnerable`; `npm audit --audit-level=high` | High/Critical vulnerability |
| **Secret scan** | Gitleaks (PR diff + full history on first run) | Any secret detected |
| **Integration tests** | xUnit + Testcontainers (SQL Server + MinIO) | Failure |
| **Migration test** | EF migrations on a fresh Testcontainers SQL Server; idempotency check | Failure |
| **Container scan** | Trivy image scan | Critical CVE |
| **E2E** | Playwright (Chromium + Firefox) + `@axe-core/playwright` | Critical / happy-path failure |

`dotnet test` passing is not the same as CI green: run `dotnet format --verify-no-changes` and the per-file coverage check locally too, since format (BOM/charset) and coverage gaps fail CI even when tests pass.

A build is **security-cleared** when CodeQL, the dependency scans, Gitleaks, and Trivy are all clean (or every finding has an approved, referenced suppression). Dependency exemptions live in `docs/security-exemptions.md`, require lead + security-owner sign-off, and expire after 90 days.

## Working discipline (carried from the pre-migration agent guardrails)

Three standing rules for any agent or contributor working the package (restored from the pre-migration `execution-handoff/agent-guardrails.md` "DO" list; mirrored in `AGENTS.md`):

- **Validate before claiming.** Never assert an `AC-###` is Met, a test passes, or CI is green without demonstrable evidence (a run, a log, a link). Report facts, not claims.
- **Every artifact has an owner and a status.** New or changed package documents carry front-matter `status` / `version` / `updated` / `owner`; work items carry an ID and a status. Nothing lands ownerless or status-less.
- **Keep the planning package authoritative.** When code and the package disagree, fix the code or raise an ADR/`OQ-` — never let them drift silently; update the affected register in the same PR as the change.

## Commit conventions

[Conventional Commits](https://www.conventionalcommits.org/): `<type>: <description>`, small and reviewable.

```
feat(P12-PR3): Reports shell — six view-tabs, filter, CSV export

<optional body explaining what and why>
```

**Types:** `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `ci`.

- Keep commits small and scoped to one change; a PR is a squash of the branch's commits.
- The PR title references the phase and its `AC-###`.
- Never skip hooks (`--no-verify`) or bypass signing unless explicitly asked; if a hook fails, fix the underlying issue.
