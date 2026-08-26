import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useRetentionPolicy, useSetRetentionSetting } from './retention';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real retention hooks vs a stubbed fetch — assert URL building, the body, and key encoding (WBS-24.5). */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);

describe('useRetentionPolicy', () => {
  it('reads the admin retention endpoint', async () => {
    const spy = stubFetch(() => ({ jsonBody: { automaticPurgeEnabled: false, settings: [] } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useRetentionPolicy(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/admin/retention');
    // The v1 posture arrives as a FACT from the server, not as something the client decides.
    expect(result.current.data!.automaticPurgeEnabled).toBe(false);
  });
});

describe('useSetRetentionSetting', () => {
  it('PUTs the value to the key-addressed endpoint', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSetRetentionSetting(), { wrapper });

    result.current.mutate({ key: 'retention.topic.years', valueJson: '{"years":7}' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/admin/retention/retention.topic.years');
    expect(lastBody(spy)).toEqual({ valueJson: '{"years":7}' });
  });

  it('percent-encodes a key so a stray slash cannot re-address the request', async () => {
    // The key is user input on a path segment. Without encoding, `a/b` would address a different
    // route entirely — the server would 404 or, worse, match something else.
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSetRetentionSetting(), { wrapper });

    result.current.mutate({ key: 'retention.a/b', valueJson: '{}' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/admin/retention/retention.a%2Fb');
  });

  it('surfaces a refusal instead of resolving', async () => {
    stubFetch(() => ({ status: 400, jsonBody: { title: 'bad key' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSetRetentionSetting(), { wrapper });

    result.current.mutate({ key: 'smtp.password', valueJson: '{}' });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
