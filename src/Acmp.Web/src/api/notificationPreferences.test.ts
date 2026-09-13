import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useNotificationPreferences, useSetNotificationPreference, type NotificationPreferences } from './notificationPreferences';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';
import { ApiError } from './apiClient';

afterEach(() => vi.unstubAllGlobals());

const LIST: NotificationPreferences = {
  items: [
    { category: 'MeetingScheduled', group: 'meetings', inApp: true },
    { category: 'VoteOpened', group: 'decisions', inApp: true },
  ],
};

describe('useNotificationPreferences', () => {
  it('GETs the caller’s preferences', async () => {
    const spy = stubFetch(() => ({ jsonBody: LIST }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useNotificationPreferences(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls[0][0])).toBe('/api/notifications/preferences');
    expect(result.current.data).toEqual(LIST);
  });
});

describe('useSetNotificationPreference', () => {
  it('PUTs ONE item wrapped in items[] and replaces the cache with the server’s full list', async () => {
    const updated: NotificationPreferences = { items: [{ ...LIST.items[0], inApp: false }, LIST.items[1]] };
    const spy = stubFetch(() => ({ jsonBody: updated }));
    const { client, wrapper } = makeQueryWrapper();
    client.setQueryData(['notificationPreferences'], LIST);
    const { result } = renderHook(() => useSetNotificationPreference(), { wrapper });

    result.current.mutate({ category: 'MeetingScheduled', inApp: false });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const [url, init] = spy.mock.calls.at(-1)!;
    expect(String(url)).toBe('/api/notifications/preferences');
    expect(init?.method).toBe('PUT');
    expect(lastBody(spy)).toEqual({ items: [{ category: 'MeetingScheduled', inApp: false }] });
    expect(client.getQueryData(['notificationPreferences'])).toEqual(updated);
  });

  it('surfaces a 400 (unknown category) as an ApiError and leaves the cache untouched', async () => {
    stubFetch(() => ({ status: 400, jsonBody: { title: 'Unknown category' } }));
    const { client, wrapper } = makeQueryWrapper();
    client.setQueryData(['notificationPreferences'], LIST);
    const { result } = renderHook(() => useSetNotificationPreference(), { wrapper });

    result.current.mutate({ category: 'Nope', inApp: false });
    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(ApiError);
    expect((result.current.error as ApiError).status).toBe(400);
    expect(client.getQueryData(['notificationPreferences'])).toEqual(LIST);
  });
});
