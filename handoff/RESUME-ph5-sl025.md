# ⛔ SUPERSEDED — do NOT paste this into a session

> **SUPERSEDED 2026-08-09 by [`RESUME-ph5-closeout.md`](RESUME-ph5-closeout.md). Kept as a record of
> the SL-025 provisioning work only. Do not act on anything below — its state is three days stale
> and several of its statements are now false.** In particular it predates AC-075/080/084 reaching
> Met, predates DEF-026 through DEF-029 entirely, and predates the correction that a STOPPED t3
> earns no CPU credits. Stamped after a stale handoff twice sent work to be redone in one session.

# Resume prompt — PH-5 / SL-025 (historical)

> Rewritten 2026-08-07 after the DEF-023 verification session. Everything below is also in the
> package (PE-176/PE-177, DEF-022→025, AV-100/101/102).

---

Resume PH-5 on ACMP. **UAT is live and login works.** Four defects were closed; two acceptance
criteria remain.

## Orient before touching anything

1. `server_info()`, `package_open("tamheed-package")`, `gate_run()` — expect 7/7, ready.
2. Read **PE-176** (this session) and **PE-173/174/175** (the two before). They hold the reasoning;
   do not re-derive it. Defects **DEF-022 → DEF-025** are all **Fixed**.
3. `git log --oneline -5`; `gh pr list`. **Close the package** — it holds a single-writer lock.

## State

- **UAT**: `https://uat.acmp.anas7ammo.dev`, instance **`i-05085d458d886dc08`**, Elastic IP
  `35.173.149.191`, at commit `02c1ce7`. **STOPPED** — start it before browser work (~40s), stop it
  when idle (~$7.65/mo vs ~$38/mo). `export AWS_PROFILE=acmp-admin` on every AWS call; never root.
- **Login works.** All four seeded accounts complete a real browser PKCE login and JIT-provision with
  the correct role. Re-prove it any time:
  `node src/Acmp.Web/uat-login-probe.mjs https://uat.acmp.anas7ammo.dev`.
  Passwords are rotated off the seed value — the probe tries the temp one, then `Uat_Acmp#2026_Rotated`.
- Acceptance: **AC-077/078/079/080/081/082/083/086 Met** · **AC-075/084 Partial** · **AC-076 Pending**.
- `/acmp/uat/env` in SSM survives termination. It is pinned to `02c1ce7` images.

## What is left

### 1. AC-075 → Met — one clean from-scratch build, zero interventions

The provisioning **scripts** already pass from scratch: `07-launch.sh` and `08-bootstrap-box.sh` both
ran clean and unattended this session, 9/9 assertions. What kept it Partial was runbook §6: certbot
refused a deploy hook committed `100644` and abandoned the whole `certonly` run (DEF-024). **Both
causes are now fixed in source** — the file mode, and the `--non-interactive --agree-tos` flags the
step cannot run without over SSM. Nothing known stands in the way of a first-attempt pass.

```bash
# terminate the current instance FIRST — never two (2 × t3.medium ≈ $76/mo)
bash deploy/aws/07-launch.sh uat
bash deploy/aws/08-bootstrap-box.sh uat <sha-with-published-images>
# then §6 certbot + §7 seed-users, verbatim from deploy/runbooks/cloud-provisioning.md
```

**If it needs any intervention, that is the finding** — record it, don't patch and claim a pass.

### 2. AC-076 — the Playwright suite against UAT (SL-027)

`uat-login-probe.mjs` is **not** that suite. AC-076 wants the real e2e specs run against the deployed
environment.

### 3. AC-084 — Partial. The e2e run on t3.medium.

## Gotchas — do not rediscover these

- **`08-bootstrap-box.sh` refuses on image-tag drift.** Re-pin `ACMP_IMAGE_TAG` / `ACMP_WEB_TAG` in
  `/acmp/uat/env` before bootstrapping — CI writes the **full 40-char sha**, and `web` carries `-uat`.
  `ACMP_ALLOW_TAG_DRIFT=1` only for a deliberate rollback.
- **Never hand-edit files on the box.** A `chmod` there left the checkout dirty and the next
  bootstrap refused to `git checkout` over it. Rebuild instead of editing.
- **The local boot gate cannot verify a browser login.** The published `web` image bakes
  `VITE_OIDC_AUTHORITY` per environment (ADR-0037), so a `-uat` bundle drives the deployed box
  wherever it runs. What it *can* prove — and now asserts — is the authorization round-trip and the
  `/kc/` header block.
- **MSYS mangles leading-slash AWS args.** `07/08/09` set `MSYS_NO_PATHCONV=1` in-script;
  `05-route53.sh` needs conversion **on** with `cygpath -m`. Never set it shell-wide.
- **Reading SSM output on Windows** needs `PYTHONIOENCODING=utf-8 PYTHONUTF8=1`.
- **`aws ssm send-command`** needs real JSON via `file://`, never `commands=[...]` shorthand. And it
  has **no `set -e`** — the invocation status is the *last* command's, which is how DEF-024 reported
  Success while issuing no certificate. Assert on the result, never on the exit code.
- **SSM caps stdout at 24k chars** — use `--quiet-pull`.
- **The Playwright MCP server is unusable.** Use the repo's own Playwright in `src/Acmp.Web`.
- **`git push` hanging?** `gh auth setup-git`, once. Don't ask the operator to push.
- **Before `cloud-stack-boot.sh`**: Docker running, and stop the local `acmp` dev stack. Its exit trap
  restores `deploy/secrets`, so a `KEEP=1` stack is left with mismatched secrets afterwards.

## Cost — binding

Budget **$60/mo**. Never two instances. Never a NAT gateway (~$32/mo). Do not launch prod until UAT is
proven. On any new instance verify `CpuCredits=standard`, 50 GB gp3, zero NAT gateways.

## Do not

- Do not re-litigate settled decisions (Elastic IP, widened Route 53 grant, `promoted_to`).
- Do not hand-edit `tamheed-package/` — the MCP tools are the only write path.
- **Do not mark an AC Met on a check that cannot fail.** DEF-023, DEF-024 and DEF-025 all passed every
  automated signal this project had. Every one was caught by looking at a *result* instead of an exit
  code.
