/*
 * Backlog calendar view (P5 refresh) — the design's month-grid chrome.
 * Faithful frame (month nav, locale weekday header, day cells, today ring, legend)
 * with an HONEST empty body: topics carry no scheduled-meeting or due date in the
 * Topics API (those arrive with meeting scheduling, P6), so no markers are placed.
 * The note states this rather than fabricating events (D1; guardrail #14, behavior SoT).
 * Gregorian throughout, localized via Intl (guardrail 9).
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../components/icons';

const WEEKS = 6; // 6 rows × 7 = 42 cells covers any month

export function Calendar() {
  const { t, i18n } = useTranslation();
  const [offset, setOffset] = useState(0); // months relative to the current month

  const now = new Date();
  const month = new Date(now.getFullYear(), now.getMonth() + offset, 1);
  const year = month.getFullYear();
  const m = month.getMonth();
  const startDow = new Date(year, m, 1).getDay(); // 0 = Sunday
  const daysInMonth = new Date(year, m + 1, 0).getDate();
  const isThisMonth = offset === 0;

  const monthLabel = new Intl.DateTimeFormat(i18n.language, { month: 'long', year: 'numeric' }).format(month);
  const weekdayFmt = new Intl.DateTimeFormat(i18n.language, { weekday: 'short' });
  // 2024-09-01 is a Sunday → seed locale-aware short weekday names, Sunday-first (Gregorian).
  const weekdays = Array.from({ length: 7 }, (_, i) => weekdayFmt.format(new Date(2024, 8, 1 + i)));

  const cells = Array.from({ length: WEEKS * 7 }, (_, i) => {
    const dayNum = i - startDow + 1;
    const inMonth = dayNum >= 1 && dayNum <= daysInMonth;
    return { key: i, day: inMonth ? dayNum : null, isToday: inMonth && isThisMonth && dayNum === now.getDate() };
  });

  return (
    <div className="cal">
      <div className="cal-bar">
        <div className="cal-nav">
          <button type="button" className="cal-navbtn cal-prev" aria-label={t('topics.calendar.prev')} onClick={() => setOffset((o) => o - 1)}>
            <Icon name="chevron" size={14} aria-hidden />
          </button>
          <span className="cal-month">{monthLabel}</span>
          <button type="button" className="cal-navbtn cal-next" aria-label={t('topics.calendar.next')} onClick={() => setOffset((o) => o + 1)}>
            <Icon name="chevron" size={14} aria-hidden />
          </button>
        </div>
        <div className="cal-legend">
          <span className="cal-leg"><span className="cal-leg-dot sched" aria-hidden="true" />{t('topics.calendar.scheduled')}</span>
          <span className="cal-leg"><span className="cal-leg-dot due" aria-hidden="true" />{t('topics.calendar.due')}</span>
        </div>
      </div>
      <div className="cal-weekdays" aria-hidden="true">
        {weekdays.map((w, i) => <span key={i} className="cal-weekday">{w}</span>)}
      </div>
      <div className="cal-grid">
        {cells.map((c) => (
          <div key={c.key} className={`cal-cell ${c.day === null ? 'out' : ''}`}>
            {c.day !== null && <span className={`cal-day ${c.isToday ? 'today' : ''}`}>{c.day}</span>}
          </div>
        ))}
      </div>
      <p className="bk-view-note" role="note">
        <Icon name="infoCircle" size={14} aria-hidden />
        {t('topics.calendar.note')}
      </p>
    </div>
  );
}
