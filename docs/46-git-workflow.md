# 46 — Git Workflow (Branch-per-Phase, PR-into-`main`)

**Purpose:** every phase and change-slice ships via a short-lived branch and a reviewed, **CI-green** Pull Request into the default branch (`main`). **No direct commits to `main`.** Added 2026-06-25 (after the scaffold landed on a single branch); binding on the execution agent.

> Default branch is **`main`** (the repo's default). Wherever this doc says `main`, use the repo's default branch.

## Rules (binding)

1. **`main` is protected** — always green and deployable. Never commit or push directly to `main`.
2. **One branch per unit of work** — each phase `P{n}` and each change-slice gets its own branch.
   - Phases: `feat/P{n}-<slug>` (e.g. `feat/P5-topics-backlog`).
   - Change-slices / fixes: `chore/<slug>` or `fix/<slug>` (e.g. `chore/keycloak-self-host`).
3. **Small conventional commits** on the branch.
4. **Open a PR into `main`** with a title + body linking the phase and its `AC-###`.
5. **CI is the gate — twice.** (a) **Before pushing**, run the full CI suite **locally** and get it green — never push red code. (b) **After pushing and after opening the PR**, actively **monitor the remote CI on GitHub** (`gh pr checks --watch` / `gh run watch`) until it is green; if it goes red, fix on the branch, push, and re-monitor — repeat until green. **Never request a merge while CI is red.** The suite = build, unit + integration tests, lint/format, i18n-parity EN↔AR, architecture/boundary tests, security baseline (`docs/32`).
6. **Merge only after** the phase **Review = GO** *and* CI is green. **Squash-merge** and **delete the branch**.
7. **Sync after merge** — `git switch main && git pull --ff-only`, then branch the next phase from a clean `main`.

## Per-phase / per-slice sequence

```bash
# 1. start from a fresh main
git switch main
git pull --ff-only

# 2. branch
git switch -c feat/P{n}-<slug>

# 3. ... build the phase in small conventional commits ...

# 3b. run the FULL CI suite LOCALLY and get it green BEFORE pushing (never push red code)
#     e.g. dotnet build && dotnet test && dotnet format --verify-no-changes && npm run lint && npm test (+ i18n-parity + architecture/boundary tests)

# 4. push (ONLY after local CI is green) + open the PR
git push -u origin feat/P{n}-<slug>
gh pr create --base main --head feat/P{n}-<slug> \
  --title "P{n}: <summary>" \
  --body "Implements P{n} (<module>). AC: <AC-###>. See docs/_progress/acceptance-audit.md."

# 5. MONITOR the remote CI until green (after push AND after opening the PR)
gh pr checks --watch        # or: gh run watch
#     if red: fix on the branch, commit, push, and re-watch — repeat until GREEN; do not request merge while red

# 6. after Review = GO and CI green: merge + clean up
gh pr merge --squash --delete-branch

# 7. sync local main for the next phase
git switch main
git pull --ff-only
```

## Roles

- **Execution agent (Claude Code):** creates the branch, commits, pushes, opens the PR, and reports CI status. It **must not merge** to `main` until the human's phase Review returns **GO** and CI is green.
- **Human (Secretary / Tech Lead):** runs the phase Review and merges (or authorizes the agent to merge) when GO + green.

## Tooling

- Requires the **GitHub CLI `gh` authenticated** (`gh auth status`) **or** a connected GitHub integration. If neither is available, the agent **pushes the branch** and the human **opens/merges the PR** in the GitHub web UI; the agent then syncs `main`.
- **Branch protection on `main`** (GitHub → Settings → Branches): require a PR before merge, require the CI status checks to pass, and disallow direct pushes. (`docs/32`)

## Scope

Applies to **every** phase `P1–P19` (`execution-handoff/phase-prompts.md`) and to every change-slice (e.g. `execution-handoff/CHANGE-001-keycloak-ownership.md`). The **Standard Footer** in `phase-prompts.md` and **guardrail #13** in `agent-guardrails.md` enforce it.
