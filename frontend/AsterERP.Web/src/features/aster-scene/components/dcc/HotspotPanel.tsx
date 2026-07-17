import { Crosshair, Image } from 'lucide-react';
import { useState } from 'react';

import type { AsterSceneAsset, SceneDocument, SceneHotspot, SceneVector3 } from '../../model/types';

interface HotspotPanelProps {
  assets: AsterSceneAsset[];
  document: SceneDocument;
  onAddHotspot: (hotspot: SceneHotspot) => void;
  onAddPanoramaScene: (asset: AsterSceneAsset) => void;
  selectedActorId: string | null;
  t: (key: string) => string;
}

export function HotspotPanel({ assets, document, onAddHotspot, onAddPanoramaScene, t }: HotspotPanelProps) {
  const panoramaAssets = assets.filter((asset) => asset.assetType.toLowerCase() === 'panorama');
  const [hotspotLabel, setHotspotLabel] = useState(t('asterscene.studio.defaultHotspotLabel'));
  const [hotspotMode, setHotspotMode] = useState<SceneHotspot['type']>('navigate');
  const [sceneId, setSceneId] = useState(document.runtime.entrySceneId);
  const [targetSceneId, setTargetSceneId] = useState(document.runtime.scenes[0]?.id ?? '');
  const [hotspotPosition, setHotspotPosition] = useState<SceneVector3>({ x: 0, y: 1.4, z: 0 });
  const [hotspotPitch, setHotspotPitch] = useState(0);
  const [hotspotYaw, setHotspotYaw] = useState(25);
  const [panoramaAssetId, setPanoramaAssetId] = useState('');

  const activeScene = document.runtime.scenes.find((scene) => scene.id === sceneId) ?? document.runtime.scenes[0];
  const panoramaAsset = panoramaAssets.find((asset) => asset.id === panoramaAssetId) ?? panoramaAssets[0];
  const canAddHotspot = hotspotMode !== 'navigate' || Boolean(targetSceneId);

  return (
    <section className="as-dcc-command-panel">
      <header>
        <h2>{t('asterscene.dcc.hotspotPanel')}</h2>
      </header>
      <label className="as-dcc-field">
        <span>{t('asterscene.studio.panoramaAsset')}</span>
        <select onChange={(event) => setPanoramaAssetId(event.target.value)} value={panoramaAssetId}>
          <option value="">{t('asterscene.studio.selectPanorama')}</option>
          {panoramaAssets.map((asset) => (
            <option key={asset.id} value={asset.id}>
              {asset.fileName}
            </option>
          ))}
        </select>
      </label>
      <button className="as-button" disabled={!panoramaAsset} onClick={() => panoramaAsset && onAddPanoramaScene(panoramaAsset)} type="button">
        <Image size={16} /> {t('asterscene.studio.panoramaScene')}
      </button>
      <label className="as-dcc-field">
        <span>{t('asterscene.studio.label')}</span>
        <input onChange={(event) => setHotspotLabel(event.target.value)} value={hotspotLabel} />
      </label>
      <label className="as-dcc-field">
        <span>{t('asterscene.studio.scene')}</span>
        <select onChange={(event) => setSceneId(event.target.value)} value={sceneId}>
          {document.runtime.scenes.map((scene) => (
            <option key={scene.id} value={scene.id}>
              {scene.name}
            </option>
          ))}
        </select>
      </label>
      <label className="as-dcc-field">
        <span>{t('asterscene.studio.action')}</span>
        <select onChange={(event) => setHotspotMode(event.target.value as SceneHotspot['type'])} value={hotspotMode}>
          <option value="navigate">{t('asterscene.hotspot.navigate')}</option>
          <option value="info">{t('asterscene.hotspot.info')}</option>
          <option value="media">{t('asterscene.hotspot.media')}</option>
          <option value="url">{t('asterscene.hotspot.url')}</option>
        </select>
      </label>
      <label className="as-dcc-field">
        <span>{t('asterscene.studio.targetScene')}</span>
        <select disabled={hotspotMode !== 'navigate'} onChange={(event) => setTargetSceneId(event.target.value)} value={targetSceneId}>
          {document.runtime.scenes.map((scene) => (
            <option key={scene.id} value={scene.id}>
              {scene.name}
            </option>
          ))}
        </select>
      </label>
      <Vector3Editor label="Position" onChange={setHotspotPosition} value={hotspotPosition} />
      <NumberEditor label="Yaw" onChange={setHotspotYaw} step={1} value={hotspotYaw} />
      <NumberEditor label="Pitch" max={89} min={-89} onChange={setHotspotPitch} step={1} value={hotspotPitch} />
      <button
        className="as-button"
        disabled={!canAddHotspot}
        onClick={() =>
          onAddHotspot({
            facing: { pitch: hotspotPitch, yaw: hotspotYaw },
            id: `hotspot_${crypto.randomUUID().replaceAll('-', '').slice(0, 10)}`,
            label: hotspotLabel.trim() || t('asterscene.studio.hotspot'),
            payload: hotspotMode === 'info' ? { body: t('asterscene.studio.defaultHotspotBody'), title: hotspotLabel.trim() || t('asterscene.studio.hotspot') } : undefined,
            position: hotspotPosition,
            sceneId,
            spherical: activeScene?.type === 'panorama720' ? { pitch: hotspotPitch, yaw: hotspotYaw } : undefined,
            target: hotspotMode === 'navigate' ? targetSceneId : '',
            trigger: { event: 'click' },
            type: hotspotMode,
            visibility: { distanceMax: 30, distanceMin: 0 }
          })
        }
        type="button"
      >
        <Crosshair size={16} /> {t('asterscene.studio.addHotspot')}
      </button>
    </section>
  );
}

function Vector3Editor({
  label,
  onChange,
  value
}: {
  label: string;
  onChange: (value: SceneVector3) => void;
  value: SceneVector3;
}) {
  return (
    <label className="as-dcc-field">
      <span>{label}</span>
      <div className="as-vector-inputs">
        {(['x', 'y', 'z'] as const).map((axis) => (
          <input
            key={axis}
            onChange={(event) =>
              onChange({
                ...value,
                [axis]: Number(event.target.value)
              })
            }
            step={0.1}
            type="number"
            value={Number(value[axis].toFixed(3))}
          />
        ))}
      </div>
    </label>
  );
}

function NumberEditor({
  label,
  max,
  min,
  onChange,
  step,
  value
}: {
  label: string;
  max?: number;
  min?: number;
  onChange: (value: number) => void;
  step: number;
  value: number;
}) {
  return (
    <label className="as-dcc-field">
      <span>{label}</span>
      <input
        max={max}
        min={min}
        onChange={(event) => onChange(Number(event.target.value))}
        step={step}
        type="number"
        value={Number(value.toFixed(3))}
      />
    </label>
  );
}
