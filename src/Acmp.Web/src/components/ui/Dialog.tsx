import { useEffect, useId, useRef } from 'react';
import { createPortal } from 'react-dom';
import type { ReactNode } from 'react';

interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  /** Header icon tile tone (Design System §12: warn for confirmations, danger for destructive). */
  tone?: 'default' | 'warn' | 'danger';
  icon?: ReactNode;
  /** Extra body content below the description. */
  children?: ReactNode;
  /** Action buttons (rendered inline-end in the footer). */
  footer?: ReactNode;
}

/**
 * Modal dialog for consequential actions (Design System §12). Focus-trapped,
 * Esc-to-close, backdrop click closes, restores focus to the prior element on
 * unmount. Rendered via a portal so it escapes any overflow/stacking context.
 */
export function Dialog({ open, onClose, title, description, tone = 'default', icon, children, footer }: DialogProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const prevFocus = useRef<HTMLElement | null>(null);
  const titleId = useId();

  useEffect(() => {
    if (!open) return;
    prevFocus.current = document.activeElement as HTMLElement | null;
    const panel = panelRef.current;
    panel?.focus();

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
        return;
      }
      if (e.key === 'Tab' && panel) {
        const focusables = panel.querySelectorAll<HTMLElement>(
          'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
        );
        if (focusables.length === 0) return;
        const first = focusables[0];
        const last = focusables[focusables.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('keydown', onKey);
      prevFocus.current?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;

  return createPortal(
    <div className="dialog-overlay" onClick={onClose}>
      <div
        ref={panelRef}
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="dialog-body">
          {icon && <div className={`dialog-icon dialog-icon-${tone}`}>{icon}</div>}
          <h2 id={titleId} className="dialog-title">
            {title}
          </h2>
          {description && <p className="dialog-desc">{description}</p>}
          {children}
        </div>
        {footer && <div className="dialog-footer">{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}
