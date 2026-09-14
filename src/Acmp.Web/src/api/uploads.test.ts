import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useUploadLimits, DEFAULT_UPLOAD_LIMITS, toMb } from './uploads';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

// AC-169: the limits a page states come from GET /api/uploads/limits; the defaults only fill the gap while it loads.
afterEach(() => vi.unstubAllGlobals());

describe('useUploadLimits', () => {
  it('answers with the defaults until the server replies, then with the server', async () => {
    const server = { ...DEFAULT_UPLOAD_LIMITS, attachmentMaxBytes: 12 * 1024 * 1024 };
    const spy = stubFetch(() => ({ jsonBody: server }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useUploadLimits(), { wrapper });
    expect(result.current).toEqual(DEFAULT_UPLOAD_LIMITS);
    await waitFor(() => expect(result.current.attachmentMaxBytes).toBe(12 * 1024 * 1024));
    expect(String(spy.mock.calls.at(-1)![0])).toBe('/api/uploads/limits');
  });

  it('states whole megabytes', () => {
    expect(toMb(100 * 1024 * 1024)).toBe(100);
    expect(toMb(2 * 1024 ** 3)).toBe(2048);
  });
});
