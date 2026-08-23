import { describe, it, expect, afterEach, vi } from 'vitest';
import { api, ApiError, setTokenGetter, localizedValidationMessage, type ProblemDetails } from './apiClient';
import { stubFetch } from '../test/queryHarness';
import i18n from '../i18n';

/*
 * The fetch wrapper is the single trust boundary between the SPA and the REST API.
 * Failure-first: a 4xx/5xx must become a typed ApiError that carries the RFC-7807
 * problem; a non-JSON error body must still produce an ApiError, never hang.
 */
describe('api() fetch wrapper', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    setTokenGetter(() => undefined); // reset module state between tests
    void i18n.changeLanguage('en');
  });

  function headersOf(spy: ReturnType<typeof stubFetch>): Record<string, string> {
    return (spy.mock.calls.at(-1)![1] as RequestInit).headers as Record<string, string>;
  }

  it('prefixes /api, sets Accept, and parses the JSON body', async () => {
    const spy = stubFetch(() => ({ jsonBody: { ok: true } }));
    const out = await api<{ ok: boolean }>('/topics');
    expect(spy).toHaveBeenCalledWith('/api/topics', expect.any(Object));
    expect(headersOf(spy).Accept).toBe('application/json');
    expect(out).toEqual({ ok: true });
  });

  it('attaches the bearer token from the injected getter', async () => {
    setTokenGetter(() => 'tok-123');
    const spy = stubFetch(() => ({ jsonBody: {} }));
    await api('/members/me', { method: 'POST' });
    expect(headersOf(spy).Authorization).toBe('Bearer tok-123');
  });

  it('omits Authorization entirely when there is no token (anonymous call)', async () => {
    const spy = stubFetch(() => ({ jsonBody: {} }));
    await api('/topics');
    expect(headersOf(spy).Authorization).toBeUndefined();
  });

  it('sends the active locale as Accept-Language', async () => {
    await i18n.changeLanguage('ar');
    const spy = stubFetch(() => ({ jsonBody: {} }));
    await api('/topics');
    expect(headersOf(spy)['Accept-Language']).toBe('ar');
  });

  it('returns undefined on 204 No Content without parsing a body', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const out = await api<void>('/topics/x/accept', { method: 'POST' });
    expect(out).toBeUndefined();
    // json() must not be relied on for 204 — the call still resolves.
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('throws a typed ApiError carrying the RFC-7807 problem on 400', async () => {
    const problem: ProblemDetails = {
      title: 'Validation failed',
      status: 400,
      errors: [{ propertyName: 'Title', errorMessage: 'Required', errorCode: 'NotEmptyValidator' }],
    };
    stubFetch(() => ({ status: 400, jsonBody: problem }));
    await expect(api('/topics', { method: 'POST' })).rejects.toMatchObject({
      name: 'ApiError',
      status: 400,
      message: 'Validation failed',
      problem: { errors: [{ errorCode: 'NotEmptyValidator' }] },
    });
  });

  it('falls back to an HTTP-status message when the error body is not JSON', async () => {
    stubFetch(() => ({ status: 500, jsonThrows: true }));
    const caught = await api('/topics').catch((e: unknown) => e);
    expect(caught).toBeInstanceOf(ApiError);
    const err = caught as ApiError;
    expect(err.status).toBe(500);
    expect(err.problem).toBeUndefined();
    expect(err.message).toBe('HTTP 500');
  });

  it('treats 401 as an ApiError (no silent swallow)', async () => {
    stubFetch(() => ({ status: 401, jsonBody: { title: 'Unauthorized' } }));
    await expect(api('/members')).rejects.toMatchObject({ status: 401, name: 'ApiError' });
  });

  it('merges caller headers over the defaults', async () => {
    const spy = stubFetch(() => ({ jsonBody: {} }));
    await api('/topics', { headers: { 'Content-Type': 'application/json' } });
    expect(headersOf(spy)['Content-Type']).toBe('application/json');
    expect(headersOf(spy).Accept).toBe('application/json');
  });
});

describe('localizedValidationMessage (BL-016)', () => {
  it('translates a known error code via the errors.* i18n catalog (not the raw server text)', () => {
    const msg = localizedValidationMessage({
      errors: [{ propertyName: 'ContentType', errorMessage: "File type 'x' is not allowed.", errorCode: 'FILE_TYPE_NOT_ALLOWED' }],
    });
    expect(msg).toBe(i18n.t('errors.FILE_TYPE_NOT_ALLOWED'));
  });

  it('falls back to the server ErrorMessage for an unmapped code', () => {
    const msg = localizedValidationMessage({
      errors: [{ propertyName: 'X', errorMessage: 'Server said no.', errorCode: 'SOME_UNMAPPED_CODE' }],
    });
    expect(msg).toBe('Server said no.');
  });

  it('returns the title when there are no field errors', () => {
    expect(localizedValidationMessage({ title: 'Nope' })).toBe('Nope');
  });
});
