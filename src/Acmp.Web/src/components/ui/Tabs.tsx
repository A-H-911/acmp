import type { ReactNode } from 'react';

export interface TabItem {
  id: string;
  label: ReactNode;
  disabled?: boolean;
}

interface TabsProps {
  items: TabItem[];
  value: string;
  onValueChange: (id: string) => void;
  /** Accessible name for the tablist (caller-localized). */
  ariaLabel: string;
}

/** Underline tabs for content sections (Design System §10). */
export function Tabs({ items, value, onValueChange, ariaLabel }: TabsProps) {
  return (
    <div className="tabs" role="tablist" aria-label={ariaLabel}>
      {items.map((it) => (
        <button
          key={it.id}
          type="button"
          role="tab"
          className="tab"
          aria-selected={it.id === value}
          disabled={it.disabled}
          onClick={() => onValueChange(it.id)}
        >
          {it.label}
        </button>
      ))}
    </div>
  );
}
