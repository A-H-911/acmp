import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Select } from './Select';
import { Table, type Column } from './Table';

describe('Select', () => {
  it('opens a listbox, selects an option, and reflects it on the trigger', async () => {
    function Host() {
      const [v, setV] = useState('');
      return (
        <Select
          ariaLabel="Urgency"
          placeholder="Select urgency"
          value={v}
          onChange={setV}
          options={[
            { value: 'normal', label: 'Normal' },
            { value: 'urgent', label: 'Urgent' },
          ]}
        />
      );
    }
    render(<Host />);
    await userEvent.click(screen.getByRole('button', { name: 'Urgency' }));
    const listbox = screen.getByRole('listbox', { name: 'Urgency' });
    await userEvent.click(within(listbox).getByRole('option', { name: 'Urgent' }));
    expect(screen.queryByRole('listbox')).toBeNull();
    // Trigger keeps aria-label="Urgency"; the chosen value renders as visible text.
    expect(screen.getByText('Urgent')).toBeInTheDocument();
  });
});

interface Row {
  id: string;
  name: string;
}

describe('Table', () => {
  it('renders rows under an accessible caption and fires sort', async () => {
    const onSort = vi.fn();
    const columns: Column<Row>[] = [{ id: 'name', header: 'Name', cell: (r) => r.name, sortable: true }];
    render(
      <Table
        caption="Members"
        getRowKey={(r) => r.id}
        rows={[{ id: '1', name: 'Ada' }]}
        columns={columns}
        sort={{ by: 'name', dir: 'asc' }}
        onSortChange={onSort}
      />,
    );
    expect(screen.getByRole('table', { name: 'Members' })).toBeInTheDocument();
    expect(screen.getByText('Ada')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /Name/ })).toHaveAttribute('aria-sort', 'ascending');
    await userEvent.click(screen.getByRole('button', { name: /Name/ }));
    expect(onSort).toHaveBeenCalledWith('name');
  });
});
