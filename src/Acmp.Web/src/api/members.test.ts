import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useMembers } from './members';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

afterEach(() => vi.unstubAllGlobals());

describe('useMembers', () => {
  it('reads the member directory', async () => {
    const spy = stubFetch(() => ({ jsonBody: [{ publicId: 'p1', fullName: 'A' }] }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMembers(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toBe('/api/members');
    expect(result.current.data).toHaveLength(1);
  });

  it('surfaces a server error', async () => {
    stubFetch(() => ({ status: 500, jsonBody: { title: 'boom' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMembers(), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(500);
  });
});
