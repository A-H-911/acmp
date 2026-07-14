import { describe, it, expect, beforeEach, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CreateDocumentDialog } from './CreateDocumentDialog';
import { renderWithAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';

const fns = vi.hoisted(() => ({ create: vi.fn().mockResolvedValue({ key: 'DOC-2026-007' }), navigate: vi.fn() }));
vi.mock('../../api/wiki', async (orig) => ({
  ...(await orig<typeof import('../../api/wiki')>()),
  useCreateDocument: () => ({ mutateAsync: fns.create, isPending: false }),
}));
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => fns.navigate,
}));

function setup() {
  renderWithAuth(<CreateDocumentDialog open onClose={vi.fn()} />, { roles: ['secretary'] });
}

describe('CreateDocumentDialog (P15e)', () => {
  beforeEach(() => {
    fns.create.mockReset().mockResolvedValue({ key: 'DOC-2026-007' });
    fns.navigate.mockReset();
  });

  it('validates that title and content are required', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Create page' }));
    expect(screen.getByText('A title is required.')).toBeInTheDocument();
    expect(screen.getByText('Content is required.')).toBeInTheDocument();
    expect(fns.create).not.toHaveBeenCalled();
  });

  it('creates a page with mirrored title/body, the selected space and tags, then navigates', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Voting rules');
    // Change the space Select from the default (Governance) to Standards.
    await user.click(screen.getByRole('button', { name: /Space/ }));
    await user.click(screen.getByRole('option', { name: 'Standards' }));
    await user.type(screen.getByLabelText(/Content/), 'Body text');
    const tags = screen.getByLabelText(/Tags/);
    await user.type(tags, 'quorum{Enter}');
    await user.click(screen.getByRole('button', { name: 'Create page' }));
    expect(fns.create).toHaveBeenCalledWith({
      title: { en: 'Voting rules', ar: 'Voting rules' },
      category: 'Standards',
      body: { en: 'Body text', ar: 'Body text' },
      tags: ['quorum'],
    });
    expect(fns.navigate).toHaveBeenCalledWith('/wiki/DOC-2026-007');
  });

  it('surfaces a server error and does not navigate', async () => {
    const user = userEvent.setup();
    fns.create.mockRejectedValueOnce(new ApiError(409, { title: 'Duplicate title' }));
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Dup');
    await user.type(screen.getByLabelText(/Content/), 'x');
    await user.click(screen.getByRole('button', { name: 'Create page' }));
    const dialog = screen.getByRole('dialog');
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Duplicate title');
    expect(fns.navigate).not.toHaveBeenCalled();
  });
});
