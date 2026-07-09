# ADR-0025: Meeting-Recording Upload, Presigned Playback, and Deletion

- Status: Accepted
- Date: 2026-07-09
- Deciders: Architecture Committee (operator-requested; implemented in the P13 recording slice)

## Context and Problem Statement

FR-056 requires a Secretary to attach a Webex recording link **and/or upload a recording file** to a meeting. The Webex-webhook path (AC-070) stores a *reference* to a host-owned Webex recording — but on accounts without a cloud-recording license the webhook never fires (recordings stay local), so the operator needs a **manual upload** path, plus a way to **play** an uploaded recording in-app and to **delete** it. Recordings are large video (up to 2 GB), so how they are stored, served, and removed is an architecture decision worth recording rather than making silently.

ADR-0014 already established `IFileStore` over self-hosted MinIO for topic attachments (AC-049/050). This ADR extends that seam to meeting recordings and settles three points: object storage vs SQL BLOB, how a browser plays a MinIO-stored file, and delete semantics across the two recording sources.

## Decision Drivers

- Reuse the shipped, live-proven `IFileStore`/MinIO seam (ADR-0014, AC-050) — no new datastore (INV-002/003).
- Large video must not stream through the API request path or buffer in browser memory.
- On-prem, single-domain (nginx/ngrok) deployment — no separate object-storage host.
- Every recording change is authorized (Secretary/Chairman) and audited (INV-004/005).

## Considered Options

1. **Store the recording file in SQL Server (BLOB).** Rejected — multi-GB blobs bloat the DB, complicate backups, and don't stream.
2. **Store in MinIO via `IFileStore`; serve playback by proxy-streaming through the API.** No MinIO exposure, but a bearer-authenticated `<video src>` can't send the token, and streaming multi-GB through Kestrel is wasteful.
3. **Store in MinIO via `IFileStore`; serve playback via a short-lived pre-signed MinIO URL.** ADR-0014's stated delivery model; the browser fetches bytes directly from MinIO (Range/seek), no API proxy. Requires MinIO be browser-reachable.

## Decision Outcome

Chosen option: **3**. A meeting recording is stored in MinIO (`acmp-recordings` bucket) via `IFileStore`; the object key is **server-derived** (`{meetingKey}/{guid}{ext}`, extension from the validated content-type — never the client filename, so SigV4 presigned URLs never break on spaces/unicode). The meeting aggregate carries the reference (`RecordingObjectKey` + display metadata), not the file. Playback is a short-lived (10-min) **pre-signed GET URL** minted by `GET /api/meetings/{key}/recording/url` and fed to a `<video>` element. MinIO is made browser-reachable through nginx `location /acmp-recordings/` (Host header preserved so the SigV4 signature validates against the public host; a dedicated presign `IMinioClient` built with the public endpoint + an explicit region avoids a region-discovery round-trip); only pre-signed GETs succeed (unsigned → 403; no bucket listing). Upload is `POST /{key}/recording` (multipart, `Policies.MinutesCapture` = Secretary/Chairman), size/MIME validated against `MeetingRecordingOptions` (video/mp4|webm|quicktime, ≤ **2 GB**); nginx `client_max_body_size` + per-endpoint Kestrel/multipart limits are raised to match. Delete is `DELETE /{key}/recording` (same policy): it clears all recording fields and, for an uploaded recording, best-effort-deletes the MinIO object; a Webex reference is only cleared (we do not own the Webex asset — a re-delivered webhook could re-attach it). Every upload/delete emits an `AuditEvent` (`Meetings.RecordingUploaded` / `Meetings.RecordingRemoved`).

### Consequences

- Good: reuses the proven storage seam; proper video streaming with seeking; no multi-GB through the API or the browser heap; both recording sources share one delete UX.
- Trade-off: MinIO's `acmp-recordings` bucket is reachable at the public origin for pre-signed GETs (mitigated: signature-gated, short TTL, no listing); a 2 GB upload buffers to container temp disk (nginx + Kestrel spool) — acceptable for occasional on-prem use; the presigned URL is a bearer-less capability for its TTL. Transcript retrieval stays deferred (P19).

## Validation

- Unit + integration: `UploadRecording` / `GetRecordingUrl` / `DeleteRecording` handlers + validators, and `MeetingRecordingApiTests` (401/403/400/404/200/204; upload→detail; presign→url; delete→null). `MinioFileStore` stays coverage-excluded (ADR-0016 §1). BE + FE gates green.
- Live (`acmp.ngrok.dev`, `secretary-test`): a real upload returns 200 through nginx (was 413 at the 1 MB default); the pre-signed URL streams (HTTP 200 full / 206 range — Range/seek works); delete removes the MinIO object (bucket empty) and clears the reference.

## Links / Notes

- Extends ADR-0014 (`IFileStore`/MinIO). Delivers FR-056's upload leg; complements AC-070 (Webex-webhook reference path). New AC-073 (upload + playback) / AC-074 (delete). Closes deferred item D-02's manual-upload leg (transcript retrieval stays deferred → P19).
