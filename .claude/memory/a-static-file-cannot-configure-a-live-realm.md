---
name: a-static-file-cannot-configure-a-live-realm
description: "Keycloak imports realm-export.json only on FIRST run, so anything declared there never reaches prod or UAT — reconcile.sh is the only seam that does. Third occurrence."
metadata: 
  node_type: memory
  type: project
  originSessionId: 01449c66-99ef-4bad-b978-5afc8ccf49ef
  modified: 2026-08-11T22:39:07.585Z
---

`deploy/keycloak/realm-export.json` reaches a **fresh stack only**. Keycloak never re-imports a realm
that already exists, and prod and UAT both have realms with real data. A client, scope, or redirect
URI declared there **silently does not appear** on the two deployments that matter — while every
health check stays green, because Keycloak itself is running perfectly.

`deploy/keycloak/reconcile.sh` exists for exactly this, and runs in **every** topology (prod is
`base + overlay`, so the sidecar from the base compose runs there too).

**Three occurrences of the same seam:**

| | |
|---|---|
| `DEF-023` | redirect URIs — *nobody could log in*, all checks green |
| CHANGE-004 | the `basic` scope carrying `sub` — JIT provisioning breaks |
| `DEF-049` | the ADR-0038 service-account client (2026-08-12) |

**A secret settles the argument outright:** a client secret can only be made to match
`gen-secrets`' file by being **set from** that file, so a confidential client cannot be fully
expressed in a static export at all.

**Why:** a single committed file cannot express state a live realm already owns, and the failure is
invisible rather than loud.

⚠ **`DEF-051`: a failed reconcile is itself silent** — nothing `depends_on` `keycloak-config`, so the
stack comes up healthy with the work undone. That is DEF-023's failure mode sitting inside DEF-023's
own fix. **A green e2e is not evidence a reconcile succeeded.**

**How to apply:** anything Keycloak-side that must exist on an *existing* deployment goes in
`reconcile.sh`, idempotently, reading its result back rather than trusting an exit code. Reserve the
export for fresh-stack defaults. Related: [[controls-must-detect-and-tell]],
[[verify-mechanically-not-carefully]].
