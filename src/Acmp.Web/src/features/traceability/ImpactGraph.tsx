/*
 * The depth-tiered SVG impact graph (P10f). Layout math lives in graphLayout.ts — this component maps
 * it to DOM and owns interaction only. Accessibility (the crux, reconciled from the design's
 * role="application", guardrail #14): roving-tabindex over real <button> nodes, LINEAR arrow-nav in
 * column/row order (no 2D spatial nav), aria-live focus announcements, and an aria-hidden edge layer.
 * Enter/click OPENS the artifact — re-centring the graph is a route change (our subgraph is composed
 * server-side around one focus), warm via the clicked node's own GUID. RTL: the edges-only SVG is
 * flipped with scaleX(-1) while nodes use logical inset-inline-start, so flipped curves meet the nodes.
 */
import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../components/icons';
import type { ImpactGraph as ImpactGraphDto, ImpactGraphNode } from '../../api/traceability';
import { GRAPH_FOCUS_TYPES, hrefFor, typeColor } from './traceMeta';
import {
  layoutGraph,
  nextFocusIndex,
  type HighlightState,
  type LaidOutNode,
} from './graphLayout';

interface Props {
  graph: ImpactGraphDto;
  focusKey: string;
  focusTitle: string;
  highlight: HighlightState;
}

/** Legend swatches — the artifact types the committee reads most; colours from the shared type map. */
const LEGEND_TYPES = ['Topic', 'Decision', 'Action', 'Risk'] as const;

export function ImpactGraph({ graph, focusKey, focusTitle, highlight }: Props) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const isRtl = i18n.dir() === 'rtl';
  const layout = layoutGraph(graph, highlight, focusKey, focusTitle);
  const nodeRefs = useRef<Map<string, HTMLButtonElement>>(new Map());
  // Start the roving tab-stop on the subject node (what the user is viewing), not the first upstream.
  const [rovingKey, setRovingKey] = useState<string>(() => layout.nodes.find((n) => n.isFocus)?.key ?? layout.navOrder[0] ?? '');
  const [liveMsg, setLiveMsg] = useState('');

  if (layout.isEmpty) {
    return <div className="ig-empty">{t('trace.graph.empty')}</div>;
  }

  // Keep the roving target valid if the graph changed under us (depth/highlight toggle).
  const activeKey = layout.navOrder.includes(rovingKey) ? rovingKey : layout.navOrder[0];

  const openNode = (n: ImpactGraphNode) => {
    if ((GRAPH_FOCUS_TYPES as readonly string[]).includes(n.type)) {
      navigate(`/traceability/${n.type}/${n.key}`, { state: { focusId: n.id, focusTitle: n.title } });
      return;
    }
    const href = hrefFor(n.type, n.key);
    if (href) navigate(href);
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLButtonElement>, n: ImpactGraphNode) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      openNode(n);
      return;
    }
    const idx = nextFocusIndex(layout.navOrder, activeKey, e.key);
    const nextKey = layout.navOrder[idx];
    if (nextKey && nextKey !== activeKey) {
      e.preventDefault();
      setRovingKey(nextKey);
      nodeRefs.current.get(nextKey)?.focus();
      const nn = layout.nodes.find((m) => m.key === nextKey);
      if (nn) setLiveMsg(nodeAria(nn, t));
    }
  };

  const focusNode = layout.nodes.find((n) => n.isFocus);

  return (
    <>
      <div className="ig-sr" aria-live="polite">{liveMsg}</div>
      <div className="ig-scroll">
        <div className="ig-canvas" style={{ inlineSize: layout.canvasW, blockSize: layout.canvasH }}>
          <svg
            width={layout.canvasW}
            height={layout.canvasH}
            className={`ig-edges${isRtl ? ' ig-edges--rtl' : ''}`}
            aria-hidden="true"
          >
            {layout.edges.map((edge) => (
              <path
                key={edge.key}
                d={edge.d}
                fill="none"
                stroke={edge.color}
                strokeWidth={edge.width}
                strokeDasharray={edge.dash}
                opacity={edge.opacity}
                className={edge.animated ? 'ig-edge--animated' : undefined}
              />
            ))}
          </svg>

          {layout.tierLabels.map((tl) => (
            <div key={tl.tier} className="ig-tier-label" style={{ insetInlineStart: tl.x, inlineSize: 152 }}>
              {tierLabel(tl.tier, t)}
            </div>
          ))}

          {layout.nodes.map((ln) => (
            <GraphNode
              key={ln.key}
              ln={ln}
              roving={ln.key === activeKey}
              refCb={(el) => {
                if (el) nodeRefs.current.set(ln.key, el);
                else nodeRefs.current.delete(ln.key);
              }}
              onKeyDown={onKeyDown}
              onActivate={() => {
                setRovingKey(ln.key);
                openNode(ln.node);
              }}
            />
          ))}
        </div>
      </div>

      <div className="ig-focusbar">
        <div className="ig-focusbar-main">
          <span className="ig-focusbar-label">{t('trace.graph.focused')}</span>
          <span className="ig-focusbar-key">{focusNode?.node.key ?? focusKey}</span>
          <span className="ig-focusbar-title">{focusNode?.node.title ?? focusTitle}</span>
        </div>
        <div className="ig-legend">
          {LEGEND_TYPES.map((type) => (
            <span className="ig-legend-item" key={type}>
              <span className="ig-legend-dot" style={{ background: typeColor(type) }} aria-hidden />
              {t(`trace.type.${type}`)}
            </span>
          ))}
        </div>
      </div>
      <div className="ig-hint">
        <Icon name="lock" size={13} aria-hidden />
        {t('trace.graph.keyboardHint')}
      </div>
    </>
  );
}

interface NodeProps {
  ln: LaidOutNode;
  roving: boolean;
  refCb: (el: HTMLButtonElement | null) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLButtonElement>, n: ImpactGraphNode) => void;
  onActivate: () => void;
}

function GraphNode({ ln, roving, refCb, onKeyDown, onActivate }: NodeProps) {
  const { t } = useTranslation();
  const { node } = ln;
  return (
    <button
      ref={refCb}
      type="button"
      className={`ig-node${ln.isFocus ? ' ig-node--focus' : ''}${ln.hit === 'blocked' ? ' ig-node--hitBlocked' : ln.hit === 'cross' ? ' ig-node--hitCross' : ''}`}
      style={{ insetInlineStart: ln.x, insetBlockStart: ln.y, inlineSize: 152, blockSize: 94, opacity: ln.dim ? 0.3 : 1 }}
      tabIndex={roving ? 0 : -1}
      aria-current={ln.isFocus ? 'true' : undefined}
      aria-label={nodeAria(ln, t)}
      onKeyDown={(e) => onKeyDown(e, node)}
      onClick={onActivate}
    >
      <span className="ig-node-row">
        <span className="ig-node-typedot" style={{ background: typeColor(node.type) }} aria-hidden />
        <span className="ig-node-key">{node.key}</span>
        {node.blocked && (
          <span className="ig-node-blocked"><Icon name="lock" size={8} aria-hidden /> {t('trace.graph.blocked')}</span>
        )}
      </span>
      <span className="ig-node-title">{node.title}</span>
      <span className="ig-node-meta">
        <span className="ig-node-type">{t(`trace.type.${node.type}`)}</span>
        {ln.crossStream && node.streams.length > 0 && <span className="ig-node-stream">{node.streams.join(', ')}</span>}
      </span>
    </button>
  );
}

/** Signed tier → the localized column label (Focus / Upstream N / Downstream N). */
function tierLabel(tier: number, t: (k: string, o?: Record<string, unknown>) => string): string {
  if (tier === 0) return t('trace.graph.tierFocus');
  return tier < 0 ? t('trace.graph.tierUp', { n: -tier }) : t('trace.graph.tierDown', { n: tier });
}

/** A node's screen-reader label: key, title, type, and any blocked / focus qualifiers. */
function nodeAria(ln: LaidOutNode, t: (k: string) => string): string {
  const parts = [ln.node.key, ln.node.title, t(`trace.type.${ln.node.type}`)];
  if (ln.node.blocked) parts.push(t('trace.graph.blocked'));
  if (ln.isFocus) parts.push(t('trace.graph.focused'));
  return parts.join(', ');
}
