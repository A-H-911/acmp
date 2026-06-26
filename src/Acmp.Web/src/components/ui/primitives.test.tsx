import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button } from './Button';
import { Field, Input } from './Field';
import { Checkbox, Toggle } from './Choice';
import { Tabs } from './Tabs';
import { Badge, Tag } from './Chip';

describe('Button', () => {
  it('loading disables the button and marks it busy', () => {
    render(<Button loading>Save</Button>);
    const btn = screen.getByRole('button', { name: 'Save' });
    expect(btn).toBeDisabled();
    expect(btn).toHaveAttribute('aria-busy', 'true');
  });

  it('applies variant and size classes', () => {
    render(
      <Button variant="ghost" size="lg">
        Go
      </Button>,
    );
    const btn = screen.getByRole('button', { name: 'Go' });
    expect(btn.className).toContain('btn-ghost');
    expect(btn.className).toContain('btn-lg');
  });
});

describe('Field', () => {
  it('associates the label and announces the error', () => {
    render(
      <Field label="Effective date" required error="Required before publishing">
        {(p) => <Input {...p} />}
      </Field>,
    );
    const input = screen.getByLabelText(/Effective date/);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Required before publishing');
    expect(input).toHaveAttribute('aria-describedby', alert.id);
  });
});

describe('Choice', () => {
  it('checkbox reflects checked state', () => {
    render(<Checkbox label="Notify members" defaultChecked readOnly />);
    expect(screen.getByRole('checkbox', { name: 'Notify members' })).toBeChecked();
  });

  it('toggle exposes role switch', () => {
    render(<Toggle label="Autosave" defaultChecked readOnly />);
    expect(screen.getByRole('switch', { name: 'Autosave' })).toBeChecked();
  });
});

describe('Tabs', () => {
  it('marks the active tab selected and fires change', async () => {
    const onChange = vi.fn();
    render(
      <Tabs
        ariaLabel="Sections"
        value="a"
        onValueChange={onChange}
        items={[
          { id: 'a', label: 'A' },
          { id: 'b', label: 'B' },
        ]}
      />,
    );
    expect(screen.getByRole('tab', { name: 'A' })).toHaveAttribute('aria-selected', 'true');
    await userEvent.click(screen.getByRole('tab', { name: 'B' }));
    expect(onChange).toHaveBeenCalledWith('b');
  });
});

describe('Chip', () => {
  it('renders tag and count badge', () => {
    render(
      <>
        <Tag>Identity</Tag>
        <Badge count={3} tone="danger" label="3 unread" />
      </>,
    );
    expect(screen.getByText('Identity')).toBeInTheDocument();
    expect(screen.getByLabelText('3 unread')).toHaveTextContent('3');
  });
});
