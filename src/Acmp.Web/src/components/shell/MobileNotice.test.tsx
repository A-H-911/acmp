import { describe, it, expect, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../../i18n';
import { MobileNotice } from './MobileNotice';

/*
 * NFR-063 / DW-061.
 *
 * ⚠ READ THIS BEFORE ADDING A "HIDES ON DESKTOP" CASE HERE. jsdom applies no media queries
 * and has no layout engine, so `display: none` is unobservable from this file: the element
 * is in the DOM at every viewport and `getByText` finds it either way. A test asserting the
 * breakpoint from jsdom would pass whether or not the media query existed — a hollow pass,
 * and worse than no test because it would read like coverage.
 *
 * The breakpoint is asserted where it can actually be observed: e2e/mobile-notice.spec.ts
 * drives a real browser at 375px and 1280px and checks visibility in both locales.
 *
 * What THIS file is for is the half a browser test would be a clumsy instrument for: that
 * the localized strings exist and resolve in both languages. If either key is deleted or
 * renamed, i18next falls back to echoing the key path and these assertions fail — which is
 * the failure mode that actually threatens this feature, since the notice is one string and
 * nothing else.
 */
describe('MobileNotice (NFR-063 — the not-optimized-for-mobile notice)', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('renders the English notice', () => {
    render(<MobileNotice />);
    expect(
      screen.getByText('ACMP is not optimized for small screens. Use a tablet or desktop for the full experience.'),
    ).toBeInTheDocument();
  });

  it('renders the Arabic notice when the language is Arabic', async () => {
    await i18n.changeLanguage('ar');
    render(<MobileNotice />);
    expect(
      screen.getByText('المنصة غير مُهيّأة للشاشات الصغيرة. استخدم جهازاً لوحياً أو حاسوباً للحصول على التجربة الكاملة.'),
    ).toBeInTheDocument();
  });

  it('resolves a real translation rather than echoing the key in either locale', async () => {
    // The failure this guards: a deleted or renamed key makes i18next render 'common.mobileNotice'
    // verbatim, which still renders "a notice" and would satisfy a laxer assertion.
    for (const lng of ['en', 'ar'] as const) {
      await i18n.changeLanguage(lng);
      expect(i18n.t('common.mobileNotice')).not.toBe('common.mobileNotice');
      expect(i18n.t('common.mobileNotice').length).toBeGreaterThan(20);
    }
  });

  it('marks the icon decorative so the notice is announced once, not twice', () => {
    const { container } = render(<MobileNotice />);
    const svg = container.querySelector('svg');
    expect(svg).toBeTruthy();
    expect(svg!.getAttribute('aria-hidden')).toBe('true');
  });
});
