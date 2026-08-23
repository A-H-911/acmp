import { useTranslation } from 'react-i18next';
import { Icon } from '../icons';

/*
 * NFR-063 / DW-061 — the "not optimized for mobile" notice.
 *
 * The requirement has two clauses. The no-broken-layout half was already met: real
 * breakpoints exist at 900, 860, 760, 720 and 560px across the feature stylesheets, so
 * narrow viewports stack rather than shatter. The notice half did not exist at all — a
 * scan of en.json's 2219 key-value pairs found no 'not optimi', 'mobile', 'small screen'
 * or 'desktop' string anywhere, and nothing rendered such a banner. A notice that must be
 * shown to a user cannot exist without a translatable string.
 *
 * WHY THIS IS CSS-ONLY AND NOT A matchMedia HOOK. The 768px figure IS the requirement's
 * threshold, so it lives in exactly one place — the media query in global.css. A hook
 * would put the same number in a second place and invite the two to drift, and would add
 * resize state and a listener to clean up for a strip of static text. Above the
 * breakpoint the element is `display: none`, which also removes it from the accessibility
 * tree, so desktop screen-reader users are not told about a constraint that does not
 * apply to them.
 *
 * ⚠ WHAT A UNIT TEST CAN AND CANNOT PROVE HERE. jsdom applies no media queries and has no
 * layout engine, so the test beside this file can only assert that the notice renders with
 * the right localized text. The property the requirement actually states — present below
 * 768px, absent above it — is asserted in e2e/mobile-notice.spec.ts against a real browser
 * at two viewport sizes, in both locales. Treating the jsdom test as proof of the
 * breakpoint would be a hollow pass.
 *
 * Mounted at the app root rather than inside AppShell on purpose: the shell wraps only
 * authenticated routes, and /login is the first thing a phone user reaches.
 */
export function MobileNotice() {
  const { t } = useTranslation();
  return (
    <div className="mobile-notice">
      <Icon name="infoCircle" size={15} />
      <span>{t('common.mobileNotice')}</span>
    </div>
  );
}
