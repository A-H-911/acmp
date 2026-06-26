import { Fragment } from 'react';
import type { ReactNode } from 'react';
import { Icon } from '../icons';

export interface Crumb {
  label: ReactNode;
  href?: string;
  current?: boolean;
}

/** Breadcrumb trail (Design System §11). Separator chevrons mirror in RTL via .dir-flip. */
export function Breadcrumb({ items, ariaLabel }: { items: Crumb[]; ariaLabel: string }) {
  return (
    <nav className="breadcrumb" aria-label={ariaLabel}>
      {items.map((c, i) => (
        // Breadcrumbs are a fixed, non-reorderable trail, so the index key is stable.
        <Fragment key={i}>
          {i > 0 && <Icon name="chevron" size={13} className="breadcrumb-sep dir-flip" aria-hidden />}
          {c.current || !c.href ? (
            <span className="breadcrumb-current" aria-current={c.current ? 'page' : undefined}>
              {c.label}
            </span>
          ) : (
            <a href={c.href}>{c.label}</a>
          )}
        </Fragment>
      ))}
    </nav>
  );
}
