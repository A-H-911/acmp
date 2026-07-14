import { describe, it, expect, beforeEach, vi } from 'vitest';
import { screen } from '@testing-library/react';
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
  beforeEach(() => fns.edit.mockReset().mockResolvedValue(undefined));

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
});
