/*
 * Navigation model — grouping, order, routes, and per-role visibility.
 * Mirrors the "ACMP Navigation & IA" design (GROUPS/ACCESS/AREA) and docs/domain/information-architecture.md
 * §2.1. A role that cannot access an area never sees its nav item (FR-024);
 * "view" access still shows the item (with a read-only marker).
 *
 * This is presentation gating only — the API enforces authorization (P4).
 */
import type { CommitteeRole } from '../auth/roles';
import type { IconName } from '../components/icons';

export type AccessLevel = 'full' | 'view' | 'none';
export type AreaKey =
  | 'session' | 'submit' | 'home' | 'backlog' | 'agenda' | 'decisions' | 'actions'
  | 'adrs' | 'risks' | 'deps' | 'research' | 'wiki' | 'diagrams'
  | 'reports' | 'audit' | 'admin';

export interface NavArea {
  key: AreaKey;
  /** i18n key for the label, e.g. nav.backlog. */
  labelKey: string;
  path: string;
  icon: IconName;
  cta?: boolean;
}

/** Canonical route per area (docs/domain/information-architecture.md §3 sitemap). */
export const AREAS: Record<AreaKey, NavArea> = {
  session: { key: 'session', labelKey: 'nav.session', path: '/session', icon: 'session', cta: true },
  submit: { key: 'submit', labelKey: 'nav.submit', path: '/backlog/submit', icon: 'plus', cta: true },
  home: { key: 'home', labelKey: 'nav.home', path: '/', icon: 'home' },
  backlog: { key: 'backlog', labelKey: 'nav.backlog', path: '/backlog', icon: 'backlog' },
  agenda: { key: 'agenda', labelKey: 'nav.agenda', path: '/meetings', icon: 'calendar' },
  decisions: { key: 'decisions', labelKey: 'nav.decisions', path: '/decisions', icon: 'decision' },
  actions: { key: 'actions', labelKey: 'nav.actions', path: '/actions', icon: 'action' },
  adrs: { key: 'adrs', labelKey: 'nav.adrs', path: '/adrs', icon: 'adr' },
  risks: { key: 'risks', labelKey: 'nav.risks', path: '/risks', icon: 'risk' },
  deps: { key: 'deps', labelKey: 'nav.deps', path: '/dependencies', icon: 'deps' },
  research: { key: 'research', labelKey: 'nav.research', path: '/research', icon: 'research' },
  wiki: { key: 'wiki', labelKey: 'nav.wiki', path: '/wiki', icon: 'wiki' },
  diagrams: { key: 'diagrams', labelKey: 'nav.diagrams', path: '/diagrams', icon: 'diagram' },
  reports: { key: 'reports', labelKey: 'nav.reports', path: '/reports', icon: 'reports' },
  audit: { key: 'audit', labelKey: 'nav.audit', path: '/audit', icon: 'audit' },
  admin: { key: 'admin', labelKey: 'nav.admin', path: '/admin/users', icon: 'admin' },
};

/** Access per area per role (design ACCESS map). Absent role ⇒ 'none'. */
const ACCESS: Record<AreaKey, Partial<Record<CommitteeRole, AccessLevel>>> = {
  home: { chairman: 'full', secretary: 'full', member: 'full', reviewer: 'full', auditor: 'full', administrator: 'full', submitter: 'full', guest: 'full' },
  submit: { secretary: 'full', member: 'full', submitter: 'full' },
  session: { guest: 'full' },
  backlog: { chairman: 'view', secretary: 'full', member: 'full', reviewer: 'view', auditor: 'view', submitter: 'view' },
  agenda: { chairman: 'full', secretary: 'full', member: 'view', reviewer: 'view', auditor: 'view', guest: 'view' },
  decisions: { chairman: 'full', secretary: 'full', member: 'view', reviewer: 'view', auditor: 'view' },
  actions: { chairman: 'view', secretary: 'full', member: 'full', reviewer: 'view', auditor: 'view', submitter: 'view' },
  adrs: { chairman: 'full', secretary: 'full', member: 'view', reviewer: 'full', auditor: 'view' },
  risks: { chairman: 'view', secretary: 'full', member: 'view', reviewer: 'view', auditor: 'view' },
  deps: { chairman: 'view', secretary: 'full', member: 'view', reviewer: 'view', auditor: 'view' },
  research: { chairman: 'view', secretary: 'full', member: 'full', reviewer: 'view', auditor: 'view' },
  wiki: { chairman: 'view', secretary: 'full', member: 'full', reviewer: 'view', auditor: 'view' },
  diagrams: { chairman: 'view', secretary: 'view', member: 'view', reviewer: 'view', auditor: 'view', guest: 'view' },
  reports: { chairman: 'full', secretary: 'full', member: 'view', reviewer: 'view', auditor: 'view', administrator: 'view' },
  audit: { auditor: 'full', chairman: 'view', secretary: 'view' },
  admin: { administrator: 'full' },
};

/** Sidebar grouping + order (design GROUPS). A null label = unlabeled CTA group. */
const GROUPS: { labelKey: string | null; items: AreaKey[] }[] = [
  { labelKey: null, items: ['session', 'submit'] },
  { labelKey: 'navGroup.committee', items: ['home', 'backlog', 'agenda', 'decisions', 'actions'] },
  { labelKey: 'navGroup.governance', items: ['adrs', 'risks', 'deps'] },
  { labelKey: 'navGroup.knowledge', items: ['research', 'wiki', 'diagrams'] },
  { labelKey: 'navGroup.insights', items: ['reports'] },
  { labelKey: 'navGroup.system', items: ['audit', 'admin'] },
];

const RANK: Record<AccessLevel, number> = { none: 0, view: 1, full: 2 };

/** Highest access an area grants across the user's roles. */
export function accessFor(area: AreaKey, roles: readonly CommitteeRole[]): AccessLevel {
  let best: AccessLevel = 'none';
  for (const role of roles) {
    const lvl = ACCESS[area][role] ?? 'none';
    if (RANK[lvl] > RANK[best]) best = lvl;
  }
  return best;
}

export interface NavItem extends NavArea { access: Exclude<AccessLevel, 'none'>; }
export interface NavGroup { labelKey: string | null; items: NavItem[]; }

/** Build the role-filtered sidebar: groups with at least one visible area. */
export function buildNav(roles: readonly CommitteeRole[]): NavGroup[] {
  return GROUPS.map((g) => ({
    labelKey: g.labelKey,
    items: g.items
      .map((key) => ({ area: AREAS[key], access: accessFor(key, roles) }))
      .filter((x): x is { area: NavArea; access: Exclude<AccessLevel, 'none'> } => x.access !== 'none')
      .map(({ area, access }) => ({ ...area, access })),
  })).filter((g) => g.items.length > 0);
}
