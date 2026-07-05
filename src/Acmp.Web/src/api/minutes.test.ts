import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useMinutesForMeeting, useMinutesAwaiting, useMinutes, useDraftMinutes, useReviseMinutes, useSubmitMinutes,
  useRequestMinutesChanges, useApproveMinutes, usePublishMinutes, useSupersedeMinutes,
} from './minutes';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real minutes hooks vs a stubbed fetch — assert URL/method/body + cache invalidation. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const methodOf = (spy: ReturnType<typeof stubFetch>) => (spy.mock.calls.at(-1)![1] as RequestInit | undefined)?.method;

describe('useMinutesAwaiting', () => {
  it('reads the committee-wide InReview approval queue (no meeting param)', async () => {
    const spy = stubFetch(() => ({ jsonBody: [{ id: '1', key: 'MIN-2026-018', status: 'InReview' }] }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMinutesAwaiting(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/minutes');
  });
});

describe('useMinutesForMeeting', () => {
  it('stays idle without a meeting id, then reads the version list', async () => {
    const spy = stubFetch(() => ({ jsonBody: [] }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ id }: { id?: string }) => useMinutesForMeeting(id), {
      wrapper, initialProps: {} as { id?: string },
    });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ id: 'm-guid' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/minutes?meeting=m-guid');
  });
});

describe('useMinutes', () => {
  it('reads the head by key, and a specific version when given', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'MIN-2026-001', version: 1 } }));
    const { wrapper } = makeQueryWrapper();
    const head = renderHook(() => useMinutes('MIN-2026-001'), { wrapper });
    await waitFor(() => expect(head.result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/minutes/MIN-2026-001');

    const v1 = renderHook(() => useMinutes('MIN-2026-001', 1), { wrapper });
    await waitFor(() => expect(v1.result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/minutes/MIN-2026-001?version=1');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMinutes('MIN-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('minutes mutations', () => {
  it('drafts by POSTing a mirrored summary and invalidates', async () => {
    const spy = stubFetch(() => ({ status: 201, jsonBody: { id: 'd1', key: 'MIN-2026-001' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useDraftMinutes('m-guid'), { wrapper });
    result.current.mutate('Roadmap discussed');
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/minutes');
    expect(methodOf(spy)).toBe('POST');
    const body = lastBody(spy) as { meetingId: string; summary: { en: string; ar: string } };
    expect(body.meetingId).toBe('m-guid');
    expect(body.summary).toEqual({ en: 'Roadmap discussed', ar: 'Roadmap discussed' });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['minutes', 'meeting', 'm-guid'] });
  });

  it('revises by PUTting a mirrored summary to the id', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'd1', key: 'MIN-2026-001' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useReviseMinutes('m-guid'), { wrapper });
    result.current.mutate({ id: 'd1', summary: 'Edited' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/minutes/d1');
    expect(methodOf(spy)).toBe('PUT');
    expect((lastBody(spy) as { summary: { ar: string } }).summary.ar).toBe('Edited');
  });

  it('POSTs submit / request-changes / approve / publish to the right routes', async () => {
    const cases: [() => { mutate: (id: string) => void }, string][] = [];
    const { wrapper } = makeQueryWrapper();
    const submit = renderHook(() => useSubmitMinutes('m'), { wrapper }).result;
    const rc = renderHook(() => useRequestMinutesChanges('m'), { wrapper }).result;
    const approve = renderHook(() => useApproveMinutes('m'), { wrapper }).result;
    const publish = renderHook(() => usePublishMinutes('m'), { wrapper }).result;
    void cases;

    for (const [hook, path] of [
      [submit, 'submit'], [rc, 'request-changes'], [approve, 'approve'], [publish, 'publish'],
    ] as const) {
      const spy = stubFetch(() => ({ status: 204 }));
      hook.current.mutate('id1');
      await waitFor(() => expect(hook.current.isSuccess).toBe(true));
      expect(urlOf(spy)).toBe(`/api/minutes/id1/${path}`);
      expect(methodOf(spy)).toBe('POST');
      vi.unstubAllGlobals();
    }
  });

  it('supersedes by POSTing a mirrored body to the id (id not in the body)', async () => {
    const spy = stubFetch(() => ({ status: 201, jsonBody: { id: 's1', key: 'MIN-2026-001', version: 2 } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSupersedeMinutes('m-guid'), { wrapper });
    result.current.mutate({ id: 'p1', summary: 'Corrected', reason: 'Fixed attendance' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/minutes/p1/supersede');
    const body = lastBody(spy) as { id?: string; summary: { en: string }; reason: { en: string } };
    expect(body).not.toHaveProperty('id');
    expect(body.summary.en).toBe('Corrected');
    expect(body.reason.en).toBe('Fixed attendance');
  });

  it('surfaces a 403 instead of swallowing it', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useApproveMinutes('m'), { wrapper });
    result.current.mutate('id1');
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(403);
  });
});
