/*
 * The 320px group-by-TYPE Relationships aside (P10f, left column of the impact-graph page). Distinct
 * from the P10e detail-page TraceabilityPanel, which groups by DIRECTION: this one groups dependency
 * edges by KIND and relationship edges by far ARTIFACT TYPE (buildTypeGroups), matching the design's
 * "Grouped by type · both directions". 1-hop only (reuses the two panel reads, already warm in cache);
 * it does not respond to the graph's depth selector. Per-item lifecycle status chip is omitted — the
 * backend returns no far-node status (cross-module read, ADR-0001); the group header conveys type + dir.
 */
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon, type IconName } from '../../components/icons';
import { Button } from '../../components/ui/Button';
import { type TypeGroup } from './traceMeta';

/** Group icon by far artifact type; dependency-kind groups (no artifactType) use the deps glyph. */
const TYPE_ICON: Partial<Record<string, IconName>> = {
  Topic: 'backlog',
  Meeting: 'calendar',
  Decision: 'decision',
  Action: 'action',
  Risk: 'risk',
  Adr: 'adr',
  Diagram: 'diagram',
  Document: 'doc',
  Dependency: 'deps',
  ResearchMission: 'research',
};

interface Props {
  groups: TypeGroup[];
  total: number;
  loading: boolean;
  onOpenGraph: () => void;
}

export function RelationshipsAside({ groups, total, loading, onOpenGraph }: Props) {
  const { t } = useTranslation();
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const toggle = (key: string) =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });

  return (
    <aside className="rg-aside" aria-label={t('trace.graph.relTitle')}>
      <div className="rg-head">
        <div className="rg-head-row">
          <Icon name="deps" size={16} aria-hidden />
          <h2>{t('trace.graph.relTitle')}</h2>
          <span className="rg-total">{total}</span>
        </div>
        <div className="rg-sub">{t('trace.graph.relSub')}</div>
      </div>
      <div className="rg-scroll">
        {loading ? (
          <p className="tp-muted" role="status" aria-busy="true">{t('common.loading')}</p>
        ) : groups.length === 0 ? (
          <p className="tp-muted">{t('trace.panel.empty')}</p>
        ) : (
          groups.map((g) => {
            const open = !collapsed.has(g.key);
            return (
              <div className="rg-group" key={g.key}>
                <button
                  type="button"
                  className="rg-group-btn"
                  aria-expanded={open}
                  onClick={() => toggle(g.key)}
                >
                  <span className={`rg-group-icon rg-dir-${g.dir}`} style={groupIconStyle(g.dir)}>
                    <Icon name={(g.artifactType && TYPE_ICON[g.artifactType]) ?? 'deps'} size={13} aria-hidden />
                  </span>
                  <span className="rg-group-main">
                    <span className="rg-group-label-row">
                      <span className="rg-group-label">{t(g.labelKey)}</span>
                      <span className={`rg-dir rg-dir-${g.dir}`}>
                        <Icon name={g.dir === 'up' ? 'arrowUp' : g.dir === 'down' ? 'arrowDown' : 'arrowRight'} size={9} aria-hidden />
                        {t(`trace.dir.${g.dir}`)}
                      </span>
                    </span>
                  </span>
                  <span className="rg-count">{g.items.length}</span>
                  <Icon name="chevronDown" size={15} className={`rg-caret${open ? ' rg-caret--open' : ''}`} aria-hidden />
                </button>
                {open && (
                  <div className="rg-items">
                    {g.items.map((it, i) => {
                      const inner = (
                        <>
                          <span className="rg-item-main">
                            <span className="rg-item-key">{it.key}</span>
                            <span className="rg-item-title">{it.title}</span>
                          </span>
                          <Icon name="chevron" size={14} className="rg-item-chev dir-flip" aria-label={t('trace.graph.goTo')} />
                        </>
                      );
                      return it.href ? (
                        <Link className="rg-item" key={`${it.key}-${i}`} to={it.href}>{inner}</Link>
                      ) : (
                        <span className="rg-item" key={`${it.key}-${i}`} title={t('trace.noRoute')}>{inner}</span>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })
        )}
        <div className="rg-foot">
          <Button variant="secondary" className="rg-foot-btn" onClick={onOpenGraph}>
            <Icon name="deps" size={15} aria-hidden /> {t('trace.graph.graphTitle')}
          </Button>
        </div>
      </div>
    </aside>
  );
}

/** Direction-tinted icon chip background (design: warn/info/neutral bg by group direction). */
function groupIconStyle(dir: TypeGroup['dir']): React.CSSProperties {
  const tone = dir === 'up' ? 'warn' : dir === 'down' ? 'info' : 'neutral';
  return { background: `var(--st-${tone}-bg)`, color: `var(--st-${tone}-fg)` };
}
