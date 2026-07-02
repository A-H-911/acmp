/*
 * Breadcrumb derivation — one place, consumed by the shell layout so EVERY page
 * (dashboard, records, placeholders, 404) renders a consistent trail
 * (Design System §11 / Usage Map decision 12). Rule: Home › Area › Record › Sub-tab.
 *
 * Derived from the URL alone — the area from the first path segment, the record
 * crumb from the leaf segment (the human-readable key, e.g. TOP-2026-014). No page
 * needs to supply its own breadcrumb. Chevron mirroring + the 12px gap live on the
 * shared `.breadcrumb` style (LP-01), so they are owned globally, not per page.
 */
import type { TFunction } from 'i18next';
import { AREAS, type AreaKey } from './navModel';
import type { Crumb } from '../components/ui/Breadcrumb';

/** First path segment → the nav area it belongs to (detail routes fold to their area). */
const SEGMENT_AREA: Record<string, AreaKey> = {
  backlog: 'backlog',
  topics: 'backlog',
  meetings: 'agenda',
  decisions: 'decisions',
  votes: 'decisions',
  actions: 'actions',
  adrs: 'adrs',
  risks: 'risks',
  dependencies: 'deps',
  research: 'research',
  wiki: 'wiki',
  diagrams: 'diagrams',
  reports: 'reports',
  audit: 'audit',
  admin: 'admin',
  session: 'session',
};

/**
 * The record/sub crumb under an area, or null when the area itself is the page.
 * Record keys (topic/meeting) are shown verbatim and mono-styled, like the design.
 */
function leafCrumb(segs: readonly string[], t: TFunction): Crumb | null {
  const [a, b] = segs;
  if (a === 'backlog' && b === 'submit') return { label: t('topics.newTopic') };
  if (a === 'topics' && b) return { label: b, mono: true };
  if (a === 'meetings' && b === 'new') return { label: t('meetings.schedule.title') };
  if (a === 'meetings' && b) return { label: b, mono: true };
  if (a === 'decisions' && b) return { label: b, mono: true };
  if (a === 'votes' && b) return { label: b, mono: true };
  if (a === 'risks' && b) return { label: b, mono: true };
  return null;
}

/** Build the breadcrumb trail for a pathname. The last crumb is always the current page. */
export function deriveBreadcrumbs(pathname: string, t: TFunction): Crumb[] {
  const segs = pathname.split('/').filter(Boolean);
  const crumbs: Crumb[] = [{ label: t('nav.home'), href: AREAS.home.path }];
  const seg0 = segs[0];

  if (seg0 === 'notifications') {
    crumbs.push({ label: t('notif.title') });
  } else if (seg0 && seg0 !== 'dashboard') {
    const areaKey = SEGMENT_AREA[seg0];
    if (areaKey) {
      const area = AREAS[areaKey];
      const areaCrumb: Crumb = { label: t(area.labelKey), href: area.path };
      const leaf = leafCrumb(segs, t);
      crumbs.push(areaCrumb);
      if (leaf) crumbs.push(leaf);
    }
    // Unknown path (e.g. the 404 catch-all) → Home is the only crumb.
  }

  const last = crumbs[crumbs.length - 1];
  last.current = true;
  last.href = undefined;
  return crumbs;
}
