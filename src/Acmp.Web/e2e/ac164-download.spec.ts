import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiCreateTopic } from './scenario';
import { realDocx, realPdf, toTempFile } from './realFiles';

/*
 * AC-164 (DEC-192 r1/r5), DEF-172: Download SAVES a topic attachment under its ORIGINAL name - an Arabic name
 * included - and opens no tab. Round trip with REAL files: a real PDF and a real DOCX are uploaded through the
 * Attachments tab, downloaded back, and the saved name and the bytes are compared with what was uploaded.
 *
 * Before this, the button window.open'ed a presigned link with no disposition: the browser showed the PDF in a
 * new tab and a save gave the storage key's GUID. No test clicked the button at all.
 */
test.describe('AC-164 - an attachment downloads under its original name', () => {
  test('a real PDF and a real DOCX (Arabic name) round-trip byte-for-byte, with no new tab', async ({ page, context }) => {
    test.setTimeout(120_000);
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    const topic = await apiCreateTopic(page.request, bearer, `AC164 ${Date.now()}`);

    const files = [
      { name: 'decision-brief.pdf', bytes: realPdf('ACMP AC-164 decision brief') },
      { name: 'تقرير المراجعة.docx', bytes: realDocx('تقرير مراجعة اللجنة - AC-164') },
    ];

    await page.goto(`/topics/${topic.key}`);
    await page.getByRole('tab', { name: /Attachments/ }).click();
    for (const f of files) {
      await page.getByLabel(/Drop files/i).setInputFiles(toTempFile(f.name, f.bytes));
      await expect(page.getByText(f.name, { exact: true })).toBeVisible({ timeout: 30_000 });
    }

    const pagesBefore = context.pages().length;
    for (const f of files) {
      const [download] = await Promise.all([
        page.waitForEvent('download'),
        page.getByRole('button', { name: `Download ${f.name}` }).click(),
      ]);
      // The name comes from the SIGNED Content-Disposition - the storage key is a GUID (C-FILE-01).
      expect(download.suggestedFilename()).toBe(f.name);
      expect(readFileSync(await download.path())).toEqual(f.bytes);
    }
    expect(context.pages().length, 'Download must not open a tab').toBe(pagesBefore);
    await expect(page.getByText('That file could not be downloaded.')).toHaveCount(0);
  });
});
