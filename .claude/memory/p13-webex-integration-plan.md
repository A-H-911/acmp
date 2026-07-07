---
name: p13-webex-integration-plan
description: P13 Webex integration (Phase 2) — app/adapter surface + governance COMPLETE & CI-green on branch feat/p13-webex-integration; WS0 infra + coverage-run + live sandbox remain.
metadata:
  type: project
---

**P13 Webex integration (Phase 2)** — branch `feat/p13-webex-integration` (committed, not yet merged). Plan file: `~/.claude/plans/peaceful-knitting-nest.md`. Governed by ADR-0023 (accepted) + ADR-0024 (proposed, worker).

**DONE + tested + validator-green (WS1/WS2/WS3/WS3b/WS5-6):**
- **WS1** `INotificationSink` + `NotificationDispatcher` (single `INotificationChannel`, fans out to sinks) — InApp became a sink; **zero changes to the ~15 callers**.
- **WS2** new module `src/Modules/Integrations/Webex` (`Acmp.Modules.Integrations.Webex`, depends ONLY on `Acmp.Shared`, ArchUnit-enforced): typed `WebexApiClient`, `AdaptiveCardBuilder` (v1.3, ≤80KB, EN/AR), `WebexNotificationSink` (Enabled-gated, **committee-wide events only** — MeetingScheduled/AgendaPublished/MinutesPublished/DecisionIssued; per-recipient fan-out collapses to ONE space post), `WebexSendJob` (429→`Schedule(Retry-After)`), testable `IWebexJobScheduler` seam (generic; wraps Hangfire `IBackgroundJobClient`).
- **WS3** first **anonymous** endpoint `POST /api/webex/webhook` + `WebexSignatureFilter` (**HMAC-SHA1** default per Webex docs — NOT SHA256; SHA256/512 configurable; raw body + 5-min replay); `IMeetingWebexWriter` (ADR-0021 write seam) + `Meeting` Webex fields (`WebexMeetingId` indexed) + migration `Meetings_AddWebexFields`; recording processor (`GET /recordings/{id}`→attach, idempotent).
- **WS3b** OAuth (`/api/webex/oauth/start|callback`, Admin-gated, **single-use state cookie** — security-review fix); `WebexToken` store (AES-GCM, own `webex` schema, migration `Webex_TokenStore`, added to `MigrationRunner` conditionally) + transparent refresh; `ScheduleMeeting` enqueues create for online meetings via Shared `IWebexMeetingProvisioner` (Meetings registers a no-op default; adapter overrides when enabled). **Dropped speculative `IMeetingIntegration` seam (YAGNI).**

**Key decisions:** Hangfire-native retry (no outbox); **space-only delivery** (`CommitteeRecipient` has NO email → per-user DM/invitations/email-attendance deferred = **D-14**); adapter **not registered at all when `Webex:Enabled=false`** (clean AC-071, dodges Hangfire-off-in-Testing DI landmine); AC-067–072 added (criteria + audit, all **Met**).

**Verification:** 710 Application (+39 Webex unit) / 143 API (+3 webhook integration AC-069/071) / 188 Domain / 41 ArchUnit (Webex boundary) — green. Keystone validator all-7 PASS. `dotnet format` applied. 2 migrations.

**REMAINING (open):** **WS0** — `Acmp.Worker` daemon container + shared `AddAcmpModules` composition root + API→Hangfire-client-only split + docker-compose worker + configurable **ngrok** ingress (implements ADR-0024). Then: a coverage-collection CI run (`check-coverage.mjs` ≥95%, not run this session — cost); operator-run **live Webex sandbox** validation (ngrok URL + creds) → flip AC-067/072 "live pass" notes; minor naming-conventions module entry + status-report regen. See [[keystone-migration-gap-remediation]], [[ci-gates-run-locally-pre-push]], [[always-stage-claude-memory-in-commits]].
