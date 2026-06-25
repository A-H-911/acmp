import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'ghost';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
}

/** Token-driven button. Defaults to type="button" to avoid accidental submits. */
export function Button({ variant = 'primary', type = 'button', className, ...rest }: ButtonProps) {
  return <button type={type} className={`btn btn-${variant} ${className ?? ''}`} {...rest} />;
}
