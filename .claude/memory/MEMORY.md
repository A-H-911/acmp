# Memory Index — ACMP

> Compacted 2026-08-05. One line per memory; detail lives in the topic files. Read the linked file before acting on anything below.

## ★ Active work — PH-5 AWS cloud deployment

- [★ PH-5 / SL-025 — UAT is live and LOGIN WORKS](ph5-sl025-uat-live.md) — **START HERE.** Four browser logins prove it; DEF-022/023/024/025 all Fixed. Only AC-075 (one clean rebuild) + AC-076 (SL-027) remain. Instance `i-05085d458d886dc08`, stopped.
- [PH-5 AWS deployment](ph5-aws-deployment.md) — earlier history (P20–P24). P20–P24 done; cloud stack now **boot-proven end-to-end from the real ECR images**. Four silent defects found & fixed (DEF-018 Express `AUTO_CLOSE` kills FTS · DEF-019 unpublishable web tag · DEF-020 Seq placeholder · a guard that missed its own case). **Remaining is operator-gated:** AC-078 needs the app key, F=switch off root, G=P25 spend.
- [Tamheed stale .lock + PID reuse](tamheed-stale-lock-pid-reuse.md) — `package_open` fails constantly; the lock holds a **bare PID** and "is it alive?" **lies** (PID reuse). Check PID **+ process identity + StartTime ≤ lock mtime** before removing.
- [Tamheed data repair](tamheed-data-repair.md) — the v2.3 migration passed 7/7 gates while damaging register data at **column** level (every gate is row-level). Re-populated on parser 2.4.0+; `DW-015 = D-15` by identity now; only DEF-012 open. Scratch-diff runs via the bundled `scripts/scratch_diff.py`.
- [Tamheed migration history](tamheed-migration-reverted.md) — cycles 1–2 fully reverted; 3rd run on 2.3.0 is the live system of record. Never hand-edit `tamheed-package/`; never re-migrate without an explicit operator order.

## Standing rules & gotchas (read before editing)

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
