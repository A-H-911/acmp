# ADR-0014: Background Jobs (Hangfire on ACMP SQL), Observability (Serilog + OpenTelemetry + Seq), and Object Storage (MinIO)

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

ACMP requires background processing (reminders, escalations, notification dispatch, diagram rendering, integrity checks), structured observability (logs, traces, metrics), and object storage (meeting recordings, transcripts, presentation files, diagram artifacts). The organization has shared infrastructure for all three (Hangfire, ELK+Seq, file storage) — but ACMP must not use it (CON-001). App-owned solutions must be chosen for each concern.

## Decision Drivers

- CON-001: no org Hangfire, no org ELK/Seq, no org notification/file infrastructure.
- Hangfire is the natural .NET background job library: SQL Server-backed (no broker needed), built-in dashboard, retry/failure handling, job history, and support for recurring jobs. Since ACMP already has SQL Server (ADR-0003), a Hangfire schema on the ACMP SQL instance adds zero operational overhead.
- Serilog is the standard structured-logging library for .NET; OpenTelemetry (via `OpenTelemetry.Extensions.Hosting`) provides traces and metrics with vendor-neutral export; Seq is an excellent self-hosted log/trace backend with a generous free tier and a Docker image — it is the chosen UI for ACMP's own logs.
- MinIO is an Apache-2.0-licensed, S3-compatible object store with a Docker image. It is the standard self-hosted alternative to S3/Azure Blob and integrates with any S3 SDK. An `IFileStore` abstraction decouples the application from MinIO so it could be replaced with Azure Blob or S3 later.
- The SQL-backed outbox (a table in ACMP's SQL schema, processed by Hangfire) guarantees at-least-once delivery of notifications and render jobs even if the app restarts mid-operation.

## Considered Options

1. **App-owned Hangfire on ACMP SQL + self-hosted Seq (Serilog + OpenTelemetry) + self-hosted MinIO** — zero external dependencies; all bundled in Docker Compose.
2. **RabbitMQ/Kafka for background jobs** — message brokers add significant operational complexity and are over-engineered for ≤20 users; no requirement for competing consumers or cross-service messaging in a modular monolith; rejected.
3. **Quartz.NET** — viable alternative to Hangfire but lacks the dashboard and SQL persistence integration; Hangfire is preferred for its operational visibility.
4. **Org's ELK** — explicitly excluded by CON-001.
5. **Azure Blob / AWS S3** — cloud storage; violates on-prem constraint; not available for a gov on-prem deployment.
6. **Local filesystem storage** — no abstraction; no distributed-safe access if multiple app instances run; not S3-compatible; rejected.

## Decision Outcome

Chosen option: "App-owned Hangfire on ACMP SQL + Serilog + OpenTelemetry + self-hosted Seq + self-hosted MinIO", because each component directly fulfils a specific runtime need with zero additional datastore dependencies (Hangfire uses ACMP SQL; Seq has its own storage), is bundled as a Docker Compose service (self-contained per ADR-0013), and is industry-standard enough for the team to operate and troubleshoot without specialized expertise.

### Consequences

- Good: Hangfire dashboard provides job visibility and retry without custom monitoring code; Seq provides a queryable log/trace UI for debugging production issues; MinIO provides S3-compatible file storage with access control; all three are bundled in Docker Compose and managed by the team without shared-infra negotiation.
- Bad / trade-off: Hangfire runs in-process with the ACMP app — a long-running job can compete with request processing for thread pool threads (mitigate: configure a dedicated Hangfire worker process or use a separate Hangfire server with a limited worker thread count). Seq's self-hosted free tier has log retention limits [unverified: confirm Seq free tier storage cap vs. expected log volume]. MinIO requires configuring access policies, bucket lifecycle, and pre-signed URL TTLs — more setup than `File.WriteAllBytes`.

## Validation

- Hangfire: verify that job queues (reminders, notifications, render, integrity-check) are visible in the Hangfire dashboard; simulate a job failure and confirm retry with exponential backoff; confirm the `HangfireSchema` is isolated from ACMP domain tables.
- Observability: structured log events appear in Seq with correct level, timestamp, correlation ID (`TraceId`), and module tag; distributed trace spans (HTTP → MediatR handler → EF Core query) appear as connected traces in Seq.
- MinIO: upload a file via `IFileStore`, receive a pre-signed time-limited URL, retrieve the file; verify the URL expires after TTL; verify file is not accessible via a direct unauthenticated URL.
- Outbox: start a notification job, kill the app process mid-job, restart — verify the job resumes and the notification is delivered exactly once (Hangfire's idempotency key or deduplication).

## Links / Notes

- Hangfire: https://www.hangfire.io — SQL Server storage; dashboard at `/hangfire`; in-process server + optional separate worker.
- Serilog: https://serilog.net — structured logging; sinks: Seq (primary), console (for Docker log driver).
- OpenTelemetry .NET: https://opentelemetry.io/docs/languages/net/ — `ActivitySource` for traces; `Meter` for metrics; OTLP exporter to Seq (Seq 2024+ accepts OTLP [unverified: confirm Seq OTLP ingest]).
- MinIO: https://min.io — Docker image `minio/minio`; S3 SDK (`AWSSDK.S3` or `Minio` .NET client) behind `IFileStore`.
- SQL-backed outbox: a `OutboxMessages` table in ACMP SQL; Hangfire polls and dispatches; guarantees durability for notification + render jobs.
- Background job types: meeting reminders (T-24h, T-1h), action due-date alerts, vote-open/close notifications, diagram render requests, nightly audit hash-chain integrity check, nightly backup trigger.
- Related: ADR-0002 (Hangfire configured in Platform module), ADR-0003 (Hangfire uses ACMP SQL), ADR-0005 (notification outbox processed by Hangfire), ADR-0006 (Tarseem render invoked from Hangfire), ADR-0009 (integrity check job), ADR-0013 (all three bundled in Docker Compose).
