import { useTranslation } from 'react-i18next';

export default function PlaceholderPage({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation();
  return (
    <section className="page">
      <h1 className="page-title">{t(titleKey)}</h1>
      <div className="surface-card muted">{t('common.empty')}</div>
    </section>
  );
}
