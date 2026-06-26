import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size = 'sm' | 'md' | 'lg';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  /** Square, centered icon-only button (Design System §05 "More"). */
  iconOnly?: boolean;
  /** Shows a spinner and disables the button while an action is in flight. */
  loading?: boolean;
}

/**
 * Token-driven button matching the Design System §05 anatomy (variant × size,
 * icon-only, loading, disabled). Defaults to type="button" to avoid accidental
 * form submits; loading implies disabled + aria-busy.
 */
export function Button({
  variant = 'primary',
  size = 'md',
  iconOnly = false,
  loading = false,
  type = 'button',
  className,
  disabled,
  children,
  ...rest
}: ButtonProps) {
  const classes = ['btn', `btn-${variant}`, size !== 'md' && `btn-${size}`, iconOnly && 'btn-icon', className]
    .filter(Boolean)
    .join(' ');
  return (
    <button type={type} className={classes} disabled={disabled || loading} aria-busy={loading || undefined} {...rest}>
      {loading && <span className="btn-spinner" aria-hidden="true" />}
      {children}
    </button>
  );
}
