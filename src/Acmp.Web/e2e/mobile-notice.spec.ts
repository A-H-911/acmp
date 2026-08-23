import { test, expect } from '@playwright/test';
import { loginAs } from './login';

/*
 * NFR-063 / DW-061 — the "not optimized for mobile" notice.
 *
 * WHY THIS TEST EXISTS RATHER THAN A JSDOM ONE. The requirement's property is "< 768px displays
 * the notice", and that is a media-query outcome. jsdom applies no media queries and has no
 * layout engine, so the unit test beside MobileNotice.tsx can only prove the strings resolve —
 * it would pass identically whether or not the breakpoint existed. Only a real browser at a real
 * viewport can tell the two apart, which is what this file does.
 *
 * WHY IT DOES NOT LOG IN. The notice is mounted at the app root rather than inside AppShell,
 * precisely because /login is the first thing a phone user reaches. That makes this spec
 * unauthenticated and therefore cheap — no Keycloak round-trip, no API seeding — which matters in
 * a suite that already drives seven services per pull request.
 *
 * THE ASSERTIONS ARE PAIRED ON PURPOSE. "Visible at 375px" alone would pass if the element were
 * simply always visible, which is the likeliest way this regresses — someone deletes the
 * `display: none` default while tidying global.css. Every case therefore checks the negative at
 * 1280px too, so the pair fails if the media query stops discriminating in either direction.
 */

const PHONE = { width: 375, height: 667 }; // iPhone SE class — comfortably under the threshold
const DESKTOP = { width: 1280, height: 800 };

/** The detector reads localStorage first (i18n/index.ts detection order), so this survives the reload. */
async function useLanguage(page: import('@playwright/test').Page, lng: 'en' | 'ar') {
  await page.addInitScript((l) => window.localStorage.setItem('i18nextLng', l), lng);
}

test.describe('NFR-063 — not-optimized-for-mobile notice', () => {
  test('is shown below 768px and absent above it (English)', async ({ page }) => {
    await useLanguage(page, 'en');
    const notice = page.locator('.mobile-notice');

    await page.setViewportSize(PHONE);
    await page.goto('/login');
    await expect(notice).toBeVisible();
    await expect(notice).toContainText('not optimized for small screens');

    await page.setViewportSize(DESKTOP);
    // The element stays in the DOM; only its computed display changes, so assert VISIBILITY.
    await expect(notice).toBeHidden();
  });

  test('is shown below 768px in Arabic, in a right-to-left document', async ({ page }) => {
    await useLanguage(page, 'ar');
    const notice = page.locator('.mobile-notice');

    await page.setViewportSize(PHONE);
    await page.goto('/login');

    // dir lives on <html> (i18n/index.ts applyDirection), which is what the app really sets.
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(notice).toBeVisible();

    /*
     * ASSERT THE PROPERTY — "real Arabic renders" — NOT A HAND-PICKED FRAGMENT.
     *
     * ⚠ THE FIRST VERSION OF THIS ASSERTION FAILED IN CI AND THE REASON IS WORTH KEEPING.
     * It looked for the substring 'الشاشات الصغيرة', which appears to be right there in the
     * sentence. It is not: the string renders 'للشاشات الصغيرة', because the preposition لـ
     * absorbs the alef of the definite article ال. The standalone form الشاشات never occurs.
     * That is the same grammar rule DEC-032 records for the الهيكلة term family — Arabic
     * morphology is a RULE, not a string substitution — and it bites test assertions exactly
     * as it bites renames.
     *
     * So this checks the script range instead: any run of Arabic characters proves the AR
     * bundle resolved, and survives every future rewording of the sentence. Paired with the
     * key-echo check, which is the failure mode that would otherwise render "a notice" and
     * satisfy a laxer assertion.
     */
    await expect(notice).toContainText(/[؀-ۿ]{3,}/);
    await expect(notice).not.toContainText('common.mobileNotice');

    await page.setViewportSize(DESKTOP);
    await expect(notice).toBeHidden();
  });

  test('the notice reaches the authenticated shell too, not just the login page', async ({ page }) => {
    /*
     * AppShell is a different React subtree from LoginPage, and mounting at the app root is the
     * only reason the notice survives that boundary. This case is what would fail if someone
     * later "tidied" MobileNotice into AppShell: /login would silently lose it — which is the
     * one screen a phone user is guaranteed to see.
     *
     * ⚠ THIS TEST REALLY LOGS IN, and that is the point. An earlier draft just navigated to
     * /backlog unauthenticated and asserted the notice — but ProtectedRoute redirects that
     * straight back to /login, so it was re-proving the previous case under a name that claimed
     * to prove the shell. A green assertion under a wrong name is worse than no test.
     */
    await useLanguage(page, 'en');
    await page.setViewportSize(PHONE);

    await page.goto('/login');
    await expect(page.locator('.mobile-notice')).toBeVisible();

    await loginAs(page, 'secretary');
    // Prove we are genuinely inside the shell before asserting the notice sits alongside it.
    await expect(page.locator('.app-shell')).toBeVisible();
    await expect(page.locator('.mobile-notice')).toBeVisible();

    await page.setViewportSize(DESKTOP);
    await expect(page.locator('.mobile-notice')).toBeHidden();
    await expect(page.locator('.app-shell')).toBeVisible();
  });
});
