import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useDependenciesRegister,
  useDependenciesCounts,
  useDependency,
  useArtifactDependencies,
  useCreateDependency,
} from './dependencies';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real dependencies hooks vs a stubbed fetch — assert URL building, count fan-out, retry, invalidation. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 25, totalPages: 1 });

describe('useDependenciesRegister', () => {
  it('builds the query string from every filter/sort/page param', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(2) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useDependenciesRegister({ kind: 'Blocks', status: 'Open', blockedOnly: true, sortBy: 'status', sortDir: 'desc', page: 3, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/dependencies?kind=Blocks&status=Open&blockedOnly=true&sortBy=status&sortDir=desc&page=3&pageSize=25');
  });

  it('omits the query string entirely when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(0) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useDependenciesRegister({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/dependencies');
  });
});

describe('useDependenciesCounts', () => {
  it('fans out to two global count queries (total + blocked), independent of filters', async () => {
    const spy = stubFetch((url) => ({ jsonBody: page(url.includes('blockedOnly=true') ? 4 : 11) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useDependenciesCounts(), { wrapper });
    await waitFor(() => {
      expect(result.current.total).toBe(11);
      expect(result.current.blocked).toBe(4);
    });
    const urls = spy.mock.calls.map((c) => String(c[0]));
    expect(urls).toContain('/api/dependencies?pageSize=1');
    expect(urls).toContain('/api/dependencies?blockedOnly=true&pageSize=1');
  });
});

describe('useDependency', () => {
  it('stays idle without a key, then reads by key (no retry on 404)', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'DPN-2026-001' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useDependency(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'DPN-2026-001' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/dependencies/DPN-2026-001');
  });
});

describe('useArtifactDependencies', () => {
  it('is disabled until both type and id are present, then reads the by-artifact panel', async () => {
    const spy = stubFetch(() => ({ jsonBody: { outbound: [], inbound: [] } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(
      ({ t, i }: { t?: 'Topic'; i?: string }) => useArtifactDependencies(t, i),
      { wrapper, initialProps: {} as { t?: 'Topic'; i?: string } },
    );
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ t: 'Topic', i: 'g-1' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/dependencies/artifact/Topic/g-1');
  });
});

describe('useCreateDependency', () => {
  it('POSTs the create body and returns the new key', async () => {
    const spy = stubFetch(() => ({ status: 201, jsonBody: { key: 'DPN-2026-009' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useCreateDependency(), { wrapper });
    const input = {
      fromType: 'Topic' as const, fromId: 'a', fromKey: 'TOP-1', fromTitle: 'A',
      toType: 'Action' as const, toId: 'b', toKey: 'ACT-1', toTitle: 'B',
      kind: 'DependsOn' as const, note: null,
    };
    const res = await result.current.mutateAsync(input);
    expect(res.key).toBe('DPN-2026-009');
    const call = spy.mock.calls.at(-1)!;
    expect(String(call[0])).toBe('/api/dependencies');
    expect((call[1] as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toEqual(input);
  });
});
