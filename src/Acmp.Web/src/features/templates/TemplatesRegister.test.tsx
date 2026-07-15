import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TemplatesRegister } from './TemplatesRegister';
import { renderWithAuth } from '../../test/render';
import type { CommitteeRole } from '../../auth/roles';
import type { TemplateSummary } from '../../api/templates';

const fns = vi.hoisted(() => ({ deprecate: vi.fn().mockResolvedValue(undefined) }));
vi.mock('../../api/templates', async (orig) => ({
  ...(await orig<typeof import('../../api/templates')>()),
  useTemplates: vi.fn(),
  useDeprecateTemplate: () => ({ mutateAsync: fns.deprecate, isPending: false }),
}));
vi.mock('./TemplateFormDialog', () => ({
  TemplateFormDialog: (p: { open: boolean; template?: TemplateSummary }) =>
    p.open ? <div data-testid="form" data-edit={p.template?.key ?? ''} /> : null,
}));
import { useTemplates } from '../../api/templates';
const mockList = useTemplates as unknown as Mock;

const ROWS: TemplateSummary[] = [
  { id: 't1', key: 'TPL-1', name: { en: 'Standard Topic', ar: 'ع1' }, targetType: 'Topic', status: 'Active', version: 3, createdAt: '2026-02-12T09:00:00Z', updatedAt: null },
  { id: 't2', key: 'TPL-2', name: { en: 'Old MoM', ar: 'ع2' }, targetType: 'MinutesOfMeeting', status: 'Deprecated', version: 1, createdAt: '2025-11-01T09:00:00Z', updatedAt: '2026-01-01T09:00:00Z' },
];

function listResult(over: Partial<ReturnType<typeof useTemplates>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useTemplates>;
}
function withRows(items = ROWS) {
  mockList.mockReturnValue(listResult({ data: { items, total: items.length, page: 1, pageSize: 200, totalPages: 1 } }));
}
function setup(roles: CommitteeRole[] = ['secretary']) {
  return renderWithAuth(<TemplatesRegister />, { roles });
}
const lastParams = () => mockList.mock.calls.at(-1)![0];

describe('TemplatesRegister (P15e)', () => {
  beforeEach(() => {
    mockList.mockReset();
    fns.deprecate.mockClear();
    withRows();
  });

  it('shows the loading skeleton while fetching', () => {
    mockList.mockReturnValue(listResult({ isLoading: true }));
    setup();
    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
  });

  it('shows an error state on failure', () => {
    mockList.mockReturnValue(listResult({ isError: true }));
    setup();
    expect(screen.getByText('Couldn’t load the templates')).toBeInTheDocument();
  });

  it('shows the empty state + New template for a manager', () => {
    withRows([]);
    setup(['administrator']);
    expect(screen.getByText('No templates yet')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'New template' }).length).toBeGreaterThan(0);
  });

  it('shows the filtered-empty variant with Clear filters when a filter is active (m13)', async () => {
    const user = userEvent.setup();
    withRows([]);
    setup();
    await user.click(screen.getByRole('button', { name: /Type/ }));
    await user.click(screen.getByRole('menuitemradio', { name: 'ADR' }));
    // A filter is active + no rows → the "no matches" variant, not "No templates yet".
    expect(screen.getByText('No matching templates')).toBeInTheDocument();
    expect(screen.queryByText('No templates yet')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Clear filters' }));
    expect(lastParams().targetType).toBeUndefined();
  });

  it('renders the real backend enum values (Active/Deprecated) with type + version', () => {
    setup();
    expect(screen.getByText('Standard Topic')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Deprecated')).toBeInTheDocument();
    expect(screen.getByText('Minutes of meeting')).toBeInTheDocument();
    expect(screen.getByText('v3')).toBeInTheDocument();
  });

  it('gates New/Edit/Deprecate to managers — a member sees none', () => {
    setup(['member']);
    expect(screen.queryByRole('button', { name: 'New template' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Edit/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Deprecate/ })).not.toBeInTheDocument();
  });

  it('passes the type + status filters to the query', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Type/ }));
    await user.click(screen.getByRole('menuitemradio', { name: 'ADR' }));
    await user.click(screen.getByRole('button', { name: /Status/ }));
    await user.click(screen.getByRole('menuitemradio', { name: 'Active' }));
    expect(lastParams()).toMatchObject({ targetType: 'Adr', statuses: ['Active'] });
  });

  it('opens the edit form seeded with the row', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Edit Standard Topic' }));
    expect(screen.getByTestId('form')).toHaveAttribute('data-edit', 'TPL-1');
  });

  it('opens the create form from New template', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'New template' }));
    expect(screen.getByTestId('form')).toHaveAttribute('data-edit', '');
  });

  it('deprecates via a confirm dialog; an already-deprecated row cannot be deprecated', async () => {
    const user = userEvent.setup();
    setup();
    // The deprecated row's action button is disabled.
    expect(screen.getByRole('button', { name: 'Deprecate Old MoM' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Deprecate Standard Topic' }));
    await user.click(screen.getByRole('button', { name: 'Deprecate' }));
    expect(fns.deprecate).toHaveBeenCalledWith('t1');
  });
});
