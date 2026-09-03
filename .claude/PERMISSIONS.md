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
noise. ⚠ **The reason it prompted is NOT that the command failed to *start* with the allowed thing** —
that mechanism is refuted below; see `DEF-135`. Claude Code splits the chain and checks each part, so
what actually defeats a chain is any *segment* with no rule behind it. **The `cd` is still worth
dropping: it is noise, and on this machine the Windows-drive spelling appears not to resolve to the
Git Bash cwd, which is enough to turn a free `cd` into a prompting one.**

Two habits, and they matter more than this file:

1. **Never prefix with `cd`.** The cwd is already the repo (`pwd` → `/c/Users/ahammo/Repos/acmp`).
2. **Prefer one command per call.** Long `&&` chains are one opaque string to the matcher — and
   `LL-049` already warns that a measurement inside a chain that has already acted is a report, not a
   control.

## ⛔⛔ THE SHAPE RULE: **EVERY SUBCOMMAND NEEDS ITS OWN RULE** (corrected 2026-09-04, `DEF-135`)

⛔ **This section asserted the wrong mechanism for a day and it produced a wrong prediction.** It read
*"the matcher matches the FIRST TOKEN of the whole command string, so anything that is not the allowed
leader defeats it"*. **That is false.** The official documentation
([`code.claude.com/docs/en/permissions`](https://code.claude.com/docs/en/permissions), §*Compound
commands*) states:

> Claude Code is aware of shell operators, so a rule like `Bash(safe-cmd *)` won't give it permission to
> run the command `safe-cmd && other-cmd`. The recognized command separators are `&&`, `||`, `;`, `|`,
> `|&`, `&`, and newlines. **A rule must match each subcommand independently.**

⭐⭐ **THE TWO MODELS DISAGREE ON A PREDICTION, AND THE FALSE ONE LOST.** Under FIRST-TOKEN a pipeline is
unmatchable and its leader is irrelevant. Under SUBCOMMAND a pipeline is allowed **exactly when every
segment has a rule**. On 2026-09-04 an agent holding this file classified
`Get-CimInstance … | Select-Object … | Format-List` as *allowed* — reasoning from the leader, which is
allowlisted — and it prompted, because `Select-Object` and `Format-List` are in neither settings file.
**Under the true model that was predictable before the command was sent.**

| Shape | Example | Why it prompts |
|---|---|---|
| `cd` prefix | `cd "C:/…/acmp" && grep -n x f` | ⚠ *should* be free — a `cd` inside the working directory is itself read-only and `cd packages/api && ls` is documented as prompting-free. **Measured here: it prompts.** Candidate cause (unproven): the Windows-drive spelling `C:/…` does not resolve to the Git Bash cwd `/c/…`, so it reads as a `cd` elsewhere |
| variable assignment | `$lock='…'; $p=Get-Process …` | an assignment is a statement, not a command — **no rule can ever match it** |
| a pipe / `&&` chain | `Get-CimInstance … \| Select-Object` | one segment (`Select-Object`) has no rule; the allowlisted leader does not carry the rest |

⭐ **THE HABIT IS UNCHANGED AND IT IS STILL ONE HABIT: ONE SIMPLE COMMAND PER CALL.** What changed is
what to check when you must chain — **every segment**, not the first token. A pipeline of allowlisted
programs (`grep … | sort | tail`) is fine; one unlisted segment sinks the whole line.

⛔ **DO NOT collapse a pipeline into `node -e '…'` or `python -c '…'`** — that was this section's advice
and it was itself a defect (`DEF-133`). `Bash(node:*)` reads as *may run scripts* and inline `-e` grants
arbitrary code, so it is gated **independently of the allowlist**. **Write a script FILE to
`.scratch/<session>/` and run `node .scratch/…/x.mjs`** — matchable, reviewable, reusable. Use
Write/Edit for files rather than `echo >`/heredocs.

## ⛔ The PowerShell tool is a SECOND tool with its OWN allowlist, and it had none

Added 2026-09-03, after the same complaint fired twice in one session. Everything above was written
about `Bash`, and it reads as though Bash is the whole story. It is not: `PowerShell` is a separate
tool with a separate matcher, and this file had **280 entries and not one `PowerShell(...)` rule** —
so *every* PowerShell call prompted regardless of how harmless it was. Three read-only calls
(`Get-Process`, `Get-Item`, `Get-CimInstance`) prompted for that reason alone.

⚠ **And the shape rule applies here too, with its own parser.** The official documentation says Claude
Code *"parses the PowerShell AST and checks each command in a compound command independently"* — `|` and
`;` split, and `&&`/`||` split **only on PowerShell 7+**; this machine runs **Windows PowerShell 5.1**.
Aliases are canonicalised, so `PowerShell(Get-ChildItem *)` also covers `gci`, `ls` and `dir`. So

```powershell
$lock='...'; $raw=(Get-Content $lock -Raw).Trim()
```

matches nothing — an assignment is a **statement**, and no rule can match a statement. And a pipeline
needs a rule for **every** cmdlet in it: `Get-CimInstance … | Select-Object … | Format-List` prompted on
2026-09-04 because only the first of the three is allowlisted. **One cmdlet per call.**

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
