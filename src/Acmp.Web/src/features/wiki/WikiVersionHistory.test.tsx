import { describe, it, expect, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WikiVersionHistory } from './WikiVersionHistory';
import { renderWithAuth } from '../../test/render';
import type { DocumentDetail, DocumentVersion } from '../../api/wiki';

vi.mock('../../api/members', () => ({
  useMembers: () => ({ data: [{ keycloakUserId: 'kc-1', fullName: 'Khalid Ahmed' }] }),
}));

const versions: DocumentVersion[] = [
  { id: 'v1', version: 1, title: { en: 'T', ar: 'T' }, body: { en: 'First cut', ar: 'أول' }, savedAt: '2026-06-01T09:00:00Z', savedByUserId: 'kc-1' },
  { id: 'v2', version: 2, title: { en: 'T', ar: 'T' }, body: { en: 'Second revision', ar: 'ثاني' }, savedAt: '2026-06-10T09:00:00Z', savedByUserId: 'kc-unknown' },
];

const DOC: DocumentDetail = {
  id: 'd1', key: 'DOC-2026-001', title: { en: 'T', ar: 'T' }, body: { en: 'x', ar: 'x' },
  status: 'Published', category: 'Governance', tags: [], ownerUserId: 'kc-1', version: 2, versions,
  createdAt: '2026-06-01T09:00:00Z', updatedAt: '2026-06-10T09:00:00Z',
};

function setup(over: Partial<DocumentDetail> = {}) {
  const onClose = vi.fn();
  renderWithAuth(<WikiVersionHistory open onClose={onClose} document={{ ...DOC, ...over }} />, { roles: ['chairman'] });
  return { onClose };
}

describe('WikiVersionHistory (P15e)', () => {
  it('lists versions newest-first with resolved savers', () => {
    setup();
    const rows = screen.getAllByRole('button', { name: /^v/ });
    expect(within(rows[0]).getByText('v2')).toBeInTheDocument();
    expect(within(rows[1]).getByText('v1')).toBeInTheDocument();
    expect(screen.getByText(/Khalid Ahmed/)).toBeInTheDocument();
    expect(screen.getByText(/kc-unknown/)).toBeInTheDocument(); // fallback when no member matches
  });

  it('renders a snapshot body when a version is selected, and clears it on re-click', async () => {
    const user = userEvent.setup();
    setup();
    const v2 = screen.getByRole('button', { name: /v2/ });
    await user.click(v2);
    expect(screen.getByText('Second revision')).toBeInTheDocument();
    await user.click(v2);
    expect(screen.queryByText('Second revision')).not.toBeInTheDocument();
  });

  it('shows an empty message when there are no earlier versions', () => {
    setup({ versions: [] });
    expect(screen.getByText('No earlier versions.')).toBeInTheDocument();
  });

  it('closes via the Close button', async () => {
    const user = userEvent.setup();
    const { onClose } = setup();
    await user.click(screen.getByRole('button', { name: 'Close' }));
    expect(onClose).toHaveBeenCalled();
  });
});

/**
 * FR-117's third clause (DW-039 / WBS-24.3). The requirement reads "versioned … viewable AND
 * diffable"; versioning and viewing shipped in P15d/P15e and the diff did not. The source comment
 * that stood in for a record — "Diff is deferred to P14 — viewable satisfies FR-117" — is what
 * DW-039 was filed about, and these tests are the evidence that replaces it.
 */
describe('WikiVersionHistory — version compare (FR-117 diff clause)', () => {
  const selectV2 = async (user: ReturnType<typeof userEvent.setup>) => {
    await user.click(screen.getByRole('button', { name: /^v2/ }));
  };

  /**
   * Multi-line bodies, local to these tests. The shared fixture is deliberately left alone: it is
   * single-line, a pre-existing test asserts its exact text, and markdown folds a lone newline into
   * one paragraph — so widening it there broke that test rather than these.
   */
  const multiline = [
    { ...versions[0], body: { en: 'First cut\nshared line', ar: 'أول' } },
    { ...versions[1], body: { en: 'Second revision\nshared line', ar: 'ثاني' } },
  ];
  const setupDiff = (over: Partial<DocumentDetail> = {}) => setup({ versions: multiline, ...over });

  it('offers a compare control naming the version it compares against', async () => {
    const user = userEvent.setup();
    setupDiff();
    await selectV2(user);
    expect(screen.getByRole('button', { name: /compare with v1/i })).toBeEnabled();
  });

  it('shows the snapshot first and only diffs when compare is chosen', async () => {
    const user = userEvent.setup();
    setupDiff();
    await selectV2(user);
    expect(screen.queryByRole('list', { name: /changes since/i })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /compare with v1/i }));
    expect(screen.getByRole('list', { name: /changes since/i })).toBeInTheDocument();
  });

  it('marks the changed line as removed-then-added and leaves the shared line unchanged', async () => {
    const user = userEvent.setup();
    setupDiff();
    await selectV2(user);
    await user.click(screen.getByRole('button', { name: /compare with v1/i }));

    const items = screen.getAllByRole('listitem');
    const cls = items.map((li) => li.className.replace('wiki-diff-line ', ''));
    expect(cls).toEqual(['wiki-diff-removed', 'wiki-diff-added', 'wiki-diff-same']);
    expect(items[2]).toHaveTextContent('shared line');
  });

  it('summarises the change with counts', async () => {
    const user = userEvent.setup();
    setupDiff();
    await selectV2(user);
    await user.click(screen.getByRole('button', { name: /compare with v1/i }));
    expect(screen.getByText('+1')).toBeInTheDocument();
    expect(screen.getByText('\u22121')).toBeInTheDocument();
  });

  it('disables compare on the oldest version rather than offering a broken control', async () => {
    const user = userEvent.setup();
    setupDiff();
    await user.click(screen.getByRole('button', { name: /^v1/ }));
    const compare = screen.getByRole('button', { name: /^compare$/i });
    expect(compare).toBeDisabled();
    expect(compare).toHaveAttribute('title', expect.stringMatching(/first version/i));
  });

  it('says so when two versions are identical instead of rendering an empty diff', async () => {
    const user = userEvent.setup();
    const same = { en: 'identical body', ar: 'نفس' };
    setupDiff({ versions: [
      { ...versions[0], body: same },
      { ...versions[1], body: same },
    ] });
    await selectV2(user);
    await user.click(screen.getByRole('button', { name: /compare with v1/i }));
    expect(screen.getByText(/no lines changed/i)).toBeInTheDocument();
  });

  it('returns to the snapshot view when View is chosen again', async () => {
    const user = userEvent.setup();
    setupDiff();
    await selectV2(user);
    await user.click(screen.getByRole('button', { name: /compare with v1/i }));
    await user.click(screen.getByRole('button', { name: /^view$/i }));
    expect(screen.queryByRole('list', { name: /changes since/i })).not.toBeInTheDocument();
  });

  it('does NOT carry compare mode over to a newly selected version', async () => {
    // Selecting a different snapshot while comparing would otherwise show a diff the user never
    // asked for, against a predecessor they did not choose.
    const user = userEvent.setup();
    setupDiff();
    await selectV2(user);
    await user.click(screen.getByRole('button', { name: /compare with v1/i }));
    await user.click(screen.getByRole('button', { name: /^v1/ }));
    expect(screen.queryByRole('list', { name: /changes since/i })).not.toBeInTheDocument();
  });
});
