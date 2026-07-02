/*
 * Honest-empty Administration tabs (templates / streams). The Usage Map schedules these modules' data
 * to a later phase (P15), so rather than fabricate rows the tab is navigable but renders the design's
 * empty state with module-specific copy. No mock data. (Job Monitor is now live — see JobMonitor.tsx.)
 */
import { useTranslation } from 'react-i18next';
import { EmptyState } from '../../components/states';
import type { IconName } from '../../components/icons';

export function ComingDataTab({ tab, icon }: { tab: 'templates' | 'streams'; icon: IconName }) {
  const { t } = useTranslation();
  return <EmptyState icon={icon} title={t(`admin.${tab}.emptyTitle`)} body={t(`admin.${tab}.emptyBody`)} />;
}
