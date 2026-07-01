import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { ActionPage } from './ActionPage';
import { ApiError } from '../../api/apiClient';
import type { ActionDetail } from '../../api/actions';

vi.mock('../../api/actions', async (orig) => ({
  ...(await orig<typeof import('../../api/actions')>()),
  useAction: vi.fn(),
}));
import { useAction } from '../../api/actions';

const mockAction = useAction as unknown as Mock;

const DETAIL: ActionDetail = {
  id: 'a1', key: 'ACT-2026-033', title: { en: 'Risk review: dual-running auth', ar: 'مراجعة مخاطر' },
  status: 'InProgress', priority: 'High', ownerUserId: 'kc-noura', ownerName: 'Noura Q',
  dueDate: '2026-06-23T00:00:00Z', isOverdue: true, progressPct: 40,
  sourceType: 'Risk', sourceId: 'g1', sourceKey: 'RSK-2026-006', meetingKey: 'MTG-2026-018',
  description: { en: 'This action tracks a concrete, owned task.', ar: 'يتتبّع هذا الإجراء مهمة محدّدة.' },
  blockedReason: null, completionNote: null, cancelReason: null,
  completedByUserId: null, completedAt: null, verifiedByUserId: null, verifiedByName: null, verifiedAt: null,
  createdAt: '2026-02-14T00:00:00Z',
};

function result(over: Partial<ReturnType<typeof useAction>>) {
  mockAction.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over } as ReturnType<typeof useAction>);
}
function setup(path = '/actions/ACT-2026-033') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/actions/:key" element={<ActionPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('ActionPage (P8b)', () => {
  beforeEach(() => mockAction.mockReset());

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    setup();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('shows a not-found empty state on 404', () => {
    result({ isError: true, error: new ApiError(404, undefined) });
    setup();
    expect(screen.getByText('Action not found')).toBeInTheDocument();
  });

  it('shows a retryable error state on a non-404 failure', async () => {
    const refetch = vi.fn();
    result({ isError: true, error: new ApiError(500, undefined), refetch });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('renders header, facts, progress, description and related from the DTO', () => {
    result({ data: DETAIL });
    setup();
    expect(screen.getByRole('heading', { name: 'Risk review: dual-running auth' })).toBeInTheDocument();
    expect(screen.getAllByText('ACT-2026-033').length).toBeGreaterThanOrEqual(2); // key chip + Action ID fact
    expect(screen.getAllByText('In Progress').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Noura Q')).toBeInTheDocument();
    expect(screen.getByText('Linked artifact')).toBeInTheDocument();
    // RSK key appears twice: the Linked-artifact fact + the related-panel source row.
    expect(screen.getAllByText('RSK-2026-006').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('40%')).toBeInTheDocument();
    expect(screen.getByText('This action tracks a concrete, owned task.')).toBeInTheDocument();
    // Related: source (display-only) + a real meeting deep-link.
    const meeting = screen.getByRole('link', { name: /MTG-2026-018/ });
    expect(meeting).toHaveAttribute('href', '/meetings/MTG-2026-018');
  });

  it('shows the overdue badge when the action is overdue', () => {
    result({ data: DETAIL });
    setup();
    expect(screen.getByText('Overdue')).toBeInTheDocument();
  });

  it('omits the description card when there is no description', () => {
    result({ data: { ...DETAIL, description: null } });
    setup();
    expect(screen.queryByText('Description')).not.toBeInTheDocument();
  });

  it('omits the related panel and shows an em dash when there are no links', () => {
    result({ data: { ...DETAIL, sourceKey: null, meetingKey: null } });
    setup();
    expect(screen.queryByRole('complementary', { name: 'Related' })).not.toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument(); // Linked artifact fact
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    result({ data: DETAIL });
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
