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
// ⚠ The stub exposes onClose as a control. Modelling only the OPEN half leaves the register's own
// dismiss handlers unreachable by construction while still looking like coverage.
vi.mock('./TemplateFormDialog', () => ({
  TemplateFormDialog: (p: { open: boolean; template?: TemplateSummary; onClose: () => void }) =>
    p.open ? (
      <div data-testid="form" data-edit={p.template?.key ?? ''}>
        <button type="button" onClick={p.onClose}>CLOSE_FORM</button>
      </div>
    ) : null,
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

  it('retries the failed fetch from the error state', async () => {
    const refetch = vi.fn();
    mockList.mockReturnValue(listResult({ isError: true, refetch }));
    setup();
    expect(screen.getByText('Couldn’t load the templates')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /retry|try again/i }));

    expect(refetch).toHaveBeenCalled();
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

  it('deprecates via a confirm dialog; an already-deprecated row cannot be deprecated', async () => {
    const user = userEvent.setup();
    setup();
    // The deprecated row's action button is disabled.
    expect(screen.getByRole('button', { name: 'Deprecate Old MoM' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Deprecate Standard Topic' }));
    await user.click(screen.getByRole('button', { name: 'Deprecate' }));
    expect(fns.deprecate).toHaveBeenCalledWith('t1');
  });

  // Backing OUT of a destructive confirm is the arm that had never run. Deprecation is the one
  // irreversible action on this register, so "changed my mind" must actually work.
  it('cancels the deprecate confirmation without deprecating', async () => {
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: 'Deprecate Standard Topic' }));
    expect(screen.getByText('Deprecate this template')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByText('Deprecate this template')).not.toBeInTheDocument();
    expect(fns.deprecate).not.toHaveBeenCalled();
  });

  // Both form dialogs could be opened and neither dismissed. They are SEPARATE mounts with separate
  // handlers — create clears a boolean, edit clears the row it was seeded with — so one closing
  // proves nothing about the other, and a stuck edit dialog holds a stale row.
  it.each([
    ['create', 'New template', ''],
    ['edit', 'Edit Standard Topic', 'TPL-1'],
  ])('opens and dismisses the %s form', async (_kind, opener, expectedSeed) => {
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: opener }));
    expect(screen.getByTestId('form')).toHaveAttribute('data-edit', expectedSeed);

    await user.click(screen.getByRole('button', { name: 'CLOSE_FORM' }));

    expect(screen.queryByTestId('form')).not.toBeInTheDocument();
  });

  // The empty state carries its OWN New template CTA, a second call site from the header's.
  it('opens the create form from the empty state', async () => {
    withRows([]);
    const user = userEvent.setup();
    setup(['administrator']);

    // TWO of them render here — the header's and the empty state's. Click the empty state's, the
    // one a first-time user actually meets, rather than whichever the query happens to return first.
    const ctas = screen.getAllByRole('button', { name: 'New template' });
    expect(ctas).toHaveLength(2);
    await user.click(ctas[1]);

    expect(screen.getByTestId('form')).toHaveAttribute('data-edit', '');
  });
});
