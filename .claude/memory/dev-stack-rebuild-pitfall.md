---
name: dev-stack-rebuild-pitfall
description: "NEVER `docker compose up --build` the long-lived dev stack to redeploy — it recreates sqlserver and hits the SQL volume-password mismatch → whole stack unhealthy."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: a67f04de-cda2-4d8f-aca8-f9541bc16a18
---

Redeploying the running `acmp` dev stack (`deploy/docker-compose.yml`, project `acmp`) with `docker compose -p acmp up -d --build api web` **broke it** on 2026-07-13: the recreate cascaded to `sqlserver`, which came up **unhealthy** — "Login failed for user 'sa' — Password did not match." SQL Server keeps the **volume's original SA password** and ignores `MSSQL_SA_PASSWORD` on a populated volume; the healthcheck uses the env value (`deploy/.env` = `Acmp_Local#2026`), so once `deploy/.env` diverges from the volume's init password, any recreate fails the healthcheck and `api`/`web` never start (`keycloak` also exited). Same class of pitfall as [[e2e-local-run-nondestructive]].

**Why:** it cost a long recovery detour and required a data-wiping reset (operator OK'd — dev data disposable). Recovery = `down -v` + fresh `up --build --env-file deploy/.env` (fresh volumes init with the current env password → consistent; migrations incl. `Research_Init` re-run). Stack is healthy again on fresh volumes as of 2026-07-13.

**How to apply:** to see FE changes live, prefer the isolated e2e stack ([[e2e-local-run-nondestructive]]), NOT a rebuild of the dev stack. If you MUST redeploy the dev stack, expect the volume-password mismatch and be ready to `down -v` (data loss) — confirm with the operator first. Never `up --build` a long-lived stack casually. Also: the dev stack bakes the **ngrok** OIDC issuer (`KEYCLOAK_AUTHORITY=https://acmp.ngrok.dev/kc/...`), so you **cannot** browser-login at `localhost:8088` locally — live VR must run on the isolated local-authority stack. See [[web-visual-verify-cache-busting]].
