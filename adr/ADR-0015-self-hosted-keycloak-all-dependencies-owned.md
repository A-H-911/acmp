# ADR-0015: ACMP Self-Hosts Keycloak and Bundles All Runtime Dependencies

- Status: Accepted
- Date: 2026-06-25
- Deciders: Architecture Committee (secretary-confirmed, 2026-06-25)
- Supersedes: the *federation / "must not own an IdP"* aspect of **ADR-0004**; amends **ADR-0013** and **CON-001**.

## Context and Problem Statement

ADR-0004 and ADR-0013 assumed the organization provides a **Keycloak** instance that ACMP federates to for SSO ("ACMP must not own an IdP"), and treated **SQL Server** and **Keycloak** as the two runtime dependencies allowed to live *outside* the ACMP Docker Compose stack.

That assumption was recorded explicitly as **ASM-001** (`docs/41-raid.md`): *"The org's Keycloak instance will be accessible to ACMP… ACMP does not need to manage a separate IdP. If false: … Keycloak must be provisioned as part of the Compose stack — a scope expansion."*

**ASM-001 is now false: the organization has no Keycloak yet.** The secretary has further directed that the platform **own all of its runtime dependencies** — i.e., extend the self-contained principle (CON-001) so there are **zero external runtime services** in v1. This ADR records the resulting decision and its blast radius.

## Decision Drivers

- **ASM-001 proved false** — there is no org Keycloak to federate to; waiting on one would block delivery with no timeline.
- **CON-001 strengthened** — the directive is that ACMP owns *all* runtime dependencies; the "two external exceptions" carve-out is withdrawn.
- **Do not build an IdP from scratch** — owning identity must not mean writing an authorization server. Keycloak (proven, OSS, OIDC) remains the engine; ACMP simply self-hosts it.
- **The application-side identity contract is unchanged** — OIDC authorization-code + PKCE, roles from realm-role/group claims, no self-registration. Only *who runs Keycloak* and *who defines the realm* change.
- Scale and sensitivity: ≤20 users, on-prem, high-sensitivity — a small, self-contained footprint is preferred and operationally acceptable.

## Considered Options

1. **Self-host Keycloak in ACMP's Compose stack, with an ACMP-owned realm; bundle SQL Server too** — fully self-contained; zero external runtime dependencies. *(Chosen.)*
2. **Build a custom identity/authorization server** — rejected: high security risk, reinvents a solved problem, violates the "don't build an IdP" guardrail.
3. **Block / wait for the org to stand up Keycloak** — rejected: no timeline; blocks PH-1; contradicts the self-contained directive.
4. **ASP.NET Core Identity (local app identity)** — rejected: abandons the OIDC standardization, MFA, and admin tooling Keycloak provides; larger long-term cost than self-hosting Keycloak.

## Decision Outcome

Chosen option: **"Self-host Keycloak (and bundle SQL Server) inside ACMP's Docker Compose stack, with an ACMP-owned realm."**

Concretely:

- **Keycloak runs as a container in ACMP's own `docker-compose`.** ACMP ships a **realm bootstrap** (`deploy/keycloak/realm-export.json` or equivalent): the ACMP realm, an OIDC client for the SPA/BFF (authorization-code + PKCE), the **eight canonical roles** as realm roles + groups (`Chairman`, `Secretary`, `Member`, `Reviewer`, `Auditor`, `Administrator`, `Submitter`, `Guest/Presenter`), and an initial bootstrap admin.
- **SQL Server is also bundled** in the Compose stack (Q1 decision) — v1 has **zero external runtime services**. (Webex stays a deferred **Phase-2** SaaS adapter, not a runtime infra dependency.)
- **The OIDC contract to the application is unchanged.** ACMP is still an OIDC client; it consumes group/realm-role claims and maps them to canonical roles; **no self-registration**. Because the realm is now ACMP-defined, ACMP controls the claim names itself (this **closes RISK-001** — no external claim-mapping coordination).
- **User provisioning is manual via the self-hosted Keycloak admin console** (Q3 decision). ACMP does **not** integrate the Keycloak Admin API in v1, so the **Membership module / P4 is unaffected** — it still just consumes identity and JIT-provisions a local profile on first login.
- **Keycloak's datastore is decided by a PH-0 spike** (Q2 decision) — see `OQ-038`: dedicated Postgres-for-Keycloak vs Keycloak-on-the-bundled-SQL-Server. v1 keeps application data SQL-Server-only (ADR-0003); the spike decides only where Keycloak's own operational store lives.

### Consequences

- **Good:** Fully self-contained — no dependency on an org IdP that does not exist; delivery is unblocked. ACMP owns its realm end to end (roles, MFA policy, session policy, claim names), so `RISK-001` dissolves and `OQ-003` (MFA/session) becomes a pure ACMP-realm setting. Reproducible from `docker-compose.yml`. Aligns with — and strengthens — the self-contained principle the organization mandated.
- **Bad / trade-off:** ACMP now **owns IdP operations** — Keycloak upgrades, CVE patching, signing-key rotation, realm + user-store backup, and admin-credential security are ACMP's responsibility, adding ops burden and attack surface (mitigated: low-traffic, internal, ≤20 users, standard Keycloak hardening; threat model `docs/24`/controls `docs/25` updated). **Login is now an ACMP-specific credential, not org-wide SSO** — unless ACMP's Keycloak later brokers/federates to a future org IdP (**deferred — `OQ-039`**). **Bundling SQL Server in production raises an edition/licensing question** (Express limits vs Standard; columnstore + FTS availability) — **`OQ-040`** for the deploy phase. **Keycloak + SQL Server are now in ACMP's 99.9% availability scope** — backup, warm-standby, and health checks must cover both.

## Validation

- `docker compose up` on a fresh VM brings up **keycloak + sqlserver + api + web + seq + minio** healthy within the deployment SLA; the realm bootstrap creates the 8 roles + groups + initial admin; a login round-trip against the bundled Keycloak lands a user on the dashboard with correct mapped roles.
- **Self-contained lint** (ADR-0013) is tightened: the Compose file and config reference **no external runtime hostname** at all in v1 (Webex is the only allowed external host, Phase 2).
- **Backup/restore** test covers Keycloak's datastore (per the `OQ-038` outcome) alongside SQL + MinIO.
- Permission-matrix tests still pass unchanged (the identity contract did not change).

## Links / Notes

- Resolves **ASM-001** (`docs/41-raid.md`) as *false → mitigated by bundling Keycloak*. Closes **RISK-001** (claim-mapping coordination with an external Keycloak admin).
- New open decisions (added to `docs/42-open-decisions.md`): **OQ-038** Keycloak datastore (PH-0 spike), **OQ-039** future upstream federation/brokering (deferred), **OQ-040** bundled SQL Server production edition/licensing (deploy phase).
- Supersedes the federation aspect of **ADR-0004** (OIDC + PKCE + claims-based roles + no self-registration remain in force); amends **ADR-0013** (the "two external exceptions" carve-out is withdrawn — all runtime dependencies are bundled) and **CON-001** (now: zero external runtime services in v1).
- Rollout: applied as a **change-set after P4 completes** (`execution-handoff/CHANGE-001-keycloak-ownership.md`); P4 code is unaffected.
