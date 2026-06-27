import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { FilterChip } from './FilterChip';

const OPTIONS = [
  { value: 'a', label: 'Alpha' },
  { value: 'b', label: 'Beta' },
];

describe('FilterChip', () => {
  it('single mode: picks an option, shows a count badge, and clears via "Any"', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    const { rerender } = render(<FilterChip label="Type" anyLabel="Any type" options={OPTIONS} value="" onChange={onChange} />);

    await user.click(screen.getByRole('button', { name: 'Type' }));
    await user.click(screen.getByRole('menuitemradio', { name: 'Beta' }));
    expect(onChange).toHaveBeenCalledWith('b');

    rerender(<FilterChip label="Type" anyLabel="Any type" options={OPTIONS} value="b" onChange={onChange} />);
    expect(screen.getByRole('button', { name: /Type/ })).toHaveTextContent('1'); // count badge
    await user.click(screen.getByRole('button', { name: /Type/ }));
    await user.click(screen.getByRole('menuitemradio', { name: 'Any type' }));
    expect(onChange).toHaveBeenLastCalledWith('');
  });

  it('multi mode: toggles a value without closing and clears the set', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    const { rerender } = render(<FilterChip multiple label="Status" clearLabel="Clear" options={OPTIONS} values={[]} onChange={onChange} />);

    await user.click(screen.getByRole('button', { name: 'Status' }));
    await user.click(screen.getByRole('menuitemradio', { name: 'Alpha' }));
    expect(onChange).toHaveBeenCalledWith(['a']);

    // Multi-select keeps the menu open after a toggle; the Clear row appears once a value is set.
    rerender(<FilterChip multiple label="Status" clearLabel="Clear" options={OPTIONS} values={['a']} onChange={onChange} />);
    await user.click(screen.getByRole('menuitem', { name: 'Clear' }));
    expect(onChange).toHaveBeenLastCalledWith([]);
  });

  it('disabled mode renders an inert pill with no popup', () => {
    render(<FilterChip label="Owner" anyLabel="Any owner" options={[]} value="" onChange={() => {}} disabled />);
    expect(screen.getByRole('button', { name: 'Owner' })).toBeDisabled();
  });

  it('is axe-clean (WCAG 2.2 AA)', async () => {
    render(<FilterChip multiple label="Status" clearLabel="Clear" options={OPTIONS} values={['a']} onChange={() => {}} />);
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
