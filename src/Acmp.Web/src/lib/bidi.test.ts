import { describe, it, expect } from 'vitest';
import { isolate } from './bidi';

describe('isolate', () => {
  it('wraps the text in FSI ... PDI', () => {
    expect(isolate('E2E Secretary')).toBe('⁨E2E Secretary⁩');
  });
});
