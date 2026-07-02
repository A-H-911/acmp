import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { MeetingMinutes } from './MeetingMinutes';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import type { CommitteeRole } from '../../auth/roles';
import type { MinutesDetail, MinutesSummary, MinutesStatus } from '../../api/minutes';

vi.mock('../../api/meetings', () => ({ useMeetingDetail: vi.fn() }));
vi.mock('../../api/minutes', async (orig) => ({
  ...(await orig<typeof import('../../api/minutes')>()),
  useMinutesForMeeting: vi.fn(),
  useMinutes: vi.fn(),
  useDraftMinutes: vi.fn(),
  useReviseMinutes: vi.fn(),
  useSubmitMinutes: vi.fn(),
  useRequestMinutesChanges: vi.fn(),
  useApproveMinutes: vi.fn(),
  usePublishMinutes: vi.fn(),
  useSupersedeMinutes: vi.fn(),
}));
import { useMeetingDetail } from '../../api/meetings';
import {
  useMinutesForMeeting, useMinutes, useDraftMinutes, useReviseMinutes, useSubmitMinutes,
  useRequestMinutesChanges, useApproveMinutes, usePublishMinutes, useSupersedeMinutes,
} from '../../api/minutes';

const m = (h: unknown) => h as unknown as Mock;
const mutation = () => ({ mutate: vi.fn(), mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });

function detail(over: Partial<MinutesDetail> = {}): MinutesDetail {
  return {
    id: 'mom1', key: 'MIN-2026-001', version: 1, meetingId: 'm1', meetingKey: 'MTG-2026-019',
    meetingTitle: 'Weekly Committee', status: 'Draft',
    summary: { en: 'Roadmap discussed at length.', ar: 'نوقشت خارطة الطريق.' },
    approvedByUserId: null, approvedByName: null, approvedAt: null, approvedBySoleAuthor: false,
    publishedAt: null, supersededByMinutesId: null, supersessionReason: null, ...over,
  };
}
const summary = (v: number, status: MinutesStatus): MinutesSummary => ({
  id: `mom${v}`, key: 'MIN-2026-001', version: v, meetingId: 'm1', meetingKey: 'MTG-2026-019', status, publishedAt: null,
});

function arrange(opts: { meetingStatus?: string; list?: MinutesSummary[]; mom?: MinutesDetail | null; listState?: object }) {
  m(useMeetingDetail).mockReturnValue({ data: { id: 'm1', key: 'MTG-2026-019', status: opts.meetingStatus ?? 'Held' } });
  m(useMinutesForMeeting).mockReturnValue({ data: opts.list ?? [], isLoading: false, isError: false, refetch: vi.fn(), ...opts.listState });
  m(useMinutes).mockReturnValue({ data: opts.mom ?? null, isLoading: false });
  for (const hook of [useDraftMinutes, useReviseMinutes, useSubmitMinutes, useRequestMinutesChanges, useApproveMinutes, usePublishMinutes, useSupersedeMinutes]) {
    m(hook).mockReturnValue(mutation());
  }
}

function setup(roles: CommitteeRole[] = ['secretary']) {
  return render(
    <AcmpAuthContext.Provider value={makeAuth(roles)}>
      <MemoryRouter initialEntries={['/meetings/MTG-2026-019/minutes']}>
        <Routes><Route path="/meetings/:key/minutes" element={<MeetingMinutes />} /></Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('MeetingMinutes (P7d)', () => {
  beforeEach(() => vi.clearAllMocks());

  it('gates minutes until the meeting has started', () => {
    arrange({ meetingStatus: 'Scheduled' });
    setup();
    expect(screen.getByText('Minutes open after the meeting starts')).toBeInTheDocument();
  });

  it('offers a manager the create form when no minutes exist yet', () => {
    arrange({ list: [], mom: null });
    setup(['secretary']);
    expect(screen.getByRole('button', { name: 'Start minutes' })).toBeInTheDocument();
  });

  it('shows a non-manager the no-access gate when nothing is published', () => {
    arrange({ list: [], mom: null });
    setup(['member']);
    expect(screen.getByText('No published minutes yet')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start minutes' })).not.toBeInTheDocument();
  });

  it('creates the initial draft with the entered body', async () => {
    const user = userEvent.setup();
    const draft = mutation();
    arrange({ list: [], mom: null });
    m(useDraftMinutes).mockReturnValue(draft);
    setup(['secretary']);
    await user.type(screen.getByLabelText('Minutes body'), 'First draft');
    await user.click(screen.getByRole('button', { name: 'Start minutes' }));
    expect(draft.mutate).toHaveBeenCalledWith('First draft');
  });

  it('lets a manager edit a draft: save, then send for review (revise → submit)', async () => {
    const user = userEvent.setup();
    const revise = mutation();
    const submit = mutation();
    arrange({ list: [summary(1, 'Draft')], mom: detail({ status: 'Draft' }) });
    m(useReviseMinutes).mockReturnValue(revise);
    m(useSubmitMinutes).mockReturnValue(submit);
    setup(['secretary']);

    await user.click(screen.getByRole('button', { name: 'Save draft' }));
    expect(revise.mutate).toHaveBeenCalledWith({ id: 'mom1', summary: 'Roadmap discussed at length.' });

    await user.click(screen.getByRole('button', { name: 'Send for review' }));
    expect(revise.mutateAsync).toHaveBeenCalled();
    expect(submit.mutate).toHaveBeenCalledWith('mom1');
  });

  it('approves & publishes an in-review MoM in one action (approve → publish)', async () => {
    const user = userEvent.setup();
    const approve = mutation();
    const publish = mutation();
    arrange({ list: [summary(1, 'InReview')], mom: detail({ status: 'InReview' }) });
    m(useApproveMinutes).mockReturnValue(approve);
    m(usePublishMinutes).mockReturnValue(publish);
    setup(['chairman']);

    await user.click(screen.getByRole('button', { name: 'Approve & publish' }));
    expect(approve.mutateAsync).toHaveBeenCalledWith('mom1');
    expect(publish.mutateAsync).toHaveBeenCalledWith('mom1');
  });

  it('requests changes on an in-review MoM', async () => {
    const user = userEvent.setup();
    const rc = mutation();
    arrange({ list: [summary(1, 'InReview')], mom: detail({ status: 'InReview' }) });
    m(useRequestMinutesChanges).mockReturnValue(rc);
    setup(['chairman']);
    await user.click(screen.getByRole('button', { name: 'Request changes' }));
    expect(rc.mutate).toHaveBeenCalledWith('mom1');
  });

  it('renders a published record read-only with the approver and version history', () => {
    arrange({
      list: [summary(1, 'Published')],
      mom: detail({ status: 'Published', approvedByName: 'Sara Chair', publishedAt: '2026-02-18T10:00:00Z' }),
    });
    setup(['member']); // a non-manager still reads the published record
    expect(screen.getByText('Sara Chair')).toBeInTheDocument();
    expect(screen.getByText('Roadmap discussed at length.')).toBeInTheDocument();
    expect(screen.getByText('Version history')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Supersede' })).not.toBeInTheDocument(); // no manager actions
  });

  it('supersedes a published MoM after validating the correction form', async () => {
    const user = userEvent.setup();
    const supersede = mutation();
    arrange({ list: [summary(1, 'Published')], mom: detail({ status: 'Published' }) });
    m(useSupersedeMinutes).mockReturnValue(supersede);
    setup(['secretary']);

    await user.click(screen.getByRole('button', { name: 'Supersede' }));
    await user.click(screen.getByRole('button', { name: 'Publish correction' }));
    expect(supersede.mutateAsync).not.toHaveBeenCalled(); // blocked — empty body + reason
    expect(screen.getByText('A minutes body is required.')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Minutes body'), 'Corrected minutes');
    await user.type(screen.getByLabelText('Reason for superseding'), 'Fixed attendance');
    await user.click(screen.getByRole('button', { name: 'Publish correction' }));
    expect(supersede.mutateAsync).toHaveBeenCalledWith({ id: 'mom1', summary: 'Corrected minutes', reason: 'Fixed attendance' });
  });

  it('opens the supersede dialog on the shared Dialog (role=dialog, Esc closes)', async () => {
    const user = userEvent.setup();
    arrange({ list: [summary(1, 'Published')], mom: detail({ status: 'Published' }) });
    m(useSupersedeMinutes).mockReturnValue(mutation());
    setup(['secretary']);

    await user.click(screen.getByRole('button', { name: 'Supersede' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument(); // shared Dialog (focus-trapped, aria-modal)
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(); // Esc-to-close from the shared Dialog
  });

  it('renders the superseded state with its reason', () => {
    arrange({
      list: [summary(2, 'Published'), summary(1, 'Superseded')],
      mom: detail({ status: 'Superseded', supersessionReason: { en: 'Replaced after a correction.', ar: 'استُبدل.' } }),
    });
    setup(['secretary']);
    expect(screen.getByText(/Replaced after a correction/)).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    arrange({ list: [summary(1, 'Published')], mom: detail({ status: 'Published', approvedByName: 'Sara Chair' }) });
    setup(['secretary']);
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
