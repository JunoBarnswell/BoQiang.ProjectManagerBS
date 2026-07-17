import { Pause, Play, SkipBack, SkipForward, StepBack, StepForward } from 'lucide-react';
import { useEffect, useState } from 'react';

import type { SceneDocument } from '../../model/types';
import { useAsterSceneEditorStore } from '../../state/editorStore';

interface TimelinePanelProps {
  dirty: boolean;
  document: SceneDocument;
  documentHash: string;
  onSetKey: () => void;
  revision: number;
  selectedActorId: string | null;
  t: (key: string) => string;
}

export function TimelinePanel({ dirty, document, documentHash, onSetKey, revision, selectedActorId, t }: TimelinePanelProps) {
  const { autoKey, currentFrame, setAutoKey, setCurrentFrame, snapEnabled, subObjectMode, transformMode, transformSpace, viewportLayout } = useAsterSceneEditorStore();
  const [playing, setPlaying] = useState(false);
  const range = document.timeline.range ?? { end: 180, start: 0 };
  const selectedActor = selectedActorId ? (document.actors.find((actor) => actor.id === selectedActorId) ?? null) : null;

  useEffect(() => {
    if (!playing) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      setCurrentFrame(currentFrame >= range.end ? range.start : currentFrame + 1);
    }, 1000 / (document.timeline.frameRate ?? 30));
    return () => window.clearInterval(timer);
  }, [currentFrame, document.timeline.frameRate, playing, range.end, range.start, setCurrentFrame]);

  return (
    <footer className="as-dcc-timeline-shell">
      <div className="as-dcc-timeline">
        <div className="as-dcc-timeline__controls">
          <button onClick={() => setCurrentFrame(range.start)} type="button">
            <SkipBack size={15} />
          </button>
          <button onClick={() => setCurrentFrame(Math.max(range.start, currentFrame - 1))} type="button">
            <StepBack size={15} />
          </button>
          <button onClick={() => setPlaying(!playing)} type="button">
            {playing ? <Pause size={15} /> : <Play size={15} />}
          </button>
          <button onClick={() => setCurrentFrame(Math.min(range.end, currentFrame + 1))} type="button">
            <StepForward size={15} />
          </button>
          <button onClick={() => setCurrentFrame(range.end)} type="button">
            <SkipForward size={15} />
          </button>
          <button className={autoKey ? 'is-active' : ''} onClick={() => setAutoKey(!autoKey)} type="button">
            {t('asterscene.dcc.timeline.autoKey')}
          </button>
          <button disabled={!selectedActorId} onClick={onSetKey} type="button">
            {t('asterscene.dcc.timeline.setKey')}
          </button>
        </div>
        <input max={range.end} min={range.start} onChange={(event) => setCurrentFrame(Number(event.target.value))} type="range" value={currentFrame} />
        <div className="as-dcc-timeline__tracks">
          <strong>
            {t('asterscene.dcc.timeline.frame')} {currentFrame}
          </strong>
          <span>
            {document.timeline.tracks.length} {t('asterscene.dcc.timeline.tracks')}
          </span>
        </div>
      </div>
      <div className="as-dcc-statusbar">
        <span>
          {t('asterscene.dcc.status.selected')}: {selectedActor?.name ?? t('asterscene.dcc.status.none')}
        </span>
        <span>
          {t('asterscene.dcc.status.actors')}: {document.actors.length}
        </span>
        <span>
          {t('asterscene.dcc.status.scenes')}: {document.runtime.scenes.length}
        </span>
        <span>
          {t('asterscene.dcc.status.mode')}: {t(`asterscene.dcc.transform.${transformMode}`)}
        </span>
        <span>
          {t('asterscene.dcc.status.space')}: {t(`asterscene.dcc.space.${transformSpace}`)}
        </span>
        <span>
          {t('asterscene.dcc.status.subobject')}: {t(`asterscene.dcc.subobject.${subObjectMode}`)}
        </span>
        <span>
          {t('asterscene.dcc.status.snap')}: {snapEnabled ? t('asterscene.common.enabled') : t('asterscene.common.disabled')}
        </span>
        <span>
          {t('asterscene.dcc.status.viewport')}: {viewportLayout === 'quad' ? t('asterscene.dcc.viewport.quad') : t('asterscene.dcc.viewport.perspective')}
        </span>
        <span>
          {t('asterscene.studio.revision')}: {revision}
        </span>
        <span>
          {t('asterscene.studio.hash')}: {documentHash.slice(0, 12)}
        </span>
        <strong>{dirty ? t('asterscene.studio.dirty') : t('asterscene.studio.clean')}</strong>
      </div>
    </footer>
  );
}
