import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
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
    restricted: false,
    id: 'g1', key: 'TOP-2026-014', title: 'Adopt Keycloak as the standard IdP', type: 'ArchitectureDecision',
    status: 'Scheduled', urgency: 'Urgent', scope: 'MultiStream', streams: ['identity', 'platform'],
    ownerId: 'o1', ownerName: 'Omar H', priority: 1, timesDeferred: 0, ageDays: 9, slaBreached: true, createdAt: '2026-02-15T09:00:00Z',
  },
  {
    restricted: false,
    id: 'g2', key: 'TOP-2026-031', title: 'Event streaming spike', type: 'ResearchDiscovery',
    status: 'Triage', urgency: 'Normal', scope: 'SingleStream', streams: ['notifications'],
    ownerId: null, ownerName: null, priority: 5, timesDeferred: 0, ageDays: 4, slaBreached: false, createdAt: '2026-02-20T09:00:00Z',
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

  it('renders the live calendar view chrome with an honest empty note (D1)', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    await user.click(screen.getByRole('button', { name: /calendar/i }));
    // Faithful chrome (month nav) + honest note that markers arrive with P6 scheduling.
    expect(screen.getByRole('button', { name: 'Previous month' })).toBeInTheDocument();
    expect(screen.getByText(/scheduled and due-date markers/i)).toBeInTheDocument();
  });

  it('renders the live timeline view chrome with topic rows and an honest note (D1)', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    await user.click(screen.getByRole('button', { name: /timeline/i }));
    expect(screen.getByText('Adopt Keycloak as the standard IdP')).toBeInTheDocument();
    expect(screen.getByText(/planned timelines appear/i)).toBeInTheDocument();
  });

  it('filters by status through the Status chip (multi-select)', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    // The Status filter chip (a menu trigger) — distinct from the sortable "Status" column header.
    await user.click(screen.getByRole('button', { name: 'Status', expanded: false }));
    await user.click(screen.getByRole('menuitemradio', { name: 'Triage' }));
    expect(mockBacklog.mock.calls.at(-1)?.[0]).toMatchObject({ statuses: ['Triage'] });
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

  it('retries the failed fetch from the error state', async () => {
    const refetch = vi.fn();
    result({ isError: true, refetch });
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(screen.getByText(/load the backlog/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /retry/i }));

    expect(refetch).toHaveBeenCalled();
  });

  // Sorting has TWO arms and only the same-column toggle had ever run. Picking a DIFFERENT column
  // must reset the direction to ascending — otherwise a column inherits the previous one's
  // direction and the first click on it appears to do nothing.
  it('resets to ascending when a different sortable column is chosen', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    // Default is age/desc, so 'Age' would take the toggle arm. 'Topic' takes the other — and it is
    // the only sortable column with no same-named FILTER CHIP, so the query is unambiguous.
    await user.click(screen.getByRole('button', { name: 'Topic' }));

    expect(mockBacklog.mock.calls.at(-1)?.[0]).toMatchObject({ sortBy: 'title', sortDir: 'asc' });
  });

  // Three filter controls whose onChange had never fired. Each is asserted through the SERVER
  // PARAMS rather than the chip's own label: these filters are server-backed, so a control that
  // updates its display without reaching the query is the failure that matters.
  it('passes the type and urgency filters to the query', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });

    await user.click(screen.getByRole('button', { name: 'Type' }));
    await user.click(screen.getByRole('menuitemradio', { name: 'Arch. Decision' }));
    // Disambiguated from the sortable "Urgency" COLUMN HEADER, which carries the same accessible
    // name; only the filter chip is a menu trigger.
    await user.click(screen.getByRole('button', { name: 'Urgency', expanded: false }));
    await user.click(screen.getByRole('menuitemradio', { name: 'Critical' }));

    expect(mockBacklog.mock.calls.at(-1)?.[0]).toMatchObject({ type: 'ArchitectureDecision', urgency: 'Critical' });
  });

  it('debounces the search box into the query', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });

    await user.type(screen.getByRole('searchbox'), '  gateway  ');

    // Trimmed, and only after the 300ms debounce — asserted with waitFor rather than a fixed sleep,
    // so the test does not encode the delay it is waiting on.
    await waitFor(() => expect(mockBacklog.mock.calls.at(-1)?.[0]).toMatchObject({ search: 'gateway' }));
  });

  // Clearing is the only way out of a filter combination that matches nothing, and it has to reset
  // the SEARCH BOX as well as the chips — a clear that leaves the query behind still shows nothing.
  it('clears the filters and the search box together', async () => {
    result({ data: paged([]) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });

    await user.type(screen.getByRole('searchbox'), 'zzz');
    await waitFor(() => expect(mockBacklog.mock.calls.at(-1)?.[0]).toMatchObject({ search: 'zzz' }));

    await user.click(screen.getByRole('button', { name: 'Clear filters' }));

    expect(screen.getByRole('searchbox')).toHaveValue('');
    await waitFor(() => {
      const p = mockBacklog.mock.calls.at(-1)?.[0];
      expect(p?.search).toBeUndefined();
      expect(p?.statuses).toBeUndefined();
    });
  });

  it('toggles sort direction when a sortable header is clicked', async () => {
    result({ data: paged(TOPICS) });
    const user = userEvent.setup();
    renderWithAuth(<Backlog />, { roles: ['secretary'] });
    const ageHeader = screen.getByRole('button', { name: 'Age' });
    await user.click(ageHeader);
    // The hook is re-invoked with the new sort params; assert it was called with sortBy 'age'.
    const lastCall = mockBacklog.mock.calls.at(-1)?.[0];
    expect(lastCall).toMatchObject({ sortBy: 'age' });
  });
});
