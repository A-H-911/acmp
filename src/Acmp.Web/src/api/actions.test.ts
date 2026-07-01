import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useActionsRegister, useActionsCounts, useAction } from './actions';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

/* Real actions hooks vs a stubbed fetch — assert URL building, count fan-out, and retry rules. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 25, totalPages: 1 });

describe('useActionsRegister', () => {
  it('builds the query string from every filter/sort/page param', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(2) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useActionsRegister({ statuses: ['Open', 'Blocked'], owner: 'kc-1', overdue: true, search: 'auth', sortBy: 'progress', sortDir: 'desc', page: 2, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/actions?status=Open&status=Blocked&owner=kc-1&overdue=true&search=auth&sortBy=progress&sortDir=desc&page=2&pageSize=25');
  });

  it('omits the query string entirely when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(0) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useActionsRegister({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/actions');
  });
});

describe('useActionsCounts', () => {
  it('fans out to two global count queries (total + overdue), independent of filters', async () => {
    const spy = stubFetch((url) => ({ jsonBody: page(url.includes('overdue=true') ? 3 : 12) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useActionsCounts(), { wrapper });
    await waitFor(() => {
      expect(result.current.total).toBe(12);
      expect(result.current.overdue).toBe(3);
    });
    const urls = spy.mock.calls.map((c) => String(c[0]));
    expect(urls).toContain('/api/actions?pageSize=1');
    expect(urls).toContain('/api/actions?overdue=true&pageSize=1');
  });
});

describe('useAction', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'ACT-2026-033' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useAction(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'ACT-2026-033' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/actions/ACT-2026-033');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAction('ACT-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});
