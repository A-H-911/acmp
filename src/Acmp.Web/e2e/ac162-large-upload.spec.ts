import { test, expect } from '@playwright/test';
import { closeSync, ftruncateSync, mkdtempSync, openSync, rmSync, writeSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { loginAs } from './login';

/*
 * AC-162 (DEC-190 a3), DEF-154 + DEF-166 - a ~100 MB attachment completes END TO END through the real stack:
 * browser -> nginx (client_max_body_size 2100m) -> Kestrel (the endpoint's own limit = configured maximum
 * + 1 MiB) -> the object store. Every lower-level test ran on TestServer, which enforces no body limit at all,
 * which is how Kestrel's 30,000,000-byte default refused valid files with the whole suite green.
 *
 * The file is written to disk and handed to Playwright as a PATH: an in-memory buffer is capped at 50 MB,
 * a path is streamed. It is exactly the 100 MB maximum - a PDF magic number, then zeros - so it also passes
 * the server's content inspector.
 */
const SIZE = 100 * 1024 * 1024;

test.describe('AC-162 - a 100 MB attachment is stored end to end', () => {
  let dir: string;
  let file: string;

  test.beforeAll(() => {
    dir = mkdtempSync(join(tmpdir(), 'acmp-ac162-'));
    file = join(dir, 'large-evidence.pdf');
    const fd = openSync(file, 'w');
    writeSync(fd, Buffer.from('%PDF-1.7\n'));
    ftruncateSync(fd, SIZE);
    closeSync(fd);
  });

  test.afterAll(() => rmSync(dir, { recursive: true, force: true }));

  test('a 100 MB file submitted with a topic lands on the topic', async ({ page }) => {
    test.setTimeout(180_000);
    await loginAs(page, 'secretary');
    await page.goto('/backlog/submit');
    await page.getByRole('button', { name: 'Arch. Decision' }).click();
    await page.getByRole('textbox', { name: 'Title', exact: true }).fill(`AC162 ${Date.now()}`);
    await page.getByRole('textbox', { name: 'Description', exact: true }).fill('AC-162 e2e: the 100 MB maximum.');
    await page.getByRole('textbox', { name: 'Why now', exact: true }).fill('DEF-154 / DEF-166.');
    await page.getByRole('button', { name: 'Core', exact: true }).click();
    await page.locator('input[type=file]').setInputFiles(file);
    // Staged, not refused: the browser's own check allows a file AT the maximum.
    await expect(page.getByText('large-evidence.pdf')).toBeVisible();

    await page.getByRole('button', { name: 'Submit for triage' }).click();

    // The submit page moves on to the topic only when every upload succeeded; a refused upload keeps it here
    // with the error, so reaching the topic IS the success signal - then the attachment must be listed.
    await expect(page).toHaveURL(/\/topics\/TOP-\d{4}-\d+$/, { timeout: 150_000 });
    await page.getByRole('tab', { name: /Attachments/ }).click();
    await expect(page.getByText('large-evidence.pdf')).toBeVisible();
  });
});
