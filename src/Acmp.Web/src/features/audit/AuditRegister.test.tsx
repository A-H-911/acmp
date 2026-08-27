import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import { AuditRegister } from './AuditRegister';
import type { AuditEvent } from '../../api/audit';

vi.mock('../../api/audit', async (orig) => ({
  ...(await orig<typeof import('../../api/audit')>()),
  useAuditRegister: vi.fn(),
  // WBS-24.6: the export's own unit coverage lives in api/audit.test.ts. Here the module boundary is
  // mocked so these tests assert what the COMPONENT is responsible for — which format it asks for,
  // that it passes the live filter, and that a refusal is shown rather than swallowed.
  exportAuditLog: vi.fn(),
  saveBlob: vi.fn(),
}));
import { useAuditRegister, exportAuditLog, saveBlob } from '../../api/audit';

const mockList = useAuditRegister as unknown as Mock;
const mockExport = exportAuditLog as unknown as Mock;
const mockSave = saveBlob as unknown as Mock;

// One enriched v2 row + one lean v1 row (system, enriched fields null) — proves the register
// renders both shapes: normalized action verb, actor fallback, artifact "—", and outcome vs "—".
const ROWS: AuditEvent[] = [
  {
    sequence: 2, occurredAt: '2026-06-24T14:22:07Z', hashVersion: 2, action: 'Vote.Closed',
    subjectType: 'Vote', subjectId: '1a2b3c4d-0000-0000-0000-000000000001', actor: 'kc-chair',
    actorName: 'Sara Khalid', actorRole: 'Chairman', outcome: 'Success', beforeJson: null,
    afterJson: '{"status":"Closed"}', correlationId: 'trace-1',
  },
  {
    sequence: 1, occurredAt: '2026-06-24T14:21:55Z', hashVersion: 1, action: 'Authentication.NoRoleClaim',
    subjectType: null, subjectId: null, actor: null, actorName: null, actorRole: null, outcome: null,
    beforeJson: null, afterJson: null, correlationId: null,
  },
];

// A subject the directory could not resolve — a system/integration actor with no member row. The UI must
// fall back to the raw id rather than render a blank actor: the audit log may never hide what it recorded.
const UNRESOLVED: AuditEvent = {
  sequence: 3, occurredAt: '2026-06-24T14:23:00Z', hashVersion: 2, action: 'Integration.WebexPosted',
  subjectType: 'Meeting', subjectId: 'aaaa1111-0000-0000-0000-000000000009', actor: 'kc-ghost',
  actorName: null, actorRole: 'Auditor,Administrator', outcome: 'Success',
  beforeJson: null, afterJson: null, correlationId: null,
};

function listResult(over: Partial<ReturnType<typeof useAuditRegister>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useAuditRegister>;
}
function withRows(items: AuditEvent[] = ROWS, total = items.length, totalPages = 1) {
  mockList.mockReturnValue(listResult({ data: { items, total, page: 1, pageSize: 25, totalPages } }));
}
function setup() {
  return render(<MemoryRouter><AuditRegister /></MemoryRouter>);
}
function lastParams() {
  return mockList.mock.calls[mockList.mock.calls.length - 1][0];
}

describe('AuditRegister (PR4)', () => {
  beforeEach(() => {
    mockList.mockReset();
    mockExport.mockReset();
    mockSave.mockReset();
    mockExport.mockResolvedValue({ blob: new Blob(['x']), filename: 'acmp-audit-20260827.csv' });
    withRows();
  });

  it('shows the loading skeleton while fetching', () => {
    mockList.mockReturnValue(listResult({ isLoading: true }));
    setup();
    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows a retryable error state on failure', async () => {
    const refetch = vi.fn();
    mockList.mockReturnValue(listResult({ isError: true, refetch }));
    setup();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('shows the header event count and read-only markers', () => {
    setup();
    expect(screen.getByText('2 events')).toBeInTheDocument();
    // Read-only appears both in the header and the card footer banner.
    expect(screen.getAllByText(/Read-only/).length).toBeGreaterThanOrEqual(2);
  });

  it('renders both an enriched (v2) and a lean (v1) row, normalized', () => {
    setup();
    // v2 row: timestamp, actor sub + role, action verb chip, artifact type + short id, outcome.
    expect(screen.getByText('Vote.Closed')).toBeInTheDocument();
    // The actor column now shows the resolved person; the subject stays on the row's title attribute.
    expect(screen.getByText('Sara Khalid')).toBeInTheDocument();
    expect(screen.getByText('Chairman')).toBeInTheDocument();
    expect(screen.getByText('Vote')).toBeInTheDocument();
    expect(screen.getByText('1a2b3c4d')).toBeInTheDocument(); // subjectId truncated to 8
    expect(screen.getByText('Success')).toBeInTheDocument();
    // v1 lean row: normalized action, "System" actor, artifact + detail em dash.
    expect(screen.getByText('Authentication.NoRoleClaim')).toBeInTheDocument();
    expect(screen.getByText('System')).toBeInTheDocument();
    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument();
  });

  // D-D. The column rendered the bare Keycloak subject as the actor's name AND derived the avatar
  // initials from it, so a reviewer saw a 36-character GUID where a person should be. An audit log a
  // human cannot read is not an audit control.
  it('renders the resolved person, not the raw Keycloak subject', () => {
    withRows();
    setup();

    expect(screen.getByText('Sara Khalid')).toBeInTheDocument();
    expect(screen.queryByText('kc-chair')).not.toBeInTheDocument();
  });

  // The subject stays reachable: display names are neither unique nor stable, so the forensic identity
  // must remain on the row even once a friendly name is shown.
  it('keeps the subject available for forensics even when a name is shown', () => {
    withRows();
    const { container } = setup();

    expect(container.querySelector('[title="kc-chair"]')).not.toBeNull();
  });

  it('falls back to the subject when the directory cannot resolve it', () => {
    withRows([UNRESOLVED]);
    setup();

    expect(screen.getByText('kc-ghost')).toBeInTheDocument();
  });

  // actorRole arrives as the raw claim list ("Auditor,Administrator"). Rendered verbatim it left
  // untranslated English in an Arabic UI, and no gate would notice: check-i18n compares KEYS only, and
  // this string is DATA, not a key.
  it('localizes the raw comma-separated role claims', () => {
    withRows([UNRESOLVED]);
    setup();

    expect(screen.getByText('Auditor · Administrator')).toBeInTheDocument();
    expect(screen.queryByText('Auditor,Administrator')).not.toBeInTheDocument();
  });

  it('does not render clickable row links (append-only, no drill-in)', () => {
    setup();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('filters by artifact type via the server params', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Artifact type' }));
    await user.click(screen.getByText('Decision')); // unique — not a type shown in the mocked rows
    expect(lastParams().entityType).toBe('Decision');
  });

  it('renders an empty state when no events match', () => {
    withRows([], 0);
    setup();
    expect(screen.getByText('No audit events')).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });

  // ---- WBS-24.6 / FR-154 / AC-152 — the "Export log" button ----

  it('renders the design reference\'s Export log button', () => {
    setup();
    expect(screen.getByRole('button', { name: /Export log/i })).toBeInTheDocument();
  });

  it.each(['csv', 'json'] as const)('exports as %s from the menu', async (format) => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Export log/i }));
    // The label, not the format token: asserting on the visible menu item is what proves the popover
    // actually opened. A token match would pass against a hidden panel.
    await user.click(screen.getByRole('menuitem', { name: format === 'csv' ? /CSV/i : /JSON/i }));

    expect(mockExport).toHaveBeenCalledTimes(1);
    expect(mockExport.mock.calls[0][1]).toBe(format);
    expect(mockSave).toHaveBeenCalledWith(expect.any(Blob), 'acmp-audit-20260827.csv');
  });

  // The whole reason the button lives on this screen: the file must be the set the reviewer is looking
  // at. If the export ever stopped forwarding the filter, the screen and the file would disagree and
  // nothing else in the suite would notice.
  it('exports the filter the register is currently showing, not the whole log', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Artifact type/i }));
    // FilterChip's options are menuitemradio, and the name is anchored: a bare /Vote/ substring would
    // also match "Vote.Closed"-style labels if the entity list ever grows.
    await user.click(await screen.findByRole('menuitemradio', { name: /^Vote$/i }));

    await user.click(screen.getByRole('button', { name: /Export log/i }));
    await user.click(screen.getByRole('menuitem', { name: /CSV/i }));

    expect(mockExport.mock.calls[0][0]).toEqual({ entityType: 'Vote' });
  });

  it('shows a refusal instead of failing silently', async () => {
    mockExport.mockRejectedValue(new Error('403'));
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Export log/i }));
    await user.click(screen.getByRole('menuitem', { name: /CSV/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/Export failed/i);
    expect(mockSave).not.toHaveBeenCalled();
  });
});
