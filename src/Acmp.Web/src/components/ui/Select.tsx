import { useEffect, useRef, useState } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';
import { Icon } from '../icons';

export interface SelectOption {
  value: string;
  label: ReactNode;
}

interface SelectProps {
  options: SelectOption[];
  value?: string;
  onChange: (value: string) => void;
  placeholder?: string;
  id?: string;
  disabled?: boolean;
  ariaLabel?: string;
  'aria-invalid'?: true;
  'aria-describedby'?: string;
}

/**
 * Single-select listbox (Design System §06). Button trigger → role="listbox" popover.
 * Keyboard: ↓/Enter/Space open; ↑/↓/Home/End move; Enter/Space select; Esc closes and
 * returns focus to the trigger. Mirrors in RTL (logical positioning).
 */
export function Select({ options, value, onChange, placeholder, id, disabled, ariaLabel, ...aria }: SelectProps) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const selected = options.find((o) => o.value === value);

  useEffect(() => {
    if (!open) return;
    panelRef.current?.focus();
    const onKey = (e: globalThis.KeyboardEvent) => {
      if (e.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open]);

  const choose = (v: string) => {
    onChange(v);
    setOpen(false);
    triggerRef.current?.focus();
  };

  const openAtSelected = () => {
    setOpen(true);
    setActive(Math.max(0, options.findIndex((o) => o.value === value)));
  };

  const onTriggerKey = (e: KeyboardEvent) => {
    if (e.key === 'ArrowDown' || e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      openAtSelected();
    }
  };

  const onListKey = (e: KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActive((a) => Math.min(options.length - 1, a + 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActive((a) => Math.max(0, a - 1));
    } else if (e.key === 'Home') {
      e.preventDefault();
      setActive(0);
    } else if (e.key === 'End') {
      e.preventDefault();
      setActive(options.length - 1);
    } else if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      const o = options[active];
      if (o) choose(o.value);
    }
  };

  return (
    <div className="select">
      <button
        ref={triggerRef}
        type="button"
        id={id}
        className="select-trigger"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={ariaLabel}
        aria-invalid={aria['aria-invalid']}
        aria-describedby={aria['aria-describedby']}
        onClick={() => (open ? setOpen(false) : openAtSelected())}
        onKeyDown={onTriggerKey}
      >
        <span className={selected ? undefined : 'select-placeholder'}>{selected ? selected.label : placeholder}</span>
        <Icon name="chevronDown" size={15} className="select-caret" aria-hidden />
      </button>
      {open && (
        <>
          <div className="select-backdrop" onClick={() => setOpen(false)} aria-hidden="true" />
          <div
            ref={panelRef}
            className="select-panel"
            role="listbox"
            aria-label={ariaLabel}
            tabIndex={-1}
            onKeyDown={onListKey}
          >
            <div className="option-list">
              {options.map((o, i) => (
                <button
                  key={o.value}
                  type="button"
                  role="option"
                  aria-selected={o.value === value}
                  className={`option ${i === active ? 'option-active' : ''}`}
                  onMouseEnter={() => setActive(i)}
                  onClick={() => choose(o.value)}
                >
                  <span className="option-label">{o.label}</span>
                  {o.value === value && <Icon name="check" size={15} aria-hidden />}
                </button>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
