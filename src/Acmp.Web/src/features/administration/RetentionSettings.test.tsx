/*
 * WBS-24.5 (DW-036 / FR-155, NFR-059, NFR-060). The screen's job is to be HONEST about a v1 posture in
 * which nothing purges, so the assertions are mostly about what it SAYS, not what it lets you do.
 */
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithAuth } from '../../test/render';
import { RetentionSettings } from './RetentionSettings';
import i18n from '../../i18n';

vi.mock('../../api/retention', () => ({
  useRetentionPolicy: vi.fn(),
  useSetRetentionSetting: vi.fn(),
}));
import { useRetentionPolicy, useSetRetentionSetting } from '../../api/retention';

const mockPolicy = useRetentionPolicy as unknown as Mock;
const mockSave = useSetRetentionSetting as unknown as Mock;
const mutate = vi.fn();

function policy(over: Record<string, unknown> = {}) {
  mockPolicy.mockReturnValue({
    data: { automaticPurgeEnabled: false, settings: [] },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
    ...over,
  });
}

describe('RetentionSettings (WBS-24.5)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSave.mockReturnValue({ mutate, isPending: false, isError: false });
    policy();
  });

  it('states the v1 posture and that automatic purge is off', () => {
    renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
    expect(screen.getByText('Retention posture')).toBeInTheDocument();
    // The clause NFR-059 and NFR-060 both turn on. A screen that showed periods without this would be
    // a blind control — a reader would take a stored number for an enforced one.
    expect(screen.getByText('Automatic purge is off')).toBeInTheDocument();
  });

  it('treats "no periods set" as the CORRECT state, not a failure', () => {
    renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
    expect(screen.getByText('No retention periods are set')).toBeInTheDocument();
    expect(screen.getByText(/expected state for this release/i)).toBeInTheDocument();
  });

  it('lists configured periods once legal has defined any', () => {
    policy({ data: { automaticPurgeEnabled: false, settings: [{ key: 'retention.topic.years', valueJson: '{"years":7}' }] } });
    renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
    // Scoped to the TABLE: the form below carries a worked example using the same key and value, so an
    // unscoped query matches twice and the assertion stops meaning "the table lists it".
    const table = within(screen.getByRole('table'));
    expect(table.getByText('retention.topic.years')).toBeInTheDocument();
    expect(table.getByText('{"years":7}')).toBeInTheDocument();
    // Still off: recording a period must not read as enabling anything.
    expect(screen.getByText('Automatic purge is off')).toBeInTheDocument();
  });

  it('submits a key and value, trimmed', async () => {
    const user = userEvent.setup();
    renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
    // ⚠ user.paste, NOT user.type: userEvent parses `{` as a keyboard-descriptor prefix, so typing
    // `{"years":7}` throws "Expected repeat modifier ... but found y". Escaping it as `{{` would work
    // and would also stop the test resembling what a person actually does with a JSON value.
    await user.click(screen.getByLabelText('Key'));
    await user.paste('  retention.topic.years  ');
    await user.click(screen.getByLabelText('Value'));
    await user.paste('  {"years":7}  ');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(mutate).toHaveBeenCalledWith({ key: 'retention.topic.years', valueJson: '{"years":7}' });
  });

  it('keeps Save disabled until both fields carry something', async () => {
    const user = userEvent.setup();
    renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
    const save = screen.getByRole('button', { name: 'Save' });
    expect(save).toBeDisabled();
    await user.click(screen.getByLabelText('Key'));
    await user.paste('retention.topic.years');
    expect(save).toBeDisabled();
    await user.click(screen.getByLabelText('Value'));
    await user.paste('{"years":7}');
    expect(save).toBeEnabled();
  });

  it('surfaces a refusal rather than failing silently', () => {
    mockSave.mockReturnValue({ mutate, isPending: false, isError: true });
    renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
    expect(screen.getByRole('alert')).toHaveTextContent(/not saved/i);
  });

  it('renders loading and error states', () => {
    policy({ data: undefined, isLoading: true });
    const { unmount } = renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
    unmount();
    policy({ data: undefined, isLoading: false, isError: true });
    renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('renders in Arabic without falling back to English', async () => {
    await i18n.changeLanguage('ar');
    try {
      renderWithAuth(<RetentionSettings />, { roles: ['administrator'] });
      // Assert the PROPERTY — a run of Arabic script — never a hand-picked fragment, because Arabic
      // morphology contracts prepositions and an exact substring can be absent from correct output.
      expect(screen.getAllByText(/[؀-ۿ]/).length).toBeGreaterThan(0);
      expect(screen.queryByText('Retention posture')).not.toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
