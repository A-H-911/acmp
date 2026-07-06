---
status: Approved
version: 1.1.0
updated: 2026-07-06
owner: lead-secretary
---

# Deferred-Work Register — ACMP

The durable index of work that is **known-not-done**: intentionally deferred features, accepted tech-debt, and manual-for-now processes. Distinct from the forward [backlog](backlog.md) (what's next) and the [out-of-scope exclusions](#out-of-scope-index-tracked-not-planned) (rejected for v1/v2). Sources: the former `docs/domain/scope-and-out-of-scope.md` Deferred/Manual-for-Now (`D-##`), the mid-build change record `CHANGE-001`, and the test-hardening deferrals recorded inline in the [progress log](../progress/progress-log.md).

## Conventions

`#` is a local index (`D-##`, no governed prefix). `Type` ∈ Feature-deferral · Manual-for-now · Tech-debt · Ops-config. `Status` ∈ Open · In progress · Done · Won't-do. A deferred item is activated when its Trigger is met; activation creates a backlog item and (if it changes a settled decision) an ADR.

## Deferred / manual-for-now

| # | Deferred item | Type | Phase-1 handling | Target phase | Trigger to activate | Status |
|---|---|---|---|---|---|---|
| D-01 | Timeline / Gantt-lite backlog view | Feature-deferral | Calendar/list views | Phase 2 | Phase 2 start | Open |
| D-02 | Webex recording/transcript retrieval + storage | Manual-for-now | Secretary pastes link / uploads file | Phase 2 | Webex Assistant enabled org-wide + API access confirmed (OQ in [open-question register](../decisions/open-question-register.md)) | Open |
| D-03 | AI candidate extraction from transcripts | Feature-deferral | Secretary reviews transcript, creates actions/decisions manually | Phase 3 | Transcripts machine-readable (depends on D-02) | Open |
| D-04 | Conflict-of-interest tracking per vote | Manual-for-now | Honor system, noted in MoM | Phase 2 | Phase 2 | Open |
| D-05 | Keystone research-mission import | Manual-for-now | Secretary creates Research Mission with Keystone package ref | Phase 2 | Keystone package schema stable + import tool built | Open |
| D-06 | Invariant exception-request workflow | Feature-deferral | Manual process outside system | Phase 3 | Demand demonstrated post-Phase 2 | Open |
| D-07 | Traceability-matrix export (CSV/Excel) | Feature-deferral | Auditor exports audit log + assembles | Phase 3 | Phase 3 | Open |
| D-08 | Notification user preferences (per-user per-event) | Feature-deferral | All-or-nothing per event type | Phase 2 | Phase 2 | Open |
| D-09 | File-attachment storage sizing / ops config | Ops-config | `IFileStore` backed by MinIO; volume sizing at deploy | Ops | Deploy-time configuration | Open |
| D-10 | Bulk topic operations (defer multiple, reassign owner) | Feature-deferral | One at a time | Phase 3 | Phase 3 | Open |
| D-11 | Tarseem diagram render sidecar (behind `IDiagramRenderer`) + Diagrams surface | Feature-deferral | Diagrams authored externally and attached as files | Phase 2 | P14 kickoff (Tarseem container build validated) | Open |
| D-12 | Email notification channel via `INotificationChannel` | Feature-deferral | In-app notification center only (no email in v1) | Phase 3 | SMTP relay available (see [dependency register](../requirements/dependency-register.md)) | Open |
| D-13 | Per-ballot crypto hash chaining (vote *state-change* chain shipped in the P9-review slice, PR #76; individual ballots covered transitively, not individually chained) | Tech-debt | Hash-chained vote state-change audit rows | Phase 2 | P16 security-hardening slice | Open |

## Change records

| # | Item | Type | Note | Status |
|---|---|---|---|---|
| CHG-001 | Keycloak ownership: federate-to-org-IdP → self-hosted bundled Keycloak (ACMP-owned realm) | Settled-decision change | Recorded as `CHANGE-001` and ratified by ADR-0015; the assumption ASM-001 was resolved FALSE and the self-host path adopted | Done |

## Out-of-scope index (tracked, not planned)

Explicit v1/v2 exclusions (former `docs/domain/scope-and-out-of-scope.md` §Out-of-Scope, `OOS-01…OOS-15`) live in [domain/scope-out-of-scope](../domain/) and are summarized in the [charter](../00-charter.md) §Out of scope. They are **rejected**, not deferred — reversing one requires an explicit future-phase decision (a new `DEC-`/ADR). Highlights: generic PM (OOS-01), in-app diagram engine (OOS-02), BPM engine (OOS-08), Kubernetes/broker (OOS-13), custom search cluster (OOS-14), WebSocket infra (OOS-15).

## Test-hardening deferrals

The S1–S7 coverage program and per-phase audits deferred a set of hardening items to the dedicated testing phase (referenced as "→ P17 / per G-TRACE" throughout the [progress log](../progress/progress-log.md)). Those remain tracked there; this register indexes their existence rather than duplicating the log narrative.
