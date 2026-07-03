import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { createElement } from 'react';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { renderWithAuth } from '../../test/render';
import { TraceabilityPanel } from './TraceabilityPanel';

vi.mock('../../api/traceability', async (orig) => ({
  ...(await orig<typeof import('../../api/traceability')>()),
  useArtifactRelationships: vi.fn(),
}));
vi.mock('../../api/dependencies', async (orig) => ({
  ...(await orig<typeof import('../../api/dependencies')>()),
  useArtifactDependencies: vi.fn(),
}));
// Dialogs have their own tests; expose whether they are open via a marker.
vi.mock('./CreateRelationshipDialog', () => ({ CreateRelationshipDialog: ({ open }: { open: boolean }) => (open ? createElement('div', null, 'REL_DIALOG_OPEN') : null) }));
vi.mock('../dependencies/CreateDependencyDialog', () => ({ CreateDependencyDialog: ({ open }: { open: boolean }) => (open ? createElement('div', null, 'DEP_DIALOG_OPEN') : null) }));
import { useArtifactRelationships } from '../../api/traceability';
import { useArtifactDependencies } from '../../api/dependencies';

const mockRels = useArtifactRelationships as unknown as Mock;
const mockDeps = useArtifactDependencies as unknown as Mock;

const relEdge = { id: 'r1', relType: 'Produces', direction: 'Outgoing', otherType: 'Action', otherId: 'x', otherKey: 'ACT-9', otherTitle: 'Do it', notes: null };
const depEdge = { id: 'd1', key: 'DPN-1', otherType: 'Topic', otherId: 'y', otherKey: 'TOP-22', otherTitle: 'Pagination', kind: 'DependsOn', status: 'Open', isBlocker: true };

function relState(over: Record<string, unknown> = {}) {
  mockRels.mockReturnValue({ data: { outgoing: [], incoming: [] }, isLoading: false, isError: false, ...over });
}
function depState(over: Record<string, unknown> = {}) {
  mockDeps.mockReturnValue({ data: { outbound: [], inbound: [] }, isLoading: false, isError: false, ...over });
}
const props = { traceType: 'Topic' as const, depType: 'Topic' as const, id: 'g1', artifactKey: 'TOP-2026-014', title: 'Gateway' };

describe('TraceabilityPanel (P10e, AC-062)', () => {
  beforeEach(() => {
    mockRels.mockReset();
    mockDeps.mockReset();
  });

  it('merges relationship + dependency edges into direction groups with a navigable link + blocked pill', () => {
    relState({ data: { outgoing: [relEdge], incoming: [] } });
    depState({ data: { outbound: [depEdge], inbound: [] } });
    renderWithAuth(<TraceabilityPanel {...props} />, { roles: ['member'] });
    // Dependency DependsOn (outbound) → Upstream, with a Blocked pill + link to the far topic.
    expect(screen.getByText('Upstream')).toBeInTheDocument();
    expect(screen.getByText('Blocked')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /TOP-22/ })).toHaveAttribute('href', '/topics/TOP-22');
    // Relationship Produces (outgoing) → Downstream.
    expect(screen.getByText('Downstream')).toBeInTheDocument();
    expect(screen.getByText('Produces')).toBeInTheDocument();
  });

  it('shows the empty state when there are no edges', () => {
    relState();
    depState();
    renderWithAuth(<TraceabilityPanel {...props} />, { roles: ['member'] });
    expect(screen.getByText('No typed relationships or dependencies yet.')).toBeInTheDocument();
  });

  it('shows the loading state', () => {
    relState({ data: undefined, isLoading: true });
    depState({ data: undefined, isLoading: true });
    renderWithAuth(<TraceabilityPanel {...props} />, { roles: ['member'] });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows the error state', () => {
    relState({ data: undefined, isError: true });
    depState();
    renderWithAuth(<TraceabilityPanel {...props} />, { roles: ['member'] });
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('offers Add relationship + Add dependency to a secretary and opens each dialog', async () => {
    relState();
    depState();
    const user = userEvent.setup();
    renderWithAuth(<TraceabilityPanel {...props} />, { roles: ['secretary'] });
    await user.click(screen.getByRole('button', { name: /Add dependency/ }));
    expect(screen.getByText('DEP_DIALOG_OPEN')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Add relationship/ }));
    expect(screen.getByText('REL_DIALOG_OPEN')).toBeInTheDocument();
  });

  it('hides the add buttons from a plain member', () => {
    relState();
    depState();
    renderWithAuth(<TraceabilityPanel {...props} />, { roles: ['member'] });
    expect(screen.queryByRole('button', { name: /Add relationship/ })).not.toBeInTheDocument();
  });

  it('omits the dependency query + Add dependency button when no depType (e.g. a Risk)', () => {
    relState({ data: { outgoing: [relEdge], incoming: [] } });
    depState();
    renderWithAuth(<TraceabilityPanel traceType="Risk" id="g2" artifactKey="RSK-1" title="R" />, { roles: ['secretary'] });
    expect(mockDeps).toHaveBeenCalledWith(undefined, 'g2');
    expect(screen.queryByRole('button', { name: /Add dependency/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Add relationship/ })).toBeInTheDocument();
  });

  it('renders a routeless target as plain text (no dead link)', () => {
    relState({ data: { outgoing: [{ ...relEdge, otherType: 'Adr', otherKey: 'ADR-3' }], incoming: [] } });
    depState();
    renderWithAuth(<TraceabilityPanel {...props} />, { roles: ['member'] });
    expect(screen.queryByRole('link', { name: /ADR-3/ })).not.toBeInTheDocument();
    expect(screen.getByText('ADR-3')).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    relState({ data: { outgoing: [relEdge], incoming: [] } });
    depState({ data: { outbound: [depEdge], inbound: [] } });
    const { container } = renderWithAuth(<TraceabilityPanel {...props} />, { roles: ['secretary'] });
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
