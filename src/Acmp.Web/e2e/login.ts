import { type Page, expect } from '@playwright/test';
import { E2E_PASSWORD, E2E_USERS, type E2eRole } from './users';

/*
 * Drives the REAL Keycloak authorization-code + PKCE login (ADR-0016 §2) — no
 * token shortcuts. Starts at the SPA, follows the redirect to the Keycloak login
 * form (a separate origin), submits the seeded credentials, and waits for the
 * round-trip back to the authenticated dashboard.
 */
export async function loginAs(page: Page, role: E2eRole): Promise<void> {
  const user = E2E_USERS[role];

  // ProtectedRoute bounces an unauthenticated visit to /login; the CTA starts signinRedirect().
  await page.goto('/');
  await page.locator('.login-cta').click();

  // Genuine Keycloak login form (origin keycloak.localhost:8085).
  await page.waitForURL(/\/realms\/acmp\/protocol\/openid-connect\/auth/, { timeout: 30_000 });
  await page.locator('#username').fill(user.username);
  await page.locator('#password').fill(E2E_PASSWORD);
  await page.locator('#kc-login').click();

  // Back in the SPA, authenticated, landed on Home ('/' — Usage Map §G).
  await page.waitForURL((url) => new URL(url).pathname === '/', { timeout: 30_000 });
  await expect(page.locator('.login-cta')).toHaveCount(0);
}

/**
 * The same real PKCE round-trip for an account the APPLICATION created (ADR-0038 / ADR-0040), rather
 * than one global-setup seeded.
 *
 * ⚠ WHY loginAs CANNOT BE REUSED, and it is a property of the product rather than of the harness:
 * KeycloakAdminClient sets the invited account's password with `temporary: true`, so Keycloak
 * interrupts the round-trip with its UPDATE PASSWORD required action. loginAs would sit waiting for a
 * dashboard that the update form is standing in front of, and time out 30 seconds later saying
 * nothing about why. Handling the interruption here keeps that fact in one place and named.
 *
 * The caller supplies `newPassword` because the temporary one is revealed exactly once and stored
 * nowhere (AC-088) — there is no way to look it up again afterwards.
 */
export async function loginWithTemporaryPassword(
  page: Page,
  username: string,
  temporaryPassword: string,
  newPassword: string,
): Promise<void> {
  await page.goto('/');
  await page.locator('.login-cta').click();

  await page.waitForURL(/\/realms\/acmp\/protocol\/openid-connect\/auth/, { timeout: 30_000 });
  await page.locator('#username').fill(username);
  await page.locator('#password').fill(temporaryPassword);
  await page.locator('#kc-login').click();

  // Keycloak's update-password form. Asserted rather than probed: if this never appears the account
  // was NOT created with a temporary credential, which is a finding about the invite path and should
  // fail loudly here instead of being skipped past.
  const newField = page.locator('#password-new');
  await newField.waitFor({ state: 'visible', timeout: 30_000 });
  await newField.fill(newPassword);
  await page.locator('#password-confirm').fill(newPassword);

  // ⚠ `input[type=submit]` FIRST, AND NOT `#kc-login`. Keycloak's update-password form submits with an
  // <input type="submit"> inside #kc-form-buttons, so a `button[type=submit]` selector matches nothing
  // and simply waits until the hook times out — which reads as "the login is slow" and is not. The
  // first run of this helper failed exactly that way, and the giveaway was that the two fills ABOVE
  // had already succeeded: the form was there and correct, only the submit selector was wrong. Both
  // element kinds are listed because the theme uses a <button> on some Keycloak versions.
  await page
    .locator('#kc-form-buttons input[type=submit], #kc-form-buttons button[type=submit]')
    .first()
    .click();

  // ⚠ NOT waitForURL('/') LIKE loginAs. A guest's landing route is not the committee dashboard, and
  // pinning one here would couple this helper to a navigation decision (DEC-048 d4) that has nothing
  // to do with logging in. Leaving Keycloak's origin behind is the actual post-condition.
  await page.waitForURL((url) => !url.href.includes('/realms/acmp/'), { timeout: 30_000 });
}
