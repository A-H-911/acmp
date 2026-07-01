---
name: web-visual-verify-cache-busting
description: "ACMP visual verify: after web edits the live :8088 can serve a stale bundle — force-recreate the container AND cache-bust the browser, or you verify old code."
metadata: 
  node_type: memory
  type: project
  originSessionId: 687c0d29-6db3-4063-b6e2-b604868200a6
---

When visually verifying ACMP web changes on the live stack (:8088), the handoff's
`docker compose build web && docker compose up -d web` is **not enough** — twice in one session
the browser rendered the OLD UI after a rebuild. Two layers cache:

1. **Container/image**: `up -d web` may not recreate. Use
   `docker compose build --no-cache web && docker compose up -d --force-recreate web` when in doubt.
2. **Browser bundle**: even with the new image served, the Playwright browser keeps the old hashed
   `assets/index-*.js` from cache. The SPA `index.html` is cached too.

**How to apply (reliable recipe):** after rebuild, in Playwright: clear caches
(`await caches.keys()` → delete) and navigate to the route **with a cache-bust query**
(`/meetings?cb=1`). Then CONFIRM the live bundle is fresh before trusting the screenshot —
compare the loaded `document.scripts[].src` hash against `fetch('/index.html',{cache:'no-store'})`'s
`assets/index-*.js` ref; they must match. Only then run the computed-px gate / screenshots.

**Why:** the hash also changes because `VITE_OIDC_AUTHORITY` is baked at build time, so a hash
differing from a local `npm run build` does NOT prove staleness — the DOM/anatomy is the real check.
See [[exact-design-fidelity-visual-loop]].
