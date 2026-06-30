/*
 * Recording tab (P6a) — routed at /meetings/:key/recording (promoted from the design's overview
 * quick-link to a peer tab per NV-08; blessed deviation, see the shell + progress log).
 *
 * The design models three recording states (ready → player + transcript; pending → "being
 * retrieved"; notranscript → "transcript unavailable"), but recordings + transcripts arrive only
 * with the Webex adapter (Phase 2, ADR-0005/CON-001). There is no recording signal on the meeting
 * detail yet, so rather than fake a state we render the honest Webex-deferred gate. When the
 * adapter ships, the recReady player/transcript path replaces this body.
 */
import { useTranslation } from 'react-i18next';
import { MeetingGate } from './MeetingGate';

export function MeetingRecording() {
  const { t } = useTranslation();
  return <MeetingGate icon="video" title={t('meetings.recordingTab.title')} body={t('meetings.recordingTab.body')} />;
}
