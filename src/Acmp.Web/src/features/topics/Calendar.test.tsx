import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from '../../i18n';
import { Calendar } from './Calendar';

/**
 * The calendar frame is deliberately an honest empty body (topics carry no
 * scheduled date until meeting scheduling lands), but its month NAVIGATION is real
 * and had never been exercised: both controls are inline `setOffset` handlers with
 * no test file at all. coverage-v8 v4 named lines 42 and 46 (DW-082).
 *
 * The assertions deliberately avoid recomputing the component's own
 * Intl.DateTimeFormat call. Reproducing the formatting here would mean the test
 * agrees with the implementation by construction and would still pass if both were
 * wrong. Instead they assert PROPERTIES: stepping changes the label, and stepping
 * back returns to exactly where it started.
 */
function setup() {
  return render(
    <I18nextProvider i18n={i18n}>
      <Calendar />
    </I18nextProvider>,
  );
}

function monthLabel(): string {
  // The month caption is the only .cal-month element in the frame.
  const el = document.querySelector('.cal-month');
  if (!el?.textContent) throw new Error('month caption not rendered - the frame changed shape');
  return el.textContent;
}

describe('Calendar month navigation', () => {
  it('steps forward a month from the next control', async () => {
    setup();
    const start = monthLabel();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('topics.calendar.next') }));
    expect(monthLabel()).not.toBe(start);
  });

  it('steps back a month from the previous control', async () => {
    setup();
    const start = monthLabel();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('topics.calendar.prev') }));
    expect(monthLabel()).not.toBe(start);
  });

  it('returns to the starting month when a step forward is undone', async () => {
    setup();
    const start = monthLabel();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('topics.calendar.next') }));
    await userEvent.click(screen.getByRole('button', { name: i18n.t('topics.calendar.prev') }));
    expect(monthLabel()).toBe(start);
  });
});
