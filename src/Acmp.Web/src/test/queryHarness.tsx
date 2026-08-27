/*
 * Test harness for the real data-layer hooks (S2). Feature/screen tests mock the
 * api hooks away; this harness does the opposite — it runs the REAL hook against a
 * stubbed `fetch`, so URL building, request bodies, retry rules, and cache
 * invalidation are actually asserted. Lives under src/test/** (coverage-excluded).
 */
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

/** A fresh QueryClient (retries off → failure-first tests resolve immediately) + its wrapper. */
export function makeQueryWrapper() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return { client, wrapper };
}

export interface FakeResponse {
  /** Defaults to 200. */
  status?: number;
  /** Overrides the ok derived from status (to force odd combinations). */
  ok?: boolean;
  /** The parsed JSON body returned by res.json(). */
  jsonBody?: unknown;
  /** When true, res.json() rejects — simulates a non-JSON error body. */
  jsonThrows?: boolean;
  /**
   * The body returned by res.blob() — for apiBlob's file downloads (WBS-24.6's audit export).
   * Purely additive: no existing caller reads it, and a stub that omits it still returns a Response
   * shaped exactly as before plus one unused method.
   */
  blobBody?: Blob;
  /**
   * Response headers. Needed since apiClient reads `X-Acmp-Auth-Reason` off every refusal (ADR-0039):
   * a fake response with no headers at all would throw before the ApiError was built.
   */
  headers?: Record<string, string>;
}

/**
 * Stub global.fetch. `impl` maps a request to a fake response; omit it (or return
 * undefined) for a default 200/empty. Call `vi.unstubAllGlobals()` in afterEach.
 */
export function stubFetch(impl?: (url: string, init?: RequestInit) => FakeResponse | undefined) {
  const spy = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const r = impl?.(url, init) ?? {};
    const status = r.status ?? 200;
    const ok = r.ok ?? (status >= 200 && status < 300);
    return {
      ok,
      status,
      headers: new Headers(r.headers ?? {}),
      json: async () => {
        if (r.jsonThrows) throw new Error('not json');
        return r.jsonBody;
      },
      blob: async () => r.blobBody ?? new Blob([]),
    } as Response;
  });
  vi.stubGlobal('fetch', spy);
  return spy;
}

/** The body of the most recent fetch call, parsed from JSON (or undefined). */
export function lastBody(spy: ReturnType<typeof stubFetch>): unknown {
  const init = spy.mock.calls.at(-1)?.[1] as RequestInit | undefined;
  const body = init?.body;
  return typeof body === 'string' ? JSON.parse(body) : body;
}
