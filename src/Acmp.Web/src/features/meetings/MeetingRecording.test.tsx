import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from 'vitest';
import { render, screen, cleanup, within, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { MeetingRecording } from './MeetingRecording';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import type { CommitteeRole } from '../../auth/roles';
import type { MeetingDetail, RecordingDto } from '../../api/meetings';

vi.mock('../../api/meetings', async () => {
  const actual = await vi.importActual<typeof import('../../api/meetings')>('../../api/meetings');
  return {
    ...actual,
    useMeetingDetail: vi.fn(),
    useRecordingUrl: vi.fn(),
    useUploadMeetingRecording: vi.fn(),
    useDeleteMeetingRecording: vi.fn(),
    downloadMeetingRecording: vi.fn(),
  };
});
// AC-169: the limits hook reads GET /api/uploads/limits; here it answers with the shipped defaults.
vi.mock('../../api/uploads', async (importOriginal) => {
  const real = await importOriginal<typeof import('../../api/uploads')>();
  return { ...real, useUploadLimits: () => real.DEFAULT_UPLOAD_LIMITS };
});

import { useMeetingDetail, useRecordingUrl, useUploadMeetingRecording, useDeleteMeetingRecording, downloadMeetingRecording } from '../../api/meetings';
import { ApiError } from '../../api/apiClient';

const mockDetail = useMeetingDetail as unknown as Mock;
const mockUrl = useRecordingUrl as unknown as Mock;
const mockUpload = useUploadMeetingRecording as unknown as Mock;
const mockDelete = useDeleteMeetingRecording as unknown as Mock;

function meeting(recording: RecordingDto | null = null): MeetingDetail {
  return {
    id: 'm1', key: 'MTG-2026-001', title: 'Q2 Review', committeeId: 'c1',
    scheduledStart: '2026-06-30T09:00:00Z', scheduledEnd: '2026-06-30T10:30:00Z',
    status: 'Held', type: 'Regular', mode: 'Remote', location: null, joinUrl: null,
    chairUserId: 'u1', chairName: 'Sara K', startedAt: null, heldAt: null,
    agenda: null, attendance: [], discussions: [], recording,
  };
}
const uploaded: RecordingDto = { source: 'Uploaded', fileName: 'board.mp4', contentType: 'video/mp4', sizeBytes: 4096, durationSeconds: null, playbackUrl: null };
const webex: RecordingDto = { source: 'Webex', fileName: null, contentType: null, sizeBytes: null, durationSeconds: null, playbackUrl: 'https://webex/play' };

function renderTab(roles: CommitteeRole[]) {
  return render(
    <AcmpAuthContext.Provider value={makeAuth(roles)}>
      <MemoryRouter initialEntries={['/meetings/MTG-2026-001/recording']}>
        <Routes>
          <Route path="/meetings/:key/recording" element={<MeetingRecording />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  mockUrl.mockReturnValue({ data: undefined });
  mockUpload.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue({}), isPending: false, isError: false });
  mockDelete.mockReturnValue({ mutate: vi.fn(), isPending: false });
});
afterEach(cleanup);

describe('MeetingRecording', () => {
  it('shows the upload form for a secretary when there is no recording', () => {
    mockDetail.mockReturnValue({ data: meeting(null) });
    renderTab(['secretary']);
    expect(screen.getByRole('button', { name: /upload recording/i })).toBeInTheDocument();
  });

  it('shows an empty state and no upload control for a plain member', () => {
    mockDetail.mockReturnValue({ data: meeting(null) });
    renderTab(['member']);
    expect(screen.queryByRole('button', { name: /upload recording/i })).not.toBeInTheDocument();
    expect(screen.getByText(/no recording yet/i)).toBeInTheDocument();
  });

  it('renders the player, "Uploaded" source chip, and manage controls for an uploaded recording', () => {
    mockDetail.mockReturnValue({ data: meeting(uploaded) });
    mockUrl.mockReturnValue({ data: { url: 'https://minio.test/signed' } });
    const { container } = renderTab(['secretary']);
    expect(container.querySelector('video')).toHaveAttribute('src', 'https://minio.test/signed');
    expect(screen.getByText('Uploaded')).toBeInTheDocument();
    expect(screen.getByText('board.mp4')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /replace/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument();
  });

  it('renders a Webex link and "Webex" source chip; a member gets no delete', () => {
    mockDetail.mockReturnValue({ data: meeting(webex) });
    renderTab(['member']);
    expect(screen.getByRole('link', { name: /open webex recording/i })).toHaveAttribute('href', 'https://webex/play');
    expect(screen.getByText('Webex')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
  });

  it('deletes a recording after confirming in the dialog', async () => {
    const mutate = vi.fn();
    mockDelete.mockReturnValue({ mutate, isPending: false });
    mockDetail.mockReturnValue({ data: meeting(uploaded) });
    mockUrl.mockReturnValue({ data: { url: 'https://minio.test/signed' } });
    const user = userEvent.setup();
    renderTab(['secretary']);

    await user.click(screen.getByRole('button', { name: /delete/i })); // trigger
    const dialog = screen.getByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /delete/i })); // confirm
    expect(mutate).toHaveBeenCalled();
  });

  // The file input's onChange is where an upload actually BEGINS, and nothing had ever fired it.
  // Asserted on the mutation argument: a picker that opens but hands the mutation nothing is
  // indistinguishable from a working one until someone tries to upload.
  it('starts the upload with the chosen file', async () => {
    const mutate = vi.fn();
    mockUpload.mockReturnValue({ mutateAsync: mutate.mockResolvedValue({}), isPending: false, isError: false });
    mockDetail.mockReturnValue({ data: meeting(null) });
    const user = userEvent.setup();
    renderTab(['secretary']);

    // The visible CTA only forwards a click to the hidden input, so both are exercised: the button
    // first (its own handler), then the input, which is what the OS picker would have driven.
    await user.click(screen.getByRole('button', { name: /upload/i }));
    const file = new File(['x'], 'board.mp4', { type: 'video/mp4' });
    await user.upload(screen.getByLabelText(/upload/i, { selector: 'input[type="file"]' }), file);

    expect(mutate).toHaveBeenCalledWith(expect.objectContaining({ file }));
  });

  it('offers Replace on an existing recording and re-opens the picker', async () => {
    const mutate = vi.fn();
    mockUpload.mockReturnValue({ mutateAsync: mutate.mockResolvedValue({}), isPending: false, isError: false });
    mockDetail.mockReturnValue({ data: meeting(uploaded) });
    mockUrl.mockReturnValue({ data: { url: 'https://minio.test/signed' } });
    const user = userEvent.setup();
    renderTab(['secretary']);

    await user.click(screen.getByRole('button', { name: /replace/i }));
    const file = new File(['y'], 'board-v2.mp4', { type: 'video/mp4' });
    await user.upload(screen.getByLabelText(/upload/i, { selector: 'input[type="file"]' }), file);

    expect(mutate).toHaveBeenCalledWith(expect.objectContaining({ file }));
  });

  // AC-166: the page states the server's limit and refuses a larger file before sending any bytes.
  it('states the limit and refuses an over-limit recording in the browser', async () => {
    const mutate = vi.fn();
    mockUpload.mockReturnValue({ mutateAsync: mutate, isPending: false, isError: false });
    mockDetail.mockReturnValue({ data: meeting(null) });
    const user = userEvent.setup();
    renderTab(['secretary']);

    expect(screen.getByText(/up to 2,048 MB/)).toBeInTheDocument();
    const big = new File(['x'], 'huge.mp4', { type: 'video/mp4' });
    Object.defineProperty(big, 'size', { value: 2 * 1024 ** 3 + 1 });
    await user.upload(screen.getByLabelText(/upload/i, { selector: 'input[type="file"]' }), big);

    expect(screen.getByRole('alert')).toHaveTextContent(/2,048 MB or smaller/);
    expect(mutate).not.toHaveBeenCalled();
  });

  // AC-166: a running upload shows its percentage, and Replace, Delete and Download wait for it (a download link
  // followed mid-upload could unload the page and abort the upload).
  it('shows the percentage while a replacement uploads and locks Replace, Delete and Download', async () => {
    let report: (p: number) => void = () => {};
    mockUpload.mockReturnValue({
      mutateAsync: vi.fn(({ onProgress }: { onProgress: (p: number) => void }) => { report = onProgress; return new Promise(() => {}); }),
      isPending: false, isError: false,
    });
    mockDetail.mockReturnValue({ data: meeting(uploaded) });
    mockUrl.mockReturnValue({ data: { url: 'https://minio.test/signed' } });
    const user = userEvent.setup();
    renderTab(['secretary']);

    await user.upload(screen.getByLabelText(/upload/i, { selector: 'input[type="file"]' }), new File(['y'], 'board-v2.mp4', { type: 'video/mp4' }));
    act(() => report(55));

    expect(screen.getByRole('progressbar', { name: 'board-v2.mp4' })).toHaveAttribute('aria-valuenow', '55');
    expect(screen.getByRole('button', { name: /delete/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /replace/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /download/i })).toBeDisabled();
  });

  // AC-166 / DEF-173: a refused REPLACE shows the server's translated reason (it used to show nothing).
  it('shows the server reason when a replacement is refused', async () => {
    mockUpload.mockReturnValue({
      mutateAsync: vi.fn().mockRejectedValue(new ApiError(400, { title: 'Bad', errors: [{ errorCode: 'FILE_TYPE_NOT_ALLOWED' }] } as never)),
      isPending: false, isError: false,
    });
    mockDetail.mockReturnValue({ data: meeting(uploaded) });
    mockUrl.mockReturnValue({ data: { url: 'https://minio.test/signed' } });
    const user = userEvent.setup();
    renderTab(['secretary']);

    await user.upload(screen.getByLabelText(/upload/i, { selector: 'input[type="file"]' }), new File(['y'], 'board-v2.mp4', { type: 'video/mp4' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('That file type isn’t allowed.');
  });

  // AC-165: Download mints its link on click (never the player's cached one) and shows a failure.
  it('downloads through a link fetched on click and shows a failure', async () => {
    (downloadMeetingRecording as unknown as Mock).mockRejectedValueOnce(new ApiError(403));
    mockDetail.mockReturnValue({ data: meeting(uploaded) });
    mockUrl.mockReturnValue({ data: { url: 'https://minio.test/signed' } });
    const user = userEvent.setup();
    renderTab(['auditor']);

    await user.click(screen.getByRole('button', { name: /download/i }));

    expect(downloadMeetingRecording).toHaveBeenCalledWith('MTG-2026-001');
    expect(await screen.findByRole('alert')).toHaveTextContent('The recording could not be downloaded.');
  });

  // Backing out of the delete confirm had never run. A recording is the meeting's only durable
  // record of what was said; the dialog exists so the destructive click is reversible until it is
  // confirmed, and an unreachable Cancel makes it a one-way door.
  it('cancels the delete confirmation without deleting', async () => {
    const mutate = vi.fn();
    mockDelete.mockReturnValue({ mutate, isPending: false });
    mockDetail.mockReturnValue({ data: meeting(uploaded) });
    mockUrl.mockReturnValue({ data: { url: 'https://minio.test/signed' } });
    const user = userEvent.setup();
    renderTab(['secretary']);

    await user.click(screen.getByRole('button', { name: /delete/i }));
    const dialog = screen.getByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /cancel/i }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  // Escape is the Dialog's OWN onClose, a different handler from the footer Cancel button above -
  // and it is the dismissal a keyboard user reaches for.
  it('dismisses the delete confirmation with Escape', async () => {
    const mutate = vi.fn();
    mockDelete.mockReturnValue({ mutate, isPending: false });
    mockDetail.mockReturnValue({ data: meeting(uploaded) });
    mockUrl.mockReturnValue({ data: { url: 'https://minio.test/signed' } });
    const user = userEvent.setup();
    renderTab(['secretary']);

    await user.click(screen.getByRole('button', { name: /delete/i }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  /*
   * safeHttps has three ways to say no and only two had run: null, and a non-https scheme. The
   * third is a string `new URL()` THROWS on, which an unparseable Webex playbackUrl would be.
   * ⚠ This is a security guard, not a formatting one - it is what stops a non-https or malformed
   * value reaching an href - so its refusal is the behaviour worth proving, by forcing it.
   */
  it('renders no link when the Webex playback URL is not a URL at all', () => {
    mockDetail.mockReturnValue({
      data: meeting({ ...webex, playbackUrl: 'not a url' }),
    });
    renderTab(['secretary']);

    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  // The shell owns loading and error, so this tab renders NOTHING rather than its own empty state
  // while the detail is still resolving - otherwise every meeting flashes "no recording" first.
  it('renders nothing until the meeting detail resolves', () => {
    mockDetail.mockReturnValue({ data: undefined });
    const { container } = renderTab(['secretary']);

    expect(container).toBeEmptyDOMElement();
  });

  it('has no accessibility violations in the upload state', async () => {
    mockDetail.mockReturnValue({ data: meeting(null) });
    const { container } = renderTab(['secretary']);
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
