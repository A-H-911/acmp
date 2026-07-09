import { describe, it, expect } from 'vitest';
import { notifType, notifKey } from './notifPresentation';

describe('notifType', () => {
  it('maps a known category to its label key, tone, and icon', () => {
    expect(notifType('AgendaPublished')).toEqual({
      labelKey: 'notif.type.agendaPublished',
      tone: 'info',
      icon: 'calendar',
    });
  });

  it('maps the TopicPrepared category to its info-toned type (D-15)', () => {
    expect(notifType('TopicPrepared')).toEqual({
      labelKey: 'notif.type.topicPrepared',
      tone: 'info',
      icon: 'checkCircle',
    });
  });

  it('falls back to a neutral default for an unknown category (never blank)', () => {
    const t = notifType('SomethingNew');
    expect(t.labelKey).toBe('notif.type.default');
    expect(t.tone).toBe('neutral');
    expect(t.icon).toBe('bell');
  });
});

describe('notifKey', () => {
  it('derives the runtime key from the deep-link last segment', () => {
    expect(notifKey('/meetings/MTG-2026-001')).toBe('MTG-2026-001');
    expect(notifKey('/topics/TOP-2026-014')).toBe('TOP-2026-014');
  });

  it('strips query and hash and trailing slashes', () => {
    expect(notifKey('/decisions/DECN-2026-003?tab=votes')).toBe('DECN-2026-003');
    expect(notifKey('/meetings/MTG-2026-001#agenda')).toBe('MTG-2026-001');
    expect(notifKey('/meetings/MTG-2026-001/')).toBe('MTG-2026-001');
  });

  it('resolves the record key from a nested deep-link (not the sub-segment)', () => {
    expect(notifKey('/meetings/MTG-2026-018/minutes')).toBe('MTG-2026-018');
    expect(notifKey('/topics/TOP-2026-014/decisions/DECN-2026-009')).toBe('TOP-2026-014');
  });

  it('falls back to the last segment when there is no canonical key shape', () => {
    expect(notifKey('/topics/42')).toBe('42');
  });

  it('returns null for a null or empty link (no key chip rendered)', () => {
    expect(notifKey(null)).toBeNull();
    expect(notifKey('')).toBeNull();
    expect(notifKey('/')).toBeNull();
  });
});
