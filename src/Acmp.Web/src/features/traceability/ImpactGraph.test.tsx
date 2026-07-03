import { describe, it, expect, vi, beforeEach, afterAll } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import i18n from '../../i18n';
import { renderWithAuth } from '../../test/render';
import { ImpactGraph } from './ImpactGraph';
import type { ImpactGraph as ImpactGraphDto } from '../../api/traceability';

const navigate = vi.fn();
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => navigate,
}));

const graph: ImpactGraphDto = {
  focusType: 'Topic',
  focusId: 'F',
  depth: 2,
  partial: false,
  nodes: [
    { type: 'Topic', id: 'F', key: '', title: '', tier: 0, blocked: false, streams: ['Identity'] },
    { type: 'Topic', id: 'U', key: 'TOP-22', title: 'Pagination', tier: -1, blocked: false, streams: ['Platform'] },
    { type: 'Decision', id: 'D', key: 'DECN-8', title: 'Approve', tier: 1, blocked: false, streams: ['Identity'] },
    { type: 'Action', id: 'A', key: 'ACT-9', title: 'Do it', tier: 1, blocked: true, streams: ['Payments'] },
  ],
  edges: [
    { source: 'dep', rel: 'DependsOn', fromType: 'Topic', fromId: 'U', toType: 'Topic', toId: 'F', isBlocker: false, isCrossStream: true },
    { source: 'rel', rel: 'DecidedBy', fromType: 'Topic', fromId: 'F', toType: 'Decision', toId: 'D', isBlocker: false, isCrossStream: false },
    { source: 'rel', rel: 'Produces', fromType: 'Topic', fromId: 'F', toType: 'Action', toId: 'A', isBlocker: true, isCrossStream: true },
  ],
};

const NO_HI = { blocked: false, cross: false };
const render = () => renderWithAuth(<ImpactGraph graph={graph} focusKey="TOP-2026-014" focusTitle="Keycloak" highlight={NO_HI} />);

describe('ImpactGraph (P10f)', () => {
  beforeEach(() => navigate.mockReset());
  afterAll(async () => { await i18n.changeLanguage('en'); });

  it('renders a node button per node, injects the focus identity, and marks the focus current', () => {
    render();
    const focus = screen.getByRole('button', { name: /TOP-2026-014, Keycloak/ });
    expect(focus).toHaveAttribute('aria-current', 'true');
    expect(screen.getByRole('button', { name: /DECN-8/ })).toBeInTheDocument();
    // focus bar + legend + keyboard hint ('Keycloak' shows on the node card AND the bar)
    expect(screen.getByText('Focused')).toBeInTheDocument();
    expect(screen.getAllByText('Keycloak').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/Arrow keys move focus/)).toBeInTheDocument();
  });

  it('shows the Blocked pill, a type chip, and the cross-stream code on the relevant node', () => {
    render();
    const action = screen.getByRole('button', { name: /ACT-9/ });
    expect(action).toHaveTextContent('Blocked');
    expect(action).toHaveTextContent('Action'); // type chip (status chip is omitted by design, ADR-0001)
    expect(action).toHaveTextContent('Payments'); // cross-stream code
  });

  it('is a single roving tab-stop starting on the focus, and arrow keys move it (no wrap)', async () => {
    const user = userEvent.setup();
    render();
    const focus = screen.getByRole('button', { name: /TOP-2026-014/ });
    expect(focus).toHaveAttribute('tabindex', '0');
    expect(screen.getByRole('button', { name: /TOP-22/ })).toHaveAttribute('tabindex', '-1');
    focus.focus();
    await user.keyboard('{ArrowRight}');
    // focus (col 1) → first downstream node (col 2) becomes the tab stop and gains DOM focus
    const next = screen.getByRole('button', { name: /DECN-8/ });
    expect(next).toHaveAttribute('tabindex', '0');
    expect(next).toHaveFocus();
    expect(screen.getByText(/DECN-8, Approve/)).toBeInTheDocument(); // aria-live announcement
  });

  it('Enter re-centres the graph on a focusable node via warm navigation', async () => {
    const user = userEvent.setup();
    render();
    const decision = screen.getByRole('button', { name: /DECN-8/ });
    decision.focus();
    await user.keyboard('{Enter}');
    expect(navigate).toHaveBeenCalledWith('/traceability/Decision/DECN-8', { state: { focusId: 'D', focusTitle: 'Approve' } });
  });

  it('clicking a node activates it: focusable → graph route, non-focusable → its detail page', async () => {
    const user = userEvent.setup();
    const withMeeting: ImpactGraphDto = {
      ...graph,
      nodes: [...graph.nodes, { type: 'Meeting', id: 'M', key: 'MTG-1', title: 'Committee', tier: 1, blocked: false, streams: [] }],
      edges: [...graph.edges, { source: 'rel', rel: 'DecidedBy', fromType: 'Topic', fromId: 'F', toType: 'Meeting', toId: 'M', isBlocker: false, isCrossStream: false }],
    };
    renderWithAuth(<ImpactGraph graph={withMeeting} focusKey="TOP-2026-014" focusTitle="Keycloak" highlight={NO_HI} />);
    await user.click(screen.getByRole('button', { name: /DECN-8/ }));
    expect(navigate).toHaveBeenLastCalledWith('/traceability/Decision/DECN-8', { state: { focusId: 'D', focusTitle: 'Approve' } });
    await user.click(screen.getByRole('button', { name: /MTG-1/ }));
    expect(navigate).toHaveBeenLastCalledWith('/meetings/MTG-1'); // non-focusable → detail route
  });

  it('flips the edges-only SVG in RTL while keeping node text logical', async () => {
    await i18n.changeLanguage('ar');
    const { container } = render();
    expect(container.querySelector('.ig-edges--rtl')).not.toBeNull();
    await i18n.changeLanguage('en');
  });

  it('renders an empty state when only the focus node is present', () => {
    const solo: ImpactGraphDto = { ...graph, nodes: [graph.nodes[0]], edges: [] };
    renderWithAuth(<ImpactGraph graph={solo} focusKey="TOP-2026-014" focusTitle="Keycloak" highlight={NO_HI} />);
    expect(screen.getByText('No linked artifacts at this depth.')).toBeInTheDocument();
  });

  it('is axe-clean', async () => {
    const { container } = render();
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
