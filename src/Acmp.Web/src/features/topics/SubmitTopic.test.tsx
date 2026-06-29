import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from 'vitest';
import { render, screen, act, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouterProvider, createMemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import { SubmitTopic } from './SubmitTopic';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import i18n from '../../i18n';

// Spy on the component's programmatic navigation (keeps createMemoryRouter/useBlocker real). This also
// sidesteps a jsdom/undici AbortSignal mismatch when the data router performs a real client navigation.
const navigateSpy = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateSpy };
});

vi.mock('../../api/topics', () => ({ useSubmitTopic: vi.fn(), uploadTopicAttachment: vi.fn() }));
import { useSubmitTopic, uploadTopicAttachment } from '../../api/topics';

const mockUseSubmit = useSubmitTopic as unknown as Mock;
const mockUpload = uploadTopicAttachment as unknown as Mock;
let mutateAsync: Mock;

function setup(initialPath = '/topics/new') {
  const router = createMemoryRouter(
    [
      { path: '/topics/new', element: <SubmitTopic /> },
      { path: '/backlog', element: <div>Backlog page</div> },
      { path: '/topics/:key', element: <div>Detail page</div> },
    ],
    { initialEntries: [initialPath] },
  );
  render(
    <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
      <RouterProvider router={router} />
    </AcmpAuthContext.Provider>,
  );
  return router;
}

describe('SubmitTopic (P5b)', () => {
  beforeEach(() => {
    localStorage.clear();
    navigateSpy.mockReset();
    mutateAsync = vi.fn().mockResolvedValue({ id: 'g1', key: 'TOP-2026-002' });
    mockUseSubmit.mockReturnValue({ mutateAsync, isPending: false });
    mockUpload.mockResolvedValue(undefined);
  });
  afterEach(() => {
    // Keep a valid mutation return between tests: a bare mockReset() leaves useSubmitTopic()
    // returning undefined, which a trailing re-render reads as `.isPending` → a swallowed
    // ErrorBoundary throw in the run log. beforeEach reassigns the return for each test.
    mockUpload.mockReset();
    void i18n.changeLanguage('en');
  });

  it('renders the five sections and the four topic-type cards', () => {
    setup();
    expect(screen.getByRole('heading', { name: 'Submit a topic' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Research/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Arch\. Decision/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Enhancement/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Governance/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Submit for triage' })).toBeInTheDocument();
  });

  it('shows localized required-field errors and does not submit an empty form (AC-030/049)', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Submit for triage' }));
    expect(screen.getByText('Select a topic type.')).toBeInTheDocument();
    expect(screen.getByText('Title is required.')).toBeInTheDocument();
    expect(screen.getByText('Description is required.')).toBeInTheDocument();
    expect(screen.getByText('Add at least one affected stream.')).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it('preserves entered form data across a locale switch (AC-039)', async () => {
    const user = userEvent.setup();
    setup();
    const title = screen.getByLabelText(/Title/);
    await user.type(title, 'Adopt Keycloak');
    await act(async () => {
      await i18n.changeLanguage('ar');
    });
    // The same control, now Arabic-labelled, keeps its value.
    expect((screen.getByLabelText(/العنوان/) as HTMLInputElement).value).toBe('Adopt Keycloak');
  });

  it('guards in-app navigation away from a dirty form and can resume editing (AC-047)', async () => {
    const user = userEvent.setup();
    const router = setup();
    await user.type(screen.getByLabelText(/Title/), 'Half-written topic');
    await act(async () => {
      await router.navigate('/backlog');
    });
    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Leave without submitting?')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Keep editing' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(screen.getByLabelText(/Title/)).toBeInTheDocument(); // still on the form
  });

  it('submits a valid topic with the defaulted source and no scope picker', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Arch\. Decision/ }));
    await user.type(screen.getByLabelText(/Title/), 'Adopt Keycloak');
    await user.type(screen.getByLabelText(/Description/), 'Consolidate IdP.');
    await user.type(screen.getByLabelText(/Why now/), 'Reduces auth sprawl.');
    await user.type(screen.getByLabelText(/Affected streams/), 'identity{Enter}');
    await user.click(screen.getByRole('button', { name: 'Submit for triage' }));
    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1));
    expect(mutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'ArchitectureDecision',
        title: 'Adopt Keycloak',
        source: 'CommitteeMember',
        streams: ['identity'],
        systems: [],
        tags: [],
      }),
    );
    // On success it clears the draft and redirects to the new topic.
    await waitFor(() => expect(navigateSpy).toHaveBeenCalledWith('/topics/TOP-2026-002'));
    expect(localStorage.getItem('acmp-topic-draft-v1')).toBeNull();
  });

  it('rejects an oversized attachment with a localized message', async () => {
    const user = userEvent.setup();
    setup();
    const big = new File([new Uint8Array(2)], 'huge.pdf', { type: 'application/pdf' });
    Object.defineProperty(big, 'size', { value: 51 * 1024 * 1024 });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, big);
    expect(screen.getByText(/50 MB or smaller/)).toBeInTheDocument();
  });

  it('uploads attached files after the topic is created', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Arch\. Decision/ }));
    await user.type(screen.getByLabelText(/Title/), 'Adopt Keycloak');
    await user.type(screen.getByLabelText(/Description/), 'Consolidate IdP.');
    await user.type(screen.getByLabelText(/Why now/), 'Reduces sprawl.');
    await user.type(screen.getByLabelText(/Affected streams/), 'identity{Enter}');
    const file = new File([new Uint8Array(8)], 'spec.pdf', { type: 'application/pdf' });
    await user.upload(document.querySelector('input[type="file"]') as HTMLInputElement, file);
    await user.click(screen.getByRole('button', { name: 'Submit for triage' }));
    await waitFor(() => expect(mockUpload).toHaveBeenCalledWith('g1', file)); // res.id from mutateAsync
  });

  it('starts from an empty form when the saved draft is corrupt', () => {
    localStorage.setItem('acmp-topic-draft-v1', '{not valid json');
    setup();
    expect((screen.getByLabelText(/Title/) as HTMLInputElement).value).toBe(''); // loadDraft caught → EMPTY
  });

  it('autosaves the draft to localStorage after a pause', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Autosaved title');
    await waitFor(
      () => expect(localStorage.getItem('acmp-topic-draft-v1')).toContain('Autosaved title'),
      { timeout: 2000 },
    );
  });

  it('warns before a hard reload while the form is dirty (beforeunload)', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Dirty draft');
    const evt = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(evt);
    expect(evt.defaultPrevented).toBe(true); // guard prevented the unload
  });

  it('formats attachment sizes in KB and MB', async () => {
    const user = userEvent.setup();
    setup();
    const kb = new File([new Uint8Array(1)], 'medium.pdf', { type: 'application/pdf' });
    Object.defineProperty(kb, 'size', { value: 200 * 1024 });
    const mb = new File([new Uint8Array(1)], 'large.pdf', { type: 'application/pdf' });
    Object.defineProperty(mb, 'size', { value: 3 * 1024 * 1024 });
    await user.upload(document.querySelector('input[type="file"]') as HTMLInputElement, [kb, mb]);
    expect(screen.getByText('200 KB')).toBeInTheDocument();
    expect(screen.getByText('3.0 MB')).toBeInTheDocument();
  });

  it('saves a draft to localStorage and leaves when "Save draft" is clicked', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Draft me later');
    await user.click(screen.getByRole('button', { name: i18n.t('submit.saveDraft') }));
    expect(localStorage.getItem('acmp-topic-draft-v1')).not.toBeNull();
    expect(navigateSpy).toHaveBeenCalled();
  });

  it('jumps to a section via the step nav (marks it current)', async () => {
    const user = userEvent.setup();
    setup();
    const nav = screen.getByRole('navigation', { name: i18n.t('submit.sectionsLabel') });
    const steps = within(nav).getAllByRole('button');
    await user.click(steps[1]); // a non-active step
    expect(steps[1]).toHaveAttribute('aria-current', 'step');
  });

  it('surfaces a server error when the submission fails', async () => {
    const user = userEvent.setup();
    mutateAsync.mockRejectedValueOnce(new Error('network down'));
    setup();
    await user.click(screen.getByRole('button', { name: /Arch\. Decision/ }));
    await user.type(screen.getByLabelText(/Title/), 'Adopt Keycloak');
    await user.type(screen.getByLabelText(/Description/), 'Consolidate IdP.');
    await user.type(screen.getByLabelText(/Why now/), 'Reduces sprawl.');
    await user.type(screen.getByLabelText(/Affected streams/), 'identity{Enter}');
    await user.click(screen.getByRole('button', { name: 'Submit for triage' }));
    expect(await screen.findByText(i18n.t('submit.submitError'))).toBeInTheDocument();
  });

  it('adds a valid attachment to the list and removes it', async () => {
    const user = userEvent.setup();
    setup();
    const file = new File([new Uint8Array(8)], 'spec.pdf', { type: 'application/pdf' });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, file);
    expect(screen.getByText('spec.pdf')).toBeInTheDocument(); // file list rendered
    await user.click(screen.getByRole('button', { name: /spec\.pdf/ })); // remove control
    expect(screen.queryByText('spec.pdf')).not.toBeInTheDocument();
  });

  it('removes the last stream token on Backspace when the input is empty', async () => {
    const user = userEvent.setup();
    setup();
    const streams = screen.getByLabelText(/Affected streams/);
    await user.type(streams, 'identity{Enter}');
    expect(screen.getByText('identity')).toBeInTheDocument(); // token added
    streams.focus();
    await user.keyboard('{Backspace}'); // empty draft + Backspace removes the last token
    expect(screen.queryByText('identity')).not.toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    const { container } = (() => {
      setup();
      return { container: document.body };
    })();
    const results = await axe.run(container, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
