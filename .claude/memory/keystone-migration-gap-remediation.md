---
name: keystone-migration-gap-remediation
description: Post-migration audit found + fixed agentic-surface losses; ladder is P1–P19 (not 18); three files were truncated by commit 16e0577 long before the migration.
metadata: 
  node_type: memory
  type: project
  originSessionId: af8554f0-2b34-4de6-bc46-c88f3dc8902a
---

Adversarial audit of the Keystone migration (PR #97), remediated on branch `fix/keystone-migration-gaps` (2026-07-06). Key durable facts:

- **The build-slice ladder is `P1…P19`**, canonically defined in `docs/planning/roadmap.md` §Build-slice ladder (P1–P12 shipped w/ PR numbers; P13 Webex · P14 Tarseem+Diagrams · P15 Research/Knowledge · P16 security · P17 testing · P18 deployment · P19 final audit). Per-slice prompts for P13–P19 live in `docs/handoff/follow-up-prompts.md`. Three OTHER "P-number" schemes exist in history — backlog priority codes P1–P4, the Usage Map's internal phase numbers, and early audit-era "P14" tokens — the roadmap's legacy-token map disambiguates; never renumber the Usage Map (INV-014).
- **Truncation accident predating the migration:** commit `16e0577` clipped the tails of `execution-handoff/phase-prompts.md` (P18 mid-sentence, P19 lost), `HANDOFF-RUNBOOK.md`, and the READMEs' guiding-principles list. Baseline `c487448` holds the full originals. The advisor's "P19 is a numeric error" conclusion was wrong because it verified against the truncated revision — when auditing "lost" content, always check the EARLIEST version, not just the pre-change parent.
- Design fidelity = **INV-014** (INV-007 = no secrets); the root CLAUDE.md had it mislabeled. The not-MCP clause + Usage-Map link are load-bearing parts of the prompt footer — keep them when editing `follow-up-prompts.md`/`review-prompts.md`.
- `docs/README.md` §A–§G is a pointer section (canon re-homed: §A DEC register · §B architecture.md · §C permission-role-matrix · §D topic-taxonomy · §E entity-lifecycles · §F naming-conventions · §G glossary) — ~273 citations across 43 files depend on it; do not remove.
- `AGENTS.md` is `generation: derived` — new standing rules must be sourced in `docs/governance/contributing.md` (§Working discipline) and only mirrored into AGENTS.md.
- Deferred register now D-01…D-13 (D-11 Tarseem, D-12 email, D-13 per-ballot crypto chaining → P16).
- Keystone validator lives at `~/.claude/plugins/cache/keystone/keystone/1.0.0/scripts/validate_package.py`; run it + bump front-matter `version` (and sync `docs/manifest.json`) on material package edits. See [[keystone-package-migration]], [[phase-prompt-standard-footer]].
