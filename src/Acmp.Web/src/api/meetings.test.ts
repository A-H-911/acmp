import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useMeetings, useScheduleMeeting, useMeetingDetail, usePreparedTopics,
  useAddAgendaItem, useRemoveAgendaItem, useMoveAgendaItem, useSetTimebox,
  useAssignPresenter, usePublishAgenda, useStartMeeting, useEndMeeting,
  useMarkAttendance, useCaptureDiscussion, useRecordActualTime,
} from './meetings';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real meetings hooks vs a stubbed fetch — assert URL/method/body + cache invalidation. */
afterEach(() => vi.unstubAllGlobals());

function urlOf(spy: ReturnType<typeof stubFetch>): string {
  return String(spy.mock.calls.at(-1)![0]);
}
function methodOf(spy: ReturnType<typeof stubFetch>): string | undefined {
  return (spy.mock.calls.at(-1)![1] as RequestInit | undefined)?.method;
}

describe('meetings reads', () => {
  it('useMeetings lists from /meetings', async () => {
    const spy = stubFetch(() => ({ jsonBody: [] }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMeetings(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings');
  });

  it('useMeetingDetail stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'MTG-2026-001' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useMeetingDetail(k), {
      wrapper,
      initialProps: {} as { k?: string },
    });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'MTG-2026-001' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/MTG-2026-001');
  });

  it('usePreparedTopics sources the Prepared pool with a generous page size', async () => {
    const spy = stubFetch(() => ({ jsonBody: { items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => usePreparedTopics(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics?status=Prepared&pageSize=200');
  });
});

describe('useScheduleMeeting', () => {
  it('POSTs the schedule and invalidates the list', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'm1', key: 'MTG-2026-002' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useScheduleMeeting(), { wrapper });
    result.current.mutate({
      title: 'Q3 review', chairUserId: 'u1', chairName: 'Chair',
      scheduledStart: '2026-07-01T09:00:00Z', scheduledEnd: '2026-07-01T10:00:00Z',
      type: 'Regular', mode: 'InPerson',
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings');
    expect(methodOf(spy)).toBe('POST');
    expect((lastBody(spy) as { title: string }).title).toBe('Q3 review');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['meetings', 'list'] });
  });

  it('surfaces a 409 conflict instead of swallowing it', async () => {
    stubFetch(() => ({ status: 409, jsonBody: { title: 'Overlapping meeting' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useScheduleMeeting(), { wrapper });
    result.current.mutate({
      title: 'x', chairUserId: 'u1', chairName: 'c',
      scheduledStart: 's', scheduledEnd: 'e', type: 'Regular', mode: 'Remote',
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(409);
  });
});

describe('agenda mutations (invalidate detail + Prepared pool)', () => {
  it('useAddAgendaItem POSTs the item without the meetingId and invalidates both keys', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'a1', items: [] } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useAddAgendaItem('MTG-2026-001'), { wrapper });
    result.current.mutate({
      meetingId: 'm1', topicId: 't1', topicKey: 'TOP-2026-001', topicTitle: 'A',
      urgent: false, timeboxMinutes: 15,
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/agenda/items');
    expect(lastBody(spy)).not.toHaveProperty('meetingId');
    expect((lastBody(spy) as { topicId: string }).topicId).toBe('t1');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['meetings', 'detail', 'MTG-2026-001'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'prepared'] });
  });

  it('useRemoveAgendaItem DELETEs the item by topic id', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'a1', items: [] } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useRemoveAgendaItem('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1', topicId: 't1' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/agenda/items/t1');
    expect(methodOf(spy)).toBe('DELETE');
  });

  it('useMoveAgendaItem posts a ±1 delta to the move endpoint', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'a1', items: [] } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMoveAgendaItem('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1', topicId: 't1', delta: -1 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/agenda/items/t1/move');
    expect(lastBody(spy)).toEqual({ delta: -1 });
  });

  it('useSetTimebox posts the minutes', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'a1', items: [] } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSetTimebox('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1', topicId: 't1', minutes: 20 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/agenda/items/t1/timebox');
    expect(lastBody(spy)).toEqual({ minutes: 20 });
  });

  it('useAssignPresenter posts the presenter', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'a1', items: [] } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAssignPresenter('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1', topicId: 't1', presenterUserId: 'u2', presenterName: 'Pre' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/agenda/items/t1/presenter');
    expect(lastBody(spy)).toEqual({ presenterUserId: 'u2', presenterName: 'Pre' });
  });

  it('usePublishAgenda POSTs publish with no body', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'a1', items: [] } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => usePublishAgenda('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/agenda/publish');
    expect(methodOf(spy)).toBe('POST');
  });
});

describe('live-meeting mutations (invalidate detail only)', () => {
  it('useStartMeeting begins the meeting and invalidates only the detail', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useStartMeeting('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/start');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['meetings', 'detail', 'MTG-2026-001'] });
    expect(invalidate).not.toHaveBeenCalledWith({ queryKey: ['topics', 'prepared'] });
  });

  it('useEndMeeting ends the meeting', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useEndMeeting('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/end');
  });

  it('useMarkAttendance posts the roster line without the meetingId', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMarkAttendance('MTG-2026-001'), { wrapper });
    result.current.mutate({
      meetingId: 'm1', userId: 'u1', name: 'Member', role: 'Member',
      status: 'Present', isVotingEligible: true,
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/attendance');
    expect(lastBody(spy)).not.toHaveProperty('meetingId');
    expect((lastBody(spy) as { status: string }).status).toBe('Present');
  });

  it('useCaptureDiscussion posts the note for an agenda topic', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useCaptureDiscussion('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1', topicId: 't1', body: 'agreed to revisit' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/discussion');
    expect(lastBody(spy)).toEqual({ topicId: 't1', body: 'agreed to revisit' });
  });

  it('useRecordActualTime posts the actual minutes + outcome', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useRecordActualTime('MTG-2026-001'), { wrapper });
    result.current.mutate({ meetingId: 'm1', topicId: 't1', actualMinutes: 12, outcome: 'Discussed' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/meetings/m1/agenda/items/t1/actual-time');
    expect(lastBody(spy)).toEqual({ actualMinutes: 12, outcome: 'Discussed' });
  });
});
