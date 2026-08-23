import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import axe from 'axe-core';
import PlaceholderPage from './PlaceholderPage';
import i18n from '../i18n';

// The placeholder keeps later-phase nav areas navigable now (shell/routing/role gating work
// without the feature built). It must render the area's localized title + the coming-soon lead.
describe('PlaceholderPage', () => {
  it('renders the localized area title and the coming-soon lead + empty state', () => {
    render(<PlaceholderPage titleKey="nav.decisions" />);
    expect(screen.getByRole('heading', { name: i18n.t('nav.decisions') })).toBeInTheDocument();
    expect(screen.getByText(i18n.t('common.comingSoon'))).toBeInTheDocument();
    // EmptyState renders a status region
    expect(screen.getAllByRole('status').length).toBeGreaterThan(0);
  });

  // DEC-028 (2026-07-17) deferred P14 INDEFINITELY, so /diagrams must not advertise a phase. The
  // copy previously read "Coming in Phase 2" — a commitment the governance record had retracted.
  it('states the surface is not built rather than promising a phase, when deferred', () => {
    render(<PlaceholderPage titleKey="nav.diagrams" deferred />);
    expect(screen.getByRole('heading', { name: i18n.t('nav.diagrams') })).toBeInTheDocument();
    expect(screen.getByText(i18n.t('common.deferredLead'))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('common.deferredTitle'))).toBeInTheDocument();
    expect(screen.queryByText(i18n.t('common.comingSoon'))).not.toBeInTheDocument();
  });

  // The regression this fix exists to prevent: no user-visible string may promise a phase the
  // roadmap has not committed to. Asserted on the rendered text in BOTH locales, because the copy
  // is per-locale and check-i18n compares keys only — it would never notice an Arabic-only promise.
  it.each(['en', 'ar'])('never promises a phase on the deferred surface (%s)', async (lng) => {
    await i18n.changeLanguage(lng);
    const { container } = render(<PlaceholderPage titleKey="nav.diagrams" deferred />);
    expect(container.textContent).not.toMatch(/Phase\s*2|المرحلة\s*2/i);
    await i18n.changeLanguage('en');
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    const { container } = render(<PlaceholderPage titleKey="nav.risks" />);
    const results = await axe.run(container, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations).toEqual([]);
  });
});
