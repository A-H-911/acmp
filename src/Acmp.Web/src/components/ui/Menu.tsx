import { useEffect, useRef, useState } from 'react';
import type { ButtonHTMLAttributes, ReactNode } from 'react';

interface MenuProps {
  /** Trigger button content (Menu owns the <button> + aria-haspopup/expanded). */
  trigger: ReactNode;
  triggerClassName?: string;
  triggerProps?: ButtonHTMLAttributes<HTMLButtonElement>;
  /** Accessible name for the menu panel. */
  label: string;
  /** Panel content; receives close() to dismiss after an action. */
  children: (close: () => void) => ReactNode;
  align?: 'start' | 'end';
  panelClassName?: string;
}

/**
 * Dismissable popup menu (Design System profile/role/select pattern). Click toggles;
 * a full-viewport backdrop catches outside clicks; Esc closes and returns focus to
 * the trigger. Panel positions to the inline-start/end edge and mirrors in RTL.
 */
export function Menu({ trigger, triggerClassName, triggerProps, label, children, align = 'end', panelClassName }: MenuProps) {
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);

  const close = () => {
    setOpen(false);
    triggerRef.current?.focus();
  };

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open]);

  return (
    <div className="menu">
      <button
        {...triggerProps}
        ref={triggerRef}
        type="button"
        className={triggerClassName}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        {trigger}
      </button>
      {open && (
        <>
          <div className="menu-backdrop" onClick={() => setOpen(false)} aria-hidden="true" />
          <div className={`menu-panel menu-panel-${align} ${panelClassName ?? ''}`} role="menu" aria-label={label}>
            {children(close)}
          </div>
        </>
      )}
    </div>
  );
}

interface MenuItemProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  icon?: ReactNode;
  checked?: boolean;
}

/** A menu row (role=menuitem, or menuitemradio when `checked` is set). */
export function MenuItem({ icon, checked, children, className, ...rest }: MenuItemProps) {
  return (
    <button
      type="button"
      role={checked === undefined ? 'menuitem' : 'menuitemradio'}
      aria-checked={checked}
      className={`menu-item ${className ?? ''}`}
      {...rest}
    >
      {icon}
      {children}
    </button>
  );
}

export function MenuSeparator() {
  return <div className="menu-sep" role="separator" />;
}
