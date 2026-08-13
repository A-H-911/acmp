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
 * ⚠ NOT IN THIS FILE, AND NOT BECAUSE IT WAS HARD. AC-010 (stream scope) is blocked by DEF-057:
 * StreamScopeRequirement is registered in DI and unit-tested but appears in NO policy, so it is
 * never evaluated and the control fails OPEN — and separately no Stream can be created at all. A
 * 403 is obtainable there, but only by a member holding no streams, which proves a different claim
 * than the AC makes. AC-011 needs a Guest presenter and is deferred with it.
 */

const STAMP = Date.now();
const REJECTION_REASON = `Out of committee scope — e2e ${STAMP}`;

let secretary: { bearer: string; member: ApiMember };
let member: { bearer: string; member: ApiMember };
let api: APIRequestContext;

/** Accepted, owner is the Secretary — the Member is a non-owner on an Accepted topic. */
let ownedByOther: ApiTopic;
/** Rejected with a recorded reason. */
let rejected: ApiTopic;

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
    streams: ['Platform'],
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
