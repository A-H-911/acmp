/*
 * Minutes tab (P6a) — honest placeholder until the Minutes & Decisions module (P7). Routed at
 * /meetings/:key/minutes; renders the shared gate so the tab strip is complete without faking a
 * feature that isn't built yet.
 */
import { useTranslation } from 'react-i18next';
import { MeetingGate } from './MeetingGate';

export function MeetingMinutes() {
  const { t } = useTranslation();
  return <MeetingGate icon="adr" title={t('meetings.minutesTab.title')} body={t('meetings.minutesTab.body')} />;
}
