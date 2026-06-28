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

function fillRequired() {
  // Field appends a "*" to required labels, so match non-exactly.
  fireEvent.change(screen.getByLabelText(/Title/), { target: { value: 'Q2 review' } });
  fireEvent.change(screen.getByLabelText(/Starts/), { target: { value: '2026-06-30T09:00' } });
  fireEvent.change(screen.getByLabelText(/Ends/), { target: { value: '2026-06-30T10:30' } });
}

describe('SchedulePage (P6 / PR-B)', () => {
  it('schedules with default Type=Regular and Mode=InPerson', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulePage />, { roles: ['secretary'] });
    fillRequired();
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutateSpy).toHaveBeenCalledTimes(1);
    expect(mutateSpy.mock.calls[0][0]).toMatchObject({
      title: 'Q2 review',
      chairUserId: 'u1',
      chairName: 'Sara K',
      type: 'Regular',
      mode: 'InPerson',
    });
  });

  it('sends the chosen Mode when a segment is picked', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulePage />, { roles: ['secretary'] });
    fillRequired();
    await user.click(screen.getByRole('button', { name: 'Remote' }));
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutateSpy.mock.calls[0][0]).toMatchObject({ mode: 'Remote' });
  });

  it('blocks submit and shows an error when the title is empty', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulePage />, { roles: ['secretary'] });
    fireEvent.change(screen.getByLabelText(/Starts/), { target: { value: '2026-06-30T09:00' } });
    fireEvent.change(screen.getByLabelText(/Ends/), { target: { value: '2026-06-30T10:30' } });
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutateSpy).not.toHaveBeenCalled();
    expect(screen.getByText('A meeting title is required.')).toBeInTheDocument();
  });
});
