---
name: a-valid-key-can-be-inert
description: A config key can be valid, correctly spelled and in a file that really loads, and still do nothing because it is only honoured at a different SCOPE — read before diagnosing any "setting is there and not working"
metadata:
  type: feedback
---

**A settings key's SCOPE is invisible at the site where the key is written.** So every reader who opens
the file confirms it is correct — and they are right about the only thing they can see.

**The instance** (`DEF-136`, `LL-058`, `PE-859` — which corrects `PE-855`; 2026-09-04).
`permissions.defaultMode: "bypassPermissions"` sat in `.claude/settings.local.json`. Valid JSON, correct
key, correct value, gitignored file, and the file demonstrably loads — its `permissions.allow` entries
work. It had **never done anything**: `defaultMode` is honoured only at **user** scope
(`~/.claude/settings.json`), in managed settings, or via a startup flag. In a project file it is
**silently ignored**, and the mode never enters the Shift+Tab cycle — so "there is no bypass mode" and
"the indicator reads auto" are one fact, not two.

Cost: **three sessions, five published mechanisms, all wrong, three shipped into commit messages that
cannot be amended.** A ~650-entry permission surface was rewritten and a shape theory was documented as
policy — both against a cause that did not exist.

---

## ⛔⛔ The second failure, which happened INSIDE the correction

The first version of this file hung a **second** mechanism on the same peg: *auto mode suspends broad
wildcard allow rules, so `Bash(grep:*)` is why a bare `grep` prompted.* **The source says the opposite** —
it suspends *"only the broad rules that grant arbitrary code execution, such as `Bash(*)` or wildcarded
interpreters"*. `Bash(python:*)` is one; `Bash(grep:*)` is not.

**I had not read the page.** I read a **subagent's summary** of it — and that summary's own examples
listed `Bash(grep *)` as *narrow*, contradicting the claim I drew from it. I shipped the claim into six
durable surfaces and three commit messages without noticing.

⭐ **`LL-006` says read the implementation, not the document describing it — and a summary of a document
is a document describing a document.** A subagent's report, a memory file, and your own earlier note are
all in that class.

**Before promoting any quoted rule to a finding, ask: did I read this, or read *about* it?** The cheapest
check that would have caught it: re-read the source you are citing and confirm it says what you concluded.

⚠ **Headlines are where hedging dies.** Every body I wrote said "candidate", "unequal confidence",
"unverified". Every *headline* said **SOLVED** / **CAUSE IDENTIFIED**. A reader sees the headline, and a
status written into prose is exactly what this project forbids everywhere else.

**Why:** nothing warns, nothing errors, no gate fires, and `grep` finds the key. A successful grep
**terminates the search**, which is worse than a failing one. This is `LL-033`'s mirror: there a pattern
fails to match the corpus's spelling; here the pattern matches perfectly and the match is never consulted.

**How to apply:**
- When a key is present, correctly spelled, and demonstrably not working, **stop checking whether it is
  written correctly** and go read *which files may set this key*. The question is never "is the file
  loaded?" — a file can load for nine keys and ignore the tenth.
- Two tells you are here rather than in a syntax fault: **(1) other keys in the same file work** — that
  proves the file loads, and it is the observation most likely to stop you looking; **(2) the failure is
  silent** — a validation error means the parser saw your key and objected.
- **Read the documentation for the tool that is consuming the config**, not just the config. This is
  [[read-before-calling-it-a-defect]] / `LL-006` aimed at the *harness* rather than at the codebase —
  when the subject is the tool you run inside, its docs are the primary source and your file is input.
- ⛔ **If the key governs the agent's own permissions, the remedy is the OPERATOR's act.** Writing a
  bypass mode into user-level settings is the agent widening its own permission surface; the classifier
  correctly refuses it, and refusing is not the same as being unable — see [[permission-prompts-four-causes]].

**Companion failure from the same session** (`PE-854`): every Bash call carried a `cd "<repo>" && …`
prefix — the exact shape the project prompt forbids — so the tools the operator named as prompting were
the agent's own doing. **An investigator generating the signal it is measuring voids its own evidence.**
Before attributing an observation, check you are not its source.
