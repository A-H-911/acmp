/*
 * Free-text token input — Enter/comma adds a token, Backspace on an empty field removes the last.
 * Extracted from SubmitTopic (P5b) when P15c-2's convert dialog needed the same streams/systems input;
 * two callers now share one copy. Styling is the global `.tokens-field/.token/.tokens-input` set in
 * styles/forms.css (loaded app-wide), so this component ships no CSS of its own.
 */
import { useRef, useState } from 'react';
import { Icon } from '../icons';

export interface TokenInputProps {
  values: string[];
  onChange: (v: string[]) => void;
  placeholder: string;
  ariaLabel: string;
  removeLabel: (v: string) => string;
  id?: string;
  ariaInvalid?: true;
  describedby?: string;
}

export function TokenInput({ values, onChange, placeholder, ariaLabel, removeLabel, id, ariaInvalid, describedby }: TokenInputProps) {
  const [draft, setDraft] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);
  const add = () => {
    const v = draft.trim();
    if (v && !values.includes(v)) onChange([...values, v]);
    setDraft('');
  };
  return (
    <div className="tokens-field" onClick={() => inputRef.current?.focus()}>
      {values.map((v) => (
        <span className="token" key={v}>
          {v}
          <button type="button" className="token-remove" aria-label={removeLabel(v)} onClick={(e) => { e.stopPropagation(); onChange(values.filter((x) => x !== v)); }}>
            <Icon name="x" size={13} aria-hidden />
          </button>
        </span>
      ))}
      <input
        ref={inputRef}
        id={id}
        className="tokens-input"
        aria-label={ariaLabel}
        aria-invalid={ariaInvalid}
        aria-describedby={describedby}
        value={draft}
        placeholder={values.length === 0 ? placeholder : undefined}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ',') {
            e.preventDefault();
            add();
          } else if (e.key === 'Backspace' && !draft && values.length) {
            onChange(values.slice(0, -1));
          }
        }}
        onBlur={add}
      />
    </div>
  );
}
