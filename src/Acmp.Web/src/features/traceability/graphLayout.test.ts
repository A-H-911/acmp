import { describe, it, expect } from 'vitest';
import type { ImpactGraph } from '../../api/traceability';
import {
  buildListRows,
  layoutGraph,
  nextFocusIndex,
  nodeKey,
  tierDir,
  NODE_W,
} from './graphLayout';

/** A small focus graph: 1 upstream, focus, 2 downstream (one blocked, two cross-stream). */
function fixture(over: Partial<ImpactGraph> = {}): ImpactGraph {
  return {
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
    ...over,
  };
}

const NO_HI = { blocked: false, cross: false };

describe('graphLayout helpers', () => {
  it('nodeKey / tierDir', () => {
    expect(nodeKey('Topic', 'F')).toBe('Topic:F');
    expect(tierDir(-2)).toBe('up');
    expect(tierDir(0)).toBe('related');
    expect(tierDir(3)).toBe('down');
  });

  it('nextFocusIndex clamps (no wrap) and handles Home/End/unknown/missing', () => {
    const order = ['a', 'b', 'c'];
    expect(nextFocusIndex(order, 'a', 'ArrowLeft')).toBe(0); // clamp low
    expect(nextFocusIndex(order, 'c', 'ArrowRight')).toBe(2); // clamp high
    expect(nextFocusIndex(order, 'b', 'ArrowDown')).toBe(2);
    expect(nextFocusIndex(order, 'b', 'ArrowUp')).toBe(0);
    expect(nextFocusIndex(order, 'b', 'Home')).toBe(0);
    expect(nextFocusIndex(order, 'b', 'End')).toBe(2);
    expect(nextFocusIndex(order, 'b', 'x')).toBe(1); // unknown key → stay
    expect(nextFocusIndex(order, 'missing', 'ArrowRight')).toBe(0);
    expect(nextFocusIndex([], 'missing', 'ArrowRight')).toBe(-1);
  });
});

describe('layoutGraph', () => {
  it('injects the focus key/title, sizes the canvas, and orders nodes column-then-row', () => {
    const l = layoutGraph(fixture(), NO_HI, 'TOP-2026-014', 'Adopt Keycloak');
    const focus = l.nodes.find((n) => n.isFocus)!;
    expect(focus.node.key).toBe('TOP-2026-014');
    expect(focus.node.title).toBe('Adopt Keycloak');
    expect(l.canvasW).toBeGreaterThan(NODE_W);
    expect(l.canvasH).toBeGreaterThan(0);
    expect(l.isEmpty).toBe(false);
    // three tiers → upstream node first (col 0), focus (col 1), then the two downstream (col 2).
    expect(l.navOrder[0]).toBe('Topic:U');
    expect(l.navOrder[1]).toBe('Topic:F');
    expect(new Set(l.navOrder)).toEqual(new Set(['Topic:U', 'Topic:F', 'Decision:D', 'Action:A']));
    expect(l.tierLabels.map((t) => t.tier)).toEqual([-1, 0, 1]);
  });

  it('builds one bezier per edge and marks cross-stream nodes from incident edges', () => {
    const l = layoutGraph(fixture(), NO_HI, 'K', 'T');
    expect(l.edges).toHaveLength(3);
    expect(l.edges[0].d).toMatch(/^M .* C /);
    expect(l.edges.every((e) => e.opacity === 0.6)).toBe(true); // no highlight → base opacity
    const cross = new Set(l.nodes.filter((n) => n.crossStream).map((n) => n.key));
    expect(cross).toEqual(new Set(['Topic:U', 'Topic:F', 'Action:A'])); // D touches no cross edge
  });

  it('blocked highlight: blocker edge animates, others dim, non-blocked non-focus nodes dim', () => {
    const l = layoutGraph(fixture(), { blocked: true, cross: false }, 'K', 'T');
    const produces = l.edges.find((e) => e.d && e.animated)!;
    expect(produces.color).toBe('var(--st-danger-dot)');
    expect(produces.dash).toBe('6 5');
    expect(l.edges.filter((e) => !e.animated).every((e) => e.opacity === 0.18)).toBe(true);
    expect(l.nodes.find((n) => n.key === 'Action:A')!.dim).toBe(false); // blocked → kept
    expect(l.nodes.find((n) => n.key === 'Decision:D')!.dim).toBe(true); // not blocked → dim
    expect(l.nodes.find((n) => n.isFocus)!.dim).toBe(false); // focus never dims
  });

  it('cross highlight: cross edges animate and cross nodes stay lit', () => {
    const l = layoutGraph(fixture(), { blocked: false, cross: true }, 'K', 'T');
    expect(l.edges.filter((e) => e.animated).map((e) => e.color)).toEqual(['var(--st-warn-dot)', 'var(--st-warn-dot)']);
    expect(l.nodes.find((n) => n.key === 'Decision:D')!.dim).toBe(true);
    expect(l.nodes.find((n) => n.key === 'Action:A')!.dim).toBe(false);
  });

  it('drops edges with an unplaced endpoint and dedups parallel rel+dep curves', () => {
    const g = fixture({
      edges: [
        ...fixture().edges,
        // parallel rel edge duplicating the F→D dep direction → collapsed
        { source: 'dep', rel: 'RelatesTo', fromType: 'Topic', fromId: 'F', toType: 'Decision', toId: 'D', isBlocker: false, isCrossStream: false },
        // dangling: endpoint 'Z' is not a node
        { source: 'rel', rel: 'Blocks', fromType: 'Topic', fromId: 'F', toType: 'Topic', toId: 'Z', isBlocker: false, isCrossStream: false },
      ],
    });
    const l = layoutGraph(g, NO_HI, 'K', 'T');
    expect(l.edges).toHaveLength(3); // 3 originals; parallel deduped, dangling dropped
  });

  it('attaches edges for back (higher→lower col) and same-column pairs', () => {
    const g = fixture({
      edges: [
        { source: 'rel', rel: 'DecidedBy', fromType: 'Topic', fromId: 'F', toType: 'Decision', toId: 'D', isBlocker: false, isCrossStream: false }, // forward
        { source: 'rel', rel: 'Blocks', fromType: 'Action', fromId: 'A', toType: 'Topic', toId: 'F', isBlocker: false, isCrossStream: false }, // back: col2 → col1
        { source: 'rel', rel: 'References', fromType: 'Decision', fromId: 'D', toType: 'Action', toId: 'A', isBlocker: false, isCrossStream: false }, // same col (+1)
      ],
    });
    const l = layoutGraph(g, NO_HI, 'K', 'T');
    expect(l.edges).toHaveLength(3);
    expect(l.edges.every((e) => e.d.startsWith('M '))).toBe(true);
  });

  it('reports an empty graph when only the focus node is present', () => {
    const g = fixture({ nodes: [{ type: 'Topic', id: 'F', key: '', title: '', tier: 0, blocked: false, streams: [] }], edges: [] });
    const l = layoutGraph(g, NO_HI, 'K', 'T');
    expect(l.isEmpty).toBe(true);
  });
});

describe('buildListRows', () => {
  it('emits a depth-ordered row per non-focus node with dir + relation-label key', () => {
    const rows = buildListRows(fixture());
    expect(rows.map((r) => r.node.key).sort()).toEqual(['ACT-9', 'DECN-8', 'TOP-22']);
    const up = rows.find((r) => r.node.key === 'TOP-22')!;
    expect(up.dir).toBe('up');
    expect(up.level).toBe(1);
    expect(up.relLabel).toBe('deps.kind.DependsOn');
    const down = rows.find((r) => r.node.key === 'DECN-8')!;
    expect(down.dir).toBe('down');
    expect(down.relLabel).toBe('trace.rel.DecidedBy');
    expect(rows.find((r) => r.node.key === 'ACT-9')!.crossStream).toBe(true);
  });

  it('emits an empty relation label for an isolated node (no incident edge)', () => {
    const g = fixture({
      nodes: [
        { type: 'Topic', id: 'F', key: '', title: '', tier: 0, blocked: false, streams: [] },
        { type: 'Action', id: 'A', key: 'ACT-1', title: 'Orphan', tier: 1, blocked: false, streams: [] },
      ],
      edges: [],
    });
    const rows = buildListRows(g);
    expect(rows).toHaveLength(1);
    expect(rows[0].relLabel).toBe('');
  });

  it('falls back to any incident edge when none is strictly shallower', () => {
    // Two same-tier downstream nodes linked only to each other + focus.
    const g = fixture({
      nodes: [
        { type: 'Topic', id: 'F', key: '', title: '', tier: 0, blocked: false, streams: [] },
        { type: 'Action', id: 'A', key: 'ACT-1', title: 'A', tier: 1, blocked: false, streams: [] },
      ],
      edges: [{ source: 'rel', rel: 'Produces', fromType: 'Topic', fromId: 'F', toType: 'Action', toId: 'A', isBlocker: false, isCrossStream: false }],
    });
    const rows = buildListRows(g);
    expect(rows).toHaveLength(1);
    expect(rows[0].relLabel).toBe('trace.rel.Produces');
  });
});
