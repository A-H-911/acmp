import { Fragment } from 'react';
import type { ReactNode } from 'react';
import { Icon } from '../icons';

export interface Crumb {
  label: ReactNode;
  href?: string;
  current?: boolean;
}

/**
 * Only relative paths/fragments and explicit http(s)/mailto schemes are linkable.
 * `javascript:`, `data:`, and malformed URLs fall through to plain text — a crumb
 * href can originate from data, so the scheme is validated here, not trusted.
 */
function safeHref(u?: string): string | undefined {
  if (!u) return undefined;
  if (u.startsWith('/') || u.startsWith('#')) return u;
  try {
    const p = new URL(u, window.location.origin);
    if (['http:', 'https:', 'mailto:'].includes(p.protocol)) return u;
  } catch {
    /* malformed → unsafe */
  }
  return undefined;
}

/** Breadcrumb trail (Design System §11). Separator chevrons mirror in RTL via .dir-flip. */
export function Breadcrumb({ items, ariaLabel }: { items: Crumb[]; ariaLabel: string }) {
  return (
    <nav className="breadcrumb" aria-label={ariaLabel}>
      {items.map((c, i) => {
        const href = c.current ? undefined : safeHref(c.href);
        return (
          // Breadcrumbs are a fixed, non-reorderable trail, so the index key is stable.
          <Fragment key={i}>
            {i > 0 && <Icon name="chevron" size={13} className="breadcrumb-sep dir-flip" aria-hidden />}
            {href ? (
              <a href={href}>{c.label}</a>
            ) : (
              <span className="breadcrumb-current" aria-current={c.current ? 'page' : undefined}>
                {c.label}
              </span>
            )}
          </Fragment>
        );
      })}
    </nav>
  );
}
