import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { MeetingPage, MeetingConduct } from './MeetingPage';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import type { MeetingDetail, Agenda } from '../../api/meetings';

// The shell orchestrates header + tab strip + <Outlet/>; the sub-pages are stubbed so we test the
// shell decisions (header card, lifecycle primary action, tab routing) in isolation. MeetingConduct
// (the /attendance + /notes element) lives in this file too, so it's tested here.
vi.mock('./MeetingWorkspace', () => ({ MeetingWorkspace: () => <div>WORKSPACE_STUB</div> }));
vi.mock('../../api/meetings', () => ({ useMeetingDetail: vi.fn(), useStartMeeting: vi.fn() }));

import { useMeetingDetail, useStartMeeting } from '../../api/meetings';

const mockDetail = useMeetingDetail as unknown as Mock;
let startSpy: Mock;

const draftAgenda: Agenda = { id: 'a1', key: 'AGD-2026-019', status: 'Draft', version: 1, totalTimeboxMinutes: 0, publishedAt: null, items: [] };
const publishedAgenda: Agenda = { id: 'a1', key: 'AGD-2026-019', status: 'Published', version: 2, totalTimeboxMinutes: 35, publishedAt: '2026-06-29T09:00:00Z', items: [] };

function meeting(over: Partial<MeetingDetail> = {}): MeetingDetail {
  return {
    id: 'm1', key: 'MTG-2026-019', title: 'Q2 Architecture Review', committeeId: 'c1',
    scheduledStart: '2026-06-30T09:00:00Z', scheduledEnd: '2026-06-30T10:30:00Z',
    status: 'Scheduled', type: 'Regular', mode: 'InPerson', location: null, joinUrl: null, chairUserId: 'u1', chairName: 'Sara K',
    startedAt: null, heldAt: null,
    agenda: publishedAgenda,
    attendance: [], discussions: [],
    ...over,
  };
}

function detailResult(over: Partial<ReturnType<typeof useMeetingDetail>>) {
  mockDetail.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over });
}

/** Render the shell as a layout route with stubbed children so we can assert tab routing + Outlet. */
function setupShell(path = '/meetings/MTG-2026-019') {
  render(
    <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/meetings/:key" element={<MeetingPage />}>
            <Route index element={<div>OVERVIEW_STUB</div>} />
            <Route path="agenda" element={<div>AGENDA_STUB</div>} />
            <Route path="attendance" element={<div>ATTENDANCE_STUB</div>} />
            <Route path="notes" element={<div>NOTES_STUB</div>} />
            <Route path="minutes" element={<div>MINUTES_STUB</div>} />
            <Route path="recording" element={<div>RECORDING_STUB</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('MeetingPage — meeting shell (P6a IA)', () => {
  beforeEach(() => {
    mockDetail.mockReset();
    startSpy = vi.fn();
    (useStartMeeting as unknown as Mock).mockReturnValue({ mutate: startSpy, isPending: false });
  });

  it('renders the header card: key chip, status chip, title, and when·type·mode meta', () => {
    detailResult({ data: meeting() });
    setupShell();
    expect(screen.getByText('MTG-2026-019')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Q2 Architecture Review' })).toBeInTheDocument();
    expect(screen.getByText('Scheduled')).toBeInTheDocument();
    expect(screen.getByText('Regular')).toBeInTheDocument();
    expect(screen.getByText('In person')).toBeInTheDocument();
  });

  it('renders the 6-tab deep-linkable strip as navigation links', () => {
    detailResult({ data: meeting() });
    setupShell();
    const nav = screen.getByRole('navigation', { name: 'Meeting sections' });
    for (const name of ['Overview', 'Agenda', 'Attendance', 'Notes', 'Minutes', 'Recording']) {
      expect(screen.getByRole('link', { name })).toBeInTheDocument();
    }
    // Index route → Overview tab is active, others are not.
    expect(screen.getByRole('link', { name: 'Overview' })).toHaveClass('active');
    expect(screen.getByRole('link', { name: 'Agenda' })).not.toHaveClass('active');
    expect(nav).toBeInTheDocument();
    expect(screen.getByText('OVERVIEW_STUB')).toBeInTheDocument();
  });

  it('marks only the active tab on a sub-route and renders its outlet', () => {
    detailResult({ data: meeting() });
    setupShell('/meetings/MTG-2026-019/agenda');
    expect(screen.getByRole('link', { name: 'Agenda' })).toHaveClass('active');
    expect(screen.getByRole('link', { name: 'Overview' })).not.toHaveClass('active');
    expect(screen.getByText('AGENDA_STUB')).toBeInTheDocument();
  });

  // lcMap: lifecycle phase → the header's primary action label.
  const actions: { name: string; over: Partial<MeetingDetail>; label: string }[] = [
    { name: 'notReady → Build agenda', over: { status: 'Scheduled', agenda: draftAgenda }, label: 'Build agenda' },
    { name: 'ready → Start meeting', over: { status: 'Scheduled', agenda: publishedAgenda }, label: 'Start meeting' },
    { name: 'inProgress → Open live notes', over: { status: 'InProgress', startedAt: '2026-06-30T09:00:00Z' }, label: 'Open live notes' },
    { name: 'concluded → Review minutes', over: { status: 'Held', heldAt: '2026-06-30T10:30:00Z' }, label: 'Review minutes' },
    { name: 'cancelled → Reschedule', over: { status: 'Cancelled' }, label: 'Reschedule' },
  ];

  it.each(actions)('header primary action: $name', ({ over, label }) => {
    detailResult({ data: meeting(over) });
    setupShell();
    expect(screen.getByRole('button', { name: label })).toBeInTheDocument();
  });

  it('Build agenda navigates to the agenda sub-route', async () => {
    detailResult({ data: meeting({ status: 'Scheduled', agenda: draftAgenda }) });
    const user = userEvent.setup();
    setupShell();
    await user.click(screen.getByRole('button', { name: 'Build agenda' }));
    expect(screen.getByText('AGENDA_STUB')).toBeInTheDocument();
  });

  it('Start meeting fires the start mutation by id', async () => {
    detailResult({ data: meeting({ status: 'Scheduled', agenda: publishedAgenda }) });
    const user = userEvent.setup();
    setupShell();
    await user.click(screen.getByRole('button', { name: 'Start meeting' }));
    expect(startSpy).toHaveBeenCalledWith({ meetingId: 'm1' });
  });

  it('shows the loading state while fetching', () => {
    detailResult({ isLoading: true });
    setupShell();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows a not-found state for a 404 meeting', async () => {
    const { ApiError } = await import('../../api/apiClient');
    detailResult({ isError: true, error: new ApiError(404) });
    setupShell();
    expect(screen.getByText('Meeting not found')).toBeInTheDocument();
  });

  it('shows the generic error state (with retry) for a non-404 failure', () => {
    detailResult({ isError: true, error: new Error('boom') });
    setupShell();
    expect(screen.getByText(/load meetings/)).toBeInTheDocument();
  });
});

// MeetingConduct: the /attendance + /notes element renders the live workspace only while InProgress;
// otherwise an honest lifecycle gate. Render-per-status covers each branch.
function setupConduct() {
  render(
    <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
      <MemoryRouter initialEntries={['/meetings/MTG-2026-019/notes']}>
        <Routes>
          <Route path="/meetings/:key/notes" element={<MeetingConduct />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('MeetingConduct — live workspace gating', () => {
  beforeEach(() => mockDetail.mockReset());

  it('InProgress renders the live workspace', () => {
    detailResult({ data: meeting({ status: 'InProgress', startedAt: '2026-06-30T09:00:00Z' }) });
    setupConduct();
    expect(screen.getByText('WORKSPACE_STUB')).toBeInTheDocument();
  });

  it('Scheduled + Published gates with the ready prompt (no workspace)', () => {
    detailResult({ data: meeting({ status: 'Scheduled', agenda: publishedAgenda }) });
    setupConduct();
    expect(screen.getByText('Agenda published — ready to start')).toBeInTheDocument();
    expect(screen.queryByText('WORKSPACE_STUB')).not.toBeInTheDocument();
  });

  it('Scheduled + Draft gates with the "publish first" prompt', () => {
    detailResult({ data: meeting({ status: 'Scheduled', agenda: draftAgenda }) });
    setupConduct();
    expect(screen.getByText('Not started yet')).toBeInTheDocument();
  });

  it('Held gates with the concluded note', () => {
    detailResult({ data: meeting({ status: 'Held', heldAt: '2026-06-30T10:30:00Z' }) });
    setupConduct();
    expect(screen.getByText('The meeting has ended. Minutes capture arrives in a later phase.')).toBeInTheDocument();
  });

  it('Cancelled gates with the cancelled note', () => {
    detailResult({ data: meeting({ status: 'Cancelled' }) });
    setupConduct();
    expect(screen.getByText('This meeting was cancelled. Its topics were returned to the backlog for rescheduling.')).toBeInTheDocument();
  });

  it('renders nothing until the detail resolves', () => {
    detailResult({ data: undefined });
    const { container } = render(
      <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
        <MemoryRouter initialEntries={['/meetings/MTG-2026-019/notes']}>
          <Routes>
            <Route path="/meetings/:key/notes" element={<MeetingConduct />} />
          </Routes>
        </MemoryRouter>
      </AcmpAuthContext.Provider>,
    );
    expect(container.querySelector('.mt-gate')).toBeNull();
  });
});
