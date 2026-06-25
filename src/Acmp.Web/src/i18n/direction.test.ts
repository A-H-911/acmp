import { describe, it, expect } from 'vitest';
import { applyDirection } from './index';

// AC-040 (logic portion): switching locale drives document dir + lang.
// Visual mirroring is verified by render/axe (see acceptance audit).
describe('applyDirection', () => {
  it('sets dir=rtl and lang=ar for Arabic', () => {
    applyDirection('ar');
    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(document.documentElement.getAttribute('lang')).toBe('ar');
  });

  it('sets dir=ltr for English', () => {
    applyDirection('en');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    expect(document.documentElement.getAttribute('lang')).toBe('en');
  });
});
