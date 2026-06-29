import { describe, it, expect } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { MeetingsCalendar } from './MeetingsCalendar';
import { renderWithAuth } from '../../test/render';
import type { MeetingSummary } from '../../api/meetings';

// A meeting dated mid-CURRENT-month, so it always lands in the calendar's default (today's) month
// regardless of when the suite runs.
const now = new Date();
const thisMonth = new Date(now.getFullYear(), now.getMonth(), 15, 10, 0, 0);

const base: MeetingSummary = {
  id: 'm1', key: 'MTG-2026-019', title: 'Q2 Architecture Review', scheduledStart: thisMonth.toISOString(),
  scheduledEnd: thisMonth.toISOString(), status: 'Scheduled', type: 'Regular', mode: 'InPerson',
  chairName: 'Sara K', itemCount: 4, agendaStatus: 'Draft',
};

describe('MeetingsCalendar (PR-B)', () => {
  it('renders seven weekday headers', () => {
    renderWithAuth(<MeetingsCalendar meetings={[]} />, { roles: ['secretary'] });
    const grid = screen.getByText('Sun').parentElement!; // the .mt-cal-grid
    for (const d of ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']) {
      expect(within(grid).getByText(d)).toBeInTheDocument();
    }
  });

  it('places a meeting in the current month as a link to its detail', () => {
    renderWithAuth(<MeetingsCalendar meetings={[base]} />, { roles: ['secretary'] });
    expect(screen.getByRole('link', { name: /Q2 Architecture Review/ })).toHaveAttribute('href', '/meetings/MTG-2026-019');
  });

  it('pages months: the current-month event clears when navigating away and returns', async () => {
    const user = userEvent.setup();
    renderWithAuth(<MeetingsCalendar meetings={[base]} />, { roles: ['secretary'] });
    expect(screen.getByRole('link', { name: /Q2 Architecture Review/ })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /next month/i }));
    expect(screen.queryByRole('link', { name: /Q2 Architecture Review/ })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /previous month/i }));
    expect(screen.getByRole('link', { name: /Q2 Architecture Review/ })).toBeInTheDocument();
  });

  it('tones the event pill by meeting status', () => {
    renderWithAuth(<MeetingsCalendar meetings={[{ ...base, status: 'Held' }]} />, { roles: ['secretary'] });
    expect(screen.getByRole('link', { name: /Q2 Architecture Review/ })).toHaveClass('success');
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    renderWithAuth(<MeetingsCalendar meetings={[base]} />, { roles: ['secretary'] });
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
