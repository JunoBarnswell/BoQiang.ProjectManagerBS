import type { ReactNode } from 'react';

import { AppIcon } from '../../../../../shared/icons/AppIcon';

interface ItemCardProps {
  actions?: ReactNode;
  badge?: ReactNode;
  children?: ReactNode;
  icon?: string;
  meta?: ReactNode;
  subtitle?: ReactNode;
  title: ReactNode;
}

export function ItemCard({ actions, badge, children, icon = 'module', meta, subtitle, title }: ItemCardProps) {
  return (
    <article className="flowise-native-item-card">
      <div className="flowise-native-item-card__top">
        <span className="flowise-native-item-card__icon">
          <AppIcon name={icon} />
        </span>
        <div className="flowise-native-item-card__title">
          <h2>{title}</h2>
          {subtitle ? <span>{subtitle}</span> : null}
        </div>
        {badge ? <div className="flowise-native-item-card__badge">{badge}</div> : null}
      </div>
      {meta ? <div className="flowise-native-item-card__meta">{meta}</div> : null}
      {children ? <div className="flowise-native-item-card__body">{children}</div> : null}
      {actions ? <footer className="flowise-native-item-card__actions">{actions}</footer> : null}
    </article>
  );
}
