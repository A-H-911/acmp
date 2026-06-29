import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Pagination } from './Pagination';
import { Select } from './Select';
import { Dialog } from './Dialog';
import { Field } from './Field';
import { DateField } from './DateField';
import { MultiSelect } from './MultiSelect';

// Branch/interaction coverage for the UI primitives whose keyboard/edge paths the existing
// suites don't reach. Behaviour-first: assert what the user/AT actually observes.

describe('Pagination', () => {
  const labels = { nav: 'Pagination', previous: 'Previous', next: 'Next' };

  it('renders nothing for a single page', () => {
    const { container } = render(
      <Pagination page={1} pageCount={1} onPageChange={vi.fn()} labels={labels} />,
    );
    expect(container.firstChild).toBeNull();
  });

  it('disables Previous on the first page and pages forward', () => {
    const onPageChange = vi.fn();
    render(<Pagination page={1} pageCount={3} onPageChange={onPageChange} labels={labels} />);
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: '1' })).toHaveAttribute('aria-current', 'page');

    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(onPageChange).toHaveBeenCalledWith(2);
    fireEvent.click(screen.getByRole('button', { name: '3' }));
    expect(onPageChange).toHaveBeenCalledWith(3);
  });

  it('disables Next on the last page and pages back', () => {
    const onPageChange = vi.fn();
    render(<Pagination page={3} pageCount={3} onPageChange={onPageChange} labels={labels} />);
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Previous' }));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });
});

describe('Select keyboard', () => {
  const options = [
    { value: 'a', label: 'Apple' },
    { value: 'b', label: 'Banana' },
    { value: 'c', label: 'Cherry' },
  ];

  it('opens from the trigger with ArrowDown and selects via Home/End/Arrows + Enter', () => {
    const onChange = vi.fn();
    render(<Select options={options} value="a" onChange={onChange} ariaLabel="Fruit" />);
    const trigger = screen.getByRole('button', { name: 'Fruit' });

    fireEvent.keyDown(trigger, { key: 'ArrowDown' }); // openAtSelected
    const listbox = screen.getByRole('listbox');
    fireEvent.keyDown(listbox, { key: 'ArrowDown' }); // active -> 1
    fireEvent.keyDown(listbox, { key: 'End' }); // -> 2
    fireEvent.keyDown(listbox, { key: 'Home' }); // -> 0
    fireEvent.keyDown(listbox, { key: 'ArrowUp' }); // clamp at 0
    fireEvent.keyDown(listbox, { key: 'ArrowDown' }); // -> 1 (Banana)
    fireEvent.keyDown(listbox, { key: 'Enter' });

    expect(onChange).toHaveBeenCalledWith('b');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument(); // closed after choose
  });

  it('opens with Enter and closes on Escape, returning focus to the trigger', () => {
    render(<Select options={options} onChange={vi.fn()} ariaLabel="Fruit" placeholder="Pick" />);
    const trigger = screen.getByRole('button', { name: 'Fruit' });
    fireEvent.keyDown(trigger, { key: 'Enter' });
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });
});

describe('Dialog focus trap', () => {
  it('wraps Tab/Shift+Tab within the dialog and closes on Escape', () => {
    const onClose = vi.fn();
    render(
      <Dialog
        open
        onClose={onClose}
        title="Confirm"
        footer={
          <>
            <button type="button">One</button>
            <button type="button">Two</button>
          </>
        }
      />,
    );
    const first = screen.getByRole('button', { name: 'One' });
    const last = screen.getByRole('button', { name: 'Two' });

    last.focus();
    fireEvent.keyDown(document, { key: 'Tab' });
    expect(first).toHaveFocus(); // forward-wrap

    first.focus();
    fireEvent.keyDown(document, { key: 'Tab', shiftKey: true });
    expect(last).toHaveFocus(); // backward-wrap

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalled();
  });
});

describe('Field help text', () => {
  it('renders help and wires aria-describedby when there is no error', () => {
    render(
      <Field label="Email" help="We never share it.">
        {(props) => <input {...props} />}
      </Field>,
    );
    expect(screen.getByText('We never share it.')).toBeInTheDocument();
    const input = screen.getByLabelText('Email');
    expect(input).toHaveAttribute('aria-describedby');
    expect(input).not.toHaveAttribute('aria-invalid');
  });
});

describe('MultiSelect', () => {
  const options = [
    { value: 'a', label: 'Apple' },
    { value: 'b', label: 'Banana' },
  ];
  const common = {
    options,
    value: [] as string[],
    onChange: vi.fn(),
    ariaLabel: 'Fruit',
    removeLabel: (l: string) => `Remove ${l}`,
  };

  it('shows the empty label when the filter matches nothing', () => {
    render(<MultiSelect {...common} emptyLabel="No matches" />);
    const input = screen.getByRole('combobox', { name: 'Fruit' });
    fireEvent.focus(input); // opens the listbox
    fireEvent.change(input, { target: { value: 'zzz' } });
    expect(screen.getByText('No matches')).toBeInTheDocument();
  });

  it('closes the listbox on Escape', () => {
    render(<MultiSelect {...common} />);
    const input = screen.getByRole('combobox', { name: 'Fruit' });
    fireEvent.focus(input);
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });
});

describe('DateField', () => {
  const labels = { previousMonth: 'Prev', nextMonth: 'Next' };

  it('opens the calendar popover and closes it on Escape', () => {
    render(
      <DateField onChange={vi.fn()} placeholder="Pick a date" labels={labels} ariaLabel="Date" />,
    );
    const trigger = screen.getByRole('button', { name: 'Date' });
    fireEvent.click(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
  });
});
