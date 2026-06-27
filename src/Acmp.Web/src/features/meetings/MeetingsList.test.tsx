import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import axe from 'axe-core';
import { MeetingsList } from './MeetingsList';
import { renderWithAuth } from '../../test/render';
import type { MeetingSummary } from '../../api/meetings';

vi.mock('../../api/meetings', () => ({ useMeetings: vi.fn() }));
import { useMeetings } from '../../api/meetings';

const mockMeetings = useMeetings as unknown as Mock;

function result(over: Partial<ReturnType<typeof useMeetings>>) {
  mockMeetings.mockReturnValue({ data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over });
}

const MEETINGS: MeetingSummary[] = [
  {
    id: 'm1', key: 'MTG-2026-019', title: 'Q2 Architecture Review', scheduledStart: '2026-06-30T09:00:00Z',
    scheduledEnd: '2026-06-30T10:30:00Z', status: 'Scheduled', chairName: 'Sara K', itemCount: 4, agendaStatus: 'Draft',
  },
  {
    id: 'm2', key: 'MTG-2026-012', title: 'Identity strategy session', scheduledStart: '2026-05-12T09:00:00Z',
    scheduledEnd: '2026-05-12T10:00:00Z', status: 'Held', chairName: 'Sara K', itemCount: 3, agendaStatus: 'Published',
  },
];

describe('MeetingsList (P6c)', () => {
  beforeEach(() => mockMeetings.mockReset());

  it('renders a row per meeting with localized status and agenda status', () => {
    result({ data: MEETINGS });
    renderWithAuth(<MeetingsList />, { roles: ['secretary'] });
    expect(screen.getByText('Q2 Architecture Review')).toBeInTheDocument();
    expect(screen.getByText('Identity strategy session')).toBeInTheDocument();
    expect(screen.getByText('Scheduled')).toBeInTheDocument();
    expect(screen.getByText('Held')).toBeInTheDocument();
    expect(screen.getByText('Draft')).toBeInTheDocument();
    expect(screen.getByText('Published')).toBeInTheDocument();
  });

  it('links each meeting to its agenda builder route', () => {
    result({ data: MEETINGS });
    renderWithAuth(<MeetingsList />, { roles: ['secretary'] });
    expect(screen.getByRole('link', { name: 'Q2 Architecture Review' })).toHaveAttribute('href', '/meetings/MTG-2026-019');
  });

  it('shows an empty state when no meetings are scheduled', () => {
    result({ data: [] });
    renderWithAuth(<MeetingsList />, { roles: ['secretary'] });
    expect(screen.getByText('No meetings scheduled yet')).toBeInTheDocument();
  });

  it('shows the loading state on first fetch', () => {
    result({ isLoading: true });
    renderWithAuth(<MeetingsList />, { roles: ['secretary'] });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows an error state with retry on failure', () => {
    result({ isError: true });
    renderWithAuth(<MeetingsList />, { roles: ['secretary'] });
    expect(screen.getByText("Couldn't load meetings")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    result({ data: MEETINGS });
    renderWithAuth(<MeetingsList />, { roles: ['secretary'] });
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
