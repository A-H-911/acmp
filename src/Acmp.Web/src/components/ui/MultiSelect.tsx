import { useEffect, useRef, useState } from 'react';
import { Icon } from '../icons';

export interface MultiOption {
  value: string;
  label: string;
}

interface MultiSelectProps {
  options: MultiOption[];
  value: string[];
  onChange: (value: string[]) => void;
  placeholder?: string;
  id?: string;
  ariaLabel?: string;
  /** Caller-localized "Remove {label}" for each token's remove button. */
  removeLabel: (label: string) => string;
  /** Caller-localized "no matches" text for an empty filtered list. */
  emptyLabel?: string;
  'aria-describedby'?: string;
}

/**
 * Multi-select with removable tokens + a filterable option list (Design System §07).
 * Chosen values render as tokens; the inline input filters; options are a
 * role="listbox" aria-multiselectable popover. Esc closes. Mirrors in RTL.
 */
export function MultiSelect({ options, value, onChange, placeholder, id, ariaLabel, removeLabel, emptyLabel = '—', ...aria }: MultiSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open]);

  const selected = options.filter((o) => value.includes(o.value));
  const filtered = options.filter((o) => o.label.toLowerCase().includes(query.toLowerCase()));
  const toggle = (v: string) => onChange(value.includes(v) ? value.filter((x) => x !== v) : [...value, v]);

  return (
    <div className="select">
      <div
        className="tokens-field"
        onClick={() => {
          setOpen(true);
          inputRef.current?.focus();
        }}
      >
        {selected.map((o) => (
          <span className="token" key={o.value}>
            {o.label}
            <button
              type="button"
              className="token-remove"
              aria-label={removeLabel(o.label)}
              onClick={(e) => {
                e.stopPropagation();
                onChange(value.filter((x) => x !== o.value));
              }}
            >
              <Icon name="x" size={13} aria-hidden />
            </button>
          </span>
        ))}
        <input
          ref={inputRef}
          id={id}
          className="tokens-input"
          aria-label={ariaLabel}
          aria-describedby={aria['aria-describedby']}
          aria-expanded={open}
          aria-haspopup="listbox"
          placeholder={selected.length === 0 ? placeholder : undefined}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onFocus={() => setOpen(true)}
        />
      </div>
      {open && (
        <>
          <div className="select-backdrop" onClick={() => setOpen(false)} aria-hidden="true" />
          <div className="select-panel" role="listbox" aria-multiselectable="true" aria-label={ariaLabel}>
            <div className="option-list">
              {filtered.length === 0 ? (
                <div className="option" aria-disabled="true">
                  {emptyLabel}
                </div>
              ) : (
                filtered.map((o) => (
                  <button
                    key={o.value}
                    type="button"
                    role="option"
                    aria-selected={value.includes(o.value)}
                    className="option ms-option"
                    onClick={() => toggle(o.value)}
                  >
                    <span className="ms-check" aria-hidden="true">
                      <Icon name="check" size={10} />
                    </span>
                    <span className="option-label">{o.label}</span>
                  </button>
                ))
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
