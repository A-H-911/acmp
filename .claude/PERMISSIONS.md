# Why `.claude/settings.json` exists, and what it deliberately does NOT allow

## The problem it fixes

`settings.local.json` had accreted **90 entries, nearly all one-off literals** captured from single
moments — `Bash(echo "exit=$?")`, a whole `awk 'NR>=884 && NR<=1331 …'` invocation, entire PowerShell
one-liners. Almost none of them generalised, so every session re-prompted for the same handful of
*shapes*. `gh pr *` was allowed but not `gh run *`; `python *` was allowed but not `git status`, `grep`,
`node`, `find` or `cat`.

## ⛔ The larger cause was not the allowlist at all

The Bash tool's own documentation says it plainly: **"`cd` in a compound command can trigger a
permission prompt."** Nearly every command was being written as

```bash
cd "C:/Users/ahammo/Repos/acmp" && git status --porcelain && gh run list …
```

The working directory **already is** the repository and persists between calls, so the `cd` was pure
noise — and it meant no prefix rule could ever match, because the command did not *start* with the
thing being allowed. **No allowlist can fix a command shape that defeats matching.**

Two habits, and they matter more than this file:

1. **Never prefix with `cd`.** The cwd is already the repo (`pwd` → `/c/Users/ahammo/Repos/acmp`).
2. **Prefer one command per call.** Long `&&` chains are one opaque string to the matcher — and
   `LL-049` already warns that a measurement inside a chain that has already acted is a report, not a
   control.

## Both matcher syntaxes are emitted, on purpose

The pre-existing entries use the legacy glob form `Bash(gh pr *)`; current Claude Code documents the
prefix form `Bash(gh pr:*)`. Which one a given build honours is not readable from the file, and **an
entry that silently never matches looks exactly like one that does**. Emitting both removes the guess
at no cost.

## What is deliberately still prompting

Allowed: read-and-analyse (where the volume is) and **local, reversible** mutations — `git add`,
`commit`, `checkout`, `rebase`, `fetch`, `mkdir`, `cp`, and running `python`/`node`.

Not allowed, and this is the point:

| Kept prompting | Why |
|---|---|
| `git push` | `DEC-077` d2 — CI must be polled to completion after **any** push to `main` |
| `gh pr create` / `merge` / `close` | outward-facing; the branch → PR → green CI → squash-merge convention in `AGENTS.md` |
| `gh run rerun` | `DEC-077` d3 — a red backend job is **never** re-run; that is the operator's call alone |
| `rm`, `docker compose up/down`, `npm publish` | destructive or environment-mutating |

An allowlist that removed those prompts would be removing the last human checkpoint in front of exactly
the actions this project's decision register spends most of its words governing. The goal was fewer
interruptions on *reading*, not fewer on *acting*.

## Scope note

`.claude/**` is path-ignored by all three workflows (`ci`, `security`, `e2e`), so changes here go direct
to `main` and run no CI. See the routing bullet in `AGENTS.md` for the full ignore list.
