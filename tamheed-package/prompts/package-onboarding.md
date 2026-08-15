# Package onboarding — a new agent meets the package cold

Paste this for an agent (or teammate's session) that has NEVER seen `tamheed-package` —
deeper than orient-resume, which assumes prior familiarity.

---

Onboard yourself onto the `tamheed-package` Tamheed package from zero:

1. `server_info` — server version, package root. `package_open("tamheed-package")`.
2. The why: read the charter and executive summary
   (`entity_query("narrative-document")` → `entity_query("document-section",
   columns=["id", "document_id", "heading", "body"])` for the charter's sections).
3. The rules: `entity_query("invariant")` (never violate; a violation needs a new
   ADR), `entity_query("constraint")`, and the approved `DEC-`/`ADR-` rows
   (`entity_query("decision")`, `entity_query("adr")`) — these are FINAL; do not
   re-litigate.
4. The shape of the work: `entity_query("phase")` and `entity_query("slice")` in
   order; `entity_query("wbs-item")` for the open backlog; `trace_query` from the MVP
   requirements to see how needs → decisions → work → tests connect.
5. The lessons: `entity_query("lesson", status="Approved")` — operator-confirmed
   lessons BIND you (the pinned ones also sit in this project's CLAUDE.md note);
   read them before writing code.
   Where it stands: `gate_run()`, `readiness_check("package")`, the last 10
   `progress-entry` / `audit-verdict` rows, and open `defect`/`deferred-work` rows.
6. The surfaces: `export_html()` and skim `review.html` — overview chips, the
   traceability flow, phase readiness. The prompts folder (`tamheed-package/prompts/`) is
   the situation playbook — know what's in it.
7. The obligations: read the "Recording obligations" table in this project's CLAUDE.md
   note — every one of them binds you from the first minute. On genuine ambiguity,
   never assume: create an `OQ-` row (owner + due_by) and put
   `[NEEDS-CLARIFICATION: OQ-NNN]` at the exact ambiguous spot (G-COMPLETE fails
   markers with no live `OQ-` behind them).
8. Report back: five lines — what this project is, the load-bearing invariants, the
   active phase/slice, the gate + readiness verdicts, and what you'd work on first.
   STOP for confirmation before writing anything.
