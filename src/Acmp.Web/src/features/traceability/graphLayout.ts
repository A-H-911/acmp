/*
 * Pure geometry + styling for the impact graph (P10f). Zero React, zero i18n — every branch here is
 * unit-testable so the SVG component can stay a thin mapper (per the ci-gates-run-locally-pre-push
 * memory: isolate branchy math, hit 100% on it, no coverage exclusions).
 *
 * The backend already ran the depth-bounded BFS and returns each node's SIGNED tier (0 = focus, negative
 * = upstream, positive = downstream). So this module never traverses — it bins nodes into tier-columns
 * (the design's layout math, ported verbatim: nodeW/H, gaps, centred columns, cubic-bezier edges) and
 * decides per-edge / per-node styling for the two highlight toggles. RTL is handled by the component
 * (logical inset-inline-start on nodes + scaleX(-1) on the edges-only SVG) — geometry stays in one space.
 */
import type { ImpactGraph, ImpactGraphEdge, ImpactGraphNode } from '../../api/traceability';
import { type PanelDir, relDirection, typeColor, DIR_COLOR } from './traceMeta';

// Design constants (ACMP Traceability & Dependencies.dc.html, renderVals). Do not retune without a
// screenshot-compare pass — the .dc.html is the pixel target (guardrail #14).
export const NODE_W = 152;
export const NODE_H = 94;
const GAP_X = 70;
const GAP_Y = 18;
const PAD_X = 24;
const PAD_TOP = 30;
const PAD_BOTTOM = 16;

/** A node's key in position/edge maps: enum-name + GUID (the dual-enum-safe key the backend uses too). */
export function nodeKey(type: string, id: string): string {
  return `${type}:${id}`;
}

/** Signed tier → the focus-relative direction shown on list rows and the aside. */
export function tierDir(tier: number): PanelDir {
  return tier < 0 ? 'up' : tier > 0 ? 'down' : 'related';
}

export interface LaidOutNode {
  key: string;
  node: ImpactGraphNode;
  x: number;
  y: number;
  col: number;
  isFocus: boolean;
  crossStream: boolean;
  dim: boolean;
}

export interface LaidOutEdge {
  key: string;
  d: string;
  color: string;
  width: number;
  dash: string;
  opacity: number;
  animated: boolean;
}

export interface TierLabel {
  x: number;
  tier: number;
}

export interface GraphLayout {
  canvasW: number;
  canvasH: number;
  nodes: LaidOutNode[];
  edges: LaidOutEdge[];
  tierLabels: TierLabel[];
  /** Node keys in a stable linear order (column, then row) — the roving-tabindex / arrow-nav order. */
  navOrder: string[];
  isEmpty: boolean;
}

export interface HighlightState {
  blocked: boolean;
  cross: boolean;
}

/**
 * Lay the graph out. `focusKey` / `focusTitle` are injected (the DTO's focus node carries empty
 * key/title — its identity is the URL, not an edge). Returns everything the component maps to DOM.
 */
export function layoutGraph(
  graph: ImpactGraph,
  hi: HighlightState,
  focusKey: string,
  focusTitle: string,
): GraphLayout {
  // Inject focus identity so the focus card and aria labels have real text.
  const nodes: ImpactGraphNode[] = graph.nodes.map((n) =>
    n.id === graph.focusId && n.tier === 0 ? { ...n, key: focusKey, title: focusTitle } : n,
  );

  const posByKey = new Map<string, { x: number; y: number; col: number }>();
  const tierVals = [...new Set(nodes.map((n) => n.tier))].sort((a, b) => a - b);
  const colOf = new Map<number, number>();
  tierVals.forEach((tv, i) => colOf.set(tv, i));
  const cols = tierVals.length;
  const byCol = new Map<number, ImpactGraphNode[]>();
  tierVals.forEach((tv) => byCol.set(tv, nodes.filter((n) => n.tier === tv)));
  const maxRows = Math.max(1, ...tierVals.map((tv) => byCol.get(tv)!.length));

  const canvasW = PAD_X * 2 + cols * NODE_W + Math.max(0, cols - 1) * GAP_X;
  const canvasH = PAD_TOP + PAD_BOTTOM + maxRows * NODE_H + (maxRows - 1) * GAP_Y;

  tierVals.forEach((tv) => {
    const colIdx = colOf.get(tv)!;
    const x = PAD_X + colIdx * (NODE_W + GAP_X);
    const list = byCol.get(tv)!;
    const colH = list.length * NODE_H + (list.length - 1) * GAP_Y;
    const y0 = PAD_TOP + (canvasH - PAD_TOP - PAD_BOTTOM - colH) / 2;
    list.forEach((n, ri) => posByKey.set(nodeKey(n.type, n.id), { x, y: y0 + ri * (NODE_H + GAP_Y), col: colIdx }));
  });

  // A node is cross-stream if any incident edge is (per-edge on the wire, per-node in the design).
  const crossKeys = new Set<string>();
  graph.edges.forEach((e) => {
    if (e.isCrossStream) {
      crossKeys.add(nodeKey(e.fromType, e.fromId));
      crossKeys.add(nodeKey(e.toType, e.toId));
    }
  });

  const anyHi = hi.blocked || hi.cross;
  const laidNodes: LaidOutNode[] = nodes.map((n) => {
    const key = nodeKey(n.type, n.id);
    const p = posByKey.get(key)!;
    const isFocus = n.id === graph.focusId && n.tier === 0;
    const crossStream = crossKeys.has(key);
    const matches = (hi.blocked && n.blocked) || (hi.cross && crossStream);
    return { key, node: n, x: p.x, y: p.y, col: p.col, isFocus, crossStream, dim: anyHi && !isFocus && !matches };
  });

  const laidEdges = layoutEdges(graph.edges, posByKey, hi);
  const navOrder = [...laidNodes]
    .sort((a, b) => (a.col - b.col) || (a.y - b.y))
    .map((n) => n.key);

  return {
    canvasW,
    canvasH,
    nodes: laidNodes,
    edges: laidEdges,
    tierLabels: tierVals.map((tv) => ({ x: PAD_X + colOf.get(tv)! * (NODE_W + GAP_X), tier: tv })),
    navOrder,
    isEmpty: laidNodes.length <= 1,
  };
}

/** Build the bezier edges. Dedups overlapping curves (backend unions rel+dep → possible parallel edge). */
function layoutEdges(
  edges: ImpactGraphEdge[],
  posByKey: Map<string, { x: number; y: number; col: number }>,
  hi: HighlightState,
): LaidOutEdge[] {
  const anyHi = hi.blocked || hi.cross;
  const seen = new Set<string>();
  const out: LaidOutEdge[] = [];
  for (const e of edges) {
    const fromKey = nodeKey(e.fromType, e.fromId);
    const toKey = nodeKey(e.toType, e.toId);
    const a = posByKey.get(fromKey);
    const b = posByKey.get(toKey);
    if (!a || !b) continue; // dangling — endpoint not placed (backend filters these, belt-and-braces)
    const dedupKey = `${fromKey}->${toKey}`;
    if (seen.has(dedupKey)) continue; // ponytail: collapse duplicate rel+dep curve; aside/list still show both
    seen.add(dedupKey);

    // Attach at facing sides so the curve reads as a flow between columns.
    let sx: number;
    let ex: number;
    if (b.col > a.col) {
      sx = a.x + NODE_W;
      ex = b.x;
    } else if (b.col < a.col) {
      sx = a.x;
      ex = b.x + NODE_W;
    } else {
      sx = a.x + NODE_W;
      ex = b.x + NODE_W;
    }
    const sy = a.y + NODE_H / 2;
    const ey = b.y + NODE_H / 2;
    const mx = (sx + ex) / 2;
    const d = `M ${sx} ${sy} C ${mx} ${sy}, ${mx} ${ey}, ${ex} ${ey}`;

    const dir = relDirection(e.source, e.rel);
    let color = DIR_COLOR[dir];
    let width = 1.6;
    let dash = '0';
    let opacity = anyHi ? 0.18 : 0.6;
    let animated = false;
    if (hi.blocked && e.isBlocker) {
      color = 'var(--st-danger-dot)';
      width = 2.4;
      dash = '6 5';
      opacity = 1;
      animated = true;
    } else if (hi.cross && e.isCrossStream) {
      color = 'var(--st-warn-dot)';
      width = 2.4;
      dash = '6 5';
      opacity = 1;
      animated = true;
    }
    out.push({ key: dedupKey, d, color, width, dash, opacity, animated });
  }
  return out;
}

export interface ListRow {
  key: string;
  node: ImpactGraphNode;
  level: number;
  indent: number;
  dir: PanelDir;
  relLabel: string;
  typeColor: string;
  crossStream: boolean;
}

/**
 * Depth-ordered rows for the List fallback (role=tree). Level = tier distance from focus (drives
 * aria-level + indent); dir/label from the discovering edge toward focus. Pure — no i18n (labels are
 * the `deps.kind.*` / `trace.rel.*` keys the panel already uses; the component resolves them).
 */
export function buildListRows(graph: ImpactGraph): ListRow[] {
  const crossKeys = new Set<string>();
  graph.edges.forEach((e) => {
    if (e.isCrossStream) {
      crossKeys.add(nodeKey(e.fromType, e.fromId));
      crossKeys.add(nodeKey(e.toType, e.toId));
    }
  });

  const rows: ListRow[] = graph.nodes
    .filter((n) => !(n.id === graph.focusId && n.tier === 0))
    .map((n) => {
      const key = nodeKey(n.type, n.id);
      const level = Math.abs(n.tier);
      const edge = discoveringEdge(graph, n, key);
      const relLabel = edge
        ? edge.source === 'dep'
          ? `deps.kind.${edge.rel}`
          : `trace.rel.${edge.rel}`
        : '';
      return {
        key,
        node: n,
        level,
        indent: Math.max(0, level - 1) * 22,
        dir: tierDir(n.tier),
        relLabel,
        typeColor: typeColor(n.type),
        crossStream: crossKeys.has(key),
      };
    });

  rows.sort((a, b) => (a.level - b.level) || a.node.key.localeCompare(b.node.key));
  return rows;
}

/** The edge that links a node toward focus (endpoint one tier shallower), for its relation label. */
function discoveringEdge(graph: ImpactGraph, n: ImpactGraphNode, key: string): ImpactGraphEdge | undefined {
  const incident = graph.edges.filter(
    (e) => nodeKey(e.fromType, e.fromId) === key || nodeKey(e.toType, e.toId) === key,
  );
  const toward = incident.find((e) => {
    const otherKey = nodeKey(e.fromType, e.fromId) === key ? nodeKey(e.toType, e.toId) : nodeKey(e.fromType, e.fromId);
    const other = graph.nodes.find((m) => nodeKey(m.type, m.id) === otherKey);
    return other != null && Math.abs(other.tier) < Math.abs(n.tier);
  });
  return toward ?? incident[0];
}

/** Roving-tabindex step: linear, clamped (no wrap — matches the role=tree fallback / APG convention). */
export function nextFocusIndex(order: readonly string[], current: string, key: string): number {
  const i = order.indexOf(current);
  if (i < 0) return order.length ? 0 : -1;
  if (key === 'ArrowRight' || key === 'ArrowDown') return Math.min(order.length - 1, i + 1);
  if (key === 'ArrowLeft' || key === 'ArrowUp') return Math.max(0, i - 1);
  if (key === 'Home') return 0;
  if (key === 'End') return order.length - 1;
  return i;
}
