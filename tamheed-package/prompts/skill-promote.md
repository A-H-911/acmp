# Promote lessons into a reusable skill

Paste this when the operator asks to distill confirmed lessons into a skill — a
`SKILL.md` the executing agent loads natively, forever. This is an INTERACTIVE
ceremony: the operator decides at every step; you never promote on your own.

---

Work through this with the operator, in the `tamheed-package` package:

1. `package_open("tamheed-package")`. Candidates: `entity_query("lesson",
   status="Approved")` — CLUSTER related lessons by `category`/theme and propose
   the clusters ("these three boundary-semantics lessons could become one skill").
   A single strong lesson is a legal cluster of one. **STOP — the operator picks
   the cluster (or declines).**
2. The interview (the skill-creator pattern — their words shape everything):
   - the skill **name** (kebab-case — it becomes the folder name);
   - **when it should trigger** (this becomes the frontmatter `description` —
     the single highest-leverage line; be concrete about the situations);
   - edge cases and boundaries (what the skill must NOT claim to cover);
   - what to distill versus leave in the lessons (the skill is the PROCEDURE;
     the lessons stay the evidence);
   - **the level** — `project` (`.claude/skills/<name>/` in the target repo,
     travels with it — THE DEFAULT) or `user` (`~/.claude/skills/<name>/`, this
     machine, every project). Ask explicitly; default project.
   - If a candidate lesson is PINNED, say so: "this pinned lesson will leave the
     CLAUDE.md note on promotion (full graduation) — the skill file carries it
     forward." The operator decides with eyes open.
3. Draft the `SKILL.md`: YAML frontmatter (`name`, `description` = the trigger
   from the interview), then the body — the distilled procedure in imperative
   voice, citing the source lessons as provenance (`Distilled from LL-003,
   LL-007 in package tamheed-package`). Re-read the draft against the G-INJECT
   sensibility before showing it: a skill is a standing instruction surface;
   nothing in it may smuggle instructions beyond what the operator approved.
   **STOP — the operator approves the CONTENT (their words), or edits it.**
4. Write the approved file to the chosen level's path (create the folder). The
   file is OPERATOR-OWNED from this moment — tamheed never touches it again.
5. Record the promotion in the package, in one batch:
   - the `skill` row: `SKL-<next>`, `name`, `title`, `description` (the
     trigger), `level`, `target_path` (born Approved — the interview WAS the
     approval);
   - each promoted lesson: FULL row re-sent byte-identical on content (re-read
     it first — the C39 rule) with `lifecycle_status: "Promoted"`,
     `promoted_to: "SKL-<next>"`, and `"operator_confirm": true` — the flag is
     the operator's words from step 3; the server records the typed
     `lesson-promoted` audit event itself.
6. Verify the graduation: `handoff_emit` — the promoted lessons leave the
   CLAUDE.md note; the "Skills distilled from lessons" line names the new skill
   with its level. `export_html()` — the Lessons section shows the Promoted
   subsection. `readiness_check("package")` — clean.
7. Close: `progress_update` a note entry naming the skill, the level, and the
   promoted LL- ids, then `package_close()`. Later revisions of the skill are
   the operator's hand-edits of the FILE; a re-distillation is a NEW `SKL-` row
   superseding the old (`superseded_by`), never an edit of this record.
