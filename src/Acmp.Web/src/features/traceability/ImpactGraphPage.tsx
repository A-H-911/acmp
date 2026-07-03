/*
 * Impact-graph page (P10f, route /traceability/:type/:key) — the container. It resolves the focus
 * artifact's GUID (the endpoint is GUID-keyed, the URL is key-based): WARM via router state passed by
 * a detail-page "Open graph" button, or COLD via a by-key detail fetch for the four focusable types.
 * It owns the view/depth/highlight state and composes the RelationshipsAside (1-hop, panel reads) with
 * the graph section (transitive, useTraceGraph). The shell owns the breadcrumb (deriveBreadcrumbs).
 */
import { useRef, useState } from 'react';
import { Navigate, useLocation, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../components/icons';
import {
  ARTIFACT_TYPES,
  useArtifactRelationships,
  useTraceGraph,
  type ArtifactType,
} from '../../api/traceability';
import { useArtifactDependencies, type DependencyEndpointType } from '../../api/dependencies';
import { useTopicDetail } from '../../api/topics';
import { useDecision } from '../../api/decisions';
import { useAction } from '../../api/actions';
import { useRisk } from '../../api/risks';
import { buildTypeGroups } from './traceMeta';
import type { HighlightState } from './graphLayout';
import { RelationshipsAside } from './RelationshipsAside';
import { ImpactGraph } from './ImpactGraph';
import { ImpactGraphList } from './ImpactGraphList';
import './graph.css';

/** Focus types that can hold governed dependency edges (drives the aside's dependency read). */
const DEP_TYPE: Partial<Record<ArtifactType, DependencyEndpointType>> = {
  Topic: 'Topic',
  Action: 'Action',
  Decision: 'Decision',
};

const DEPTHS = [1, 2, 3] as const;

export function ImpactGraphPage() {
  const { t, i18n } = useTranslation();
  const { type: rawType, key = '' } = useParams();
  const location = useLocation();
  const warm = location.state as { focusId?: string; focusTitle?: string } | null;
  const validType = (ARTIFACT_TYPES as readonly string[]).includes(rawType ?? '');
  const type = rawType as ArtifactType;

  const [depth, setDepth] = useState(2);
  const [view, setView] = useState<'graph' | 'list'>('graph');
  const [highlight, setHighlight] = useState<HighlightState>({ blocked: false, cross: false });
  const graphRef = useRef<HTMLDivElement>(null);

  const focus = useFocusIdentity(validType ? type : undefined, key, warm, i18n.language);
  const focusEnabled = validType && !!focus.id;

  const graph = useTraceGraph(focusEnabled ? type : undefined, focus.id, depth);
  const rels = useArtifactRelationships(focusEnabled ? type : undefined, focus.id);
  const deps = useArtifactDependencies(focusEnabled ? DEP_TYPE[type] : undefined, focus.id);

  const groups = buildTypeGroups(rels.data, deps.data);
  const total = groups.reduce((sum, g) => sum + g.items.length, 0);
  const asideLoading = rels.isLoading || (!!DEP_TYPE[type] && deps.isLoading);

  if (!validType) return <Navigate to="/" replace />;

  const focusTitle = focus.title || key;
  const openGraph = () => {
    setView('graph');
    graphRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  // Plain <div> wrapper: a named <section> would be a `region` landmark and nest the aside's
  // complementary landmark inside it (axe landmark-complementary-is-top-level). The shell's <main>
  // is the page landmark; the aside sits directly under it.
  return (
    <div>
      <div className="ig-head">
        <div>
          <h1>{t('trace.graph.title')}</h1>
          <div className="ig-sub">
            <span className="ig-key">{key}</span>
            <span className="ig-dot" aria-hidden />
            <span>{focusTitle}</span>
          </div>
        </div>
      </div>

      <div className="ig-grid">
        <RelationshipsAside groups={groups} total={total} loading={asideLoading} onOpenGraph={openGraph} />

        <section className="ig-card" aria-label={t('trace.graph.graphTitle')} ref={graphRef}>
          <div className="ig-controls">
            <h2>{t('trace.graph.graphTitle')}</h2>
            <div className="ig-seg" role="group" aria-label={t('trace.graph.graphTitle')}>
              <button type="button" className={`ig-seg-btn${view === 'graph' ? ' ig-seg-btn--on' : ''}`} aria-pressed={view === 'graph'} onClick={() => setView('graph')}>
                <Icon name="deps" size={14} aria-hidden /> {t('trace.graph.graph')}
              </button>
              <button type="button" className={`ig-seg-btn${view === 'list' ? ' ig-seg-btn--on' : ''}`} aria-pressed={view === 'list'} onClick={() => setView('list')}>
                <Icon name="viewList" size={14} aria-hidden /> {t('trace.graph.list')}
              </button>
            </div>
          </div>

          <div className="ig-toolbar">
            <div className="ig-depth">
              <span className="ig-depth-label">{t('trace.graph.depth')}</span>
              <div className="ig-depth-group">
                {DEPTHS.map((d) => (
                  <button key={d} type="button" className={`ig-depth-btn${depth === d ? ' ig-depth-btn--on' : ''}`} aria-pressed={depth === d} onClick={() => setDepth(d)}>
                    {d}
                  </button>
                ))}
              </div>
              <span className="ig-depth-hint">{t('trace.graph.depthHint')}</span>
            </div>
            <div className="ig-divider" aria-hidden />
            <div className="ig-toggles">
              <button type="button" className="ig-toggle ig-toggle--blocked" aria-pressed={highlight.blocked} onClick={() => setHighlight((h) => ({ ...h, blocked: !h.blocked }))}>
                <span className="ig-toggle-dot" style={{ background: 'var(--st-danger-dot)' }} aria-hidden /> {t('trace.graph.blockedWork')}
              </button>
              <button type="button" className="ig-toggle ig-toggle--cross" aria-pressed={highlight.cross} onClick={() => setHighlight((h) => ({ ...h, cross: !h.cross }))}>
                <span className="ig-toggle-dot" style={{ background: 'var(--st-warn-dot)' }} aria-hidden /> {t('trace.graph.crossStream')}
              </button>
            </div>
          </div>

          <GraphBody
            focus={focus}
            focusKey={key}
            focusTitle={focusTitle}
            view={view}
            highlight={highlight}
            graph={graph}
          />
        </section>
      </div>
    </div>
  );
}

interface GraphBodyProps {
  focus: FocusIdentity;
  focusKey: string;
  focusTitle: string;
  view: 'graph' | 'list';
  highlight: HighlightState;
  graph: ReturnType<typeof useTraceGraph>;
}

/** The graph/list surface with all the load/error/unsupported branches kept out of the controls JSX. */
function GraphBody({ focus, focusKey, focusTitle, view, highlight, graph }: GraphBodyProps) {
  const { t } = useTranslation();

  if (focus.unsupported) return <div className="ig-empty">{t('trace.graph.unsupported')}</div>;
  if (focus.loading || graph.isLoading) return <div className="ig-empty" role="status" aria-busy="true">{t('common.loading')}</div>;
  if (focus.error || graph.isError || !graph.data) return <div className="ig-empty" role="alert">{t('trace.graph.error')}</div>;

  return (
    <>
      {graph.data.partial && (
        <div className="ig-partial" role="status">
          <Icon name="warnTriangle" size={14} aria-hidden /> {t('trace.graph.partial')}
        </div>
      )}
      {view === 'graph' ? (
        <ImpactGraph graph={graph.data} focusKey={focusKey} focusTitle={focusTitle} highlight={highlight} />
      ) : (
        <ImpactGraphList graph={graph.data} focusKey={focusKey} />
      )}
    </>
  );
}

interface FocusIdentity {
  id: string | undefined;
  title: string;
  loading: boolean;
  error: boolean;
  unsupported?: boolean;
}

/**
 * Resolve the focus GUID + a display title. Warm (router state) short-circuits; otherwise a by-key
 * detail fetch for the type. All four hooks mount unconditionally (rules of hooks) but only the one
 * matching the type — and only on the cold path — is enabled. A valid-but-non-focusable type with no
 * warm state is "unsupported" (open the graph from that artifact's own page instead).
 */
function useFocusIdentity(
  type: ArtifactType | undefined,
  key: string,
  warm: { focusId?: string; focusTitle?: string } | null,
  lang: string,
): FocusIdentity {
  const cold = !warm?.focusId;
  const topic = useTopicDetail(cold && type === 'Topic' ? key : undefined);
  const decision = useDecision(cold && type === 'Decision' ? key : undefined);
  const action = useAction(cold && type === 'Action' ? key : undefined);
  const risk = useRisk(cold && type === 'Risk' ? key : undefined);

  if (warm?.focusId) return { id: warm.focusId, title: warm.focusTitle ?? key, loading: false, error: false };

  const q = type === 'Topic' ? topic : type === 'Decision' ? decision : type === 'Action' ? action : type === 'Risk' ? risk : undefined;
  if (!q) return { id: undefined, title: key, loading: false, error: false, unsupported: true };

  const raw = q.data as { id: string; title: string | { en: string; ar: string } } | undefined;
  const title = raw ? (typeof raw.title === 'string' ? raw.title : lang === 'ar' ? raw.title.ar : raw.title.en) : key;
  return { id: raw?.id, title, loading: q.isLoading, error: q.isError };
}
