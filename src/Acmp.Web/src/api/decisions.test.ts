import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useDecision, useDecisionsRegister, useSupersedeDecision, useRecordDecision, useIssueDecision } from './decisions';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real decisions hooks vs a stubbed fetch — assert URL/method/body + cache invalidation. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const methodOf = (spy: ReturnType<typeof stubFetch>) => (spy.mock.calls.at(-1)![1] as RequestInit | undefined)?.method;

describe('useDecision', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'DECN-2026-008' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useDecision(k), {
      wrapper,
      initialProps: {} as { k?: string },
    });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'DECN-2026-008' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/decisions/DECN-2026-008');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useDecision('DECN-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('useDecisionsRegister', () => {
  it('passes status + limit as query params', async () => {
    const spy = stubFetch(() => ({ jsonBody: [{ id: '1', key: 'DECN-2026-008', status: 'Issued' }] }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useDecisionsRegister({ status: 'Issued', limit: 5 }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/decisions?status=Issued&limit=5');
  });

  it('reads the whole register with no params', async () => {
    const spy = stubFetch(() => ({ jsonBody: [] }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useDecisionsRegister(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/decisions');
  });
});

describe('useSupersedeDecision', () => {
  it('POSTs the successor body to the prior id and invalidates the prior detail', async () => {
    const spy = stubFetch(() => ({ status: 201, jsonBody: { id: 's1', key: 'DECN-2026-015' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useSupersedeDecision('DECN-2026-008'), { wrapper });
    result.current.mutate({
      priorDecisionId: 'p1',
      outcome: 'Approved',
      title: { en: 'Adopt Keycloak (v2)', ar: 'اعتماد كيكلوك (٢)' },
      statement: { en: 'The committee adopts Keycloak (v2).', ar: 'تعتمد اللجنة.' },
      rationale: { en: 'Corrected', ar: 'مصحح' },
      alternatives: null,
      conditions: [],
      reason: { en: 'Federated pivot', ar: 'تحوّل' },
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/decisions/p1/supersede');
    expect(methodOf(spy)).toBe('POST');
    const body = lastBody(spy) as { priorDecisionId?: string; title: { en: string } };
    expect(body).not.toHaveProperty('priorDecisionId'); // stripped from the body — it's the URL id
    expect(body.title.en).toBe('Adopt Keycloak (v2)');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['decisions', 'detail', 'DECN-2026-008'] });
  });

  it('surfaces a 403 (non-chair) instead of swallowing it', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useSupersedeDecision('DECN-2026-008'), { wrapper });
    result.current.mutate({
      priorDecisionId: 'p1', outcome: 'Approved',
      title: { en: 't', ar: 't' }, statement: { en: 's', ar: 's' }, rationale: { en: 'r', ar: 'r' },
      conditions: [], reason: { en: 'x', ar: 'x' },
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(403);
  });
});

describe('useRecordDecision', () => {
  it('POSTs the draft body (incl. the coupled voteId) and invalidates the register', async () => {
    const spy = stubFetch(() => ({ status: 201, jsonBody: { id: 'd1', key: 'DECN-2026-020' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useRecordDecision(), { wrapper });
    result.current.mutate({
      topicId: 'top-1', meetingId: 'mtg-1', outcome: 'Rejected',
      title: { en: 'Reject the migration', ar: 'رفض' }, statement: { en: 's', ar: 's' }, rationale: { en: 'r', ar: 'r' },
      alternatives: null, voteId: 'vote-1', conditions: [],
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/decisions');
    expect(methodOf(spy)).toBe('POST');
    expect((lastBody(spy) as { voteId?: string }).voteId).toBe('vote-1');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['decisions', 'register'] });
  });
});

describe('useIssueDecision', () => {
  it('POSTs the override body to /{id}/issue and refreshes both decisions and votes (auto-ratify)', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useIssueDecision(), { wrapper });
    result.current.mutate({ id: 'd1', chairOverride: true, overrideJustification: { en: 'j', ar: 'j' } });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/decisions/d1/issue');
    expect(methodOf(spy)).toBe('POST');
    expect(lastBody(spy)).toMatchObject({ chairOverride: true, overrideJustification: { en: 'j' } });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['decisions'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['votes'] });
  });

  it('surfaces the SoD-3 403 (chair was the vote closer) instead of swallowing it', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Denied' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useIssueDecision(), { wrapper });
    result.current.mutate({ id: 'd1', chairOverride: false, overrideJustification: null });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(403);
  });
});
