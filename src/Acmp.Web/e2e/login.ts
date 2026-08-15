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
  // ⚠ SUBMIT THE FORM, DO NOT HUNT FOR ITS BUTTON. Two runs were spent guessing at Keycloak's markup
  // for this one control — first `#kc-form-buttons button[type=submit]`, then the same with
  // `input[type=submit]` — and both matched NOTHING and waited out the hook, which reads as "the login
  // is slow" rather than "that selector is wrong". The fills above always succeeded, so the form was
  // there and correct every time; only my idea of its submit control was not.
  //
  // Pressing Enter in a text input submits the owning form natively, so this depends on the form
  // EXISTING rather than on the theme's choice of <input type=submit> vs <button>, on a container id,
  // or on a label. That is one assumption instead of three, and it is the one already proven by the
  // fills. The right lesson from a selector that matched nothing is to stop needing the selector.
  await page.locator('#password-confirm').fill(newPassword);
  await page.locator('#password-confirm').press('Enter');

  // ⚠ NOT waitForURL('/') LIKE loginAs. A guest's landing route is not the committee dashboard, and
  // pinning one here would couple this helper to a navigation decision (DEC-048 d4) that has nothing
  // to do with logging in. Leaving Keycloak's origin behind is the actual post-condition.
  await page.waitForURL((url) => !url.href.includes('/realms/acmp/'), { timeout: 30_000 });
}
