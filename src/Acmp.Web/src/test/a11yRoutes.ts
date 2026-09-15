/*
 * AC-170 (WBS-40.6, DEC-196): THE ROUTE MANIFEST THE ACCESSIBILITY SWEEP VISITS.
 *
 * One list, read by two instruments:
 *  - e2e/rtl-a11y.spec.ts sweeps every entry with axe, in English and Arabic, as the principal named here;
 *  - src/test/a11yRouteCoverage.test.ts compares this list with the app's own route table (App.tsx's
 *    appRoutes), so a route added to the app and not added here fails BY NAME - and an entry left here after
 *    its route is removed fails too, so the list cannot rot in either direction.
 *
 * A parameterised pattern (':key') is opened on a real seeded record by the spec, never swept as a shape.
 * Plain data only: no Playwright or React import, so both the app and the e2e type-check can read it.
 */

/** Who opens the route: the Secretary for the committee surface, the Administrator for /admin (RequireRole
 *  admits no one else), and nobody for the sign-in page. */
export type A11yPrincipal = 'secretary' | 'administrator' | 'signedOut';

export interface A11yRoute {
  readonly pattern: string;
  readonly as: A11yPrincipal;
}

export const A11Y_ROUTES: readonly A11yRoute[] = [
  { pattern: '/login', as: 'signedOut' },

  // Static routes (23 were swept before AC-170; /profile and /profile/preferences were not).
  { pattern: '/', as: 'secretary' },
  { pattern: '/notifications', as: 'secretary' },
  { pattern: '/profile', as: 'secretary' },
  { pattern: '/profile/preferences', as: 'secretary' },
  { pattern: '/session', as: 'secretary' },
  { pattern: '/session/preview', as: 'secretary' },
  { pattern: '/backlog', as: 'secretary' },
  { pattern: '/backlog/submit', as: 'secretary' },
  { pattern: '/meetings', as: 'secretary' },
  { pattern: '/meetings/new', as: 'secretary' },
  { pattern: '/decisions', as: 'secretary' },
  { pattern: '/actions', as: 'secretary' },
  { pattern: '/adrs', as: 'secretary' },
  { pattern: '/invariants', as: 'secretary' },
  { pattern: '/risks', as: 'secretary' },
  { pattern: '/dependencies', as: 'secretary' },
  { pattern: '/research', as: 'secretary' },
  { pattern: '/wiki', as: 'secretary' },
  { pattern: '/templates', as: 'secretary' },
  { pattern: '/diagrams', as: 'secretary' },
  { pattern: '/reports', as: 'secretary' },
  { pattern: '/search', as: 'secretary' },
  { pattern: '/members', as: 'secretary' },
  { pattern: '/audit', as: 'secretary' },
  { pattern: '/admin/users', as: 'administrator' },

  // Parameterised routes - each opened on a record the spec seeds first.
  { pattern: '/topics/:key', as: 'secretary' },
  { pattern: '/topics/:key/edit', as: 'secretary' },
  { pattern: '/meetings/:key', as: 'secretary' },
  { pattern: '/meetings/:key/agenda', as: 'secretary' },
  { pattern: '/meetings/:key/attendance', as: 'secretary' },
  { pattern: '/meetings/:key/notes', as: 'secretary' },
  { pattern: '/meetings/:key/minutes', as: 'secretary' },
  { pattern: '/meetings/:key/recording', as: 'secretary' },
  { pattern: '/decisions/:key', as: 'secretary' },
  { pattern: '/votes/:key', as: 'secretary' },
  { pattern: '/actions/:key', as: 'secretary' },
  { pattern: '/adrs/:key', as: 'secretary' },
  { pattern: '/invariants/:key', as: 'secretary' },
  { pattern: '/risks/:key', as: 'secretary' },
  { pattern: '/dependencies/:key', as: 'secretary' },
  { pattern: '/traceability/:type/:key', as: 'secretary' },
  { pattern: '/research/:key', as: 'secretary' },
  { pattern: '/wiki/:key', as: 'secretary' },
];

/** Route-table entries that are not screens, each with the reason it is not swept. */
export const A11Y_EXCLUDED: Readonly<Record<string, string>> = {
  '/auth/callback': 'the OIDC redirect handler: it exchanges the code and navigates away, it never rests as a page',
  '/dashboard': 'a legacy alias that is only <Navigate to="/"> - the destination "/" is swept',
  '/admin': 'only <Navigate to="/admin/users"> - the destination is swept',
  '/*': 'the not-found page for unknown paths (AC-170 excludes it by name)',
};
