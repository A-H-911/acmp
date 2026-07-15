import { describe, it, expect, beforeEach, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WikiReadingView } from './WikiReadingView';
import { renderWithAuth } from '../../test/render';
import type { DocumentDetail } from '../../api/wiki';

const fns = vi.hoisted(() => ({ publish: vi.fn().mockResolvedValue(undefined), archive: vi.fn().mockResolvedValue(undefined) }));
vi.mock('../../api/wiki', async (orig) => ({
  ...(await orig<typeof import('../../api/wiki')>()),
  usePublishDocument: () => ({ mutateAsync: fns.publish, isPending: false }),
  useArchiveDocument: () => ({ mutateAsync: fns.archive, isPending: false }),
}));
vi.mock('../../api/members', () => ({
  useMembers: () => ({ data: [{ keycloakUserId: 'kc-1', fullName: 'Khalid Ahmed' }] }),
}));
vi.mock('./WikiLinkedArtifacts', () => ({
  WikiLinkedArtifacts: (p: { documentId: string }) => (
    <aside data-testid="linked-artifacts" data-documentid={p.documentId} />
  ),
}));

const DOC: DocumentDetail = {
  id: 'd1', key: 'DOC-2026-001',
  title: { en: 'Committee governance model', ar: 'نموذج' },
  body: { en: 'The committee is the **system of record**.\n\nSecond paragraph with fifty words '.repeat(1) + Array.from({ length: 40 }, () => 'word').join(' '), ar: 'محتوى' },
  status: 'Published', category: 'Governance', tags: ['governance', 'process'],
  ownerUserId: 'kc-1', version: 8, versions: [],
  createdAt: '2026-06-01T09:00:00Z', updatedAt: '2026-06-14T09:00:00Z',
};

function setup(over: Partial<DocumentDetail> = {}, canManage = true, roles: ('chairman' | 'member')[] = ['chairman']) {
  const onEdit = vi.fn();
  const onHistory = vi.fn();
  renderWithAuth(<WikiReadingView document={{ ...DOC, ...over }} canManage={canManage} onEdit={onEdit} onHistory={onHistory} />, { roles });
  return { onEdit, onHistory };
}

describe('WikiReadingView (P15e)', () => {
  beforeEach(() => { fns.publish.mockClear(); fns.archive.mockClear(); });

  it('renders the title, resolved author, tags, read-time and body', () => {
    setup();
    expect(screen.getByRole('heading', { name: 'Committee governance model' })).toBeInTheDocument();
    expect(screen.getByText('Khalid Ahmed')).toBeInTheDocument();
    expect(screen.getByText('governance')).toBeInTheDocument();
    expect(screen.getByText(/min read/)).toBeInTheDocument();
    expect(screen.getByText('system of record')).toBeInTheDocument(); // bold markdown rendered
    expect(screen.getByText('Governance')).toBeInTheDocument(); // breadcrumb category
  });

  it('falls back to the raw owner id when no member matches', () => {
    setup({ ownerUserId: 'kc-unknown' });
    expect(screen.getByText('kc-unknown')).toBeInTheDocument();
  });

  it('shows History to a non-manager but hides the lifecycle actions (m18)', () => {
    setup({}, false, ['member']);
    // History is a read affordance — visible to every reader.
    expect(screen.getByRole('button', { name: /History/ })).toBeInTheDocument();
    // Edit + the status badge stay manager-only.
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.queryByText('Published')).not.toBeInTheDocument();
  });

  it('Draft + manager: offers Publish, Archive, Edit and History', () => {
    setup({ status: 'Draft' });
    expect(screen.getByRole('button', { name: 'Publish' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Archive' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /History/ })).toBeInTheDocument();
  });

  it('Published: offers Archive + Edit but not Publish', () => {
    setup({ status: 'Published' });
    expect(screen.queryByRole('button', { name: 'Publish' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Archive' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
  });

  it('Archived: no Publish/Archive/Edit, but still shows the status badge', () => {
    setup({ status: 'Archived' });
    expect(screen.queryByRole('button', { name: 'Publish' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Archive' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.getByText('Archived')).toBeInTheDocument();
  });

  it('publishes and archives via the hooks', async () => {
    const user = userEvent.setup();
    setup({ status: 'Draft' });
    await user.click(screen.getByRole('button', { name: 'Publish' }));
    expect(fns.publish).toHaveBeenCalledWith('d1');
    await user.click(screen.getByRole('button', { name: 'Archive' }));
    expect(fns.archive).toHaveBeenCalledWith('d1');
  });

  it('calls onEdit and onHistory from the action row', async () => {
    const user = userEvent.setup();
    const { onEdit, onHistory } = setup({ status: 'Published' });
    await user.click(screen.getByRole('button', { name: 'Edit' }));
    expect(onEdit).toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: /History/ }));
    expect(onHistory).toHaveBeenCalled();
  });

  it('mounts the linked-artifacts card focused on the document', () => {
    setup();
    const card = screen.getByTestId('linked-artifacts');
    expect(card).toHaveAttribute('data-documentid', 'd1');
  });

  it('uses createdAt for the updated line when updatedAt is null', () => {
    setup({ updatedAt: null });
    expect(screen.getByText(/Updated/)).toBeInTheDocument();
  });
});
