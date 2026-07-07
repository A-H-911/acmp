import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, cleanup } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { MeetingOverview } from './MeetingOverview';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import type { MeetingDetail, Agenda } from '../../api/meetings';
import type { Member } from '../../api/members';

vi.mock('../../api/meetings', async () => {
  const actual = await vi.importActual<typeof import('../../api/meetings')>('../../api/meetings');
  return { ...actual, useMeetingDetail: vi.fn() };
});
vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));

import { useMeetingDetail } from '../../api/meetings';
import { useMembers } from '../../api/members';

const mockDetail = useMeetingDetail as unknown as Mock;
const mockMembers = useMembers as unknown as Mock;

const itemsAgenda: Agenda = {
  id: 'a1', key: 'AGD-2026-019', status: 'Published', version: 2, totalTimeboxMinutes: 35, publishedAt: '2026-06-29T09:00:00Z',
  items: [
    { topicId: 't1', topicKey: 'TOP-2026-014', topicTitle: 'Adopt Keycloak', urgent: true, order: 0, timeboxMinutes: 20, presenterUserId: null, presenterName: null, outcome: 'Pending', actualMinutes: 0 },
    { topicId: 't2', topicKey: 'TOP-2026-031', topicTitle: 'Event streaming spike', urgent: false, order: 1, timeboxMinutes: 15, presenterUserId: null, presenterName: null, outcome: 'Pending', actualMinutes: 0 },
  ],
};
const draftAgenda: Agenda = { id: 'a1', key: 'AGD-2026-019', status: 'Draft', version: 1, totalTimeboxMinutes: 0, publishedAt: null, items: [] };

const MEMBERS: Member[] = [
  { publicId: 'u1', keycloakUserId: 'kc-fixture', fullName: 'Sara K', email: 's@x.com', role: 'Chairman', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
  { publicId: 'u2', keycloakUserId: 'kc-fixture', fullName: 'Omar R', email: 'o@x.com', role: 'Member', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
  { publicId: 'u3', keycloakUserId: 'kc-fixture', fullName: 'Lina M', email: 'l@x.com', role: 'Reviewer', status: 'Active', isActive: true, isVotingEligible: false, streams: [] },
];

function meeting(over: Partial<MeetingDetail> = {}): MeetingDetail {
  return {
    id: 'm1', key: 'MTG-2026-019', title: 'Q2 Architecture Review', committeeId: 'c1',
    scheduledStart: '2026-06-30T09:00:00Z', scheduledEnd: '2026-06-30T10:30:00Z',
    status: 'Scheduled', type: 'Regular', mode: 'InPerson', location: null, joinUrl: null, chairUserId: 'u1', chairName: 'Sara K',
    startedAt: null, heldAt: null, agenda: itemsAgenda, attendance: [], discussions: [],
    ...over,
  };
}

function setup(detail: MeetingDetail | undefined = meeting()) {
  mockDetail.mockReturnValue({ data: detail });
  mockMembers.mockReturnValue({ data: MEMBERS });
  render(
    <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
      <MemoryRouter initialEntries={['/meetings/MTG-2026-019']}>
        <Routes>
          <Route path="/meetings/:key" element={<MeetingOverview />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('MeetingOverview (P6a)', () => {
  beforeEach(() => [mockDetail, mockMembers].forEach((m) => m.mockReset()));

  it('ready meeting: no banner, agenda preview, readiness met, quorum heuristic', () => {
    setup();
    // ready phase shows no lifecycle banner.
    expect(screen.queryByText('Agenda not published')).not.toBeInTheDocument();
    // agenda preview card (reused AgendaPreview) lists items.
    expect(screen.getByText('Adopt Keycloak')).toBeInTheDocument();
    // readiness rows.
    expect(screen.getByText('Agenda published')).toBeInTheDocument();
    expect(screen.getByText('Topics scheduled')).toBeInTheDocument();
    // 2 voting-eligible of 3 active → needed = floor(2/2)+1 = 2 of 2.
    expect(screen.getByText('2 of 2')).toBeInTheDocument();
  });

  it('notReady meeting: banner + no-agenda gate (AgendaPreview empty branch never reached)', () => {
    setup(meeting({ status: 'Scheduled', agenda: draftAgenda }));
    expect(screen.getByText('Agenda not published')).toBeInTheDocument();
    expect(screen.getByText('No agenda yet')).toBeInTheDocument();
    expect(screen.queryByText('Adopt Keycloak')).not.toBeInTheDocument();
  });

  it('renders the quick links to each sub-route', () => {
    setup();
    expect(screen.getByRole('link', { name: 'Build the agenda' })).toHaveAttribute('href', '/meetings/MTG-2026-019/agenda');
    expect(screen.getByRole('link', { name: 'Open the live workspace' })).toHaveAttribute('href', '/meetings/MTG-2026-019/notes');
    expect(screen.getByRole('link', { name: 'Review minutes' })).toHaveAttribute('href', '/meetings/MTG-2026-019/minutes');
    expect(screen.getByRole('link', { name: 'View recording' })).toHaveAttribute('href', '/meetings/MTG-2026-019/recording');
  });

  it('shows the Join Webex link only for an https joinUrl (P13)', () => {
    // https URL → link rendered.
    setup(meeting({ mode: 'Remote', joinUrl: 'https://acmp.webex.com/meet/abc' }));
    expect(screen.getByRole('link', { name: 'Join Webex meeting' })).toHaveAttribute(
      'href',
      'https://acmp.webex.com/meet/abc',
    );

    // null joinUrl → no link.
    cleanup();
    setup(meeting({ mode: 'InPerson', joinUrl: null }));
    expect(screen.queryByRole('link', { name: 'Join Webex meeting' })).not.toBeInTheDocument();

    // non-https (defensive) → suppressed.
    cleanup();
    setup(meeting({ mode: 'Remote', joinUrl: 'javascript:alert(1)' }));
    expect(screen.queryByRole('link', { name: 'Join Webex meeting' })).not.toBeInTheDocument();
  });

  it('renders nothing until the detail resolves', () => {
    const { container } = (() => {
      mockDetail.mockReturnValue({ data: undefined });
      mockMembers.mockReturnValue({ data: [] });
      return render(
        <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
          <MemoryRouter initialEntries={['/meetings/MTG-2026-019']}>
            <Routes>
              <Route path="/meetings/:key" element={<MeetingOverview />} />
            </Routes>
          </MemoryRouter>
        </AcmpAuthContext.Provider>,
      );
    })();
    expect(container.querySelector('.mt-overview')).toBeNull();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
