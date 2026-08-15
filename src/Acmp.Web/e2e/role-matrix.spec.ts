import { test, expect, type APIRequestContext, type Browser } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { type E2eRole } from './users';

/*
 * AC-005 / AC-006 / AC-007 — THE LIVE LEG. Tranche A of the Partials campaign.
 *
 * WHY THESE ACs WERE PARTIAL, AND IT WAS THE SAME REASON FOR ALL THREE. The deny matrix is already
 * proven by PermissionMatrixTests, exhaustively, over 34 policies × 8 roles — but that runs in
 * Acmp.Application.Tests, and the HTTP layer above it is covered by Acmp.Api.Tests, which
 * authenticates with a SYNTHETIC TestAuthHandler. RealJwtAuthTests boots the real JwtBearer scheme
 * but only covers the 401 fail-closed paths. So no test anywhere drove a REAL KEYCLOAK TOKEN through
 * the role matrix, and "any direct API call returns 403" was an assertion about a fake principal.
 * This spec closes exactly that gap and nothing more: the matrix is not re-derived here, it is
 * carried through the real auth stack.
 *
 * WHY A THIN BODY AND A RANDOM GUID ARE LEGITIMATE HERE, rather than a shortcut that makes the test
 * lie. SharedKernelExtensions registers AuthorizationBehavior BEFORE ValidationBehavior and
 * TransactionBehavior, so a role refusal happens before the body is validated and before any entity
 * is looked up. That is what lets these cases run with no fixtures at all. ⚠ The body must still be
 * WELL-FORMED JSON with a Content-Type: minimal-API binding runs before MediatR, and DEF-046 is the
 * defect where a missing Content-Type turned a real call into a 415 that no test caught.
 *
 * ⚠ EVERY DENIAL IS PAIRED WITH AN ALLOWED-ROLE CONTROL ON THE SAME ROUTE. Without it a 403 could
 * come from the route being wrong, a typo'd path, or a middleware refusing everyone — and the test
 * would pass for a reason that has nothing to do with roles. The control asserts only "NOT 403":
 * an allowed caller hitting a random guid legitimately gets 404 or 400, and pinning the exact code
 * would couple this spec to handler internals it is not about.
 */

const RANDOM_ID = '00000000-0000-4000-8000-0000000000ff';

interface Call {
  readonly label: string;
  readonly path: string;
  readonly body?: unknown;
}

/** The six capabilities AC-005 names, in its own order, plus the create AC-006 needs. */
const CALLS = {
  triage: { label: 'triage (accept a topic)', path: `/api/topics/${RANDOM_ID}/accept`, body: {} },
  agendaPublish: { label: 'agenda publishing', path: `/api/meetings/${RANDOM_ID}/agenda/publish`, body: {} },
  meetingSchedule: { label: 'meeting scheduling', path: '/api/meetings', body: { title: 'x', scheduledStart: '2030-01-01T10:00:00Z', scheduledEnd: '2030-01-01T11:00:00Z' } },
  voteManage: { label: 'vote management (open a vote)', path: `/api/votes/${RANDOM_ID}/open`, body: {} },
  voteClose: { label: 'closing a vote', path: `/api/votes/${RANDOM_ID}/close`, body: {} },
  voteCast: { label: 'casting a vote', path: `/api/votes/${RANDOM_ID}/cast`, body: { choice: 'Approve' } },
  decisionRecord: { label: 'decision recording', path: '/api/decisions', body: { title: 'x', outcome: 'Approved' } },
  decisionApprove: { label: 'chairman approval (issue a decision)', path: `/api/decisions/${RANDOM_ID}/issue`, body: {} },
  topicSubmit: { label: 'submitting a topic', path: '/api/topics', body: { title: 'x', description: 'x' } },
} as const satisfies Record<string, Call>;

async function post(request: APIRequestContext, bearer: string, call: Call) {
  // `data` makes Playwright send JSON with a Content-Type — DEF-046 is what happens without one.
  return request.post(call.path, { headers: { Authorization: bearer }, data: call.body ?? {} });
}

/** Log a role in through the real PKCE round-trip and keep its bearer. */
async function bearerFor(browser: Browser, role: E2eRole): Promise<string> {
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  try {
    await loginAs(page, role);
    return await captureBearer(page);
  } finally {
    await ctx.close();
  }
}

test.describe('Role matrix through a REAL Keycloak token (AC-005 / AC-006 / AC-007)', () => {
  test('AC-005 — a Submitter is refused every capability the AC names, and an allowed role is not', async ({ browser, request }) => {
    const submitter = await bearerFor(browser, 'submitter');
    const secretary = await bearerFor(browser, 'secretary');

    // AC-005's own list: triage, agenda publishing, meeting scheduling, vote management,
    // decision recording, chairman approval.
    for (const call of [CALLS.triage, CALLS.agendaPublish, CALLS.meetingSchedule, CALLS.voteManage, CALLS.decisionRecord, CALLS.decisionApprove]) {
      const denied = await post(request, submitter, call);
      expect(denied.status(), `Submitter must be refused ${call.label} (${call.path})`).toBe(403);
    }

    // The control. Secretary holds five of those six in PermissionMatrixTests; a random guid then
    // yields 404/400, never 403 — which is what proves the 403s above are about the ROLE.
    for (const call of [CALLS.triage, CALLS.agendaPublish, CALLS.meetingSchedule, CALLS.voteManage, CALLS.decisionRecord]) {
      const allowed = await post(request, secretary, call);
      expect(allowed.status(), `Secretary must NOT be refused ${call.label} by role`).not.toBe(403);
    }
  });

  test('AC-006 — an Auditor is refused every mutation, and the refusal is AUDITED', async ({ browser, request }) => {
    const auditor = await bearerFor(browser, 'auditor');

    for (const call of [CALLS.topicSubmit, CALLS.triage, CALLS.meetingSchedule, CALLS.decisionRecord, CALLS.voteManage]) {
      const denied = await post(request, auditor, call);
      expect(denied.status(), `Auditor must be refused ${call.label} (${call.path})`).toBe(403);
    }

    // Read-only access is the other half of the role and must still work, or "refused every
    // mutation" would be indistinguishable from "refused everything".
    const read = await request.get('/api/topics', { headers: { Authorization: auditor } });
    expect(read.status(), 'an Auditor must still READ').toBe(200);

  });

  /*
   * AC-006's SECOND CLAUSE — "with an audit event emitted". THIS NOW HOLDS (DEF-056 fixed).
   *
   * WHAT THE FIRST RUN MEASURED, and it is why this case exists at all: the refusals above all
   * returned 403, and GET /api/audit?action=Authorization.Forbidden returned status 200 with an
   * EMPTY items array — right query shape, no rows. The cause was layering, not the sink: every
   * write endpoint carries a per-endpoint .RequireAuthorization(Policies.X), so ASP.NET's
   * authorization middleware short-circuits with 403 BEFORE MediatR, and AuthorizationBehavior — at
   * the time the only place in the codebase that emitted Authorization.Forbidden — never ran.
   *
   * ⚠ THIS CASE CARRIED A `test.fail(true, ...)` UNTIL THE FIX LANDED, AND THE SHAPE IS WORTH
   * KEEPING IN MIND rather than just deleting: it PASSED while the gap existed and went RED the day
   * someone closed it, so the gap could never go quiet and the fix could not land unnoticed. A skip
   * would have said "we didn't look". What now proves the fix is the same assertion, unmarked.
   *
   * The emitter is AuditingAuthorizationResultHandler, an IAuthorizationMiddlewareResultHandler —
   * ONE seam that covers every endpoint the authorization middleware forbids, including ones added
   * later, rather than a policy each route must remember to opt into.
   */
  test('AC-006 — a refused mutation leaves an Authorization.Forbidden row', async ({ browser, request }) => {
    const auditor = await bearerFor(browser, 'auditor');

    const denied = await post(request, auditor, CALLS.topicSubmit);
    expect(denied.status(), 'precondition: the mutation really is refused').toBe(403);

    const audit = await request.get('/api/audit?action=Authorization.Forbidden&pageSize=50', {
      headers: { Authorization: auditor },
    });
    expect(audit.status()).toBe(200);
    const page = (await audit.json()) as { items: Array<{ action: string }> };
    expect(page.items.length, 'a refused mutation must leave an Authorization.Forbidden row').toBeGreaterThan(0);
    expect(page.items.every((e) => e.action === 'Authorization.Forbidden')).toBe(true);
  });

  test('AC-007 — an Administrator is refused committee content: vote, approve, close (SoD-5)', async ({ browser, request }) => {
    const administrator = await bearerFor(browser, 'administrator');
    const chairman = await bearerFor(browser, 'chairman');

    // The AC names exactly these three. Platform-admin authority never extends to committee content.
    for (const call of [CALLS.voteCast, CALLS.decisionApprove, CALLS.voteClose]) {
      const denied = await post(request, administrator, call);
      expect(denied.status(), `Administrator must be refused ${call.label} — SoD-5`).toBe(403);
    }

    // The control that makes SoD-5 mean something: the Chairman holds all three, so the refusals
    // above are a statement about the Administrator ROLE and not about these three routes.
    for (const call of [CALLS.voteCast, CALLS.decisionApprove, CALLS.voteClose]) {
      const allowed = await post(request, chairman, call);
      expect(allowed.status(), `Chairman must NOT be refused ${call.label} by role`).not.toBe(403);
    }
  });
});
