---
name: p16-hardening-b2b-b3-b4
description: "P16 B2b+B3+B4 security hardening — one big PR, branch feat/P16-hardening-b2b-b3-b4, IN PROGRESS (code done; e2e/live-VR + PR remain)"
metadata: 
  node_type: memory
  type: project
  originSessionId: 4b0b3dcf-1a1a-4574-9bdf-3d8b343cdb8b
---

**P16 B2b+B3+B4 = one big PR** (operator chose this + full-scope). Branch `feat/P16-hardening-b2b-b3-b4` off `main` (`11c6372`). Plan: `~/.claude/plans/start-b2b-b3-and-hidden-crown.md`. Follows [[p16b-ci-security-gates]].

## ★ The big finding — the CSP frontend refactor was NEVER NEEDED (operator agreed, skipped)

The locked "drop `style-src 'unsafe-inline'` via a FULL ~110-site refactor + @dnd-kit CSSOM shim" rested on a **false premise**. **CSP governs `<style>` elements and `style=""` attributes — NOT the CSSOM.** react-dom applies the `style={{}}` prop via **CSSOM writes** (`style.setProperty(name,v)` / `style[name]=v` — read directly in `node_modules/react-dom/.../setValueForStyle`), never `setAttribute('style')`. Only **SSR** serializes a style attribute, and this is a client-rendered Vite SPA. Also: `index.html` has no inline style; `MarkdownView`'s DOMPurify allowlist is `['href','title','class']` + tag list excludes `<style>`; **no CSS-in-JS dep**.

**Browser-verified** under `style-src 'self'` (scratch HTML + meta CSP, served over http): CSSOM `style[name]=v`, `setProperty('--x')`, and the **@dnd-kit `translate3d` transform shape** all **APPLY**; `setAttribute('style')` and an injected `<style>` are **BLOCKED**. ⇒ All 110 `style={{}}` sites incl. `SortableList.tsx:44` are already CSP-clean. **Risk R1 (shim vs 95% coverage gate) is GONE.** Operator chose "skip refactor, log as style hygiene" → **D-22** (explicitly says: do NOT activate for CSP reasons — that rationale is disproven).

> Lesson: verifying a locked decision's *necessity* ≠ re-litigating it. The goal (`style-src 'self'`) shipped; the refactor was only the assumed means.

## Commits on the branch (7)

Pre-existing 4 (do NOT redo): `3e00b69` digest-pin + non-root (D-21), `0c14c65` rate-limit + DataProtection, `a7c08b6` magic-byte sniff, `ddc85af` Serilog PII redaction. This session added:
- `463f2b8` **B3 headers + docs**: nginx **HSTS** (1y+includeSubDomains, no preload) + **Permissions-Policy** + **CSP `style-src 'self'`** (unsafe-inline DROPPED), all **server-level** (inherited; a per-location `add_header` would DROP the set). Crypto scaffold doc → `deployment.md` §3.4 (**Partial/Operator-P18**, never Met). Seq alert runbook → `post-release-operating-model.md` §2.4.
- `59f03cb` **B4 read-only FS**: `read_only`+`cap_drop:[ALL]`+`no-new-privileges` on api/worker/web; sidecars **no-new-privileges only**. Also fixed **ngrok `web:80`→`web:8080`** (latent bug from the nginx-unprivileged switch; profile-gated so e2e never caught it).
- `c84a42b` **Testcontainers 3.10→4.13** (see below).

## ★ Two real bugs my own earlier commits caused — both found by verifying, not by CI

1. **Testcontainers 3.10 cannot parse a digest in a Dockerfile `FROM`** (`MatchImage.Match` → `ArgumentException: Cannot parse image: ...server:2022-latest@sha256:...`). My digest-pin (`3e00b69`) broke `SearchProvidersFtsTests` (2 FAIL at ~0.2s). **tag@digest AND digest-only both fail on 3.10**; 4.x parses both (optional tag+digest regex groups). Fixed by upgrading **`Testcontainers.MsSql`/`.Minio` 3.10.0→4.13.0** (one test csproj, no prod code) — keeps the C-SUP-01 pin. Also had to pass container images **explicitly** (`MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")`, `MinioBuilder("minio/minio:RELEASE.2023-01-31T02-24-19Z")`) — clears 3 CS0618 obsolete-ctor warnings AND stops a TC bump silently moving the test images.
2. **web tmpfs `/etc/nginx/conf.d` MUST carry `uid=101,gid=101,mode=0755`.** A bare tmpfs **inherits the image dir's root:root 0755**, so nginx (UID 101) can't write its rendered config → `envsubst: conf.d is not writable` → and since the mount also hides the stock `default.conf`, nginx starts with **NO server block**: logs "ready for start up", forks workers, reports `running`, **listens on nothing**. (tmpfs on a path that does NOT exist in the image gets 1777 — that's why `/tmp` and `/keys` were fine.)

## ★ R3 was under-diagnosed by the plan — TWO buffering layers, not one

`proxy_request_buffering off` fixes only **nginx**. **ASP.NET `IFormFile` independently spools any multipart section >64 KB to `Path.GetTempPath()`.** Recording cap = **2 GiB** (`MeetingsEndpoints.RecordingUploadMaxBytes`) vs api RAM budget **512 MB–1 GB** (`deployment.md` §9), and **tmpfs counts against the container memory cgroup** ⇒ a RAM-backed `/tmp` OOMs on the first real upload. ⇒ **api `/tmp` = disk-backed named volume `api-tmp`** (the C-CON-003 "writable only where needed" exception). worker/web `/tmp` = tmpfs (spool nothing).

## Gates — all run locally, GREEN (2026-07-16)

full `dotnet test` **1451 pass / 0 fail**; coverage **416 files, 99.65%** (≥95%); `dotnet format --verify-no-changes` exit 0 (**NOTE: `--nologo` is NOT a valid dotnet-format option** — it prints help + exit 1 and looks like a failure); `check-vulns.mjs` 0 High/Critical; `trivy config deploy/` **0 misconfig**; `trivy fs` CRITICAL,HIGH exit 0 (**needs `--timeout 20m` locally** — default deadline exceeds on the dirty tree).

**Keystone validator = `NOT READY` (G-IDS, 74 findings) — PRE-EXISTING, verified identical on `main` (`git archive main docs`).** Not caused by this branch; none of the findings touch my files. Report, don't "fix".

## ★ STATE: PR #141 OPEN — ALL 9 CHECKS GREEN, `mergeStateStatus=CLEAN` — **AWAITING MERGE CONSENT (do NOT self-merge)**

Live validation DONE (isolated `-p acmpe2e`, dev stack stopped then **restored healthy**, dev volumes intact):
read-only stack boots healthy across every container (runtime-verified `ReadonlyRootfs`/`CapDrop[ALL]`/non-root) ·
**e2e 40/40** (incl. VR sweep EN-light+AR-dark + drag specs) · **CSP: 26 screens → 0 violations** + a real kanban drag
**engaged** (accept dialog opened ⇒ not vacuous) → 0 violations · rate limit **140 posts → exactly 120×200 + 20×429**,
`Retry-After: 60` · **300 MB** recording upload → **HTTP 200** through read-only nginx (64m tmpfs) + read-only api ·
PNG spoofed as `video/mp4` → **400** · Seq **500 real `Acmp.Api` events → 0 unmasked** PII. Pixel-VR dropped (nothing
refactored ⇒ no visual delta). Tracking + progress-log + status-report (v1.9.0) + D-22 all committed.

**★ @dnd-kit is TREE-SHAKEN OUT of the production bundle** (`SortableList` imported only by its own test; the real
kanban/agenda drags are **native HTML5 DnD**, not @dnd-kit) — so plan risk R1 was moot **three times over**.

**⚠ 3 gotchas that cost CI cycles / near-misses:**
1. **`dotnet format` MUST be solution-wide.** I ran it scoped to one .csproj → exit 0, while CI's `acmp.sln` run
   → **exit 2, 7 `error CHARSET`** (Write-tool files lack the required UTF-8 BOM, from the EARLIER commits). Fixed
   by `dotnet format acmp.sln` (BOM only, 7 files, 1 line each). See [[ci-gates-run-locally-pre-push]].
2. **Never run a load test concurrently with the e2e suite.** My 140-post 429 burst ran during the suite → 1 spurious
   `audit-vr` failure (every audited write takes the `acmp-audit-chain` applock, ADR-0028, so the burst serialized
   against the spec's seeding). Clean re-run = 40/40.
3. **Restoring the dev stack needs `--build web`.** The dev `acmp-web` image predated this branch's
   nginx-unprivileged switch → stock nginx runs as **root**, and `cap_drop:[ALL]` removes **CAP_DAC_OVERRIDE**, so
   root could NOT write the uid-101 tmpfs → `Permission denied`, web `Exited(1)`. Rebuild with `--env-file deploy/.env`
   (the memory's `--build` trap is only about using `.env.example` against the `acmp` volumes).

**Next:** operator merges #141 (squash) → P16 complete bar its stated residuals (OQ-027 ZAP Deferred; C-CRYPTO
Partial/Operator-P18; trivy-image report-only; ClamAV opt-in) → then **P14** Tarseem/Diagrams.

**Facts:** trivy local needs `MSYS_NO_PATHCONV=1` (Git-Bash mangles `-v "$PWD:/repo"` AND `docker exec <abs-path>`); trivy `config` uses `-q`. `dotnet format --include <many paths>` mis-parses → format per-project(.csproj). **NEVER `npm run e2e:up`** (no `-p` → hits the `acmp` project → wipes dev volumes) — see [[e2e-local-run-nondestructive]]. No new ADR. No AC verdict change.
