import { describe, it, expect, afterEach, vi } from 'vitest';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import SessionPage from './SessionPage';
import { renderWithAuth } from '../../test/render';
import { makeQueryWrapper, stubFetch } from '../../test/queryHarness';

/*
 * FR-159 / AC-092 / DEC-037 — the GUEST / PRESENTER SHELL.
 *
 * These run the REAL useMySession against a stubbed fetch, so the page is asserted against what the
 * server actually says — including the two answers that are not data: 204 (you are not presenting)
 * and a 401 carrying `access_expired`, which is the AC's "the page states that access has ended".
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
    { id: 'a-2', fileName: 'sequence.svg', contentType: 'image/svg+xml', sizeBytes: 4096 },
  ],
};

function renderPage() {
  const { wrapper: Query } = makeQueryWrapper();
  return renderWithAuth(<Query><SessionPage /></Query>, { roles: ['guest'] });
}

describe('SessionPage (FR-159 / AC-092)', () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('renders the banner, the topic card, the slot and the materials', async () => {
    stubFetch(() => ({ jsonBody: SESSION }));
    renderPage();

    expect(await screen.findByText(/presenter access/i)).toBeInTheDocument();
    // The expiry is the server's own stored value, formatted — not "after the meeting".
    expect(screen.getByText(/expires.*2 Sep 2026|expires.*Sep 2, 2026/i)).toBeInTheDocument();

    expect(screen.getByText('TOP-2026-022')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /standardize api pagination/i })).toBeInTheDocument();
    expect(screen.getByText(/cursor-based pagination/i)).toBeInTheDocument();

    expect(screen.getByText('Weekly Architecture Committee')).toBeInTheDocument();
    expect(screen.getByText(/MTG-2026-019 · Item 3 of 6/)).toBeInTheDocument();
    expect(screen.getByText(/15 min/)).toBeInTheDocument();

    expect(screen.getByText('proposal.pdf')).toBeInTheDocument();
    expect(screen.getByText('sequence.svg')).toBeInTheDocument();
  });

  it('opens a material through a freshly fetched pre-signed URL', async () => {
    const user = userEvent.setup();
    const open = vi.fn();
    vi.stubGlobal('open', open);
    const spy = stubFetch((url) =>
      url.includes('/session/materials/')
        ? { jsonBody: { url: 'https://storage.example/presigned' } }
        : { jsonBody: SESSION },
    );
    renderPage();

    await user.click(await screen.findByRole('button', { name: /proposal\.pdf/i }));

    // Fetched ON CLICK — a URL embedded at render time would already have expired.
    expect(spy.mock.calls.some((c) => String(c[0]).includes('/session/materials/a-1'))).toBe(true);
    expect(open).toHaveBeenCalledWith('https://storage.example/presigned', '_blank', 'noopener,noreferrer');
  });

  it('says so when a material cannot be opened instead of doing nothing', async () => {
    const user = userEvent.setup();
    stubFetch((url) =>
      url.includes('/session/materials/') ? { status: 404 } : { jsonBody: SESSION },
    );
    renderPage();

    await user.click(await screen.findByRole('button', { name: /proposal\.pdf/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not be opened/i);
  });

  it('shows the not-presenting state when the server answers 204', async () => {
    stubFetch(() => ({ status: 204 }));
    renderPage();

    expect(await screen.findByText(/you are not presenting/i)).toBeInTheDocument();
  });

  it('STATES THAT ACCESS HAS ENDED when the server refuses with access_expired (AC-092)', async () => {
    // The refusal carries its reason in a header, and the reason is what makes this terminal: no new
    // token can fix an ended window, so the page must say so rather than retry.
    stubFetch(() => ({ status: 401, headers: { 'X-Acmp-Auth-Reason': 'access_expired' } }));
    renderPage();

    expect(await screen.findByText(/your access has ended/i)).toBeInTheDocument();
    expect(screen.queryByText(/presenter access/i)).not.toBeInTheDocument();
  });

  it('is axe-clean', async () => {
    stubFetch(() => ({ jsonBody: SESSION }));
    const { container } = renderPage();
    await screen.findByText(/presenter access/i);

    const results = await axe.run(container, { rules: { region: { enabled: false } } });

    expect(results.violations).toEqual([]);
  });
});
