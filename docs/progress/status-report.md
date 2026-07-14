---
status: Approved
version: 1.7.0
updated: 2026-07-13
owner: Claude Code execution agent
generation: derived
---

# Status Report — ACMP

Derived snapshot of where the build is. Regenerated each update cycle from the [progress log](progress-log.md), the [acceptance audit](../validation/acceptance-audit.md), and the registers. This is the "where you are now" pointer referenced by [AGENTS.md](../../AGENTS.md).

## Current position

- **Phase:** PH-1 (MVP Governance) **complete**; PH-2 (Governance Expansion) **substantially delivered**. The canonical build-slice ladder (`P1…P19`) is in [planning/roadmap.md](../planning/roadmap.md) §Build-slice ladder.
- **Latest slice:** **P15c (Research → execution-topic convert flows + FR-113/114/115 traceability) COMPLETE — P15c-1 backend MERGED (PR #113 `bab2adc`); P15c-2 frontend BUILT + all gates green + live pixel-VR PASS (branch `feat/p15c2-research-convert-frontend`, awaiting commit GO).** P15c-2 is FE-only: `ConvertToTopicDialog` (pre-filled topic form → `POST /api/topics/from-research` → navigate to the new `TOP-`), header **Convert** button (Completed-only, INV-014 flag) + per-recommendation Convert (Accepted), the shared `TraceabilityPanel` mounted on the mission in a two-column grid (FR-114), a navigable `TOP-` header xref with a plain-indicator fallback for older missions (FR-115), an **edge-driven** "Converted → `TOP-`" chip that self-heals a failed best-effort status-mark, and `ResearchMission` added to `GRAPH_FOCUS_TYPES` + a cold-resolver so the impact graph opens on a mission incl. refresh (FR-113). `TokenInput` extracted to `components/ui`. 939 FE tests / cov 99.64% / i18n 1640 / oxlint / build green; live VR EN-light + AR-dark RTL pixel-faithful to `ACMP Research & Knowledge.dc.html` (`e2e/p15c-convert-vr.spec.ts`). **FR-113/114/115 → Met.** Prior in P15: **P15a backend + P15b UI MERGED to `main` (PR #107 `7d82aba`, PR #108 `c06dc85`, 2026-07-13); P15b live pixel-VR MERGED (PR #110 `abb84a9`).** New **Research** module (schema `research`): `ResearchMission` (RMS-) + `Finding` (FND-) + `Recommendation` (REC-) with manual CRUD/lifecycle, INV-005 enriched audit, `Research.Manage` RBAC, migration `Research_Init` (P15a); the `/research` register + mission detail + create/manage dialogs — **role + status gated** (Chair/Sec; mission must be `Active` for child mutations), built to `ACMP Research & Knowledge.dc.html` with design-only sections omitted (guardrail #14) (P15b). Keystone import deferred (FR-112 → D-05); traceability graph edges (FR-113/115) + convert (W16) deferred → **P15c**. Also merged **PR #106 (`186f24c`)** — governance-register refresh + Keystone-import deferral + **OQ-050** (non-topic-scoped governance artifacts are A-only) + **D-17** (owned-child audit enrichment deferred). P15b live pixel-VR pending (post-merge). Prior slice: **Audit module (literal AC-017) — MERGED to `main` (PR #105, squash `f32ca31`, 2026-07-12).** Closes the last Pending core-governance ACs **AC-017/018/019/020 → Met** (INV-005 realized end-to-end): enriched hash-versioned `AuditEvent` + before/after capture in the **same transaction** as the module write (NFR-042, ADR-0026); 74 governed emit sites → `EmitEnrichedAsync`; `GET /api/audit` register + `GET /api/audit/verify` chain check gated to {Auditor, Chairman, Secretary} — Administrator excluded on SoD-5 (ADR-0027); `/audit` UI to `ACMP Lists & Registers.dc.html` (INV-014). All four CI checks green incl. the full-stack **e2e** (first proof all 11 contexts boot on the shared `DbConnection` — validates the `PersistSecurityInfo` boot fix). Deferred to P16 (logged): DB-permission immutability + nightly Hangfire verify job. Prior slice: **P13 adversarial-audit remediation — MERGED (two GO-gated slices).** Slice 1 (PR #101, squash `3a973c4`): `MinioFileStore` real coverage via a Testcontainers-MinIO integration test (the FR-056 recording store behind AC-073/074 was falsely coverage-excluded), honest AC rows, recording-tab UI/i18n/a11y minors. Slice 2 (PR #102, squash `308611d`): the **dormant** Webex hardening — fail-closed `TokenEncryptionKey` (`ValidateOnStart`), graceful refresh degradation (AC-072 `null`-on-revoke), refresh audit (`Webex.OAuthTokenRefreshed`, INV-005), webhook replay guard, per-job `Retry-After`, and a real in-memory-Hangfire dead-letter test that restores **AC-068/069 → genuinely-tested `Met`**. Live Webex validation is owed to the operator (sandbox is operator-only). Prior merges this window: **D-15** topic *Prepare*-UI (PR #100, squash `b6bb185`; OQ-049) and **P13** Webex adapter WS0–WS3b + meeting-recording (PR #99, squash `7e3394e`; AC-067–074 `Met`). `main` is green and deployable.
- **Next:** continue **P15** — **P15d/e** Knowledge (wiki + templates) → **P15f/g** global search (**OQ-034 Arabic word-breaker + INV-002 go live**) → **P15h** template wiring; plus **P14** Tarseem/Diagrams and hardening **P16–P19**. Residuals: FR-115 source-edge **backfill for pre-P15c missions** + the `UpdateMissionDraft` source edge (fast-follow, deferred from P15c-1); **D-19** flaky `DecisionPage.test`; operator token rotation (Webex + ngrok in `deploy/.env`); AC-070 production live-confirm (D-02); D-15 design-update-owed.

## Delivered (by module / slice)

| Area | State | Evidence |
|---|---|---|
| Platform · Membership · Topics · Meetings | Delivered | P1–P6 slices; [acceptance audit](../validation/acceptance-audit.md) |
| Minutes & Decisions (P7) · Actions (P8) | Delivered | P7a–d, P8a–d merged |
| Voting (P9) | Delivered | P9a backend + P9b UI merged |
| Risks · Dependencies · Traceability + impact graph (P10) | Delivered | P10a–g merged; FR-095/096 |
| Governance — ADRs & Invariants (P11) + Decision→ADR promotion | Delivered | P11a–e merged; FR-068/099–109 |
| Dashboards & Reports (P12) | Delivered | P12 PR1–PR3 + audit remediation merged; AC-064/065/066 Met |
| Webex integration + meeting recording (P13) + audit remediation | Delivered — AC-067–074 Met | WS0–WS3b (ADR-0023/0024); recording upload/playback/delete (ADR-0025); audit slices #101/#102 (Testcontainers-MinIO coverage + Webex hardening, AC-068/069 tested-Met); AC-070 live-attach = production residual (env caveat) |

## Gate snapshot (Keystone package)

The package is under Keystone v1.0.0 governance. Critical gates are confirmed by `python <keystone>/scripts/validate_package.py docs` (see [execution-readiness report](../handoff/execution-readiness-report.md) for the authoritative result). Coverage: the [acceptance audit](../validation/acceptance-audit.md) tracks every `AC-001…074` → verdict (G-PROGRESS); traceability matrix links MVP FR/NFR → decision/WBS/test/AC (G-TRACE).

**AC verdicts (regenerated 2026-07-12 from the acceptance-audit rollup):** **74 ACs · 34 Met · 37 Partial · 3 Pending · 0 Not-met.** The 3 Pending: AC-004 (Keycloak idle-timeout, needs a live realm session policy) and AC-060/061 (global + Arabic search → the P15 Search module). Most `Partial`s are governance-feature ACs whose domain/handler/HTTP legs are proven but whose dedicated live real-stack VR leg is noted "→ P17"; `main` is green at `f32ca31` (all four CI checks incl. e2e). Verdicts are the ledger's per-AC test refs, not a fresh full-suite re-run.

## Open items

- Accepted-open questions: [decisions/open-question-register.md](../decisions/open-question-register.md) (the `Deferred` rows carry applied defaults).
- Deferred work: [execution/deferred-work-register.md](../execution/deferred-work-register.md) (`D-01…D-13`; Webex/AI/Gantt/Tarseem/email/per-ballot-chaining/etc.).
- Design-update-owed items from the P11/P12 audits are logged in the progress log (reference-design divergences blessed by the operator; the design is to be updated to match).
