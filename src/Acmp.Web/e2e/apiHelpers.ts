import { type APIRequestContext, type Page } from '@playwright/test';
import { loginAs } from './login';
import { type E2eRole } from './users';
import { apiMembers, type ApiMember } from './scenario';

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

/**
 * Log a role in via the real PKCE round-trip, capture its bearer, force member provisioning (POST
 * /members/me is idempotent — the same guard dnd-and-failures.spec uses against the async login-time
 * provision), and return the provisioned member row for that ACMP role. The shared P17b session seam.
 */
export async function roleSession(
  page: Page,
  role: E2eRole,
  acmpRole: string,
): Promise<{ bearer: string; member: ApiMember }> {
  await loginAs(page, role);
  const bearer = await captureBearer(page);
  const member = await meMember(page, bearer);
  if (member.role !== acmpRole)
    throw new Error(`[e2e] logged in as ${role} but the provisioned member is ${member.role}, not ${acmpRole}`);
  return { bearer, member };
}

/**
 * The member row belonging to THE CALLER OF THIS BEARER, matched on publicId.
 *
 * This used to be `.find((m) => m.role === acmpRole)` — a lookup keyed on a property that is NOT
 * UNIQUE — and it is why five specs failed against UAT while passing in CI. CI builds a fresh
 * database every run, so exactly one Chairman row exists and picking "the Chairman" is
 * unambiguous. A long-lived environment is different: DEF-029 means deleting a Keycloak user
 * STRANDS its member row forever, and KeycloakUserId is uniquely indexed, so a re-created fixture
 * user necessarily gets a SECOND row. Measured on UAT: E2E Chairman, Member and Secretary each
 * held two rows with two distinct subs. `.find` then returned whichever came first, so a spec
 * rostered one identity while its bearer carried the other — surfacing as a 409 whose body is
 * indistinguishable from "you have already voted", and as reminders delivered to the orphan.
 *
 * POST /members/me is the authority: it provisions from the token's own claims and returns THAT
 * row, so its publicId is the only identifier guaranteed to belong to this bearer. It stays
 * idempotent, so it also keeps the pre-existing guard against racing the SPA's async login-time
 * provision.
 */
export async function meMember(page: Page, bearer: string): Promise<ApiMember> {
  const res = await page.request.post('/api/members/me', { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] provision self ${res.status()} ${await res.text()}`);
  const { publicId } = (await res.json()) as { publicId: string };

  const member = (await apiMembers(page.request, bearer)).find((m) => m.publicId === publicId);
  if (!member) throw new Error(`[e2e] no member row for the caller (publicId ${publicId}) in GET /members`);
  return member;
}
