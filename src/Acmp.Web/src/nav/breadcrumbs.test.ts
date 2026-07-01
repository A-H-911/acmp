import { describe, it, expect } from 'vitest';
import { deriveBreadcrumbs } from './breadcrumbs';
import i18n from '../i18n';

const t = i18n.t.bind(i18n);
/** Labels of the trail, in order. */
const labels = (p: string) => deriveBreadcrumbs(p, t).map((c) => c.label);

describe('deriveBreadcrumbs', () => {
  it('marks only the last crumb current, with its link stripped', () => {
    const crumbs = deriveBreadcrumbs('/backlog', t);
    expect(crumbs[0]).toMatchObject({ href: '/' }); // Home links onward
    const last = crumbs[crumbs.length - 1];
    expect(last.current).toBe(true);
    expect(last.href).toBeUndefined();
  });

  it('shows Home only on the dashboard (root and /dashboard alias)', () => {
    expect(labels('/')).toEqual([t('nav.home')]);
    expect(labels('/dashboard')).toEqual([t('nav.home')]);
    expect(deriveBreadcrumbs('/', t)[0].current).toBe(true);
  });

  it('builds Home › Area for a top-level area page', () => {
    expect(labels('/backlog')).toEqual([t('nav.home'), t('nav.backlog')]);
    expect(labels('/meetings')).toEqual([t('nav.home'), t('nav.agenda')]);
    expect(labels('/audit')).toEqual([t('nav.home'), t('nav.audit')]);
    expect(labels('/admin/users')).toEqual([t('nav.home'), t('nav.admin')]);
  });

  it('appends a named leaf for create routes', () => {
    expect(labels('/backlog/submit')).toEqual([t('nav.home'), t('nav.backlog'), t('topics.newTopic')]);
    expect(labels('/meetings/new')).toEqual([t('nav.home'), t('nav.agenda'), t('meetings.schedule.title')]);
  });

  it('shows the record key verbatim and mono-styled for detail routes', () => {
    const topic = deriveBreadcrumbs('/topics/TOP-2026-014', t);
    expect(topic.map((c) => c.label)).toEqual([t('nav.home'), t('nav.backlog'), 'TOP-2026-014']);
    expect(topic[2]).toMatchObject({ mono: true, current: true });

    const meeting = deriveBreadcrumbs('/meetings/MTG-2026-001', t);
    expect(meeting[2]).toMatchObject({ label: 'MTG-2026-001', mono: true });

    const decision = deriveBreadcrumbs('/decisions/DECN-2026-008', t);
    expect(decision.map((c) => c.label)).toEqual([t('nav.home'), t('nav.decisions'), 'DECN-2026-008']);
    expect(decision[2]).toMatchObject({ label: 'DECN-2026-008', mono: true, current: true });
  });

  it('handles the notifications inbox and unknown paths', () => {
    expect(labels('/notifications')).toEqual([t('nav.home'), t('notif.title')]);
    // Unknown path (the 404 catch-all) collapses to Home as the current page.
    expect(labels('/totally-unknown')).toEqual([t('nav.home')]);
  });
});
