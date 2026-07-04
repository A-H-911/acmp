/*
 * List / breadcrumb fallback for the impact graph (P10f) — the genuine non-SVG view reachable via the
 * Graph/List segmented control. A focus-path crumb + a role=tree of depth-indented rows (aria-level =
 * tier distance). Rows are real links (navigable when the far type has a route). Per-node lifecycle
 * status chip omitted (ADR-0001) — rows carry the relation label, a Blocked pill, and the stream code.
 */
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../components/icons';
import type { ImpactGraph } from '../../api/traceability';
import { hrefFor } from './traceMeta';
import { buildListRows, type HighlightState } from './graphLayout';

interface Props {
  graph: ImpactGraph;
  focusKey: string;
  highlight?: HighlightState;
}

export function ImpactGraphList({ graph, focusKey, highlight }: Props) {
  const { t } = useTranslation();
  const rows = buildListRows(graph, highlight);
  const focusNode = graph.nodes.find((n) => n.id === graph.focusId && n.tier === 0);
  const focusStream = focusNode?.streams.length ? focusNode.streams.join(', ') : '—';

  return (
    <div className="igl">
      <div className="igl-path">
        <span className="igl-path-label">{t('trace.graph.path')}</span>
        <span className="igl-path-key igl-path-key--focus">{focusKey}</span>
        <Icon name="chevron" size={12} className="dir-flip" aria-hidden />
        <span className="igl-path-key igl-path-key--stream">{focusStream}</span>
      </div>
      <div className="igl-tree" role="tree" aria-label={t('trace.graph.graphAria', { key: focusKey })}>
        {rows.map((r) => {
          const href = hrefFor(r.node.type, r.node.key);
          const hitClass = r.hit === 'blocked' ? ' igl-row--hitBlocked' : r.hit === 'cross' ? ' igl-row--hitCross' : '';
          const inner = (
            <>
              {r.indent > 0 && <span className="igl-connector" aria-hidden />}
              <span className={`igl-rel igl-rel-${r.dir}`}>
                <Icon
                  name={r.dir === 'up' ? 'arrowUp' : r.dir === 'down' ? 'arrowDown' : 'arrowRight'}
                  size={11}
                  aria-hidden
                />
                {r.relLabel ? t(r.relLabel) : t(`trace.dir.${r.dir}`)}
              </span>
              <span className="igl-typedot" style={{ background: r.typeColor }} aria-hidden />
              <span className="igl-key">{r.node.key}</span>
              <span className="igl-title">{r.node.title}</span>
              {r.node.blocked && (
                <span className="igl-blocked"><Icon name="lock" size={9} aria-hidden /> {t('trace.graph.blocked')}</span>
              )}
              {r.crossStream && r.node.streams.length > 0 && (
                <span className="igl-stream">{r.node.streams.join(', ')}</span>
              )}
              {href && <Icon name="chevron" size={14} className="igl-chev dir-flip" aria-label={t('trace.graph.goTo')} />}
            </>
          );
          const common = { role: 'treeitem' as const, 'aria-level': r.level };
          return href ? (
            <Link key={r.key} to={href} className={`igl-row${hitClass}`} style={{ marginInlineStart: r.indent }} {...common}>
              {inner}
            </Link>
          ) : (
            <div key={r.key} className={`igl-row${hitClass}`} style={{ marginInlineStart: r.indent }} tabIndex={0} {...common}>
              {inner}
            </div>
          );
        })}
      </div>
    </div>
  );
}
