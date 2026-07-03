import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import { Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { renderWithAuth } from '../../test/render';
import { DependencyPage } from './DependencyPage';
import { ApiError } from '../../api/apiClient';
import type { DependencyDetail } from '../../api/dependencies';

vi.mock('../../api/dependencies', async (orig) => ({
  ...(await orig<typeof import('../../api/dependencies')>()),
  useDependency: vi.fn(),
}));
import { useDependency } from '../../api/dependencies';

const mockDep = useDependency as unknown as Mock;

const DETAIL: DependencyDetail = {
  id: 'd1', key: 'DPN-2026-001',
  fromType: 'Topic', fromId: 'a', fromKey: 'TOP-2026-014', fromTitle: 'Gateway migration',
  toType: 'System', toId: 'b', toKey: 'SVC-Gateway', toTitle: 'API Gateway',
  kind: 'BlockedBy', status: 'Open', isBlocker: true, note: 'Pagination standard must land first.',
  createdAt: '2026-03-01T00:00:00Z',
};

function result(over: Partial<ReturnType<typeof useDependency>>) {
  mockDep.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over } as ReturnType<typeof useDependency>);
}
function setup(path = '/dependencies/DPN-2026-001') {
  return renderWithAuth(
    <Routes>
      <Route path="/dependencies/:key" element={<DependencyPage />} />
    </Routes>,
    { route: path },
  );
}

describe('DependencyPage (P10e)', () => {
  beforeEach(() => mockDep.mockReset());

  it('renders the edge facts, note, blocked chip, and endpoints from the DTO', () => {
    result({ data: DETAIL });
    setup();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('TOP-2026-014');
    expect(screen.getAllByText('Blocked by').length).toBeGreaterThan(0);
    expect(screen.getByText('Pagination standard must land first.')).toBeInTheDocument();
    // The From endpoint (Topic) is navigable; the To endpoint (System) has no route → plain text.
    expect(screen.getByRole('link', { name: /TOP-2026-014/ })).toHaveAttribute('href', '/topics/TOP-2026-014');
    expect(screen.queryByRole('link', { name: /SVC-Gateway/ })).not.toBeInTheDocument();
    expect(screen.getAllByText('SVC-Gateway').length).toBeGreaterThan(0); // facts value + related endpoint
  });

  it('omits the note card when there is no note', () => {
    result({ data: { ...DETAIL, note: null } });
    setup();
    expect(screen.queryByText('Notes')).not.toBeInTheDocument();
  });

  it('shows a not-found empty state on 404', () => {
    result({ isError: true, error: new ApiError(404, undefined) });
    setup();
    expect(screen.getByText('Dependency not found')).toBeInTheDocument();
  });

  it('shows a retryable error state on a non-404 failure', () => {
    result({ isError: true, error: new ApiError(500, undefined) });
    setup();
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    setup();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    result({ data: DETAIL });
    const { container } = setup();
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
