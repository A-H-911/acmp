import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useSystemHealth } from './systemHealth';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

afterEach(() => vi.unstubAllGlobals());

describe('useSystemHealth', () => {
  it('reads the system health report', async () => {
    const spy = stubFetch(() => ({ jsonBody: { status: 'Healthy', entries: [{ name: 'api', status: 'Healthy', durationMs: 1 }] } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSystemHealth(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toBe('/api/admin/health');
    expect(result.current.data?.entries).toHaveLength(1);
  });

  it('surfaces a server error', async () => {
    stubFetch(() => ({ status: 503, jsonBody: { title: 'boom' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSystemHealth(), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(503);
  });
});
