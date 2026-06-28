import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { MeetingPage } from './MeetingPage';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import type { MeetingDetail, Agenda } from '../../api/meetings';

// Isolate the page orchestration from the heavy sibling features. The AgendaBuilder stub echoes
// its readOnly prop so we can assert the builder-vs-viewer gating decision at the page level (the
// read-only DOM itself is covered in AgendaBuilder.test).
vi.mock('./AgendaBuilder', () => ({
  AgendaBuilder: ({ readOnly }: { readOnly?: boolean }) => <div>{readOnly ? 'AGENDA_VIEWER' : 'AGENDA_BUILDER'}</div>,
}));
vi.mock('./MeetingWorkspace', () => ({ MeetingWorkspace: () => <div>WORKSPACE_STUB</div> }));
vi.mock('../../api/meetings', () => ({ useMeetingDetail: vi.fn(), useStartMeeting: vi.fn() }));

import { useMeetingDetail, useStartMeeting } from '../../api/meetings';

const mockDetail = useMeetingDetail as unknown as Mock;
let startSpy: Mock;

const draftAgenda: Agenda = { id: 'a1', key: 'AGD-2026-019', status: 'Draft', version: 1, totalTimeboxMinutes: 0, publishedAt: null, items: [] };
const publishedAgenda: Agenda = { id: 'a1', key: 'AGD-2026-019', status: 'Published', version: 2, totalTimeboxMinutes: 35, publishedAt: '2026-06-29T09:00:00Z', items: [] };
const lockedAgenda: Agenda = { ...publishedAgenda, status: 'Locked', version: 3 };

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

describe('MeetingPage — meeting-detail IA + state gating', () => {
  beforeEach(() => {
    mockDetail.mockReset();
    startSpy = vi.fn();
    (useStartMeeting as unknown as Mock).mockReturnValue({ mutate: startSpy, isPending: false });
  });

  it('renders the four-tab bar (Agenda · Meeting · Minutes · Recording)', () => {
    detailResult({ data: meeting() });
    setup();
    for (const name of ['Agenda', 'Meeting', 'Minutes', 'Recording']) {
      expect(screen.getByRole('tab', { name })).toBeInTheDocument();
    }
  });

  // The state machine: (meeting.status × agenda.status) → default tab · agenda builder|viewer · banner.
  const matrix: {
    name: string;
    over: Partial<MeetingDetail>;
    defaultAgendaContent: 'AGENDA_BUILDER' | 'AGENDA_VIEWER';
    banner: string | null;
  }[] = [
    { name: 'Scheduled + Draft → builder, "not published" banner', over: { status: 'Scheduled', agenda: draftAgenda }, defaultAgendaContent: 'AGENDA_BUILDER', banner: 'Agenda not published' },
    { name: 'Scheduled + Published → viewer, no banner (ready)', over: { status: 'Scheduled', agenda: publishedAgenda }, defaultAgendaContent: 'AGENDA_VIEWER', banner: null },
    { name: 'Scheduled + Locked → viewer, no banner (ready)', over: { status: 'Scheduled', agenda: lockedAgenda }, defaultAgendaContent: 'AGENDA_VIEWER', banner: null },
    { name: 'Cancelled → viewer, cancelled banner', over: { status: 'Cancelled', agenda: publishedAgenda }, defaultAgendaContent: 'AGENDA_VIEWER', banner: 'Meeting cancelled' },
  ];

  it.each(matrix)('default-tab state: $name', ({ over, defaultAgendaContent, banner }) => {
    detailResult({ data: meeting(over) });
    setup();
    // These default to the Agenda tab → assert builder vs viewer directly.
    expect(screen.getByText(defaultAgendaContent)).toBeInTheDocument();
    if (banner) {
      expect(screen.getByText(banner)).toBeInTheDocument();
    } else {
      expect(screen.queryByText('Agenda not published')).not.toBeInTheDocument();
    }
  });

  it('Scheduled + Draft: agenda is the EDITABLE builder (the bug fix is gated here)', () => {
    detailResult({ data: meeting({ status: 'Scheduled', agenda: draftAgenda }) });
    setup();
    expect(screen.getByRole('tab', { name: 'Agenda' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('AGENDA_BUILDER')).toBeInTheDocument();
  });

  it('InProgress: defaults to the live workspace, agenda renders as VIEWER, in-progress banner', async () => {
    detailResult({ data: meeting({ status: 'InProgress', startedAt: '2026-06-30T09:00:00Z' }) });
    const user = userEvent.setup();
    setup();
    expect(screen.getByRole('tab', { name: 'Meeting' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('WORKSPACE_STUB')).toBeInTheDocument();
    expect(screen.getByText('Meeting in progress')).toBeInTheDocument();
    // The agenda is no longer editable once the meeting is running.
    await user.click(screen.getByRole('tab', { name: 'Agenda' }));
    expect(screen.getByText('AGENDA_VIEWER')).toBeInTheDocument();
    expect(screen.queryByText('AGENDA_BUILDER')).not.toBeInTheDocument();
  });

  it('Held: concluded banner, Meeting tab shows the concluded recap (not the live workspace)', () => {
    detailResult({ data: meeting({ status: 'Held', heldAt: '2026-06-30T10:30:00Z' }) });
    setup();
    // The banner and the gate both carry a "concluded" headline; assert their unique bodies.
    expect(screen.getByText('A minutes draft will be available for review once the Minutes module ships.')).toBeInTheDocument(); // banner
    expect(screen.getByText('The meeting has ended. Minutes capture arrives in a later phase.')).toBeInTheDocument(); // gate (default Meeting tab)
    expect(screen.getByRole('tab', { name: 'Meeting' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByText('WORKSPACE_STUB')).not.toBeInTheDocument();
  });

  it('Meeting tab: Scheduled + Published shows Start meeting and starts by id', async () => {
    detailResult({ data: meeting({ status: 'Scheduled', agenda: publishedAgenda }) });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Meeting' }));
    expect(screen.getByText('Agenda published — ready to start')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Start meeting' }));
    expect(startSpy).toHaveBeenCalledWith({ meetingId: 'm1' }, expect.anything());
  });

  it('Meeting tab: Scheduled + Draft shows "publish first" and no Start control', async () => {
    detailResult({ data: meeting({ status: 'Scheduled', agenda: draftAgenda }) });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Meeting' }));
    expect(screen.getByText('Not started yet')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start meeting' })).not.toBeInTheDocument();
  });

  it('Meeting tab: Cancelled shows the cancelled note', async () => {
    detailResult({ data: meeting({ status: 'Cancelled' }) });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Meeting' }));
    // Both the banner and the gate carry the cancelled copy; the gate body is unique.
    expect(screen.getByText('This meeting was cancelled. Its topics were returned to the backlog for rescheduling.')).toBeInTheDocument();
  });

  it('Minutes tab: honest P7 placeholder', async () => {
    detailResult({ data: meeting() });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Minutes' }));
    expect(screen.getByText('Minutes arrive in a later phase')).toBeInTheDocument();
  });

  it('Recording tab: honest P13 placeholder', async () => {
    detailResult({ data: meeting() });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: 'Recording' }));
    expect(screen.getByText('Recording is a later-phase integration')).toBeInTheDocument();
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
