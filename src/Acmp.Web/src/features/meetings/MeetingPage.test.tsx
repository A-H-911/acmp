import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { MeetingPage } from './MeetingPage';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import type { MeetingDetail } from '../../api/meetings';

// Isolate the page orchestration from the heavy sibling features.
vi.mock('./AgendaBuilder', () => ({ AgendaBuilder: () => <div>AGENDA_STUB</div> }));
vi.mock('./MeetingWorkspace', () => ({ MeetingWorkspace: () => <div>WORKSPACE_STUB</div> }));
vi.mock('../../api/meetings', () => ({ useMeetingDetail: vi.fn(), useStartMeeting: vi.fn() }));

import { useMeetingDetail, useStartMeeting } from '../../api/meetings';

const mockDetail = useMeetingDetail as unknown as Mock;
let startSpy: Mock;

function meeting(over: Partial<MeetingDetail> = {}): MeetingDetail {
  return {
    id: 'm1', key: 'MTG-2026-019', title: 'Q2 Architecture Review', committeeId: 'c1',
    scheduledStart: '2026-06-30T09:00:00Z', scheduledEnd: '2026-06-30T10:30:00Z',
    status: 'Scheduled', location: null, joinUrl: null, chairUserId: 'u1', chairName: 'Sara K',
    startedAt: null, heldAt: null,
    agenda: { id: 'a1', key: 'AGD-2026-019', status: 'Published', version: 2, totalTimeboxMinutes: 35, publishedAt: '2026-06-29T09:00:00Z', items: [] },
    attendance: [], discussions: [],
    ...over,
  };
}

function detailResult(over: Partial<ReturnType<typeof useMeetingDetail>>) {
  mockDetail.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over });
}

function setup() {
  render(
    <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
      <MemoryRouter initialEntries={['/meetings/MTG-2026-019']}>
        <Routes>
          <Route path="/meetings/:key" element={<MeetingPage />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('MeetingPage (P6d)', () => {
  beforeEach(() => {
    mockDetail.mockReset();
    startSpy = vi.fn();
    (useStartMeeting as unknown as Mock).mockReturnValue({ mutate: startSpy, isPending: false });
  });

  it('defaults to the Agenda builder tab for a Scheduled meeting', () => {
    detailResult({ data: meeting() });
    setup();
    expect(screen.getByRole('tab', { name: 'Agenda builder' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('AGENDA_STUB')).toBeInTheDocument();
  });

  it('shows a Start meeting control on the Meeting tab when the agenda is Published, and starts by id', async () => {
    detailResult({ data: meeting() });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Meeting' }));
    expect(screen.getByText('Agenda published — ready to start')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Start meeting' }));
    expect(startSpy).toHaveBeenCalledWith({ meetingId: 'm1' }, expect.anything());
  });

  it('shows a "publish first" prompt (no Start) on the Meeting tab when the agenda is Draft', async () => {
    detailResult({ data: meeting({ agenda: { id: 'a1', key: 'AGD-2026-019', status: 'Draft', version: 1, totalTimeboxMinutes: 0, publishedAt: null, items: [] } }) });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Meeting' }));
    expect(screen.getByText('Not started yet')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start meeting' })).not.toBeInTheDocument();
  });

  it('defaults to the live workspace for an InProgress meeting', () => {
    detailResult({ data: meeting({ status: 'InProgress', startedAt: '2026-06-30T09:00:00Z' }) });
    setup();
    expect(screen.getByRole('tab', { name: 'Meeting' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('WORKSPACE_STUB')).toBeInTheDocument();
  });

  it('switches back from Meeting to the Agenda builder tab', async () => {
    detailResult({ data: meeting({ status: 'InProgress', startedAt: '2026-06-30T09:00:00Z' }) });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Agenda builder' }));
    expect(screen.getByText('AGENDA_STUB')).toBeInTheDocument();
  });

  it('shows a concluded prompt on the Meeting tab once the meeting is Held', async () => {
    detailResult({ data: meeting({ status: 'Held', heldAt: '2026-06-30T10:30:00Z' }) });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Meeting' }));
    expect(screen.getByText('Meeting concluded')).toBeInTheDocument();
    expect(screen.queryByText('WORKSPACE_STUB')).not.toBeInTheDocument();
  });

  it('shows the loading state while fetching', () => {
    detailResult({ isLoading: true });
    setup();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows a not-found state for a 404 meeting', async () => {
    const { ApiError } = await import('../../api/apiClient');
    detailResult({ isError: true, error: new ApiError(404) });
    setup();
    expect(screen.getByText('Meeting not found')).toBeInTheDocument();
  });
});
