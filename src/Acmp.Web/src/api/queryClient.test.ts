import { describe, it, expect } from 'vitest';
import { queryClient } from './queryClient';
import { ApiError } from './apiClient';

/*
 * The retry policy is a correctness rule, not cosmetics: retrying a 4xx wastes
 * the user's time on an error that won't fix itself (auth/validation), while a
 * transient 5xx/network blip deserves exactly one more try. Assert the predicate
 * directly so the rule can't silently regress.
 */
describe('queryClient retry policy', () => {
  const retry = queryClient.getDefaultOptions().queries!.retry as (
    failureCount: number,
    error: Error,
  ) => boolean;

  it('never retries a 4xx ApiError', () => {
    expect(retry(0, new ApiError(400))).toBe(false);
    expect(retry(0, new ApiError(401))).toBe(false);
    expect(retry(0, new ApiError(404))).toBe(false);
  });

  it('retries a 5xx ApiError once, then stops', () => {
    expect(retry(0, new ApiError(500))).toBe(true);
    expect(retry(1, new ApiError(503))).toBe(false);
  });

  it('retries a non-ApiError (network blip) once, then stops', () => {
    expect(retry(0, new Error('network'))).toBe(true);
    expect(retry(1, new Error('network'))).toBe(false);
  });

  it('does not refetch on window focus (low-traffic committee tool)', () => {
    expect(queryClient.getDefaultOptions().queries!.refetchOnWindowFocus).toBe(false);
  });
});
