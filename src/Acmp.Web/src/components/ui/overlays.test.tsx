import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Menu, MenuItem } from './Menu';
import { Dialog } from './Dialog';
import { ToastProvider, useToast } from './Toast';
import { Button } from './Button';

describe('Menu', () => {
  it('opens on click and closes on Escape', async () => {
    render(
      <Menu label="Account" trigger="Open">
        {() => <MenuItem>Log out</MenuItem>}
      </Menu>,
    );
    expect(screen.queryByRole('menu')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Open' }));
    expect(screen.getByRole('menu', { name: 'Account' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('menu')).toBeNull();
  });
});

describe('Dialog', () => {
  it('renders a modal dialog when open and Esc invokes onClose', async () => {
    const onClose = vi.fn();
    render(
      <Dialog open onClose={onClose} title="Publish agenda?" description="Members notified." footer={<button>OK</button>} />,
    );
    const dlg = screen.getByRole('dialog', { name: 'Publish agenda?' });
    expect(dlg).toHaveAttribute('aria-modal', 'true');
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
  });

  it('renders nothing when closed', () => {
    render(<Dialog open={false} onClose={() => {}} title="x" />);
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});

describe('Toast', () => {
  it('queues a toast and dismisses it', async () => {
    function Fire() {
      const { toast } = useToast();
      return <Button onClick={() => toast({ tone: 'success', title: 'Saved' })}>Fire</Button>;
    }
    render(
      <ToastProvider regionLabel="Notifications">
        <Fire />
      </ToastProvider>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Fire' }));
    expect(screen.getByText('Saved')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByText('Saved')).toBeNull();
  });
});
