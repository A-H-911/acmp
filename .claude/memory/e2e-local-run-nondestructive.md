---
name: e2e-local-run-nondestructive
description: How to run the Playwright e2e locally without wiping the operator's dev stack — the dev stack and the e2e are architecturally incompatible (ngrok vs local OIDC issuer).
metadata:
  node_type: memory
  type: project
---

The Playwright e2e (`src/Acmp.Web/e2e/*.spec.ts`, ADR-0016 §2) can only run against a **fresh `.env.example` stack** (local KC authority `http://keycloak.localhost:8085/realms/acmp`), **NOT the operator's running dev stack**. The dev stack bakes `KEYCLOAK_AUTHORITY` = the **ngrok URL** (`deploy/.env`, needed for Webex webhooks), and web + API + Keycloak must all agree on the issuer byte-for-byte — so an e2e login against the dev stack redirects to `https://acmp.ngrok.dev/kc/...` and **times out at `login.ts`**. CI runs the e2e on a throwaway stack (the `e2e.yml` job does `up --build` then `down -v` each run; it only triggers on PRs to `main` + `workflow_dispatch`, ~6 min).

**Run it locally NON-DESTRUCTIVELY (operator's `acmp` volumes untouched):**
1. `docker compose -f deploy/docker-compose.yml stop` — stop the dev stack (frees ports 8088/8085/1433/9000/etc., **keeps volumes**).
2. `docker compose -p acmpe2e -f deploy/docker-compose.yml --env-file deploy/.env.example up -d --build --wait` — isolated project = **separate volumes**, local KC authority baked into the `acmpe2e-web` image (the `web` service takes `VITE_OIDC_AUTHORITY` from `${KEYCLOAK_AUTHORITY}` as a **build arg**, so `.env.example` must be present at build).
3. `cd src/Acmp.Web && set -a && source ../../deploy/.env.example && set +a && npm run e2e -- <spec.ts>` — global-setup seeds the per-role KC users + waits for health; playwright baseURL defaults to `http://localhost:8088`. Chromium resolves `keycloak.localhost` to loopback.
4. `docker compose -p acmpe2e -f deploy/docker-compose.yml --env-file deploy/.env.example down -v` — wipes **only** the `acmpe2e` volumes.
5. `docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --wait` — restart the dev stack on the **real** env (reuses the `acmp-*` images, which already carry your code if you rebuilt them against `deploy/.env`).

**⚠ The `npm run e2e:up` / `e2e:down` scripts are DESTRUCTIVE as-written — do NOT use them while the dev stack matters.** They run `docker compose -f ../../deploy/docker-compose.yml --env-file .env.example up -d --build --wait` (and `down -v`) with **no `-p`**. Because `deploy/docker-compose.yml` hardcodes **`name: acmp`** (line 1), those scripts resolve to the **same `acmp` project + volumes** as the dev stack → `up --build --env-file .env.example` is exactly the password trap below (SQL/KC `.env.example` passwords vs `acmp_*` volumes initialized with `deploy/.env` → unhealthy), and `e2e:down -v` would **wipe the dev volumes**. Always run the isolated procedure above with an explicit **`-p acmpe2e`** instead (the CLI `-p` overrides the compose `name:`). Confirmed 2026-07-13 (P15b VR). The KC admin token for global-setup needs `.env.example`'s password — export `KC_BOOTSTRAP_ADMIN_PASSWORD=ChangeMe_KC#2026` (or `source deploy/.env.example`) for the playwright run.

**⚠ Persisted-volume password trap (bit twice in the D-15 session):** NEVER run `up --build --env-file deploy/.env.example` against the dev `acmp` project. It recreates SQL Server + Keycloak with `.env.example` passwords that **don't match** the volumes (initialized with `deploy/.env`) → `Login failed for user 'sa'` / container **unhealthy**. The volume owns the password (fixed at first init); the env-file only matters at init. Restore by recreating with `--env-file deploy/.env`. (Same class as the KC-bootstrap note in [[p10f-risks-deps-traceability-plan]].)

Local gates do NOT include Playwright — e2e is CI-only. See [[ci-gates-run-locally-pre-push]] (the unit/coverage/format/i18n/build gates) and [[web-visual-verify-cache-busting]] (stale-bundle-after-rebuild). Proven in the D-15 slice ([[topic-prepare-ui-gap-d15]]) where the swapped `core-loop.spec` was validated this way.
