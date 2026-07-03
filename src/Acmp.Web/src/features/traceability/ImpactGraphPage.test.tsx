import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { ImpactGraphPage } from './ImpactGraphPage';

vi.mock('../../api/traceability', async (orig) => ({
  ...(await orig<typeof import('../../api/traceability')>()),
  useTraceGraph: vi.fn(),
  useArtifactRelationships: vi.fn(),
}));
vi.mock('../../api/dependencies', async (orig) => ({
  ...(await orig<typeof import('../../api/dependencies')>()),
  useArtifactDependencies: vi.fn(),
}));
vi.mock('../../api/topics', () => ({ useTopicDetail: vi.fn() }));
vi.mock('../../api/decisions', () => ({ useDecision: vi.fn() }));
vi.mock('../../api/actions', () => ({ useAction: vi.fn() }));
vi.mock('../../api/risks', () => ({ useRisk: vi.fn() }));
// Children have their own tests — reduce them to markers so this test is about the container.
vi.mock('./ImpactGraph', () => ({ ImpactGraph: ({ focusKey, focusTitle }: { focusKey: string; focusTitle: string }) => <div>GRAPH {focusKey} {focusTitle}</div> }));
vi.mock('./ImpactGraphList', () => ({ ImpactGraphList: () => <div>LIST_VIEW</div> }));
vi.mock('./RelationshipsAside', () => ({ RelationshipsAside: ({ total, onOpenGraph }: { total: number; onOpenGraph: () => void }) => <aside>ASIDE {total}<button onClick={onOpenGraph}>reveal</button></aside> }));

import { useTraceGraph, useArtifactRelationships } from '../../api/traceability';
import { useArtifactDependencies } from '../../api/dependencies';
import { useTopicDetail } from '../../api/topics';
import { useDecision } from '../../api/decisions';
import { useAction } from '../../api/actions';
import { useRisk } from '../../api/risks';

const mockGraph = useTraceGraph as unknown as Mock;
const mockRels = useArtifactRelationships as unknown as Mock;
const mockDeps = useArtifactDependencies as unknown as Mock;
const mocks = { Topic: useTopicDetail, Decision: useDecision, Action: useAction, Risk: useRisk } as Record<string, unknown>;

const graphData = { focusType: 'Topic', focusId: 'g1', depth: 2, nodes: [], edges: [], partial: false };
function graphState(over: Record<string, unknown> = {}) {
  mockGraph.mockReturnValue({ data: graphData, isLoading: false, isError: false, ...over });
}

function renderPage(route: string, state?: unknown) {
  return render(
    <MemoryRouter initialEntries={[state ? { pathname: route, state } : route]}>
      <Routes>
        <Route path="/" element={<div>HOME</div>} />
        <Route path="/traceability/:type/:key" element={<ImpactGraphPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('ImpactGraphPage (P10f)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    graphState();
    mockRels.mockReturnValue({ data: { outgoing: [], incoming: [] }, isLoading: false, isError: false });
    mockDeps.mockReturnValue({ data: { outbound: [], inbound: [] }, isLoading: false, isError: false });
    (Object.values(mocks) as Mock[]).forEach((m) => m.mockReturnValue({ data: undefined, isLoading: false, isError: false }));
    Element.prototype.scrollIntoView = vi.fn();
  });

  it('warm path: uses the router-state GUID (no cold fetch) and renders the graph', () => {
    renderPage('/traceability/Topic/TOP-2026-014', { focusId: 'g1', focusTitle: 'Keycloak' });
    expect(screen.getByRole('heading', { name: 'Traceability & dependencies' })).toBeInTheDocument();
    expect(screen.getByText(/GRAPH TOP-2026-014 Keycloak/)).toBeInTheDocument();
    expect(useTraceGraph).toHaveBeenLastCalledWith('Topic', 'g1', 2);
    expect(useTopicDetail).toHaveBeenCalledWith(undefined); // cold fetch short-circuited
  });

  it('cold path: resolves a Topic GUID + title by key', () => {
    (mocks.Topic as Mock).mockReturnValue({ data: { id: 'g9', title: 'Cold Topic' }, isLoading: false, isError: false });
    renderPage('/traceability/Topic/TOP-9');
    expect(useTopicDetail).toHaveBeenCalledWith('TOP-9');
    expect(useTraceGraph).toHaveBeenLastCalledWith('Topic', 'g9', 2);
    expect(screen.getByText(/GRAPH TOP-9 Cold Topic/)).toBeInTheDocument();
  });

  it('cold path: resolves a Decision with a bilingual (LocalizedText) title', () => {
    (mocks.Decision as Mock).mockReturnValue({ data: { id: 'd1', title: { en: 'Approve', ar: 'موافقة' } }, isLoading: false, isError: false });
    renderPage('/traceability/Decision/DECN-8');
    expect(screen.getByText(/GRAPH DECN-8 Approve/)).toBeInTheDocument();
  });

  it('a valid but non-focusable type (no warm state) shows the unsupported hint', () => {
    renderPage('/traceability/Meeting/MTG-1');
    expect(screen.getByText("Open the impact graph from the artifact's own page.")).toBeInTheDocument();
    expect(useTraceGraph).toHaveBeenLastCalledWith(undefined, undefined, 2); // graph query stays disabled
  });

  it('an invalid :type redirects home', () => {
    renderPage('/traceability/Nonsense/X');
    expect(screen.getByText('HOME')).toBeInTheDocument();
  });

  it('shows loading, error, and partial states', () => {
    graphState({ data: undefined, isLoading: true });
    const { unmount } = renderPage('/traceability/Topic/TOP-1', { focusId: 'g1', focusTitle: 'K' });
    expect(screen.getByRole('status')).toBeInTheDocument();
    unmount();

    graphState({ data: undefined, isError: true });
    const r2 = renderPage('/traceability/Topic/TOP-1', { focusId: 'g1', focusTitle: 'K' });
    expect(screen.getByRole('alert')).toBeInTheDocument();
    r2.unmount();

    graphState({ data: { ...graphData, partial: true } });
    renderPage('/traceability/Topic/TOP-1', { focusId: 'g1', focusTitle: 'K' });
    expect(screen.getByText(/Showing a partial graph/)).toBeInTheDocument();
  });

  it('toggles the List view and re-reveals the graph from the aside', async () => {
    const user = userEvent.setup();
    renderPage('/traceability/Topic/TOP-1', { focusId: 'g1', focusTitle: 'K' });
    await user.click(screen.getByRole('button', { name: /^List$/ }));
    expect(screen.getByText('LIST_VIEW')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'reveal' })); // aside onOpenGraph → back to graph
    expect(screen.getByText(/GRAPH/)).toBeInTheDocument();
  });

  it('changes depth and toggles the highlight filters', async () => {
    const user = userEvent.setup();
    renderPage('/traceability/Topic/TOP-1', { focusId: 'g1', focusTitle: 'K' });
    await user.click(screen.getByRole('button', { name: '3' }));
    expect(useTraceGraph).toHaveBeenLastCalledWith('Topic', 'g1', 3);
    const blocked = screen.getByRole('button', { name: /Blocked work/ });
    expect(blocked).toHaveAttribute('aria-pressed', 'false');
    await user.click(blocked);
    expect(blocked).toHaveAttribute('aria-pressed', 'true');
  });

  it('reflects the aside loading state from the panel reads', () => {
    mockRels.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    renderPage('/traceability/Topic/TOP-1', { focusId: 'g1', focusTitle: 'K' });
    expect(screen.getByText(/ASIDE/)).toBeInTheDocument(); // aside renders (loading handled inside it)
  });

  it('is axe-clean', async () => {
    const { container } = renderPage('/traceability/Topic/TOP-1', { focusId: 'g1', focusTitle: 'Keycloak' });
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
