/*
 * Honest-empty Administration tabs (templates / streams / job monitor). The Usage Map schedules these
 * modules' data to later phases (templates/streams → P15, jobs → P14), so rather than fabricate rows
 * the tab is navigable but renders the design's empty state with module-specific copy. No mock data.
 */
import { useTranslation } from 'react-i18next';
import { EmptyState } from '../../components/states';
import type { IconName } from '../../components/icons';

export function ComingDataTab({ tab, icon }: { tab: 'templates' | 'streams' | 'jobs'; icon: IconName }) {
  const { t } = useTranslation();
  return <EmptyState icon={icon} title={t(`admin.${tab}.emptyTitle`)} body={t(`admin.${tab}.emptyBody`)} />;
}
