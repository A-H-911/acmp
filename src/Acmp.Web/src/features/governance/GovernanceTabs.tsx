/*
 * Governance register tab bar (P11d) — the shared "ADRs | Architecture Invariants" switch that heads both
 * the ADR register (/adrs) and the Invariant register (/invariants), matching the design's isAdrs tab bar.
 * The active tab is the current page (a static span); the other is a Link to its route, so tabs deep-link.
 *
 * Each tab shows its own count only when the hosting page supplies it (the ADR page knows the ADR total,
 * the Invariant page knows the Invariant total) — this avoids cross-fetching the other register's count.
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
    { key: 'invariants', to: '/invariants', icon: 'shieldUser', labelKey: 'adrs.tab.invariants', count: invCount },
  ];
  return (
    <div className="adr-tabs" role="tablist" aria-label={t('adrs.tabsLabel')}>
      {tabs.map((tab) => {
        const label = (
          <>
            <Icon name={tab.icon} size={16} aria-hidden /> {t(tab.labelKey)}
            {tab.count !== undefined && <span className="adr-tab-count">{tab.count}</span>}
          </>
        );
        return tab.key === active ? (
          <span key={tab.key} className="adr-tab is-active" role="tab" aria-selected="true">
            {label}
          </span>
        ) : (
          <Link key={tab.key} className="adr-tab" role="tab" aria-selected="false" to={tab.to}>
            {label}
          </Link>
        );
      })}
    </div>
  );
}
