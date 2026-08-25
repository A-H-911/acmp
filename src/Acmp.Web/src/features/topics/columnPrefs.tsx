import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../components/icons';
import { Menu } from '../../components/ui/Menu';
import type { Column } from '../../components/ui/Table';

/**
 * FR-032 / DW-033 (WBS-24.1) — user-configurable backlog columns: show/hide and reorder.
 *
 * ⚠ NO-REFERENCE COMPOSITION, and only for the PICKER (INV-014). `ACMP Backlog & Topic.dc.html`
 * DOES specify the dense table — eight columns at 112px / minmax(220px,1fr) / 124px / 150px /
 * 140px / 104px / 96px / 84px — and TopicsTable already matches it, so the table half of FR-032
 * shipped long ago. The design carries NO column-configuration control anywhere, so this picker
 * is composed from the shared design system (Menu popover, Icon, button classes) and flagged
 * here rather than silently invented.
 *
 * Preferences live in localStorage, per browser, following SubmitTopic.tsx's draft pattern:
 * FR-032 asks that a power user surface the fields relevant to THEIR workflow, and v1 has no
 * user-preferences endpoint.
 * ponytail: localStorage; move to an API only when prefs must follow a user across devices.
 *
 * Reordering is move-up/move-down buttons rather than drag-and-drop. Drag needs a keyboard and
 * screen-reader story of its own — WBS-23.2 built one for the kanban and it was not small — and
 * buttons are operable by both from the start. This is a preferences popover, not a canvas.
 */

const STORAGE_KEY = 'acmp.backlog.columns';

/**
 * The topic cell carries the row's title and the link into the topic, so hiding it leaves rows
 * unreadable and unclickable. Pinning one column is simpler — and more visible — than a
 * "you must keep at least one" rule that only appears when the user trips over it.
 */
export const PINNED_COLUMN = 'topic';

export interface ColumnPrefs {
  /** Every known column id, in display order. */
  order: string[];
  /** Ids the user has hidden. Never contains PINNED_COLUMN. */
  hidden: string[];
}

/**
 * Reads stored preferences and reconciles them against the columns that exist today. Unknown
 * ids are dropped and NEW ids are appended rather than discarded: a column added by a later
 * release must appear for existing users, instead of being invisible to exactly the people who
 * once configured this table.
 */
export function readColumnPrefs(allIds: string[]): ColumnPrefs {
  let stored: unknown = null;
  try {
    stored = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null');
  } catch {
    stored = null; // A corrupted or unreadable value must not take the backlog down with it.
  }
  const raw = (stored ?? {}) as Partial<ColumnPrefs>;
  const known = (ids: unknown) =>
    Array.isArray(ids) ? ids.filter((id): id is string => typeof id === 'string' && allIds.includes(id)) : [];

  const storedOrder = known(raw.order);
  return {
    order: [...storedOrder, ...allIds.filter((id) => !storedOrder.includes(id))],
    hidden: known(raw.hidden).filter((id) => id !== PINNED_COLUMN),
  };
}

/** Applies preferences to a column set: hidden columns removed, the rest in the stored order. */
export function applyColumnPrefs<T>(
  // Split across lines deliberately: scripts/check-hardcoded-strings.mjs matches `>...<` and reads
  // straight through TypeScript generics, so a one-line signature with two `Column<T>` in it is
  // reported as JSX text (trap 30 — a scanner that can measure itself). Every other generic here
  // happens to be multi-line already, which is why the gate has stayed green.
  columns: Column<T>[],
  prefs: ColumnPrefs,
): Column<T>[] {
  const byId = new Map(columns.map((c) => [c.id, c]));
  return prefs.order
    .filter((id) => !prefs.hidden.includes(id))
    .map((id) => byId.get(id))
    .filter((c): c is Column<T> => c !== undefined);
}

export function useColumnPrefs(allIds: string[]) {
  const [prefs, setPrefs] = useState<ColumnPrefs>(() => readColumnPrefs(allIds));

  const write = useCallback((next: ColumnPrefs) => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // A full or blocked store must not break the table — the change still applies this session.
    }
  }, []);

  const toggle = useCallback(
    (id: string) => {
      if (id === PINNED_COLUMN) return;
      setPrefs((p) => {
        const next = {
          ...p,
          hidden: p.hidden.includes(id) ? p.hidden.filter((h) => h !== id) : [...p.hidden, id],
        };
        write(next);
        return next;
      });
    },
    [write],
  );

  const move = useCallback(
    (id: string, delta: -1 | 1) => {
      setPrefs((p) => {
        const from = p.order.indexOf(id);
        const to = from + delta;
        if (from < 0 || to < 0 || to >= p.order.length) return p;
        const order = [...p.order];
        [order[from], order[to]] = [order[to], order[from]];
        const next = { ...p, order };
        write(next);
        return next;
      });
    },
    [write],
  );

  const reset = useCallback(() => {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Same reasoning as write(): a storage failure never blocks the reset taking effect below.
    }
    setPrefs({ order: [...allIds], hidden: [] });
  }, [allIds]);

  return { prefs, toggle, move, reset };
}

interface PickerProps {
  columns: { id: string; label: string }[];
  prefs: ColumnPrefs;
  onToggle: (id: string) => void;
  onMove: (id: string, delta: -1 | 1) => void;
  onReset: () => void;
}

export function ColumnPicker({ columns, prefs, onToggle, onMove, onReset }: PickerProps) {
  const { t } = useTranslation();
  const labels = new Map(columns.map((c) => [c.id, c.label]));
  const hiddenCount = prefs.hidden.length;

  return (
    <Menu
      label={t('topics.columns.label')}
      // align="start", NOT the default "end". This trigger sits at the inline-START of .bk-bar, so
      // anchoring the panel's END to the trigger's END throws it off-screen: measured in a real
      // browser at x=-123 in LTR and right-edge 1345 against a 1200px viewport in RTL. jsdom has no
      // layout, so no unit test can see this.
      align="start"
      triggerClassName="btn btn-secondary bk-cols-trigger"
      trigger={
        <>
          <Icon name="cog" size={14} aria-hidden />
          {t('topics.columns.trigger')}
          {hiddenCount > 0 && <span className="bk-cols-count">{hiddenCount}</span>}
        </>
      }
    >
      {() => (
        <div className="bk-cols">
          <ul className="bk-cols-list">
            {prefs.order.map((id, i) => {
              const label = labels.get(id);
              if (label === undefined) return null;
              const visible = !prefs.hidden.includes(id);
              const pinned = id === PINNED_COLUMN;
              return (
                <li key={id} className="bk-cols-row">
                  <button
                    type="button"
                    role="menuitemcheckbox"
                    aria-checked={visible}
                    className={`bk-cols-toggle${visible ? '' : ' is-hidden'}`}
                    disabled={pinned}
                    title={pinned ? t('topics.columns.pinned') : undefined}
                    onClick={() => onToggle(id)}
                  >
                    <Icon name="check" size={14} aria-hidden />
                    <span className="bk-cols-name">{label}</span>
                  </button>
                  <span className="bk-cols-move">
                    <button
                      type="button"
                      className="bk-cols-arrow"
                      disabled={i === 0}
                      aria-label={t('topics.columns.moveUp', { column: label })}
                      onClick={() => onMove(id, -1)}
                    >
                      <Icon name="chevronUp" size={13} aria-hidden />
                    </button>
                    <button
                      type="button"
                      className="bk-cols-arrow"
                      disabled={i === prefs.order.length - 1}
                      aria-label={t('topics.columns.moveDown', { column: label })}
                      onClick={() => onMove(id, 1)}
                    >
                      <Icon name="chevronDown" size={13} aria-hidden />
                    </button>
                  </span>
                </li>
              );
            })}
          </ul>
          <button type="button" className="bk-cols-reset" onClick={onReset}>
            {t('topics.columns.reset')}
          </button>
        </div>
      )}
    </Menu>
  );
}
