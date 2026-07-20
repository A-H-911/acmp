import { test, expect, type Page } from '@playwright/test';
import { loginAs } from './login';

/*
 * AC-049 / AC-030 (BL-016) — server validation is surfaced to the user in the ACTIVE LOCALE, not the raw
 * English server text. The client pre-checks only file SIZE, so a small file that declares image/png but
 * carries text bytes passes the client and trips the server's magic-byte inspector (FILE_CONTENT_MISMATCH);
 * the SPA renders the localized message via the BL-016 error-code catalog. AC-030 is the required-field path.
 */

// Declares image/png (an allowed type) but the bytes are text → server content-mismatch, not a client reject.
const MISLABELLED_PNG = { name: 'not-really.png', mimeType: 'image/png', buffer: Buffer.from('plain text, definitely not PNG bytes') };

async function fillSubmitForm(page: Page, title: string): Promise<void> {
  await page.goto('/backlog/submit');
  await page.getByRole('button', { name: 'Arch. Decision' }).click();
  await page.getByRole('textbox', { name: 'Title', exact: true }).fill(title);
  await page.getByRole('textbox', { name: 'Description', exact: true }).fill('AC-049 e2e description.');
  await page.getByRole('textbox', { name: 'Why now', exact: true }).fill('AC-049 e2e justification.');
  const streams = page.getByRole('textbox', { name: 'Affected streams', exact: true });
  await streams.fill('Platform');
  await streams.press('Enter');
}

test.describe('AC-049 / AC-030 — locale-aware validation (BL-016)', () => {
  test('a content-mismatched attachment is rejected with a localized (not raw-English) error', async ({ page }) => {
    await loginAs(page, 'secretary');
    await fillSubmitForm(page, `AC049 ${Date.now()}`);
    await page.locator('input[type=file]').setInputFiles(MISLABELLED_PNG);
    await page.getByRole('button', { name: 'Submit for triage' }).click();

    // The BL-016-localized FILE_CONTENT_MISMATCH message — NOT the server's raw "does not match its declared
    // type '...'" text (which would name the content type and prove the code was dropped on the floor).
    const alert = page.getByRole('alert').filter({ hasText: 'match its declared type' });
    await expect(alert).toBeVisible();
    await expect(alert).not.toContainText("'image/png'");
  });

  test('required-field submit is blocked in-locale with no topic created (AC-030)', async ({ page }) => {
    await loginAs(page, 'secretary');
    await page.goto('/backlog/submit');

    // Submit with nothing filled → client validation shows localized field errors, no request, stays on the form.
    await page.getByRole('button', { name: 'Submit for triage' }).click();
    await expect(page.getByRole('alert').first()).toBeVisible();
    await expect(page).toHaveURL(/\/backlog\/submit$/);
  });
});
