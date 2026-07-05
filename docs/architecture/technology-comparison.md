---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Technology Comparison — ACMP

This document summarizes the build-vs-buy-vs-integrate verdicts for ACMP as weighted-criteria comparisons. Options are scored **1–5** (5 = best fit) per criterion; **Weighted score** = Σ(weight × score), out of 5. Weights reflect ACMP's binding constraints: on-prem + government sensitivity, EN/AR RTL first-class, SQL Server mandate, self-contained (CON-001), and right-sizing for ≤20 users. Rejected options are retained with their reason. Each verdict cites its governing ADR. Capability claims that could not be verified against a primary source are marked `unverified`.

---

## 1. Platform strategy — Build vs Buy vs OSS vs Fork

No off-the-shelf product covers architecture-committee governance + traceability graph + EN/AR RTL + on-prem SQL Server + government sensitivity.

| Criterion | Weight | Buy SaaS (BoardEffect/OnBoard/Diligent) | OSS platform (Backstage/Outline) | Fork OSS (Log4brains/Backstage) | **Build modular monolith** |
|---|---|---|---|---|---|
| Fit for gov / on-prem | 0.25 | 1 (SaaS only) | 3 (self-hostable) | 3 | **5** |
| EN/AR RTL first-class | 0.20 | 1 (weak/none) | 2 (weak) | 2 (build it yourself) | **5** |
| Architecture-committee workflow | 0.20 | 2 (generic) | 1 (dev portal, not committee) | 2 (wrong start domain) | **5** |
| Time to first value | 0.10 | 4 (if it fit) | 2 (heavy customization) | 2 (fight the model) | **3** |
| Long-term control | 0.15 | 1 (none) | 2 (limited) | 2 (forks diverge) | **5** |
| Cost | 0.10 | 2 (recurring licence) | 3 (dev + complexity) | 3 (maintenance debt) | **4** |
| **Weighted score** | **1.00** | **1.55** | **2.15** | **2.40** | **4.65** |

**Verdict: Build a modular monolith; integrate proven tools (Keycloak, Tarseem, MinIO, Seq, Hangfire, TipTap); reuse patterns (MADR, C4, docs-as-code).** ADR-0001, ADR-0002. Rejected: Backstage (Node.js + PostgreSQL stack mismatch, no Arabic/RTL, IDP not committee tool); board SaaS (cloud-only, cannot enter a sensitive network); forking (starts from the wrong domain model).

---

## 2. Datastore — SQL Server only vs polyglot

| Criterion | Weight | **SQL Server only** (transactional + columnstore + FTS) | Polyglot (SQL + separate analytics/search DB) |
|---|---|---|---|
| Honors SQL Server mandate | 0.25 | **5** | 3 (adds a second engine) |
| Single-transaction immutability (vote+decision+audit) | 0.25 | **5** | 2 (distributed tx / dual-write) |
| In-process traceability graph traversal | 0.15 | **5** | 2 (cross-store joins) |
| Operational simplicity (≤20 users) | 0.20 | **5** | 2 (extra containers, sync) |
| Analytical/report performance | 0.15 | **4** (columnstore) | 4 (purpose-built OLAP) |
| **Weighted score** | **1.00** | **4.85** | **2.45** |

**Verdict: SQL Server only** — transactional store, columnstore reporting read models, and Full-Text Search in one app-owned instance. ADR-0003. Rejected: separate analytics DB and FILESTREAM (binary blobs degrade SQL performance — files go to MinIO instead). Second store considered only on measured evidence.

---

## 3. Search — SQL FTS vs OpenSearch vs Meilisearch

At ≤20 users and content in the hundreds (not millions) of documents.

| Criterion | Weight | **SQL Server FTS** | OpenSearch (self-hosted) | Meilisearch |
|---|---|---|---|---|
| Self-contained (no extra container) | 0.25 | **5** | 2 | 3 |
| Adequate quality at this scale | 0.20 | **4** | 5 | 5 |
| Operational overhead / index sync | 0.20 | **5** | 2 | 3 |
| Arabic tokenization / stemming | 0.15 | 3 (`unverified` — SQL Arabic word-breaker quality) | 4 | 4 (`unverified` — Arabic tokenizer) |
| Fuzzy / typo tolerance | 0.10 | 2 | 5 | 5 |
| Resource footprint | 0.10 | **5** | 2 | 4 |
| **Weighted score** | **1.00** | **4.20** | **2.95** | **3.65** |

**Verdict: SQL Server FTS in v1**, with self-hosted OpenSearch adopted only if FTS proves measurably insufficient (never the org's ELK). ADR-0011. Open question: SQL Server's Arabic word-breaker quality — evaluate with real Arabic topic/MoM content; if poor, Meilisearch is the lower-overhead fallback.

---

## 4. Diagrams — Tarseem sidecar vs Structurizr vs build vs draw.io

| Criterion | Weight | **Tarseem sidecar** | Structurizr (Lite) | Build diagram editor | Embed draw.io |
|---|---|---|---|---|---|
| Arabic / RTL first-class | 0.25 | **5** (HarfBuzz + geometry mirror) | 1 (no RTL layout) | 3 (build it) | 2 |
| JSON-spec-as-source (diff/hash/re-render) | 0.20 | **5** | 4 (DSL text) | 3 | 1 (not spec-driven) |
| Coverage of needed diagram families | 0.15 | **5** (11 families incl. C4) | 4 (C4 focus) | 2 | 4 |
| Integration effort (thin sidecar) | 0.20 | **5** | 3 (Java server) | 1 (years of work) | 3 |
| Stack fit / self-hostable | 0.10 | **4** (Python container) | 3 (Java) | 4 | 4 |
| Maturity | 0.10 | 3 (v1.0.0, early) | **5** (established) | 2 | 4 |
| **Weighted score** | **1.00** | **4.65** | **3.05** | **2.45** | **2.80** |

**Verdict: Integrate Tarseem as a Phase-2 render sidecar; the version-controlled JSON spec is the source of truth.** ADR-0006. Rejected: Structurizr (no Arabic RTL layout, duplicates Tarseem); building an engine (prohibitive); draw.io (not spec-driven). Early-maturity risk mitigated because specs outlive the renderer and artifacts regenerate from them.

---

## 5. Identity — self-hosted Keycloak vs federate vs build vs cloud

| Criterion | Weight | **Self-hosted Keycloak (ACMP realm)** | Federate to org IdP | Build own IdP (ASP.NET Identity) | Cloud (Entra ID) |
|---|---|---|---|---|---|
| Self-contained (CON-001) | 0.25 | **5** (bundled, app-owned) | 2 (external dependency) | 4 | 1 (cloud) |
| On-prem / data residency | 0.20 | **5** | 4 | 5 | 1 |
| Proven auth engine (no security build) | 0.20 | **5** (OIDC + PKCE) | 5 | 1 (months of critical work) | 5 |
| Claims-based roles + no self-registration | 0.15 | **5** | 4 | 3 | 4 |
| Operational burden (upgrades, backups) | 0.10 | 3 (now ACMP's scope) | **4** (org-run) | 2 | 4 |
| Time to value | 0.10 | **4** | 4 | 1 | 4 |
| **Weighted score** | **1.00** | **4.65** | **3.65** | **2.75** | **2.65** |

**Verdict: Integrate self-hosted Keycloak** with an ACMP-owned realm, consumed via OIDC auth-code + PKCE; roles from group/realm-role claims; no self-registration. ADR-0015 (supersedes the earlier "federate to org IdP" framing of ADR-0004; OIDC + PKCE + claims-based roles remain in force). Rejected: building an IdP (security-critical reinvention); cloud IdP (violates on-prem). Keycloak now sits in ACMP's own ops scope, covered by the bundled backup + warm standby.

---

## 6. Deployment — Docker Compose vs Kubernetes

| Criterion | Weight | **Docker Compose (single VM + warm standby)** | Kubernetes |
|---|---|---|---|
| Right-sized for ≤20 users | 0.30 | **5** | 1 (disproportionate) |
| Operational simplicity / single team | 0.25 | **5** | 2 |
| Meets 99.9% via redundancy + backups | 0.20 | **4** (standby + fast restart) | 5 (HA cluster) |
| Self-contained (CON-001) | 0.15 | **5** | 3 (control plane weight) |
| Resource footprint | 0.10 | **5** | 2 |
| **Weighted score** | **1.00** | **4.75** | **2.15** |

**Verdict: On-prem VM(s) + Docker Compose; no Kubernetes, no service mesh, no message broker.** Availability target met by warm standby + nightly backups + `restart: unless-stopped`, not clustering. NFR-055, C-DEPLOY/C-SCALE; consistent with ADR-0001/ADR-0013.

---

## 7. Companion-tool verdicts (settled, single dominant option)

These reduced to one viable option under the constraints; recorded for traceability rather than as multi-option scorings.

| Component | Verdict | Tool / approach | Rationale | Governing ADR |
|---|---|---|---|---|
| ADR module | Build (MADR pattern) | In-app ADR entity; MADR field structure extended with committee fields | Needs committee workflow + bilingual labels + traceability; Log4brains is static-site-only | ADR-0009 (immutability); MADR reuse-partially |
| Wiki / Knowledge | Build thin + embed TipTap | Knowledge module + TipTap OSS editor on SQL Server | Outline needs PostgreSQL (stack mismatch); thin module suffices; TipTap Pro features not required | ADR-0003 (SQL-only datastore) |
| Notifications | Build in-app (v1) + Webex adapter (Phase 2) | `INotificationChannel`; in-app center only in v1 | No SMTP relay in v1; org notification platform off-limits; adapter plugs in later | ADR-0005 |
| Object storage | Integrate | Self-hosted MinIO behind `IFileStore`; pre-signed URLs | S3-compatible, lightweight, Docker; swappable via the abstraction | ADR-0003, ADR-0014 |
| Background jobs | Build/configure | App-owned Hangfire, in-process, on ACMP's own SQL | Solves durable jobs + retries + dashboard; org Hangfire forbidden (CON-001) | ADR-0004 (jobs note), ADR-0014 |
| Observability | Integrate | Self-hosted Seq + Serilog + OpenTelemetry | Right-sized, .NET-native; org ELK/Seq forbidden (CON-001) | ADR-0014 |
| Reporting | Build | SQL columnstore + EF Core read models + React dashboards | Adequate at this scale; no separate BI/OLAP layer needed | ADR-0003, ADR-0022 |
| Research / Keystone | Build standalone + optional import | Research module standalone; Keystone import additive | Not all research uses Keystone; it must never be a hard dependency | ADR-0007 |

Board/committee SaaS (BoardEffect, OnBoard, Diligent, Fellow, Hugo) are all cloud-only with weak or `unverified` RTL support and cannot be deployed in a sensitive network — evaluated learn-only. Backstage, Structurizr, Outline, and adr-tools/dotnet-adr are learn-only pattern sources, not dependencies.

---

## Traceability

Summarizes `docs/domain/build-vs-buy-vs-integrate.md` (Deliverable 31), informed by `docs/domain/open-source-landscape.md` (Deliverable 29), `docs/domain/tarseem-analysis.md`, and `docs/domain/keystone-analysis.md`. ADRs: ADR-0001 (modular monolith), ADR-0002 (.NET stack), ADR-0003 (SQL Server + MinIO + columnstore), ADR-0004/ADR-0015 (Keycloak, self-hosted), ADR-0005 (notifications), ADR-0006 (Tarseem), ADR-0007 (Keystone optional), ADR-0009 (immutability), ADR-0011 (SQL FTS), ADR-0012 (frontend), ADR-0013 (deployment), ADR-0014 (jobs/observability/storage), ADR-0022 (reporting). Weights and per-criterion scores are this document's synthesis for comparison; the verdicts and their rationale are drawn from the cited sources.
