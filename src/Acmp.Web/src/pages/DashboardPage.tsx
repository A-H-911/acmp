import { useTranslation } from 'react-i18next';

export default function DashboardPage() {
  const { t } = useTranslation();
  return (
    <section className="page">
      <h1 className="page-title">{t('dashboard.title')}</h1>
      <p className="page-lead">{t('dashboard.welcome')}</p>
      <div className="surface-card">{t('dashboard.placeholder')}</div>
    </section>
  );
}
