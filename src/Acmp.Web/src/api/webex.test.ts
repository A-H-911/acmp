import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useWebexStatus } from './webex';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

afterEach(() => vi.unstubAllGlobals());

describe('useWebexStatus', () => {
  it('reads the Webex status from /api/webex/status', async () => {
    const spy = stubFetch(() => ({ jsonBody: { enabled: true, canAutoCreate: true } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useWebexStatus(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toBe('/api/webex/status');
    expect(result.current.data).toEqual({ enabled: true, canAutoCreate: true });
  });
});
