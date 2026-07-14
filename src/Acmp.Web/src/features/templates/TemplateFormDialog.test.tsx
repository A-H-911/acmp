import { describe, it, expect, beforeEach, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TemplateFormDialog } from './TemplateFormDialog';
import { renderWithAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';
import type { TemplateSummary, TemplateDetail } from '../../api/templates';

const fns = vi.hoisted(() => ({
  create: vi.fn().mockResolvedValue({ key: 'TPL-1' }),
  edit: vi.fn().mockResolvedValue({ key: 'TPL-1' }),
  detail: undefined as TemplateDetail | undefined,
}));
vi.mock('../../api/templates', async (orig) => ({
  ...(await orig<typeof import('../../api/templates')>()),
  useCreateTemplate: () => ({ mutateAsync: fns.create, isPending: false }),
  useEditTemplate: () => ({ mutateAsync: fns.edit, isPending: false }),
  useTemplate: () => ({ data: fns.detail }),
}));

const SUMMARY: TemplateSummary = {
  id: 't1', key: 'TPL-2026-001', name: { en: 'Standard Topic', ar: 'قياسي' },
  targetType: 'Topic', status: 'Active', version: 3, createdAt: '2026-02-12T09:00:00Z', updatedAt: null,
};
const DETAIL: TemplateDetail = { ...SUMMARY, body: '# Topic template body' };

describe('TemplateFormDialog (P15e)', () => {
  beforeEach(() => {
    fns.create.mockReset().mockResolvedValue({ key: 'TPL-1' });
    fns.edit.mockReset().mockResolvedValue({ key: 'TPL-1' });
    fns.detail = undefined;
  });

  it('create: validates name + body are required', async () => {
    const user = userEvent.setup();
    renderWithAuth(<TemplateFormDialog open onClose={vi.fn()} />, { roles: ['administrator'] });
    await user.click(screen.getByRole('button', { name: 'Create template' }));
    expect(screen.getByText('A name is required.')).toBeInTheDocument();
    expect(screen.getByText('A body is required.')).toBeInTheDocument();
    expect(fns.create).not.toHaveBeenCalled();
  });

  it('create: mirrors the name, sends the chosen target type + body', async () => {
    const user = userEvent.setup();
    renderWithAuth(<TemplateFormDialog open onClose={vi.fn()} />, { roles: ['secretary'] });
    await user.type(screen.getByLabelText(/Name/), 'ADR record');
    await user.click(screen.getByRole('button', { name: /Target type/ }));
    await user.click(screen.getByRole('option', { name: 'ADR' }));
    await user.type(screen.getByLabelText(/Body/), '# body');
    await user.click(screen.getByRole('button', { name: 'Create template' }));
    expect(fns.create).toHaveBeenCalledWith({ name: { en: 'ADR record', ar: 'ADR record' }, targetType: 'Adr', body: '# body' });
  });

  it('edit: pre-fills name + body, disables the target type, and PUTs name + body', async () => {
    const user = userEvent.setup();
    fns.detail = DETAIL;
    renderWithAuth(<TemplateFormDialog open onClose={vi.fn()} template={SUMMARY} />, { roles: ['secretary'] });
    expect(screen.getByDisplayValue('Standard Topic')).toBeInTheDocument();
    expect(await screen.findByDisplayValue('# Topic template body')).toBeInTheDocument();
    // Target type is immutable → the Select trigger is disabled.
    expect(screen.getByRole('button', { name: /Target type/ })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(fns.edit).toHaveBeenCalledWith({ id: 't1', name: { en: 'Standard Topic', ar: 'Standard Topic' }, body: '# Topic template body' });
  });

  it('surfaces a 409 (deprecated template) inline', async () => {
    const user = userEvent.setup();
    fns.detail = DETAIL;
    fns.edit.mockRejectedValueOnce(new ApiError(409, { title: 'Template is deprecated' }));
    renderWithAuth(<TemplateFormDialog open onClose={vi.fn()} template={SUMMARY} />, { roles: ['secretary'] });
    await screen.findByDisplayValue('# Topic template body');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    const dialog = screen.getByRole('dialog');
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Template is deprecated');
  });
});
