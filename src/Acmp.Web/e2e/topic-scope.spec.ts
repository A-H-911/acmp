import { test, expect, type APIRequestContext } from '@playwright/test';
import { roleSession } from './apiHelpers';
import { apiCreateTopic, type ApiMember, type ApiTopic } from './scenario';

/*
 * AC-009 / AC-033 / AC-034 — THE LIVE LEG. Tranche B.
 *
 * WHY THESE WERE PARTIAL: proven by TopicTests and TopicHandlerTests, i.e. the aggregate and the
 * handler in isolation. Nothing carried ownership, the post-Accept lock, or rejection immutability
 * through a REAL Keycloak token to the HTTP surface, which is what each AC actually says ("the API
 * returns HTTP 403", "the system rejects the mutation").
 *
 * ONE SHARED FIXTURE, REUSED THREE WAYS (the operator's decision, PE-286): three topics built once
 * per run — one owned by the Member, one owned by somebody else, one Rejected. Cheaper than three
 * setups and closer to how a committee's data really looks.
 *
 * ⚠ THE ACCEPTED RISK IS COUPLING: a broken fixture fails all three at once, and shared state is
 * where this suite historically breaks (DEF-045). Mitigated the way ac043-reorder does it — every
 * topic carries a per-run stamp, so nothing here reads a row another run created.
 *
 * ⚠ NOT IN THIS FILE, AND NOT BECAUSE IT WAS HARD. AC-010 (stream scope) is still blocked by
 * DEF-057: StreamScopeRequirement is registered in DI and unit-tested but appears in NO policy,
 * so it is never evaluated and the control fails OPEN. It is wired in ADR-0042 step 7, and the
 * AC-010 leg belongs here at step 8.
 *
 * ⚠ THE SECOND HALF OF THAT NOTE IS NOW STALE AND IS CORRECTED RATHER THAN DELETED, because the
 * reasoning still matters: it used to read "no Stream can be created at all". ADR-0042 step 1
 * seeded the taxonomy, so streams now EXIST — but that changes nothing about the AC yet, since
 * no policy carries the requirement. ⚠ When step 8 lands, AC-010 must be evidenced against a
 * member assigned to a DIFFERENT stream than the topic — never an unassigned one, whose refusal
 * proves "a member with no streams is denied", a different claim from the one AC-010 makes.
 * AC-011 needs a Guest presenter and is deferred with it.
 */

const STAMP = Date.now();
/** AC-010: the stream the Member is assigned to, and one they are not. Both are seeded (ADR-0043 step 1). */
const HELD_STREAM = 'core';
const UNHELD_STREAM = 'government';
const REJECTION_REASON = `Out of committee scope — e2e ${STAMP}`;

let secretary: { bearer: string; member: ApiMember };
let member: { bearer: string; member: ApiMember };
let api: APIRequestContext;

/** Accepted, owner is the Secretary — the Member is a non-owner on an Accepted topic. */
let ownedByOther: ApiTopic;
/** Rejected with a recorded reason. */
let rejected: ApiTopic;
/** AC-010: accepted with the MEMBER as owner, affecting the stream the member holds. */
let inScope: ApiTopic;
/** AC-010: the same, affecting a stream the member does NOT hold. */
let outOfScope: ApiTopic;

async function accept(topic: ApiTopic, owner: ApiMember) {
  const res = await api.post(`/api/topics/${topic.id}/accept`, {
    headers: { Authorization: secretary.bearer, 'Content-Type': 'application/json' },
    data: { ownerId: owner.publicId, ownerName: owner.fullName },
  });
  if (!res.ok()) throw new Error(`[e2e] accept ${res.status()} ${await res.text()}`);
}

/** A well-formed edit. Only the description differs, so a refusal is about WHO, never about WHAT. */
function editBody(topic: ApiTopic, description: string) {
  return {
    topicId: topic.id,
    title: topic.title,
    description,
    justification: 'E2E setup justification.',
    urgency: 'Normal',
    streams: ['core'],
    systems: [],
    tags: [],
  };
}

const edit = (bearer: string, topic: ApiTopic, description: string) =>
  api.put(`/api/topics/${topic.id}`, {
    headers: { Authorization: bearer, 'Content-Type': 'application/json' },
    data: editBody(topic, description),
  });

test.beforeAll(async ({ browser }) => {
  const memberCtx = await browser.newContext();
  member = await roleSession(await memberCtx.newPage(), 'member', 'Member');

  const secretaryCtx = await browser.newContext();
  secretary = await roleSession(await secretaryCtx.newPage(), 'secretary', 'Secretary');
  api = secretaryCtx.request;

  ownedByOther = await apiCreateTopic(api, secretary.bearer, `TrB owned-by-other ${STAMP}`);
  await accept(ownedByOther, secretary.member);

  rejected = await apiCreateTopic(api, secretary.bearer, `TrB rejected ${STAMP}`);
  const res = await api.post(`/api/topics/${rejected.id}/reject`, {
    headers: { Authorization: secretary.bearer, 'Content-Type': 'application/json' },
    data: { reason: REJECTION_REASON },
  });
  if (!res.ok()) throw new Error(`[e2e] reject ${res.status()} ${await res.text()}`);

  // ---- AC-010 fixture (ADR-0043 step 8) ----
  //
  // ⚠ THE MEMBER'S ASSIGNMENT IS SET ONCE AND NEVER TOGGLED; the TOPIC's stream is the variable.
  // Flipping the member between runs would mutate state every other spec's `member` session shares,
  // and it would also make the two runs differ in the principal rather than in the resource — which
  // is not what AC-010 says. One member, one assignment, two topics.
  //
  // ⚠ THE MEMBER HOLDS A STREAM THROUGHOUT, NEVER NONE. An unassigned member is refused too, but
  // that proves "unassigned is denied" — a DIFFERENT claim, and the one ADR-0043 negative
  // consequence (4) explicitly warns against evidencing this AC with.
  const adminCtx = await browser.newContext();
  const administrator = await roleSession(await adminCtx.newPage(), 'administrator', 'Administrator');

  // ⚠ FIRST EXECUTION OF THIS ENDPOINT IN E2E, EVER (DEF-066): assignment through HTTP was broken by
  // an IDENTITY column on member_streams until ADR-0043 step 5 rebuilt the table. If this setup
  // fails, that is a finding about the product, not about the fixture.
  const streamsRes = await adminCtx.request.get('/api/members/streams', {
    headers: { Authorization: administrator.bearer },
  });
  if (!streamsRes.ok()) throw new Error(`[e2e] list streams ${streamsRes.status()} ${await streamsRes.text()}`);
  const taxonomy = (await streamsRes.json()) as { publicId: string; code: string }[];
  const held = taxonomy.find((s) => s.code === HELD_STREAM);
  if (!held) throw new Error(`[e2e] the seeded taxonomy has no '${HELD_STREAM}' stream`);

  const assign = await adminCtx.request.put(`/api/members/${member.member.publicId}/streams`, {
    headers: { Authorization: administrator.bearer, 'Content-Type': 'application/json' },
    data: [held.publicId],
  });
  if (!assign.ok()) throw new Error(`[e2e] assign streams ${assign.status()} ${await assign.text()}`);

  // Accepted with the MEMBER as owner: grant-on-accept writes the Owner capability, so
  // CapabilityRequirement is satisfied for both topics and stream scope is the only thing left that
  // can tell them apart. Prepare is the action because it is the one TopicEdit path a Member can
  // actually reach — UpdateTopic uses TopicTriage post-Accept (DEF-068).
  inScope = await apiCreateTopic(api, secretary.bearer, `TrB in-scope ${STAMP}`, [HELD_STREAM]);
  await accept(inScope, member.member);

  outOfScope = await apiCreateTopic(api, secretary.bearer, `TrB out-of-scope ${STAMP}`, [UNHELD_STREAM]);
  await accept(outOfScope, member.member);
});

/*
 * AC-009 — ONLY THE REFUSAL HALF IS ASSERTED HERE, AND THE REASON IS RECORDED RATHER THAN GLOSSED.
 *
 * The AC's positive clause is "a Member assigned as Owner submits an edit ... the edit is accepted".
 * THAT IS NOT REACHABLE OVER HTTP TODAY, and the first run of this spec is what showed it — the
 * Owner's own edit came back 403. Reading UpdateTopicHandler explains why, and it is by design:
 *   - PRE-Accept  → the submitter edits freely, otherwise Policies.TopicEdit (owner-AiO or Sec/Chair)
 *   - POST-Accept → Policies.TopicTriage, i.e. SECRETARY/CHAIRMAN ONLY — which is AC-034's own rule
 * The ABAC Owner grant is written on Accept ("grant-on-accept ... so the ABAC owner check resolves
 * for later edits"), but by then the topic is post-Accept and TopicEdit is no longer consulted for
 * it. The one pre-Accept status reachable AFTER Accept is Reopened — and Topic.Reopen exists in the
 * aggregate with NO ENDPOINT, so nothing can get there.
 *
 * So this asserts what is true and provable, and AC-009 stays Partial with that gap named. Making it
 * pass by editing pre-Accept as the SUBMITTER would prove submitter-authorization, not ownership —
 * a different claim wearing the AC's name.
 */
test('AC-009 — a Member is refused an edit on a topic they do not own', async () => {
  const theirs = await edit(member.bearer, ownedByOther, `non-owner edit ${STAMP}`);
  expect(theirs.status(), 'a Member must be refused a topic they do not own').toBe(403);

  // The control: the same request shape from a principal who IS allowed. Without it, the 403 above
  // is equally consistent with "this route refuses everyone" or "the body is malformed".
  const bySecretary = await edit(secretary.bearer, ownedByOther, `control edit ${STAMP}`);
  expect(bySecretary.status(), 'the Secretary is not refused the same request').toBeLessThan(300);
});

test('AC-034 — after Acceptance a non-owner Member is refused, and the Secretary is not', async () => {
  // AC-034 names two things: the refusal, and that the Secretary retains metadata editing. Asserting
  // only the refusal would be satisfied by a topic nobody can edit, which is a different product.
  const asMember = await edit(member.bearer, ownedByOther, `post-accept member edit ${STAMP}`);
  expect(asMember.status(), 'a non-owner Member is refused after Acceptance').toBe(403);

  const asSecretary = await edit(secretary.bearer, ownedByOther, `post-accept secretary edit ${STAMP}`);
  expect(asSecretary.status(), 'the Secretary may still edit metadata after Acceptance').toBeLessThan(300);
});

test('AC-033 — a Rejected topic cannot be re-triaged, and the rejection survives the attempt', async () => {
  /*
   * "Delete or modify the rejection event" has no endpoint — the rejection is a status transition
   * plus an append-only audit row, so there is nothing to DELETE. What is reachable, and what the
   * AC's substance is, is that the recorded rejection cannot be overwritten: re-triaging is refused
   * by the aggregate's status guard (Topic.cs RequireStatus → InvalidOperationException → 409), and
   * the topic is STILL Rejected afterwards.
   *
   * The read-back is the assertion that matters. A 409 alone proves a request was refused; only
   * reading the topic afterwards proves the recorded rejection is intact — which is what "immutable"
   * claims.
   */
  const reAccept = await api.post(`/api/topics/${rejected.id}/accept`, {
    headers: { Authorization: secretary.bearer, 'Content-Type': 'application/json' },
    data: { ownerId: member.member.publicId, ownerName: member.member.fullName },
  });
  expect(reAccept.status(), 'a Rejected topic must not be re-accepted').toBe(409);

  const reReject = await api.post(`/api/topics/${rejected.id}/reject`, {
    headers: { Authorization: secretary.bearer, 'Content-Type': 'application/json' },
    data: { reason: `overwritten ${STAMP}` },
  });
  expect(reReject.status(), 'a Rejected topic must not be re-rejected with a new reason').toBe(409);

  // ⚠ BY KEY, not id — the route is GET /api/topics/{key}. The first run 404'd on the id, which
  // reads exactly like "the topic is gone" and would be a very misleading way to fail an
  // immutability test. Every OTHER call here takes the id; only the read takes the key.
  const after = await api.get(`/api/topics/${rejected.key}`, { headers: { Authorization: secretary.bearer } });
  expect(after.status()).toBe(200);
  const body = (await after.json()) as { status: string };
  expect(body.status, 'the topic is still Rejected after both attempts').toBe('Rejected');
});

/*
 * AC-010 — THE LIVE LEG (ADR-0043 step 8). "A Member assigned to Stream-A is denied a write on a
 * Stream-B topic, and the refusal is recorded."
 *
 * WHY IT COULD NOT BE WRITTEN UNTIL NOW: StreamScopeRequirement was registered in DI, unit-tested,
 * and in NO POLICY (DEF-057), so the handler was never invoked and the control failed OPEN. Step 7
 * wired it; this is the evidence.
 *
 * ⚠ WHAT THIS LEG ADDS OVER THE API-LEVEL PAIR in TopicEndpointsCoverageTests, which already proves
 * the same discrimination: a real Keycloak token, a real role mapping, and the deployed middleware
 * pipeline. The API tests use a synthetic TestAuthHandler and the InMemory provider — the harness
 * that hid DEF-066 for two whole steps of this slice.
 */
test('AC-010 — a Member holding one stream may act on their own topic in it', async () => {
  const allowed = await api.post(`/api/topics/${inScope.id}/prepare`, {
    headers: { Authorization: member.bearer },
  });

  // ⚠ ASSERTED FIRST, DELIBERATELY. If the fixture is broken — the owner grant never resolved, the
  // role mapped wrong, the assignment did not land — this fails here, loudly. Asserting only the
  // refusal would let every one of those failures pass as a 403 that proves nothing.
  expect(allowed.status(), "the owner holds this topic's stream, so nothing should refuse it")
    .toBe(204);
});

test('AC-010 — the same Member is refused their own topic in a stream they do not hold', async () => {
  const refused = await api.post(`/api/topics/${outOfScope.id}/prepare`, {
    headers: { Authorization: member.bearer },
  });

  // Same member, same role, same Owner capability, same request — only the TOPIC's stream differs.
  // That is what makes this stream scope and not ownership.
  expect(refused.status(), `the owner holds ${HELD_STREAM} and this topic affects ${UNHELD_STREAM}`)
    .toBe(403);
});
