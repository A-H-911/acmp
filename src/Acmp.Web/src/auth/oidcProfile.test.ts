import { describe, it, expect } from 'vitest';
import { claimStringsFrom, displayNameFrom, initialsFrom } from './oidcProfile';

describe('oidcProfile helpers', () => {
  it('gathers role claims from realm, client, and group sources', () => {
    const claims = claimStringsFrom({
      realm_access: { roles: ['chairman', 'offline_access'] },
      resource_access: { 'acmp-web': { roles: ['member'] } },
      groups: ['/acmp/auditor'],
    });
    expect(claims).toEqual(['chairman', 'offline_access', 'member', '/acmp/auditor']);
  });

  it('returns no claims for an empty profile', () => {
    expect(claimStringsFrom(undefined)).toEqual([]);
    expect(claimStringsFrom({})).toEqual([]);
  });

  it('prefers name, then username, then email for display', () => {
    expect(displayNameFrom({ name: 'Sara Q', preferred_username: 'sq' })).toBe('Sara Q');
    expect(displayNameFrom({ preferred_username: 'sq' })).toBe('sq');
    expect(displayNameFrom({ email: 'a@b.co' })).toBe('a@b.co');
    expect(displayNameFrom(undefined)).toBe('User');
  });

  it('derives initials from one or two names', () => {
    expect(initialsFrom('Sara Qasim')).toBe('SQ');
    expect(initialsFrom('Sara')).toBe('SA');
    expect(initialsFrom('')).toBe('?');
  });
});
