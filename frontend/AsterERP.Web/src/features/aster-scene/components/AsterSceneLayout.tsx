import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';

import { useI18n } from '@/core/i18n/I18nProvider';

import '../styles/aster-scene.css';

interface AsterSceneLayoutProps {
  actions?: ReactNode;
  children: ReactNode;
  eyebrow?: string;
  title: string;
}

export function AsterSceneLayout({ actions, children, eyebrow, title }: AsterSceneLayoutProps) {
  const { translate: t } = useI18n();

  return (
    <main className="as-page">
      <header className="as-page__header">
        <div>
          {eyebrow ? <span className="as-eyebrow">{eyebrow}</span> : null}
          <h1>{title}</h1>
        </div>
        <nav className="as-tabs" aria-label={t('asterscene.nav.aria')}>
          <NavLink to="/explore">{t('asterscene.nav.explore')}</NavLink>
          <NavLink to="/dashboard">{t('asterscene.nav.dashboard')}</NavLink>
          <NavLink to="/assets">{t('asterscene.nav.assets')}</NavLink>
          <NavLink to="/pricing">{t('asterscene.nav.pricing')}</NavLink>
        </nav>
        {actions ? <div className="as-actions">{actions}</div> : null}
      </header>
      {children}
    </main>
  );
}
