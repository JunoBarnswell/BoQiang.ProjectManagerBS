import { useQuery } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';

import { useI18n } from '@/core/i18n/I18nProvider';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneState } from '../components/AsterSceneState';
import { ScenePreviewCanvas } from '../components/ScenePreviewCanvas';
import { createClientMutationId } from '../core/scene-document/documentKernel';
import type { SceneHotspot } from '../model/types';
import '../styles/aster-scene.css';

export function AsterScenePlayerPage() {
  const { translate: t } = useI18n();
  const { publishCode = '' } = useParams();
  const [activeHotspot, setActiveHotspot] = useState<SceneHotspot | null>(null);
  const enteredPublishCodeRef = useRef<string | null>(null);
  const manifestQuery = useQuery({
    enabled: Boolean(publishCode),
    queryFn: ({ signal }) => asterSceneApi.public.manifest(publishCode, signal),
    queryKey: ['asterscene', 'player', publishCode]
  });
  const manifest = manifestQuery.data?.data ?? null;

  const recordRuntimeEvent = useCallback(
    (eventType: string, sceneId?: string | null, hotspotId?: string | null) => {
      if (!publishCode) {
        return;
      }

      void asterSceneApi.public
        .recordRuntimeEvent({
          clientEventId: createClientMutationId(eventType),
          eventType,
          hotspotId,
          publishCode,
          sceneId
        })
        .catch(() => undefined);
    },
    [publishCode]
  );

  useEffect(() => {
    if (!manifest || enteredPublishCodeRef.current === manifest.publishCode) {
      return;
    }

    enteredPublishCodeRef.current = manifest.publishCode;
    recordRuntimeEvent('view', manifest.entrySceneId);
    recordRuntimeEvent('scene-enter', manifest.entrySceneId);
  }, [manifest, recordRuntimeEvent]);

  const handleHotspotActivate = useCallback(
    (hotspot: SceneHotspot) => {
      setActiveHotspot(hotspot);
      recordRuntimeEvent('hotspot-click', hotspot.sceneId ?? manifest?.entrySceneId ?? null, hotspot.id);
    },
    [manifest?.entrySceneId, recordRuntimeEvent]
  );

  if (manifestQuery.isLoading) {
    return <AsterSceneState title={t('asterscene.player.loading')} />;
  }

  if (manifestQuery.isError || !manifest) {
    return <AsterSceneState title={t('asterscene.player.failed')} description={t('asterscene.player.failedDescription')} />;
  }

  return (
    <main className="as-player">
      <ScenePreviewCanvas manifest={manifest} onHotspotActivate={handleHotspotActivate} />
      <div className="as-player__hud">
        <div>
          <span>{t('asterscene.player.manifest')}</span>
          <strong>{manifest.publishCode}</strong>
        </div>
        <Link to="/explore">{t('asterscene.nav.explore')}</Link>
      </div>
      {activeHotspot ? (
        <aside className="as-player__hotspot">
          <button aria-label={t('asterscene.player.closeHotspot')} onClick={() => setActiveHotspot(null)} type="button">
            ×
          </button>
          <span>{activeHotspot.type ?? 'navigate'}</span>
          <strong>{activeHotspot.payload?.title ?? activeHotspot.label}</strong>
          {activeHotspot.payload?.body ? <p>{activeHotspot.payload.body}</p> : null}
          {activeHotspot.payload?.url ? (
            <a href={activeHotspot.payload.url} rel="noreferrer" target="_blank">
              {t('asterscene.player.openLink')}
            </a>
          ) : null}
        </aside>
      ) : null}
    </main>
  );
}
