import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useActionsRegister, useActionsCounts, useAction,
  useStartAction, useUnblockAction, useVerifyAction,
  useBlockAction, useCancelAction, useUpdateActionProgress, useCompleteAction,
  useCreateAction,
} from './actions';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

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

describe('action lifecycle mutations', () => {
  const methodOf = (spy: ReturnType<typeof stubFetch>) => (spy.mock.calls.at(-1)![1] as RequestInit).method;

  it('start POSTs to /{id}/start with no body and invalidates the whole actions family', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useStartAction(), { wrapper });
    await result.current.mutateAsync({ id: 'g1' });
    expect(urlOf(spy)).toBe('/api/actions/g1/start');
    expect(methodOf(spy)).toBe('POST');
    expect(lastBody(spy)).toBeUndefined();
    expect(inval).toHaveBeenCalledWith({ queryKey: ['actions'] });
  });

  it('unblock and verify POST to their own no-body routes', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const unblock = renderHook(() => useUnblockAction(), { wrapper });
    await unblock.result.current.mutateAsync({ id: 'g2' });
    expect(urlOf(spy)).toBe('/api/actions/g2/unblock');
    const verify = renderHook(() => useVerifyAction(), { wrapper });
    await verify.result.current.mutateAsync({ id: 'g3' });
    expect(urlOf(spy)).toBe('/api/actions/g3/verify');
  });

  it('block and cancel POST the mirrored bilingual reason body', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const block = renderHook(() => useBlockAction(), { wrapper });
    await block.result.current.mutateAsync({ id: 'g1', reason: { en: 'stuck', ar: 'stuck' } });
    expect(urlOf(spy)).toBe('/api/actions/g1/block');
    expect(lastBody(spy)).toEqual({ reason: { en: 'stuck', ar: 'stuck' } });
    const cancel = renderHook(() => useCancelAction(), { wrapper });
    await cancel.result.current.mutateAsync({ id: 'g1', reason: { en: 'drop', ar: 'drop' } });
    expect(urlOf(spy)).toBe('/api/actions/g1/cancel');
    expect(lastBody(spy)).toEqual({ reason: { en: 'drop', ar: 'drop' } });
  });

  it('progress POSTs the numeric percent and complete POSTs the optional note', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const prog = renderHook(() => useUpdateActionProgress(), { wrapper });
    await prog.result.current.mutateAsync({ id: 'g1', progressPct: 60 });
    expect(urlOf(spy)).toBe('/api/actions/g1/progress');
    expect(lastBody(spy)).toEqual({ progressPct: 60 });
    const done = renderHook(() => useCompleteAction(), { wrapper });
    await done.result.current.mutateAsync({ id: 'g1', completionNote: null });
    expect(urlOf(spy)).toBe('/api/actions/g1/complete');
    expect(lastBody(spy)).toEqual({ completionNote: null });
  });

  it('create POSTs the full body to /api/actions and invalidates the actions family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { key: 'ACT-2026-050' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useCreateAction(), { wrapper });
    const out = await result.current.mutateAsync({
      title: { en: 't', ar: 't' }, description: null, priority: 'Normal',
      ownerUserId: 'kc-1', ownerName: 'One', dueDate: '2026-03-02T00:00:00Z',
      sourceType: 'Decision', sourceId: 'g1', sourceKey: 'DECN-1', meetingKey: null,
    });
    expect(urlOf(spy)).toBe('/api/actions');
    expect(methodOf(spy)).toBe('POST');
    expect(lastBody(spy)).toMatchObject({ priority: 'Normal', ownerUserId: 'kc-1', sourceType: 'Decision', sourceId: 'g1' });
    expect(out.key).toBe('ACT-2026-050');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['actions'] });
  });
});
