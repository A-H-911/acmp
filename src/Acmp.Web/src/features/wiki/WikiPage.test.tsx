import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { WikiPage } from './WikiPage';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';
import type { CommitteeRole } from '../../auth/roles';
import type { DocumentSummary, DocumentDetail } from '../../api/wiki';

vi.mock('../../api/wiki', async (orig) => ({
  ...(await orig<typeof import('../../api/wiki')>()),
  useWikiDocuments: vi.fn(),
  useDocument: vi.fn(),
}));
import { useWikiDocuments, useDocument } from '../../api/wiki';

// Stub the leaf components — WikiPage owns the tree + state switching; the children have their own tests.
vi.mock('./WikiReadingView', () => ({
  WikiReadingView: (p: { document: DocumentDetail; onEdit: () => void; onHistory: () => void }) => (
    <div data-testid="reading" data-key={p.document.key}>
      <button onClick={p.onEdit}>stub-edit</button>
      <button onClick={p.onHistory}>stub-history</button>
    </div>
  ),
}));
vi.mock('./WikiEditor', () => ({
  WikiEditor: (p: { document: DocumentDetail; onDone: () => void }) => (
    <div data-testid="editor"><button onClick={p.onDone}>stub-done</button></div>
  ),
}));
vi.mock('./WikiVersionHistory', () => ({ WikiVersionHistory: (p: { open: boolean }) => (p.open ? <div data-testid="history" /> : null) }));
vi.mock('./CreateDocumentDialog', () => ({ CreateDocumentDialog: (p: { open: boolean }) => (p.open ? <div data-testid="create" /> : null) }));

const mockList = useWikiDocuments as unknown as Mock;
const mockDoc = useDocument as unknown as Mock;

const summary = (over: Partial<DocumentSummary>): DocumentSummary => ({
  id: over.key ?? 'x', key: 'DOC-X', title: { en: 'Title', ar: 'ع' }, status: 'Published', category: 'Governance',
  tags: [], ownerUserId: 'kc-1', version: 1, createdAt: '2026-06-01T09:00:00Z', updatedAt: null, ...over,
});

const DOCS: DocumentSummary[] = [
  summary({ id: '1', key: 'DOC-1', title: { en: 'Governance model', ar: 'ع1' }, status: 'Published', category: 'Governance' }),
  summary({ id: '2', key: 'DOC-2', title: { en: 'Draft page', ar: 'ع2' }, status: 'Draft', category: 'Governance' }),
  summary({ id: '3', key: 'DOC-3', title: { en: 'Archived page', ar: 'ع3' }, status: 'Archived', category: 'Governance' }),
  summary({ id: '4', key: 'DOC-4', title: { en: 'API standards', ar: 'ع4' }, status: 'Published', category: 'Standards' }),
];

const DETAIL: DocumentDetail = {
  id: '1', key: 'DOC-1', title: { en: 'Governance model', ar: 'ع' }, body: { en: 'b', ar: 'b' },
  status: 'Published', category: 'Governance', tags: [], ownerUserId: 'kc-1', version: 1, versions: [],
  createdAt: '2026-06-01T09:00:00Z', updatedAt: null,
};

function listResult(over: Partial<ReturnType<typeof useWikiDocuments>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useWikiDocuments>;
}
function withDocs(items = DOCS) {
  mockList.mockReturnValue(listResult({ data: { items, total: items.length, page: 1, pageSize: 500, totalPages: 1 } }));
}
function docResult(over: Partial<ReturnType<typeof useDocument>>) {
  return { data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over } as ReturnType<typeof useDocument>;
}
function setup(route = '/wiki', roles: CommitteeRole[] = ['secretary']) {
  return render(
    <AcmpAuthContext.Provider value={makeAuth(roles)}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path="/wiki" element={<WikiPage />} />
          <Route path="/wiki/:key" element={<WikiPage />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('WikiPage (P15e)', () => {
  beforeEach(() => {
    mockList.mockReset();
    mockDoc.mockReset().mockReturnValue(docResult({}));
    withDocs();
  });

  it('shows the loading skeleton while the register is fetching', () => {
    mockList.mockReturnValue(listResult({ isLoading: true }));
    setup();
    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
  });

  it('shows an error state on a register failure', () => {
    mockList.mockReturnValue(listResult({ isError: true }));
    setup();
    expect(screen.getByText('Couldn’t load the wiki')).toBeInTheDocument();
  });

  it('groups Published pages into spaces and hides Archived; a manager also sees Draft (marked)', () => {
    setup('/wiki', ['secretary']);
    expect(screen.getByRole('link', { name: 'Governance model' })).toBeInTheDocument();
    const draftLink = screen.getByRole('link', { name: /Draft page/ });
    expect(draftLink).toHaveAccessibleName(/Draft$/); // draft marker appended to the link name
    expect(screen.queryByRole('link', { name: 'Archived page' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'API standards' })).toBeInTheDocument();
    // Space headers (localized category labels).
    expect(screen.getByText('Governance')).toBeInTheDocument();
    expect(screen.getByText('Standards')).toBeInTheDocument();
  });

  it('hides Draft pages and the New-page button from a non-manager', () => {
    setup('/wiki', ['member']);
    expect(screen.queryByRole('link', { name: 'Draft page' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Governance model' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New page' })).not.toBeInTheDocument();
  });

  it('no documents at all: shows the empty state + New page for a manager', () => {
    withDocs([]);
    setup('/wiki', ['secretary']);
    expect(screen.getByText('No pages yet')).toBeInTheDocument();
    // Two New-page affordances for a manager here: the tree action + the empty-state CTA.
    expect(screen.getAllByRole('button', { name: 'New page' }).length).toBeGreaterThan(0);
  });

  it('opens the create dialog from the New-page button', async () => {
    const user = userEvent.setup();
    setup('/wiki', ['secretary']);
    await user.click(screen.getByRole('button', { name: 'New page' }));
    expect(screen.getByTestId('create')).toBeInTheDocument();
  });

  it('filters the tree by search and shows the search-empty state on no match', async () => {
    const user = userEvent.setup();
    setup('/wiki', ['secretary']);
    await user.type(screen.getByRole('searchbox', { name: /Search this wiki/ }), 'zzz');
    expect(screen.getByText('No pages match')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Governance model' })).not.toBeInTheDocument();
  });

  it('with docs but no page selected: shows the "select a page" hint', () => {
    setup('/wiki');
    expect(screen.getByText('Select a page')).toBeInTheDocument();
  });

  it('renders the reading view for a selected page and toggles into the editor', async () => {
    const user = userEvent.setup();
    mockDoc.mockReturnValue(docResult({ data: DETAIL }));
    setup('/wiki/DOC-1');
    expect(screen.getByTestId('reading')).toHaveAttribute('data-key', 'DOC-1');
    await user.click(screen.getByRole('button', { name: 'stub-edit' }));
    expect(screen.getByTestId('editor')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'stub-done' }));
    expect(screen.getByTestId('reading')).toBeInTheDocument();
  });

  it('opens the version panel from the reading view', async () => {
    const user = userEvent.setup();
    mockDoc.mockReturnValue(docResult({ data: DETAIL }));
    setup('/wiki/DOC-1');
    await user.click(screen.getByRole('button', { name: 'stub-history' }));
    expect(screen.getByTestId('history')).toBeInTheDocument();
  });

  it('shows a not-found state for an unknown key (404)', () => {
    mockDoc.mockReturnValue(docResult({ isError: true, error: new ApiError(404, undefined) }));
    setup('/wiki/DOC-999');
    expect(screen.getByText('Page not found')).toBeInTheDocument();
  });

  it('shows a retryable error for a non-404 document failure', () => {
    mockDoc.mockReturnValue(docResult({ isError: true, error: new ApiError(500, undefined) }));
    setup('/wiki/DOC-1');
    expect(screen.getByText('Couldn’t load the wiki')).toBeInTheDocument();
  });

  it('shows the article skeleton while the document is loading', () => {
    mockDoc.mockReturnValue(docResult({ isLoading: true }));
    setup('/wiki/DOC-1');
    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
  });
});
