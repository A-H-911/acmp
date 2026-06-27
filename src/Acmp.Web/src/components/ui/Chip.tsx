import type { ReactNode } from 'react';

/** Stream / metadata tag (Design System §08). Neutral by default; `tone="info"`
 * for affected-stream chips (the topic-detail overview renders streams in the info tone). */
export function Tag({ children, className, tone = 'neutral' }: { children: ReactNode; className?: string; tone?: 'neutral' | 'info' }) {
  return <span className={`tag ${tone === 'info' ? 'tag-info' : ''} ${className ?? ''}`}>{children}</span>;
}

/** Count badge (Design System §08). `tone="danger"` for attention counts (e.g. unread). */
export function Badge({ count, tone = 'neutral', label }: { count: number; tone?: 'neutral' | 'danger'; label?: string }) {
  return (
    <span className={`badge ${tone === 'danger' ? 'badge-danger' : ''}`} aria-label={label}>
      {count}
    </span>
  );
}
