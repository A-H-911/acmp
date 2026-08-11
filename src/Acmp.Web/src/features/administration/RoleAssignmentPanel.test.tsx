import { describe, it, expect, afterEach, vi } from 'vitest';
import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import type { ReactElement } from 'react';
import i18n from '../../i18n';
import { RoleAssignmentPanel } from './RoleAssignmentPanel';
import { renderWithAuth } from '../../test/render';
import { makeQueryWrapper, stubFetch } from '../../test/queryHarness';
import type { Member } from '../../api/members';

/*
 * FR-157 / AC-089 — the role-assignment panel.
 *
 * These run the REAL useAssignRoles against a stubbed fetch rather than mocking the hook, so every
 * assertion below is about what reached (or did not reach) the WIRE. That distinction is the whole
 * point of guard 2: a test that asserted "the confirm handler ran" would pass just as happily on a
 * cosmetic dialog that submitted anyway.
 */

const MEMBER: Member = {
  publicId: 'm-1',
  keycloakUserId: 'kc-1',
  fullName: 'Omar H',
  email: 'omar@acmp.gov',
  role: 'Member',
  status: 'Active',
  isActive: true,
  isVotingEligible: true,
  streams: [],
};

function renderPanel(ui: ReactElement = <RoleAssignmentPanel member={MEMBER} />) {
  const { wrapper: Query } = makeQueryWrapper();
  return renderWithAuth(<Query>{ui}</Query>, { roles: ['administrator'] });
}

/** Open the role listbox and pick an option by its visible label. */
async function chooseRole(user: ReturnType<typeof userEvent.setup>, label: string) {
  await user.click(screen.getByRole('button', { name: /committee role/i }));
  await user.click(screen.getByRole('option', { name: label }));
}

/** The bodies of every PUT that reached the wire. */
function sentBodies(spy: ReturnType<typeof stubFetch>): unknown[] {
  return spy.mock.calls
    .filter((c) => (c[1] as RequestInit | undefined)?.method === 'PUT')
    .map((c) => JSON.parse(String((c[1] as RequestInit).body)));
}

// cleanup() FIRST, deliberately: this file's afterEach runs before the setup file's auto-cleanup,
// so switching the language here would re-render still-mounted components outside act() and every
// test in the file would emit an act warning — attributed to whichever test happened to be running.
afterEach(async () => {
  cleanup();
  vi.unstubAllGlobals();
  await i18n.changeLanguage('en');
});

describe('RoleAssignmentPanel (FR-157 / AC-089)', () => {
  it('sends the chosen role as the whole set, unconfirmed, for an ordinary role', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const user = userEvent.setup();
    renderPanel();

    await chooseRole(user, 'Reviewer');
    await user.click(screen.getByRole('button', { name: 'Save role' }));

    expect(await screen.findByRole('status')).toHaveTextContent(/Role saved/);
    expect(sentBodies(spy)).toEqual([{ roles: ['Reviewer'], confirmedPrivileged: false }]);
  });

  // GUARD 2, FORCED. The gate is proven by what does NOT reach the server: selecting a privileged
  // role and pressing Save must produce zero requests. If the dialog were cosmetic this fails.
  it.each(['Administrator', 'Chairman'])('sends NOTHING when %s is chosen until it is confirmed', async (role) => {
    const spy = stubFetch(() => ({ status: 204 }));
    const user = userEvent.setup();
    renderPanel();

    await chooseRole(user, role);
    await user.click(screen.getByRole('button', { name: 'Save role' }));

    expect(sentBodies(spy)).toEqual([]);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('still sends nothing when the privileged confirmation is cancelled', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const user = userEvent.setup();
    renderPanel();

    await chooseRole(user, 'Administrator');
    await user.click(screen.getByRole('button', { name: 'Save role' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }));

    expect(sentBodies(spy)).toEqual([]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('sends confirmedPrivileged only after the confirmation is actually given', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const user = userEvent.setup();
    renderPanel();

    await chooseRole(user, 'Administrator');
    await user.click(screen.getByRole('button', { name: 'Save role' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Grant this role' }));

    expect(await screen.findByRole('status')).toBeInTheDocument();
    expect(sentBodies(spy)).toEqual([{ roles: ['Administrator'], confirmedPrivileged: true }]);
  });

  // The condition is "the set being SENT is privileged", mirroring the server — not "the member is
  // gaining privilege". Someone who is already an Administrator moving to Chairman is not becoming
  // more privileged, and the server still refuses that request without the flag.
  it('confirms a privileged→privileged move, not only a promotion', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const user = userEvent.setup();
    renderPanel(<RoleAssignmentPanel member={{ ...MEMBER, role: 'Administrator' }} />);

    await chooseRole(user, 'Chairman');
    await user.click(screen.getByRole('button', { name: 'Save role' }));

    expect(sentBodies(spy)).toEqual([]);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('does not offer a save while nothing has changed (a no-op save would sign the person out)', () => {
    stubFetch(() => ({ status: 204 }));
    renderPanel();

    expect(screen.getByRole('button', { name: 'Save role' })).toBeDisabled();
  });
});

describe('RoleAssignmentPanel — server refusals are surfaced, not pre-empted (AC-089)', () => {
  // ⚠ The control stays live for a member the server will refuse. Pre-hiding it would move the rule
  // into the SPA, where it is presentation gating and enforces nothing; the refusal below is the
  // server's, and the UI's job is to say what it was.
  it('names the self-change guard when the server answers 403', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    const user = userEvent.setup();
    renderPanel();

    await chooseRole(user, 'Reviewer');
    await user.click(screen.getByRole('button', { name: 'Save role' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/cannot change your own roles/i);
  });

  it('names the last-Administrator guard when the server answers 409', async () => {
    stubFetch(() => ({ status: 409, jsonBody: { title: 'Conflict' } }));
    const user = userEvent.setup();
    renderPanel(<RoleAssignmentPanel member={{ ...MEMBER, role: 'Administrator' }} />);

    await chooseRole(user, 'Reviewer');
    await user.click(screen.getByRole('button', { name: 'Save role' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/left with no Administrator/i);
  });

  it('falls back to the server title for any other failure', async () => {
    stubFetch(() => ({ status: 500, jsonBody: { title: 'An unexpected error occurred' } }));
    const user = userEvent.setup();
    renderPanel();

    await chooseRole(user, 'Reviewer');
    await user.click(screen.getByRole('button', { name: 'Save role' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('An unexpected error occurred');
  });

  it('clears a stale refusal when a different role is picked', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    const user = userEvent.setup();
    renderPanel();

    await chooseRole(user, 'Reviewer');
    await user.click(screen.getByRole('button', { name: 'Save role' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();

    await chooseRole(user, 'Auditor');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('RoleAssignmentPanel — i18n and accessibility', () => {
  // check-i18n compares KEYS only, so a key with an English value passes it and renders English to
  // an Arabic reader. This asserts the VALUES exist in Arabic.
  it('renders in Arabic, including the privileged confirmation', async () => {
    stubFetch(() => ({ status: 204 }));
    await i18n.changeLanguage('ar');
    const user = userEvent.setup();
    renderPanel();

    expect(screen.getAllByText('دور اللجنة').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'حفظ الدور' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /دور اللجنة/ }));
    await user.click(screen.getByRole('option', { name: 'مسؤول النظام' }));
    await user.click(screen.getByRole('button', { name: 'حفظ الدور' }));

    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByRole('button', { name: 'منح هذا الدور' })).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'إلغاء' })).toBeInTheDocument();
  });

  it('is axe-clean, including the open confirmation dialog', async () => {
    stubFetch(() => ({ status: 204 }));
    const user = userEvent.setup();
    renderPanel();

    await chooseRole(user, 'Administrator');
    await user.click(screen.getByRole('button', { name: 'Save role' }));

    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
