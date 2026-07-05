/*
 * Shared presentational pieces for the role dashboards (P12-PR2), composed from the
 * card system of "ACMP Dashboards & Reports.dc.html" (surface card, segmented bar +
 * legend, stat tiles, mono-keyed lists). Logical CSS only (RTL-safe); tokens from the
 * design system. Pure — every number arrives already computed by dashboardAgg.
 */
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { StatusTone } from '../../components/ui/StatusChip';
import { Icon } from '../../components/icons';
import { LoadingState, ErrorState } from '../../components/states';

/** StatusChip tones and the design's status CSS-var groups differ for one name
 *  ('scheduled' → --st-sched-*). Map before building a token reference. */
const TONE_VAR: Record<StatusTone, string> = {
  neutral: 'neutral', info: 'info', scheduled: 'sched', warn: 'warn', success: 'success', danger: 'danger',
};
const dotVar = (tone: StatusTone) => `var(--st-${TONE_VAR[tone]}-dot)`;

export function DashCard({
  span, title, headerRight, children,
}: { span: number; title: string; headerRight?: ReactNode; children: ReactNode }) {
  return (
    <section className="dash-card" style={{ gridColumn: `span ${span}` }}>
      <div className="dash-card-head">
        <h2 className="dash-card-title">{title}</h2>
        {headerRight}
      </div>
      {children}
    </section>
  );
}

export interface Segment { key: string; label: string; count: number; tone: StatusTone; }

/** Segmented proportional bar + legend (design's "Backlog health"). Zero-count segments drop
 *  out of the bar but stay in the legend so every bucket is accounted for. */
export function SegmentBar({ segments, total }: { segments: Segment[]; total: number }) {
  return (
    <>
      {/* Decorative — the legend below is the accessible source of the same counts. */}
      <div className="dash-seg" aria-hidden="true">
        {total > 0
          ? segments.filter((s) => s.count > 0).map((s) => (
              <span key={s.key} style={{ inlineSize: `${(s.count / total) * 100}%`, background: dotVar(s.tone) }} />
            ))
          : null}
      </div>
      <ul className="dash-legend">
        {segments.map((s) => (
          <li key={s.key}>
            <span className="dash-legend-dot" style={{ background: dotVar(s.tone) }} />
            <b>{s.count}</b> {s.label}
          </li>
        ))}
      </ul>
    </>
  );
}

export interface StatTile { key: string; value: number | string; label: string; tone?: StatusTone; }

export function StatTiles({ tiles }: { tiles: StatTile[] }) {
  return (
    <div className="dash-stats">
      {tiles.map((s) => (
        <div key={s.key} className="dash-stat">
          <div className="dash-stat-v" style={s.tone ? { color: `var(--st-${TONE_VAR[s.tone]}-fg)` } : undefined}>{s.value}</div>
          <div className="dash-stat-l">{s.label}</div>
        </div>
      ))}
    </div>
  );
}

export interface KeyRow { key: string; to: string; primary: string; right?: ReactNode; }

/** Mono-keyed list row → detail link (design's "My topics" / decisions list pattern).
 *  Renders a localized empty note when the list is dry (a live zero, not an error). */
export function KeyList({ rows, emptyLabel }: { rows: KeyRow[]; emptyLabel: string }) {
  if (rows.length === 0) return <p className="dash-empty">{emptyLabel}</p>;
  return (
    <ul className="dash-keylist">
      {rows.map((r) => (
        <li key={r.key}>
          <Link to={r.to}>
            <span className="dash-key">{r.key}</span>
            <span className="dash-primary">{r.primary}</span>
            {r.right}
            <Icon name="chevron" size={14} className="dir-flip dash-row-chev" aria-hidden />
          </Link>
        </li>
      ))}
    </ul>
  );
}

/** Loading/error gate shared by all three variants; children render only once every read resolved. */
export function DashState({
  isLoading, isError, onRetry, children,
}: { isLoading: boolean; isError: boolean; onRetry: () => void; children: ReactNode }) {
  const { t } = useTranslation();
  if (isLoading) return <LoadingState label={t('dashboard.loading')} />;
  if (isError) return <ErrorState title={t('dashboard.error.title')} body={t('dashboard.error.body')} onRetry={onRetry} />;
  return <>{children}</>;
}
