# Release close-out — package scope, blocking-clean

Paste this to close out a release of `tamheed-package` (semi-auto: forced transitions and
human gates need your explicit words).

---

Close out the release against the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")` if not already open.
2. `readiness_check("package")` — resolve EVERY blocking failure before anything else:
   - decisions/ADRs still Proposed/Draft → approve, reject, or supersede them (the
     close cannot rest on proposed decisions);
   - ACs whose latest verdict isn't Met → verify and `audit_record` with evidence
     plus `verified_by`/`verification_method`/`against_commit`, or retire/supersede
     deliberately;
   - open critical/high defects → fix, disposition, or convert to `deferred-work`
     (a `scope-change` first if that changes scope); medium/low only surface as the
     defects-minor advisory — decide each, never downgrade severity to pass;
   - undischarged risks → discharge (`discharged_by` the proving AC/test) or move the
     risk_state deliberately;
   - a rule that genuinely cannot be met this release → a `WVR-` waiver (rule +
     entity + justification + approver + expiry) on the operator's explicit words
     only; readiness reports it "waived", never silently.
3. **Expired waivers**: the readiness report lists `expired_waivers` — an expired
   waiver no longer satisfies its rule. For each: prefer RESOLVING the underlying
   item now; re-approval is a fresh operator-worded `WVR-` row (or a full-row
   upsert with a new `expires`) — never a silent carry-over into the release.
4. Advisory findings (the register-liveness prompt is the full playbook): review each and
   say what you decided — carrying one is legal, silence is not.
5. `human_required` gates: read each `GATE-` definition to the operator, get the
   explicit decision, upsert the gate row's `outcome` (Go/Hold/Redirect/Kill), and
   record it as a `progress_update` (event_type "gate-decision", subject_id the
   `GATE-` id).
6. `gate_run()` must pass; `export_html()` — the review surface ships with the release.
7. Release notes from `entity_query("progress-entry")` since the last release;
   `work_bind` the release tag/commit to the phase and headline entities.
8. `package_close()` and commit the package `data/` with the release.
