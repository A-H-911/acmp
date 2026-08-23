---
name: inmemory-provider-hides-db-refusals
description: "A write path never exercised against SQL Server can be broken while four suites pass — the EF InMemory provider has no identity columns, no filtered indexes, no FK behaviour."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 92bd3c15-77a4-4a75-851c-dd5bd3f3175a
  modified: 2026-08-14T00:35:07.186Z
---

**`DEF-066` (2026-08-14): assigning a stream to a member had NEVER worked against a real
database, and `ADR-0043` steps 3 and 4 both shipped on top of it.**
`membership.member_streams.StreamId` was an IDENTITY column — EF's `OwnsMany` composite-key
convention treats the non-FK half as store-generated — so `AssignStreams`, which sets a real
`Stream.Id`, was refused: *"Cannot insert explicit value for identity column"*.

**Why:** the failure is only visible on the provider that enforces it. `Acmp.Api.Tests` runs
InMemory (no identity columns — it accepts the explicit key), e2e runs
`KEYCLOAK_ADMIN_ENABLED=false` so the invite path never executes and nothing assigns streams,
domain tests use no database, and **no integration test had ever written a `member_streams` row**.
Four green suites over a feature that could not work once. It was found because a step-5 test
failed in its *setup* — that setup was the first code in the repo ever to assign a stream against
SQL Server.

**How to apply:** before trusting an EF write path, ask *"has this ever run against SQL Server?"*
— grep `Acmp.Integration.Tests` for a write to that table, not just for tests of the handler.
`Acmp.Integration.Tests` exists precisely to contrast "SQL rejects / InMemory accepts", but it only
covers what someone wrote. Pairs with [[read-before-calling-it-a-defect]] in reverse: here the
implementation *looked* correct at every layer a reviewer checks.

Two more things this session, same file:
- **EF cannot scaffold an identity removal.** Its generated `AlterColumn` throws *"To change the
  IDENTITY property of a column, the column needs to be dropped and recreated."* Rebuild the table
  by hand (create → copy → drop → `sp_rename`), and **copy rows rather than dropping the column**.
- **`dotnet ef migrations add` rewrites the model snapshot with CRLF** and the repo is LF, so a
  no-schema-change migration shows a whole-file diff. `git checkout` the snapshot when the change
  is line-endings only; normalize to UTF-8 **BOM + LF** (`.editorconfig` sets `charset =
  utf-8-bom`) or the format gate fails. See [[verify-mechanically-not-carefully]].
- **Semgrep `csharp-sqli` taints a method PARAMETER reaching `CommandText`.** A test DB name built
  from a literal + `Guid` is clean; the same string passed in as an argument is blocking. Remove
  the taint source rather than adding `// nosemgrep`.

---

**The same question, one layer out: "has this call ever been PERMITTED by the real grant?"**
(2026-08-14, SC-011 / PR #275.) Widening `IIdentityProvider` with a read raised the identical
shape of risk: the handler tests fake the port, so a Keycloak permission failure is invisible until
production. Standing up a **throwaway Keycloak 26.0** (`docker run … start-dev`, one realm, one
confidential client granted **exactly** `admin-client.env`'s `{manage-users}` and nothing else)
answered it in ninety seconds:

| call | status |
|---|---|
| `GET /users?first=&max=` | **200** |
| `GET /users/{id}/role-mappings/realm` | **200** |
| `GET /roles/{name}/users` — the "fewer calls" inversion | **403** |

**The clever shape is the one the minimal grant refuses.** Reading roles by listing users per role
is six calls instead of one per user, and it would have failed *in production only* — `DEF-066`'s
class exactly. The adapter therefore reads role mappings per user because that is what the grant
allows, and `probe-keycloak-grant.mjs` gained call 9 so the claim stays re-verifiable rather than
asserted in a comment. **The port widened; the credential did not.**

**How to apply:** a throwaway realm/container is cheap and gives a real answer. Any time a design
depends on a permission you have not personally seen succeed under the *exact* production grant,
measure it before building around it — and record the shape that was REFUSED, because that is the
one a later "optimisation" will reach for.
