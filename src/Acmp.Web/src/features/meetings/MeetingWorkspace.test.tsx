import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { MeetingWorkspace } from './MeetingWorkspace';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import type { MeetingDetail } from '../../api/meetings';
import type { Member } from '../../api/members';

vi.mock('../../api/meetings', () => ({
  useMeetingDetail: vi.fn(),
  useEndMeeting: vi.fn(),
  useMarkAttendance: vi.fn(),
  useCaptureDiscussion: vi.fn(),
  useRecordActualTime: vi.fn(),
}));
vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));

import {
  useMeetingDetail,
  useEndMeeting,
  useMarkAttendance,
  useCaptureDiscussion,
  useRecordActualTime,
} from '../../api/meetings';
import { useMembers } from '../../api/members';

const mockDetail = useMeetingDetail as unknown as Mock;
const mockMembers = useMembers as unknown as Mock;

let endSpy: Mock, markSpy: Mock, captureSpy: Mock, recordSpy: Mock;

const MEETING: MeetingDetail = {
  id: 'm1', key: 'MTG-2026-019', title: 'Q2 Architecture Review', committeeId: 'c1',
  scheduledStart: '2026-06-30T09:00:00Z', scheduledEnd: '2026-06-30T10:30:00Z',
  status: 'InProgress', type: 'Regular', mode: 'InPerson', location: null, joinUrl: null, chairUserId: 'u1', chairName: 'Sara K',
  startedAt: '2026-06-30T09:00:00Z', heldAt: null,
  agenda: {
    id: 'a1', key: 'AGD-2026-019', status: 'Published', version: 2, totalTimeboxMinutes: 35, publishedAt: '2026-06-29T09:00:00Z',
    items: [
      { topicId: 't1', topicKey: 'TOP-2026-014', topicTitle: 'Adopt Keycloak', urgent: true, order: 0, timeboxMinutes: 20, presenterUserId: null, presenterName: null, outcome: 'Pending', actualMinutes: 0 },
      { topicId: 't2', topicKey: 'TOP-2026-031', topicTitle: 'Event streaming spike', urgent: false, order: 1, timeboxMinutes: 15, presenterUserId: null, presenterName: null, outcome: 'Pending', actualMinutes: 0 },
    ],
  },
  attendance: [
    { userId: 'u1', name: 'Sara K', role: 'Chair', status: 'Present', isVotingEligible: true, joinedAt: null },
  ],
  discussions: [],
};

const MEMBERS: Member[] = [
  { publicId: 'u1', fullName: 'Sara K', email: 'sara@example.com', role: 'Chairman', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
  { publicId: 'u2', fullName: 'Omar R', email: 'omar@example.com', role: 'Member', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
];

function setup(detail: MeetingDetail = MEETING) {
  mockDetail.mockReturnValue({ data: detail, isLoading: false, isError: false, error: null });
  mockMembers.mockReturnValue({ data: MEMBERS });
  render(
    <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
      <MemoryRouter initialEntries={['/meetings/MTG-2026-019']}>
        <Routes>
          <Route path="/meetings/:key" element={<MeetingWorkspace />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('MeetingWorkspace (P6d)', () => {
  beforeEach(() => {
    [mockDetail, mockMembers].forEach((m) => m.mockReset());
    endSpy = vi.fn();
    markSpy = vi.fn();
    captureSpy = vi.fn();
    recordSpy = vi.fn();
    (useEndMeeting as unknown as Mock).mockReturnValue({ mutate: endSpy, isPending: false });
    (useMarkAttendance as unknown as Mock).mockReturnValue({ mutate: markSpy, isPending: false });
    (useCaptureDiscussion as unknown as Mock).mockReturnValue({ mutate: captureSpy, isPending: false });
    (useRecordActualTime as unknown as Mock).mockReturnValue({ mutate: recordSpy, isPending: false });
  });

  it('renders the live header, elapsed timer, agenda spine, active item, and attendance', () => {
    setup();
    expect(screen.getByRole('heading', { name: 'Q2 Architecture Review' })).toBeInTheDocument();
    expect(screen.getByText('Live')).toBeInTheDocument();
    expect(screen.getByRole('timer')).toBeInTheDocument();
    // Default active item = first Pending item.
    expect(screen.getByRole('heading', { name: 'Adopt Keycloak' })).toBeInTheDocument();
    expect(screen.getByText('TOP-2026-014')).toBeInTheDocument();
    // Attendance roster from active members.
    expect(screen.getByText('Sara K')).toBeInTheDocument();
    expect(screen.getByText('Omar R')).toBeInTheDocument();
    expect(screen.getByText('1 of 2 present · 2 needed')).toBeInTheDocument();
  });

  it('selects an agenda-spine item to make it the active item', async () => {
    const user = userEvent.setup();
    setup();
    const spine = screen.getByRole('navigation', { name: 'Agenda' });
    await user.click(within(spine).getByRole('button', { name: /Event streaming spike/ }));
    expect(screen.getByRole('heading', { name: 'Event streaming spike' })).toBeInTheDocument();
  });

  it('autosaves a discussion note on blur (topicId + body)', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText('Discussion notes'), 'Agreed to pilot Keycloak.');
    await user.tab(); // blur the textarea → autosave (no explicit Save button)
    expect(captureSpy).toHaveBeenCalledTimes(1);
    expect(captureSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't1', body: 'Agreed to pilot Keycloak.' });
  });

  it('toolbar Bold wraps the selection in markdown marks', async () => {
    const user = userEvent.setup();
    setup();
    const ta = screen.getByLabelText('Discussion notes') as HTMLTextAreaElement;
    await user.type(ta, 'pilot');
    ta.setSelectionRange(0, 5);
    await user.click(screen.getByRole('button', { name: 'Bold' }));
    expect(ta).toHaveValue('**pilot**');
  });

  it('toggles a present member to absent', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Toggle attendance for Sara K' }));
    expect(markSpy).toHaveBeenCalledWith(
      expect.objectContaining({ meetingId: 'm1', userId: 'u1', name: 'Sara K', role: 'Chair', status: 'Absent', isVotingEligible: true }),
    );
  });

  it('toggles an absent member to present', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Toggle attendance for Omar R' }));
    expect(markSpy).toHaveBeenCalledWith(
      expect.objectContaining({ userId: 'u2', name: 'Omar R', role: 'Member', status: 'Present' }),
    );
  });

  it('ends the meeting from the End → Minutes control', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'End → Minutes' }));
    expect(endSpy).toHaveBeenCalledWith({ meetingId: 'm1' }, expect.anything());
  });

  // DV-16: actual-time + outcome recorder on the active item.
  it('records actual time + outcome for the active item (DV-16)', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText('Actual time'), '18');
    await user.click(screen.getByRole('button', { name: 'Outcome' }));
    await user.click(screen.getByRole('option', { name: 'Discussed' }));
    await user.click(screen.getByRole('button', { name: 'Record time' }));
    expect(recordSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't1', actualMinutes: 18, outcome: 'Discussed' });
  });

  it('records time-only WITHOUT an outcome (omits the field, never sends "") (DV-16)', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText('Actual time'), '12');
    await user.click(screen.getByRole('button', { name: 'Record time' }));
    // outcome is omitted (undefined), not '' — '' is not a valid AgendaItemOutcome and would 400.
    expect(recordSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't1', actualMinutes: 12, outcome: undefined });
  });

  it('disables Record time until a valid minutes value is entered (DV-16)', async () => {
    const user = userEvent.setup();
    setup();
    expect(screen.getByRole('button', { name: 'Record time' })).toBeDisabled();
    await user.type(screen.getByLabelText('Actual time'), '5');
    expect(screen.getByRole('button', { name: 'Record time' })).toBeEnabled();
  });

  it('renders the P7/P8/P9 stub actions as disabled', () => {
    setup();
    expect(screen.getByRole('button', { name: 'Record decision' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Create action' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Call vote' })).toBeDisabled();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
