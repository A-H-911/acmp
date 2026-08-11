import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useMembers, useInviteUser, useAssignRoles } from './members';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/** The headers of the most recent fetch call. */
function lastHeaders(spy: ReturnType<typeof stubFetch>): Record<string, string> {
  return ((spy.mock.calls.at(-1)?.[1] as RequestInit | undefined)?.headers ?? {}) as Record<string, string>;
}

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

  // DEF-046. This shipped without the header and could never have worked: minimal-API body binding
  // answers 415 Unsupported Media Type, verified against the real pipeline. Nothing caught it —
  // every backend test uses PostAsJsonAsync, which sets the header itself, and the panel test mocks
  // the hook away. The assertion belongs HERE, where the omission was.
  it('sends application/json so the server can bind the body at all (DEF-046)', async () => {
    const spy = stubFetch(() => ({ jsonBody: invited }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useInviteUser(), { wrapper });
    result.current.mutate({ email: 'new@acmp.gov', fullName: 'New Person' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(lastHeaders(spy)['Content-Type']).toBe('application/json');
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

describe('useAssignRoles (FR-157 / AC-089)', () => {
  it('PUTs the role set and the confirmation flag to the member', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useAssignRoles(), { wrapper });
    result.current.mutate({ publicId: 'm-1', roles: ['Reviewer'], confirmedPrivileged: false });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toContain('/members/m-1/roles');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('PUT');
    expect(lastHeaders(spy)['Content-Type']).toBe('application/json');
    expect(lastBody(spy)).toEqual({ roles: ['Reviewer'], confirmedPrivileged: false });
  });

  // The server REFUSES a privileged grant without this flag, so a hook that dropped it would make
  // the whole confirmation unreachable — the failure would look like a server bug, not a lost field.
  it('carries confirmedPrivileged through to the wire when a privileged role is granted', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useAssignRoles(), { wrapper });
    result.current.mutate({ publicId: 'm-1', roles: ['Administrator'], confirmedPrivileged: true });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(lastBody(spy)).toEqual({ roles: ['Administrator'], confirmedPrivileged: true });
  });

  it('invalidates the roster so the directory stops showing the old role', async () => {
    stubFetch(() => ({ status: 204 }));
    const { wrapper, client } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');

    const { result } = renderHook(() => useAssignRoles(), { wrapper });
    result.current.mutate({ publicId: 'm-1', roles: ['Reviewer'], confirmedPrivileged: false });

    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ['members'] }));
  });

  it('surfaces a guard refusal as an error rather than a silent no-op', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useAssignRoles(), { wrapper });
    result.current.mutate({ publicId: 'self', roles: ['Member'], confirmedPrivileged: false });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
