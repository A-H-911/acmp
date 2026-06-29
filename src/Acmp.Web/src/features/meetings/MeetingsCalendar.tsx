/*
 * Meetings calendar (P6 / PR-B) — the design's `listCalView` month grid
 * (ACMP Meetings.dc.html isList, calendar branch ~L201). Anatomy + literal px from the mock:
 * card pad 18 · header (month 15/700 + prev/next 34px nav buttons) · 7-col grid gap 6 ·
 * cells min-height 64, border-soft, radius 8 · event pills 9.5px, status-toned.
 *
 * Behaviour over the mock's static Feb-2026 dummy data (guardrail 14 — visual fidelity, real
 * behaviour): the grid is computed from the real meetings' `scheduledStart`, defaults to the
 * current month, and the chevrons actually page months. Month name + weekday labels come from
 * Intl (Gregorian, localized, RTL-safe) so nothing is hardcoded (guardrail 9). The accessible
 * data table is the List view; this is the alternate visual view, with each event a real link.
 */
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { MeetingSummary } from '../../api/meetings';
import { Icon } from '../../components/icons';
import { meetingTone } from './meetingStatus';

interface CalCell {
  key: string;
  day: number | null;
  meetings: MeetingSummary[];
}

/** A month's worth of cells: leading blanks to the 1st's weekday, the days, trailing blanks to a
 *  whole number of weeks. Sunday-first to match the design's Sun..Sat header. */
function buildCells(year: number, month: number, byDay: Map<number, MeetingSummary[]>): CalCell[] {
  const firstDow = new Date(year, month, 1).getDay(); // 0 = Sunday
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const total = Math.ceil((firstDow + daysInMonth) / 7) * 7;
  const cells: CalCell[] = [];
  for (let i = 0; i < total; i++) {
    const day = i - firstDow + 1;
    const valid = day >= 1 && day <= daysInMonth;
    cells.push({ key: `c${i}`, day: valid ? day : null, meetings: valid ? byDay.get(day) ?? [] : [] });
  }
  return cells;
}

/** Sun..Sat short weekday labels, localized. 2023-01-01 (UTC) is a Sunday. */
function useDowLabels(lang: string): string[] {
  return useMemo(() => {
    const fmt = new Intl.DateTimeFormat(lang, { weekday: 'short' });
    return Array.from({ length: 7 }, (_, i) => fmt.format(new Date(Date.UTC(2023, 0, 1 + i))));
  }, [lang]);
}

export function MeetingsCalendar({ meetings }: { meetings: MeetingSummary[] }) {
  const { t, i18n } = useTranslation();
  const [view, setView] = useState(() => {
    const now = new Date();
    return { year: now.getFullYear(), month: now.getMonth() };
  });
  const dowLabels = useDowLabels(i18n.language);

  const byDay = useMemo(() => {
    const map = new Map<number, MeetingSummary[]>();
    for (const m of meetings) {
      const d = new Date(m.scheduledStart);
      if (d.getFullYear() === view.year && d.getMonth() === view.month) {
        const arr = map.get(d.getDate()) ?? [];
        arr.push(m);
        map.set(d.getDate(), arr);
      }
    }
    return map;
  }, [meetings, view]);

  const cells = useMemo(() => buildCells(view.year, view.month, byDay), [view, byDay]);
  const monthLabel = useMemo(
    () => new Intl.DateTimeFormat(i18n.language, { month: 'long', year: 'numeric' }).format(new Date(view.year, view.month, 1)),
    [i18n.language, view],
  );
  const formatWhen = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

  const step = (delta: number) =>
    setView((v) => {
      const d = new Date(v.year, v.month + delta, 1);
      return { year: d.getFullYear(), month: d.getMonth() };
    });

  return (
    <section className="mt-cal" aria-label={t('meetings.calendar.label')}>
      <div className="mt-cal-head">
        <div className="mt-cal-month" aria-live="polite">
          {monthLabel}
        </div>
        <div className="mt-cal-nav">
          <button type="button" className="mt-cal-navbtn" onClick={() => step(-1)} aria-label={t('meetings.calendar.prevMonth')}>
            <Icon name="chevron" size={15} className="mt-cal-prev" />
          </button>
          <button type="button" className="mt-cal-navbtn" onClick={() => step(1)} aria-label={t('meetings.calendar.nextMonth')}>
            <Icon name="chevron" size={15} className="mt-cal-next" />
          </button>
        </div>
      </div>
      <div className="mt-cal-grid">
        {dowLabels.map((label, i) => (
          <div key={i} className="mt-cal-dow">
            {label}
          </div>
        ))}
        {cells.map((c) => (
          <div key={c.key} className={`mt-cal-cell${c.day === null ? ' blank' : ''}`}>
            {c.day !== null && <div className="mt-cal-num">{c.day}</div>}
            {c.meetings.map((m) => (
              <Link
                key={m.id}
                to={`/meetings/${m.key}`}
                className={`mt-cal-event ${meetingTone(m.status)}`}
                aria-label={t('meetings.calendar.eventAria', { title: m.title, when: formatWhen(m.scheduledStart) })}
              >
                {m.title}
              </Link>
            ))}
          </div>
        ))}
      </div>
    </section>
  );
}
