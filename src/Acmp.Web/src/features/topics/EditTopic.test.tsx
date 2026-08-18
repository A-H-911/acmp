import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { EditTopic } from './EditTopic';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';
import i18n from '../../i18n';
import type { TopicDetail as Topic } from '../../api/topics';
import type { CommitteeRole } from '../../auth/roles';

// Stub the react-query-backed StreamPicker's data hook (the same shape SubmitTopic.test.tsx uses):
// keeps this suite query-free while still rendering the REAL picker, so the chips these tests click
// are the ones users click.
vi.mock('../../api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/members')>()),
  useAssignableStreams: () => ({
    data: [
      { publicId: 's-core', code: 'core', nameEn: 'Core', nameAr: 'الأساسي', isWildcard: false },
      { publicId: 's-gov', code: 'government', nameEn: 'Government', nameAr: 'الحكومي', isWildcard: false },
    ],
    isLoading: false,
    isError: false,
  }),
}));

vi.mock('../../api/topics', () => ({ useTopicDetail: vi.fn(), useUpdateTopic: vi.fn(), useSetTopicConfidentiality: vi.fn() }));
import { useTopicDetail, useUpdateTopic, useSetTopicConfidentiality } from '../../api/topics';

const mockDetail = useTopicDetail as unknown as Mock;
const mockUpdate = useUpdateTopic as unknown as Mock;
const mockClassify = useSetTopicConfidentiality as unknown as Mock;
let mutateAsync: Mock;
let classifyMutate: Mock;

const TOPIC: Topic = {
  id: 'g1', key: 'TOP-2026-014', title: 'Adopt Keycloak as the standard IdP', description: 'Consolidate IdP onto Keycloak.',
  justification: 'Reduce auth sprawl and audit cost.', type: 'ArchitectureDecision', status: 'Submitted', urgency: 'Urgent',
  scope: 'MultiStream', source: 'CommitteeMember', streams: ['core', 'government'], systems: ['Auth Service'], tags: ['iam'],
  ownerId: 'o1', ownerName: 'Omar H', submittedByName: 'Omar H', priority: 1, ageDays: 9, slaBreached: false,
  createdAt: '2026-02-15T09:00:00Z', revisitOn: null, history: [], comments: [], attachments: [],
};

function setup(topic: Partial<Topic> = {}, roles: CommitteeRole[] = ['secretary']) {
  mockDetail.mockReturnValue({
    data: { ...TOPIC, ...topic }, isLoading: false, isError: false, error: null, refetch: vi.fn(),
  });
  render(
    <AcmpAuthContext.Provider value={makeAuth(roles)}>
      <MemoryRouter initialEntries={['/topics/TOP-2026-014/edit']}>
        <Routes>
          <Route path="/topics/:key/edit" element={<EditTopic />} />
          <Route path="/topics/:key" element={<div>DETAIL_PAGE</div>} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('EditTopic (AC-034)', () => {
  beforeEach(() => {
    mockDetail.mockReset();
    mutateAsync = vi.fn().mockResolvedValue(undefined);
    mockUpdate.mockReturnValue({ mutateAsync, isPending: false });
    mockClassify.mockReset();
    classifyMutate = vi.fn();
    mockClassify.mockReturnValue({ mutate: classifyMutate, isPending: false });
  });

  // The whole point of the slice: PUT /api/topics/{id} had no caller at all, so nothing proved the
  // command was reachable from the app.
  it('sends every editable field, because PUT replaces rather than patches', async () => {
    setup();

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    expect(mutateAsync).toHaveBeenCalledWith({
      topicId: 'g1',
      edit: expect.objectContaining({
        title: TOPIC.title,
        description: TOPIC.description,
        justification: TOPIC.justification,
        urgency: 'Urgent',
        streams: ['core', 'government'],
        systems: ['Auth Service'],
        tags: ['iam'],
      }),
    });
  });

  // DEF-058. ⚠ Asserted as ABSENT rather than "equal to the current scope": the server reads a
  // missing scope as "leave it alone" and skips the triage check only then, so sending the unchanged
  // value would cost an ordinary edit an authorization it does not need.
  it('omits scope entirely when it was not changed', async () => {
    setup();

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    expect(mutateAsync.mock.calls[0][0].edit).not.toHaveProperty('scope');
  });

  it('sends the new scope when the secretary elevates it', async () => {
    setup();

    await userEvent.click(screen.getByRole('button', { name: /organization-wide/i }));
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    expect(mutateAsync.mock.calls[0][0].edit.scope).toBe('OrgWide');
  });

  // Elevating scope widens write access to every stream-bounded member (ADR-0043 clause 5), so it is
  // a triage act. The server is the gate; not rendering the control keeps a Member from being
  // offered a button that can only ever 403.
  // FR-163 / C-AUTHZ-04 (DEC-063 d2). Both halves asserted: the control appears for the two roles that
  // may classify, and is ABSENT for everyone else. Presence alone would pass against a control offered
  // to a Member, who would then be handed a 403 the UI could have prevented.
  it('offers the confidentiality toggle to a secretary and posts the flipped state', async () => {
    setup({}, ['secretary']);

    // ⚠ The accessible name is the FIELD LABEL, because Field associates its <label> with the
    // control's id — so a screen reader announces "Confidentiality, not pressed", and the visible
    // text carries the human-readable state. Both are asserted; querying by the visible text alone
    // would have silently depended on the label association NOT existing.
    const toggle = screen.getByRole('button', { name: /confidentiality/i });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    expect(toggle).toHaveTextContent(/not restricted/i);

    await userEvent.click(toggle);

    expect(classifyMutate).toHaveBeenCalledWith({ topicId: TOPIC.id, restricted: true });
  });

  it('shows the toggle as pressed for an already-restricted topic', () => {
    setup({ restricted: true }, ['secretary']);

    const toggle = screen.getByRole('button', { name: /confidentiality/i });
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
    expect(toggle).toHaveTextContent(/^\s*Restricted\s*$/);
  });

  it('does not offer the confidentiality toggle to a member', () => {
    setup({}, ['member']);

    // DEC-063 d2 excludes the OWNER on purpose — a plain Member must not be able to hide a topic
    // from the committee, so the control is not offered even on their own topic.
    expect(screen.queryByRole('button', { name: /confidentiality/i })).not.toBeInTheDocument();
  });

  it('does not offer the scope control to a member', () => {
    setup({}, ['member']);

    expect(screen.queryByRole('button', { name: /organization-wide/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save changes/i })).toBeInTheDocument();
  });

  // AC-034: content freezes at Acceptance, metadata stays editable until a Decision. Asserted through
  // the DISABLED state rather than absence — a locked field the user can still see is an
  // explanation, and `fieldset[disabled]` is what actually enforces it in the browser.
  it('locks the content fields once the topic is accepted but leaves metadata editable', () => {
    setup({ status: 'Accepted' });

    expect(screen.getByLabelText(/^title/i)).toBeDisabled();
    expect(screen.getByRole('button', { name: /organization-wide/i })).toBeEnabled();
    expect(screen.getByRole('button', { name: /save changes/i })).toBeEnabled();
  });

  it('locks everything once the topic is decided, and says why', () => {
    setup({ status: 'Decided' });

    expect(screen.getByLabelText(/^title/i)).toBeDisabled();
    expect(screen.getByRole('button', { name: /save changes/i })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent(/no longer be edited/i);
  });

  // Mirrors the server guard (DEF-059): a live topic must affect at least one stream. Proven by the
  // request NOT going out — a client-side message that still submits is worse than none.
  it('refuses to save a topic with no streams', async () => {
    setup();

    for (const chip of screen.getAllByRole('button', { pressed: true })) {
      if (/^core$|^government$/i.test(chip.textContent ?? '')) await userEvent.click(chip);
    }
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    expect(mutateAsync).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent(/at least one affected stream/i);
  });

  it('returns to the topic after a successful save', async () => {
    setup();

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    expect(await screen.findByText('DETAIL_PAGE')).toBeInTheDocument();
  });

  // A refusal must be SHOWN, not swallowed into a navigation that implies the save worked. The
  // server can refuse for a reason the form cannot predict — a non-triage actor changing scope, or
  // a topic that moved to Decided between load and save.
  it('shows a save failure and stays on the form', async () => {
    mutateAsync = vi.fn().mockRejectedValue(new ApiError(403, undefined));
    mockUpdate.mockReturnValue({ mutateAsync, isPending: false });
    setup();

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn’t save/i);
    expect(screen.queryByText('DETAIL_PAGE')).not.toBeInTheDocument();
  });

  it('renders the loading and not-found states rather than an empty form', () => {
    mockDetail.mockReturnValue({ data: undefined, isLoading: true, isError: false, error: null, refetch: vi.fn() });
    const { unmount } = render(
      <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
        <MemoryRouter initialEntries={['/topics/TOP-2026-014/edit']}>
          <Routes><Route path="/topics/:key/edit" element={<EditTopic />} /></Routes>
        </MemoryRouter>
      </AcmpAuthContext.Provider>,
    );
    expect(screen.queryByRole('button', { name: /save changes/i })).not.toBeInTheDocument();
    unmount();

    mockDetail.mockReturnValue({
      data: undefined, isLoading: false, isError: true, error: new ApiError(404, undefined), refetch: vi.fn(),
    });
    render(
      <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
        <MemoryRouter initialEntries={['/topics/NOPE/edit']}>
          <Routes><Route path="/topics/:key/edit" element={<EditTopic />} /></Routes>
        </MemoryRouter>
      </AcmpAuthContext.Provider>,
    );
    expect(screen.getByText(/topic not found/i)).toBeInTheDocument();
  });

  it('offers a retry when the topic could not be loaded at all', () => {
    const refetch = vi.fn();
    mockDetail.mockReturnValue({ data: undefined, isLoading: false, isError: true, error: new Error('boom'), refetch });
    render(
      <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
        <MemoryRouter initialEntries={['/topics/TOP-2026-014/edit']}>
          <Routes><Route path="/topics/:key/edit" element={<EditTopic />} /></Routes>
        </MemoryRouter>
      </AcmpAuthContext.Provider>,
    );

    expect(screen.getByRole('button', { name: /retry|try again/i })).toBeInTheDocument();
  });

  // Guardrail 9 / DEF-064. check-i18n.mjs compares KEY SETS, so "parity OK" is true and meaningless
  // about the VALUES — it passes just as happily over a mojibake string or an English one pasted into
  // ar.json. This renders the screen in Arabic and reads the text back, which is the only check that
  // fails when the Arabic half is wrong.
  it('renders its own Arabic strings, not an English fallback', async () => {
    await i18n.changeLanguage('ar');
    try {
      setup();
      expect(screen.getByRole('button', { name: 'حفظ التغييرات' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('تعديل TOP-2026-014');
      expect(screen.getByRole('button', { name: 'على مستوى المؤسسة' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('has no detectable accessibility violations', async () => {
    setup();

    const results = await axe.run(document.body, { rules: { region: { enabled: false } } });

    expect(results.violations).toEqual([]);
  });
});
