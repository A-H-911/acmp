/*
 * The four canonical screen states (docs/domain/information-architecture.md §4): empty, loading, error,
 * permission-denied. Every list/detail screen composes these rather than
 * hand-rolling its own. All copy comes through i18n (guardrail 9).
 */
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon, type IconName } from './icons';
import { Button } from './ui/Button';

interface StateProps {
  title?: string;
  body?: string;
  icon?: ReactNode;
  action?: ReactNode;
  note?: string;
  tone?: 'default' | 'error';
}

function StateShell({ title, body, icon, action, note, tone = 'default' }: StateProps) {
  return (
    <div className="state" role="status">
      {icon && <div className={`state-icon ${tone === 'default' ? '' : tone}`}>{icon}</div>}
      {title && <p className="state-title">{title}</p>}
      {body && <p className="state-body">{body}</p>}
      {action && <div className="state-actions">{action}</div>}
      {note && <p className="state-note state-note-mono">{note}</p>}
    </div>
  );
}

export function EmptyState({ title, body, icon }: { title?: string; body?: string; icon?: IconName }) {
  const { t } = useTranslation();
  return <StateShell icon={<Icon name={icon ?? 'doc'} size={20} />} title={title ?? t('state.emptyTitle')} body={body ?? t('state.emptyBody')} />;
}

export function LoadingState({ label }: { label?: string }) {
  const { t } = useTranslation();
  return (
    <div className="state" role="status" aria-live="polite" aria-busy="true">
      <span className="visually-hidden">{label ?? t('common.loading')}</span>
      <div style={{ inlineSize: '100%', maxInlineSize: '32rem' }}>
        <div className="skeleton skeleton-row" style={{ inlineSize: '60%' }} />
        <div className="skeleton skeleton-row" />
        <div className="skeleton skeleton-row" style={{ inlineSize: '80%' }} />
      </div>
    </div>
  );
}

export function ErrorState({ title, body, onRetry, requestId }: { title?: string; body?: string; onRetry?: () => void; requestId?: string }) {
  const { t } = useTranslation();
  return (
    <StateShell
      tone="error"
      icon={<Icon name="alertCircle" size={20} aria-hidden />}
      title={title ?? t('state.errorTitle')}
      body={body ?? t('common.error')}
      action={onRetry && (
        <Button onClick={onRetry}><Icon name="refresh" size={15} />{t('common.retry')}</Button>
      )}
      note={requestId ? `${t('state.errorRef')} · ${requestId}` : undefined}
    />
  );
}

export function PermissionDenied({ body, path }: { body?: string; path?: string }) {
  const { t } = useTranslation();
  // No self-registration (ADR-0015): "Request access" emails an administrator rather than self-serving.
  const deniedPath = path ?? (typeof window !== 'undefined' ? window.location.pathname : '');
  return (
    <StateShell
      icon={<Icon name="lock" size={20} aria-hidden />}
      title={t('state.deniedTitle')}
      body={body ?? t('common.permissionDenied')}
      action={
        <>
          <a className="state-btn state-btn-primary" href="/">{t('state.backHome')}</a>
          <a className="state-btn" href={`mailto:?subject=${encodeURIComponent(`${t('state.requestAccess')}: ${deniedPath}`)}`}>{t('state.requestAccess')}</a>
        </>
      }
      note={deniedPath ? `403 · ${deniedPath}` : undefined}
    />
  );
}
