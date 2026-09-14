import { describe, it, expect, afterEach, vi } from 'vitest';
import { api, apiUpload, ApiError, setTokenGetter, localizedValidationMessage, type ProblemDetails } from './apiClient';
import { FakeXhr, installFakeXhr } from '../test/fakeXhr';
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

// DEF-171: the upload path reports progress, and fails exactly the way api() does.
describe('apiUpload() - XHR upload with progress', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    setTokenGetter(() => undefined);
  });

  it('posts the form with the token and language, reports whole percentages, and resolves the JSON body', async () => {
    installFakeXhr();
    setTokenGetter(() => 'tok-9');
    const seen: number[] = [];
    const form = new FormData();
    const done = apiUpload<{ id: string }>('/topics/t1/attachments', form, (p) => seen.push(p));
    const xhr = FakeXhr.last!;
    expect(xhr.method).toBe('POST');
    expect(xhr.url).toBe('/api/topics/t1/attachments');
    expect(xhr.body).toBe(form);
    expect(xhr.headers.Authorization).toBe('Bearer tok-9');
    expect(xhr.headers['Accept-Language']).toBe(i18n.language);
    expect(xhr.headers['Content-Type']).toBeUndefined(); // the browser sets the multipart boundary
    xhr.progress(25, 100);
    xhr.progress(999, 1000);
    xhr.respond(201, { id: 'a1' });
    await expect(done).resolves.toEqual({ id: 'a1' });
    expect(seen).toEqual([25, 99]);
  });

  it('rejects a refusal as ApiError with the problem and the auth reason', async () => {
    installFakeXhr();
    const done = apiUpload('/x', new FormData());
    FakeXhr.last!.respond(400, { title: 'Bad', errors: [{ errorCode: 'FILE_TOO_LARGE' }] }, { 'X-Acmp-Auth-Reason': 'roles_changed' });
    const err = (await done.catch((e: unknown) => e)) as ApiError;
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(400);
    expect(err.problem?.errors?.[0].errorCode).toBe('FILE_TOO_LARGE');
    expect(err.authRefusal).toBe('roles_changed');
  });

  it('rejects an empty-body refusal and a transport failure as ApiError without a problem', async () => {
    installFakeXhr();
    const empty = apiUpload('/x', new FormData());
    FakeXhr.last!.respond(400);
    await expect(empty).rejects.toMatchObject({ status: 400, problem: undefined });

    const dropped = apiUpload('/x', new FormData());
    FakeXhr.last!.fail();
    await expect(dropped).rejects.toMatchObject({ status: 0 });
  });
});
