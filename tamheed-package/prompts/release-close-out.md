# Release close-out — package scope, blocking-clean

Paste this to close out a release of `tamheed-package` (semi-auto: forced transitions and
human gates need your explicit words).

---

Close out the release against the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")` if not already open.
2. `readiness_check("package")` — resolve EVERY blocking failure before anything else:
   - decisions/ADRs still Proposed/Draft → approve, reject, or supersede them (the
     close cannot rest on proposed decisions);
   - ACs whose latest verdict isn't Met → verify and `audit_record` with evidence, or
     retire/supersede deliberately;
   - open defects → fix, disposition, or convert to `deferred-work` (a `scope-change`
     first if that changes scope);
   - undischarged risks → discharge (`discharged_by` the proving AC/test) or move the
     risk_state deliberately.
3. Advisory findings (open questions, deferred work, unapproved EPs): review each and
   say what you decided — carrying one is legal, silence is not.
4. `human_required` gates: read each `GATE-` definition to the operator, get the
   explicit confirmation, and record it as a `progress_update` naming the `GATE-` id.
5. `gate_run()` must pass; `export_html()` — the review surface ships with the release.
6. Release notes from `entity_query("progress-entry")` since the last release;
   `work_bind` the release tag/commit to the phase and headline entities.
7. `package_close()` and commit the package `data/` with the release.
