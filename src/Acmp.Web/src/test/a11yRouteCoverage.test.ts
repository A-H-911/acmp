import { describe, it, expect } from 'vitest';
import type { RouteObject } from 'react-router-dom';
import { appRoutes } from '../App';
import { A11Y_EXCLUDED, A11Y_ROUTES } from './a11yRoutes';

/*
 * AC-170 (WBS-40.6, DEC-196): the accessibility sweep's route list is compared with the app's OWN route table.
 * DW-071's first trigger ("whenever a new route ships") depended on someone remembering to add the route to
 * e2e/rtl-a11y.spec.ts, and every route that shipped before the trigger was written sat outside it by
 * construction (DEF-126 found /meetings that way). This walks appRoutes - the tree the router actually runs -
 * so forgetting is a red test that names the route.
 */

/** Every full path the route tree declares: nested paths joined, an index route taking its parent's path,
 *  and a pathless layout route (ProtectedRoute, the RequireRole wrappers) passing its path through. */
export function declaredPaths(routes: readonly RouteObject[], parent = ''): string[] {
  return routes.flatMap((r) => {
    const own = r.index ? parent || '/' : r.path === undefined ? parent : join(parent, r.path);
    const here = r.index || r.path !== undefined ? [own] : [];
    return [...here, ...(r.children ? declaredPaths(r.children, own) : [])];
  });
}

function join(parent: string, path: string): string {
  if (path.startsWith('/')) return path;
  return `${parent === '/' ? '' : parent}/${path}`;
}

describe('AC-170: the accessibility sweep covers every route the app declares', () => {
  const declared = new Set(declaredPaths(appRoutes));
  const swept = new Set(A11Y_ROUTES.map((r) => r.pattern));
  const excluded = new Set(Object.keys(A11Y_EXCLUDED));

  it('reads a real route table (the check has a subject)', () => {
    expect(declared.size).toBeGreaterThan(40);
    expect(declared).toContain('/topics/:key');
    expect(declared).toContain('/meetings/:key/agenda');
  });

  it('every declared route is swept or excluded by name - an unswept route fails naming itself', () => {
    const unswept = [...declared].filter((p) => !swept.has(p) && !excluded.has(p)).sort();
    expect(unswept, 'routes the app declares that the accessibility sweep does not visit').toEqual([]);
  });

  it('every swept or excluded entry is a route the app still declares - the list cannot rot', () => {
    const stale = [...swept, ...excluded].filter((p) => !declared.has(p)).sort();
    expect(stale, 'manifest entries with no matching route').toEqual([]);
  });

  it('no route is both swept and excluded, and none is listed twice', () => {
    expect([...swept].filter((p) => excluded.has(p))).toEqual([]);
    expect(A11Y_ROUTES.length).toBe(swept.size);
  });

  it('walks nesting the way the router does', () => {
    const tree: RouteObject[] = [
      { path: '/x', children: [{ index: true }, { path: 'y', children: [{ path: 'z' }] }] },
      { children: [{ path: '/free' }, { path: 'rel' }] },
    ];
    expect(declaredPaths(tree)).toEqual(['/x', '/x', '/x/y', '/x/y/z', '/free', '/rel']);
  });
});
