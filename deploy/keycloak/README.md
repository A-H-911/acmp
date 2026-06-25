# Self-hosted Keycloak (ADR-0015)

ACMP bundles its **own** Keycloak with an **ACMP-owned realm** — there is no external IdP
(ASM-001 was false; the org has no Keycloak). v1 has **zero external runtime services**
(CON-001 strengthened). The application-side OIDC contract is unchanged (ADR-0004):
authorization-code + PKCE, roles from realm-role/group claims, **no self-registration**.

## What imports on first start

`realm-export.json` is mounted read-only into `/opt/keycloak/data/import/` and applied via
`--import-realm`. It defines:

- Realm **`acmp`** (registration disabled, brute-force protection on).
- Public OIDC client **`acmp-web`** (standard flow + **PKCE S256**), with an **audience mapper**
  emitting `aud: acmp-api` (the API validates that audience) and realm-role / group claim mappers.
- The **8 canonical roles** as both realm roles **and** groups, named **exactly** to match
  `AcmpRoles.All`: `Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest`.
  > Do **not** rename `Guest` to "Guest/Presenter": the claim mapper (`KeycloakRoleClaimMapper` /
  > SPA `roles.ts`) takes the leaf after the last `/`, so "Guest/Presenter" would mis-map to
  > `presenter` and fail. Presenter is a per-topic relationship (docs/10 §D), not a global role.
- An initial admin user **`acmp-admin`** (`Administrator` + `Secretary`) with **no committed
  password** — it imports with an `UPDATE_PASSWORD` required action (guardrail 7: no secrets in source).

## User provisioning (manual — Q3 / ADR-0015)

ACMP does **not** call the Keycloak Admin API. Provision people by hand:

1. Log in to the admin console at <http://localhost:8085/admin/> with the bootstrap admin
   (`KC_BOOTSTRAP_ADMIN_USERNAME` / `KC_BOOTSTRAP_ADMIN_PASSWORD` from `deploy/.env`).
2. Set a password for `acmp-admin` (realm **acmp** → Users), or create new users and add them to the
   matching group. On first SPA login, ACMP JIT-provisions a local profile (P4 Membership) — unchanged.

## Issuer / hostname wiring (why `keycloak.localhost:8085`)

The token issuer must be **byte-identical** for the browser and the API. `KC_HOSTNAME` pins it to
`http://keycloak.localhost:8085`:

- **Browser (SPA):** `*.localhost` resolves to loopback automatically, so `keycloak.localhost:8085`
  reaches the published port. `VITE_OIDC_AUTHORITY` uses the same URL.
- **API (in-cluster):** `extra_hosts: keycloak.localhost:host-gateway` routes that host to the Docker
  host, where `8085` is published — so the API validates against the same issuer the browser used.

If your browser does not auto-resolve `*.localhost`, add `127.0.0.1 keycloak.localhost` to your hosts file.

## Datastore (OQ-038)

Keycloak's **own** operational store is a dedicated **Postgres** container (`keycloak-db`, volume
`kcdata`) — OQ-038 option (a). ACMP **application** data stays SQL-Server-only (ADR-0003); this store
holds only Keycloak's realm/users/sessions and is covered by backup/restore (docs/33). The live
`docker compose up` bring-up is the operator's verification (the build sandbox cannot launch the stack).

## Production hardening (P18, not in this dev profile)

- Run `start` (production mode) behind TLS at a real hostname; set `KC_HOSTNAME` to that URL and the
  API's `Authentication__Keycloak__RequireHttpsMetadata=true`.
- Rotate the bootstrap admin credential; set realm MFA + session policy (OQ-003, now an ACMP-realm setting).
- Back up `kcdata` (Postgres dump) alongside SQL Server + MinIO; include the realm export.
