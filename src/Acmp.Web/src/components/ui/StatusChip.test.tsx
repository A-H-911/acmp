import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusChip } from './StatusChip';

describe('StatusChip', () => {
  it('renders the label and the tone class (meaning never color-only)', () => {
    const { container } = render(<StatusChip tone="danger" label="Overdue" />);
    expect(screen.getByText('Overdue')).toBeInTheDocument();
    const chip = container.querySelector('.status-chip');
    expect(chip).toHaveClass('danger');
    // A dot accompanies the label so status doesn't rely on color alone.
    expect(container.querySelector('.status-chip-dot')).toBeInTheDocument();
  });

  it('defaults to the standard size and opts into the dense (sm) variant', () => {
    const { container, rerender } = render(<StatusChip tone="info" label="Accepted" />);
    expect(container.querySelector('.status-chip')).not.toHaveClass('status-chip-sm');
    rerender(<StatusChip tone="info" label="Accepted" size="sm" />);
    expect(container.querySelector('.status-chip')).toHaveClass('status-chip-sm');
  });
});
