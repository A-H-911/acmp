# RESUME — ACMP

**The single entry point. Rewritten 2026-08-12 at session end (second rewrite that day).** Every
`handoff/RESUME-*.md` and every `handoff/prm-*.md` other than this file and `handoff/prm-next.md` is
⛔ superseded history. This file is durably named so it never needs renaming again. The paste-able
kickoff prompt is **`handoff/prm-next.md`** — edit that file, never add another `prm-*.md`.

---

## 0. Orient (2 minutes, do not skip)

```
server_info() · package_open("tamheed-package") · gate_run()
```

⚠ **If `package_open` fails on `.lock`**, check the PID *properly* before removing it — the lock holds
a bare PID and "is it alive?" **lies** under PID reuse. Confirm the process does not exist, or that
its identity and `StartTime` do not match the lock's mtime, then delete
`tamheed-package/data/.lock`. Never remove it reflexively.

Then read **§2**. Then read `SC-003` … `SC-008` — six records of where an approved document and the
code legitimately diverged, and *why the code was right*.

---

## 1. State

| | |
|---|---|
| `main` | green (after two re-runs — see §5) · gates **7/7** · **136 evidenced / 12 narrated** |
| Verdicts | **81 Met · 12 Partial · 0 Pending** over 93 ACs — **the Pending is gone** |
| **Production** | ★ **LIVE ON `65e45d4`** · always-on · `i-04d9717feea79204b` · https://acmp.anas7ammo.dev |
| **UAT** | **also on `65e45d4`** · `i-07ac28ac2fedab921` · **stopped when idle** — start from `deploy/runbooks/cloud-operations.md` §1 |
| In-app user management | ★ **ENABLED on both** (`KEYCLOAK_ADMIN_ENABLED=true`, pinned 32-char secret) |
| Open defects | `DEF-012` `DEF-038` `DEF-039` `DEF-041` `DEF-045` `DEF-055` (**6 of 55**) |
| Open questions | `OQ-074` and `OQ-076` (everything else is `Deferred` by design or answered) |

**Phases `P1`–`P19` are COMPLETE.** `P14` (Tarseem diagrams) is deferred indefinitely (`DEC-028`).
The remaining work is **not a new slice**; it is the list in §4.

⚠ **The deployable sha is NOT HEAD, and do not trust a number written here for it.** `ci.yml`
`paths-ignore` skips `*.md`, `docs/`, `.claude/`, `tamheed-package/`, so governance and handoff
commits publish **no images** — but a code commit landing after this file was written does. **Both
boxes run `65e45d4`**; whether a *newer* published sha exists is a question for ECR, not for this
sentence. Ask it directly, and remember `web` is a **separate, environment-suffixed** tag (ADR-0037,
`DEF-019`) — `08-bootstrap-box.sh` refuses unless **both** pins resolve:

```bash
for r in api worker sqlserver-fts; do
  aws ecr describe-images --region us-east-1 --repository-name "acmp/$r" \
    --query 'sort_by(imageDetails,&imagePushedAt)[-1].{tag:imageTags[0],at:imagePushedAt}' --output text
done
aws ecr describe-images --region us-east-1 --repository-name acmp/web \
  --query 'sort_by(imageDetails,&imagePushedAt)[-2:].{tag:imageTags[0],at:imagePushedAt}' --output text
```

⚠ **`main`'s push run can fail for reasons that are not the code, and `#250`'s did — twice.** Attempt
1: the `backend` job hit its 25-minute timeout. Attempt 2: **25 of 303 `Acmp.Api.Tests` failed with
`HttpClient.Timeout of 100 seconds elapsing`** and the suite took 17m9s instead of ~5m — against an
**in-process** `TestHost`, where a 100-second HTTP call means the runner was starved, not that
anything was broken. Attempt 3 passed clean. Separately the `sbom` job failed the Security workflow
on a third-party download (`HTTP status=000`); its re-run passed in 16s.

**How it was established as environmental rather than assumed:** none of `17b6edf`, `65e45d4` or
`69e865a` touches a **single `.cs` file** (they are `.tsx`, shell + workflow, and one `.mjs`), the
`65e45d4` push run passed in 9m34s, and PR #250 had passed the identical tree in 5m31s. **Before
concluding `main` is red for a code reason, check `gh run list --branch main` and ask what the
failing commit actually changed** — a timeout is a symptom of the host, not a diagnosis of the code.

---

## 2. ⚠ Rules this project has paid for. Read them before you write code.

**A. Read the implementation before calling something a defect.** Now **nine** instances; none was
caught by a gate. Last session it made a task disappear (`DW-025`'s premise was false — ACMP has no
reschedule). This session it killed a suspected defect in three minutes: the live invite probe showed
an invited member's role resolves to `Guest`, which looked like the guest-expiry sweep would disable
invitees — but `ExpireGuestAccess.cs:59` filters `AccessExpiresAt != null && < now`, and an invitee's
window is null. **Read the predicate, not the doc comment that describes it.**

**B. An ADR/AC citation in a test name is load-bearing, and no gate reads it** (`SC-004`, `SC-007`).
Before overriding a test whose name or `InlineData` cites an ADR or AC, read that row.

**C. When an ADR names a specific seam, check the harness can reach it before approving** (`SC-005`).

**D. Check whether it is already built.** Grep the domain enums, `i18n/locales/en.json`, and
`ACMP product context/*.dc.html` first.

**E. A green suite is not a look.** Render new screens in a browser, **in both directions**. ⚠ The
throwaway harness must import **only the stylesheets the real route imports**, or it lies to you.

**F. Prove, don't assume.** `OQ-070`'s answer (`manage-users` **alone**) contradicted my own written
candidate, and no gate would have caught the wider grant.

**G. Verify the DEPLOYED state, not the file that describes it.** This is the rule that paid best
this session and it paid three times over — see §3.

**H. ⚠ NEW — a measurement that indicts known-good code is measuring itself.** My line-ending check
was `grep -c $'\r'`; the quoting was lost, it degraded to `grep -c ''`, and it returned the LINE
COUNT for every file — reporting CRLF for `gen-secrets.sh`, which CI runs green every day. The tell
was that it convicted something already proven innocent. Use `tr -cd '\r' | wc -c`, which cannot
degrade that way. **And do not accept a single tool's negative as proof of absence:** the `Grep` tool
returned "No files found" for `AccessExpires`, which is in the tree — PowerShell found it instantly.

---

## 3. ✅ What shipped this session

### ★ The headline: production was 56 commits behind, and the handoff said otherwise

RESUME §4 item 1 used to say enabling in-app user management was "one variable". **It never was.**
Three measurements, in the order rule G asks:

1. SSM pinned `ACMP_IMAGE_TAG=e403e18…` on **both** prod and uat; `rev-list e403e18..bcd8e96` = **56**.
2. `GET /api/session/me` answered **404** on prod and **401** after the deploy.
3. **The decisive one:** `git show e403e18:deploy/keycloak/reconcile.sh` has **no `ensure_admin_client`**
   (it arrived in `122f41d`/#237). Setting the flag on the old image would have booted a perfectly
   healthy host authenticating as a Keycloak client **that did not exist**. Not a hypothesis — the
   prod reconcile log for this deploy literally reads `creating client 'acmp-admin-svc'`.

⚠ **One of my own probes was right for the wrong reason and is corrected in `OQ-075`:** I first used
`/api/session`, which is the `MapGroup` **prefix**, not a route — it 404s on the new code too. The
valid form is `/api/session/me`. The conclusion never rested on it, but a wrong evidence string in a
resolved row is what the next reader repeats.

### The release itself (`OQ-075`, resolved)

Backup **first and read back from S3** (`Acmp_20260812_173331.bak` + keycloak) → UAT → verify → prod.
Both: three one-shots `exited 0`, six services healthy, `db-migrate` clean against the live database,
`smoke.sh` PASSED, and both reconcile logs end **`realm-management grant is exactly: manage-users`** —
`OQ-070`'s minimum grant now proven on **two deployed realms**, not only in CI.

### The ADR-0038 write path ran for real, for the first time ever

`#250` adds `src/Acmp.Web/uat-invite-probe.mjs`. **No CI run could have done this:** `IIdentityProvider`
is registered only when the flag is on, `deploy/.env.example` sets it **false**, so the seven-service
e2e stack with a real Keycloak never even constructs the adapter, and every backend test uses
`FakeIdentityProvider`. Measured on UAT: invite **200**, roles **204**, and `AC-093`'s audit row read
back **out of the hash chain** (seq 1181, actor, subject, timestamp, **both** `beforeJson` and
`afterJson`), chain intact including that row. **`AC-093` Met**; `AC-088`/`AC-091` re-evidenced live.

### `DEF-053` `#248` · `DEF-054` `#249` — both Fixed

`/session` carries `RequireRole {guest, chairman, secretary}`; `up.sh` asserts `keycloak-config`
converged. Both proven by **forcing** the refusal: reverting `App.tsx` fails exactly the five denied
roles and no others; neutering `assert-oneshot.sh`'s comparison fails exactly the two cases that
depend on it. `assert-oneshot.sh` was extracted **because an inline check could not be tested** —
`up.sh` runs `gen-secrets.sh`, which clobbers your live dev secrets.

### `AC-004` — the last Pending now has evidence, and it is bad news (`OQ-076`)

The live realm has `ssoSessionIdleTimeout=1800`, so the control is real. But `automaticSilentRenew:
true` and **no app-side inactivity detection exists anywhere** — silent renew resets the SSO idle
clock, so an open tab is **never idle**. The 30-minute timeout can only fire once the tab is closed,
when there is no session to redirect. Same shape as `AC-090`. Recorded as **analysis, not
measurement** — the 30-minute observation has not been run.

---

## 4. Everything left, in order

**1. ★ LOG IN TO PRODUCTION. Nobody has, since the release, and it is the only thing standing
between "deployed" and "verified".** This is `DEF-023`'s lesson verbatim: six healthy containers, a
valid certificate, a correct issuer and `/api/` answering 401 — and nobody could log in. The release
put **two fail-closed middlewares** in front of every request on prod for the first time:
`GuestSurfaceMiddleware` (deny-by-default) and `PrincipalRevalidationMiddleware`. ⚠ **A member with
no role claim resolves to `Guest`** — measured on UAT — so any production account whose token lacks a
role claim is now confined to the guest surface where it previously had read access. **It is an
operator action:** `uat-login-probe.mjs` cannot reach prod (it carries UAT fixture passwords), so it
needs a real human account. Sign in, confirm the dashboard renders with your own name and role, open
`/members` and confirm the roster and the invite panel are there.

*What is already proven and does not need re-checking:* `smoke.sh` passes · `/api/session/me` is 401
not 404 · both reconcile logs read `realm-management grant is exactly: manage-users` · and the
production admin-client credential chain works end to end — a `client_credentials` request with the
pinned secret returns **200 with an access_token**, while a deliberately wrong secret returns **401
`unauthorized_client`**, so the endpoint is genuinely checking (`PE-281`).

**2. `OQ-076` — an operator decision.** Accept max-lifespan and amend
`AC-004`'s wording, or build inactivity detection that **stops** `automaticSilentRenew` (small, and it
makes the AC literally true). **Not** lowering `ssoSessionMaxLifespan` — that logs out *active* users
on a fixed clock. Also fix `AC-090`'s text, which cites a "60-minute idle timeout" against a realm
that says 30.

**3. The 11 remaining Partials — one campaign, approach already agreed.** `AC-003` `AC-005` `AC-006`
`AC-007` `AC-009` `AC-010` `AC-011` `AC-033` `AC-034` `AC-041` `AC-048`. Nearly all are Partial for
the *same* reason: proven by unit/handler tests with **no live leg**. `Acmp.Api.Tests` authenticates
with `TestAuthHandler`, a synthetic scheme, and `RealJwtAuthTests` only covers the 401 fail-closed
paths — so no test anywhere drives a **real Keycloak token** through the role matrix.
**The operator chose: CI E2E now, re-evidence on UAT later.** Suggested shape: one spec in the
existing `e2e.yml` stack that, per role, forces a 403 on the forbidden endpoints and asserts the nav
is absent — that covers `AC-005/006/007` with no fixtures. `AC-009/010/011/033/034` need ownership,
stream and meeting-scope fixtures and are a second tranche. `AC-041` is a manual Playwright render
that needs promoting into CI. **`AC-048` (`beforeunload`) is probably unprovable** — Playwright
auto-dismisses the native dialog — and Partial-with-a-recorded-reason is a legitimate outcome.

**4. `OQ-074`** — `DEC-037` never said *whose* view Chairman/Secretary "preview". Shipped as their
own slot. ⚠ **New evidence:** `navModel.ts`'s ACCESS map grants `session` to **guest only**, so
Chairman/Secretary are permitted on a page they have **no nav link to**. Answering has a nav
consequence either way; `DEF-053` deliberately left the map alone rather than pre-empt it.

**5. `DEF-038`** — the roster lists only members who have logged in. ⚠ **Partly overtaken:**
`GetMembers` now returns Active **or** Invited, so anyone invited *through the app* appears. The
residue is the 25 accounts seeded directly into Keycloak before that existed.

**6. `Streams.NameAr` on prod** — still not done. Real table is `membership.streams`.`name_ar`; the
C# names do not exist in SQL and every module owns a schema.

**7. `DEF-055`** (low) — `09-put-env.sh` refuses `ENABLED=true` without a `KEYCLOAK_ADMIN_CLIENT_SECRET`
and gives a reason that is **not** the real one (gen-secrets always writes the file, so ValidateOnStart
would pass). The behaviour is defensible; the message and comment are wrong. Do **not** relax the
`CHANGE_ME` branch.

**8. Remaining defects** — `DEF-039` (System Health renders a MinIO tile; the cloud moved to S3),
`DEF-041` (voting-eligibility toggle absent from the accessibility tree), `DEF-012` (package-data
residue in `v_backlog`), `DEF-045` (classified: harness causes, no product defect — but **cause 3 is
still unfixed**, so a UAT e2e run is red for a known reason).

**9. `OQ-062` is stricter in code than in the decision** — a *permanent* UAT Webex ban vs "off
**until** a UAT space exists", so the exit condition can never be met.

**10. `AC-091`'s last clause, if you want it** — first login consuming the invite. Proving it live
needs the temporary password, which the probe refuses to print. A future probe could carry the value
**in-process** from the invite response into a login without ever rendering it.

**Not on this list, deliberately:** the ~45 `Deferred` open questions and the `DW-0xx` backlog are
parked by design. If a reschedule capability is ever built, it **must** call `IGuestWindowWriter`.

---

## 5. Gotchas that cost real time

- **Deploy as `acmp-admin`, never root.** Root bypasses the budget IAM-deny brake (`AC-085` leg 5);
  `[default]` in `~/.aws/config` **is** root and its session expires.
- **The deploy sequence that worked**, end to end: back up prod and **confirm the object in S3** →
  start UAT and poll **SSM `PingStatus`** (`instance-running` is not readiness) → fetch
  `/acmp/<env>/env`, re-pin both tags → `09-put-env.sh` → `08-bootstrap-box.sh <env> <full-sha>` →
  `smoke.sh` → read the **`keycloak-config` log** back over SSM.
- ⚠ **PowerShell joins arrays with SPACES.** `aws ssm get-parameter ... --output text` returns an
  **array of lines**; `[IO.File]::WriteAllText(path, $array)` writes one space-joined line and would
  have destroyed the env file. Use `($v -join "`n")` and verify the line count before publishing.
- **`/acmp/*` env parameters are LF**, not CRLF — an older memory said CRLF and went stale the same
  evening. `aws ssm get-parameter-history` settles it in one call.
- **The keycloak container's `docker exec` shell has no `KC_BOOTSTRAP_ADMIN_PASSWORD`** — the
  entrypoint exports it for its own process only. Read `/run/secrets/kc_bootstrap_admin_password`.
- **Use PowerShell for any `aws` call with a `/`-leading argument** — Git Bash rewrites `/acmp/prod/env`
  into `C:/Program Files/Git/acmp/...`. (`MSYS_NO_PATHCONV=1` also works, and the `deploy/aws/*` scripts
  already set it.)
- **Windows `python3` cannot see Git Bash's `/tmp`** — pass Windows-style absolute paths when building
  SSM `--parameters file://` payloads.
- **Write the Tamheed package only from `main`** — `tamheed-package/data` is git-tracked.
  `defect.fixed_by` is a **FOREIGN KEY**: put PR refs in `custom_attributes`.
  `open_question.lifecycle_status` is a **CHECK** over
  `Draft/Proposed/Approved/Rejected/Deferred/Implemented/Superseded/Obsolete` — "Resolved" rolls the
  batch back.
- **`gh pr create --body` with backticks/quotes breaks under PowerShell** — always `--body-file`.
  Same for `git commit -m` with a here-string: use `-F`.
- **A compose `secrets:` entry whose file is MISSING fails the WHOLE stack.**
- **New `.cs` files need a UTF-8 BOM**, and `.cs` must be **LF**.
- **Never run `gen-secrets.sh` against the repo to test it** — `SECRETS_DIR` is hardcoded and it will
  clobber the operator's live dev secrets.
- **`git status --porcelain` reports an untracked *directory*** — use `-uall`.
- **`realm-export.json` reaches FRESH STACKS ONLY.** `reconcile.sh` is the only seam that reaches
  prod/UAT.
- **The Playwright E2E suite is NOT UAT-only** — `e2e.yml` runs the full 7-service stack with a real
  Keycloak on every PR. ⚠ **But with `KEYCLOAK_ADMIN_ENABLED=false`**, so it never exercises the
  ADR-0038 write path at all.
- **Local `dotnet test` shows ~31 integration failures with Docker off** — Testcontainers, not a
  regression.
- **Prod and UAT differ on purpose.** Do not harmonise them.
