import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { loginAs } from './login';
import { captureBearer, meMember } from './apiHelpers';
import { apiScheduleMeeting } from './scenario';
import { realMp4, throttleUpload, toTempFile } from './realFiles';

/*
 * AC-166 + AC-165 (DEC-192 r5), DEF-173 / DEF-174: a REAL video recording, uploaded over a SLOW uplink, shows
 * its percentage rising to completion; then Download saves it under its original name, in place.
 *
 * The local network finishes an upload in milliseconds, which is why no test ever saw that the recording page
 * had no progress at all. Chromium's network emulation throttles the upload so the bar is observable.
 */
test.describe('AC-165/166 - recording upload progress and download', () => {
  test('a real MP4 uploads with a rising percentage over a slow uplink, then downloads under its name', async ({ page }) => {
    test.setTimeout(180_000);
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    // The caller's own member row (provisioned on demand): a fresh database has no other member yet.
    const meeting = await apiScheduleMeeting(page.request, bearer, `AC166 ${Date.now()}`, await meMember(page, bearer));

    await page.goto(`/meetings/${meeting.key}/recording`);
    // AC-166: the page states the server's limit (2 GB default).
    await expect(page.getByText(/up to 2,048 MB/)).toBeVisible();

    const bytes = realMp4(4 * 1024 * 1024); // the real video + a 4 MB `free` box: still a valid MP4
    const lift = await throttleUpload(page, 1024 * 1024); // ~1 MB/s, so the upload takes a few seconds
    await page.getByLabel(/upload recording/i).setInputFiles(toTempFile('meeting-recording.mp4', bytes));

    const bar = page.getByRole('progressbar', { name: 'meeting-recording.mp4' });
    await expect(bar).toBeVisible();
    // Observed MID-FLIGHT: a value strictly between 0 and 100 proves the bar tracks bytes sent, not a spinner.
    await expect.poll(async () => Number(await bar.getAttribute('aria-valuenow').catch(() => '0')), { timeout: 60_000, intervals: [100] })
      .toBeGreaterThan(0);
    const mid = Number(await bar.getAttribute('aria-valuenow').catch(() => '100'));
    await expect(bar).toBeHidden({ timeout: 120_000 });
    await lift();
    expect(mid, 'the percentage must be seen below 100 while the upload runs').toBeLessThan(100);
    await expect(page.getByText('meeting-recording.mp4')).toBeVisible();

    // AC-165: Download mints its link on click and saves under the original name, in place.
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: /download/i }).click(),
    ]);
    expect(download.suggestedFilename()).toBe('meeting-recording.mp4');
    expect(readFileSync(await download.path()).equals(bytes)).toBe(true);
    await expect(page).toHaveURL(new RegExp(`/meetings/${meeting.key}/recording$`));
  });
});
