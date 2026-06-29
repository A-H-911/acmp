import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DatePicker } from './DatePicker';
import { Icon } from '../icons';

interface DateFieldProps {
  /** Selected date as ISO yyyy-mm-dd (Gregorian). */
  value?: string;
  onChange: (iso: string) => void;
  /** Shown when nothing is selected (caller-localized). */
  placeholder: string;
  /** Caller-localized month-nav button names for the popover calendar. */
  labels: { previousMonth: string; nextMonth: string };
  id?: string;
  ariaLabel?: string;
  'aria-invalid'?: true;
  'aria-describedby'?: string;
}

/**
 * Date field (Design System §06/§07): a field-styled trigger (calendar icon + the selected
 * date, or a placeholder) that opens the shared `DatePicker` in a popover — mirrors the
 * `Select` open/close + backdrop + Escape behaviour. Replaces the browser-native
 * `datetime-local`, which doesn't localize (it renders mm/dd/yyyy even under `dir="rtl"`).
 * Month + weekday labels come from Intl (Gregorian, localized); numerals stay Latin for
 * auditability, matching the DatePicker.
 */
export function DateField({ value, onChange, placeholder, labels, id, ariaLabel, ...aria }: DateFieldProps) {
  const { i18n } = useTranslation();
  const lang = i18n.language;
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  const { weekdayLabels, monthLabels } = useMemo(() => {
    const wd = new Intl.DateTimeFormat(lang, { weekday: 'narrow' });
    const mo = new Intl.DateTimeFormat(lang, { month: 'long' });
    return {
      weekdayLabels: Array.from({ length: 7 }, (_, i) => wd.format(new Date(Date.UTC(2023, 0, 1 + i)))),
      monthLabels: Array.from({ length: 12 }, (_, i) => mo.format(new Date(Date.UTC(2023, i, 1)))),
    };
  }, [lang]);

  const display = value ? new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(new Date(`${value}T00:00:00`)) : '';

  useEffect(() => {
    if (!open) return;
    panelRef.current?.focus({ preventScroll: true });
    const onKey = (e: globalThis.KeyboardEvent) => {
      if (e.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open]);

  const choose = (iso: string) => {
    onChange(iso);
    setOpen(false);
    triggerRef.current?.focus();
  };

  return (
    <div className="select">
      <button
        ref={triggerRef}
        type="button"
        id={id}
        className="select-trigger datefield-trigger"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label={ariaLabel}
        aria-invalid={aria['aria-invalid']}
        aria-describedby={aria['aria-describedby']}
        onClick={() => setOpen((o) => !o)}
      >
        <Icon name="calendar" size={15} className="datefield-icon" aria-hidden />
        <span className={display ? 'datefield-value' : 'select-placeholder'}>{display || placeholder}</span>
      </button>
      {open && (
        <>
          <div className="select-backdrop" onClick={() => setOpen(false)} aria-hidden="true" />
          <div ref={panelRef} className="datefield-pop" tabIndex={-1}>
            <DatePicker value={value} onChange={choose} labels={labels} weekdayLabels={weekdayLabels} monthLabels={monthLabels} />
          </div>
        </>
      )}
    </div>
  );
}
