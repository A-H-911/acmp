import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useBacklog,
  useTopicDetail,
  useSubmitTopic,
  useAcceptTopic,
  useReturnTopic,
  usePrepareTopic,
  useReactivateTopic,
  useCloseTopic,
  useReopenTopic,
  useConvertTopic,
  useReclassifyTopic,
  useSetTopicConfidentiality,
  useUpdateTopic,
  useMoveTopicPriority,
  useAddTopicComment,
  useUploadTopicAttachment,
  uploadTopicAttachment,
  downloadTopicAttachment,
  useConvertResearchToTopic,
} from './topics';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';
import { FakeXhr, installFakeXhr } from '../test/fakeXhr';

/*
 * Real topic hooks against a stubbed fetch. The screen tests mock these hooks, so
 * the URL building, request bodies, and cache invalidation are unasserted there —
 * this is where they get proven. Failure-first: a denied/404 must surface as an error.
 */
afterEach(() => vi.unstubAllGlobals());

function urlOf(spy: ReturnType<typeof stubFetch>): string {
  return String(spy.mock.calls.at(-1)![0]);
}

describe('useBacklog', () => {
  it('builds a repeated status param and omits empty filters', async () => {
    const spy = stubFetch(() => ({ jsonBody: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 0 } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useBacklog({ statuses: ['Proposed', 'Accepted'], search: 'auth', page: 2 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const url = urlOf(spy);
    expect(url).toContain('/api/topics?');
    expect(url).toContain('status=Proposed');
    expect(url).toContain('status=Accepted');
    expect(url).toContain('search=auth');
    expect(url).toContain('page=2');
    expect(url).not.toContain('type=');
    expect(url).not.toContain('stream=');
  });

  it('maps every supported filter into the query string', async () => {
    const spy = stubFetch(() => ({ jsonBody: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 0 } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () =>
        useBacklog({
          type: 'Standard', stream: 'data', urgency: 'High', ownerId: 'u1',
          includeClosed: true, sortBy: 'priority', sortDir: 'desc', pageSize: 50,
        }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const url = urlOf(spy);
    for (const part of ['type=Standard', 'stream=data', 'urgency=High', 'ownerId=u1', 'includeClosed=true', 'sortBy=priority', 'sortDir=desc', 'pageSize=50']) {
      expect(url).toContain(part);
    }
  });

  it('emits a bare /topics when no filters are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 0 } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useBacklog({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics');
  });

  it('surfaces a server error instead of swallowing it', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useBacklog({}), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(403);
  });
});

describe('useTopicDetail', () => {
  it('does not fetch while the key is undefined (enabled gate)', async () => {
    const spy = stubFetch(() => ({ jsonBody: {} }));
    const { wrapper } = makeQueryWrapper();
    renderHook(() => useTopicDetail(undefined), { wrapper });
    // give the query a tick; it must stay idle
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
  });

  it('reads by key once a key is provided', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'TOP-2026-001' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useTopicDetail('TOP-2026-001'), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/TOP-2026-001');
  });
});

describe('topic mutations', () => {
  it('useSubmitTopic POSTs the payload and invalidates the backlog', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'g1', key: 'TOP-2026-009' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useSubmitTopic(), { wrapper });
    result.current.mutate({
      title: 'T', description: 'D', justification: 'J', type: 'Standard',
      urgency: 'Normal', source: 'CommitteeMember', streams: [], systems: [], tags: [],
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics');
    expect((init as RequestInit).method).toBe('POST');
    expect((lastBody(spy) as { title: string }).title).toBe('T');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
  });

  it('useAcceptTopic posts the owner to the accept endpoint', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAcceptTopic(), { wrapper });
    result.current.mutate({ topicId: 'abc', ownerId: 'u1', ownerName: 'Owner One' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/accept');
    expect(lastBody(spy)).toEqual({ ownerId: 'u1', ownerName: 'Owner One' });
  });

  it('useMoveTopicPriority POSTs the ±1 delta to the move endpoint and invalidates the backlog (AC-043)', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useMoveTopicPriority(), { wrapper });
    result.current.mutate({ topicId: 'abc', delta: 1 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/priority/move');
    expect(lastBody(spy)).toEqual({ delta: 1 });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
  });

  it('useMoveTopicPriority sends the TARGET IDENTITY for a drag, never a computed position (AC-141)', async () => {
    // This client renders the filtered, sorted and page-truncated backlog, so its indices are not the
    // server's column indices. The body must therefore carry targetTopicId; delta 0 is the sentinel that
    // tells the server which addressing mode is in play (its validator refuses 0 with no target).
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMoveTopicPriority(), { wrapper });
    result.current.mutate({ topicId: 'abc', targetTopicId: 'xyz' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/priority/move');
    expect(lastBody(spy)).toEqual({ delta: 0, targetTopicId: 'xyz' });
  });

  it('usePrepareTopic POSTs to the prepare endpoint and invalidates backlog, pool, and detail', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => usePrepareTopic('TOP-2026-001'), { wrapper });
    result.current.mutate('abc');
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/prepare');
    expect((init as RequestInit).method).toBe('POST');
    // the pool key is what unblocks the agenda builder (D-15) — assert all three invalidations
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'prepared'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  // FR-160 / FR-161 / FR-045 — the lifecycle exits (AC-109, AC-110, AC-112). Each asserts the URL,
  // the verb and the invalidations: a mutation that hits the right endpoint but refreshes nothing
  // leaves the user staring at the pre-transition status, which is indistinguishable from a failure.
  it('useReactivateTopic POSTs to the reactivate endpoint and invalidates backlog + detail', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useReactivateTopic('TOP-2026-001'), { wrapper });
    result.current.mutate('abc');
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/reactivate');
    expect((init as RequestInit).method).toBe('POST');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  it('useCloseTopic POSTs to the close endpoint and invalidates backlog + detail', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useCloseTopic('TOP-2026-001'), { wrapper });
    result.current.mutate('abc');
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/close');
    expect((init as RequestInit).method).toBe('POST');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  it('useReopenTopic POSTs the justification the server requires', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useReopenTopic('TOP-2026-001'), { wrapper });
    result.current.mutate({ topicId: 'abc', reason: 'new regulatory guidance' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/reopen');
    expect((init as RequestInit).method).toBe('POST');
    // the reason travels as `reason` because the endpoint binds ReasonBody (FR-044's mandatory-reason rule)
    expect(lastBody(spy)).toEqual({ reason: 'new regulatory guidance' });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  // AC-113 / FR-030. Unlike the other lifecycle mutations this one READS the response: the server
  // creates a successor topic and the caller navigates to it, so a hook that swallowed the body
  // would leave the UI stranded on a topic that is now terminal.
  it('useConvertTopic POSTs the target type and reason, and returns the successor key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'new-guid', key: 'TOP-2026-777' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useConvertTopic('TOP-2026-001'), { wrapper });
    result.current.mutate({ topicId: 'abc', targetType: 'ArchitectureDecision', reason: 'research concluded' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/convert');
    expect((init as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toEqual({ targetType: 'ArchitectureDecision', reason: 'research concluded' });
    expect(result.current.data).toEqual({ id: 'new-guid', key: 'TOP-2026-777' });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
    // both topics' traceability panels gained the ConvertedTo edge
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['traceability'] });
  });

  // FR-164 / DW-032. Sends BOTH fields because the endpoint and the domain method take both (the
  // dialog offers both since WBS-40.2). Invalidates the BACKLOG as well as the detail: type is a
  // backlog column and facet, and source a facet.
  it('useReclassifyTopic POSTs the type and source, and invalidates backlog + detail', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useReclassifyTopic('TOP-2026-001'), { wrapper });
    result.current.mutate({ topicId: 'abc', type: 'ResearchDiscovery', source: 'SecurityFinding' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/reclassify');
    expect((init as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toEqual({ type: 'ResearchDiscovery', source: 'SecurityFinding' });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  // FR-163 / C-AUTHZ-04. PUT of the desired STATE, not a POST of an action, so a repeat is a no-op
  // rather than a second classification event. Invalidates the BACKLOG as well as the detail because
  // classifying removes the topic from other people's lists.
  it('useSetTopicConfidentiality PUTs the desired state and invalidates backlog + detail', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useSetTopicConfidentiality('TOP-2026-001'), { wrapper });
    result.current.mutate({ topicId: 'abc', restricted: true });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/confidentiality');
    expect((init as RequestInit).method).toBe('PUT');
    expect(lastBody(spy)).toEqual({ restricted: true });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  // AC-034 / DEF-058. The endpoint REPLACES, so every editable field must go out on every save —
  // and `scope` must be absent unless the editor changed it, because the server reads a missing
  // scope as "leave it alone" and only then skips the triage authorization.
  it('useUpdateTopic PUTs the whole editable topic and invalidates detail + backlog', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useUpdateTopic('TOP-2026-001'), { wrapper });

    result.current.mutate({
      topicId: 'abc',
      edit: {
        title: 'T', description: 'D', justification: 'J', urgency: 'Urgent',
        streams: ['core'], systems: ['Auth'], tags: ['iam'], scope: 'OrgWide',
      },
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(urlOf(spy)).toBe('/api/topics/abc');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('PUT');
    expect(lastBody(spy)).toEqual({
      title: 'T', description: 'D', justification: 'J', urgency: 'Urgent',
      streams: ['core'], systems: ['Auth'], tags: ['iam'], scope: 'OrgWide',
    });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
  });

  it('useUpdateTopic surfaces a refusal rather than reporting success', async () => {
    stubFetch(() => ({ status: 403 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useUpdateTopic('TOP-2026-001'), { wrapper });

    result.current.mutate({
      topicId: 'abc',
      edit: { title: 'T', description: 'D', justification: 'J', urgency: 'Normal', streams: ['core'], systems: [], tags: [], scope: 'OrgWide' },
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ApiError);
  });

  it('useReturnTopic routes reject vs defer to different endpoints/bodies', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result: reject } = renderHook(() => useReturnTopic(), { wrapper });
    reject.current.mutate({ topicId: 'abc', mode: 'reject', reason: 'out of scope' });
    await waitFor(() => expect(reject.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/reject');
    expect(lastBody(spy)).toEqual({ reason: 'out of scope' });

    const { result: defer } = renderHook(() => useReturnTopic(), { wrapper });
    defer.current.mutate({ topicId: 'abc', mode: 'defer', reason: 'later', revisitOn: '2026-09-01' });
    await waitFor(() => expect(defer.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/defer');
    expect(lastBody(spy)).toEqual({ reason: 'later', revisitOn: '2026-09-01' });
  });

  it('useReturnTopic defaults revisitOn to null when omitted', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useReturnTopic(), { wrapper });
    result.current.mutate({ topicId: 'abc', mode: 'defer', reason: 'later' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(lastBody(spy)).toEqual({ reason: 'later', revisitOn: null });
  });

  it('useAddTopicComment maps the body to the reason field and invalidates the detail', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'c1' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useAddTopicComment('TOP-2026-001'), { wrapper });
    result.current.mutate({ topicId: 'abc', body: 'a comment' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/comments');
    expect(lastBody(spy)).toEqual({ reason: 'a comment' });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  it('uploadTopicAttachment sends the file as multipart FormData and reports progress (DEF-171)', async () => {
    installFakeXhr();
    const seen: number[] = [];
    const file = new File(['x'], 'spec.pdf', { type: 'application/pdf' });
    const done = uploadTopicAttachment('abc', file, (p) => seen.push(p));
    const xhr = FakeXhr.last!;
    expect(xhr.url).toBe('/api/topics/abc/attachments');
    expect(xhr.body).toBeInstanceOf(FormData);
    expect((xhr.body as FormData).get('file')).toBe(file);
    expect(xhr.headers['Content-Type']).toBeUndefined(); // browser sets the multipart boundary
    xhr.progress(1, 2);
    xhr.respond(201, {});
    await done;
    expect(seen).toEqual([50]);
  });

  // WBS-40.12 / AC-163: the URL is fetched on click from the topic-scoped route and opened in a new tab.
  // AC-164: Download fetches the topic-scoped URL on click and follows it in place - no new tab, no popup.
  it('downloadTopicAttachment fetches the topic-scoped URL and follows it without opening a window', async () => {
    const spy = stubFetch(() => ({ jsonBody: { url: 'https://storage.example/presigned' } }));
    const open = vi.fn();
    vi.stubGlobal('open', open);
    const clicked: string[] = [];
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { clicked.push(this.href); });
    await downloadTopicAttachment('t1', 'a1');
    expect(urlOf(spy)).toBe('/api/topics/t1/attachments/a1/url');
    expect(clicked).toEqual(['https://storage.example/presigned']);
    expect(open).not.toHaveBeenCalled();
    click.mockRestore();
  });

  it('downloadTopicAttachment rejects on a 404 and follows nothing', async () => {
    stubFetch(() => ({ status: 404, jsonBody: {} }));
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    await expect(downloadTopicAttachment('t1', 'gone')).rejects.toBeInstanceOf(ApiError);
    expect(click).not.toHaveBeenCalled();
    click.mockRestore();
  });

  it('useUploadTopicAttachment invalidates the detail query on success', async () => {
    installFakeXhr();
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useUploadTopicAttachment('TOP-2026-001'), { wrapper });
    result.current.mutate({ topicId: 'abc', file: new File(['x'], 'a.pdf') });
    await waitFor(() => expect(FakeXhr.last).toBeDefined());
    FakeXhr.last!.respond(201, {});
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  it('useConvertResearchToTopic POSTs /topics/from-research and invalidates the backlog', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'top-guid', key: 'TOP-2026-030' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useConvertResearchToTopic(), { wrapper });
    const body = {
      missionId: 'm1', recommendationId: 'r2', title: 'T', description: 'D', justification: 'J',
      type: 'ResearchDiscovery', urgency: 'Normal', streams: ['IAM'], systems: [], tags: [],
    };
    result.current.mutate(body);
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/from-research');
    expect(lastBody(spy)).toEqual(body);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
  });
});
