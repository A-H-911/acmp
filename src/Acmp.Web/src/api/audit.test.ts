import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useAuditRegister, exportAuditLog, saveBlob } from './audit';
import { makeQueryWrapper, stubFetch } from '../test/queryHarness';

/* Real audit hook vs a stubbed fetch — assert read-only URL building (every filter + paging). */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 25, totalPages: 1 });

describe('useAuditRegister', () => {
  it('builds the query string from every filter + paging param', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(3) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useAuditRegister({ entityType: 'Vote', actor: 'kc-1', action: 'Vote.Closed', from: '2026-06-01T00:00:00Z', to: '2026-06-30T00:00:00Z', page: 2, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/audit?entityType=Vote&actor=kc-1&action=Vote.Closed&from=2026-06-01T00%3A00%3A00Z&to=2026-06-30T00%3A00%3A00Z&page=2&pageSize=25');
  });

  it('omits the query string entirely when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(0) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAuditRegister({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/audit');
  });
});

/* WBS-24.6 / FR-154 / AC-152 — the export. */
describe('exportAuditLog', () => {
  it('forwards every filter and the format, and drops paging', async () => {
    const spy = stubFetch(() => ({ blobBody: new Blob(['a,b\n']) }));
    await exportAuditLog(
      { entityType: 'Vote', actor: 'kc-1', action: 'Vote.Closed', from: '2026-06-01T00:00:00Z', page: 7, pageSize: 25 },
      'csv',
    );
    const url = urlOf(spy);
    expect(url).toContain('/api/audit/export?');
    expect(url).toContain('entityType=Vote');
    expect(url).toContain('actor=kc-1');
    expect(url).toContain('format=csv');
    // Paging is NOT a filter: an export is the whole matching set, so forwarding page/pageSize would
    // silently export one page of it — the truncation the server-side test also guards against.
    expect(url).not.toContain('page=');
    expect(url).not.toContain('pageSize=');
  });

  it('builds a valid URL when no filter is set at all', async () => {
    const spy = stubFetch(() => ({ blobBody: new Blob([]) }));
    await exportAuditLog({}, 'json');
    // The '?' must come from THIS call, not from a filter — with no filters, toQuery returns ''.
    expect(urlOf(spy)).toBe('/api/audit/export?format=json');
  });

  it('names the file by format', async () => {
    stubFetch(() => ({ blobBody: new Blob([]) }));
    expect((await exportAuditLog({}, 'json')).filename).toMatch(/^acmp-audit-\d{8}\d{6}\.json$/);
  });

  it('propagates a refusal instead of resolving with an empty file', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    await expect(exportAuditLog({}, 'csv')).rejects.toMatchObject({ status: 403 });
  });
});

describe('saveBlob', () => {
  it('hands the browser an object URL and revokes it', async () => {
    // Replace the GLOBALS and assert the OBSERVABLE — createObjectURL/revokeObjectURL are absent in
    // jsdom, so a spy on them would be version-dependent (the trap CI caught in WBS-24.4).
    const created: Blob[] = [];
    const revoked: string[] = [];
    vi.stubGlobal('URL', Object.assign(Object.create(URL), {
      createObjectURL: (b: Blob) => { created.push(b); return 'blob:acmp/1'; },
      revokeObjectURL: (u: string) => { revoked.push(u); },
    }));
    const clicked: HTMLAnchorElement[] = [];
    const realCreate = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      const el = realCreate(tag) as HTMLElement;
      if (tag === 'a') {
        (el as HTMLAnchorElement).click = () => { clicked.push(el as HTMLAnchorElement); };
      }
      return el;
    });

    const blob = new Blob(['x']);
    saveBlob(blob, 'acmp-audit-1.csv');

    expect(created).toEqual([blob]);
    expect(clicked).toHaveLength(1);
    expect(clicked[0].download).toBe('acmp-audit-1.csv');
    expect(clicked[0].href).toContain('blob:acmp/1');
    // Revocation is deferred a tick on purpose; assert it actually happens rather than assuming.
    await new Promise((r) => setTimeout(r, 0));
    expect(revoked).toEqual(['blob:acmp/1']);
    vi.restoreAllMocks();
  });
});
