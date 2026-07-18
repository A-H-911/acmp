import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { RecordDecisionDialog } from './RecordDecisionDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const record = vi.hoisted(() => vi.fn());
const issue = vi.hoisted(() => vi.fn());
vi.mock('../../api/decisions', async (orig) => ({
  ...(await orig<typeof import('../../api/decisions')>()),
  useRecordDecision: () => ({ mutateAsync: record, isPending: false }),
  useIssueDecision: () => ({ mutateAsync: issue, isPending: false }),
}));

// A Closed vote on this topic+meeting exists → the decision couples + ratifies it on issue.
const closedVotes = vi.hoisted(() => [
  { id: 'vote-1', key: 'VOTE-2026-014', topicId: 'top-1', meetingId: 'mtg-1', status: 'Closed' },
]);
vi.mock('../../api/votes', () => ({
  useVotesRegister: () => ({ data: closedVotes }),
}));

const SOURCE = { topicId: 'top-1', topicKey: 'TOP-2026-014', meetingId: 'mtg-1' };

function setup() {
  return render(
    <MemoryRouter>
      <RecordDecisionDialog open onClose={vi.fn()} source={SOURCE} />
    </MemoryRouter>,
  );
}

async function fillRequired(user: ReturnType<typeof userEvent.setup>) {
  // getByRole uses the accessible name (excludes the aria-hidden required "*"); getByLabelText would
  // need exact:false because the Field renders the marker into the label's text content.
  await user.type(screen.getByRole('textbox', { name: 'Title' }), 'Adopt Keycloak');
  await user.type(screen.getByRole('textbox', { name: 'Decision statement' }), 'The committee adopts Keycloak.');
  await user.type(screen.getByRole('textbox', { name: 'Rationale' }), 'A single IdP reduces auth maintenance.');
}

describe('RecordDecisionDialog (P17b, W12)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    record.mockResolvedValue({ id: 'dec-1', key: 'DECN-2026-020' });
    issue.mockResolvedValue(undefined);
  });

  it('records the decision coupled to the closed vote, issues it, then navigates to the new decision', async () => {
    const user = userEvent.setup();
    setup();
    // The coupled-vote hint is shown so the chair knows issuing will ratify it.
    expect(screen.getByText(/Ratifies VOTE-2026-014/)).toBeInTheDocument();

    await fillRequired(user);
    await user.click(screen.getByRole('button', { name: 'Record & issue' }));

    expect(record).toHaveBeenCalledTimes(1);
    expect(record.mock.calls[0][0]).toMatchObject({ topicId: 'top-1', voteId: 'vote-1', outcome: 'Approved' });
    expect(issue).toHaveBeenCalledWith({ id: 'dec-1', chairOverride: false, overrideJustification: null });
    expect(nav).toHaveBeenCalledWith('/decisions/DECN-2026-020');
  });

  it('sends the override flag + justification when the chair issues against the vote', async () => {
    const user = userEvent.setup();
    setup();
    await fillRequired(user);
    await user.click(screen.getByRole('checkbox', { name: /Issue against the vote/ }));
    await user.type(screen.getByRole('textbox', { name: 'Override justification' }), 'Later evidence overturned the vote.');
    await user.click(screen.getByRole('button', { name: 'Record & issue' }));

    expect(issue.mock.calls[0][0]).toMatchObject({
      id: 'dec-1',
      chairOverride: true,
      overrideJustification: { en: 'Later evidence overturned the vote.', ar: 'Later evidence overturned the vote.' },
    });
  });

  it('surfaces a 403 (SoD-3: chair was the vote closer) inline and does not navigate', async () => {
    issue.mockRejectedValueOnce(
      new ApiError(403, { title: 'The chairman issuing a vote-coupled decision cannot be the vote’s sole counter.' }),
    );
    const user = userEvent.setup();
    setup();
    await fillRequired(user);
    await user.click(screen.getByRole('button', { name: 'Record & issue' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('cannot be the vote');
    expect(nav).not.toHaveBeenCalled();
  });

  it('does not re-record the Draft when a failed issue is retried (record-once guard)', async () => {
    issue.mockRejectedValueOnce(new ApiError(409, { title: 'downstream link required' }));
    const user = userEvent.setup();
    setup();
    await fillRequired(user);
    await user.click(screen.getByRole('button', { name: 'Record & issue' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();

    // Retry: the held Draft is re-issued; record must not run a second time (no duplicate Draft).
    await user.click(screen.getByRole('button', { name: 'Record & issue' }));
    expect(record).toHaveBeenCalledTimes(1);
    expect(issue).toHaveBeenCalledTimes(2);
  });

  it('blocks submit and shows field errors when required fields are empty', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Record & issue' }));
    expect(screen.getByText('A title is required.')).toBeInTheDocument();
    expect(record).not.toHaveBeenCalled();
  });
});
