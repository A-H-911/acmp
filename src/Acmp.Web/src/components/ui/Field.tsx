import { useId } from 'react';
import type { InputHTMLAttributes, ReactNode, TextareaHTMLAttributes } from 'react';
import { Icon } from '../icons';

/** Props the Field injects into its control so label/help/error are associated (WCAG §06). */
export interface FieldControlProps {
  id: string;
  'aria-invalid'?: true;
  'aria-describedby'?: string;
}

interface FieldProps {
  label: string;
  required?: boolean;
  help?: string;
  /** When set, renders an inline role="alert" message and marks the control invalid. */
  error?: string;
  children: (props: FieldControlProps) => ReactNode;
}

/**
 * Form field wrapper: always-visible associated label, required marker, help text,
 * and inline announced validation. Wires id/aria-invalid/aria-describedby into the
 * control via a render prop so association is automatic, not hand-maintained.
 */
export function Field({ label, required, help, error, children }: FieldProps) {
  const id = useId();
  const helpId = `${id}-help`;
  const errId = `${id}-err`;
  return (
    <div className="field">
      <label className="field-label" htmlFor={id}>
        {label}
        {required && <span className="field-req" aria-hidden="true">*</span>}
      </label>
      {children({ id, 'aria-invalid': error ? true : undefined, 'aria-describedby': error ? errId : help ? helpId : undefined })}
      {error ? (
        <p className="field-error" id={errId} role="alert">
          <Icon name="alertCircle" size={13} aria-hidden />
          {error}
        </p>
      ) : help ? (
        <p className="field-help" id={helpId}>
          {help}
        </p>
      ) : null}
    </div>
  );
}

export function Input({ className, ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={`input ${className ?? ''}`} {...rest} />;
}

export function Textarea({ className, ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={`textarea ${className ?? ''}`} {...rest} />;
}
