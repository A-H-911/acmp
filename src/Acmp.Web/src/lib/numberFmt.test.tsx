/*
 * The JSX half of NFR-037. `formatNumber` is unit-tested next door; what these cover is that a
 * number written into JSX picks up the LIVE UI language rather than a captured one — the failure a
 * pure-function test cannot see, because the language is not one of its arguments.
 */
import { describe, it, expect, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../i18n';
import { Num, Bytes } from './numberFmt';

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

  it('renders Arabic-Indic digits in Arabic', async () => {
    await i18n.changeLanguage('ar');
    render(<Num value={12345} />);
    expect(screen.getByText('١٢٬٣٤٥')).toBeInTheDocument();
  });

  it('passes Intl options straight through as props', async () => {
    await i18n.changeLanguage('en');
    render(<Num value={0.5} style="percent" />);
    expect(screen.getByText('50%')).toBeInTheDocument();
  });
});

describe('<Bytes>', () => {
  it('renders the numeral in the reader digits and leaves the unit symbol alone', async () => {
    await i18n.changeLanguage('ar');
    render(<Bytes value={1_572_864} />);
    expect(screen.getByText('١٫٥ MB')).toBeInTheDocument();
  });
});
