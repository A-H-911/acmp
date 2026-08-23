---
name: reconciliation-and-voting-eligibility
description: "The DEF-065 reconciliation (#275) and DEF-041 voting eligibility (#276/#277) — what shipped, what still blocks the deploy, and the four lessons that cost time"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7f0a7303-5ece-41a2-9f0e-cb8560e23cb5
  modified: 2026-08-14T18:50:18.021Z
---

**2026-08-14. Two queue items delivered; the deploy is still blocked, and now by an OPERATOR
ACTION rather than unwritten code.**

## DEF-065 / DEF-071 — the reconciliation (PR #275, `9508eef`)

`POST /api/members/reconcile` (Administrator only) creates a `committee_members` row for every
Keycloak account that has none **and grants it the wildcard stream in the same operation** —
`DEF-071`'s missing clause. Reconciliation runs on a host already up, and `Program.cs` migrates at
boot, so the deploy carrying it runs the step-5 backfill **first**, against ~1 row; creating rows
and leaving assignment to a later step reproduces `DEF-065` exactly. **Only for rows it creates** —
an admin may have deliberately narrowed someone. `SC-011` records the port widening
(`IIdentityProvider.ListUsersAsync`, its first READ on a four-write port).

⚠⚠ **`DEF-065` + `DEF-038` STAY OPEN and the deploy is STILL BLOCKED — the code existing is not
the fix; the command RUNNING in prod is.** Three steps on the `DEF-065` row: two env vars
(`DEC-047` d3) → deploy → one authenticated POST, **then read the returned partition** rather than
assuming it ran. ⚠ The Administrator token has **no CLI path** (the service account is not a
committee member) — take it from a signed-in admin's devtools.

⚠ **`ListUsersAsync` has NO live coverage** — e2e runs `KEYCLOAK_ADMIN_ENABLED=false`. `AC-011`
would change that; it is the same gap that let `DEF-066` survive two steps.

## DEF-041 — voting eligibility (PR #276 `8f200f1`, #277 `e31f7ac`)

Chairman **or** Secretary; **Administrator excluded** under SoD-5 and that is the *discriminating*
case, named in both the API theory and the component test. ⚠ The row's a11y claim was **wrong** —
the toggle was a `span role="switch" aria-disabled`, a *disabled switch*, inoperable but present.
⚠⚠ **The design contradicted the placement the code comment implied**: `UsersMembership.tsx`'s
header says editing lands in the user detail (how `ADR-0042` step 3 placed streams), but the
`.dc.html` draws the toggle as an operable **button in the directory row** and defines a
`voteEligible` detail label nothing renders. Following the comment would have been an INV-014
deviation reached by *careful* reasoning from a stale comment.

## The four lessons

1. ⚠⚠ **MEASURE A KEYCLOAK GRANT, NEVER ASSUME IT.** A throwaway KC 26.0 granted exactly
   `{manage-users}`: `GET /users` **200**, `/users/{id}/role-mappings/realm` **200**, but
   `GET /roles/{name}/users` **403**. The *clever* shape (6 calls by role instead of 1/user) is the
   one the minimal grant REFUSES — a production-only 403 of the `DEF-066` class. Grant did not
   widen; `probe-keycloak-grant.mjs` call 9 keeps it re-verifiable. Same question as
   [[inmemory-provider-hides-db-refusals]], one layer out.
2. ⚠ **An endpoint policy over a command that already carries `AllowedRoles` proves NOTHING in a
   test** — removing `.RequireAuthorization(Policies.AdminUsers)` left all 9 API tests GREEN
   (`AuthorizationBehavior` enforces the same matrix). It also means every per-endpoint policy
   ships a **403 nobody audits** until `DEF-056`.
3. ⚠ **A mutation nothing catches is a decision nobody recorded.** "Only rows it creates" survived
   4 mutants, but the *plausible* wrong version — wildcard any member holding **zero** streams —
   was caught by nothing until a test carried a pre-existing zero-stream member. Ask of every
   deliberate asymmetry: **which test fails if someone later "fixes" it?**
4. ⚠⚠ **The suite could not see the regression #276 shipped.** Turning the `span` into a `button`
   left `.adm-switch`'s hard-coded `cursor: not-allowed`, telling *exactly the two roles allowed to
   use it* that it is forbidden. **Component tests, axe and CI all run in JSDOM, which does not
   render.** If a change is visual, LOOK: throwaway page importing only the real route's
   stylesheets, served over **http** (`file:` is blocked in the Playwright MCP), measured in-browser.
   ⚠ The full-page screenshot then showed a stray shape the DOM proved absent — **a screenshot is
   evidence about pixels, not elements; when they disagree, measure.** Pairs with
   [[a-green-suite-is-not-a-look]].

## DEF-072 (mine) — I turned `G-COMPLETE` red

Quoting a `.dc.html`'s template syntax into a progress entry trips the unfinished-work screen (it
includes an empty-mustache pattern). **Code-span quoted fragments** — the screen strips code spans
first. ⚠ `progress_entries` is **append-only**, so the repair was `package_close` → whole-file
`git checkout` of `progress_entries.jsonl` → `package_open` (store is a fresh **in-memory** SQLite
rebuilt from JSONL) → re-append. ⚠ The first `DEF-072` title spelled out the marker words and was
caught by the same screen: **describing a pattern counts as writing it.** ⚠⚠ **Run `gate_run()`
AFTER writing, not only before** — v4 now has a typed `correction` progress event for this.
