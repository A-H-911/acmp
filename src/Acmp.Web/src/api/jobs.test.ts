import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useAdminJobs, useRequeueJob } from './jobs';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

afterEach(() => vi.unstubAllGlobals());

const JOBS = {
  configured: true,
  counts: { succeeded: 1, processing: 0, scheduled: 0, enqueued: 0, failed: 1 },
  jobs: [],
};

describe('useAdminJobs', () => {
  it('reads the jobs report from /api/admin/jobs', async () => {
    const spy = stubFetch(() => ({ jsonBody: JOBS }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAdminJobs(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toBe('/api/admin/jobs');
    expect(result.current.data?.configured).toBe(true);
  });

  it('surfaces a server error (403)', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'nope' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAdminJobs(), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(403);
  });
});

describe('useRequeueJob', () => {
  it('POSTs to the (encoded) requeue endpoint and invalidates the jobs query', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useRequeueJob(), { wrapper });

    result.current.mutate('job 1');
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const call = spy.mock.calls.at(-1)!;
    expect(String(call[0])).toBe('/api/admin/jobs/job%201/requeue');
    expect((call[1] as RequestInit).method).toBe('POST');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['admin', 'jobs'] });
  });
});
