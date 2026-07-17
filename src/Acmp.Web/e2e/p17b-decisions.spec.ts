import { test, expect } from '@playwright/test';
import { roleSession } from './apiHelpers';
import { apiCreateTopic, apiRecordDecision, apiIssueDecision } from './scenario';

/*
 * P17b — live real-stack leg for the decision supersession AC (bin (a) of the P17b-0 triage):
 *   AC-028  superseding an Issued decision creates a successor and marks the prior Superseded; both readable.
 *
 * The prior Issued decision is seeded via the API record→issue endpoints (there is no create-decision UI —
 * the record button is a disabled stub; that gap is the fresh-session decision-issue UI build). A cheap seed
 * uses a `Deferred` outcome with no vote, so the issue gates (AC-029 downstream-link, SoD-3) are skipped.
 *
 * Supersede is Chairman-only (DecisionChairApprove), so a single chairman session drives the whole flow. The
 * successor's DECN- key is deliberately NOT rendered as a back-link on the prior (the DTO carries only its
 * Guid — flagged in DecisionPage), so the SupersededByDecisionId back-link stays on its API/unit proof; this
 * live leg proves the round-trip: the successor is created + readable, and the prior flips to Superseded with
 * its content intact.
 */

test.describe('P17b — decisions (AC-028)', () => {
  test('a chairman supersedes an issued decision; the prior becomes Superseded and both stay readable', async ({ page }) => {
    test.setTimeout(120_000);
    // The supersede dialog is tall (full successor body); give the viewport room so its footer button is
    // on-screen — the modal is fixed-position and does not page-scroll to an off-viewport footer.
    await page.setViewportSize({ width: 1280, height: 1400 });
    const { bearer } = await roleSession(page, 'chairman', 'Chairman');
    const topic = await apiCreateTopic(page.request, bearer, `P17b Decision Supersede ${Date.now()}`);

    const priorStatement = `Prior statement kept intact ${Date.now()}`;
    const prior = await apiRecordDecision(page.request, bearer, {
      topicId: topic.id,
      title: 'P17b prior decision',
      statement: priorStatement,
      rationale: 'Prior rationale.',
    });
    await apiIssueDecision(page.request, bearer, prior.id); // Draft → Issued

    // Chairman opens the issued decision and supersedes it through the dialog (outcome left at its default).
    await page.goto(`/decisions/${prior.key}`);
    await page.getByRole('button', { name: 'Supersede' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('textbox', { name: 'Title', exact: true }).fill('P17b successor decision');
    await dialog.getByRole('textbox', { name: 'Decision statement' }).fill('Successor statement — corrects the prior.');
    await dialog.getByRole('textbox', { name: 'Rationale' }).fill('Successor rationale.');
    await dialog.getByRole('textbox', { name: 'Reason for superseding' }).fill('Superseding to correct an error.');

    const [res] = await Promise.all([
      page.waitForResponse((r) => r.url().includes(`/api/decisions/${prior.id}/supersede`) && r.request().method() === 'POST'),
      dialog.getByRole('button', { name: 'Supersede decision' }).click(),
    ]);
    expect(res.status()).toBe(201);
    const successor = (await res.json()) as { key: string };

    // The dialog navigates to the NEW decision — it is readable.
    await expect(page).toHaveURL(new RegExp(`/decisions/${successor.key}`));
    await expect(page.getByRole('heading', { name: 'P17b successor decision' })).toBeVisible();

    // The prior decision is now Superseded, still readable, its original content unchanged.
    await page.goto(`/decisions/${prior.key}`);
    await expect(page.getByText('This decision has been superseded')).toBeVisible();
    await expect(page.getByText(priorStatement)).toBeVisible();
  });
});
