import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ConvertToAdrDialog } from './ConvertToAdrDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const promote = vi.hoisted(() => vi.fn().mockResolvedValue({ key: 'ADR-2026-004' }));
vi.mock('../../api/adrs', () => ({
  usePromoteDecisionToAdr: () => ({ mutateAsync: promote, isPending: false }),
}));

function setup() {
  return render(
    <MemoryRouter>
      <ConvertToAdrDialog open onClose={vi.fn()} decisionId="dec-guid" decisionKey="DECN-2026-008" />
    </MemoryRouter>,
  );
}

describe('ConvertToAdrDialog (P11e)', () => {
  beforeEach(() => vi.clearAllMocks());

  it('promotes the decision then navigates to the new ADR', async () => {
    const user = userEvent.setup();
    setup();
    expect(screen.getByText(/pre-filled from DECN-2026-008/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Create ADR' }));
    expect(promote).toHaveBeenCalledWith('dec-guid');
    expect(nav).toHaveBeenCalledWith('/adrs/ADR-2026-004');
  });

  it('surfaces a 409 (already promoted / not issued) and does not navigate', async () => {
    promote.mockRejectedValueOnce(new ApiError(409, { title: 'This decision has already been promoted to ADR ADR-2026-002.' }));
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Create ADR' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('already been promoted');
    expect(nav).not.toHaveBeenCalled();
  });
});
