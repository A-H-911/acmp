---
name: a-valid-key-can-be-inert
description: A config key can be valid, correctly spelled and in a file that really loads, and still do nothing because it is only honoured at a different SCOPE — read before diagnosing any "setting is there and not working"
metadata:
  type: feedback
---

**A settings key's SCOPE is invisible at the site where the key is written.** So every reader who opens
the file confirms it is correct — and they are right about the only thing they can see.

**The instance** (`DEF-136`, `LL-058`, `PE-855`, 2026-09-04). `permissions.defaultMode:
"bypassPermissions"` sat in `.claude/settings.local.json`. Valid JSON, correct key, correct value,
gitignored file, and the file demonstrably loads — its `permissions.allow` entries work. It had
**never done anything**: `defaultMode` is honoured only at **user** scope (`~/.claude/settings.json`),
in managed settings, or via a startup flag. In a project file it is **silently ignored**.

Cost: **three sessions, four published mechanisms, all wrong, two shipped into commit messages that
cannot be amended.** A ~650-entry permission surface was rewritten and a shape theory was documented as
policy — both against a cause that did not exist.

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
