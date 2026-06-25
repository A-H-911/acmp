import type { HTMLAttributes } from 'react';

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** Apply default internal padding. Off when the card hosts its own layout. */
  padded?: boolean;
}

export function Card({ padded = true, className, ...rest }: CardProps) {
  return <div className={`card ${padded ? 'card-pad' : ''} ${className ?? ''}`} {...rest} />;
}
