import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MeetingMinutes } from './MeetingMinutes';

describe('MeetingMinutes (P6a placeholder)', () => {
  it('renders the honest P7 placeholder gate', () => {
    render(<MeetingMinutes />);
    expect(screen.getByText('Minutes arrive in a later phase')).toBeInTheDocument();
  });
});
