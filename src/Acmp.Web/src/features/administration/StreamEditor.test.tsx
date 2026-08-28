import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AddStreamButton, RenameStreamButton } from './StreamEditor';
import { useCreateStream, useRenameStream, type StreamRef } from '../../api/members';
import { makeQueryWrapper } from '../../test/queryHarness';
import i18n from '../../i18n';

vi.mock('../../api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/members')>()),
  useCreateStream: vi.fn(),
  useRenameStream: vi.fn(),
}));

const mockCreate = useCreateStream as unknown as ReturnType<typeof vi.fn>;
const mockRename = useRenameStream as unknown as ReturnType<typeof vi.fn>;

const CORE: StreamRef = {
  publicId: 's1', code: 'core', nameEn: 'Core', nameAr: 'الأساسي', isWildcard: false,
};

/** A mutation stub whose mutateAsync resolves or rejects on demand. */
function mutation(over: Record<string, unknown> = {}) {
  return { mutateAsync: vi.fn().mockResolvedValue({ publicId: 'new' }), isPending: false, ...over };
}

const wrap = () => ({ wrapper: makeQueryWrapper().wrapper });

describe('AddStreamButton (WBS-24.7 — NFR-010 configuration-driven)', () => {
  beforeEach(() => {
    mockCreate.mockReturnValue(mutation());
    mockRename.mockReturnValue(mutation());
    return i18n.changeLanguage('en');
  });

  it('reproduces the design reference’s primary action', () => {
    render(<AddStreamButton />, wrap());

    // ACMP Administration.dc.html gives this section primary: L('Add stream','إضافة مسار').
    expect(screen.getByRole('button', { name: 'Add stream' })).toBeInTheDocument();
  });

  it('opens a dialog that asks for the code and BOTH language halves of the name', async () => {
    const user = userEvent.setup();
    render(<AddStreamButton />, wrap());

    await user.click(screen.getByRole('button', { name: 'Add stream' }));

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByLabelText(/Code/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Name \(English\)/i)).toBeInTheDocument();
    // Guardrail 9: a single-language user-facing string is not shippable, so the Arabic half is not
    // optional and the form must ask for it rather than defaulting it.
    expect(screen.getByLabelText(/Name \(Arabic\)/i)).toBeInTheDocument();
  });

  it('creates the stream with trimmed values and closes', async () => {
    const create = mutation();
    mockCreate.mockReturnValue(create);
    const user = userEvent.setup();
    render(<AddStreamButton />, wrap());

    await user.click(screen.getByRole('button', { name: 'Add stream' }));
    await user.type(screen.getByLabelText(/Code/i), '  mobile  ');
    await user.type(screen.getByLabelText(/Name \(English\)/i), ' Mobile ');
    await user.type(screen.getByLabelText(/Name \(Arabic\)/i), ' الجوال ');
    // Index 1 is the dialog's confirm; index 0 is the trigger that opened it. Both legitimately
    // carry the design's "Add stream" label, so the query is positional rather than ambiguous.
    await user.click(screen.getAllByRole('button', { name: 'Add stream' })[1]);

    expect(create.mutateAsync).toHaveBeenCalledWith({
      code: 'mobile', nameEn: 'Mobile', nameAr: 'الجوال',
    });
  });

  it('refuses a code that is not a usable scope key, and says why', async () => {
    const user = userEvent.setup();
    render(<AddStreamButton />, wrap());

    await user.click(screen.getByRole('button', { name: 'Add stream' }));
    await user.type(screen.getByLabelText(/Code/i), 'has space');

    // The message mirrors CreateStreamValidator so the refusal is explained here rather than only
    // arriving from the server after a round trip.
    expect(screen.getByText(/letters, digits and hyphens/i)).toBeInTheDocument();
  });

  it('keeps the confirm disabled until every required field is present', async () => {
    const user = userEvent.setup();
    render(<AddStreamButton />, wrap());

    await user.click(screen.getByRole('button', { name: 'Add stream' }));
    const confirm = screen.getAllByRole('button', { name: 'Add stream' })[1];

    expect(confirm).toBeDisabled();
    await user.type(screen.getByLabelText(/Code/i), 'mobile');
    expect(confirm).toBeDisabled();
    await user.type(screen.getByLabelText(/Name \(English\)/i), 'Mobile');
    expect(confirm).toBeDisabled(); // the Arabic half is still missing
    await user.type(screen.getByLabelText(/Name \(Arabic\)/i), 'الجوال');
    expect(confirm).toBeEnabled();
  });

  it('surfaces a refused save instead of closing as though it worked', async () => {
    // The duplicate-code refusal is the one an administrator will actually meet. A dialog that
    // closed here would report success for a stream that does not exist.
    mockCreate.mockReturnValue(mutation({ mutateAsync: vi.fn().mockRejectedValue(new Error('409')) }));
    const user = userEvent.setup();
    render(<AddStreamButton />, wrap());

    await user.click(screen.getByRole('button', { name: 'Add stream' }));
    await user.type(screen.getByLabelText(/Code/i), 'core');
    await user.type(screen.getByLabelText(/Name \(English\)/i), 'Core');
    await user.type(screen.getByLabelText(/Name \(Arabic\)/i), 'الأساسي');
    await user.click(screen.getAllByRole('button', { name: 'Add stream' })[1]);

    expect(await screen.findByText(/could not be saved/i)).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('discards what was typed when the dialog is cancelled', async () => {
    const user = userEvent.setup();
    render(<AddStreamButton />, wrap());

    await user.click(screen.getByRole('button', { name: 'Add stream' }));
    await user.type(screen.getByLabelText(/Code/i), 'scratch');
    await user.click(screen.getByRole('button', { name: /Cancel/i }));
    await user.click(screen.getByRole('button', { name: 'Add stream' }));

    expect(screen.getByLabelText(/Code/i)).toHaveValue('');
  });
});

describe('RenameStreamButton', () => {
  beforeEach(() => {
    mockCreate.mockReturnValue(mutation());
    mockRename.mockReturnValue(mutation());
    return i18n.changeLanguage('en');
  });

  it('opens pre-filled with the stream’s current bilingual name', async () => {
    const user = userEvent.setup();
    render(<RenameStreamButton stream={CORE} />, wrap());

    await user.click(screen.getByRole('button', { name: /Rename Core/i }));

    expect(screen.getByLabelText(/Name \(English\)/i)).toHaveValue('Core');
    expect(screen.getByLabelText(/Name \(Arabic\)/i)).toHaveValue('الأساسي');
  });

  it('offers NO way to change the code, and says the code stays put', async () => {
    const user = userEvent.setup();
    render(<RenameStreamButton stream={CORE} />, wrap());

    await user.click(screen.getByRole('button', { name: /Rename Core/i }));

    // ⚠ THE LOAD-BEARING ASSERTION. Topics carry the code and the ABAC intersect resolves on it, so
    // a re-code would silently re-scope every topic naming the old value. The API cannot express it
    // and neither may the form — a field here would be a data migration behind a text box.
    expect(screen.queryByLabelText(/^Code/i)).not.toBeInTheDocument();
    expect(screen.getByText(/code core stays as it is/i)).toBeInTheDocument();
  });

  it('submits only the two names', async () => {
    const rename = mutation({ mutateAsync: vi.fn().mockResolvedValue(undefined) });
    mockRename.mockReturnValue(rename);
    const user = userEvent.setup();
    render(<RenameStreamButton stream={CORE} />, wrap());

    await user.click(screen.getByRole('button', { name: /Rename Core/i }));
    await user.clear(screen.getByLabelText(/Name \(English\)/i));
    await user.type(screen.getByLabelText(/Name \(English\)/i), 'Core Platform');
    await user.click(screen.getByRole('button', { name: /^Save$/i }));

    expect(rename.mutateAsync).toHaveBeenCalledWith({
      publicId: 's1', nameEn: 'Core Platform', nameAr: 'الأساسي',
    });
  });

  it('will not submit an emptied name in either language', async () => {
    const user = userEvent.setup();
    render(<RenameStreamButton stream={CORE} />, wrap());

    await user.click(screen.getByRole('button', { name: /Rename Core/i }));
    await user.clear(screen.getByLabelText(/Name \(Arabic\)/i));

    expect(screen.getByRole('button', { name: /^Save$/i })).toBeDisabled();
  });

  it('surfaces a refused rename rather than closing silently', async () => {
    mockRename.mockReturnValue(mutation({ mutateAsync: vi.fn().mockRejectedValue(new Error('500')) }));
    const user = userEvent.setup();
    render(<RenameStreamButton stream={CORE} />, wrap());

    await user.click(screen.getByRole('button', { name: /Rename Core/i }));
    await user.click(screen.getByRole('button', { name: /^Save$/i }));

    expect(await screen.findByText(/could not be saved/i)).toBeInTheDocument();
  });

  it('closes without renaming when cancelled', async () => {
    const rename = mutation();
    mockRename.mockReturnValue(rename);
    const user = userEvent.setup();
    render(<RenameStreamButton stream={CORE} />, wrap());

    await user.click(screen.getByRole('button', { name: /Rename Core/i }));
    await user.click(screen.getByRole('button', { name: /Cancel/i }));

    expect(rename.mutateAsync).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
