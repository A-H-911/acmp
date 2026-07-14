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
