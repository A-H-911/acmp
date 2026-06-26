import type { ReactNode } from 'react';

/** Stream / metadata tag (Design System §08). Neutral, low-emphasis. */
export function Tag({ children, className }: { children: ReactNode; className?: string }) {
  return <span className={`tag ${className ?? ''}`}>{children}</span>;
}

/** Count badge (Design System §08). `tone="danger"` for attention counts (e.g. unread). */
export function Badge({ count, tone = 'neutral', label }: { count: number; tone?: 'neutral' | 'danger'; label?: string }) {
  return (
    <span className={`badge ${tone === 'danger' ? 'badge-danger' : ''}`} aria-label={label}>
      {count}
    </span>
  );
}
