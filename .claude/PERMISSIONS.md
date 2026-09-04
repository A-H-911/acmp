# Permissions — the allowlist approach is abandoned

**2026-09-04. Everything this file used to argue was built on a false premise, so it has been reduced to
the two things that are actually true.** The old contents — a 140-line theory of command "shapes",
matcher costumes and a governance exclusion table — are in git history if anyone wants them.

## 1. The allowlist is not consulted for `Bash`

**Measured by the operator, three separate times, not inferred.** Plain single commands — `grep`, `cat`,
`ls`, `wc`, `git log`, `sed`, with no `cd`, no pipe and no heredoc, every one allowlisted in both
settings files in both matcher syntaxes — **prompt**. So does `python <script-file>`.

A bare allowlisted leader with no arguments cannot fail a prefix match. **So no rule is matching, one
cause explains every prompt, and no theory of command shapes is required.** Two full sessions were spent
building such theories, and both were wrong in a way that could not be seen from inside them.

⚠ `PowerShell` is a different tool and its rules demonstrably DO work, so the files parse and are read.

⛔ **Adding allowlist entries cannot help.** If nothing is consulted, more entries change nothing — and
the auto-mode classifier correctly refuses to let the agent widen its own permission surface, so this is
not something an agent can fix by editing config.

⭐⭐ **A THIRD CANDIDATE, FOUND 2026-09-04 IN THE SETTINGS SCHEMA ITSELF, AND IT IS THE FIRST ONE
GROUNDED IN DOCUMENTATION RATHER THAN INFERENCE — BUT IT IS STILL A CANDIDATE.** This session runs in
**auto mode**, and the official permissions documentation says that in auto mode *"a classifier reviews
actions instead of you"* — the approval table it publishes describes **Manual** mode. The settings
schema also documents `autoMode.classifyAllShell`: *"When true, every Bash/PowerShell allow rule is
**suspended** while auto mode is active so all shell commands are routed through the classifier."*

**So in auto mode the allowlist may simply not be the deciding mechanism — the classifier is.** That
would explain every observation at once: allowlisted commands prompting, a bypass MODE fixing it
instantly, and `/auto-mode-setup` (which only enriches classifier CONTEXT) not fixing it.
⛔ **Not asserted as the cause.** `classifyAllShell` defaults to false and is not set here, so the
mechanism is not established — only that auto mode routes decisions somewhere other than where this
project spent two sessions looking. `DEF-136` carries all three candidates and their isolating steps,
**all of which are the operator's**: disable the `Bash`-matched `PreToolUse` hook for one session,
restart and read the harness's startup rule-validation warnings, or compare behaviour in Manual mode
against auto mode.

## 3. Where `defaultMode` belongs

⚠ **Not in this file's sibling `settings.json`** — that file is tracked and **this repository is
public**, so a bypass mode committed there disables permission prompts for everyone who clones it. A
background security review flagged exactly that on 2026-09-04 and it was correct. Put it in
`.claude/settings.local.json`, which is gitignored and personal; local settings take precedence over
project settings, so it works from there and travels with nobody.

## 2. If you want prompts to stop, it is one setting

Not an allowlist entry. A permission **mode** — `Shift+Tab` to cycle to bypass, `/permissions` to set
it, or `permissions.defaultMode` in a settings file. That path does not go through the broken matcher,
which is why it works when nothing else does.

## What the settings files now contain

`.claude/settings.json` is a single deduplicated allowlist in the documented `Bash(cmd:*)` syntax —
about 130 entries covering the real command families once each, instead of ~650 spread over two files
with one-off literals like `Bash(echo "exit=$?")` captured from single moments. `settings.local.json`
keeps only the Playwright MCP tools and the ECC env var.

⚠ It deliberately does **not** allowlist `rm`, `git push`, `gh pr create`/`merge`/`close` or
`gh run rerun`. That is not a claim those will prompt — see §1 — it is just not granting them in config.
A permission mode overrides all of it either way.
