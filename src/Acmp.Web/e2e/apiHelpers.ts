import { type APIRequestContext, type Page } from '@playwright/test';

/*
 * S6b (ADR-0016 §2) E2E helpers for the two steps the UI alone can't drive on a fresh stack.
 *
 * Why these exist: the live stack boots with an EMPTY database (no seeder). Members appear
 * only after a real login self-provisions them; and a topic reaches `Prepared` (the agenda
 * pool source) only via the backend `prepare` endpoint — there is no v1 UI button for it.
 * Everything else in the core loop is driven through the real UI.
 */

/**
 * Capture the SPA's current access token by reading the `Authorization` header off a live
 * `/api` request. With Keycloak direct-grant disabled (the S6a finding), the PKCE browser
 * session is the only token source — so we reuse the header the SPA already sends rather than
 * reverse-engineering oidc-client-ts storage. Must be called on an authenticated page.
 */
export async function captureBearer(page: Page): Promise<string> {
  const [req] = await Promise.all([
    page.waitForRequest((r) => r.url().includes('/api/') && !!r.headers()['authorization'], { timeout: 20_000 }),
    page.goto('/backlog'),
  ]);
  const auth = req.headers()['authorization'];
  if (!auth) throw new Error('[e2e] no Authorization header captured from an /api request');
  return auth;
}

/**
 * Mark an Accepted topic Prepared via the API — the one core-loop step with no v1 UI control.
 * The agenda pool reads `?status=Prepared`, but Topic→Prepared is API-only; the secretary's own
 * bearer satisfies the handler's ABAC (Policies.TopicEdit = Owner or Secretary/Chairman).
 */
export async function prepareTopic(request: APIRequestContext, bearer: string, topicId: string): Promise<void> {
  const res = await request.post(`/api/topics/${topicId}/prepare`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] prepare ${topicId} failed: ${res.status()} ${await res.text()}`);
}
