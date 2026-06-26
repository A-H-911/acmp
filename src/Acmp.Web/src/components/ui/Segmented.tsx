import type { ReactNode } from 'react';

export interface SegmentedItem {
  id: string;
  label: ReactNode;
}

interface SegmentedProps {
  items: SegmentedItem[];
  value: string;
  onValueChange: (id: string) => void;
  /** Accessible name for the group (caller-localized). */
  ariaLabel: string;
}

/** Segmented control for view modes — list / table / kanban (Design System §10). */
export function Segmented({ items, value, onValueChange, ariaLabel }: SegmentedProps) {
  return (
    <div className="segmented" role="group" aria-label={ariaLabel}>
      {items.map((it) => (
        <button
          key={it.id}
          type="button"
          className="segmented-item"
          aria-pressed={it.id === value}
          onClick={() => onValueChange(it.id)}
        >
          {it.label}
        </button>
      ))}
    </div>
  );
}
