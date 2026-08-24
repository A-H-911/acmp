import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import { SortableList } from './SortableList';

interface Row { id: string; name: string; }
const rows: Row[] = [
  { id: 'a', name: 'Alpha' },
  { id: 'b', name: 'Bravo' },
  { id: 'c', name: 'Charlie' },
];

function setup(onReorder = vi.fn()) {
  render(
    <I18nextProvider i18n={i18n}>
      <SortableList
        items={rows}
        getId={(r) => r.id}
        getLabel={(r) => r.name}
        onReorder={onReorder}
        ariaLabel="Test list"
        renderItem={(r) => <span>{r.name}</span>}
      />
    </I18nextProvider>,
  );
  return onReorder;
}

describe('SortableList keyboard fallback', () => {
  it('reorders via the Move down control (no drag needed)', async () => {
    const user = userEvent.setup();
    const onReorder = setup();
    await user.click(screen.getByRole('button', { name: /Move Alpha down/i }));
    expect(onReorder).toHaveBeenCalledWith([
      { id: 'b', name: 'Bravo' },
      { id: 'a', name: 'Alpha' },
      { id: 'c', name: 'Charlie' },
    ]);
  });

  // The suite asserted Move up is DISABLED on the first row but never clicked an
  // ENABLED one, so the upward branch had never executed. coverage-v8 v4 surfaced
  // it (line 62); v3 credited the line for the JSX around the handler.
  it('reorders via the Move up control on a row where it is enabled', async () => {
    const user = userEvent.setup();
    const onReorder = setup();
    await user.click(screen.getByRole('button', { name: /Move Bravo up/i }));
    expect(onReorder).toHaveBeenCalledWith([
      { id: 'b', name: 'Bravo' },
      { id: 'a', name: 'Alpha' },
      { id: 'c', name: 'Charlie' },
    ]);
  });

  it('disables Move up on the first row and Move down on the last', () => {
    setup();
    expect(screen.getByRole('button', { name: /Move Alpha up/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Move Charlie down/i })).toBeDisabled();
  });
});
