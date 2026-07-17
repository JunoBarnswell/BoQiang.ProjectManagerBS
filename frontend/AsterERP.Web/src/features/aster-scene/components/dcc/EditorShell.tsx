import { Box, Crosshair, GitBranch, MonitorCog, Palette, SlidersHorizontal, Wrench, type LucideIcon } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';

import type { AsterSceneCommand } from '../../core/command/CommandStack';
import {
  addPrimitiveActor,
  addTimelineKeyframe,
  assignMaterialToActor,
  createMaterialFromAsset,
  createStandaloneMaterial,
  duplicateActor,
  mutateEditableMesh,
  placeAssetActor,
  readActorTransform,
  removeActor,
  renameActor,
  setActorDisplayFlag,
  type PrimitiveDefinition,
  type PrimitivePlacement,
  updateActorTransform,
  upsertModifier
} from '../../core/dcc/sceneDocumentDcc';
import { addSceneHotspot, applyComplexExhibitionHall, createClientMutationId, updateMaterialBaseColor, upsertPanoramaScene } from '../../core/scene-document/documentKernel';
import type { ExhibitionHallLabels } from '../../core/scene-document/documentKernel';
import type { AsterSceneAsset, AsterSceneProject, AsterScenePublishVersion, SceneActor, SceneComponent, SceneDocument, SceneHotspot, SceneVector3 } from '../../model/types';
import { useAsterSceneDocumentStore } from '../../state/documentStore';
import { useAsterSceneEditorStore } from '../../state/editorStore';
import { useAsterSceneSelectionStore } from '../../state/selectionStore';

import { AssetBrowserPanel } from './AssetBrowserPanel';
import { AsterSceneVersionPanel } from './AsterSceneVersionPanel';
import { CommandPanel } from './CommandPanel';
import { DccToolbar } from './DccToolbar';
import { HotspotPanel } from './HotspotPanel';
import { MaterialEditorPanel } from './MaterialEditorPanel';
import { ModifyPanel } from './ModifyPanel';
import { SceneExplorerPanel } from './SceneExplorerPanel';
import { StudioViewport } from './StudioViewport';
import { TimelinePanel } from './TimelinePanel';

interface DccWorkbenchShellProps {
  assets: AsterSceneAsset[];
  autosavePending: boolean;
  dirty: boolean;
  document: SceneDocument;
  documentHash: string;
  onPublish: () => void;
  onRollback: (version: AsterScenePublishVersion) => void;
  onSave: () => void;
  project: AsterSceneProject;
  publishPending: boolean;
  rollbackPending: boolean;
  revision: number;
  savePending: boolean;
  t: (key: string) => string;
  versions: AsterScenePublishVersion[];
}

type CommandPanelKey = 'create' | 'modify' | 'material' | 'hotspot' | 'hierarchy' | 'display' | 'utilities';

const COMMAND_PANEL_TABS: Array<{ icon: LucideIcon; key: CommandPanelKey; labelKey: string }> = [
  { icon: Box, key: 'create', labelKey: 'asterscene.dcc.createPanel' },
  { icon: SlidersHorizontal, key: 'modify', labelKey: 'asterscene.dcc.modifyPanel' },
  { icon: Palette, key: 'material', labelKey: 'asterscene.dcc.materialEditor' },
  { icon: Crosshair, key: 'hotspot', labelKey: 'asterscene.dcc.hotspotPanel' },
  { icon: GitBranch, key: 'hierarchy', labelKey: 'asterscene.dcc.panel.hierarchy' },
  { icon: MonitorCog, key: 'display', labelKey: 'asterscene.dcc.panel.display' },
  { icon: Wrench, key: 'utilities', labelKey: 'asterscene.dcc.panel.utilities' }
];

export function DccWorkbenchShell({
  assets,
  autosavePending,
  dirty,
  document,
  documentHash,
  onPublish,
  onRollback,
  onSave,
  project,
  publishPending,
  rollbackPending,
  revision,
  savePending,
  t,
  versions
}: DccWorkbenchShellProps) {
  const { applyCommand, redo, undo } = useAsterSceneDocumentStore();
  const { actorId, selectActor } = useAsterSceneSelectionStore();
  const { autoKey, currentFrame, subObjectMode } = useAsterSceneEditorStore();
  const [activePrimitive, setActivePrimitive] = useState<PrimitiveDefinition | null>(null);
  const [activeCommandPanel, setActiveCommandPanel] = useState<CommandPanelKey>('create');
  const [showVersions, setShowVersions] = useState(false);
  const selectedActor = document.actors.find((actor) => actor.id === actorId) ?? null;
  const selectedTransform = selectedActor ? findActorComponent(document, selectedActor, 'transform') : null;
  const selectedMaterialId = selectedActor ? readActorMaterialId(document, selectedActor) : null;
  const selectedMaterial = document.materials.find((material) => material.id === selectedMaterialId) ?? null;

  const runCommand = (label: string, redoAction: (state: SceneDocument) => SceneDocument, undoState = document) => {
    const command: AsterSceneCommand<SceneDocument> = {
      id: createClientMutationId('cmd'),
      label,
      redo: redoAction,
      timestamp: Date.now(),
      undo: () => undoState
    };
    applyCommand(command);
  };

  const handleCreatePrimitive = (definition: PrimitiveDefinition) => {
    setActivePrimitive((current) => (current?.code === definition.code ? null : definition));
    selectActor(null);
  };

  const handlePrimitivePlaced = (definition: PrimitiveDefinition, placement: PrimitivePlacement) => {
    let nextActorId: string | null = null;
    runCommand(t('asterscene.dcc.command.createPrimitive'), (state) => {
      const result = addPrimitiveActor(state, definition, t(definition.labelKey), placement);
      nextActorId = result.actorId;
      return result.document;
    });
    if (nextActorId) {
      selectActor(nextActorId);
    }
  };

  const handleDuplicate = () => {
    if (!actorId) {
      return;
    }

    let nextActorId: string | null = null;
    runCommand(t('asterscene.dcc.tool.duplicate'), (state) => {
      const result = duplicateActor(state, actorId);
      nextActorId = result.actorId;
      return result.document;
    });
    if (nextActorId) {
      selectActor(nextActorId);
    }
  };

  const handlePlaceAsset = (asset: AsterSceneAsset) => {
    let nextActorId: string | null = null;
    runCommand(t('asterscene.dcc.command.placeAsset'), (state) => {
      const result = placeAssetActor(state, asset);
      nextActorId = result.actorId;
      return result.document;
    });
    if (nextActorId) {
      selectActor(nextActorId);
    }
  };

  const handleSetKey = () => {
    if (!actorId) {
      return;
    }

    const transform = readActorTransform(document, actorId);
    if (!transform) {
      return;
    }

    runCommand(t('asterscene.dcc.timeline.setKey'), (state) =>
      addTimelineKeyframe(
        state,
        { property: 'transform', targetId: actorId, type: 'transform' },
        { easing: 'linear', frame: currentFrame, value: transform }
      )
    );
  };

  const handleTransformCommit = (nextActorId: string, transform: { position: SceneVector3; rotation: SceneVector3; scale: SceneVector3 }) => {
    runCommand(t('asterscene.studio.commandMoveActor'), (state) => {
      const transformed = updateActorTransform(state, nextActorId, transform);
      return autoKey
        ? addTimelineKeyframe(
            transformed,
            { property: 'transform', targetId: nextActorId, type: 'transform' },
            { easing: 'linear', frame: currentFrame, value: transform }
          )
        : transformed;
    });
  };

  return (
    <main className="as-dcc-studio">
      <DccToolbar
        canPublish={!dirty && !publishPending}
        canSave={dirty && !savePending && !autosavePending}
        hasSelection={Boolean(actorId)}
        onDelete={() => {
          if (actorId) {
            runCommand(t('asterscene.dcc.tool.delete'), (state) => removeActor(state, actorId));
            selectActor(null);
          }
        }}
        onDuplicate={handleDuplicate}
        onPublish={onPublish}
        onOpenVersions={() => setShowVersions(true)}
        onRedo={redo}
        onSave={onSave}
        onToggleHidden={() => actorId && runCommand(t('asterscene.dcc.tool.hide'), (state) => setActorDisplayFlag(state, actorId, 'hidden', true))}
        onUndo={undo}
        publishing={publishPending}
        saving={savePending || autosavePending}
        t={t}
      />
      {showVersions ? (
        <AsterSceneVersionPanel
          dirty={dirty}
          onClose={() => setShowVersions(false)}
          onRollback={onRollback}
          pending={rollbackPending}
          t={t}
          versions={versions}
        />
      ) : null}
      <div className="as-dcc-titlebar">
        <div>
          <span>{t('asterscene.studio.title')}</span>
          <strong>{project.projectName}</strong>
        </div>
        <dl>
          <dt>{t('asterscene.studio.revision')}</dt>
          <dd>{revision}</dd>
          <dt>{t('asterscene.studio.hash')}</dt>
          <dd>{documentHash.slice(0, 12)}</dd>
          <dt>{t('asterscene.studio.autosave')}</dt>
          <dd>{dirty ? (autosavePending ? t('asterscene.studio.saving') : t('asterscene.studio.dirty')) : t('asterscene.studio.clean')}</dd>
        </dl>
        {project.currentPublishCode ? (
          <Link className="as-button" to={`/player/${project.currentPublishCode}`}>
            {t('asterscene.studio.player')}
          </Link>
        ) : null}
      </div>
      <SceneExplorerPanel
        document={document}
        onSelectActor={selectActor}
        onToggleFrozen={(nextActorId, value) => runCommand(t('asterscene.dcc.tool.freeze'), (state) => setActorDisplayFlag(state, nextActorId, 'frozen', value))}
        onToggleHidden={(nextActorId, value) => runCommand(t('asterscene.dcc.tool.hide'), (state) => setActorDisplayFlag(state, nextActorId, 'hidden', value))}
        selectedActorId={actorId}
        t={t}
      />
      <section className="as-dcc-workspace">
        <StudioViewport
          activePrimitive={activePrimitive}
          document={document}
          onActorSelect={selectActor}
          onPlacementCancel={() => setActivePrimitive(null)}
          onPrimitivePlace={handlePrimitivePlaced}
          onTransformCommit={handleTransformCommit}
          selectedActorId={actorId}
          t={t}
        />
      </section>
      <aside className="as-dcc-right">
        <div aria-label={t('asterscene.dcc.commandPanel')} className="as-dcc-command-tabs" role="tablist">
          {COMMAND_PANEL_TABS.map((tab) => {
            const Icon = tab.icon;
            return (
              <button
                aria-selected={activeCommandPanel === tab.key}
                className={activeCommandPanel === tab.key ? 'is-active' : ''}
                key={tab.key}
                onClick={() => setActiveCommandPanel(tab.key)}
                role="tab"
                title={t(tab.labelKey)}
                type="button"
              >
                <Icon size={16} />
                <span>{t(tab.labelKey)}</span>
              </button>
            );
          })}
        </div>
        <div className="as-dcc-command-stack">
          {activeCommandPanel === 'create' ? <CommandPanel activePrimitiveCode={activePrimitive?.code ?? null} onCreatePrimitive={handleCreatePrimitive} t={t} /> : null}
          {activeCommandPanel === 'modify' ? (
            <ModifyPanel
              document={document}
              onMeshOperation={(operation) => actorId && runCommand(t(`asterscene.dcc.mesh.${operation}`), (state) => mutateEditableMesh(state, actorId, subObjectMode, operation))}
              onRenameActor={(name) => actorId && runCommand(t('asterscene.dcc.command.rename'), (state) => renameActor(state, actorId, name))}
              onTransformChange={(patch) => actorId && runCommand(t('asterscene.studio.commandMoveActor'), (state) => updateActorTransform(state, actorId, patch))}
              onUpsertModifier={(modifier) => actorId && runCommand(t('asterscene.dcc.command.modifier'), (state) => upsertModifier(state, actorId, modifier))}
              selectedActor={selectedActor}
              selectedTransform={selectedTransform}
              t={t}
            />
          ) : null}
          {activeCommandPanel === 'material' ? (
            <MaterialEditorPanel
              document={document}
              onAssignMaterial={(materialId) => actorId && runCommand(t('asterscene.dcc.material.assign'), (state) => assignMaterialToActor(state, actorId, materialId))}
              onCreateMaterial={() => runCommand(t('asterscene.dcc.material.new'), (state) => createStandaloneMaterial(state, t('asterscene.dcc.material.new')))}
              onUpdateMaterialColor={(materialId, value) => runCommand(t('asterscene.studio.commandRecolorMaterial'), (state) => updateMaterialBaseColor(state, materialId, value))}
              selectedMaterialId={selectedMaterial?.id ?? null}
              t={t}
            />
          ) : null}
          {activeCommandPanel === 'hotspot' ? (
            <HotspotPanel
              assets={assets}
              document={document}
              onAddHotspot={(hotspot: SceneHotspot) => runCommand(t('asterscene.studio.commandAddHotspot'), (state) => addSceneHotspot(state, hotspot))}
              onAddPanoramaScene={(asset) => runCommand(t('asterscene.studio.commandAddPanorama'), (state) => upsertPanoramaScene(state, { id: asset.id, kind: 'panorama', url: asset.runtimeUrl ?? undefined }, { sceneName: t('asterscene.studio.panoramaFoyer') }))}
              selectedActorId={actorId}
              t={t}
            />
          ) : null}
          {activeCommandPanel === 'hierarchy' ? <DisabledCommandPanel label={t('asterscene.dcc.panel.hierarchy')} t={t} /> : null}
          {activeCommandPanel === 'display' ? <DisabledCommandPanel label={t('asterscene.dcc.panel.display')} t={t} /> : null}
          {activeCommandPanel === 'utilities' ? <DisabledCommandPanel label={t('asterscene.dcc.panel.utilities')} t={t} /> : null}
        </div>
      </aside>
      <aside className="as-dcc-bottom-left">
        <AssetBrowserPanel assets={assets} onCreateMaterialFromAsset={(asset) => runCommand(t('asterscene.dcc.material.new'), (state) => createMaterialFromAsset(state, asset, asset.fileName))} onPlaceAsset={handlePlaceAsset} t={t} />
      </aside>
      <TimelinePanel dirty={dirty} document={document} documentHash={documentHash} onSetKey={handleSetKey} revision={revision} selectedActorId={actorId} t={t} />
      <button className="as-dcc-floating-hall" onClick={() => runCommand(t('asterscene.studio.commandCreateHall'), (state) => applyComplexExhibitionHall(state, createHallLabels(t)))} type="button">
        {t('asterscene.studio.hall')}
      </button>
    </main>
  );
}

export const EditorShell = DccWorkbenchShell;

function DisabledCommandPanel({ label, t }: { label: string; t: (key: string) => string }) {
  return (
    <section className="as-dcc-command-panel as-dcc-command-panel--disabled">
      <header>
        <h2>{label}</h2>
        <span>{t('asterscene.dcc.panel.pending')}</span>
      </header>
      <p className="as-dcc-muted">{t('asterscene.dcc.panel.pendingDetail')}</p>
    </section>
  );
}

function createHallLabels(t: (key: string) => string): ExhibitionHallLabels {
  return {
    actors: {
      booth: t('asterscene.studio.hallActor.booth'),
      ceiling: t('asterscene.studio.hallActor.ceiling'),
      eastWall: t('asterscene.studio.hallActor.eastWall'),
      featurePanel: t('asterscene.studio.hallActor.featurePanel'),
      fillLight: t('asterscene.studio.hallActor.fillLight'),
      floor: t('asterscene.studio.hallActor.floor'),
      introPanel: t('asterscene.studio.hallActor.introPanel'),
      keyLight: t('asterscene.studio.hallActor.keyLight'),
      northWall: t('asterscene.studio.hallActor.northWall'),
      plinth1: t('asterscene.studio.hallActor.plinth1'),
      plinth2: t('asterscene.studio.hallActor.plinth2'),
      plinth3: t('asterscene.studio.hallActor.plinth3'),
      southWall: t('asterscene.studio.hallActor.southWall'),
      wallWashLight: t('asterscene.studio.hallActor.wallWashLight'),
      westWall: t('asterscene.studio.hallActor.westWall')
    },
    materials: {
      accent: t('asterscene.studio.hallMaterial.accent'),
      booth: t('asterscene.studio.hallMaterial.booth'),
      floor: t('asterscene.studio.hallMaterial.floor'),
      media: t('asterscene.studio.hallMaterial.media'),
      wall: t('asterscene.studio.hallMaterial.wall')
    },
    sceneName: t('asterscene.studio.hall')
  };
}

function findActorComponent(document: SceneDocument, actor: SceneActor, type: SceneComponent['type']): SceneComponent | null {
  return document.components.find((component) => actor.components.includes(component.id) && component.type === type) ?? null;
}

function readActorMaterialId(document: SceneDocument, actor: SceneActor): string | null {
  const binding = findActorComponent(document, actor, 'materialBinding');
  return typeof binding?.materialId === 'string' ? binding.materialId : null;
}
