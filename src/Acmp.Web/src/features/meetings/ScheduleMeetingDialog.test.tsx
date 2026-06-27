import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from 'vitest';
import { screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import i18n from '../../i18n';
import { ScheduleMeetingDialog } from './ScheduleMeetingDialog';
import { renderWithAuth } from '../../test/render';
import type { Member } from '../../api/members';

const navigate = vi.fn();
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => navigate,
}));

const mutate = vi.fn();
vi.mock('../../api/meetings', () => ({ useScheduleMeeting: vi.fn(() => ({ mutate, isPending: false, isError: false })) }));
vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));
import { useMembers } from '../../api/members';
const mockMembers = useMembers as unknown as Mock;

const MEMBERS: Member[] = [
  { publicId: 'c1', fullName: 'Sara Chair', email: 's@acmp.gov', role: 'Chairman', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
  { publicId: 'm1', fullName: 'Omar Member', email: 'o@acmp.gov', role: 'Member', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
];

describe('ScheduleMeetingDialog (P6)', () => {
  beforeEach(() => {
    navigate.mockReset();
    mutate.mockReset();
    mockMembers.mockReturnValue({ data: MEMBERS, isLoading: false, isError: false });
  });
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('schedules with the chair defaulted to the Chairman and follows the new meeting on success', async () => {
    mutate.mockImplementation((_input, opts) => opts?.onSuccess?.({ key: 'MTG-2026-020' }));
    const onClose = vi.fn();
    const user = userEvent.setup();
    renderWithAuth(<ScheduleMeetingDialog open onClose={onClose} />);

    fireEvent.change(screen.getByLabelText(/Title/), { target: { value: 'Q3 Architecture Review' } });
    fireEvent.change(screen.getByLabelText(/Starts/), { target: { value: '2026-07-01T09:00' } });
    fireEvent.change(screen.getByLabelText(/Ends/), { target: { value: '2026-07-01T10:30' } });
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutate).toHaveBeenCalledTimes(1);
    const payload = mutate.mock.calls[0][0];
    expect(payload).toMatchObject({ title: 'Q3 Architecture Review', chairUserId: 'c1', chairName: 'Sara Chair' });
    expect(payload.scheduledStart).toMatch(/Z$/);
    expect(payload.scheduledEnd > payload.scheduledStart).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/meetings/MTG-2026-020');
    expect(onClose).toHaveBeenCalled();
  });

  it('blocks submit and shows an error when the title is empty', async () => {
    const user = userEvent.setup();
    renderWithAuth(<ScheduleMeetingDialog open onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText(/Starts/), { target: { value: '2026-07-01T09:00' } });
    fireEvent.change(screen.getByLabelText(/Ends/), { target: { value: '2026-07-01T10:30' } });
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutate).not.toHaveBeenCalled();
    expect(screen.getByText(/meeting title is required/i)).toBeTruthy();
  });

  it('blocks submit when the end is not after the start', async () => {
    const user = userEvent.setup();
    renderWithAuth(<ScheduleMeetingDialog open onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText(/Title/), { target: { value: 'Bad window' } });
    fireEvent.change(screen.getByLabelText(/Starts/), { target: { value: '2026-07-01T10:30' } });
    fireEvent.change(screen.getByLabelText(/Ends/), { target: { value: '2026-07-01T09:00' } });
    await user.click(screen.getByRole('button', { name: 'Schedule' }));

    expect(mutate).not.toHaveBeenCalled();
    expect(screen.getByText(/end time must be after the start/i)).toBeTruthy();
  });

  it('renders the Arabic chrome when the locale is AR', async () => {
    await i18n.changeLanguage('ar');
    renderWithAuth(<ScheduleMeetingDialog open onClose={vi.fn()} />);
    expect(screen.getByText('جدولة اجتماع')).toBeTruthy();
  });

  it('has no axe (WCAG 2.2 AA) violations', async () => {
    const { container } = renderWithAuth(<ScheduleMeetingDialog open onClose={vi.fn()} />);
    const results = await axe.run(container, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations).toEqual([]);
  });
});
