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

  // Back in the SPA, authenticated, landed on the dashboard.
  await page.waitForURL(/\/dashboard/, { timeout: 30_000 });
  await expect(page.locator('.login-cta')).toHaveCount(0);
}
