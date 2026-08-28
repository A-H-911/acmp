import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useMembers, useInviteUser, useAssignRoles, useStreams, useAssignStreams,
  useSetVotingEligibility, useCreateStream, useRenameStream,
} from './members';
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
    result.current.mutate({ email: 'new@acmp.gov', fullName: 'New Person', streamPublicIds: ['s1'] });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toContain('/members/invite');
    // ⚠ streamPublicIds is REQUIRED server-side (ADR-0043 clause 2); a body without it is refused,
    // so asserting the old two-field shape would have pinned a request that cannot succeed.
    expect(lastBody(spy)).toEqual({ email: 'new@acmp.gov', fullName: 'New Person', streamPublicIds: ['s1'] });
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
    result.current.mutate({ email: 'new@acmp.gov', fullName: 'New Person', streamPublicIds: ['s1'] });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(lastHeaders(spy)['Content-Type']).toBe('application/json');
  });

  it('invalidates the roster so the new Invited member appears', async () => {
    stubFetch(() => ({ jsonBody: invited }));
    const { wrapper, client } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');

    const { result } = renderHook(() => useInviteUser(), { wrapper });
    result.current.mutate({ email: 'new@acmp.gov', fullName: 'New Person', streamPublicIds: ['s1'] });

    // Without this the roster keeps its cached list and the invited person is invisible until a
    // manual refresh — which reads exactly like the invite having failed.
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ['members'] }));
  });

  it('surfaces a server refusal as an error rather than a silent no-op', async () => {
    stubFetch(() => ({ status: 409, jsonBody: { title: 'Conflict' } }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useInviteUser(), { wrapper });
    result.current.mutate({ email: 'dupe@acmp.gov', fullName: 'Dupe Person', streamPublicIds: ['s1'] });

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

describe('useStreams / useAssignStreams (BL-024 / ADR-0042 step 3)', () => {
  it('reads the committee taxonomy from the members area', async () => {
    const spy = stubFetch(() => ({ jsonBody: [] }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useStreams(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toContain('/members/streams');
  });

  // ⚠ THE BODY IS A BARE ARRAY, not a wrapped object. The minimal-API endpoint binds
  // `IReadOnlyList<Guid> streamPublicIds` directly, so a hook that sent `{ streamPublicIds: [...] }`
  // would bind to an empty list and silently CLEAR the member's streams instead of setting them —
  // a wrong-shape body that succeeds is far worse here than one that 400s.
  it('PUTs a bare array of stream ids, replacing the assignment', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useAssignStreams(), { wrapper });
    result.current.mutate({ publicId: 'm-1', streamPublicIds: ['s1', 's2'] });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toContain('/members/m-1/streams');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('PUT');
    expect(lastHeaders(spy)['Content-Type']).toBe('application/json');
    expect(lastBody(spy)).toEqual(['s1', 's2']);
  });

  // Clearing every stream must reach the server as an empty array — it is a legitimate correction,
  // and a hook that skipped the call would leave the roster showing streams the member no longer has.
  it('sends an empty array when every stream is cleared', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useAssignStreams(), { wrapper });
    result.current.mutate({ publicId: 'm-1', streamPublicIds: [] });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(lastBody(spy)).toEqual([]);
  });
});

describe('useSetVotingEligibility (DEF-041 / DEC-046 d4)', () => {
  // ⚠ THE DESIRED STATE IS ON THE WIRE, NOT A TOGGLE INSTRUCTION. Two clicks racing each other
  // would otherwise resolve to whichever order the server happened to see last, and neither caller
  // could tell what they had set. Asserting the BODY is what pins that — a URL-only assertion
  // passes just as well against a "flip it" endpoint.
  it('PUTs the requested state to the member voting-eligibility route', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useSetVotingEligibility(), { wrapper });
    result.current.mutate({ publicId: 'm-9', isVotingEligible: false });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toContain('/members/m-9/voting-eligibility');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('PUT');
    expect(lastHeaders(spy)['Content-Type']).toBe('application/json');
    expect(lastBody(spy)).toEqual({ isVotingEligible: false });
  });

  it('carries the true direction too — granting a vote, not only removing one', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useSetVotingEligibility(), { wrapper });
    result.current.mutate({ publicId: 'm-9', isVotingEligible: true });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(lastBody(spy)).toEqual({ isVotingEligible: true });
  });

  // A 403 is the matrix working (the server refuses Administrator under SoD-5), so the hook must
  // surface it as an error rather than resolving and letting the switch look as if it moved.
  it('surfaces a refusal as an error', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useSetVotingEligibility(), { wrapper });
    result.current.mutate({ publicId: 'm-9', isVotingEligible: true });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

/*
 * WBS-24.7 — the two write paths that make NFR-010's configuration-driven clause true. These run the
 * REAL hooks against a stubbed fetch, so the URL, the verb and the body shape are asserted rather
 * than assumed; the component tests mock these hooks away and could never catch a wrong URL.
 */
describe('useCreateStream', () => {
  it('POSTs the code and both names to the streams collection', async () => {
    const spy = stubFetch(() => ({ jsonBody: { publicId: 'new-1' } }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useCreateStream(), { wrapper });
    result.current.mutate({ code: 'mobile', nameEn: 'Mobile', nameAr: 'الجوال' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toBe('/api/members/streams');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toEqual({ code: 'mobile', nameEn: 'Mobile', nameAr: 'الجوال' });
  });

  // The duplicate-code refusal is the one an administrator meets. If the hook resolved on it, the
  // dialog would close and report a stream that does not exist.
  it('surfaces a refused duplicate as an error', async () => {
    stubFetch(() => ({ status: 400, jsonBody: { title: 'already exists' } }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useCreateStream(), { wrapper });
    result.current.mutate({ code: 'core', nameEn: 'Core', nameAr: 'الأساسي' });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe('useRenameStream', () => {
  it('PUTs to the stream and sends ONLY the two names', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useRenameStream(), { wrapper });
    result.current.mutate({ publicId: 's-1', nameEn: 'Core Platform', nameAr: 'منصة الأساس' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(String(spy.mock.calls.at(-1)![0])).toBe('/api/members/streams/s-1');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('PUT');
    // ⚠ NO `code` KEY. Topics carry the code and the ABAC intersect resolves on it, so the API must
    // not be able to express a re-code at all — a body carrying one would be a data migration.
    expect(lastBody(spy)).toEqual({ nameEn: 'Core Platform', nameAr: 'منصة الأساس' });
  });

  it('surfaces a refusal as an error rather than resolving', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => useRenameStream(), { wrapper });
    result.current.mutate({ publicId: 'missing', nameEn: 'X', nameAr: 'س' });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
