import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Backlog } from './Backlog';
import { renderWithAuth } from '../../test/render';
import type { TopicSummary } from '../../api/topics';

vi.mock('../../api/topics', () => ({ useBacklog: vi.fn() }));
import { useBacklog } from '../../api/topics';

const mockBacklog = useBacklog as unknown as Mock;

function result(over: Partial<ReturnType<typeof useBacklog>>) {
  mockBacklog.mockReturnValue({ data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over });
}

const TOPICS: TopicSummary[] = [
  {
    id: 'g1', key: 'TOP-2026-014', title: 'Adopt Keycloak as the standard IdP', type: 'ArchitectureDecision',
    status: 'Scheduled', urgency: 'Urgent', scope: 'MultiStream', streams: ['identity', 'platform'],
    ownerId: 'o1', ownerName: 'Omar H', priority: 1, ageDays: 9, slaBreached: true, createdAt: '2026-02-15T09:00:00Z',
  },
  {
    id: 'g2', key: 'TOP-2026-031', title: 'Event streaming spike', type: 'ResearchDiscovery',
    status: 'Triage', urgency: 'Normal', scope: 'SingleStream', streams: ['notifications'],
    ownerId: null, ownerName: null, priority: 5, ageDays: 4, slaBreached: false, createdAt: '2026-02-20T09:00:00Z',
  },
];

const paged = (items: TopicSummary[]) => ({ items, total: items.length, page: 1, pageSize: 25, totalPages: 1 });

describe('Backlog (P5b)', () => {
  beforeEach(() => mockBacklog.mockReset());

  it('renders a table row per topic with localized type and status', () => {
    result({ data: paged(TOPICS) });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });

    expect(screen.getByText('Adopt Keycloak as the standard IdP')).toBeInTheDocument();
    expect(screen.getByText('Event streaming spike')).toBeInTheDocument();
    expect(screen.getByText('Arch. Decision')).toBeInTheDocument();
    expect(screen.getByText('Research')).toBeInTheDocument();
    expect(screen.getByText('Scheduled')).toBeInTheDocument();
    expect(screen.getByText('Triage')).toBeInTheDocument();
  });

  it('exposes the eight backlog columns', () => {
    result({ data: paged(TOPICS) });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(screen.getAllByRole('columnheader')).toHaveLength(8);
  });

  it('links each topic title to its detail route', () => {
    result({ data: paged(TOPICS) });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(screen.getByRole('link', { name: 'Adopt Keycloak as the standard IdP' })).toHaveAttribute('href', '/topics/TOP-2026-014');
  });

  it('flags an SLA-breached topic with an accessible aging note (AC-057)', () => {
    result({ data: paged(TOPICS) });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    // Only the breached row carries the SLA note.
    expect(screen.getByText(/Past its review SLA/)).toBeInTheDocument();
  });

  it('marks a non-Normal urgency topic as Urgent and an unassigned owner', () => {
    result({ data: paged(TOPICS) });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    // "Urgent" shows twice on the urgent row: the urgency cell + the visually-hidden title marker.
    expect(screen.getAllByText('Urgent').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
  });

  it('shows the count summary', () => {
    result({ data: paged(TOPICS) });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument();
  });

  it('switches to the list view and still renders topics', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    await user.click(screen.getByRole('button', { name: /list/i }));
    expect(screen.getByText('Adopt Keycloak as the standard IdP')).toBeInTheDocument();
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument();
  });

  it('renders an honest coming-soon shell for the calendar view (no P5 data)', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    await user.click(screen.getByRole('button', { name: /calendar/i }));
    expect(screen.getByText('View coming soon')).toBeInTheDocument();
  });

  it('disables the Stream and Owner filters this slice', () => {
    result({ data: paged(TOPICS) });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(screen.getByRole('button', { name: 'Stream' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Owner' })).toBeDisabled();
  });

  it('shows the empty state with clear-filters and new-topic actions', () => {
    result({ data: paged([]) });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(screen.getByText('No topics match these filters')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Clear filters' })).toBeInTheDocument();
    // Header + empty-state both offer "New topic".
    expect(screen.getAllByRole('link', { name: 'New topic' }).length).toBeGreaterThanOrEqual(1);
  });

  it('shows the loading state on first fetch', () => {
    result({ isLoading: true });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows an error state with retry on failure', () => {
    result({ isError: true });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(screen.getByText(/load the backlog/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('toggles sort direction when a sortable header is clicked', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    const ageHeader = screen.getByRole('button', { name: /age/i });
    await user.click(ageHeader);
    // The hook is re-invoked with the new sort params; assert it was called with sortBy 'age'.
    const lastCall = mockBacklog.mock.calls.at(-1)?.[0];
    expect(lastCall).toMatchObject({ sortBy: 'age' });
  });
});
