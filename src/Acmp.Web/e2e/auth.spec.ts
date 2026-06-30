import { test, expect } from '@playwright/test';
import { loginAs } from './login';

/*
 * S6 (ADR-0016 §2) — the auth round-trip the InMemory/unit suites can never
 * exercise: a real browser, a real Keycloak realm, a real authorization-code +
 * PKCE exchange. Failure-first: the protected-deep-link guard comes first.
 */
test.describe('auth — real Keycloak PKCE round-trip', () => {
  test('unauthenticated deep-link is redirected to the sign-in page', async ({ page }) => {
    await page.goto('/backlog');
    await expect(page).toHaveURL(/\/login/);
    await expect(page.locator('.login-cta')).toBeVisible();
  });

  test('signs in via Keycloak and lands authenticated on the dashboard', async ({ page }) => {
    await loginAs(page, 'secretary');
    await expect(page).toHaveURL(/\/$/);
    // Authenticated shell rendered (the app chrome only mounts behind ProtectedRoute).
    // The shell has the primary nav + the breadcrumb nav, so target the primary one.
    await expect(page.locator('nav.sidebar')).toBeVisible();
  });
});
