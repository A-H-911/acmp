import { test, expect, type Page } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiMembers, apiCreateTopic, type ApiMember } from './scenario';

/*
 * P17b — live real-stack leg for the traceability ACs (bin (a) of the P17b-0 triage):
 *   AC-063  a typed edge created through the UI is reflected on BOTH endpoints' panels
 *   AC-062  an artifact detail page shows its typed relationships with a navigable link
 *
 * One spec covers both: the AC-063 create round-trip is what puts a relationship on the panel, so the
 * same assertions prove AC-062's "panel displays typed relationships with a navigable link". The edge's
 * AUDIT leg (AC-063 "the edge creation is audited") is not observable from the browser and stays on its
 * existing unit proof (TraceabilityTests: create audits `Relationship.Created`) — this spec proves the
 * user-facing round-trip the InMemory/unit suites cannot.
 *
 * Setup that isn't under test (two topics) is seeded via the API with a real captured bearer; the UI is
 * reserved for the create action being asserted (the scenario.ts convention).
 *
 * ⚠ AUTHORED, NOT YET RUN against the isolated `-p acmpe2e` stack. Selectors + strings are taken from the
 * components (TraceabilityPanel.tsx, CreateRelationshipDialog.tsx, ArtifactPicker.tsx) and en.json, and
 * the custom `Select`→option interaction mirrors core-loop.spec's owner picker — but the first live run
 * is the verification. Likely first-run adjustment points are commented `// VERIFY:`.
 */

const TRACE_PANEL = 'Traceability'; // TraceabilityPanel aside aria-label (trace.panel.title)

async function secretarySession(page: Page): Promise<{ bearer: string; secretary: ApiMember }> {
  await loginAs(page, 'secretary');
  const bearer = await captureBearer(page);
  // Force JIT provisioning before reading the directory (POST /members/me is idempotent) — same guard
  // dnd-and-failures.spec uses to avoid racing the SPA's async login-time provision.
  await page.request.post('/api/members/me', { headers: { Authorization: bearer } });
  const me = (await apiMembers(page.request, bearer)).find((m) => m.role === 'Secretary');
  if (!me) throw new Error('[e2e] secretary member not provisioned after login');
  return { bearer, secretary: me };
}

/** The panel's "Relationships" aside on a detail page. */
function panel(page: Page) {
  return page.getByRole('complementary', { name: TRACE_PANEL }); // <aside aria-label> → role=complementary
}

test.describe('P17b — traceability (AC-062 / AC-063)', () => {
  test('a typed edge created via the UI appears on both endpoints, with a navigable link', async ({ page, request }) => {
    test.setTimeout(120_000);
    const { bearer } = await secretarySession(page);
    const stamp = Date.now();
    const source = await apiCreateTopic(request, bearer, `P17b Trace Source ${stamp}`);
    const target = await apiCreateTopic(request, bearer, `P17b Trace Target ${stamp}`);

    // AC-062 baseline: a fresh topic's panel is honestly empty (no fabricated edges).
    await page.goto(`/topics/${source.key}`);
    await expect(panel(page)).toContainText('No typed relationships or dependencies yet.');

    // AC-063 create: open the dialog and link source → target. Relationship type is left at its default
    // ("References") — AC-063 tests typed-edge creation + bidirectional reflection, not a specific type,
    // so this avoids the extra Select interaction. The DerivedFrom example in the AC text is one type
    // among many the same flow produces.
    await panel(page).getByRole('button', { name: 'Add relationship' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // ArtifactPicker = two custom Selects (Type → Artifact). Button-opens-options, like core-loop's picker.
    await dialog.getByRole('button', { name: 'Type' }).click(); // VERIFY: custom Select button name = ariaLabel
    await page.getByRole('option', { name: 'Topic', exact: true }).click();
    await dialog.getByRole('button', { name: 'Artifact' }).click();
    await page.getByRole('option', { name: new RegExp(target.key) }).click(); // option label = `${key} — ${title}`

    const [createRes] = await Promise.all([
      page.waitForResponse((r) => r.url().endsWith('/api/traceability') && r.request().method() === 'POST'),
      dialog.getByRole('button', { name: 'Create relationship' }).click(),
    ]);
    expect(createRes.status()).toBe(201);
    await expect(dialog).toHaveCount(0);

    // Source panel now shows the edge to the target, as a navigable link (AC-062: navigable link).
    const sourceEdge = panel(page).locator('.tp-item', { hasText: target.key });
    await expect(sourceEdge).toBeVisible();
    await expect(sourceEdge.locator('a.tp-item-link')).toHaveAttribute('href', new RegExp(target.key));

    // AC-063 "both endpoints": the reverse edge shows on the target's panel too.
    await page.goto(`/topics/${target.key}`);
    const targetEdge = panel(page).locator('.tp-item', { hasText: source.key });
    await expect(targetEdge).toBeVisible();
    await expect(targetEdge.locator('a.tp-item-link')).toHaveAttribute('href', new RegExp(source.key));
  });
});
