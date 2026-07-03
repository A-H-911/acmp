/*
 * Traceability panel (P10e, AC-062) — the shared "Relationships" aside mounted on every artifact
 * detail page (Topic/Decision/Action/Risk). It MERGES two edge sources at read time:
 *   - typed Relationship edges     (GET /api/traceability/{type}/{id})
 *   - governed Dependency edges    (GET /api/dependencies/artifact/{type}/{id})
 * into one Upstream / Downstream / Related view. Dependency data is only fetched when the artifact
 * has a DependencyEndpointType (Topic/Action/System/Decision) — a Risk detail passes no depType, so
 * it shows relationship edges only (no DependencyEndpointType.Risk exists — honest, flagged).
 *
 * Scope (operator-GO'd): this is the detail-page ASIDE only. The standalone group-by-type
 * Relationships page + the SVG impact graph ship together in P10f.
 *
 * Reconciliations: no far-artifact lifecycle status chip (cross-module read, ADR-0001) — dependency
 * edges instead carry their own "Blocked" pill; routeless target types render as plain text (no dead
 * links). "Add relationship"/"Add dependency" are Chairman/Secretary only (UI gate; the API enforces).
 */
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { Icon } from '../../components/icons';
import { Button } from '../../components/ui/Button';
import { useArtifactRelationships, type ArtifactType } from '../../api/traceability';
import { useArtifactDependencies, type DependencyEndpointType } from '../../api/dependencies';
import { buildPanelRows, panelRowCount, type PanelDir, type PanelRow } from './traceMeta';
import { CreateRelationshipDialog } from './CreateRelationshipDialog';
import { CreateDependencyDialog } from '../dependencies/CreateDependencyDialog';
import './traceability.css';

interface Props {
  traceType: ArtifactType;
  /** Present only for artifacts that can hold dependency edges (Topic/Action/System/Decision). */
  depType?: DependencyEndpointType;
  id: string;
  artifactKey: string;
  title: string;
}

const DIR_ORDER: PanelDir[] = ['up', 'down', 'related'];

export function TraceabilityPanel({ traceType, depType, id, artifactKey, title }: Props) {
  const { t } = useTranslation();
  const auth = useAuth();
  const canLink = hasRole(auth, 'chairman', 'secretary');
  const [relOpen, setRelOpen] = useState(false);
  const [depOpen, setDepOpen] = useState(false);

  const rels = useArtifactRelationships(traceType, id);
  const deps = useArtifactDependencies(depType, id);

  const grouped = buildPanelRows(rels.data, deps.data);
  const count = panelRowCount(grouped);
  const loading = rels.isLoading || (!!depType && deps.isLoading);
  const errored = rels.isError || (!!depType && deps.isError);

  return (
    <aside className="tp" aria-label={t('trace.panel.title')}>
      <div className="tp-head">
        <div className="tp-head-title">
          <Icon name="deps" size={16} aria-hidden />
          <h2>{t('trace.panel.title')}</h2>
        </div>
        {canLink && (
          <div className="tp-head-actions">
            {depType && (
              <Button variant="secondary" size="sm" onClick={() => setDepOpen(true)}>
                <Icon name="plus" size={13} aria-hidden /> {t('trace.addDependency')}
              </Button>
            )}
            <Button variant="secondary" size="sm" onClick={() => setRelOpen(true)}>
              <Icon name="plus" size={13} aria-hidden /> {t('trace.addRelationship')}
            </Button>
          </div>
        )}
      </div>

      <div className="tp-body">
        {loading ? (
          <p className="tp-muted" role="status" aria-busy="true">{t('common.loading')}</p>
        ) : errored ? (
          <p className="tp-muted tp-error" role="alert">{t('trace.panel.error')}</p>
        ) : count === 0 ? (
          <p className="tp-muted">{t('trace.panel.empty')}</p>
        ) : (
          DIR_ORDER.filter((d) => grouped[d].length > 0).map((d) => (
            <section className="tp-group" key={d}>
              <h3 className={`tp-group-head tp-dir-${d}`}>{t(`trace.dir.${d}`)}</h3>
              <ul className="tp-list">
                {grouped[d].map((row) => (
                  <EdgeRow key={`${row.source}-${row.id}`} row={row} />
                ))}
              </ul>
            </section>
          ))
        )}
      </div>

      <CreateRelationshipDialog
        open={relOpen}
        onClose={() => setRelOpen(false)}
        source={{ type: traceType, id, key: artifactKey, title }}
      />
      {depType && (
        <CreateDependencyDialog
          open={depOpen}
          onClose={() => setDepOpen(false)}
          from={{ type: depType, id, key: artifactKey, title }}
        />
      )}
    </aside>
  );
}

/** One merged edge row: relation label + far artifact (navigable when a route exists) + Blocked pill. */
function EdgeRow({ row }: { row: PanelRow }) {
  const { t } = useTranslation();
  const inner = (
    <>
      <span className="tp-rel">{t(row.relLabel)}</span>
      <span className="tp-other">
        <span className="tp-other-key">{row.otherKey}</span>
        <span className="tp-other-title">{row.otherTitle}</span>
      </span>
      {row.blocked && <span className="tp-blocked">{t('deps.blocked')}</span>}
    </>
  );
  return (
    <li className="tp-item">
      {row.href ? (
        <Link className="tp-item-link" to={row.href}>
          {inner}
          <Icon name="chevron" size={14} className="dir-flip tp-chev" aria-hidden />
        </Link>
      ) : (
        <span className="tp-item-static" title={t('trace.noRoute')}>{inner}</span>
      )}
    </li>
  );
}
