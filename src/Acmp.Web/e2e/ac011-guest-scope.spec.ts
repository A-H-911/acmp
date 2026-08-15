import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { roleSession, captureBearer } from './apiHelpers';
import { loginWithTemporaryPassword } from './login';
import { apiPreparedTopic, apiScheduleMeeting, apiAddAgendaItem, type ApiMember, type ApiMeeting, type ApiTopic } from './scenario';

/*
 * AC-011 — THE LIVE LEG. "A Guest/Presenter assigned as Presenter on a topic for a meeting, attempting
 * to access another topic or any action outside that meeting scope, gets HTTP 403."
 *
 * WHY IT COULD NOT BE WRITTEN BEFORE TODAY, and it is one fact rather than an oversight: a guest
 * presenter can only be created through IGuestProvisioner, which MembershipInfrastructureExtensions
 * registers ONLY when KeycloakAdmin:Enabled is true — and deploy/.env.example sets it false, so CI ran
 * the full seven-service stack with a genuine Keycloak and never constructed the port at all. AV-011
 * recorded the gap as "presenter meeting-window runtime enforcement" and topic-scope.spec.ts says
 * plainly "AC-011 needs a Guest presenter and is deferred with it". DEC-042 turned the flag on in CI;
 * this is the evidence that was waiting for it.
 *
 * ⚠ EVERY PRINCIPAL HERE IS REAL. The guest is created BY THE APPLICATION through the Secretary's own
 * endpoint and signs in through the genuine PKCE round-trip with the temporary password the invite
 * returned. Seeding a Keycloak user with a Guest realm role would have been far easier and would have
 * proven something else — that a role claim is refused — rather than that the presenter RELATIONSHIP
 * is what bounds this person.
 */

const STAMP = Date.now();
const GUEST_EMAIL = `e2e-guest-${STAMP}@acmp.test`;
const GUEST_NAME = `E2E Guest ${STAMP}`;
/** The temporary password is revealed once and stored nowhere (AC-088), so the guest picks a new one. */
const GUEST_NEW_PASSWORD = 'E2e!Guest#Passw0rd';

let secretary: { bearer: string; member: ApiMember };
let api: APIRequestContext;

/** The meeting the guest presents at, and the topic they present. */
let theirMeeting: ApiMeeting;
let theirTopic: ApiTopic;
/** A meeting and a topic the guest has no relationship with whatsoever. */
let otherMeeting: ApiMeeting;
let otherTopic: ApiTopic;

let guestPage: Page;
let guestBearer: string;
let accessExpiresAt: string;

test.beforeAll(async ({ browser }) => {
  // TWO full PKCE round-trips (the Secretary's, then the guest's — the second including Keycloak's
  // forced password change) plus seven API calls, against a real seven-service stack. The 60s default
  // is a per-TEST budget and this setup honestly exceeds it.
  //
  // ⚠ RAISED ONLY AFTER THE REAL CAUSE OF THE FIRST TIMEOUT WAS FOUND AND FIXED — a submit selector
  // that matched nothing and waited out the clock. Raising this first would have bought a slower
  // failure in the same place and read as "the login is slow", which it was not.
  test.setTimeout(180_000);

  const secretaryCtx = await browser.newContext();
  secretary = await roleSession(await secretaryCtx.newPage(), 'secretary', 'Secretary');
  api = secretaryCtx.request;

  theirTopic = await apiPreparedTopic(api, secretary.bearer, `AC-011 presented ${STAMP}`, secretary.member);
  otherTopic = await apiPreparedTopic(api, secretary.bearer, `AC-011 unrelated ${STAMP}`, secretary.member);

  theirMeeting = await apiScheduleMeeting(api, secretary.bearer, `AC-011 their meeting ${STAMP}`, secretary.member);
  await apiAddAgendaItem(api, secretary.bearer, theirMeeting.id, theirTopic, secretary.member);

  // The out-of-scope meeting carries an agenda item of its own, so the two meetings differ ONLY in who
  // presents. A bare meeting would let "there was nothing to see" pass as scoping.
  otherMeeting = await apiScheduleMeeting(api, secretary.bearer, `AC-011 other meeting ${STAMP}`, secretary.member);
  await apiAddAgendaItem(api, secretary.bearer, otherMeeting.id, otherTopic, secretary.member);

  // ⚠ FIRST EXECUTION OF THE ADR-0040 GUEST INVITE IN E2E, EVER. If this fails, that is a finding
  // about the product — the write path has never run anywhere except the manual UAT probe.
  const invite = await api.post(`/api/meetings/${theirMeeting.id}/guest-presenters`, {
    headers: { Authorization: secretary.bearer, 'Content-Type': 'application/json' },
    data: { topicId: theirTopic.id, email: GUEST_EMAIL, fullName: GUEST_NAME },
  });
  if (!invite.ok()) throw new Error(`[e2e] invite guest presenter ${invite.status()} ${await invite.text()}`);
  const invited = (await invite.json()) as { temporaryPassword: string; accessExpiresAt: string };
  accessExpiresAt = invited.accessExpiresAt;

  const guestCtx = await browser.newContext();
  guestPage = await guestCtx.newPage();
  await loginWithTemporaryPassword(guestPage, GUEST_EMAIL, invited.temporaryPassword, GUEST_NEW_PASSWORD);
  // /session and not /backlog: a guest is refused every topic route, so the token has to be captured
  // off a page they can actually load.
  guestBearer = await captureBearer(guestPage, '/session');
});

const asGuest = (url: string) => guestPage.request.get(url, { headers: { Authorization: guestBearer } });

/*
 * ASSERTED FIRST AND ON PURPOSE — the AC-010 lesson, and it matters more here than usual. A guest is
 * refused almost everything, so if the invite silently produced a person with no slot, EVERY refusal
 * below would still pass while proving only that the fixture was broken.
 */
test('AC-011 — the guest presenter reaches their own session, scoped to the slot they present', async () => {
  const res = await asGuest('/api/session/me');
  expect(res.status(), 'the guest must be able to load their own session').toBe(200);

  const session = (await res.json()) as { meetingKey: string; topicKey: string; accessExpiresAt: string };
  expect(session.meetingKey, 'the session is the meeting they present at').toBe(theirMeeting.key);
  expect(session.topicKey, 'and the topic they present').toBe(theirTopic.key);

  // The access WINDOW, the second half of "scoped only to the assigned topic and meeting window".
  // The same stored column the per-request refusal (ADR-0039) and the hourly expiry sweep both read,
  // so asserting it here is asserting the value those controls will act on.
  expect(session.accessExpiresAt, 'the guest carries the window the invite granted').toBe(accessExpiresAt);
});

test('AC-011 — the guest presenter is refused a topic they do not present', async () => {
  const refused = await asGuest(`/api/topics/${otherTopic.key}`);
  expect(refused.status(), 'a topic outside the guest session must be refused').toBe(403);
  expect(refused.headers()['x-acmp-auth-reason'], 'and refused AS a guest-scope decision').toBe('guest_scope');
});

/*
 * ⚠ THE ASSIGNED TOPIC IS REFUSED TOO, AND THAT IS THE DESIGN RATHER THAN A BUG. A guest reaches their
 * material through /session, which re-derives the topic from their own slot; /api/topics is not part of
 * the guest surface at all. Asserted so that nobody "fixes" the refusal above by opening the topic
 * routes to guests — which would pass that test and silently widen the surface ADR-0040 exists to keep
 * narrow.
 */
test('AC-011 — even the presented topic is not reachable through the committee topic routes', async () => {
  const refused = await asGuest(`/api/topics/${theirTopic.key}`);
  expect(refused.status(), 'the guest surface is /session, never /api/topics').toBe(403);
});

/*
 * DEF-073 — "or any action outside that meeting scope". Reading somebody else's meeting is exactly
 * that, and until this change it answered 200: GuestSurfaceMiddleware admits the /api/meetings PREFIX
 * because the design's role matrix grants Guest "agenda (view)", and a path gate cannot tell one
 * meeting from another. The row-level half now lives in GetMeetingDetail.
 */
test('AC-011 / DEF-073 — the guest presenter is refused a meeting they do not present at', async () => {
  const refused = await asGuest(`/api/meetings/${otherMeeting.key}`);
  expect(refused.status(), 'a meeting outside the guest session must be refused').toBe(403);
});

test('AC-011 / DEF-073 — the guest presenter may read the meeting they DO present at', async () => {
  // The control for the refusal above: same caller, same route shape, only the meeting differs. That is
  // what makes it scope rather than "guests cannot read meetings".
  const allowed = await asGuest(`/api/meetings/${theirMeeting.key}`);
  expect(allowed.status(), 'their own meeting is the one thing this route must still serve').toBe(200);
});

test('AC-011 / DEF-073 — the meetings list shows the guest only their own meeting', async () => {
  const res = await asGuest('/api/meetings');
  expect(res.status()).toBe(200);

  const keys = ((await res.json()) as { key: string }[]).map((m) => m.key);
  // Asserted by KEY and both directions. A count would pass just as happily if the filter kept the
  // wrong meeting, and this run shares a database with every other spec's fixtures.
  expect(keys, 'their own meeting is listed').toContain(theirMeeting.key);
  expect(keys, 'and the one they do not present at is not').not.toContain(otherMeeting.key);
});

/*
 * AC-011's SECOND HALF — "scoped only to the assigned topic AND MEETING WINDOW" (DEC-052 d3).
 *
 * ⚠ THIS EXISTS BECAUSE THE OPERATOR OVERRULED A CHEAPER VERDICT. I proposed recording the window
 * clause as a composition — the value SET, proven by the session assertion above, plus the value
 * ENFORCED, proven by PrincipalRevalidatorTests — with the seam named in the verdict. The operator
 * required the whole clause be a MEASUREMENT instead, and this is it.
 *
 * The trick is that a window cannot be waited out in a test, so the meeting is placed in the PAST:
 * ScheduleMeeting validates only that the end follows the start, so a meeting that ended weeks ago is
 * legitimately creatable, and a guest invited onto it gets AccessExpiresAt = that end + the 24h grace
 * — already elapsed when they first sign in. Same invite path, same login, only the window differs.
 */
test('AC-011 — a guest whose meeting window has already passed is refused, live', async ({ browser }) => {
  test.setTimeout(180_000);

  const endedTopic = await apiPreparedTopic(api, secretary.bearer, `AC-011 ended ${STAMP}`, secretary.member);
  const endedMeeting = await apiScheduleMeeting(
    api, secretary.bearer, `AC-011 ended meeting ${STAMP}`, secretary.member,
    '2026-07-01T09:00:00.000Z', '2026-07-01T10:00:00.000Z');
  await apiAddAgendaItem(api, secretary.bearer, endedMeeting.id, endedTopic, secretary.member);

  const lateEmail = `e2e-guest-late-${STAMP}@acmp.test`;
  const invite = await api.post(`/api/meetings/${endedMeeting.id}/guest-presenters`, {
    headers: { Authorization: secretary.bearer, 'Content-Type': 'application/json' },
    data: { topicId: endedTopic.id, email: lateEmail, fullName: `E2E Late Guest ${STAMP}` },
  });
  if (!invite.ok()) throw new Error(`[e2e] invite late guest ${invite.status()} ${await invite.text()}`);
  const late = (await invite.json()) as { temporaryPassword: string; accessExpiresAt: string };

  // The fixture's own precondition, asserted rather than assumed: if the grace ever changed so that
  // this window were still open, every assertion below would pass for the wrong reason.
  expect(new Date(late.accessExpiresAt).getTime(),
    'the invite must already be expired for this to measure anything').toBeLessThan(Date.now());

  const lateCtx = await browser.newContext();
  const latePage = await lateCtx.newPage();
  await loginWithTemporaryPassword(latePage, lateEmail, late.temporaryPassword, GUEST_NEW_PASSWORD);

  // ⚠ ASSERTED THROUGH A REQUEST THE PAGE MAKES, not through a captured bearer. Keycloak still issues
  // this person a perfectly valid token — it knows nothing about ACMP's window — so the refusal is
  // ACMP's alone, applied per request by the ADR-0039 revalidation middleware.
  const refused = await latePage.request.get('/api/session/me');
  expect(refused.status(), 'an elapsed guest window must refuse at the API, not merely in the banner')
    .toBe(401);
  expect(refused.headers()['x-acmp-auth-reason'], 'and refused AS an expiry, not as a scope decision')
    .toBe('access_expired');

  await lateCtx.close();
});

test('AC-011 — a guest may not WRITE to the meeting they present at', async () => {
  // Scope bounds reads; it must not be read as promoting the guest inside their own meeting. The gate
  // refuses non-GET on /api/meetings regardless of which meeting it is.
  const refused = await guestPage.request.post(`/api/meetings/${theirMeeting.id}/start`, {
    headers: { Authorization: guestBearer },
  });
  expect(refused.status(), 'a guest presents at a meeting; they do not run it').toBe(403);
});
