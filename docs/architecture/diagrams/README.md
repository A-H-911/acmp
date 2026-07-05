---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Diagrams — ACMP

ACMP diagrams are authored as **diagram-as-code inside [`../architecture.md`](../architecture.md)** — the container topology and module relationships live there as version-controlled text, so they diff, review, and evolve alongside the architecture prose. From **Phase 2**, the same diagrams are additionally maintained as a version-controlled Tarseem JSON spec (the source of truth) and rendered to SVG/PNG/PDF artifacts by the **Tarseem render sidecar** (ADR-0006); render is deterministic and needs no network. The diagram families the architecture uses are: **context** (C4 Level 1 system context), **component** (C4 Level 2 container and Level 3 module maps), **data-flow** (core-loop runtime sequence), **integration** (external adapters — Keycloak, Webex, Tarseem, Keystone), **deployment** (Compose topology + warm standby), and **dependency** (module dependency graph and traceability edges).
