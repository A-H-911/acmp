---
name: immutable-history-cleanup-asymmetry
description: Under immutable history, deleting an upstream identity does not delete what it created downstream — it orphans it, permanently.
metadata:
  node_type: memory
  type: project
---

**Creating is reversible. Cleaning up is not.** ACMP's audit events are hash-chained and immutable,
so a `CommitteeMember` row can be **deactivated but never removed**. Identity is the Keycloak `sub`
(P17b made that universal). Those two facts together mean deleting a Keycloak user does not undo the
rows it caused — it strands them, and the orphan outlives the reason you deleted it.

**Why (DEF-029, 2026-08-09):** I deleted the fixed-password `e2e-*` Keycloak users after an e2e run,
for a real reason — their password is committed in `e2e/users.ts` and UAT's 443 is open to
`0.0.0.0/0`. The next run re-seeded them, Keycloak minted **new subs**, and JIT provisioning created
a **second** member row for each. Measured: `/api/members` returned **10 rows where 7 were
expected**, three names duplicated. `core-loop.spec.ts` then failed on an ambiguous
`getByRole('option', {name: 'E2E Member'})` — and it can never pass on that database until an
Administrator deactivates the orphans.

**How to apply:**
- **Disable, never delete**, any identity whose downstream rows are immutable:
  `PUT /admin/realms/acmp/users/{id}` with `{"enabled": false}`. A disabled account cannot
  authenticate — the security benefit is identical — and the `sub` survives, so the next run
  re-enables the *same* identity and duplicates nothing.
- Before deleting anything upstream, ask: *what did this create that I cannot remove?*
- To tell an orphan from its live twin, match `keycloakUserId` against the subs still in the realm.
  `MemberDto` exposes it; display name, role and email are all identical between the pair.
- Deactivating a member is an ordinary governance action, not data repair — but it needs the
  **Administrator** realm role, and only `acmp-admin` holds it.

See [[ph5-sl025-uat-live]] and [[localhost-ci-hides-load-races]].
