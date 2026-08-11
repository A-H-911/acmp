import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useMembers, useInviteUser } from './members';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real member hooks vs a stubbed fetch — assert URL building, the invite body, and cache invalidation. */
afterEach(() => vi.unstubAllGlobals());

describe('useMembers', () => {
  it('reads the directory', async () => {
    const spy = stubFetch(() => ({ jsonBody: [] }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useMembers(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toContain('/members');
  });
});

describe('useInviteUser (FR-156 / AC-088)', () => {
  const invited = {
    publicId: 'p1',
    fullName: 'New Person',
    email: 'new@acmp.gov',
    status: 'Invited',
    temporaryPassword: 'T3mp-Pass',
  };

  it('POSTs the email and full name and returns the one-time password', async () => {
    const spy = stubFetch(() => ({ jsonBody: invited }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useInviteUser(), { wrapper });
    result.current.mutate({ email: 'new@acmp.gov', fullName: 'New Person' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toContain('/members/invite');
    expect(lastBody(spy)).toEqual({ email: 'new@acmp.gov', fullName: 'New Person' });
    expect(result.current.data?.temporaryPassword).toBe('T3mp-Pass');
  });

  it('invalidates the roster so the new Invited member appears', async () => {
    stubFetch(() => ({ jsonBody: invited }));
    const { wrapper, client } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');

    const { result } = renderHook(() => useInviteUser(), { wrapper });
    result.current.mutate({ email: 'new@acmp.gov', fullName: 'New Person' });

    // Without this the roster keeps its cached list and the invited person is invisible until a
    // manual refresh — which reads exactly like the invite having failed.
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ['members'] }));
  });

  it('surfaces a server refusal as an error rather than a silent no-op', async () => {
    stubFetch(() => ({ status: 409, jsonBody: { title: 'Conflict' } }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useInviteUser(), { wrapper });
    result.current.mutate({ email: 'dupe@acmp.gov', fullName: 'Dupe Person' });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
