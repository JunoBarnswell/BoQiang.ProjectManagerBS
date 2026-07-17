import type { ReactNode } from 'react';

import '../styles/aster-scene.css';

interface AsterSceneStateProps {
  action?: ReactNode;
  description?: ReactNode;
  title: ReactNode;
}

export function AsterSceneState({ action, description, title }: AsterSceneStateProps) {
  return (
    <section className="as-state">
      <h2>{title}</h2>
      {description ? <p>{description}</p> : null}
      {action ? <div className="as-state__action">{action}</div> : null}
    </section>
  );
}
