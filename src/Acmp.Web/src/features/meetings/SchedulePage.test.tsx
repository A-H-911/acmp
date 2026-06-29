import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SchedulePage } from './SchedulePage';
import { renderWithAuth } from '../../test/render';

vi.mock('../../api/meetings', () => ({ useScheduleMeeting: vi.fn() }));
vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));
const navigateSpy = vi.fn();
vi.mock('react-router-dom', async (orig) => ({ ...(await orig<typeof import('react-router-dom')>()), useNavigate: () => navigateSpy }));

import { useScheduleMeeting } from '../../api/meetings';
import { useMembers } from '../../api/members';

let mutateSpy: Mock;

beforeEach(() => {
  mutateSpy = vi.fn();
  navigateSpy.mockReset();
  (useScheduleMeeting as unknown as Mock).mockReturnValue({ mutate: mutateSpy, isPending: false, isError: false });
  (useMembers as unknown as Mock).mockReturnValue({
    data: [{ publicId: 'u1', fullName: 'Sara K', role: 'Chairman', isActive: true, isVotingEligible: true }],
    isLoading: false,
    isError: false,
  });
});

// Pick a date via the DateField popover (defaults to the current month → always has a 15th) and
// set the start/end times. Title is filled too; chair defaults to the Chairman.
async function pickDateTime(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Date' }));
  await user.click(screen.getByRole('gridcell', { name: '15' }));
  fireEvent.change(screen.getByLabelText('Start time'), { target: { value: '09:00' } });
  fireEvent.change(screen.getByLabelText('End time'), { target: { value: '10:30' } });
}

async function fillRequired(user: ReturnType<typeof userEvent.setup>) {
  // Field appends a "*" to required labels, so match non-exactly.
  fireEvent.change(screen.getByLabelText(/Title/), { target: { value: 'Q2 review' } });
  await pickDateTime(user);
}

describe('SchedulePage (P6 / PR-B)', () => {
  it('schedules with default Type=Regular and Mode=InPerson', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulePage />, { roles: ['secretary'] });
    await fillRequired(user);
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutateSpy).toHaveBeenCalledTimes(1);
    const payload = mutateSpy.mock.calls[0][0];
    expect(payload).toMatchObject({
      title: 'Q2 review',
      chairUserId: 'u1',
      chairName: 'Sara K',
      type: 'Regular',
      mode: 'InPerson',
    });
    // start & end are the SAME day (single-day meeting) and end is after start.
    expect(new Date(payload.scheduledStart).toDateString()).toBe(new Date(payload.scheduledEnd).toDateString());
    expect(new Date(payload.scheduledEnd).getTime()).toBeGreaterThan(new Date(payload.scheduledStart).getTime());
  });

  it('sends the chosen Mode when a segment is picked', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulePage />, { roles: ['secretary'] });
    await fillRequired(user);
    await user.click(screen.getByRole('button', { name: 'Remote' }));
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutateSpy.mock.calls[0][0]).toMatchObject({ mode: 'Remote' });
  });

  it('blocks submit and shows an error when the title is empty', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulePage />, { roles: ['secretary'] });
    await pickDateTime(user);
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutateSpy).not.toHaveBeenCalled();
    expect(screen.getByText('A meeting title is required.')).toBeInTheDocument();
  });

  it('blocks submit and flags the date when no date is picked', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulePage />, { roles: ['secretary'] });
    fireEvent.change(screen.getByLabelText(/Title/), { target: { value: 'Q2 review' } });
    fireEvent.change(screen.getByLabelText('Start time'), { target: { value: '09:00' } });
    fireEvent.change(screen.getByLabelText('End time'), { target: { value: '10:30' } });
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutateSpy).not.toHaveBeenCalled();
    expect(screen.getByText('A meeting date is required.')).toBeInTheDocument();
  });
});
