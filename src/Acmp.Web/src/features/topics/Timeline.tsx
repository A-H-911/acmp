/*
 * Backlog timeline view (P5 refresh) — the design's gantt chrome.
 * Faithful frame (Topic column + 6 week columns, one row per real topic) with an
 * HONEST empty track: topics carry no planned start/span in the Topics API (the
 * design's bars are mock spans), so no bars are drawn. The note states this rather
 * than fabricating spans (D1; guardrail #14, behavior SoT). Real topic keys/titles
 * populate the rows so the structure is meaningful.
 */
import { useTranslation } from 'react-i18next';
import { Icon } from '../../components/icons';
import type { TopicSummary } from '../../api/topics';

const WEEKS = 6;

export function Timeline({ rows }: { rows: TopicSummary[] }) {
  const { t } = useTranslation();
  const weeks = Array.from({ length: WEEKS }, (_, i) => t('topics.timeline.week', { n: i + 1 }));

  return (
    <div className="tl-wrap">
      <div className="tl">
        <div className="tl-head">
          <span className="tl-head-topic">{t('topics.col.topic')}</span>
          <div className="tl-head-weeks">
            {weeks.map((w, i) => <span key={i} className="tl-week">{w}</span>)}
          </div>
        </div>
        {rows.map((r) => (
          <div key={r.id} className="tl-row">
            <div className="tl-row-topic">
              <span className="bk-key">{r.key}</span>
              <span className="tl-row-title">{r.title}</span>
            </div>
            <div className="tl-track" aria-hidden="true">
              {Array.from({ length: WEEKS }, (_, i) => <span key={i} className="tl-cell" />)}
            </div>
          </div>
        ))}
      </div>
      <p className="bk-view-note" role="note">
        <Icon name="infoCircle" size={14} aria-hidden />
        {t('topics.timeline.note')}
      </p>
    </div>
  );
}
