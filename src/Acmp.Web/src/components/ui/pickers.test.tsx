import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MultiSelect } from './MultiSelect';
import { DatePicker } from './DatePicker';

describe('MultiSelect', () => {
  it('adds a token from the list and removes it', async () => {
    function Host() {
      const [v, setV] = useState<string[]>([]);
      return (
        <MultiSelect
          ariaLabel="Affected systems"
          placeholder="Add system…"
          value={v}
          onChange={setV}
          removeLabel={(l) => `Remove ${l}`}
          options={[
            { value: 'a', label: 'Identity' },
            { value: 'b', label: 'Payments' },
          ]}
        />
      );
    }
    render(<Host />);
    await userEvent.click(screen.getByPlaceholderText('Add system…'));
    const listbox = screen.getByRole('listbox', { name: 'Affected systems' });
    await userEvent.click(within(listbox).getByRole('option', { name: 'Identity' }));
    expect(screen.getByRole('button', { name: 'Remove Identity' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove Identity' }));
    expect(screen.queryByRole('button', { name: 'Remove Identity' })).toBeNull();
  });
});

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];
const DOWS = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];

describe('DatePicker', () => {
  it('renders the selected month and emits ISO on day select', async () => {
    const onChange = vi.fn();
    render(
      <DatePicker
        value="2026-02-15"
        onChange={onChange}
        labels={{ previousMonth: 'Previous month', nextMonth: 'Next month' }}
        weekdayLabels={DOWS}
        monthLabels={MONTHS}
      />,
    );
    expect(screen.getByText('February 2026')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('gridcell', { name: '20' }));
    expect(onChange).toHaveBeenCalledWith('2026-02-20');
  });

  it('navigates to the next month', async () => {
    render(
      <DatePicker
        value="2026-02-15"
        onChange={vi.fn()}
        labels={{ previousMonth: 'Previous month', nextMonth: 'Next month' }}
        weekdayLabels={DOWS}
        monthLabels={MONTHS}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Next month' }));
    expect(screen.getByText('March 2026')).toBeInTheDocument();
  });
});
