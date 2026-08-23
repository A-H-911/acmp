import { test, expect, type APIRequestContext, type Browser } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { E2E_PASSWORD, E2E_ROLELESS_USER } from './users';

/*
 * AC-003 — THE LIVE LEG. "Given a user whose Keycloak token has no ACMP role claim, when they
 * complete SSO login, then the system denies access or assigns the minimum default role per
 * configuration, and an AuthEvent is emitted to the audit log."
 *
 * WHY THIS COULD ONLY EVER BE PROVEN HERE. The emitter for the AC's own moment — a validated token
 * whose claims map to zero ACMP roles — lives in JwtBearerEvents.OnTokenValidated
 * (AuthenticationExtensions.MapKeycloakRolesAsync), and SC-005 already recorded that this seam is
 * UNREACHABLE from every API test: Acmp.Api.Tests authenticates with the synthetic TestAuthHandler,
 * and RealJwtAuthTests boots the real JwtBearer scheme with no Authority, so every token is rejected
 * before OnTokenValidated can fire. A real Keycloak token is the only thing that reaches it.
 *
 * ⚠ AV-159 SAID NO AUDIT ROW IS WRITTEN FOR A ROLE-LESS LOGIN, AND THAT WAS WRONG — my own row,
 * corrected here. It searched for emitters of `Authorization.Forbidden` and concluded from their
 * absence that nothing recorded this, missing `Authentication.NoRoleClaim` entirely. That is why
 * this spec asserts the row by the name the PRODUCT uses rather than the name the register expected.
 *
 * WHICH BRANCH THE SYSTEM TAKES: deny. The AC permits either, and there is no configured
 * DefaultRole, so PrimaryRole resolves to nothing and provisioning refuses — the safer branch.
 */

/** An Auditor's bearer, the only fixture role that may read /api/audit (Policies.AuditRead). */
async function auditorBearer(browser: Browser): Promise<string> {
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  try {
    await loginAs(page, 'auditor');
    return await captureBearer(page);
  } finally {
    await ctx.close();
  }
}

async function noRoleClaimRows(request: APIRequestContext, bearer: string): Promise<number> {
  const res = await request.get('/api/audit?action=Authentication.NoRoleClaim&pageSize=1', {
    headers: { Authorization: bearer },
  });
  expect(res.status(), 'an Auditor must be able to read the audit register').toBe(200);
  return ((await res.json()) as { total: number }).total;
}

test('AC-003 — a token with no ACMP role claim is denied at provisioning, and the login is on the audit log', async ({
  browser,
  request,
}) => {
  const auditor = await auditorBearer(browser);

  // ⚠ A DELTA, NOT AN ABSOLUTE COUNT. "There is at least one NoRoleClaim row" would pass on a row
  // left by any earlier run against a long-lived environment, so it could never discriminate between
  // "this login was recorded" and "something once was". The before/after pair binds the assertion to
  // the login this test performs.
  const before = await noRoleClaimRows(request, auditor);

  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  let provisionStatus: number;
  try {
    await page.goto('/');
    await page.locator('.login-cta').click();
    await page.waitForURL(/\/realms\/acmp\/protocol\/openid-connect\/auth/, { timeout: 30_000 });
    await page.locator('#username').fill(E2E_ROLELESS_USER.username);
    await page.locator('#password').fill(E2E_PASSWORD);

    // ⚠ THE POST-CONDITION IS THE PROVISIONING RESPONSE, NOT A LANDING ROUTE — and that choice is the
    // whole reason loginAs is not reused here. loginAs waits for pathname '/' and for the login CTA to
    // vanish, both of which are statements about what the SPA renders for a COMMITTEE MEMBER. This
    // principal is not one, so waiting on either would be guessing at a screen nobody has specified,
    // and a wrong guess shows up as a bare timeout that says nothing about the login (the lesson
    // loginWithTemporaryPassword was built from).
    //
    // AuthProvider fires POST /members/me from an effect keyed on `oidc.isAuthenticated` ALONE — no
    // ACMP role is involved — so this response is guaranteed by the one fact already proven by the
    // fills above: that the Keycloak round-trip completes. One assumption instead of three.
    const [provision] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/api/members/me') && r.request().method() === 'POST',
        { timeout: 60_000 },
      ),
      page.locator('#kc-login').click(),
    ]);
    provisionStatus = provision.status();
  } finally {
    await ctx.close();
  }

  // 403 and not 401: the token is VALID and the identity is established — what is missing is any
  // committee role. A 401 here would mean the deny happened for the wrong reason, and the SPA would
  // answer it by renewing a token that is not the problem.
  expect(provisionStatus, 'a validated token with no committee role must be DENIED, not provisioned').toBe(403);

  const after = await noRoleClaimRows(request, auditor);
  expect(after, "the AC's AuthEvent — Authentication.NoRoleClaim, emitted at OnTokenValidated").toBeGreaterThan(
    before,
  );

  // The denial itself is recorded too, by ProvisionCurrentUser's own emit (DEF-056's family arriving
  // by a second route: this request PASSES authorization and is refused inside the handler, so the
  // AuditingAuthorizationResultHandler structurally cannot see it).
  const refusals = await request.get('/api/audit?action=Authorization.Forbidden&pageSize=50', {
    headers: { Authorization: auditor },
  });
  expect(refusals.status()).toBe(200);
  const page1 = (await refusals.json()) as { items: Array<{ action: string }> };
  expect(page1.items.length, 'the refused provisioning attempt leaves its own row').toBeGreaterThan(0);
});
