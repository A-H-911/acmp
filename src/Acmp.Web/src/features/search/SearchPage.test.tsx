import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { SearchPage } from './SearchPage';
import type { SearchGroup } from '../../api/search';

vi.mock('../../api/search', async (orig) => ({
  ...(await orig<typeof import('../../api/search')>()),
  useSearch: vi.fn(),
}));
import { useSearch } from '../../api/search';

const mockSearch = useSearch as unknown as Mock;

function result(over: Partial<ReturnType<typeof useSearch>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useSearch>;
}

function setup(url = '/search?q=keycloak') {
  return render(<MemoryRouter initialEntries={[url]}><SearchPage /></MemoryRouter>);
}

const GROUPS: SearchGroup[] = [
  {
    type: 'Topics',
    items: [
      {
        type: 'Topics', id: 't1', key: 'TOP-2026-001',
        title: { en: 'Adopt Keycloak', ar: 'اعتماد كيكلوك' },
        excerpt: 'Consolidate IAM onto Keycloak.', status: 'Backlog',
        deepLink: '/topics/TOP-2026-001',
      },
    ],
  },
  {
    type: 'Decisions',
    items: [
      {
        type: 'Decisions', id: 'd1', key: 'DECN-2026-004',
        title: { en: 'Keycloak adopted', ar: 'اعتماد كيكلوك' },
        excerpt: 'The committee approved…', status: 'Issued',
        deepLink: '/decisions/DECN-2026-004',
      },
    ],
  },
];

describe('SearchPage', () => {
  beforeEach(() => mockSearch.mockReset());

  it('renders grouped hits with deep links, keys, excerpts and status (AC-060)', () => {
    mockSearch.mockReturnValue(result({ data: GROUPS }));
    setup();

    // Group headings fall back to the raw type when i18n is not loaded.
    expect(screen.getByText('Topics')).toBeTruthy();
    expect(screen.getByText('Decisions')).toBeTruthy();

    const link = screen.getByRole('link', { name: /Adopt Keycloak/ });
    expect(link.getAttribute('href')).toBe('/topics/TOP-2026-001');
    expect(screen.getByText('TOP-2026-001')).toBeTruthy();
    expect(screen.getByText('Consolidate IAM onto Keycloak.')).toBeTruthy();
    expect(screen.getByText('Backlog')).toBeTruthy();
    expect(screen.getByText('DECN-2026-004')).toBeTruthy();
  });

  it('shows the prompt when there is no query and never queries', () => {
    mockSearch.mockReturnValue(result({}));
    setup('/search');
    expect(screen.queryByText('TOP-2026-001')).toBeNull();
    // useSearch is called with the empty term (the hook itself disables the request).
    expect(mockSearch).toHaveBeenCalledWith('');
  });

  it('shows a loading state while searching', () => {
    mockSearch.mockReturnValue(result({ isLoading: true }));
    setup();
    expect(screen.queryByText('TOP-2026-001')).toBeNull();
  });

  it('shows an error state whose retry action actually refetches', async () => {
    const user = userEvent.setup();
    const refetch = vi.fn();
    mockSearch.mockReturnValue(result({ isError: true, refetch }));
    setup();
    expect(screen.getByRole('button', { name: /retry/i })).toBeTruthy();
    // Asserting the button EXISTS left onRetry uninvoked, so the retry path had
    // never run. coverage-v8 v4 named that line; v3 credited it for the JSX.
    await user.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('shows an empty state when nothing matches', () => {
    mockSearch.mockReturnValue(result({ data: [] }));
    setup();
    expect(screen.queryByText('TOP-2026-001')).toBeNull();
  });
});
