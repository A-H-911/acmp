import { useTranslation } from 'react-i18next';
import { EmptyState } from '../components/states';

/*
 * Foundation placeholder for nav areas whose feature screens land in later
 * phases. Renders the area's localized title + the empty state, so the shell,
 * routing, and role gating are fully navigable now without building features.
 */
export default function PlaceholderPage({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation();
  return (
    <section className="page">
      <h1 className="page-title">{t(titleKey)}</h1>
      <p className="page-lead">{t('common.comingSoon')}</p>
      <EmptyState />
    </section>
  );
}
