import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ConvertToTopicDialog } from './ConvertToTopicDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const convert = vi.hoisted(() => vi.fn().mockResolvedValue({ id: 'top-guid', key: 'TOP-2026-030' }));
const markConverted = vi.hoisted(() => vi.fn());
vi.mock('../../api/topics', () => ({ useConvertResearchToTopic: () => ({ mutateAsync: convert, isPending: false }) }));
vi.mock('../../api/research', () => ({ useMarkRecommendationConverted: () => ({ mutate: markConverted, isPending: false }) }));

function setup(over: Partial<Parameters<typeof ConvertToTopicDialog>[0]> = {}) {
  return render(
    <MemoryRouter>
      <ConvertToTopicDialog
        onClose={vi.fn()}
        missionId="m1"
        seedTitle="Adopt a unified IdP"
        seedDescription="Standardise identity across streams"
        {...over}
      />
    </MemoryRouter>,
  );
}

describe('ConvertToTopicDialog (P15c-2)', () => {
  beforeEach(() => vi.clearAllMocks());

  it('pre-fills title + description from the seed', () => {
    setup();
    expect(screen.getByLabelText(/Title/)).toHaveValue('Adopt a unified IdP');
    expect(screen.getByLabelText(/Description/)).toHaveValue('Standardise identity across streams');
  });

  it('trims a seed title longer than the 120-char topic limit', () => {
    setup({ seedTitle: 'x'.repeat(200) });
    expect((screen.getByLabelText(/Title/) as HTMLInputElement).value).toHaveLength(120);
  });

  it('validates justification + at least one stream before submitting', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Convert' }));
    expect(screen.getByText('A justification is required.')).toBeInTheDocument();
    expect(screen.getByText('Add at least one stream.')).toBeInTheDocument();
    expect(convert).not.toHaveBeenCalled();
  });

  it('converts a mission (no recommendation) and navigates to the new topic', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Justification/), 'Cuts per-stream maintenance');
    await user.type(screen.getByLabelText(/Affected streams/), 'IAM{Enter}');
    await user.click(screen.getByRole('button', { name: 'Convert' }));
    expect(convert).toHaveBeenCalledWith({
      missionId: 'm1', recommendationId: null,
      title: 'Adopt a unified IdP', description: 'Standardise identity across streams',
      justification: 'Cuts per-stream maintenance', type: 'ResearchDiscovery', urgency: 'Normal',
      streams: ['IAM'], systems: [], tags: [],
    });
    expect(markConverted).not.toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith('/topics/TOP-2026-030');
  });

  it('converts a recommendation and fires the best-effort mark-converted (non-fatal)', async () => {
    const user = userEvent.setup();
    setup({ recommendationId: 'r2' });
    await user.type(screen.getByLabelText(/Justification/), 'Rollback safety');
    await user.type(screen.getByLabelText(/Affected streams/), 'Platform{Enter}');
    await user.click(screen.getByRole('button', { name: 'Convert' }));
    expect(convert).toHaveBeenCalledWith(expect.objectContaining({ missionId: 'm1', recommendationId: 'r2', streams: ['Platform'] }));
    expect(markConverted).toHaveBeenCalledWith({ id: 'm1', recommendationId: 'r2', topicId: 'top-guid' });
    expect(nav).toHaveBeenCalledWith('/topics/TOP-2026-030');
  });

  it('surfaces a 409 inline and does not navigate', async () => {
    convert.mockRejectedValueOnce(new ApiError(409, { title: 'This recommendation has already been converted to topic TOP-2026-011.' }));
    const user = userEvent.setup();
    setup({ recommendationId: 'r2' });
    await user.type(screen.getByLabelText(/Justification/), 'x');
    await user.type(screen.getByLabelText(/Affected streams/), 'IAM{Enter}');
    await user.click(screen.getByRole('button', { name: 'Convert' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('already been converted');
    expect(nav).not.toHaveBeenCalled();
  });

  it('shows a generic error on a non-ApiError failure', async () => {
    convert.mockRejectedValueOnce(new Error('network'));
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Justification/), 'x');
    await user.type(screen.getByLabelText(/Affected streams/), 'IAM{Enter}');
    await user.click(screen.getByRole('button', { name: 'Convert' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('could not be created');
    expect(nav).not.toHaveBeenCalled();
  });
});
