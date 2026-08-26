import i18n, { type FormatterModule } from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import en from './locales/en.json';
import ar from './locales/ar.json';
import { formatNumber } from '../lib/numberFmt';

export const supportedLngs = ['en', 'ar'] as const;
export type AppLanguage = (typeof supportedLngs)[number];

// Drive document direction + lang from the active language (guardrail 9: RTL is first-class).
export function applyDirection(lng: string): void {
  const dir = lng === 'ar' ? 'rtl' : 'ltr';
  document.documentElement.setAttribute('dir', dir);
  document.documentElement.setAttribute('lang', lng);
}

/*
 * NFR-037's number half (DW-068). Every NUMBER handed to t() is localized HERE, once, rather than by
 * tagging ~60 placeholders across two locale files with `{{x, number}}` — a tag that is silently
 * absent from the sixty-first, and from every placeholder written after today.
 *
 * HOW IT FIRES, because the obvious wiring does not. `interpolation.format` looks like the hook and
 * is not: i18next overwrites it with its own Formatter during init, and that Formatter returns the
 * value untouched when no format is named (`if (!format) return value`). A formatter MODULE is the
 * documented slot, and `alwaysFormat` is what makes i18next call it for placeholders that name no
 * format at all. Both halves are load-bearing; removing either makes numbers silently Latin again.
 *
 * Keying off the RUNTIME TYPE is what makes it safe to apply everywhere: `12` is localized, while
 * `TOP-042`, an ADR id and a pre-formatted date are strings and pass through untouched.
 *
 * ⚠ i18next's built-in named formats (`number`, `datetime`, `relativetime`, `list`) are NOT
 * inherited — the class is not exported, so there is nothing to delegate to. No placeholder in
 * either locale file uses the `{{value, format}}` syntax today, which is why that costs nothing.
 * If you need one, register it with `i18n.services.formatter.add(name, fn)`; `add` still works.
 */
const numberFormatter: FormatterModule = {
  type: 'formatter',
  init: () => {},
  add(name, fc) {
    named[name.toLowerCase().trim()] = fc;
  },
  addCached(name, fc) {
    named[name.toLowerCase().trim()] = (value, lng, options) => fc(lng, options)(value);
  },
  format(value, format, lng) {
    if (!format) return typeof value === 'number' ? formatNumber(value, lng) : value;
    const fc = named[format.toLowerCase().trim()];
    return fc ? fc(value, lng, {}) : value;
  },
};
const named: Record<string, (value: unknown, lng: string | undefined, options: unknown) => string> = {};

i18n
  .use(LanguageDetector)
  .use(numberFormatter)
  .use(initReactI18next)
  .init({
    resources: { en: { translation: en }, ar: { translation: ar } },
    fallbackLng: 'en',
    supportedLngs: [...supportedLngs],
    interpolation: { escapeValue: false, alwaysFormat: true },
    detection: { order: ['localStorage', 'navigator'], caches: ['localStorage'] },
  });

i18n.on('languageChanged', applyDirection);
applyDirection(i18n.language || 'en');

export default i18n;
