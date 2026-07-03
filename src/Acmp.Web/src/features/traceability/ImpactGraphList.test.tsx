import { describe, it, expect } from 'vitest';
import { screen, within } from '@testing-library/react';
import axe from 'axe-core';
import { renderWithAuth } from '../../test/render';
import { ImpactGraphList } from './ImpactGraphList';
import type { ImpactGraph } from '../../api/traceability';

const graph: ImpactGraph = {
  focusType: 'Topic',
  focusId: 'F',
  depth: 2,
  partial: false,
  nodes: [
    { type: 'Topic', id: 'F', key: 'TOP-2026-014', title: 'Keycloak', tier: 0, blocked: false, streams: ['Identity'] },
    { type: 'Topic', id: 'U', key: 'TOP-22', title: 'Pagination', tier: -1, blocked: false, streams: ['Platform'] },
    { type: 'Action', id: 'A', key: 'ACT-9', title: 'Do it', tier: 1, blocked: true, streams: ['Payments'] },
    { type: 'Adr', id: 'R', key: 'ADR-3', title: 'Record', tier: 1, blocked: false, streams: [] },
  ],
  edges: [
    { source: 'dep', rel: 'DependsOn', fromType: 'Topic', fromId: 'U', toType: 'Topic', toId: 'F', isBlocker: false, isCrossStream: true },
    { source: 'rel', rel: 'Produces', fromType: 'Topic', fromId: 'F', toType: 'Action', toId: 'A', isBlocker: true, isCrossStream: true },
    { source: 'rel', rel: 'RecordedAs', fromType: 'Topic', fromId: 'F', toType: 'Adr', toId: 'R', isBlocker: false, isCrossStream: false },
  ],
};

describe('ImpactGraphList (P10f)', () => {
  it('renders a role=tree of depth-indented rows with the focus-path crumb', () => {
    renderWithAuth(<ImpactGraphList graph={graph} focusKey="TOP-2026-014" />);
    const tree = screen.getByRole('tree');
    const items = within(tree).getAllByRole('treeitem');
    expect(items).toHaveLength(3); // non-focus nodes
    expect(screen.getByText('Path')).toBeInTheDocument();
    expect(screen.getByText('Identity')).toBeInTheDocument(); // focus stream
    // navigable far nodes render as anchor treeitems carrying the detail href
    const hrefs = items.map((i) => i.getAttribute('href'));
    expect(hrefs).toContain('/actions/ACT-9');
    expect(hrefs).toContain('/topics/TOP-22');
  });

  it('shows a Blocked pill + stream code, and renders routeless rows as non-anchor rows', () => {
    renderWithAuth(<ImpactGraphList graph={graph} focusKey="TOP-2026-014" />);
    expect(screen.getByText('Blocked')).toBeInTheDocument();
    expect(screen.getByText('Payments')).toBeInTheDocument(); // cross-stream code on the blocked action
    // ADR has no route → its treeitem is a plain div (no href), never a dead link
    const adrRow = screen.getByText('ADR-3').closest('[role="treeitem"]')!;
    expect(adrRow.tagName).toBe('DIV');
    expect(adrRow).not.toHaveAttribute('href');
  });

  it('is axe-clean', async () => {
    const { container } = renderWithAuth(<ImpactGraphList graph={graph} focusKey="TOP-2026-014" />);
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
