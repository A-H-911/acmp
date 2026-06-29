import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useNotifications,
  useInfiniteNotifications,
  useMarkNotificationRead,
  useMarkAllNotificationsRead,
} from './notifications';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

afterEach(() => vi.unstubAllGlobals());

const PAGE = { items: [], unreadCount: 0, total: 0, hasMore: false };

function urlOf(spy: ReturnType<typeof stubFetch>): string {
  return String(spy.mock.calls.at(-1)![0]);
}

describe('notification reads', () => {
  it('useNotifications reads the small recent page', async () => {
    const spy = stubFetch(() => ({ jsonBody: PAGE }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useNotifications(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/notifications?page=1&pageSize=8');
  });

  it('useInfiniteNotifications offers a next page only while the server says hasMore', async () => {
    stubFetch(() => ({ jsonBody: { ...PAGE, hasMore: true } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useInfiniteNotifications(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.hasNextPage).toBe(true);
  });

  it('useInfiniteNotifications stops paging when hasMore is false', async () => {
    stubFetch(() => ({ jsonBody: { ...PAGE, hasMore: false } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useInfiniteNotifications(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.hasNextPage).toBe(false);
  });
});

describe('notification mutations', () => {
  it('useMarkNotificationRead POSTs the read endpoint and invalidates the feed', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useMarkNotificationRead(), { wrapper });
    result.current.mutate('n1');
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/notifications/n1/read');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['notifications'] });
  });

  it('useMarkAllNotificationsRead POSTs read-all and invalidates the feed', async () => {
    const spy = stubFetch(() => ({ jsonBody: { marked: 3 } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useMarkAllNotificationsRead(), { wrapper });
    result.current.mutate();
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/notifications/read-all');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['notifications'] });
  });
});
