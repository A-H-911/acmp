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

## ⛔⛔ THE SHAPE RULE HAS THREE COSTUMES, AND NAMING ONLY THE FIRST IS WHY IT KEPT FIRING

Added 2026-09-03. In ONE session the same defect prompted the operator three separate times, and each
time it wore different clothes. **The rule is not "don't use `cd`" — it is that the matcher matches the
FIRST TOKEN of the whole command string, so anything that is not the allowed leader defeats it:**

| Costume | Example | Why it matches nothing |
|---|---|---|
| `cd` prefix | `cd "…" && grep -n x f` | the string starts with `cd`, not `grep` |
| variable assignment | `$lock='…'; $p=Get-Process …` | starts with `$lock=`, not `Get-Process` |
| **a pipe** | `tail -1 f \| python -m json.tool` | a pipeline is a compound command, exactly like `&&` |

⚠ **The pipe is the one that keeps getting missed**, because it *looks* like a single command and the
leader *is* allowed. `Bash(tail:*)` no more matches `tail … | python` than it matches `tail … && python`.

⭐ **The remedy is one habit, not three rules: ONE SIMPLE COMMAND PER CALL, and the first token is the
thing being allowed.** Where a pipeline was doing real work, push the work into the single program that
is already allowed — `node -e '…'` or `python -c '…'` can read, parse and print in one allowed leader,
which is both shorter and matchable. Use Write/Edit for files rather than `echo >`/heredocs.

## ⛔ The PowerShell tool is a SECOND tool with its OWN allowlist, and it had none

Added 2026-09-03, after the same complaint fired twice in one session. Everything above was written
about `Bash`, and it reads as though Bash is the whole story. It is not: `PowerShell` is a separate
tool with a separate matcher, and this file had **280 entries and not one `PowerShell(...)` rule** —
so *every* PowerShell call prompted regardless of how harmless it was. Three read-only calls
(`Get-Process`, `Get-Item`, `Get-CimInstance`) prompted for that reason alone.

⚠ **And the shape rule applies here too, in a second costume.** A prefix matcher matches the FIRST
TOKEN, so

```powershell
$lock='...'; $raw=(Get-Content $lock -Raw).Trim()
```

starts with `$lock=`, not with a cmdlet — and matches nothing, exactly as `cd "…" && grep` matches
nothing. **One cmdlet per call, and the first token is the cmdlet name.**

⭐ **Prefer Bash anyway.** The allowlist's coverage lives there, so a task doable in Bash should be
done in Bash; reach for PowerShell only where Bash genuinely cannot (Windows process identity, CIM,
ACLs). `Bash(ps *)` and `Bash(tasklist *)` were added so PID-liveness checks — the recurring
stale-`.lock` recipe — no longer need PowerShell at all.

⛔ Only READ-ONLY cmdlets are allowed: `Get-Process`, `Get-CimInstance`, `Get-Content`, `Get-Item`,
`Get-ChildItem`, `Get-Command`, `Test-Path`, `Get-Date`, `Select-String`, `Measure-Object`.
**`Remove-Item` is deliberately NOT among them** — it is `rm`, and the table below governs it.

## ⛔⛔ A `Write(…)` RULE IS DEAD. ONLY `Edit(…)` GOVERNS FILE WRITES — AND THE PATH FORM MATTERS

Added 2026-09-03, and it is the one fault in this whole family that **neither the operator nor the
agent could see**, because it fails exactly like a rule that is simply absent.

**`Write(path)` is never consulted by file permission checks.** `Edit(path)` rules cover ALL
file-editing tools, Write included. This file carried four `Write(.scratch/**)`-shaped entries across
`settings.json` and `settings.local.json`, every one of them inert. They have been removed; the
`Edit(…)` twin already sat on the next line in all four cases, so nothing was narrowed.

⚠⚠ **AND THE SECOND HALF IS THE PATH FORM, WHICH IS THE PART THAT WAS ACTUALLY PROMPTING.** The rules
are written `Edit(.scratch/**)` (repo-relative) and `Edit(//c/Users/ahammo/Repos/acmp/.scratch/**)`
(gitbash-absolute). **A Windows absolute path — `C:\Users\ahammo\Repos\acmp\.scratch\…` — matches
NEITHER.** So a Write whose `file_path` was the Windows form prompted while an identical Write on the
relative form did not.

⭐ **The habit: pass Write/Edit a REPO-RELATIVE path** (`.scratch/<session>/x.txt`). It matches the rule
as written, it is shorter, and it does not depend on which of three absolute spellings the matcher
normalises to.

⭐⭐ **WHY THIS ONE COST FOUR ROUNDS, AND IT GENERALISES BEYOND PERMISSIONS.** A dead rule and a missing
rule produce the identical symptom, so reading the allowlist can never distinguish them — the file
*looked* correct, and was, apart from being inert. **The agent cannot observe permission prompts at
all**: they never appear in a tool result, so every diagnosis was inference from the operator's timing.
`DEF-078`'s shape — a green control with no subject — pointed at the config surface itself. **What
settled it was the harness printing its own rule-validation warnings on session start**, which is an
instrument neither party had; nothing either of us could read would have got there.

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

⚠ **The tamheed MCP WRITE tools were added on 2026-09-03, on the operator's explicit answer** —
`progress_update`, `entity_upsert`, `audit_record`, `work_bind`, `package_close`, `handoff_emit`.
Only the READ tools had been listed, so every recording batch prompted six or more times. They
qualify under the "local, reversible" rule above: the canonical JSONL lives in the git working tree,
so a bad write is `git checkout`-able, and **`git push` still prompts** — the checkpoint that
matters is unchanged. ⛔ This was put to the operator rather than added silently, because the
exclusion table above is a deliberate governance surface and was silent on MCP writes.

An allowlist that removed those prompts would be removing the last human checkpoint in front of exactly
the actions this project's decision register spends most of its words governing. The goal was fewer
interruptions on *reading*, not fewer on *acting*.

## Scope note

`.claude/**` is path-ignored by all three workflows (`ci`, `security`, `e2e`), so changes here go direct
to `main` and run no CI. See the routing bullet in `AGENTS.md` for the full ignore list.
