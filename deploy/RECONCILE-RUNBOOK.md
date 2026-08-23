# Reconcile runbook — the one thing blocking the stream-scope deploy (`DEF-065`)

**What this fixes.** ~25 Keycloak accounts have never signed in, so they have no `committee_members`
row. The `ADR-0043` step-5 backfill assigns the wildcard stream to members holding none — and **a
migration reaches only the rows that exist when it runs**, which in production is about ONE. Without
this, every one of those people is refused every stream-scoped write from their first login.

**Why a runbook and not just the row.** `DEF-065` carries these steps in prose. This file is what you
follow while a deploy is half-done. Read it end to end **before** starting: step 1 is the only one
that is awkward to undo.

> ⚠ **The command existing is not the fix. The command RUNNING is.** `DEF-065` stays Open until
> step 4 passes, and closing it before then recreates the trap this whole stream recorded once
> already — *correct, proven and evidenced is not the same as production being safe.*

---

## Before you start

| check | why |
|---|---|
| `git log --oneline -1` on the deployed ref | the build must contain `9508eef` (PR #275) or later, or `/api/members/reconcile` does not exist |
| You can sign in to the SPA as an **Administrator** | step 3 needs a bearer token and there is **no CLI path** — see the ⚠ below |
| Production is still quiet | prod had 0 topics and 1 sign-in ever; if that has changed, re-read the residual note in step 3 |

⚠ **The Administrator token has no CLI path, and this is not an oversight.** The Keycloak service
account (`acmp-admin-svc`) is *not* a committee member, so its token can never satisfy
`Policies.AdminUsers`. The only way to get a qualifying token is to be a signed-in Administrator.

---

## Step 1 — enable in-app user management (TWO variables, not one)

`DEC-047` d3: enabling the feature is **two** variables. `09-put-env.sh` refuses a placeholder
secret, deliberately, because `reconcile.sh` pushes that value into the Keycloak client — a
placeholder would publish the credential.

```bash
# in the environment's SSM parameter payload
KEYCLOAK_ADMIN_ENABLED=true
KEYCLOAK_ADMIN_CLIENT_SECRET=<a strong, real secret>     # NOT a placeholder; an unset one
                                                          # regenerates on every boot and never stays valid
```

⚠ `KeycloakAdminOptions` is `ValidateOnStart`. A flag that arrives **before** its secret **stops the
host** rather than degrading — so set both in the same push.

⚠ Never read the secret back out of Keycloak. The `OQ-070` probe did exactly that, put it in command
output, and had to be rotated. It travels one way: `gen-secrets` → `reconcile.sh` → Keycloak.

## Step 2 — deploy

Nothing special. On boot, `Program.cs` applies migrations **before** the first request, so the
step-5 backfill runs here — against the ~1 row that exists. That is expected and is exactly why
step 3 is not optional.

**No Keycloak grant change is needed.** `{manage-users}` was *measured* to cover the read the
reconciliation performs (`GET /users` → 200, `GET /users/{id}/role-mappings/realm` → 200). If you
ever see a 403 from this feature, re-run `scripts/probe-keycloak-grant.mjs` and **read the refusal
rather than widening the grant** (`ADR-0038`).

## Step 3 — run the reconciliation, **immediately** after the deploy

1. Sign in to the SPA as an Administrator.
2. Devtools → Network → any `/api/...` request → copy the `Authorization: Bearer …` value.
3. Call it:

```bash
curl -sS -X POST "https://<host>/api/members/reconcile" \
  -H "Authorization: Bearer <paste the token>" \
  -H "Content-Type: application/json"
```

⚠ **Do this immediately, not tomorrow.** Anyone who signs in *between* the deploy and this call gets
a JIT row with **zero streams**, and the command will not touch it — it is no longer a row this run
creates. That person falls back to `ADR-0043` clause (2): the roster shows them unassigned and an
Administrator assigns streams by hand. The window is minutes wide; keep it that way.

## Step 4 — verify the partition, do not assume it ran

The response is a partition of the realm. **Every account lands in exactly one bucket and the
buckets sum to the total** — that property is the point, because a bare `created` count cannot be
told apart from a run that silently skipped half the realm.

```json
{ "identityAccounts": 26, "created": 24, "alreadyProvisioned": 1,
  "skippedDisabled": 0, "skippedNoCommitteeRole": 1, "skippedDuplicateEmail": 0 }
```

Check it arithmetically:

```
identityAccounts == created + alreadyProvisioned + skippedDisabled
                             + skippedNoCommitteeRole + skippedDuplicateEmail
```

Then run the script, which re-reads the database rather than trusting the response:

```bash
node scripts/verify-reconcile.mjs --response reconcile-response.json \
  --connection "<the app's SQL connection string>"
```

### If a count looks wrong

| symptom | what it means | what to do |
|---|---|---|
| the buckets do **not** sum to `identityAccounts` | a bucket is missing from the response, or the handler changed | stop; this is a defect, register it |
| `created` is 0 and `alreadyProvisioned` is high | it already ran — this is the idempotent second run | fine, nothing to do |
| `skippedNoCommitteeRole` is large | accounts hold no canonical committee role in Keycloak | expected for the service account and bootstrap admin; investigate if it is more than a couple |
| `skippedDuplicateEmail` > 0 | two Keycloak accounts share one email — `DEF-045`'s duplicate identities | do **not** merge by hand; register it and decide which account is real |
| **409 Conflict** | either no identity provider is configured (step 1 incomplete) or **no wildcard stream exists** | read the server log — the message names which. A missing wildcard means the `ADR-0042` seed skipped a pre-existing `all-streams` row |
| **403** | your token is not an Administrator's | you used the service account, or a Secretary — only Administrator passes `Policies.AdminUsers` |

## Step 5 — close the loop in the package

```
work_bind("<deploy ref>", ["DEF-065", "DEF-038"])
audit_record / progress_update   # the partition, verbatim
```

Then set **`DEF-065` → Fixed** and **`DEF-038` → Fixed** — `DEF-038` closes here too, because the
roster now lists those people as ordinary members, which is the route `DEC-046` d2 chose over a
parallel Keycloak listing.

⚠ Do not close either one before step 4 passes.
