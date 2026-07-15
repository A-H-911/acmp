import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useSearch } from './search';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

/* Real useSearch hook vs a stubbed fetch — blank terms stay idle; a real term encodes into /api/search?q=. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);

describe('useSearch', () => {
  it('stays idle for a blank query (no request)', async () => {
    const spy = stubFetch(() => ({ jsonBody: [] }));
    const { wrapper } = makeQueryWrapper();
    renderHook(() => useSearch('   '), { wrapper });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
  });

  it('encodes the trimmed term and requests grouped results', async () => {
    const spy = stubFetch(() => ({ jsonBody: [{ type: 'Topics', items: [] }] }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSearch('  adopt keycloak  '), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/search?q=adopt%20keycloak');
  });
});
