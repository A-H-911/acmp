import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useAuditRegister } from './audit';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

/* Real audit hook vs a stubbed fetch — assert read-only URL building (every filter + paging). */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 25, totalPages: 1 });

describe('useAuditRegister', () => {
  it('builds the query string from every filter + paging param', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(3) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useAuditRegister({ entityType: 'Vote', actor: 'kc-1', action: 'Vote.Closed', from: '2026-06-01T00:00:00Z', to: '2026-06-30T00:00:00Z', page: 2, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/audit?entityType=Vote&actor=kc-1&action=Vote.Closed&from=2026-06-01T00%3A00%3A00Z&to=2026-06-30T00%3A00%3A00Z&page=2&pageSize=25');
  });

  it('omits the query string entirely when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(0) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAuditRegister({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/audit');
  });
});
