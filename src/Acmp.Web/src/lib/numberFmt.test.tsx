/*
 * The JSX half of NFR-037. `formatNumber` is unit-tested next door; what these cover is that a
 * number written into JSX picks up the LIVE UI language rather than a captured one — the failure a
 * pure-function test cannot see, because the language is not one of its arguments.
 */
import { describe, it, expect, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../i18n';
import { Num, Bytes, Pct } from './numberFmt';

const original = i18n.language;
afterEach(async () => {
  await i18n.changeLanguage(original);
});

describe('<Num>', () => {
  it('renders grouped Latin digits in English', async () => {
    await i18n.changeLanguage('en');
    render(<Num value={12345} />);
    expect(screen.getByText('12,345')).toBeInTheDocument();
  });

  it('renders Latin digits in Arabic too (AC-167)', async () => {
    await i18n.changeLanguage('ar');
    render(<Num value={12345} />);
    expect(screen.getByText('12,345')).toBeInTheDocument();
  });

  it('passes Intl options straight through as props', async () => {
    await i18n.changeLanguage('en');
    render(<Num value={0.5} style="percent" />);
    expect(screen.getByText('50%')).toBeInTheDocument();
  });
});

describe('<Pct>', () => {
  it('renders Latin digits and the % sign in Arabic (AC-167)', async () => {
    await i18n.changeLanguage('ar');
    const { container } = render(<Pct value={87} />);
    // Matched on CONTENT: Intl wraps the sign in invisible bidi marks (U+200E) on purpose.
    expect(container.textContent).toContain('87');
    expect(container.textContent).toContain('%');
    expect(container.textContent).not.toMatch(/[٠-٩٪]/);
  });

  it('renders the ASCII sign in English', async () => {
    await i18n.changeLanguage('en');
    const { container } = render(<Pct value={87} />);
    expect(container.textContent).toBe('87%');
  });
});

describe('<Bytes>', () => {
  it('renders Latin digits with the Arabic unit in Arabic (AC-167, AC-168)', async () => {
    await i18n.changeLanguage('ar');
    render(<Bytes value={1_572_864} />);
    expect(screen.getByText('1.5 م.ب')).toBeInTheDocument();
  });
});
