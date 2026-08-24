import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Menu } from './Menu';

/**
 * Menu had no test file. coverage-v8 v4 named line 55 - the outside-click backdrop's
 * onClick - which meant the component's DISMISS behaviour was entirely unexercised
 * (DW-082). The component documents three dismissal paths: the trigger toggle, the
 * backdrop, and Escape-with-focus-return. All three are asserted here.
 */
function setup() {
  return render(
    <Menu trigger="Open" label="Test menu">
      {(close) => (
        <button type="button" role="menuitem" onClick={close}>
          Item
        </button>
      )}
    </Menu>,
  );
}

describe('Menu', () => {
  it('toggles the panel from the trigger and reflects state in aria-expanded', async () => {
    const user = userEvent.setup();
    setup();
    const trigger = screen.getByRole('button', { name: 'Open' });
    expect(trigger).toHaveAttribute('aria-expanded', 'false');

    await user.click(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('menu', { name: 'Test menu' })).toBeInTheDocument();
  });

  it('closes when the outside-click backdrop is clicked', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Open' }));

    // The backdrop is aria-hidden by design, so it is not reachable by role -
    // query the element the component actually renders for outside clicks.
    const backdrop = document.querySelector('.menu-backdrop');
    expect(backdrop).not.toBeNull();
    fireEvent.click(backdrop as Element);

    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('closes on Escape and returns focus to the trigger', async () => {
    const user = userEvent.setup();
    setup();
    const trigger = screen.getByRole('button', { name: 'Open' });
    await user.click(trigger);

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('menu')).toBeNull();
    expect(trigger).toHaveFocus();
  });

  it('dismisses via the close() handed to the panel content', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Open' }));

    await user.click(screen.getByRole('menuitem', { name: 'Item' }));

    expect(screen.queryByRole('menu')).toBeNull();
  });
});
