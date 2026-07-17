import type { ReactNode } from 'react';

interface MainCardProps {
  actions?: ReactNode;
  children: ReactNode;
  description?: ReactNode;
  title: ReactNode;
  toolbar?: ReactNode;
}

export function MainCard({ actions, children, description, title, toolbar }: MainCardProps) {
  return (
    <section className="flowise-native-main-card">
      <header className="flowise-native-main-card__header">
        <div>
          <h1>{title}</h1>
          {description ? <p>{description}</p> : null}
        </div>
        {actions ? <div className="flowise-native-main-card__actions">{actions}</div> : null}
      </header>
      {toolbar ? <div className="flowise-native-main-card__toolbar">{toolbar}</div> : null}
      {children}
    </section>
  );
}
