# Memory Index — ACMP

> Compacted 2026-08-05. One line per memory; detail lives in the topic files. Read the linked file before acting on anything below.

## ★ Active work — PRODUCTION IS LIVE

- **★ START HERE: `handoff/RESUME-adr0038-frontend.md`** — the single entry point (2026-08-12). Earlier ACMP resume files are ⛔ superseded history; `RESUME-arabic-rename.md` still holds the SSM/Keycloak gotchas and the operator security actions.
- **Prod** https://acmp.anas7ammo.dev · `i-04d9717feea79204b` · **always-on**, live on `e403e18`. **UAT** `i-07ac28ac2fedab921` · **stopped when idle**. They differ ON PURPOSE — don't harmonise them. 26 real members seeded; budget $100/mo.
- ⚠ **The deployable sha is NOT HEAD** — `ci.yml` `paths-ignore` skips `*.md`/`docs/`/`.claude/`/`tamheed-package/`, so governance commits publish no images. Deploy the newest sha that has ECR images. **Deploy as `acmp-admin`, never root** (root bypasses the budget IAM-deny brake, AC-085 leg 5; `[default]` IS root and its session expires).
- ✅ **ADR-0038 is feature-complete and wired** — #234 backend · #235 invite UI · #236 role UI · #237 deploy plumbing · #238 reconcile guard. `AC-088`/`AC-089` **Met**; `FR-156`/`157`/`158` done.
- ✅ **The minimum Keycloak grant is ONE role: `manage-users`** — proven on UAT in two runs (`{}` → 403 = necessary; `{manage-users}` → 8/8 = sufficient ⇒ minimal). ⚠ **My candidate (`+ view-realm`) was WIDER than the truth**, and no gate would have said so. Re-runnable: `scripts/probe-keycloak-grant.mjs`. CI now proves the client exists every run (`realm-management grant is exactly: manage-users`).
- ⚠ **`realm-export.json` reaches FRESH STACKS ONLY** — Keycloak never re-imports an existing realm, so prod/UAT silently lack anything declared there. `reconcile.sh` is the only seam. **Third occurrence.** See [A static file cannot configure a live realm](a-static-file-cannot-configure-a-live-realm.md).
- ⚠ **`compose up --wait` does NOT wait for a one-shot** (measured) — so `up.sh`, i.e. dev **and on-prem prod**, cannot catch a failed realm reconcile. The cloud deploy can (`08-bootstrap-box.sh:271`).
- ⚠ **Open decisions for the operator:** `OQ-069` (`FR-156`/`157` say "Administrator **or Secretary**" but `/admin` is Administrator-only — **do not just widen the route**, it exposes templates/health/jobs/notifications, SoD-5) · `OQ-071` (automated probe-based grant test still owed) · `DEF-050` (Webex secrets travel via compose `environment:` while their files are mounted by nothing — ADR-0032 violation) · `OQ-062` is stricter in code than in the decision (a **permanent** UAT Webex ban vs "off **until** a UAT space exists", so the exit condition can never be met).
- **★ NEXT:** (1) deploy with `KEYCLOAK_ADMIN_ENABLED=true` → evidence `AC-090` behaviourally; everything it needs is in place. (2) `FR-159` guest invite (`AC-092` Pending). (3) `OQ-071`'s automated test. (4) `up.sh`/on-prem reconcile guard.
- ⚠ **`Streams.NameAr` on prod is NOT done** (in scope for Day 3). Real table is **`membership.streams`.`name_ar`** — the C# names don't exist in SQL and every module owns a schema. Exact query in the resume.
- [★ Read the implementation before calling it a defect](read-before-calling-it-a-defect.md) — the most expensive pattern across these sessions, and it recurs: it has since made a defect **smaller** too (`DEF-051`'s cloud half was always guarded). **None of it is ever caught by a gate.**
- ⚠ **An ADR citation in a TEST NAME is load-bearing, and no gate reads it** (`SC-004`). `ADR-0038` silently contradicted `ADR-0015` §Q3; a `describe` string caught it. Supersede **narrowly**. The same shape hit `OQ-042`'s resolution.
- ⚠ **Check whether it's already built** — three times in one session it already was. See [The feature is often already half-built](check-before-building.md).
- ⚠ **Write the Tamheed package ONLY from `main`** — `tamheed-package/data` is git-tracked, so a feature branch fragments the record. `defect.fixed_by` is a **FOREIGN KEY**; put PR refs in `custom_attributes`.
- ⚠ **`gh pr merge` can stick at `mergeable: UNKNOWN`** — usually transient, but #221 stayed stuck through nine attempts. Fix: recreate the branch by cherry-picking onto current `main`.
- **`AC-085` leg 1 is observable** — SQS `acmp-budget-observer` + `deploy/scripts/check-budget-notification.sh` reads the **body**, not a count (a count can't discriminate on a shared topic). When spend crosses **$2.30**, re-run and `audit_record` the body.
- ✅ **Arabic rename done** (`DEC-032`) — **definiteness is preserved**; the approved plan would have shipped ungrammatical Arabic. See [Arabic rename is a grammar rule](arabic-rename-grammar-not-substitution.md).
- ✅ **Rename live-data gap closed** — 351 nvarchar columns / 83 tables across both DBs hold neither half of the term family. See [A clean scan must prove it had a subject](scan-must-prove-it-had-a-subject.md).
- ✅ **Day 3 + the deploy chain done** — `cloud-operations.md` (not `operations.md`: two files named "operations" for different topologies is how the `promote.sh` defect happened; guard = `check-runbook-drift.mjs`). Full regression green; `DEF-045` was all harness. See [Guard the property, not the value](guard-the-property-not-the-value.md) · [The suite assumed a fresh database](e2e-assumes-a-fresh-database.md).
- ⚠ **The Playwright E2E suite is NOT UAT-only** — `e2e.yml` runs the full 7-service stack with real Keycloak on every PR. UAT adds *deployed-topology* validation, not application logic.

## PH-5 history (UAT)

- [PH-5 / SL-025 — UAT is live and LOGIN WORKS](ph5-sl025-uat-live.md) — earlier UAT milestone. Four browser logins prove it; DEF-022/023/024/025 all Fixed. **As of 2026-08-09: AC-075 and AC-076 are both Met; PH-5 is 11 Met / 1 Partial — only AC-085 leg 1 (a budget notification observed to ARRIVE) is open, and it is an observation wait, not work.** All 33 defects Fixed except DEF-012 (package-data). ⚠ **Instance is `i-07ac28ac2fedab921`** — the old `i-05085d458d886dc08` no longer exists; the box was replaced and the budget stop-action re-pointed itself, which is how AC-085 leg 4 got evidenced. Always read the live instance id from `describe-instances`, never from notes.
- [PH-5 AWS deployment](ph5-aws-deployment.md) — earlier history (P20–P24). P20–P24 done; cloud stack now **boot-proven end-to-end from the real ECR images**. Four silent defects found & fixed (DEF-018 Express `AUTO_CLOSE` kills FTS · DEF-019 unpublishable web tag · DEF-020 Seq placeholder · a guard that missed its own case). **Remaining is operator-gated:** AC-078 needs the app key, F=switch off root, G=P25 spend.
- [⚠ Commit package writes before git ops](commit-package-writes-before-git-ops.md) — `tamheed-package/data` is git-TRACKED; `git reset --hard` destroys uncommitted package writes. (Supersedes the retracted "Tamheed loses writes" note — that was my own reset.)
- [Tamheed stale .lock + PID reuse](tamheed-stale-lock-pid-reuse.md) — `package_open` fails constantly; the lock holds a **bare PID** and "is it alive?" **lies** (PID reuse). Check PID **+ process identity + StartTime ≤ lock mtime** before removing.
- [Tamheed data repair](tamheed-data-repair.md) — the v2.3 migration passed 7/7 gates while damaging register data at **column** level (every gate is row-level). Re-populated on parser 2.4.0+; `DW-015 = D-15` by identity now; only DEF-012 open. Scratch-diff runs via the bundled `scripts/scratch_diff.py`.
- [Tamheed migration history](tamheed-migration-reverted.md) — cycles 1–2 fully reverted; 3rd run on 2.3.0 is the live system of record. Never hand-edit `tamheed-package/`; never re-migrate without an explicit operator order.

## Standing rules & gotchas (read before editing)

- [★ Controls must DETECT **and** TELL](controls-must-detect-and-tell.md) — **five** instances here (DEF-023/030/031/032, OQ-068). The "tell" half is always asserted in a comment rather than tested. Check `AlarmActions`, check service-principal topic policies, force the transition.
- [★ Verify mechanically, not carefully](verify-mechanically-not-carefully.md) — `entity_upsert` replaces FULL rows; the JSONL flushes on EVERY write, so git HEAD is a live baseline. Use `scratchpad/pkgdiff.py`. Also `PYTHONIOENCODING=utf-8` (cp1252 eats `—`/`→`) and never `sed -i` a CRLF file.
- [Install the schedule, not just the daemon](install-the-schedule-not-just-the-daemon.md) — the bootstrap installed `cronie` but left the crontab a manual runbook step, so a rebuilt box had an EMPTY schedule. Fixed in #206; verify by `diff`, not by eye.
- [⚠ Baselines are numbers, not properties](baselines-as-numbers-not-properties.md) — "non-zero against a zero baseline" got falsified by my own later fixes and turned a real check into one that passes for the wrong reason. No gate can see it. **Recurred 2026-08-09:** AV-117's replacement ("above six") passed falsely within 70 minutes — on a shared topic, NO count-based test can discriminate (AV-118).
- [⚠ Absence needs an untruncated search](absence-claims-need-untruncated-search.md) — `tail`/`head`/`-m` in the pipeline means you may NOT claim "it isn't there". Cost a false lead in the permanent record; second occurrence of the shape.
- [⚠ Immutable history → cleanup is asymmetric](immutable-history-cleanup-asymmetry.md) — DEF-029: deleting a Keycloak user ORPHANS its member rows forever. **Disable, never delete.**
- [Write the handoff LAST](write-the-handoff-last.md) — a handoff written before the session's final verdict ships stale; stamp superseded ones with a ⛔ banner immediately.
- [Localhost CI hides load races](localhost-ci-hides-load-races.md) — DEF-028: async-data races only appear against a real remote host, and **derived** error state erases its own evidence before the screenshot is taken.
- [Git push hang → `gh auth setup-git`](git-push-hang-fix.md) — GCM blocks forever; one command fixes it for good. Don't ask the operator to push.
- [Run CI gates locally pre-push](ci-gates-run-locally-pre-push.md) — `dotnet test` ≠ CI green. Also `dotnet format --verify-no-changes` (**new `.cs` files need a UTF-8 BOM**) + `node scripts/check-coverage.mjs .` (≥95% per-file).
- [Always stage .claude/memory in commits](always-stage-claude-memory-in-commits.md) — `.claude/memory/` **is** this memory directory and is repo-tracked; every commit includes it.
- [Coverage & E2E mandate](coverage-and-e2e-mandate.md) — standing goal ≥95% coverage FE+BE + adversarial E2E every flow; GO-gated slices.
- [E2E local run (non-destructive)](e2e-local-run-nondestructive.md) — Playwright needs a fresh `.env.example` stack; **`-p acmpe2e` ONLY**, never `npm run e2e:up` (destructive). Cannot run against the dev stack (ngrok issuer).
- [Dev-stack rebuild pitfall](dev-stack-rebuild-pitfall.md) — **never `up --build`** the long-lived dev stack (SQL volume/password mismatch → unhealthy).
- [Exact design fidelity + visual loop](exact-design-fidelity-visual-loop.md) — "from `<file>.dc.html`" means pixel-exact, verified by screenshot compare — not nearest-token.
- [Design: breadcrumb spacing](breadcrumb-spacing-rule.md) — 12px gap below breadcrumb, owned globally on `.breadcrumb`; don't re-add per page.
- [i18n parity ≠ completeness](i18n-parity-not-completeness.md) — `check-i18n` only checks EN/AR key parity; add every enum value by hand or the UI renders raw English.
- [Web visual-verify cache busting](web-visual-verify-cache-busting.md) — `:8088` can serve a stale bundle; force-recreate, clear cache, `?cb=`, confirm the JS hash.
- [User prefers simple English](user-prefers-simple-english.md) — non-native speaker; short plain sentences with a clear recommendation.
- [Phase prompt Standard Footer](phase-prompt-standard-footer.md) — every pasted phase prompt carries the DoD footer.
- ⚠ **AC id cells in markdown tables must stay BARE** (`| AC-001 |`, never bolded) — bolding breaks the Keystone G-PROGRESS gate.

## Completed ladder P1–P19 (reference only — do not re-open)

- [P19 release readiness + D-23](p19-release-readiness.md) — ladder P1–P19 COMPLETE; D-23 follow-up fully landed (#149/#150). Rollup 62 Met / 11 Partial / 1 Pending at that point.
- [P18 deployment](p18-deployment.md) — Docker secrets everywhere (ADR-0032), prod overlay, least-priv `acmp_svc` (ADR-0031), backup/restore/promote scripts + runbook (ADR-0033).
- [P17b decision-issuance UI](p17a-test-hygiene.md) — record→issue chairman-gated dialog; **all identity = `member.keycloakUserId` (KC sub)**, never publicId.
- [P17/P18/P19 slice notes](next-p17-p18-p19.md) — what each closing slice was.
- [P16 hardening B2b/B3/B4](p16-hardening-b2b-b3-b4.md) — CSP refactor proven unnecessary; Testcontainers 4.13; nginx conf.d tmpfs needs `uid=101`.
- [P16b CI security gates](p16b-ci-security-gates.md) — dep-CVE gate + Gitleaks/Semgrep/Trivy all gating.
- [P16a audit & vote crypto](p16a-audit-vote-crypto.md) — per-ballot chaining + nightly verify (ADR-0030/0031).
- [P15 Research & Knowledge](p15-research-knowledge-plan.md) · [P15 audit remediation](p15-audit-remediation.md) · [P15f/g search](p15f-search-progress.md) — global search on SQL FTS; `Dockerfile.sqlserver` carries FTS.
- [P13 Webex](p13-webex-integration-plan.md) · [P13 audit remediation](p13-audit-remediation.md) · [P13 recording upload](p13-recording-upload.md) — Phase-2 adapter, worker split, presigned playback. ⚠ `rm -rf coverage-out TestResults` before check-coverage.
- [P12 Dashboards & Reports](p12-dashboards-reports-plan.md) · [P11 ADRs & Invariants](p11-adrs-invariants-plan.md) · [P10 Risks/Deps/Traceability](p10-risks-deps-traceability-plan.md) — role-exclusive dashboards; Decision→ADR promotion; impact graph.
- [P9 Voting](p9-voting-plan.md) · [P8 Actions](p8-actions-plan.md) · [P7 Minutes & Decisions](p7-minutes-decisions-plan.md) — vote aggregate in Decisions; AC-029 hard gate; version-preserving supersede.
- [P6a meeting IA](p6a-meeting-ia-plan.md) · [P6b notifications IA](p6b-notifications-ia-plan.md) — both shipped.
- [Audit slice (AC-017)](audit-slice-literal-ac017.md) — hash-chained AuditEvent same-tx (ADR-0026/0027).
- [Topic Prepare UI (D-15)](topic-prepare-ui-gap-d15.md) — "Mark prepared" + badge + Secretary notify.
- [Webex coverage-gate exclusion](webex-coverage-gate-async-exclusion.md) — RESOLVED; `CompilerGeneratedAttribute` dropped async coverage.
- [Keystone package migration](keystone-package-migration.md) · [Keystone gap remediation](keystone-migration-gap-remediation.md) — superseded by Tamheed; `docs/` is a frozen archive.
