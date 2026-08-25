import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import {
  ColumnPicker,
  PINNED_COLUMN,
  applyColumnPrefs,
  readColumnPrefs,
  useColumnPrefs,
  type ColumnPrefs,
} from './columnPrefs';
import type { Column } from '../../components/ui/Table';

const KEY = 'acmp.backlog.columns';
const IDS = ['key', 'topic', 'type', 'status'];

/** Minimal columns — only `id` matters to applyColumnPrefs; the cells are never rendered here. */
const COLUMNS: Column<{ id: string }>[] = IDS.map((id) => ({ id, header: id, cell: () => id }));

beforeEach(() => {
  localStorage.clear();
  vi.restoreAllMocks();
});

describe('readColumnPrefs', () => {
  it('returns every known column in declaration order when nothing is stored', () => {
    expect(readColumnPrefs(IDS)).toEqual({ order: IDS, hidden: [] });
  });

  it('survives a corrupted stored value instead of taking the backlog down', () => {
    localStorage.setItem(KEY, 'not json {{{');
    expect(readColumnPrefs(IDS)).toEqual({ order: IDS, hidden: [] });
  });

  it('drops ids that no longer exist and APPENDS ids the stored order never knew', () => {
    // 'legacy' was removed from the app; 'status' is newer than this stored preference.
    localStorage.setItem(KEY, JSON.stringify({ order: ['type', 'legacy', 'key', 'topic'], hidden: ['legacy'] }));
    const prefs = readColumnPrefs(IDS);
    expect(prefs.order).toEqual(['type', 'key', 'topic', 'status']);
    expect(prefs.hidden).toEqual([]);
  });

  it('never lets the pinned column come back hidden from storage', () => {
    localStorage.setItem(KEY, JSON.stringify({ order: IDS, hidden: [PINNED_COLUMN, 'type'] }));
    expect(readColumnPrefs(IDS).hidden).toEqual(['type']);
  });

  it('ignores a stored shape whose fields are not arrays', () => {
    localStorage.setItem(KEY, JSON.stringify({ order: 'key', hidden: 7 }));
    expect(readColumnPrefs(IDS)).toEqual({ order: IDS, hidden: [] });
  });
});

describe('applyColumnPrefs', () => {
  it('removes hidden columns and returns the rest in the stored order', () => {
    const prefs: ColumnPrefs = { order: ['status', 'topic', 'key', 'type'], hidden: ['key'] };
    expect(applyColumnPrefs(COLUMNS, prefs).map((c) => c.id)).toEqual(['status', 'topic', 'type']);
  });

  it('skips an ordered id that has no matching column', () => {
    const prefs: ColumnPrefs = { order: ['topic', 'ghost'], hidden: [] };
    expect(applyColumnPrefs(COLUMNS, prefs).map((c) => c.id)).toEqual(['topic']);
  });
});

/** Drives the hook through the real picker, which is also how the coverage gate sees the handlers. */
function Harness() {
  const { prefs, toggle, move, reset } = useColumnPrefs(IDS);
  return (
    <>
      <output data-testid="order">{prefs.order.join(',')}</output>
      <output data-testid="hidden">{prefs.hidden.join(',')}</output>
      <ColumnPicker
        columns={[...IDS.map((id) => ({ id, label: id })), { id: 'ghost', label: 'ghost' }]}
        prefs={prefs}
        onToggle={toggle}
        onMove={move}
        onReset={reset}
      />
    </>
  );
}

async function openPicker() {
  const user = userEvent.setup();
  render(<Harness />);
  await user.click(screen.getByRole('button', { name: /columns/i }));
  return user;
}

describe('ColumnPicker + useColumnPrefs', () => {
  it('hides a column, persists it, and shows how many are hidden', async () => {
    const user = await openPicker();
    await user.click(screen.getByRole('menuitemcheckbox', { name: 'type' }));

    expect(screen.getByTestId('hidden')).toHaveTextContent('type');
    expect(JSON.parse(localStorage.getItem(KEY)!).hidden).toEqual(['type']);
    // The trigger carries the count so a user who hid a column can see that from outside.
    expect(screen.getByRole('button', { name: /columns/i })).toHaveTextContent('1');
  });

  it('shows a hidden column again when toggled back', async () => {
    const user = await openPicker();
    const type = () => screen.getByRole('menuitemcheckbox', { name: 'type' });
    await user.click(type());
    expect(type()).toHaveAttribute('aria-checked', 'false');
    await user.click(type());
    expect(type()).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByTestId('hidden')).toHaveTextContent('');
  });

  it('refuses to hide the pinned column — the control is disabled, not merely ignored', async () => {
    await openPicker();
    const pinned = screen.getByRole('menuitemcheckbox', { name: PINNED_COLUMN });
    expect(pinned).toBeDisabled();
    expect(pinned).toHaveAttribute('aria-checked', 'true');
  });

  it('ignores a programmatic attempt to hide the pinned column', async () => {
    // The disabled button cannot be clicked, so the guard inside toggle() is proven directly:
    // without it, a caller reaching past the UI could empty the table.
    function Direct() {
      const { prefs, toggle } = useColumnPrefs(IDS);
      return (
        <button type="button" onClick={() => toggle(PINNED_COLUMN)}>
          hidden:{prefs.hidden.join(',') || 'none'}
        </button>
      );
    }
    const user = userEvent.setup();
    render(<Direct />);
    await user.click(screen.getByRole('button'));
    expect(screen.getByRole('button')).toHaveTextContent('hidden:none');
  });

  it('moves a column earlier and later, and persists each move', async () => {
    const user = await openPicker();
    await user.click(screen.getByRole('button', { name: /move type earlier/i }));
    expect(screen.getByTestId('order')).toHaveTextContent('key,type,topic,status');

    await user.click(screen.getByRole('button', { name: /move type later/i }));
    expect(screen.getByTestId('order')).toHaveTextContent('key,topic,type,status');
    expect(JSON.parse(localStorage.getItem(KEY)!).order).toEqual(['key', 'topic', 'type', 'status']);
  });

  it('disables the arrows at each end rather than silently doing nothing', async () => {
    await openPicker();
    expect(screen.getByRole('button', { name: /move key earlier/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /move status later/i })).toBeDisabled();
  });

  it('restores the default order and clears storage on reset', async () => {
    const user = await openPicker();
    await user.click(screen.getByRole('menuitemcheckbox', { name: 'type' }));
    await user.click(screen.getByRole('button', { name: /move status earlier/i }));
    await user.click(screen.getByRole('button', { name: /reset/i }));

    expect(screen.getByTestId('order')).toHaveTextContent(IDS.join(','));
    expect(screen.getByTestId('hidden')).toHaveTextContent('');
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('does not render a preference for a column that no longer exists', async () => {
    await openPicker();
    // 'ghost' is in the label list but not in prefs.order, so it must not appear as a row.
    expect(screen.queryByRole('menuitemcheckbox', { name: 'ghost' })).not.toBeInTheDocument();
  });

  it('keeps working when localStorage refuses the write', async () => {
    const setItem = vi.spyOn(localStorage, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });
    const user = await openPicker();
    await user.click(screen.getByRole('menuitemcheckbox', { name: 'type' }));

    // The change still applies for this session; only the persistence was lost.
    expect(screen.getByTestId('hidden')).toHaveTextContent('type');
    expect(setItem).toHaveBeenCalled();
  });

  it('still resets when localStorage refuses the removal', async () => {
    const user = await openPicker();
    await user.click(screen.getByRole('menuitemcheckbox', { name: 'type' }));
    vi.spyOn(localStorage, 'removeItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });
    await user.click(screen.getByRole('button', { name: /reset/i }));
    expect(screen.getByTestId('hidden')).toHaveTextContent('');
  });

  it('reads a corrupted store as defaults when the getter itself throws', () => {
    vi.spyOn(localStorage, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });
    expect(readColumnPrefs(IDS)).toEqual({ order: IDS, hidden: [] });
  });

  it('leaves the order untouched when asked to move an id it does not hold', async () => {
    function Direct() {
      const { prefs, move } = useColumnPrefs(IDS);
      const [, force] = useState(0);
      return (
        <button
          type="button"
          onClick={() => {
            move('ghost', 1);
            force((n) => n + 1);
          }}
        >
          {prefs.order.join(',')}
        </button>
      );
    }
    const user = userEvent.setup();
    render(<Direct />);
    await user.click(screen.getByRole('button'));
    expect(screen.getByRole('button')).toHaveTextContent(IDS.join(','));
  });
});
