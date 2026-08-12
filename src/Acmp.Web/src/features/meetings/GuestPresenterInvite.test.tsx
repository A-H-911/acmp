import { describe, it, expect, afterEach, vi } from 'vitest';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GuestPresenterInvite } from './GuestPresenterInvite';
import { renderWithAuth } from '../../test/render';
import { makeQueryWrapper, stubFetch, lastBody } from '../../test/queryHarness';

/*
 * FR-159 / AC-092 — the Secretary's guest-presenter invite.
 *
 * These run the REAL useInviteGuestPresenter against a stubbed fetch, so what is asserted is what
 * reached the WIRE and what came back off it. A test that mocked the hook could not tell a panel
 * that invites from one that only looks like it does.
 */

const INVITED = {
  publicId: 'g-1',
  fullName: 'Nadia Presenter',
  email: 'nadia@vendor.example',
  accessExpiresAt: '2026-09-02T10:30:00Z',
  temporaryPassword: 'Temp-Passw0rd!',
};

function renderInvite() {
  const { wrapper: Query } = makeQueryWrapper();
  return renderWithAuth(
    <Query>
      <GuestPresenterInvite meetingKey="MTG-2026-001" meetingId="m-1" topicId="t-1" topicKey="TOP-2026-022" />
    </Query>,
    { roles: ['secretary'] },
  );
}

async function openAndFill(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: /invite a guest/i }));
  await user.type(screen.getByLabelText(/email/i), 'nadia@vendor.example');
  await user.type(screen.getByLabelText(/full name/i), 'Nadia Presenter');
}

describe('GuestPresenterInvite (FR-159)', () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('sends the slot and the guest to the meeting-scoped invite endpoint', async () => {
    const user = userEvent.setup();
    const spy = stubFetch(() => ({ jsonBody: INVITED }));
    renderInvite();

    await openAndFill(user);
    await user.click(screen.getByRole('button', { name: /invite guest/i }));

    // The URL carries the MEETING and the body carries the SLOT: the window is derived server-side
    // from that meeting, so a client that could name its own expiry would defeat the point.
    expect(String(spy.mock.calls[0][0])).toContain('/meetings/m-1/guest-presenters');
    expect(lastBody(spy)).toEqual({ topicId: 't-1', email: 'nadia@vendor.example', fullName: 'Nadia Presenter' });
  });

  it('shows the exact expiry instant and the one-time password after inviting', async () => {
    const user = userEvent.setup();
    stubFetch(() => ({ jsonBody: INVITED }));
    renderInvite();

    await openAndFill(user);
    await user.click(screen.getByRole('button', { name: /invite guest/i }));

    expect(await screen.findByText('Temp-Passw0rd!')).toBeInTheDocument();
    // The instant, not "after the meeting" — the inviter can see exactly what they granted.
    expect(screen.getByText('Access ends')).toBeInTheDocument();
    // Locale- and timezone-formatted, so assert the month and year rather than a literal string.
    expect(screen.getByText(/Sep.*2026/i)).toBeInTheDocument();
  });

  it('discards the credential when the dialog is closed', async () => {
    const user = userEvent.setup();
    stubFetch(() => ({ jsonBody: INVITED }));
    renderInvite();

    await openAndFill(user);
    await user.click(screen.getByRole('button', { name: /invite guest/i }));
    expect(await screen.findByText('Temp-Passw0rd!')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^done$/i }));

    // Gone, and re-opening does not bring it back: it exists only for the life of the panel.
    expect(screen.queryByText('Temp-Passw0rd!')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /invite a guest/i }));
    expect(screen.queryByText('Temp-Passw0rd!')).not.toBeInTheDocument();
  });

  it('surfaces a refusal instead of reporting success', async () => {
    const user = userEvent.setup();
    stubFetch(() => ({ status: 403, jsonBody: { error: 'forbidden' } }));
    renderInvite();

    await openAndFill(user);
    await user.click(screen.getByRole('button', { name: /invite guest/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not be invited/i);
    expect(screen.queryByText('Temp-Passw0rd!')).not.toBeInTheDocument();
  });

  it('refuses to submit until both fields are filled', async () => {
    const user = userEvent.setup();
    const spy = stubFetch(() => ({ jsonBody: INVITED }));
    renderInvite();

    await user.click(screen.getByRole('button', { name: /invite a guest/i }));
    await user.type(screen.getByLabelText(/email/i), 'nadia@vendor.example');

    expect(screen.getByRole('button', { name: /invite guest/i })).toBeDisabled();
    expect(spy).not.toHaveBeenCalled();
  });

  it('copies the password to the clipboard on request', async () => {
    // AFTER userEvent.setup(), which installs a clipboard stub of its own and would overwrite this.
    const user = userEvent.setup();
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    stubFetch(() => ({ jsonBody: INVITED }));
    renderInvite();

    await openAndFill(user);
    await user.click(screen.getByRole('button', { name: /invite guest/i }));
    await user.click(await screen.findByRole('button', { name: /^copy$/i }));

    expect(writeText).toHaveBeenCalledWith('Temp-Passw0rd!');
    expect(await screen.findByRole('button', { name: /copied/i })).toBeInTheDocument();
  });
});
