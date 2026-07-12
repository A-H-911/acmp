import { describe, it, expect } from 'vitest';
import { auditTone, outcomeTone, actorInitials, formatTimestamp, AUDIT_ENTITY_TYPES } from './auditMeta';

describe('auditTone', () => {
  it('overrides to danger for a Denied/Failure outcome, whatever the verb', () => {
    expect(auditTone('Vote.Closed', 'Denied')).toBe('danger');
    expect(auditTone('Topic.Created', 'Failure')).toBe('danger');
  });

  it('maps the verb keyword to the design tone', () => {
    expect(auditTone('Decision.Issued', 'Success')).toBe('success');
    expect(auditTone('Topic.Updated', 'Success')).toBe('info');
    expect(auditTone('Vote.BallotCast', 'Success')).toBe('scheduled');
    expect(auditTone('Minutes.Locked', 'Success')).toBe('neutral');
    expect(auditTone('Adr.Superseded', 'Success')).toBe('warn');
    expect(auditTone('Membership.RoleGranted', 'Success')).toBe('danger'); // 'role' → danger
  });

  it('falls back to neutral for an unrecognized verb', () => {
    expect(auditTone('Something.Weird', null)).toBe('neutral');
  });
});

describe('outcomeTone', () => {
  it('is danger for Denied/Failure, neutral otherwise', () => {
    expect(outcomeTone('Denied')).toBe('danger');
    expect(outcomeTone('Failure')).toBe('danger');
    expect(outcomeTone('Success')).toBe('neutral');
    expect(outcomeTone(null)).toBe('neutral');
  });
});

describe('actorInitials', () => {
  it('is empty for a system/null actor', () => {
    expect(actorInitials(null)).toBe('');
  });
  it('takes the first letter of the first two hyphen parts', () => {
    expect(actorInitials('kc-chair')).toBe('KC');
  });
  it('falls back to the first two characters of a single-token subject', () => {
    expect(actorInitials('system')).toBe('SY');
  });
});

describe('formatTimestamp', () => {
  it('formats a Gregorian date · 24h time in English', () => {
    const s = formatTimestamp('2026-06-24T14:22:07Z', 'en');
    expect(s).toContain('2026');
    expect(s).toContain('·');
  });
  it('formats in Arabic with Gregorian calendar + Latin digits (INV-009)', () => {
    const s = formatTimestamp('2026-06-24T14:22:07Z', 'ar');
    expect(s).toContain('·');
    expect(s).toContain('2026'); // Latin numerals, not Arabic-Indic — the INV-009 obligation
    expect(s).toContain('24');
  });
  it('returns the raw input for an unparseable timestamp', () => {
    expect(formatTimestamp('not-a-date', 'en')).toBe('not-a-date');
  });
});

describe('AUDIT_ENTITY_TYPES', () => {
  it('lists the governed CLR aggregate names the entityType filter matches', () => {
    expect(AUDIT_ENTITY_TYPES).toContain('Vote');
    expect(AUDIT_ENTITY_TYPES).toContain('MinutesOfMeeting');
  });
});
