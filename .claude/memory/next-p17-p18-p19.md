---
name: next-p17-p18-p19
description: "★START HERE★ Next session = P17/P18/P19 planning. P16 merged+complete; P14 deferred indefinitely (DEC-028). PH-2 closed."
metadata: 
  node_type: memory
  type: project
  originSessionId: a04eded5-ff3f-4f18-814a-385f38361e91
---

# ★ Next session: plan P17 → P18 → P19 ★ (operator instruction, 2026-07-17)

**Ladder state:** P1–P13 + P15 + **P16 shipped**. **P14 is DEFERRED INDEFINITELY — do NOT start it** (DEC-028;
re-opens only on explicit operator instruction; its prompt in `follow-up-prompts.md` carries a ⛔ DO-NOT-START
banner). **With P14 out, PH-2 is closed** — the only remaining slices are the three cross-cutting closing ones.

- **P16 COMPLETE + MERGED** — PR #141 squash **`e15cfff`** (B2b+B3+B4), joining #124 (B1) + #126 (B2). See
  [[p16-hardening-b2b-b3-b4]].
- **PR #142 MERGED** (`6984e5a`) — registers the P14 deferral.
- ⚠ **The Keystone claim that used to sit here was FALSE.** It read: *"validator is green (RESULT: OK, 6/6) … never
  un-bold the AC ids in acceptance-audit.md"*. In fact `main` was **NOT READY on G-PROGRESS** from `e15cfff` until
  P17a fixed it — **because** the AC ids were bolded. **The ids MUST stay BARE.** Verified history + root cause:
  [[p17a-test-hygiene]].
- **P17 is now in flight** — P17a done; next = **P17b-0 AC triage**. See [[p17a-test-hygiene]].

## What each slice actually is (from the roadmap ladder)

- **P17 — Testing hardening.** First task per the roadmap: **harvest the "→ P17" deferrals from the progress log**
  (there are many; the S1–S7 coverage/E2E slices partially advanced this, ADR-0016). Known concrete items:
  - Several AC rows sit **`Partial` only because their residual is a *live* leg → P17** (e.g. **AC-025** voting
    immutability, **AC-027/028** decisions read/supersede). These are evidence gaps, not code gaps — a live
    HTTP/UI leg flips them to `Met`. That is likely the single biggest AC-verdict win available.
  - **D-19** — flaky `DecisionPage.test.tsx` (non-deterministic decision pick); reds CI on unrelated PRs. Cheap fix.
  - A stale `→ P14` note survives in a **P7b historical blockquote** in acceptance-audit.md
    ("vote/audit-timeline → P9/P14") — judge it with context; no AC depends on it.
- **P18 — Deployment.** production compose + overrides, migration-on-deploy, nightly backups + warm-standby
  restore, deployment/rollback runbook. **⚠ P18 now carries real SECURITY weight** — it is where P16's residuals
  actually become effective:
  - **C-CRYPTO Step B** (SQL server-authentication: `TrustServerCertificate=False` + a CA cert whose CN/SAN is
    `sqlserver`). SQL **transit is already encrypted** in-stack as of P16-B3 (`Encrypt=True`, self-signed).
  - **MinIO + Seq TLS** — both still plaintext; **neither auto-generates a cert** (unlike SQL Server), so both need
    operator-provided certs. The **Seq leg is 3 endpoints** (api OTLP + BOTH Serilog sinks; the worker has no OTLP var).
  - **TDE** — *not* edition-blocked in the bundled stack (Developer = Enterprise features); real blocker is
    **certificate key custody** (`down -v` destroys it ⇒ backups unrecoverable). **MinIO SSE** needs a KES server.
  - **The least-priv app login** that makes ADR-0031's audit `DENY` stop being inert (it runs as `sa` today).
  - **⚠ OQ-040 (SQL edition) is a SECURITY decision, not just capacity** — Express/Web support **neither TDE nor
    `Encryption for backups`` ⇒ its "start with Express" default forecloses **two P1 controls**. It is recorded
    `Blocking? = No`; the operator may want to re-classify it. Surfaced in `deployment.md` §3.4 +
    `security-controls-audit.md`.
- **P19 — Final audit & release readiness.** `[BLOCK]` checkpoint gates (`docs/execution/checkpoints.md`) +
  Definition of Done; final acceptance-audit report.

## Known-open items a planner should weigh

**D-19** (flaky FE test) · **D-22** (inline-style hygiene — explicitly **NOT** a CSP matter, that rationale is
disproven) · **D-20** (Confidentiality ABAC = a *feature*, not hardening) · **D-02** (Webex production live-confirm)
· **D-05** (Keystone import, unscheduled) · **D-11** (Tarseem, now unscheduled) · **D-15** design-update-owed ·
OQ-027 ZAP/DAST leg still **Deferred** · Trivy **image** scan report-only · ClamAV operator opt-in (OQ-026).

**Possible governance drift to check in P17/P19:** **DEC-022** names **`@dnd-kit`** as the settled frontend DnD
library, but P16 found it is **tree-shaken out of the production bundle** — `SortableList` is imported only by its
own test, and the real kanban/agenda drags are **native HTML5 DnD**. Either the decision or the code is stale.

## Working rules that bit this session (read before touching gates)

`dotnet format` **acmp.sln** — never a single `.csproj` (a scoped run passes while CI reds on `error CHARSET`);
`--nologo` is **not** a valid dotnet-format option; never pipe it to `tail` (swallows `$?`). Don't run a load test
concurrently with the e2e suite (audit applock ⇒ spurious failures). See [[ci-gates-run-locally-pre-push]],
[[e2e-local-run-nondestructive]] (**never `npm run e2e:up`**). **Cost discipline:** the P16 session ran ~$410 —
plan P17–P19 in a fresh context.
