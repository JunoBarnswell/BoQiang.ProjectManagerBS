import type {
  SceneActor,
  SceneAssetRef,
  SceneComponent,
  SceneDocument,
  SceneGeometry,
  SceneHotspot,
  SceneMaterial,
  SceneMaterialPbrPatch,
  ScenePbrMaterial,
  ScenePbrTextureSlot,
  SceneTextureBinding,
  SceneUvTransform,
  SceneRuntimeScene,
  SceneVector3
} from '../../model/types';

export function createClientMutationId(prefix = 'as'): string {
  return `${prefix}_${Date.now().toString(36)}_${crypto.randomUUID().replaceAll('-', '')}`;
}

export function normalizeSceneDocument(document: SceneDocument): SceneDocument {
  const meta = { ...(document.meta as SceneDocument['meta'] & { schemaVersion?: unknown }) };
  delete meta.schemaVersion;
  return {
    ...document,
    meta: {
      ...meta,
      updatedAt: new Date().toISOString()
    },
    revision: Number.isFinite(document.revision) ? document.revision : 1
  };
}

export async function computeSceneDocumentHash(document: SceneDocument): Promise<string> {
  const payload = serializeSceneDocumentForHash(document);
  const bytes = new TextEncoder().encode(payload);
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(digest))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('');
}

export function serializeSceneDocumentForHash(document: SceneDocument): string {
  return JSON.stringify(document).replace(/[<>&\u007f-\uffff]/g, (character) =>
    `\\u${character.charCodeAt(0).toString(16).toUpperCase().padStart(4, '0')}`
  );
}

export function appendActor(document: SceneDocument, actor: SceneActor): SceneDocument {
  if (document.actors.some((item) => item.id === actor.id)) {
    return document;
  }

  return {
    ...document,
    actors: [...document.actors, actor],
    runtime: {
      ...document.runtime,
      scenes: document.runtime.scenes.map((scene, index) =>
        index === 0 ? { ...scene, actors: [...scene.actors, actor.id] } : scene
      )
    }
  };
}

export interface ExhibitionHallLabels {
  sceneName?: string;
  materials?: Partial<Record<'accent' | 'booth' | 'floor' | 'media' | 'wall', string>>;
  actors?: Partial<
    Record<
      | 'booth'
      | 'ceiling'
      | 'fillLight'
      | 'floor'
      | 'introPanel'
      | 'keyLight'
      | 'featurePanel'
      | 'northWall'
      | 'plinth1'
      | 'plinth2'
      | 'plinth3'
      | 'southWall'
      | 'wallWashLight'
      | 'westWall'
      | 'eastWall',
      string
    >
  >;
}

export interface PanoramaSceneLabels {
  sceneName?: string;
}

export function applyComplexExhibitionHall(document: SceneDocument, labels: ExhibitionHallLabels = {}): SceneDocument {
  const existingRuntimeScenes = document.runtime.scenes.filter((scene) => scene.id !== 'scene_hall');
  const keptActors = document.actors.filter((actor) => !actor.id.startsWith('hall_'));
  const keptComponents = document.components.filter((component) => !component.id.startsWith('hall_'));
  const keptGeometries = document.geometries.filter((geometry) => !geometry.id.startsWith('hall_'));
  const keptMaterials = document.materials.filter((material) => !material.id.startsWith('hall_'));
  const hall = createHallDocumentParts(labels);

  return normalizeSceneDocument({
    ...document,
    actors: [...keptActors, ...hall.actors],
    components: [...keptComponents, ...hall.components],
    geometries: [...keptGeometries, ...hall.geometries],
    interactions: {
      ...document.interactions,
      nav: {
        mode: 'walkthrough',
        collision: true,
        boundaries: { minX: -6.2, maxX: 6.2, minZ: -5.4, maxZ: 5.4 }
      }
    },
    materials: [...keptMaterials, ...hall.materials],
    runtime: {
      ...document.runtime,
      camera: {
        mode: 'orbit',
        position: { x: 6.5, y: 4.2, z: 8 },
        target: { x: 0, y: 1, z: 0 },
        fov: 55
      },
      entrySceneId: 'scene_hall',
      scenes: [
        {
          actors: hall.actors.map((actor) => actor.id),
          id: 'scene_hall',
          name: labels.sceneName ?? 'Complex Exhibition Hall',
          type: 'model3d'
        },
        ...existingRuntimeScenes
      ]
    }
  });
}

export function upsertPanoramaScene(document: SceneDocument, asset: SceneAssetRef, labels: PanoramaSceneLabels = {}): SceneDocument {
  const assetRef: SceneAssetRef = {
    id: asset.id,
    kind: 'panorama',
    url: asset.url
  };
  const assets = upsertById(document.assets, assetRef);
  const panoramaScene: SceneRuntimeScene = {
    actors: [],
    environment: {
      fov: 70,
      initialPitch: 0,
      initialYaw: 0,
      panoramaAssetId: asset.id,
      projection: 'equirectangular',
      stereo: 'mono',
      yawOffset: 0
    },
    id: 'scene_panorama_foyer',
    name: labels.sceneName ?? '720 Panorama Foyer',
    type: 'panorama720'
  };

  return normalizeSceneDocument({
    ...document,
    assets,
    runtime: {
      ...document.runtime,
      scenes: upsertById(document.runtime.scenes, panoramaScene)
    }
  });
}

export function addSceneHotspot(document: SceneDocument, hotspot: SceneHotspot): SceneDocument {
  const normalized: SceneHotspot = {
    enabled: true,
    type: 'navigate',
    ...hotspot
  };

  return normalizeSceneDocument({
    ...document,
    interactions: {
      ...document.interactions,
      hotspots: upsertById(document.interactions.hotspots, normalized)
    }
  });
}

export function updateActorTransform(document: SceneDocument, actorId: string, position: SceneVector3): SceneDocument {
  const actor = document.actors.find((item) => item.id === actorId);
  if (!actor) {
    return document;
  }

  const transformId = actor.components.find((id) => {
    const component = document.components.find((item) => item.id === id);
    return component?.type === 'transform';
  });

  if (!transformId) {
    return document;
  }

  return normalizeSceneDocument({
    ...document,
    components: document.components.map((component) =>
      component.id === transformId
        ? {
            ...component,
            position
          }
        : component
    )
  });
}

export function updateMaterialBaseColor(document: SceneDocument, materialId: string, patch: string | SceneMaterialPbrPatch): SceneDocument {
  const pbrPatch = typeof patch === 'string' ? { baseColor: patch } : patch;
  return normalizeSceneDocument({
    ...document,
    materials: document.materials.map((material) =>
      material.id === materialId
        ? applyMaterialPbrPatch(material, pbrPatch)
        : material
    )
  });
}

function applyMaterialPbrPatch(material: SceneMaterial, patch: SceneMaterialPbrPatch): SceneMaterial {
  const pbr: ScenePbrMaterial = {
    ...material.pbr,
    ...copyDefinedPbrScalarPatch(patch),
    textureSlots: mergeTextureSlots(material.pbr?.textureSlots, patch.textureSlots),
    uvTransform: mergeUvTransform(material.pbr?.uvTransform, patch.uvTransform)
  };

  const next: SceneMaterial = {
    ...material,
    pbr
  };

  if (typeof patch.baseColor === 'string') {
    next.baseColor = patch.baseColor;
  }

  if (typeof patch.emissive === 'string') {
    next.emissive = patch.emissive;
  }

  if (typeof patch.metallic === 'number') {
    next.metallic = patch.metallic;
  }

  if (typeof patch.roughness === 'number') {
    next.roughness = patch.roughness;
  }

  if (typeof patch.opacity === 'number') {
    next.opacity = patch.opacity;
  }

  return next;
}

function copyDefinedPbrScalarPatch(patch: SceneMaterialPbrPatch): ScenePbrMaterial {
  const next: ScenePbrMaterial = {};

  if (patch.alphaMode) {
    next.alphaMode = patch.alphaMode;
  }

  if (typeof patch.baseColor === 'string') {
    next.baseColor = patch.baseColor;
  }

  if (typeof patch.doubleSided === 'boolean') {
    next.doubleSided = patch.doubleSided;
  }

  if (typeof patch.emissive === 'string') {
    next.emissive = patch.emissive;
  }

  if (typeof patch.metallic === 'number') {
    next.metallic = clamp(patch.metallic, 0, 1);
  }

  if (typeof patch.opacity === 'number') {
    next.opacity = clamp(patch.opacity, 0, 1);
  }

  if (typeof patch.roughness === 'number') {
    next.roughness = clamp(patch.roughness, 0, 1);
  }

  return next;
}

function mergeTextureSlots(
  current: ScenePbrMaterial['textureSlots'],
  patch: SceneMaterialPbrPatch['textureSlots']
): ScenePbrMaterial['textureSlots'] {
  if (!patch) {
    return current;
  }

  const next: Partial<Record<ScenePbrTextureSlot, SceneTextureBinding>> = { ...(current ?? {}) };
  (Object.entries(patch) as Array<[ScenePbrTextureSlot, SceneTextureBinding | null | undefined]>).forEach(([slot, binding]) => {
    if (!binding || (!binding.assetId && !binding.url)) {
      delete next[slot];
      return;
    }

    next[slot] = {
      ...binding,
      assetId: binding.assetId?.trim() || undefined,
      url: binding.url?.trim() || undefined
    };
  });

  return Object.keys(next).length > 0 ? next : undefined;
}

function mergeUvTransform(current: SceneUvTransform | undefined, patch: SceneUvTransform | undefined): SceneUvTransform | undefined {
  if (!patch) {
    return current;
  }

  return {
    offset: patch.offset ?? current?.offset,
    repeat: patch.repeat ?? current?.repeat,
    rotation: typeof patch.rotation === 'number' ? patch.rotation : current?.rotation
  };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, Number.isFinite(value) ? value : min));
}

export function renameActor(document: SceneDocument, actorId: string, name: string): SceneDocument {
  return {
    ...document,
    actors: document.actors.map((actor) => (actor.id === actorId ? { ...actor, name } : actor))
  };
}

function createHallDocumentParts(labels: ExhibitionHallLabels): {
  actors: SceneActor[];
  components: SceneComponent[];
  geometries: SceneGeometry[];
  materials: SceneMaterial[];
} {
  const materialLabels = {
    accent: labels.materials?.accent ?? 'Aster Accent Panel',
    booth: labels.materials?.booth ?? 'Warm Booth Finish',
    floor: labels.materials?.floor ?? 'Polished Dark Floor',
    media: labels.materials?.media ?? 'Media Surface',
    wall: labels.materials?.wall ?? 'Gallery White Wall'
  };
  const actorLabels = {
    booth: labels.actors?.booth ?? 'Central Brand Booth',
    ceiling: labels.actors?.ceiling ?? 'Acoustic Ceiling',
    eastWall: labels.actors?.eastWall ?? 'East Wall',
    featurePanel: labels.actors?.featurePanel ?? 'Feature Media Panel',
    fillLight: labels.actors?.fillLight ?? 'Fill Area Light',
    floor: labels.actors?.floor ?? 'Main Floor',
    introPanel: labels.actors?.introPanel ?? 'Intro Media Panel',
    keyLight: labels.actors?.keyLight ?? 'Key Area Light',
    northWall: labels.actors?.northWall ?? 'North Wall',
    plinth1: labels.actors?.plinth1 ?? 'Product Plinth 1',
    plinth2: labels.actors?.plinth2 ?? 'Product Plinth 2',
    plinth3: labels.actors?.plinth3 ?? 'Product Plinth 3',
    southWall: labels.actors?.southWall ?? 'South Wall',
    wallWashLight: labels.actors?.wallWashLight ?? 'Wall Wash Light',
    westWall: labels.actors?.westWall ?? 'West Wall'
  };
  const materials: SceneMaterial[] = [
    { baseColor: '#eef2f7', id: 'hall_mat_wall', metallic: 0, name: materialLabels.wall, roughness: 0.78, type: 'pbr' },
    { baseColor: '#1f2937', id: 'hall_mat_floor', metallic: 0, name: materialLabels.floor, roughness: 0.42, type: 'pbr' },
    { baseColor: '#d7b46a', id: 'hall_mat_booth', metallic: 0.1, name: materialLabels.booth, roughness: 0.38, type: 'pbr' },
    { baseColor: '#0f766e', id: 'hall_mat_accent', metallic: 0, name: materialLabels.accent, roughness: 0.5, type: 'pbr' },
    { baseColor: '#ffffff', emissive: '#f8fbff', id: 'hall_mat_media', metallic: 0, name: materialLabels.media, roughness: 0.28, type: 'pbr' }
  ];
  const geometries: SceneGeometry[] = [
    { id: 'hall_geo_floor', parameters: { depth: 11, height: 0.12, width: 13 }, type: 'box' },
    { id: 'hall_geo_wall_long', parameters: { depth: 0.18, height: 3.2, width: 13 }, type: 'box' },
    { id: 'hall_geo_wall_short', parameters: { depth: 11, height: 3.2, width: 0.18 }, type: 'box' },
    { id: 'hall_geo_ceiling', parameters: { depth: 11, height: 0.12, width: 13 }, type: 'box' },
    { id: 'hall_geo_plinth', parameters: { depth: 1.2, height: 0.9, width: 1.5 }, type: 'box' },
    { id: 'hall_geo_booth', parameters: { depth: 1.8, height: 2.2, width: 2.6 }, type: 'box' },
    { id: 'hall_geo_panel', parameters: { depth: 0.08, height: 1.35, width: 2 }, type: 'box' }
  ];
  const components: SceneComponent[] = [];
  const actors: SceneActor[] = [];

  const addMesh = (
    id: string,
    name: string,
    geometryId: string,
    materialId: string,
    position: SceneVector3,
    scale: SceneVector3 = { x: 1, y: 1, z: 1 },
    rotation: SceneVector3 = { x: 0, y: 0, z: 0 },
    type: SceneActor['type'] = 'structure'
  ) => {
    const transformId = `${id}_transform`;
    const meshId = `${id}_mesh`;
    const materialBindingId = `${id}_material`;
    actors.push({ components: [transformId, meshId, materialBindingId], id, name, type });
    components.push(
      { id: transformId, position, rotation, scale, type: 'transform' },
      { castShadow: false, geometryId, id: meshId, receiveShadow: true, type: 'mesh' },
      { id: materialBindingId, materialId, type: 'materialBinding' }
    );
  };

  addMesh('hall_floor', actorLabels.floor, 'hall_geo_floor', 'hall_mat_floor', { x: 0, y: -0.06, z: 0 });
  addMesh('hall_ceiling', actorLabels.ceiling, 'hall_geo_ceiling', 'hall_mat_wall', { x: 0, y: 3.25, z: 0 });
  addMesh('hall_wall_north', actorLabels.northWall, 'hall_geo_wall_long', 'hall_mat_wall', { x: 0, y: 1.55, z: -5.5 });
  addMesh('hall_wall_south', actorLabels.southWall, 'hall_geo_wall_long', 'hall_mat_wall', { x: 0, y: 1.55, z: 5.5 });
  addMesh('hall_wall_west', actorLabels.westWall, 'hall_geo_wall_short', 'hall_mat_wall', { x: -6.5, y: 1.55, z: 0 });
  addMesh('hall_wall_east', actorLabels.eastWall, 'hall_geo_wall_short', 'hall_mat_wall', { x: 6.5, y: 1.55, z: 0 });
  addMesh('hall_booth_center', actorLabels.booth, 'hall_geo_booth', 'hall_mat_booth', { x: 0, y: 1.1, z: -0.7 });
  addMesh('hall_panel_intro', actorLabels.introPanel, 'hall_geo_panel', 'hall_mat_media', { x: -2.2, y: 1.45, z: -4.85 });
  addMesh('hall_panel_feature', actorLabels.featurePanel, 'hall_geo_panel', 'hall_mat_accent', { x: 2.2, y: 1.45, z: -4.85 });

  [
    ['hall_plinth_1', actorLabels.plinth1, -3.5, 0.45, 1.9],
    ['hall_plinth_2', actorLabels.plinth2, 0, 0.45, 2.25],
    ['hall_plinth_3', actorLabels.plinth3, 3.5, 0.45, 1.9]
  ].forEach(([id, name, x, y, z]) => {
    addMesh(String(id), String(name), 'hall_geo_plinth', 'hall_mat_booth', { x: Number(x), y: Number(y), z: Number(z) }, undefined, undefined, 'exhibit');
  });

  [
    ['hall_light_key', actorLabels.keyLight, -3.8, 2.8, 1.8, 1.25],
    ['hall_light_fill', actorLabels.fillLight, 3.8, 2.8, 1.8, 0.9],
    ['hall_light_wallwash', actorLabels.wallWashLight, 0, 2.7, -3.9, 0.75]
  ].forEach(([id, name, x, y, z, intensity]) => {
    const transformId = `${id}_transform`;
    const lightId = `${id}_light`;
    actors.push({ components: [transformId, lightId], id: String(id), name: String(name), type: 'light' });
    components.push(
      { id: transformId, position: { x: Number(x), y: Number(y), z: Number(z) }, rotation: { x: 0, y: 0, z: 0 }, scale: { x: 1, y: 1, z: 1 }, type: 'transform' },
      { color: '#ffffff', id: lightId, intensity: Number(intensity), range: 8, type: 'light' }
    );
  });

  return { actors, components, geometries, materials };
}

function upsertById<T extends { id: string }>(items: T[], next: T): T[] {
  const index = items.findIndex((item) => item.id === next.id);
  if (index < 0) {
    return [...items, next];
  }

  return items.map((item) => (item.id === next.id ? next : item));
}

export function removeActor(document: SceneDocument, actorId: string): SceneDocument {
  return {
    ...document,
    actors: document.actors.filter((actor) => actor.id !== actorId),
    runtime: {
      ...document.runtime,
      scenes: document.runtime.scenes.map((scene) => ({
        ...scene,
        actors: scene.actors.filter((id) => id !== actorId)
      }))
    }
  };
}
