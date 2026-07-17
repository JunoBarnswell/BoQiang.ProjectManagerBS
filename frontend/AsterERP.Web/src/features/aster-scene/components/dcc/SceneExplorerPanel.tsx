import { Box, Eye, EyeOff, Lock, Unlock } from 'lucide-react';

import type { SceneActor, SceneDocument } from '../../model/types';

interface SceneExplorerPanelProps {
  document: SceneDocument;
  onSelectActor: (actorId: string | null) => void;
  onToggleFrozen: (actorId: string, value: boolean) => void;
  onToggleHidden: (actorId: string, value: boolean) => void;
  selectedActorId: string | null;
  t: (key: string) => string;
}

export function SceneExplorerPanel({ document, onSelectActor, onToggleFrozen, onToggleHidden, selectedActorId, t }: SceneExplorerPanelProps) {
  const roots = document.actors.filter((actor) => !actor.parentId);

  return (
    <aside className="as-dcc-panel as-dcc-panel--explorer">
      <header>
        <h2>{t('asterscene.dcc.sceneExplorer')}</h2>
        <span>{document.actors.length}</span>
      </header>
      <div className="as-dcc-tree">
        {roots.map((actor) => (
          <ActorTreeRow
            actor={actor}
            depth={0}
            document={document}
            key={actor.id}
            onSelectActor={onSelectActor}
            onToggleFrozen={onToggleFrozen}
            onToggleHidden={onToggleHidden}
            selectedActorId={selectedActorId}
            t={t}
          />
        ))}
      </div>
    </aside>
  );
}

function ActorTreeRow({
  actor,
  depth,
  document,
  onSelectActor,
  onToggleFrozen,
  onToggleHidden,
  selectedActorId,
  t
}: {
  actor: SceneActor;
  depth: number;
  document: SceneDocument;
  onSelectActor: (actorId: string | null) => void;
  onToggleFrozen: (actorId: string, value: boolean) => void;
  onToggleHidden: (actorId: string, value: boolean) => void;
  selectedActorId: string | null;
  t: (key: string) => string;
}) {
  const children = document.actors.filter((item) => item.parentId === actor.id);
  const hidden = actor.display?.hidden === true;
  const frozen = actor.display?.frozen === true;
  return (
    <>
      <div className={actor.id === selectedActorId ? 'as-dcc-tree__row is-active' : 'as-dcc-tree__row'} style={{ paddingLeft: 8 + depth * 14 }}>
        <button onClick={() => onToggleHidden(actor.id, !hidden)} title={t('asterscene.dcc.sceneExplorer.visibility')} type="button">
          {hidden ? <EyeOff size={14} /> : <Eye size={14} />}
        </button>
        <button onClick={() => onToggleFrozen(actor.id, !frozen)} title={t('asterscene.dcc.sceneExplorer.freeze')} type="button">
          {frozen ? <Lock size={14} /> : <Unlock size={14} />}
        </button>
        <button className="as-dcc-tree__name" onClick={() => onSelectActor(actor.id)} type="button">
          <Box size={15} /> {actor.name}
        </button>
        <span>{actor.type}</span>
      </div>
      {children.map((child) => (
        <ActorTreeRow
          actor={child}
          depth={depth + 1}
          document={document}
          key={child.id}
          onSelectActor={onSelectActor}
          onToggleFrozen={onToggleFrozen}
          onToggleHidden={onToggleHidden}
          selectedActorId={selectedActorId}
          t={t}
        />
      ))}
    </>
  );
}
