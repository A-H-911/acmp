import type { InputHTMLAttributes } from 'react';
import { Icon } from '../icons';

type ChoiceBase = Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> & { label: string };

/** 18px checkbox (Design System §06). The native input is visually hidden; the box renders the state. */
export function Checkbox({ label, className, ...rest }: ChoiceBase) {
  return (
    <label className={`choice ${className ?? ''}`}>
      <input type="checkbox" {...rest} />
      <span className="choice-box" aria-hidden="true">
        <Icon name="check" size={12} />
      </span>
      {label}
    </label>
  );
}

/** 18px radio. Group via a shared `name`. */
export function Radio({ label, className, ...rest }: ChoiceBase) {
  return (
    <label className={`choice ${className ?? ''}`}>
      <input type="radio" {...rest} />
      <span className="choice-box radio" aria-hidden="true">
        <span className="choice-radio-dot" />
      </span>
      {label}
    </label>
  );
}

/** 36×20 toggle exposed as role="switch" (Design System §06). */
export function Toggle({ label, className, ...rest }: ChoiceBase) {
  return (
    <label className={`toggle ${className ?? ''}`}>
      <input type="checkbox" role="switch" {...rest} />
      <span className="toggle-track" aria-hidden="true">
        <span className="toggle-knob" />
      </span>
      {label}
    </label>
  );
}
