# ADR-0004: Keycloak (OIDC) as Identity Provider

- Status: Accepted — OIDC identity protocol in force; the *federation / external-org-Keycloak* aspect is **superseded by ADR-0015** (ACMP self-hosts Keycloak)
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

> **Amendment (2026-06-25, ADR-0015):** ASM-001 proved false — the organization has no Keycloak. ACMP now **self-hosts Keycloak** as a bundled container with an **ACMP-owned realm**, instead of federating to an org instance. Everything below about the **OIDC protocol** (authorization-code + PKCE), **roles from group/realm-role claims**, **no self-registration**, and **ABAC for per-topic capabilities** remains in force — only *who runs Keycloak and defines the realm* changed (ACMP, not the org). See ADR-0015.

## Context and Problem Statement

ACMP is a high-sensitivity governance tool. It needs SSO (committee members must not manage a separate ACMP password), role-based authorization (Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest/Presenter), and controlled onboarding (no self-registration). The organization is mid-migration from an internal auth service to Keycloak.

## Decision Drivers

- The organization's internal auth service is migrating to Keycloak; ACMP should land on the destination, not the origin.
- ACMP must not build its own IdP — that is out of scope and a security anti-pattern (password storage, MFA, session management are unsolved problems ACMP should not own).
- Committee roles are sourced from Keycloak group/realm-role claims and mapped to ACMP's canonical role set; ACMP trusts the claim, not a local user table for identity.
- No self-registration: membership is invitation/provisioned only (a Secretary or Administrator creates a user or links an existing Keycloak account). The sensitivity of the data (unreleased architectural decisions, votes) makes public self-registration a security risk.
- Per-topic capabilities (Owner, Assignee, Presenter) are ABAC attributes managed inside ACMP; these are not Keycloak roles.

## Considered Options

1. **Keycloak (OIDC, authorization-code + PKCE)** — federate to org Keycloak; consume group/realm-role claims; map to ACMP canonical roles.
2. **Build a local authentication system** — username/password, local user table, ACMP-managed sessions. Rejected: security burden, no SSO, password management.
3. **Azure AD / Entra ID** — not available in this deployment context; org is on Keycloak.
4. **ASP.NET Core Identity (local)** — local identity with optional external provider. Unnecessary if Keycloak is available and federating to it is the brief mandate.

## Decision Outcome

Chosen option: "Keycloak OIDC (authorization-code + PKCE)", because the organization mandates SSO via Keycloak, ACMP must not own an IdP, and Keycloak's group/realm-role claim mechanism cleanly maps to the ACMP canonical role set without building a custom authorization server. PKCE ensures the authorization-code flow is secure for SPA/BFF usage.

### Consequences

- Good: no password management in ACMP; SSO for committee members; Keycloak's MFA, session management, and account lifecycle are inherited; roles are centrally managed by Keycloak admins; ACMP token validation is a standard ASP.NET Core JWT middleware configuration.
- Bad / trade-off: ACMP has a runtime dependency on the Keycloak server being available (if Keycloak is down, ACMP login fails). Mitigate: token validation is local (JWT signature + expiry check); existing sessions continue until token expiry. Role changes in Keycloak take effect at next token refresh — there is a window of up to token TTL where stale roles are honoured. Keycloak group/role schema changes must be coordinated with ACMP role-mapping configuration.

## Validation

- Integration test: obtain a token from a test Keycloak realm, pass it to ACMP's protected endpoints, verify correct role resolution.
- Test role-claim mismatch scenarios (unknown claim, missing claim, revoked user) and confirm ACMP returns 401/403 correctly.
- Test that invitation-only provisioning prevents unauthenticated or non-provisioned users from accessing any protected resource.

## Links / Notes

- Onboarding flow: Administrator provisions a user in Keycloak (or confirms an existing Keycloak account), assigns the correct group/role, and links the account in ACMP. ACMP creates a local profile on first authenticated login (just-in-time provisioning of the local display record, not the identity).
- Per-topic ABAC (Owner/Assignee/Presenter) is stored and enforced in ACMP's own authorization layer, not in Keycloak.
- Full role-permission matrix: `docs/10-permission-role-matrix.md`.
- Related: ADR-0001 (modular monolith — auth as Platform module), ADR-0010 (voting attribution requires authenticated user identity), ADR-0013 (Keycloak is one of two allowed external identity dependencies).
