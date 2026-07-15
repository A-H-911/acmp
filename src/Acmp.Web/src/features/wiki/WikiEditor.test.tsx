import { describe, it, expect, beforeEach, vi } from 'vitest';
import { screen, act, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WikiEditor } from './WikiEditor';
import { renderWithAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';
import type { DocumentDetail } from '../../api/wiki';

const fns = vi.hoisted(() => ({ edit: vi.fn().mockResolvedValue(undefined) }));
vi.mock('../../api/wiki', async (orig) => ({
  ...(await orig<typeof import('../../api/wiki')>()),
  useEditDocument: () => ({ mutateAsync: fns.edit, isPending: false }),
}));

const DOC: DocumentDetail = {
  id: 'd1', key: 'DOC-2026-001',
  title: { en: 'Governance model', ar: 'نموذج' },
  body: { en: 'Original body', ar: 'أصلي' },
  status: 'Published', category: 'Governance', tags: ['governance'],
  ownerUserId: 'kc-1', version: 3, versions: [],
  createdAt: '2026-06-01T09:00:00Z', updatedAt: null,
};

function setup() {
  const onDone = vi.fn();
  renderWithAuth(<WikiEditor document={DOC} onDone={onDone} />, { roles: ['chairman'] });
  return { onDone };
}

describe('WikiEditor (P15e)', () => {
  beforeEach(() => {
    fns.edit.mockReset().mockResolvedValue(undefined);
    localStorage.clear(); // isolate the WK8 draft between tests
  });

  it('renders the editing badge, both pane labels and the current body', () => {
    setup();
    expect(screen.getByText('Editing')).toBeInTheDocument();
    expect(screen.getByText('Markdown')).toBeInTheDocument();
    expect(screen.getByText('Preview')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Original body')).toBeInTheDocument();
  });

  it('saves the edited body, passing title/category/tags through unchanged', async () => {
    const user = userEvent.setup();
    setup();
    const editor = screen.getByDisplayValue('Original body');
    await user.clear(editor);
    await user.type(editor, 'Revised body');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(fns.edit).toHaveBeenCalledWith({
      id: 'd1',
      title: DOC.title,
      category: 'Governance',
      body: { en: 'Revised body', ar: 'Revised body' },
      tags: ['governance'],
    });
  });

  it('cancel calls onDone without saving', async () => {
    const user = userEvent.setup();
    const { onDone } = setup();
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onDone).toHaveBeenCalled();
    expect(fns.edit).not.toHaveBeenCalled();
  });

  it('surfaces a 409 (archived between load and save) inline', async () => {
    const user = userEvent.setup();
    fns.edit.mockRejectedValueOnce(new ApiError(409, { title: 'Document is archived' }));
    setup();
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Document is archived');
  });

  it('each formatting-toolbar tool inserts its markdown (M3)', async () => {
    const user = userEvent.setup();
    setup();
    const editor = screen.getByDisplayValue('Original body') as HTMLTextAreaElement;
    for (const name of ['Bold', 'Italic', 'Heading', 'List', 'Quote', 'Link', 'Cross-link']) {
      await user.click(screen.getByRole('button', { name }));
    }
    expect(editor.value).toContain('**'); // bold mark
    expect(editor.value).toContain('[['); // cross-link
    expect(editor.value).toContain('# '); // heading prefix
  });

  it('autosaves the body to a per-doc draft after typing settles, showing the indicator (WK8)', () => {
    vi.useFakeTimers();
    try {
      setup();
      const editor = screen.getByDisplayValue('Original body');
      fireEvent.change(editor, { target: { value: 'Work in progress' } });
      act(() => vi.advanceTimersByTime(600));
      expect(localStorage.getItem('acmp:wiki-draft:d1:en')).toBe('Work in progress');
      expect(screen.getByText('Draft autosaved')).toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });

  it('restores a saved draft on reopen and clears it on cancel (WK8)', async () => {
    const user = userEvent.setup();
    localStorage.setItem('acmp:wiki-draft:d1:en', 'Restored draft');
    const { onDone } = setup();
    expect(screen.getByDisplayValue('Restored draft')).toBeInTheDocument();
    expect(screen.getByText('Draft autosaved')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onDone).toHaveBeenCalled();
    expect(localStorage.getItem('acmp:wiki-draft:d1:en')).toBeNull();
  });
});
