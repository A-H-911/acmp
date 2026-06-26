import { useState } from 'react';
import { Icon } from '../icons';

interface DatePickerProps {
  /** Selected date as ISO yyyy-mm-dd (Gregorian). */
  value?: string;
  onChange: (iso: string) => void;
  /** Caller-localized nav button names. */
  labels: { previousMonth: string; nextMonth: string };
  /** Caller-localized weekday initials, Sunday-first (length 7). */
  weekdayLabels: string[];
  /** Caller-localized month names (length 12). */
  monthLabels: string[];
}

function toIso(y: number, m: number, d: number): string {
  return `${y}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
}

/**
 * Gregorian month calendar (Design System §07). Numerals stay Latin for
 * auditability. Each day is a focusable gridcell button; month nav chevrons
 * mirror in RTL. (Roving arrow-key grid navigation is a future enhancement;
 * days are individually tabbable today.)
 */
export function DatePicker({ value, onChange, labels, weekdayLabels, monthLabels }: DatePickerProps) {
  const today = new Date();
  const selected = value ? new Date(`${value}T00:00:00`) : undefined;
  const [view, setView] = useState(() => {
    const base = selected ?? today;
    return { y: base.getFullYear(), m: base.getMonth() };
  });

  const firstWeekday = new Date(view.y, view.m, 1).getDay();
  const daysInMonth = new Date(view.y, view.m + 1, 0).getDate();
  const cells: Array<number | null> = [
    ...Array<null>(firstWeekday).fill(null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ];

  const prevMonth = () => setView((v) => (v.m === 0 ? { y: v.y - 1, m: 11 } : { y: v.y, m: v.m - 1 }));
  const nextMonth = () => setView((v) => (v.m === 11 ? { y: v.y + 1, m: 0 } : { y: v.y, m: v.m + 1 }));

  const sameCell = (date: Date | undefined, d: number) =>
    !!date && date.getFullYear() === view.y && date.getMonth() === view.m && date.getDate() === d;

  return (
    <div className="datepicker">
      <div className="datepicker-head">
        <button type="button" className="datepicker-nav datepicker-prev" aria-label={labels.previousMonth} onClick={prevMonth}>
          <Icon name="chevron" size={15} aria-hidden />
        </button>
        <div className="datepicker-title">
          {monthLabels[view.m]} {view.y}
        </div>
        <button type="button" className="datepicker-nav datepicker-next" aria-label={labels.nextMonth} onClick={nextMonth}>
          <Icon name="chevron" size={15} aria-hidden />
        </button>
      </div>
      <div className="datepicker-grid" role="grid">
        {weekdayLabels.map((w, i) => (
          <div key={i} className="datepicker-dow" role="columnheader" aria-label={w}>
            {w}
          </div>
        ))}
        {cells.map((d, i) =>
          d === null ? (
            <div key={`pad-${i}`} />
          ) : (
            <button
              key={d}
              type="button"
              role="gridcell"
              className={`datepicker-day ${sameCell(today, d) ? 'is-today' : ''}`}
              aria-selected={sameCell(selected, d) || undefined}
              aria-current={sameCell(today, d) ? 'date' : undefined}
              onClick={() => onChange(toIso(view.y, view.m, d))}
            >
              {d}
            </button>
          ),
        )}
      </div>
    </div>
  );
}
