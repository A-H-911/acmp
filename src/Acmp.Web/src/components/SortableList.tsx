/*
 * Shared accessible sortable list (ADR-0012). Pointer + keyboard dragging via
 * @dnd-kit, AND an always-present keyboard fallback (Move up / Move down
 * buttons) so reordering never depends on drag — docs/14 §5. Generic over the
 * item type; feature screens (kanban, agenda builder) consume it at their
 * phases. P3 ships the component + its test only.
 */
import { type ReactNode } from 'react';
import {
  DndContext, KeyboardSensor, PointerSensor, useSensor, useSensors,
  closestCenter, type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext, arrayMove, sortableKeyboardCoordinates,
  useSortable, verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { useTranslation } from 'react-i18next';
import { Icon } from './icons';

interface SortableListProps<T> {
  items: T[];
  getId: (item: T) => string;
  onReorder: (items: T[]) => void;
  renderItem: (item: T) => ReactNode;
  /** Accessible name of a row, used on the handle + move buttons. */
  getLabel: (item: T) => string;
  ariaLabel: string;
}

interface RowProps<T> {
  item: T;
  id: string;
  label: string;
  index: number;
  count: number;
  onMove: (from: number, to: number) => void;
  children: ReactNode;
}

function SortableRow<T>({ id, label, index, count, onMove, children }: RowProps<T>) {
  const { t } = useTranslation();
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id });
  const style = { transform: CSS.Transform.toString(transform), transition };
  return (
    <li ref={setNodeRef} style={style} className={`sortable-item ${isDragging ? 'dragging' : ''}`}>
      <button
        type="button"
        className="sortable-handle"
        aria-label={t('dnd.dragHandle', { name: label })}
        {...attributes}
        {...listeners}
      >
        <Icon name="grip" size={16} />
      </button>
      <div style={{ flex: '1 1 auto', minInlineSize: 0 }}>{children}</div>
      <button
        type="button"
        className="sortable-move"
        aria-label={t('dnd.moveUp', { name: label })}
        disabled={index === 0}
        onClick={() => onMove(index, index - 1)}
      >
        <Icon name="arrowUp" size={14} />
      </button>
      <button
        type="button"
        className="sortable-move"
        aria-label={t('dnd.moveDown', { name: label })}
        disabled={index === count - 1}
        onClick={() => onMove(index, index + 1)}
      >
        <Icon name="arrowDown" size={14} />
      </button>
    </li>
  );
}

export function SortableList<T>({ items, getId, onReorder, renderItem, getLabel, ariaLabel }: SortableListProps<T>) {
  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const move = (from: number, to: number) => {
    if (to < 0 || to >= items.length) return;
    onReorder(arrayMove(items, from, to));
  };

  /* v8 ignore start -- @dnd-kit pointer-drag end: jsdom cannot dispatch the sensor's
     pointer sequence, so this fires only in a real browser. The accessible keyboard
     reorder (Move up/down buttons → `move`) is unit-tested; the drag path is covered
     by the S6 Playwright E2E. */
  const onDragEnd = (e: DragEndEvent) => {
    const { active, over } = e;
    if (!over || active.id === over.id) return;
    const from = items.findIndex((i) => getId(i) === active.id);
    const to = items.findIndex((i) => getId(i) === over.id);
    if (from !== -1 && to !== -1) onReorder(arrayMove(items, from, to));
  };
  /* v8 ignore stop */

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
      <SortableContext items={items.map(getId)} strategy={verticalListSortingStrategy}>
        <ul aria-label={ariaLabel} style={{ listStyle: 'none', margin: 0, padding: 0 }}>
          {items.map((item, index) => (
            <SortableRow
              key={getId(item)}
              item={item}
              id={getId(item)}
              label={getLabel(item)}
              index={index}
              count={items.length}
              onMove={move}
            >
              {renderItem(item)}
            </SortableRow>
          ))}
        </ul>
      </SortableContext>
    </DndContext>
  );
}
