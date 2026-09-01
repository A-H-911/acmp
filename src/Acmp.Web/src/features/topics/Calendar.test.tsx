import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import { MemoryRouter } from 'react-router-dom';
import i18n from '../../i18n';
import { Calendar } from './Calendar';

/**
 * The month NAVIGATION assertions below are the originals and are deliberately unchanged:
 * they assert PROPERTIES rather than recomputing the component's own Intl.DateTimeFormat
 * call, because reproducing the formatting here would mean the test agrees with the
 * implementation by construction and would still pass if both were wrong.
 *
 * What is new is the markers (FR-035 / DW-037). The dates come from the MEETINGS API — the
 * topic read model carries no scheduled date at all — so the API is mocked at that boundary.
 */
vi.mock('../../api/meetings', () => ({
  useMeetings: vi.fn(),
  useMeetingDetail: vi.fn(),
  useAgendaProjection: vi.fn(),
}));
import { useMeetings, useMeetingDetail, useAgendaProjection } from '../../api/meetings';

const meetingsMock = useMeetings as unknown as Mock;
const detailMock = useMeetingDetail as unknown as Mock;
const projectionMock = useAgendaProjection as unknown as Mock;

/** A meeting on a fixed day of the CURRENT month, so the default view always contains it. */
function meetingOn(day: number, over: Record<string, unknown> = {}) {
  const now = new Date();
  const d = new Date(now.getFullYear(), now.getMonth(), day, 10, 0, 0);
  return {
    id: `m-${day}`,
    key: `MTG-${day}`,
    title: `Committee session ${day}`,
    scheduledStart: d.toISOString(),
    scheduledEnd: d.toISOString(),
    status: 'Scheduled',
    type: 'Regular',
    mode: 'InPerson',
    chairName: 'Chair',
    itemCount: 3,
    agendaStatus: 'Published',
    ...over,
  };
}

const agenda = (items: { topicId: string; topicKey: string; topicTitle: string }[]) => ({
  agenda: { id: 'a1', key: 'AG-1', status: 'Published', version: 1, totalTimeboxMinutes: 0, publishedAt: null, items },
});

beforeEach(() => {
  meetingsMock.mockReset();
  detailMock.mockReset();
  projectionMock.mockReset();
  meetingsMock.mockReturnValue({ data: [] });
  detailMock.mockReturnValue({ data: undefined, isLoading: false });
  projectionMock.mockReturnValue({ data: [] });
});

function setup() {
  return render(
    <MemoryRouter>
      <I18nextProvider i18n={i18n}>
        <Calendar />
      </I18nextProvider>
    </MemoryRouter>,
  );
}

function monthLabel(): string {
  // The month caption is the only .cal-month element in the frame.
  const el = document.querySelector('.cal-month');
  if (!el?.textContent) throw new Error('month caption not rendered - the frame changed shape');
  return el.textContent;
}

describe('Calendar month navigation', () => {
  it('steps forward a month from the next control', async () => {
    setup();
    const start = monthLabel();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('topics.calendar.next') }));
    expect(monthLabel()).not.toBe(start);
  });

  it('steps back a month from the previous control', async () => {
    setup();
    const start = monthLabel();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('topics.calendar.prev') }));
    expect(monthLabel()).not.toBe(start);
  });

  it('returns to the starting month when a step forward is undone', async () => {
    setup();
    const start = monthLabel();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('topics.calendar.next') }));
    await userEvent.click(screen.getByRole('button', { name: i18n.t('topics.calendar.prev') }));
    expect(monthLabel()).toBe(start);
  });
});

describe('Calendar markers (FR-035)', () => {
  it('says plainly when the month holds no meetings, instead of showing an empty grid', () => {
    setup();
    expect(screen.getByRole('note')).toHaveTextContent(i18n.t('topics.calendar.noneThisMonth'));
    expect(document.querySelectorAll('.cal-event')).toHaveLength(0);
  });

  it('places a chip on the scheduled day carrying the meeting and its topic count', () => {
    meetingsMock.mockReturnValue({ data: [meetingOn(12)] });
    setup();
    const chip = screen.getByRole('button', { name: /Committee session 12/ });
    expect(chip).toHaveTextContent('3');
    // The count is a bare numeral with a label, so no plural agreement is required in either
    // language; the label is what a screen reader reads.
    expect(chip).toHaveTextContent(i18n.t('topics.calendar.agendaItems'));
  });

  it('switches the note to the due-date caveat once the month has meetings', () => {
    meetingsMock.mockReturnValue({ data: [meetingOn(12)] });
    setup();
    expect(screen.getByRole('note')).toHaveTextContent(i18n.t('topics.calendar.dueNote'));
  });

  it('places two meetings on the same day in the same cell', () => {
    meetingsMock.mockReturnValue({ data: [meetingOn(12), meetingOn(12, { id: 'm-12b', key: 'MTG-12B', title: 'Extra session' })] });
    setup();
    const cell = document.querySelector('.cal-cell:has(.cal-event)');
    expect(cell?.querySelectorAll('.cal-event')).toHaveLength(2);
  });

  it('ignores a malformed scheduled date rather than breaking the grid', () => {
    meetingsMock.mockReturnValue({ data: [meetingOn(12, { scheduledStart: 'not-a-date' })] });
    setup();
    // The grid still renders and simply carries no chip — a bad row must not take the view down.
    expect(document.querySelectorAll('.cal-cell').length).toBeGreaterThan(0);
    expect(document.querySelectorAll('.cal-event')).toHaveLength(0);
  });

  it('lists the selected meeting topics, which is the only place the API can supply them', async () => {
    meetingsMock.mockReturnValue({ data: [meetingOn(12)] });
    detailMock.mockReturnValue({
      data: agenda([{ topicId: 't1', topicKey: 'TOP-2026-014', topicTitle: 'Adopt Keycloak' }]),
      isLoading: false,
    });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Committee session 12/ }));

    expect(screen.getByText('TOP-2026-014')).toBeInTheDocument();
    expect(screen.getByText('Adopt Keycloak')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: i18n.t('topics.calendar.openMeeting') })).toHaveAttribute('href', '/meetings/MTG-12');
  });

  it('marks the selected chip pressed and clears it when clicked again', async () => {
    meetingsMock.mockReturnValue({ data: [meetingOn(12)] });
    const user = userEvent.setup();
    setup();
    const chip = () => screen.getByRole('button', { name: /Committee session 12/ });

    expect(chip()).toHaveAttribute('aria-pressed', 'false');
    await user.click(chip());
    expect(chip()).toHaveAttribute('aria-pressed', 'true');
    await user.click(chip());
    expect(chip()).toHaveAttribute('aria-pressed', 'false');
    expect(document.querySelector('.cal-detail')).toBeNull();
  });

  it('shows a loading line while the selected meeting resolves', async () => {
    meetingsMock.mockReturnValue({ data: [meetingOn(12)] });
    detailMock.mockReturnValue({ data: undefined, isLoading: true });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Committee session 12/ }));
    expect(screen.getByText(i18n.t('common.loading'))).toBeInTheDocument();
  });

  it('says the agenda is empty rather than rendering a blank panel', async () => {
    meetingsMock.mockReturnValue({ data: [meetingOn(12, { itemCount: 0 })] });
    detailMock.mockReturnValue({ data: agenda([]), isLoading: false });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Committee session 12/ }));
    expect(screen.getByText(i18n.t('topics.calendar.noTopics'))).toBeInTheDocument();
  });
});

/*
 * WBS-26.5 / DW-086 — TOPIC CHIPS IN THE GRID, keyed per the reference (DEC-108 d1) and nested under
 * the meeting chip rather than replacing it (DEC-109 d3), because AC-145 is Met and immutable and its
 * Then clause requires that meeting chip.
 */
describe('the agenda projection in the month grid', () => {
  it('renders a topic KEY chip for each topic on a scheduled meeting', () => {
    const meeting = meetingOn(12);
    meetingsMock.mockReturnValue({ data: [meeting] });
    projectionMock.mockReturnValue({
      data: [{
        meetingId: meeting.id,
        meetingKey: meeting.key,
        scheduledStart: meeting.scheduledStart,
        items: [{ topicId: 't1', topicKey: 'TOP-2026-007', topicTitle: 'An open topic' }],
      }],
    });

    setup();

    // THE MEETING CHIP SURVIVES — asserted first, because removing it would falsify AC-145 and every
    // assertion below would still pass.
    expect(screen.getByRole('button', { name: new RegExp(meeting.title, 'i') })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'TOP-2026-007' })).toBeInTheDocument();
  });

  it('renders a localized placeholder, and NOT a link, for a redacted topic', () => {
    const meeting = meetingOn(12);
    meetingsMock.mockReturnValue({ data: [meeting] });
    projectionMock.mockReturnValue({
      data: [{
        meetingId: meeting.id,
        meetingKey: meeting.key,
        scheduledStart: meeting.scheduledStart,
        // The server redacts a Restricted topic to EMPTY key and title rather than sending an English
        // word, because that would break the EN+AR guardrail. Mapping it is the client's job.
        items: [{ topicId: 't2', topicKey: '', topicTitle: '' }],
      }],
    });

    setup();

    expect(screen.getByText(/restricted/i)).toBeInTheDocument();
    // ⛔ THE POINT OF THE CASE: there is nothing the caller may open, so it must not be a link.
    expect(screen.queryByRole('link', { name: /restricted/i })).not.toBeInTheDocument();
  });
});
