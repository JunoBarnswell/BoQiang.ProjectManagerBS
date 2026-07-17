import type {
  AsterSceneAsset,
  SceneActor,
  SceneComponent,
  SceneDocument,
  SceneGeometry,
  SceneGeometryType,
  SceneMaterial,
  SceneModifier,
  SceneSubObjectMode,
  SceneTimelineKeyframe,
  SceneTimelineTrack,
  SceneVector3
} from '../../model/types';
import { normalizeSceneDocument } from '../scene-document/documentKernel';

import { HalfEdgeMesh } from './HalfEdgeMesh';

export interface PrimitiveDefinition {
  actorType: SceneActor['type'];
  code: PrimitiveCode;
  componentType?: SceneComponent['type'];
  geometryType?: SceneGeometryType;
  icon: string;
  labelKey: string;
  parameters: Record<string, unknown>;
}

export interface PrimitivePlacement {
  position: SceneVector3;
  parameters?: Record<string, unknown>;
}

export interface TransformPatch {
  position?: SceneVector3;
  rotation?: SceneVector3;
  scale?: SceneVector3;
}

export interface ModifierParameterDefinition {
  defaultValue: boolean | number | string;
  key: string;
  max?: number;
  min?: number;
  step?: number;
  type: 'boolean' | 'number' | 'select';
  values?: string[];
}

export interface ModifierDefinition {
  parameters: ModifierParameterDefinition[];
  previewSupported: boolean;
  type: SceneModifier['type'];
}

export type PrimitiveCode =
  | 'box'
  | 'camera'
  | 'ceiling'
  | 'collider'
  | 'cone'
  | 'cylinder'
  | 'door'
  | 'helper'
  | 'hotspot'
  | 'light'
  | 'mediaSurface'
  | 'panoramaPortal'
  | 'plane'
  | 'plinth'
  | 'showcase'
  | 'sphere'
  | 'torus'
  | 'tube'
  | 'wall'
  | 'window';

export const primitiveCatalog: PrimitiveDefinition[] = [
  { actorType: 'mesh', code: 'box', geometryType: 'box', icon: 'cube', labelKey: 'asterscene.dcc.primitive.box', parameters: { depth: 1, height: 1, width: 1 } },
  { actorType: 'mesh', code: 'plane', geometryType: 'plane', icon: 'square', labelKey: 'asterscene.dcc.primitive.plane', parameters: { depth: 2, width: 2 } },
  { actorType: 'mesh', code: 'sphere', geometryType: 'sphere', icon: 'circle', labelKey: 'asterscene.dcc.primitive.sphere', parameters: { radius: 0.55, segments: 32 } },
  { actorType: 'mesh', code: 'cylinder', geometryType: 'cylinder', icon: 'cylinder', labelKey: 'asterscene.dcc.primitive.cylinder', parameters: { height: 1.4, radiusBottom: 0.5, radiusTop: 0.5 } },
  { actorType: 'mesh', code: 'cone', geometryType: 'cone', icon: 'cone', labelKey: 'asterscene.dcc.primitive.cone', parameters: { height: 1.4, radius: 0.55 } },
  { actorType: 'mesh', code: 'torus', geometryType: 'torus', icon: 'torus', labelKey: 'asterscene.dcc.primitive.torus', parameters: { radius: 0.55, tube: 0.16 } },
  { actorType: 'mesh', code: 'tube', geometryType: 'tube', icon: 'tube', labelKey: 'asterscene.dcc.primitive.tube', parameters: { height: 1.8, radius: 0.18 } },
  { actorType: 'structure', code: 'wall', geometryType: 'wall', icon: 'wall', labelKey: 'asterscene.dcc.primitive.wall', parameters: { depth: 0.18, height: 3, width: 4 } },
  { actorType: 'structure', code: 'door', geometryType: 'door', icon: 'door', labelKey: 'asterscene.dcc.primitive.door', parameters: { depth: 0.12, height: 2.2, width: 1.1 } },
  { actorType: 'structure', code: 'window', geometryType: 'window', icon: 'window', labelKey: 'asterscene.dcc.primitive.window', parameters: { depth: 0.1, height: 1.1, width: 1.6 } },
  { actorType: 'structure', code: 'ceiling', geometryType: 'ceiling', icon: 'ceiling', labelKey: 'asterscene.dcc.primitive.ceiling', parameters: { depth: 4, height: 0.1, width: 4 } },
  { actorType: 'exhibit', code: 'plinth', geometryType: 'box', icon: 'plinth', labelKey: 'asterscene.dcc.primitive.plinth', parameters: { depth: 1, height: 0.85, width: 1.2 } },
  { actorType: 'exhibit', code: 'showcase', geometryType: 'box', icon: 'showcase', labelKey: 'asterscene.dcc.primitive.showcase', parameters: { depth: 1.1, height: 1.6, width: 1.6 } },
  { actorType: 'mesh', code: 'mediaSurface', componentType: 'mediaSurface', geometryType: 'plane', icon: 'panel', labelKey: 'asterscene.dcc.primitive.mediaSurface', parameters: { depth: 1, width: 1.8 } },
  { actorType: 'light', code: 'light', componentType: 'light', icon: 'light', labelKey: 'asterscene.dcc.primitive.light', parameters: {} },
  { actorType: 'camera', code: 'camera', componentType: 'camera', icon: 'camera', labelKey: 'asterscene.dcc.primitive.camera', parameters: {} },
  { actorType: 'hotspot', code: 'hotspot', componentType: 'hotspotAnchor', icon: 'hotspot', labelKey: 'asterscene.dcc.primitive.hotspot', parameters: {} },
  { actorType: 'helper', code: 'helper', componentType: 'helper', icon: 'helper', labelKey: 'asterscene.dcc.primitive.helper', parameters: {} },
  { actorType: 'helper', code: 'collider', componentType: 'collider', icon: 'collider', labelKey: 'asterscene.dcc.primitive.collider', parameters: {} },
  { actorType: 'helper', code: 'panoramaPortal', componentType: 'hotspotAnchor', icon: 'portal', labelKey: 'asterscene.dcc.primitive.panoramaPortal', parameters: {} }
];

export const modifierCatalog: ModifierDefinition[] = [
  {
    parameters: [
      { defaultValue: 'object', key: 'selectionMode', type: 'select', values: ['object', 'vertex', 'edge', 'polygon', 'element'] }
    ],
    previewSupported: true,
    type: 'editPoly'
  },
  {
    parameters: [
      { defaultValue: 0, key: 'offsetX', step: 0.05, type: 'number' },
      { defaultValue: 0, key: 'offsetY', step: 0.05, type: 'number' },
      { defaultValue: 1, key: 'repeatX', min: 0.01, step: 0.05, type: 'number' },
      { defaultValue: 1, key: 'repeatY', min: 0.01, step: 0.05, type: 'number' },
      { defaultValue: 0, key: 'rotation', step: 1, type: 'number' }
    ],
    previewSupported: true,
    type: 'uvwMap'
  },
  {
    parameters: [
      { defaultValue: 0, key: 'translateX', step: 0.1, type: 'number' },
      { defaultValue: 0, key: 'translateY', step: 0.1, type: 'number' },
      { defaultValue: 0, key: 'translateZ', step: 0.1, type: 'number' },
      { defaultValue: 1, key: 'scaleX', min: 0.01, step: 0.05, type: 'number' },
      { defaultValue: 1, key: 'scaleY', min: 0.01, step: 0.05, type: 'number' },
      { defaultValue: 1, key: 'scaleZ', min: 0.01, step: 0.05, type: 'number' }
    ],
    previewSupported: true,
    type: 'xform'
  },
  {
    parameters: [{ defaultValue: 15, key: 'angle', step: 1, type: 'number' }],
    previewSupported: false,
    type: 'bend'
  },
  {
    parameters: [{ defaultValue: 0.2, key: 'amount', step: 0.05, type: 'number' }],
    previewSupported: false,
    type: 'taper'
  },
  {
    parameters: [{ defaultValue: 30, key: 'angle', step: 1, type: 'number' }],
    previewSupported: false,
    type: 'twist'
  },
  {
    parameters: [{ defaultValue: 0.04, key: 'thickness', min: 0, step: 0.01, type: 'number' }],
    previewSupported: false,
    type: 'shell'
  },
  {
    parameters: [
      { defaultValue: 3, key: 'count', min: 1, step: 1, type: 'number' },
      { defaultValue: 1, key: 'offsetX', step: 0.1, type: 'number' },
      { defaultValue: 0, key: 'offsetY', step: 0.1, type: 'number' },
      { defaultValue: 0, key: 'offsetZ', step: 0.1, type: 'number' }
    ],
    previewSupported: true,
    type: 'array'
  },
  {
    parameters: [
      { defaultValue: true, key: 'axisX', type: 'boolean' },
      { defaultValue: false, key: 'axisY', type: 'boolean' },
      { defaultValue: false, key: 'axisZ', type: 'boolean' }
    ],
    previewSupported: true,
    type: 'mirror'
  },
  {
    parameters: [{ defaultValue: 1, key: 'iterations', min: 1, step: 1, type: 'number' }],
    previewSupported: true,
    type: 'subdivide'
  },
  {
    parameters: [
      { defaultValue: 'union', key: 'operation', type: 'select', values: ['union', 'subtract', 'intersect'] },
      { defaultValue: '', key: 'operandActorId', type: 'select', values: [] }
    ],
    previewSupported: false,
    type: 'boolean'
  }
];

export function addPrimitiveActor(document: SceneDocument, definition: PrimitiveDefinition, name: string, placement?: PrimitivePlacement): { actorId: string; document: SceneDocument } {
  const idSeed = `${definition.code}_${crypto.randomUUID().replaceAll('-', '').slice(0, 10)}`;
  const actorId = `actor_${idSeed}`;
  const transformId = `${actorId}_transform`;
  const parameters = {
    ...definition.parameters,
    ...(placement?.parameters ?? {})
  };
  const components: SceneComponent[] = [
    {
      id: transformId,
      position: placement?.position ?? nextPlacement(document.actors.length),
      rotation: { x: 0, y: 0, z: 0 },
      scale: { x: 1, y: 1, z: 1 },
      type: 'transform'
    }
  ];
  const actorComponentIds = [transformId];
  const geometries = [...document.geometries];
  const materials = [...document.materials];

  if (definition.componentType === 'light') {
    const lightId = `${actorId}_light`;
    components.push({ color: '#ffffff', id: lightId, intensity: 1.1, range: 8, type: 'light' });
    actorComponentIds.push(lightId);
  } else if (definition.componentType === 'camera') {
    const cameraId = `${actorId}_camera`;
    components.push({ fov: 50, id: cameraId, near: 0.1, far: 300, type: 'camera' });
    actorComponentIds.push(cameraId);
  } else if (definition.componentType === 'helper' || definition.componentType === 'collider' || definition.componentType === 'hotspotAnchor') {
    const helperId = `${actorId}_${definition.componentType}`;
    components.push({ id: helperId, shape: definition.code, type: definition.componentType });
    actorComponentIds.push(helperId);
  } else {
    const geometryId = `${actorId}_geometry`;
    const meshId = `${actorId}_mesh`;
    const materialId = `${actorId}_material`;
    const bindingId = `${actorId}_materialBinding`;
    const modifierId = `${actorId}_modifiers`;
    const slotsId = `${actorId}_slots`;
    const material = createDefaultMaterial(materialId, name);
    geometries.push(createGeometry(geometryId, definition, parameters));
    materials.push(material);
    components.push(
      { castShadow: true, geometryId, id: meshId, receiveShadow: true, type: 'mesh' },
      { id: bindingId, materialId, type: 'materialBinding' },
      { id: slotsId, slots: [{ id: `${materialId}_slot`, materialId, name: 'Slot 1' }], type: 'materialSlots' },
      { id: modifierId, modifiers: [{ enabled: true, id: `${actorId}_editPoly`, name: 'Edit Poly', parameters: {}, type: 'editPoly' }], type: 'modifierStack' }
    );
    actorComponentIds.push(meshId, bindingId, slotsId, modifierId);
  }

  const actor: SceneActor = {
    components: actorComponentIds,
    display: { hidden: false, frozen: false, showWire: false },
    flags: { castShadow: true, receiveShadow: true, renderable: definition.actorType !== 'helper', selectable: true },
    id: actorId,
    layerId: 'layer_default',
    name,
    parentId: null,
    pivot: { x: 0, y: 0, z: 0 },
    tags: [definition.code],
    type: definition.actorType
  };

  return {
    actorId,
    document: normalizeSceneDocument({
      ...document,
      actors: [...document.actors, actor],
      components: [...document.components, ...components],
      geometries,
      materials,
      runtime: {
        ...document.runtime,
        scenes: document.runtime.scenes.map((scene, index) =>
          scene.id === document.runtime.entrySceneId || index === 0
            ? { ...scene, actors: [...new Set([...scene.actors, actorId])] }
            : scene
        )
      }
    })
  };
}

export function placeAssetActor(document: SceneDocument, asset: AsterSceneAsset): { actorId: string; document: SceneDocument } {
  if (normalizeAssetKind(asset.assetType) === 'model') {
    return placeModelAssetActor(document, asset);
  }

  const definition = primitiveCatalog.find((item) => item.code === 'box') ?? primitiveCatalog[0];
  const placed = addPrimitiveActor(document, definition, asset.fileName.replace(/\.[^.]+$/, ''));
  const assetRef = {
    id: asset.id,
    kind: normalizeAssetKind(asset.assetType),
    metadata: asset.metadata ?? undefined,
    url: asset.runtimeUrl ?? undefined,
    version: asset.currentVersion
  };
  return {
    actorId: placed.actorId,
    document: normalizeSceneDocument({
      ...placed.document,
      actors: placed.document.actors.map((actor) => (actor.id === placed.actorId ? { ...actor, tags: [...(actor.tags ?? []), 'asset', asset.assetType.toLowerCase()] } : actor)),
      assets: upsertAssetRef(placed.document.assets, assetRef),
      components: placed.document.components.map((component) =>
        component.id === `${placed.actorId}_mesh`
          ? { ...component, assetId: asset.id, sourceUrl: asset.runtimeUrl }
          : component
      )
    })
  };
}

function placeModelAssetActor(document: SceneDocument, asset: AsterSceneAsset): { actorId: string; document: SceneDocument } {
  const idSeed = `model_${crypto.randomUUID().replaceAll('-', '').slice(0, 10)}`;
  const actorId = `actor_${idSeed}`;
  const transformId = `${actorId}_transform`;
  const rendererId = `${actorId}_meshRenderer`;
  const geometryId = `${actorId}_geometry`;
  const materialId = `${actorId}_material`;
  const bindingId = `${actorId}_materialBinding`;
  const slotsId = `${actorId}_slots`;
  const modifierId = `${actorId}_modifiers`;
  const material = createDefaultMaterial(materialId, asset.fileName.replace(/\.[^.]+$/, ''));
  const actor: SceneActor = {
    components: [transformId, rendererId, bindingId, slotsId, modifierId],
    display: { hidden: false, frozen: false, showWire: false },
    flags: { castShadow: true, receiveShadow: true, renderable: true, selectable: true },
    id: actorId,
    layerId: 'layer_default',
    name: asset.fileName.replace(/\.[^.]+$/, ''),
    parentId: null,
    pivot: { x: 0, y: 0, z: 0 },
    tags: ['asset', 'model'],
    type: 'mesh'
  };
  const assetRef = {
    id: asset.id,
    kind: 'model',
    metadata: asset.metadata ?? undefined,
    url: asset.runtimeUrl ?? undefined,
    version: asset.currentVersion
  };

  return {
    actorId,
    document: normalizeSceneDocument({
      ...document,
      actors: [...document.actors, actor],
      assets: upsertAssetRef(document.assets, assetRef),
      components: [
        ...document.components,
        {
          id: transformId,
          position: nextPlacement(document.actors.length),
          rotation: { x: 0, y: 0, z: 0 },
          scale: { x: 1, y: 1, z: 1 },
          type: 'transform'
        },
        {
          assetId: asset.id,
          castShadow: true,
          geometryId,
          id: rendererId,
          receiveShadow: true,
          sourceUrl: asset.runtimeUrl ?? undefined,
          type: 'meshRenderer'
        },
        { id: bindingId, materialId, type: 'materialBinding' },
        { id: slotsId, slots: [{ id: `${materialId}_slot`, materialId, name: 'Slot 1' }], type: 'materialSlots' },
        { id: modifierId, modifiers: [], type: 'modifierStack' }
      ],
      geometries: [
        ...document.geometries,
        {
          generatedMeshRef: {
            assetId: asset.id,
            version: asset.currentVersion
          },
          id: geometryId,
          parameters: {
            assetId: asset.id,
            sourceUrl: asset.runtimeUrl ?? undefined
          },
          type: 'generatedMesh'
        }
      ],
      materials: [...document.materials, material],
      runtime: {
        ...document.runtime,
        scenes: document.runtime.scenes.map((scene, index) =>
          scene.id === document.runtime.entrySceneId || index === 0
            ? { ...scene, actors: [...new Set([...scene.actors, actorId])] }
            : scene
        )
      }
    })
  };
}

export function addTimelineKeyframe(document: SceneDocument, track: Omit<SceneTimelineTrack, 'id' | 'keyframes'>, keyframe: SceneTimelineKeyframe): SceneDocument {
  const existingTrack = document.timeline.tracks.find((item) => item.targetId === track.targetId && item.property === track.property);
  const tracks = existingTrack
    ? document.timeline.tracks.map((item) =>
        item.id === existingTrack.id
          ? { ...item, keyframes: upsertFrame(item.keyframes, keyframe) }
          : item
      )
    : [
        ...document.timeline.tracks,
        {
          ...track,
          id: `track_${crypto.randomUUID().replaceAll('-', '').slice(0, 12)}`,
          keyframes: [keyframe]
        }
      ];

  return normalizeSceneDocument({
    ...document,
    timeline: {
      ...document.timeline,
      currentFrame: keyframe.frame,
      frameRate: document.timeline.frameRate ?? 30,
      range: document.timeline.range ?? { end: 180, start: 0 },
      tracks
    }
  });
}

export function alignActorToTarget(document: SceneDocument, actorId: string, targetId: string, axes: Array<keyof SceneVector3>): SceneDocument {
  const sourceTransform = readActorTransform(document, actorId);
  const targetTransform = readActorTransform(document, targetId);
  if (!sourceTransform || !targetTransform) {
    return document;
  }

  const position = {
    ...sourceTransform.position,
    ...axes.reduce<Partial<SceneVector3>>((current, axis) => ({ ...current, [axis]: targetTransform.position[axis] }), {})
  };
  return updateActorTransform(document, actorId, { position });
}

export function assignMaterialToActor(document: SceneDocument, actorId: string, materialId: string): SceneDocument {
  const actor = document.actors.find((item) => item.id === actorId);
  if (!actor) {
    return document;
  }

  return normalizeSceneDocument({
    ...document,
    components: document.components.map((component) =>
      actor.components.includes(component.id) && component.type === 'materialBinding'
        ? { ...component, materialId }
        : component
    )
  });
}

export function createMaterialFromAsset(document: SceneDocument, asset: AsterSceneAsset, name: string): SceneDocument {
  const materialId = `mat_${crypto.randomUUID().replaceAll('-', '').slice(0, 12)}`;
  const material = createDefaultMaterial(materialId, name);
  const assetKind = normalizeAssetKind(asset.assetType);
  return normalizeSceneDocument({
    ...document,
    assets: upsertAssetRef(
      document.assets,
      {
        id: asset.id,
        kind: assetKind,
        metadata: asset.metadata ?? undefined,
        url: asset.runtimeUrl ?? undefined,
        version: asset.currentVersion
      }
    ),
    materials: [
      ...document.materials,
      {
        ...material,
        pbr: {
          ...material.pbr,
          textureSlots: assetKind === 'texture' || assetKind === 'image' || assetKind === 'hdri'
            ? {
                baseColor: {
                  assetId: asset.id,
                  url: asset.runtimeUrl ?? undefined
                }
              }
            : material.pbr?.textureSlots
        }
      }
    ]
  });
}

export function createStandaloneMaterial(document: SceneDocument, name: string): SceneDocument {
  const materialId = `mat_${crypto.randomUUID().replaceAll('-', '').slice(0, 12)}`;
  return normalizeSceneDocument({
    ...document,
    materials: [...document.materials, createDefaultMaterial(materialId, name)]
  });
}

export function duplicateActor(document: SceneDocument, actorId: string): { actorId: string | null; document: SceneDocument } {
  const actor = document.actors.find((item) => item.id === actorId);
  if (!actor) {
    return { actorId: null, document };
  }

  const suffix = crypto.randomUUID().replaceAll('-', '').slice(0, 10);
  const componentIdMap = new Map<string, string>();
  actor.components.forEach((componentId) => componentIdMap.set(componentId, `${componentId}_${suffix}`));
  const nextActorId = `${actor.id}_${suffix}`;
  const components: SceneComponent[] = document.components
    .filter((component) => actor.components.includes(component.id))
    .map((component) => ({ ...component, id: componentIdMap.get(component.id) ?? `${component.id}_${suffix}` }));
  const duplicatedActor: SceneActor = {
    ...actor,
    components: actor.components.map((componentId) => componentIdMap.get(componentId) ?? componentId),
    id: nextActorId,
    name: `${actor.name} Copy`
  };
  const transform = components.find((component) => component.type === 'transform');
  if (transform) {
    const position = readVectorRecord(transform.position, { x: 0, y: 0, z: 0 });
    transform.position = { ...position, x: position.x + 0.8, z: position.z + 0.8 };
  }

  return {
    actorId: nextActorId,
    document: normalizeSceneDocument({
      ...document,
      actors: [...document.actors, duplicatedActor],
      components: [...document.components, ...components],
      runtime: {
        ...document.runtime,
        scenes: document.runtime.scenes.map((scene) =>
          scene.actors.includes(actorId) ? { ...scene, actors: [...scene.actors, nextActorId] } : scene
        )
      }
    })
  };
}

export function mutateEditableMesh(document: SceneDocument, actorId: string, mode: SceneSubObjectMode, operation: string): SceneDocument {
  const actor = document.actors.find((item) => item.id === actorId);
  const meshComponent = actor ? findActorComponent(document, actor, 'mesh') : null;
  const geometry = meshComponent ? document.geometries.find((item) => item.id === readString(meshComponent.geometryId)) : null;
  if (!actor || !geometry) {
    return document;
  }

  const mesh = new HalfEdgeMesh(geometry.editableMeshRef?.inline ?? HalfEdgeMesh.box(1, 1, 1).toPayload());
  const faceSelection = mode === 'object' ? mesh.faces.map((_, index) => index) : [0];
  const edgeSelection = [0, 1];
  const vertexSelection = [0, 1, 2, 3];
  const nextMesh = applyMeshOperation(mesh, operation, faceSelection, edgeSelection, vertexSelection);
  const validation = nextMesh.validate();
  const nextGeometry: SceneGeometry = {
    ...geometry,
    editableMeshRef: { inline: nextMesh.toPayload() },
    topology: validation.summary,
    type: 'editableMesh'
  };

  return normalizeSceneDocument({
    ...document,
    geometries: document.geometries.map((item) => (item.id === geometry.id ? nextGeometry : item))
  });
}

export function removeActor(document: SceneDocument, actorId: string): SceneDocument {
  const actor = document.actors.find((item) => item.id === actorId);
  const componentIds = new Set(actor?.components ?? []);
  return normalizeSceneDocument({
    ...document,
    actors: document.actors.filter((item) => item.id !== actorId && item.parentId !== actorId),
    components: document.components.filter((component) => !componentIds.has(component.id)),
    runtime: {
      ...document.runtime,
      scenes: document.runtime.scenes.map((scene) => ({
        ...scene,
        actors: scene.actors.filter((id) => id !== actorId)
      }))
    }
  });
}

export function renameActor(document: SceneDocument, actorId: string, name: string): SceneDocument {
  return normalizeSceneDocument({
    ...document,
    actors: document.actors.map((actor) => (actor.id === actorId ? { ...actor, name: name.trim() || actor.name } : actor))
  });
}

export function setActorDisplayFlag(document: SceneDocument, actorId: string, flag: 'frozen' | 'hidden' | 'showWire', value: boolean): SceneDocument {
  return normalizeSceneDocument({
    ...document,
    actors: document.actors.map((actor) =>
      actor.id === actorId ? { ...actor, display: { ...actor.display, [flag]: value } } : actor
    )
  });
}

export function updateActorTransform(document: SceneDocument, actorId: string, patch: TransformPatch): SceneDocument {
  const actor = document.actors.find((item) => item.id === actorId);
  if (!actor) {
    return document;
  }

  return normalizeSceneDocument({
    ...document,
    components: document.components.map((component) => {
      if (!actor.components.includes(component.id) || component.type !== 'transform') {
        return component;
      }

      return {
        ...component,
        position: patch.position ?? component.position,
        rotation: patch.rotation ?? component.rotation,
        scale: patch.scale ?? component.scale
      };
    })
  });
}

export function upsertModifier(document: SceneDocument, actorId: string, modifier: SceneModifier): SceneDocument {
  const actor = document.actors.find((item) => item.id === actorId);
  if (!actor) {
    return document;
  }

  const normalizedModifier = normalizeModifier(modifier);
  const stack = document.components.find((component) => actor.components.includes(component.id) && component.type === 'modifierStack');
  if (!stack) {
    const componentId = createComponentId(document, actorId, 'modifiers');
    return normalizeSceneDocument({
      ...document,
      actors: document.actors.map((item) =>
        item.id === actorId ? { ...item, components: [...item.components, componentId] } : item
      ),
      components: [
        ...document.components,
        {
          id: componentId,
          modifiers: [normalizedModifier],
          type: 'modifierStack'
        }
      ]
    });
  }

  return normalizeSceneDocument({
    ...document,
    components: document.components.map((component) => {
      if (!actor.components.includes(component.id) || component.type !== 'modifierStack') {
        return component;
      }

      const modifiers = readModifiers(component);
      const nextModifiers = modifiers.some((item) => item.id === normalizedModifier.id)
        ? modifiers.map((item) => (item.id === normalizedModifier.id ? normalizedModifier : item))
        : [...modifiers, normalizedModifier];
      return { ...component, modifiers: nextModifiers };
    })
  });
}

export function readActorTransform(document: SceneDocument, actorId: string): Required<TransformPatch> | null {
  const actor = document.actors.find((item) => item.id === actorId);
  const transform = actor ? findActorComponent(document, actor, 'transform') : null;
  if (!transform) {
    return null;
  }

  return {
    position: readVectorRecord(transform.position, { x: 0, y: 0, z: 0 }),
    rotation: readVectorRecord(transform.rotation, { x: 0, y: 0, z: 0 }),
    scale: readVectorRecord(transform.scale, { x: 1, y: 1, z: 1 })
  };
}

export function readModifiers(component: SceneComponent): SceneModifier[] {
  return Array.isArray(component.modifiers)
    ? component.modifiers
        .filter((modifier): modifier is Record<string, unknown> => Boolean(modifier) && typeof modifier === 'object')
        .map((modifier) => normalizeModifier(modifier))
    : [];
}

export function createModifier(type: SceneModifier['type'], name: string): SceneModifier {
  return normalizeModifier({
    enabled: true,
    id: `modifier_${type}_${crypto.randomUUID().replaceAll('-', '').slice(0, 8)}`,
    name,
    parameters: {},
    type
  });
}

export function getModifierDefinition(type: SceneModifier['type']): ModifierDefinition {
  return modifierCatalog.find((item) => item.type === type) ?? { parameters: [], previewSupported: false, type };
}

function normalizeModifier(input: SceneModifier | Record<string, unknown>): SceneModifier {
  const type = readString(input.type) || 'editPoly';
  const definition = getModifierDefinition(type);
  const parameters = normalizeModifierParameters(definition, input.parameters);
  const previewSupported = definition.previewSupported;
  return {
    enabled: previewSupported && (typeof input.enabled === 'boolean' ? input.enabled : true),
    id: readString(input.id) || `modifier_${type}_${crypto.randomUUID().replaceAll('-', '').slice(0, 8)}`,
    name: readString(input.name) || type,
    order: readOptionalNumber(input.order),
    parameters,
    previewSupported,
    type
  };
}

function normalizeModifierParameters(definition: ModifierDefinition, current: unknown): Record<string, unknown> {
  const currentRecord = current && typeof current === 'object' ? (current as Record<string, unknown>) : {};
  return definition.parameters.reduce<Record<string, unknown>>((parameters, parameter) => {
    const value = currentRecord[parameter.key];
    if (parameter.type === 'number') {
      parameters[parameter.key] = clampNumber(value, Number(parameter.defaultValue), parameter.min, parameter.max);
      return parameters;
    }

    if (parameter.type === 'boolean') {
      parameters[parameter.key] = typeof value === 'boolean' ? value : Boolean(parameter.defaultValue);
      return parameters;
    }

    const stringValue = typeof value === 'string' ? value : String(parameter.defaultValue);
    parameters[parameter.key] = parameter.values?.length && !parameter.values.includes(stringValue)
      ? String(parameter.defaultValue)
      : stringValue;
    return parameters;
  }, {});
}

function applyMeshOperation(mesh: HalfEdgeMesh, operation: string, faceSelection: number[], edgeSelection: number[], vertexSelection: number[]): HalfEdgeMesh {
  switch (operation) {
    case 'bridge':
      return mesh.bridgeEdges(edgeSelection[0] ?? 0, edgeSelection[1] ?? 1);
    case 'bevel':
      return mesh.insetFaces(faceSelection, 0.14).extrudeFaces(faceSelection, 0.08);
    case 'collapse':
      return mesh.collapseEdges(edgeSelection);
    case 'connect':
      return mesh.subdivideFaces(faceSelection);
    case 'detach':
      return mesh.detachFaces(faceSelection);
    case 'extrude':
      return mesh.extrudeFaces(faceSelection, 0.28);
    case 'flipNormal':
      return mesh.flipFaces(faceSelection);
    case 'inset':
      return mesh.insetFaces(faceSelection, 0.18);
    case 'subdivide':
      return mesh.subdivideFaces(faceSelection);
    case 'weld':
      return mesh.weldVertices(vertexSelection, 0.02);
    default:
      return mesh;
  }
}

function createDefaultMaterial(id: string, name: string): SceneMaterial {
  return {
    baseColor: '#8a9099',
    id,
    metallic: 0,
    name: `${name} Material`,
    pbr: {
      alphaMode: 'opaque',
      baseColor: '#8a9099',
      doubleSided: false,
      metallic: 0,
      opacity: 1,
      roughness: 0.68
    },
    roughness: 0.68,
    type: 'pbr'
  };
}

function createGeometry(id: string, definition: PrimitiveDefinition, parameters: Record<string, unknown> = definition.parameters): SceneGeometry {
  const type = definition.geometryType ?? 'box';
  if (type === 'box' || definition.code === 'box') {
    const mesh = HalfEdgeMesh.box(
      readNumber(parameters.width, 1),
      readNumber(parameters.height, 1),
      readNumber(parameters.depth, 1)
    );
    return {
      editableMeshRef: { inline: mesh.toPayload() },
      id,
      parameters,
      topology: mesh.topologySummary(),
      type: 'editableMesh'
    };
  }

  return {
    id,
    parameters,
    type
  };
}

function findActorComponent(document: SceneDocument, actor: SceneActor, type: SceneComponent['type']): SceneComponent | null {
  return document.components.find((component) => actor.components.includes(component.id) && component.type === type) ?? null;
}

function nextPlacement(actorCount: number): SceneVector3 {
  const column = actorCount % 5;
  const row = Math.floor(actorCount / 5);
  return { x: (column - 2) * 1.4, y: 0.5, z: row * 1.4 };
}

function readNumber(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function readOptionalNumber(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined;
}

function readString(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function readVectorRecord(value: unknown, fallback: SceneVector3): SceneVector3 {
  if (!value || typeof value !== 'object') {
    return fallback;
  }

  const record = value as Record<string, unknown>;
  return {
    x: readNumber(record.x, fallback.x),
    y: readNumber(record.y, fallback.y),
    z: readNumber(record.z, fallback.z)
  };
}

function upsertFrame(keyframes: SceneTimelineKeyframe[], keyframe: SceneTimelineKeyframe): SceneTimelineKeyframe[] {
  const next = keyframes.some((item) => item.frame === keyframe.frame)
    ? keyframes.map((item) => (item.frame === keyframe.frame ? keyframe : item))
    : [...keyframes, keyframe];
  return next.sort((first, second) => first.frame - second.frame);
}

function upsertAssetRef<T extends { id: string }>(items: T[], item: T): T[] {
  return items.some((current) => current.id === item.id) ? items.map((current) => (current.id === item.id ? item : current)) : [...items, item];
}

function normalizeAssetKind(value: string): string {
  return value.trim().toLowerCase();
}

function createComponentId(document: SceneDocument, actorId: string, suffix: string): string {
  const baseId = `${actorId}_${suffix}`;
  if (!document.components.some((component) => component.id === baseId)) {
    return baseId;
  }

  return `${baseId}_${crypto.randomUUID().replaceAll('-', '').slice(0, 8)}`;
}

function clampNumber(value: unknown, fallback: number, min?: number, max?: number): number {
  const numberValue = typeof value === 'number' && Number.isFinite(value) ? value : fallback;
  const minValue = typeof min === 'number' ? min : -Infinity;
  const maxValue = typeof max === 'number' ? max : Infinity;
  return Math.min(maxValue, Math.max(minValue, numberValue));
}
