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

  it('shows the designed Phase-2 state instead of the generic coming-soon when phase2', () => {
    render(<PlaceholderPage titleKey="nav.diagrams" phase2 />);
    expect(screen.getByRole('heading', { name: i18n.t('nav.diagrams') })).toBeInTheDocument();
    expect(screen.getByText(i18n.t('common.phase2Lead'))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('common.phase2Title'))).toBeInTheDocument();
    expect(screen.queryByText(i18n.t('common.comingSoon'))).not.toBeInTheDocument();
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
