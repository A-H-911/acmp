/*
 * Governance register tab bar (P11d) — the shared "ADRs | Architecture Invariants" switch that heads both
 * the ADR register (/adrs) and the Invariant register (/invariants), matching the design's isAdrs tab bar.
 * These are route deep-links, not a true tab widget, so it is a <nav> with the current page marked
 * aria-current — not role="tablist" (which would promise a tabpanel/roving-tabindex contract we don't honor).
 *
 * Both counts render on both tabs (matching the design); each hosting page supplies both totals from the two
 * lightweight count queries.
 */
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon, type IconName } from '../../components/icons';
import './governance.css';

type TabKey = 'adrs' | 'invariants';

interface Props {
  active: TabKey;
  adrCount?: number;
  invCount?: number;
}

interface TabDef {
  key: TabKey;
  to: string;
  icon: IconName;
  labelKey: string;
  count?: number;
}

export function GovernanceTabs({ active, adrCount, invCount }: Props) {
  const { t } = useTranslation();
  const tabs: TabDef[] = [
    { key: 'adrs', to: '/adrs', icon: 'adr', labelKey: 'adrs.tab.adrs', count: adrCount },
    { key: 'invariants', to: '/invariants', icon: 'shieldPlus', labelKey: 'adrs.tab.invariants', count: invCount },
  ];
  return (
    <nav className="adr-tabs" aria-label={t('adrs.tabsLabel')}>
      {tabs.map((tab) => {
        const label = (
          <>
            <Icon name={tab.icon} size={16} aria-hidden /> {t(tab.labelKey)}
            {tab.count !== undefined && <span className="adr-tab-count">{tab.count}</span>}
          </>
        );
        return tab.key === active ? (
          <span key={tab.key} className="adr-tab is-active" aria-current="page">
            {label}
          </span>
        ) : (
          <Link key={tab.key} className="adr-tab" to={tab.to}>
            {label}
          </Link>
        );
      })}
    </nav>
  );
}
