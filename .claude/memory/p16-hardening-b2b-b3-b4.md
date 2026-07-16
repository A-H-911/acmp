---
name: p16-hardening-b2b-b3-b4
description: "P16 B2b+B3+B4 security hardening — one big PR, branch feat/P16-hardening-b2b-b3-b4, IN PROGRESS"
metadata: 
  node_type: memory
  type: project
  originSessionId: 4b0b3dcf-1a1a-4574-9bdf-3d8b343cdb8b
---

**P16 B2b+B3+B4 = one big PR** (operator chose this + full-scope, no trims). Branch `feat/P16-hardening-b2b-b3-b4` off `main` (`11c6372`). Devil's-advocate-revised plan: `~/.claude/plans/start-b2b-b3-and-hidden-crown.md`. Follows the merged Batch-2 gates ([[p16b-ci-security-gates]]).

**Locked decisions (operator):** (1) CSP — **drop `style-src 'unsafe-inline'`, full refactor** incl. a @dnd-kit CSSOM shim (they chose this over keeping it, eyes-open on the coverage-gate risk); (2) magic-byte — **Mime-Detective NuGet** (25.8.1); (3) containers — **full read-only-FS**; (4) everything one PR.

**DONE + committed (all tested green, whole solution builds):**
- `3e00b69` **B2b+B4 containers:** digest-pin 5 Dockerfile bases + 4 e2e-pulled compose images (minio/postgres/keycloak×2; **ngrok NOT pinned** — behind `profiles:[ngrok]`, unvalidated); backend api+worker → non-root `USER app` (UID 1654); web → `nginxinc/nginx-unprivileged` (UID 101, **listens 8080**, compose `8088:8080`+healthcheck); sqlserver `--no-install-recommends`; report-only `trivy-image` job; **trivy-fs gate CRITICAL→CRITICAL,HIGH; D-21 CLOSED**. (`trivy config deploy/` = 0 misconfig.)
- `0c14c65` **B4 rate-limit + DataProtection:** `AddAcmpRateLimiting` (fixed-window, partition by **`sub`** not IP → no ForwardedHeaders needed; webhook = global bucket; 429+Retry-After) applied via `.RequireRateLimiting` on search/2 uploads/webhook. `AddAcmpDataProtection` (persist to `DataProtection:KeysPath` for read-only-FS). Both in `Acmp.Api/Infrastructure/HardeningExtensions.cs`. **Security headers stay nginx-only** (server-level `add_header` is inherited by `/api/` → API dup would double them).
- `a7c08b6` **B4 magic-byte sniff (C-FILE-01):** `IFileContentInspector`/`MimeFileContentInspector` (Acmp.Shared). Mime-Detective for pdf/png/jpeg/docx; **direct magic for video** (mp4/quicktime=`ftyp`@offset4, webm=EBML) — MD's default set doesn't cover them; svg/json=structural head-check. Bounded head-read + position restore. Wired into UploadRecording + AttachFileToTopic (fail-closed pre-store); topic-attach key now **server-derived** (guid+ext, no raw filename).
- `ddc85af` **B4 PII redaction (C-PRIV-01/02):** `SensitiveDataMaskingEnricher` (Acmp.Shared, Serilog) masks sensitive property NAMES (email/token/secret/signed-url/connstring→`***`); wired into both hosts.

**REMAINING (mostly ENV-DEPENDENT → next session):**
1. **read-only-FS layer (needs destructive e2e loop):** compose `read_only:true`+`tmpfs`(/tmp; web /var/cache/nginx,/run,conf.d,envsubst-dir)+`cap_drop:[ALL]`+`security_opt:[no-new-privileges]` on **app containers (api/worker/web)** only (C-CON-003 = app containers; sidecars get no-new-privileges only); web `proxy_request_buffering off` on `/api/` (2 GB uploads vs read-only nginx, R3); compose env `DataProtection__KeysPath=/keys`+tmpfs. **Validate via isolated `-p acmpe2e`** (dev stack was up 12h — must `stop` it first; `e2e:up` is destructive, [[e2e-local-run-nondestructive]]).
2. **B3 nginx headers:** HSTS + Permissions-Policy **server-level** (inherited; don't add per-location); CSRF SameSite note.
3. **B3 CSP frontend refactor (R1, highest risk):** ~25 `style={{}}` files — 18 static→CSS classes, **7 dynamic→CSSOM** `useCssVars` ref hook (CSSOM `el.style.setProperty` is NOT CSP-governed, MDN-confirmed); **`SortableList.tsx:44` @dnd-kit transform → CSSOM shim** (build FIRST; jsdom can't drive drag → keep `/* v8 ignore */`, cover via Playwright drag E2E). THEN nginx CSP `style-src 'self'`. **Mandatory live-VR** (`-p acmpe2e`, EN-light+AR-dark, **zero CSP violations**, drag works).
4. **B3 crypto scaffold+doc (C-CRYPTO, pure doc):** connection `Encrypt` is already env-driven (SharedKernelExtensions does NOT override it) → operator flips `Encrypt=True`+cert (transit, OQ-024 no mTLS); TDE edition-gated (OQ-040)/SSE/backup = operator. Existing AES-GCM `WebexTokenProtector` covers the one persisted secret. Status → **Partial (Operator/P18)**.
5. **B4 Seq alert runbook (C-INS-01, doc):** signals off structured props (bulk export, integrity-verifier tamper, 401/403 bursts, 429). OQ-025 audit+alert no-dual-control.
6. **Tracking:** flip `security-controls-audit.md` (C-CON-001/002, C-API-03, C-FILE-01, C-PRIV-01/02 → Met; C-CRYPTO→Partial; C-CON-003/C-WEB-01/02/C-INS-01 pending); progress-log, status-report, acceptance-audit (**no AC change**), memory. `validate_package.py docs`.
7. **Final validation:** full `dotnet test` + `check-coverage.mjs` ≥95% (watch MimeFileContentInspector/HardeningExtensions/UploadRecording/AttachFileToTopic), `dotnet format --verify-no-changes`, trivy, e2e green. Then **draft PR** (incomplete until env-work done); **merge needs explicit "merge without review" consent**.

**Facts:** local trivy fs needs `MSYS_NO_PATHCONV=1` (Git-Bash mangles `-v "$PWD:/repo"`); trivy `config` uses `-q` not `--no-progress`. `dotnet format --include <many paths>` mis-parses → format per-project. No new ADR unless read-only-FS forces one. No AC verdict change. Cost this session was very high (~$56+) — resume fresh.
