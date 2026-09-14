import { test, expect, type Page } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer, meMember } from './apiHelpers';
import { apiCreateTopic, apiScheduleMeeting } from './scenario';
import { realPdf, throttleUpload, toTempFile } from './realFiles';

/*
 * AC-169 (DEC-192 r5), DEF-179 / DEF-180: the attachment pickers, as a person uses them.
 *   1. a REAL attachment over a SLOW uplink shows its percentage rising to completion, and the picker offers only
 *      the types the server accepts;
 *   2. a refused upload shows its TRANSLATED reason (in Arabic), and picking the SAME file again is a new attempt;
 *   3. limit parity: every page states the server's configured maximum - and, fed a limit no default carries,
 *      states THAT, so a hard-coded copy cannot hide behind a default that happens to match.
 */

interface Limits { attachmentMaxBytes: number; attachmentContentTypes: string[]; recordingMaxBytes: number; recordingContentTypes: string[] }

const mb = (bytes: number) => new Intl.NumberFormat('en').format(Math.round(bytes / (1024 * 1024)));

async function liveLimits(page: Page, bearer: string): Promise<Limits> {
  const res = await page.request.get('/api/uploads/limits', { headers: { Authorization: bearer } });
  expect(res.ok()).toBe(true);
  return res.json();
}

test.describe('AC-169 - uploads: progress, refusal, re-pick, limit parity', () => {
  test('a real attachment over a slow uplink shows a rising percentage; the picker offers only allowed types', async ({ page }) => {
    test.setTimeout(120_000);
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    const limits = await liveLimits(page, bearer);
    const topic = await apiCreateTopic(page.request, bearer, `AC169a ${Date.now()}`);

    await page.goto(`/topics/${topic.key}`);
    await page.getByRole('tab', { name: /Attachments/ }).click();
    const input = page.locator('input[type=file]');
    await expect(input).toHaveAttribute('accept', limits.attachmentContentTypes.join(','));

    const lift = await throttleUpload(page, 1024 * 1024);
    await input.setInputFiles(toTempFile('committee-evidence.pdf', realPdf('AC-169 evidence', 3 * 1024 * 1024)));
    const bar = page.getByRole('progressbar', { name: 'committee-evidence.pdf' });
    await expect(bar).toBeVisible();
    await expect.poll(async () => Number(await bar.getAttribute('aria-valuenow').catch(() => '0')), { timeout: 60_000, intervals: [100] })
      .toBeGreaterThan(0);
    const mid = Number(await bar.getAttribute('aria-valuenow').catch(() => '100'));
    await expect(bar).toBeHidden({ timeout: 60_000 });
    await lift();
    expect(mid, 'the percentage must be seen below 100 while the upload runs').toBeLessThan(100);
    await expect(page.getByText('committee-evidence.pdf', { exact: true })).toBeVisible();
  });

  test('a refused file shows its reason in Arabic, and picking the same file again is a new attempt', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('i18nextLng', 'ar'));
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    const topic = await apiCreateTopic(page.request, bearer, `AC169b ${Date.now()}`);

    let attempts = 0;
    page.on('request', (r) => { if (r.method() === 'POST' && /\/api\/topics\/[^/]+\/attachments$/.test(r.url())) attempts++; });

    await page.goto(`/topics/${topic.key}`);
    await page.getByRole('tab', { name: /المرفقات/ }).click();
    const notes = toTempFile('ملاحظات.txt', Buffer.from('ملاحظات الاجتماع - plain text is not an allowed type\n', 'utf8'));
    const refusal = page.getByRole('alert').filter({ hasText: 'نوع الملف غير مسموح.' });

    await page.locator('input[type=file]').setInputFiles(notes);
    await expect(refusal).toBeVisible();
    await expect(page.getByRole('alert').filter({ hasText: /not allowed|isn’t allowed/i })).toHaveCount(0);
    expect(attempts).toBe(1);

    // DEF-179: the input kept the old value, so choosing the same file again fired no change event at all.
    await page.locator('input[type=file]').setInputFiles(notes);
    await expect.poll(() => attempts).toBe(2);
    await expect(refusal).toBeVisible();
  });

  test('every upload page states the server limit, and follows the server when it changes', async ({ page }) => {
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    const live = await liveLimits(page, bearer);
    const topic = await apiCreateTopic(page.request, bearer, `AC169c ${Date.now()}`);
    const meeting = await apiScheduleMeeting(page.request, bearer, `AC169c ${Date.now()}`, await meMember(page, bearer));

    const statesLimits = async (l: Limits) => {
      await page.goto('/backlog/submit');
      await expect(page.getByText(`up to ${mb(l.attachmentMaxBytes)} MB`)).toBeVisible();
      await page.goto(`/topics/${topic.key}`);
      await page.getByRole('tab', { name: /Attachments/ }).click();
      await expect(page.getByText(`up to ${mb(l.attachmentMaxBytes)} MB`)).toBeVisible();
      await page.goto(`/meetings/${meeting.key}/recording`);
      await expect(page.getByText(`up to ${mb(l.recordingMaxBytes)} MB`)).toBeVisible();
    };

    // The live server's own numbers.
    await statesLimits(live);

    // A limit no client default carries: a page showing a copy instead of the server's number fails here.
    const odd: Limits = { ...live, attachmentMaxBytes: 37 * 1024 * 1024, recordingMaxBytes: 777 * 1024 * 1024 };
    await page.route('**/api/uploads/limits', (route) => route.fulfill({ json: odd }));
    await statesLimits(odd);
  });
});
