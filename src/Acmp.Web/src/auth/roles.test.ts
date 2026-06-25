import { describe, it, expect } from 'vitest';
import { rolesFromClaims } from './roles';

describe('rolesFromClaims', () => {
  it('maps bare, prefixed, and group-path claim shapes', () => {
    expect(rolesFromClaims(['chairman'])).toEqual(['chairman']);
    expect(rolesFromClaims(['acmp-auditor'])).toEqual(['auditor']);
    expect(rolesFromClaims(['/acmp/member'])).toEqual(['member']);
  });

  it('treats legacy "coordinator" as secretary', () => {
    expect(rolesFromClaims(['coordinator'])).toEqual(['secretary']);
  });

  it('ignores unrelated claims and de-duplicates', () => {
    expect(rolesFromClaims(['offline_access', 'CHAIRMAN', 'chairman'])).toEqual(['chairman']);
  });

  it('returns empty for missing claims', () => {
    expect(rolesFromClaims(undefined)).toEqual([]);
    expect(rolesFromClaims([])).toEqual([]);
  });
});
