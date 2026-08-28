import { describe, it, expect, afterEach, vi } from 'vitest';
import { cleanup, screen } from '@testing-library/react';
import axe from 'axe-core';
import SessionPreviewPage from './SessionPreviewPage';
import { renderWithAuth } from '../../test/render';
import { makeQueryWrapper, stubFetch } from '../../test/queryHarness';

/*
 * FR-165 / DEC-086 — the Chairman/Secretary preview of a chosen presenter's session view.
 *
 * These run the REAL useSessionPreview against a stubbed fetch, so the page is asserted against what the
 * server actually says — including 204, which is not data but a state: "there is nothing to preview",
 * and which must render the SAME emptiness the presenter would see rather than an error.
 */

const SESSION = {
  accessExpiresAt: '2026-09-02T10:30:00Z',
  meetingKey: 'MTG-2026-019',
  meetingTitle: 'Weekly Architecture Committee',
  slotStart: '2026-09-01T07:40:00Z',
  slotEnd: '2026-09-01T07:55:00Z',
  itemNumber: 3,
  itemCount: 6,
  timeboxMinutes: 15,
  topicKey: 'TOP-2026-022',
  topicTitle: 'Standardize API pagination across public services',
  topicSummary: 'A proposal to mandate cursor-based pagination for all public-facing APIs.',
  materials: [
    { id: 'a-1', fileName: 'proposal.pdf', contentType: 'application/pdf', sizeBytes: 2_500_000 },
  ],
};

// renderWithAuth already supplies the MemoryRouter, so the target is passed through its `route`
// option — nesting a second Router throws, and the search string is what this page reads.
function renderPage(search = '?meetingId=m-1&topicId=t-1') {
  const { wrapper: Query } = makeQueryWrapper();
  return renderWithAuth(<Query><SessionPreviewPage /></Query>, {
    roles: ['secretary'],
    route: `/session/preview${search}`,
  });
}

describe('SessionPreviewPage (FR-165)', () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("renders the targeted presenter's slot behind a preview banner", async () => {
    stubFetch(() => ({ jsonBody: SESSION }));
    renderPage();

    expect(await screen.findByText(/this is what the presenter sees/i)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /standardize api pagination/i })).toBeInTheDocument();
    expect(screen.getByText('MTG-2026-019', { exact: false })).toBeInTheDocument();
    // The banner carries the TARGET's expiry — a Secretary's own access never expires, so this value
    // can only have come from the previewed person's row.
    expect(screen.getByText(/expires.*2 Sep 2026|expires.*Sep 2, 2026/i)).toBeInTheDocument();
  });

  // DEC-086 d2 — LISTED, NEVER OPENABLE.
  //
  // ⚠ ASSERTED AS THE ABSENCE OF A CONTROL, not as the presence of a disabled one, because those are
  // different promises: a disabled button says "later", nothing says "not here". The material must still
  // be VISIBLE, so the pair of assertions is what pins the behaviour — a page that dropped materials
  // entirely would satisfy the negative half on its own.
  it('lists materials without an open control', async () => {
    stubFetch(() => ({ jsonBody: SESSION }));
    renderPage();

    expect(await screen.findByText('proposal.pdf')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /open/i })).not.toBeInTheDocument();
    expect(screen.getByText(/materials are listed here, not opened/i)).toBeInTheDocument();
  });

  // EMPTY-STATE PARITY. A 204 means no presenter on that slot, a cancelled meeting, or an agenda item
  // that no longer exists — all of which are exactly what the PRESENTER's page shows. Rendering an error
  // here would tell a Secretary something had gone wrong when the truth is that nothing is assigned.
  it('renders the same emptiness the presenter would see when the server sends 204', async () => {
    stubFetch(() => ({ status: 204 }));
    renderPage();

    expect(await screen.findByText(/nothing to preview/i)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /standardize api pagination/i })).not.toBeInTheDocument();
  });

  // A hand-typed URL with no target never reaches the server: the query is disabled, so this asserts the
  // page does not sit on a spinner forever waiting for a request it will not make.
  it('shows the empty state when the URL carries no target', async () => {
    stubFetch(() => ({ jsonBody: SESSION }));
    renderPage('');

    expect(await screen.findByText(/nothing to preview/i)).toBeInTheDocument();
  });

  it('is axe-clean', async () => {
    stubFetch(() => ({ jsonBody: SESSION }));
    const { container } = renderPage();
    await screen.findByText(/this is what the presenter sees/i);

    const results = await axe.run(container, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });

    expect(results.violations).toEqual([]);
  });
});
