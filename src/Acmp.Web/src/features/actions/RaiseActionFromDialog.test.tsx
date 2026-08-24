import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RaiseActionFromDialog } from './RaiseActionFromDialog';

vi.mock('../../api/topics', () => ({ useBacklog: vi.fn() }));
vi.mock('../../api/decisions', () => ({ useDecisionsRegister: vi.fn() }));
vi.mock('../../api/meetings', () => ({ useMeetings: vi.fn() }));
import { useBacklog } from '../../api/topics';
import { useDecisionsRegister } from '../../api/decisions';
import { useMeetings } from '../../api/meetings';

const mockTopics = useBacklog as unknown as Mock;
const mockDecisions = useDecisionsRegister as unknown as Mock;
const mockMeetings = useMeetings as unknown as Mock;

const idle = { isLoading: false, isError: false, refetch: vi.fn() };

function setup(onPicked = vi.fn()) {
  render(<RaiseActionFromDialog open onClose={vi.fn()} onPicked={onPicked} />);
  return onPicked;
}

describe('RaiseActionFromDialog', () => {
  beforeEach(() => {
    // Clear CALL HISTORY, not just return values: the "queries only the selected type" assertion is
    // about who was called, and without this it sees the previous test's clicks and fails for a
    // reason that has nothing to do with the component.
    vi.clearAllMocks();
    mockTopics.mockReturnValue({
      ...idle,
      data: { items: [{ id: 't-1', key: 'TOP-2026-014', title: 'Adopt mTLS between services' }] },
    });
    mockDecisions.mockReturnValue({
      ...idle,
      data: [
        { id: 'd-1', key: 'DECN-2026-008', title: { en: 'Approve Keycloak as the IdP', ar: 'اعتماد كيكلوك' } },
        { id: 'd-2', key: 'DECN-2026-009', title: { en: 'Defer the diagram sidecar', ar: 'تأجيل المخططات' } },
      ],
    });
    mockMeetings.mockReturnValue({
      ...idle,
      data: [{ id: 'm-1', key: 'MTG-2026-019', title: 'Q2 review' }],
    });
  });

  // The whole reason this dialog exists: an ActionItem's (SourceType, SourceId) is non-nullable, so the
  // source must be chosen BEFORE the create form opens. The pair handed upward has to be the artifact's
  // PublicId and display key — anything else and the command is rejected or the Linked column is wrong.
  it('emits the picked artifact as sourceType + sourceId + sourceKey', async () => {
    const user = userEvent.setup();
    const onPicked = setup();

    await user.click(screen.getByRole('button', { name: /DECN-2026-009/ }));

    expect(onPicked).toHaveBeenCalledWith({
      sourceType: 'Decision',
      sourceId: 'd-2',
      sourceKey: 'DECN-2026-009',
    });
  });

  it('switches the source type and emits that type', async () => {
    const user = userEvent.setup();
    const onPicked = setup();

    await user.click(screen.getByRole('button', { name: 'Meeting' }));
    await user.click(screen.getByRole('button', { name: /MTG-2026-019/ }));

    expect(onPicked).toHaveBeenCalledWith({
      sourceType: 'Meeting',
      sourceId: 'm-1',
      sourceKey: 'MTG-2026-019',
    });
  });

  // Only the selected type should query. Three hooks in one body would hit all three registers every
  // time the dialog opened, which is why each type lives in its own component.
  it('queries only the selected source type', () => {
    setup();
    expect(mockDecisions).toHaveBeenCalled();
    expect(mockMeetings).not.toHaveBeenCalled();
    expect(mockTopics).not.toHaveBeenCalled();
  });

  it('filters candidates by title or key', async () => {
    const user = userEvent.setup();
    setup();

    await user.type(screen.getByRole('searchbox', { name: /filter/i }), 'Keycloak');

    expect(screen.getByRole('button', { name: /DECN-2026-008/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /DECN-2026-009/ })).not.toBeInTheDocument();
  });

  it('shows an empty state rather than a blank list when nothing matches', async () => {
    const user = userEvent.setup();
    setup();

    await user.type(screen.getByRole('searchbox', { name: /filter/i }), 'zzzz-no-match');

    expect(screen.getByText('Nothing to link to')).toBeInTheDocument();
  });

  it('lists topics and emits Topic when that type is selected', async () => {
    const user = userEvent.setup();
    const onPicked = setup();

    await user.click(screen.getByRole('button', { name: 'Topic' }));
    await user.click(screen.getByRole('button', { name: /TOP-2026-014/ }));

    expect(onPicked).toHaveBeenCalledWith({
      sourceType: 'Topic',
      sourceId: 't-1',
      sourceKey: 'TOP-2026-014',
    });
  });

  // The picker fetches, so it owns the same three states every register does. A chooser that renders a
  // blank body while loading — or silently empty on failure — reads as "there is nothing to link to",
  // which is a different and wrong answer.
  it('shows the loading state while candidates are fetching', () => {
    mockDecisions.mockReturnValue({ ...idle, isLoading: true, data: undefined });
    setup();

    expect(screen.getAllByRole('status').length).toBeGreaterThan(0);
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('shows a retryable error state when candidates fail to load', async () => {
    const user = userEvent.setup();
    const refetch = vi.fn();
    mockDecisions.mockReturnValue({ ...idle, isError: true, data: undefined, refetch });
    setup();

    await user.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  // The retry assertion above covers only the DECISION picker. Each source type is its own
  // component with its own onRetry, and the other two had never been exercised - coverage-v8 v4
  // named both lines (DW-082).
  it('offers a working retry when TOPIC candidates fail to load', async () => {
    const user = userEvent.setup();
    const refetch = vi.fn();
    mockTopics.mockReturnValue({ ...idle, isError: true, data: undefined, refetch });
    setup();

    await user.click(screen.getByRole('button', { name: 'Topic' }));
    await user.click(screen.getByRole('button', { name: /retry/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it('offers a working retry when MEETING candidates fail to load', async () => {
    const user = userEvent.setup();
    const refetch = vi.fn();
    mockMeetings.mockReturnValue({ ...idle, isError: true, data: undefined, refetch });
    setup();

    await user.click(screen.getByRole('button', { name: 'Meeting' }));
    await user.click(screen.getByRole('button', { name: /retry/i }));

    expect(refetch).toHaveBeenCalled();
  });

  // Candidates are <button>s, not clickable rows: the picker has to be operable by keyboard.
  it('exposes every candidate as a focusable control', () => {
    setup();
    const list = screen.getByRole('list');
    within(list).getAllByRole('button').forEach((b) => expect(b.tagName).toBe('BUTTON'));
  });
});
