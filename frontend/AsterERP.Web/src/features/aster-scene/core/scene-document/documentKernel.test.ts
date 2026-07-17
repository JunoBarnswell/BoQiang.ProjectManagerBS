import { describe, expect, it } from 'vitest';

import type { SceneActor, SceneDocument } from '../../model/types';

import {
  addSceneHotspot,
  appendActor,
  applyComplexExhibitionHall,
  computeSceneDocumentHash,
  normalizeSceneDocument,
  removeActor,
  renameActor,
  serializeSceneDocumentForHash,
  updateActorTransform,
  updateMaterialBaseColor,
  upsertPanoramaScene
} from './documentKernel';

function makeDocument(): SceneDocument {
  return {
    actors: [
      {
        components: ['transform_floor'],
        id: 'actor_floor',
        name: 'Floor',
        type: 'mesh'
      }
    ],
    assets: [],
    components: [{ id: 'transform_floor', type: 'transform' }],
    extensions: {},
    geometries: [],
    identity: {
      documentId: 'doc-1',
      locale: 'zh-CN',
      projectId: 'project-1'
    },
    interactions: {
      blueprints: [],
      hotspots: [],
      nav: {}
    },
    materials: [],
    meta: {
      product: 'AsterScene',
      title: 'Factory',
      updatedAt: '2026-01-01T00:00:00.000Z'
    },
    publish: {
      license: 'standard-remix',
      slug: null,
      visibility: 'Private'
    },
    quality: {},
    revision: 1,
    runtime: {
      camera: {},
      entrySceneId: 'scene_main',
      scenes: [
        {
          actors: ['actor_floor'],
          id: 'scene_main',
          name: 'Main'
        }
      ]
    },
    timeline: {
      sequences: [],
      tracks: []
    },
    uv: {
      layouts: []
    }
  };
}

describe('AsterScene document kernel', () => {
  it('appends and removes actors while keeping runtime scene actor references consistent', () => {
    const actor: SceneActor = {
      components: ['transform_box'],
      id: 'actor_box',
      name: 'Box',
      type: 'mesh'
    };

    const appended = appendActor(makeDocument(), actor);
    const duplicate = appendActor(appended, actor);
    const removed = removeActor(duplicate, actor.id);

    expect(appended.actors.map((item) => item.id)).toEqual(['actor_floor', 'actor_box']);
    expect(appended.runtime.scenes[0]?.actors).toEqual(['actor_floor', 'actor_box']);
    expect(duplicate.actors).toHaveLength(2);
    expect(removed.actors.map((item) => item.id)).toEqual(['actor_floor']);
    expect(removed.runtime.scenes[0]?.actors).toEqual(['actor_floor']);
  });

  it('renames actors without changing unrelated document sections', () => {
    const renamed = renameActor(makeDocument(), 'actor_floor', 'Ground Plane');

    expect(renamed.actors[0]?.name).toBe('Ground Plane');
    expect(renamed.identity.projectId).toBe('project-1');
    expect(renamed.runtime.entrySceneId).toBe('scene_main');
  });

  it('normalizes revision and computes deterministic document hashes', async () => {
    const document = makeDocument();
    const normalized = normalizeSceneDocument({ ...document, revision: Number.NaN });

    const firstHash = await computeSceneDocumentHash(document);
    const secondHash = await computeSceneDocumentHash(makeDocument());

    expect(normalized.revision).toBe(1);
    expect(normalized.meta.updatedAt).not.toBe(document.meta.updatedAt);
    expect(firstHash).toBe(secondHash);
    expect(firstHash).toHaveLength(64);
  });

  it('serializes document hashes with System.Text.Json compatible escaping', () => {
    const document = makeDocument();
    const withChineseActor: SceneDocument = {
      ...document,
      actors: [
        {
          ...document.actors[0],
          name: '盒体'
        } as SceneActor
      ],
      meta: {
        ...document.meta,
        title: 'A&B<'
      }
    };

    const serialized = serializeSceneDocumentForHash(withChineseActor);

    expect(serialized).toContain('"name":"\\u76D2\\u4F53"');
    expect(serialized).toContain('"title":"A\\u0026B\\u003C"');
    expect(serialized).not.toContain('盒体');
  });

  it('creates a complex exhibition hall with model scene, lights, materials, and stable actor references', () => {
    const document = applyComplexExhibitionHall(makeDocument());
    const hallScene = document.runtime.scenes.find((scene) => scene.id === 'scene_hall');
    const lightActors = document.actors.filter((actor) => actor.type === 'light');

    expect(document.runtime.entrySceneId).toBe('scene_hall');
    expect(hallScene?.type).toBe('model3d');
    expect(hallScene?.actors.length).toBeGreaterThan(10);
    expect(lightActors).toHaveLength(3);
    expect(document.geometries.some((geometry) => geometry.id === 'hall_geo_booth')).toBe(true);
    expect(document.materials.some((material) => material.id === 'hall_mat_media')).toBe(true);
    expect(hallScene?.actors.every((id) => document.actors.some((actor) => actor.id === id))).toBe(true);
  });

  it('binds a 720 panorama asset to a runtime scene and keeps repeated updates idempotent', () => {
    const first = upsertPanoramaScene(makeDocument(), { id: 'asset_panorama', kind: 'panorama', url: '/uploads/panorama.jpg' });
    const second = upsertPanoramaScene(first, { id: 'asset_panorama', kind: 'panorama', url: '/uploads/panorama.jpg' });
    const panoramaScenes = second.runtime.scenes.filter((scene) => scene.id === 'scene_panorama_foyer');

    expect(panoramaScenes).toHaveLength(1);
    expect(panoramaScenes[0]?.type).toBe('panorama720');
    expect(panoramaScenes[0]?.environment?.panoramaAssetId).toBe('asset_panorama');
    expect(second.assets.filter((asset) => asset.id === 'asset_panorama')).toHaveLength(1);
  });

  it('adds hotspots and property edits through the persisted document model', () => {
    const document = applyComplexExhibitionHall(makeDocument());
    const moved = updateActorTransform(document, 'hall_plinth_1', { x: -4, y: 0.45, z: 2.5 });
    const recolored = updateMaterialBaseColor(moved, 'hall_mat_booth', '#c084fc');
    const hotspot = addSceneHotspot(recolored, {
      id: 'hotspot_plinth_1',
      label: 'Open panorama foyer',
      position: { x: -4, y: 1.25, z: 2.5 },
      sceneId: 'scene_hall',
      target: 'scene_panorama_foyer',
      type: 'navigate'
    });

    const transform = hotspot.components.find((component) => component.id === 'hall_plinth_1_transform');
    expect(transform?.position).toEqual({ x: -4, y: 0.45, z: 2.5 });
    expect(hotspot.materials.find((material) => material.id === 'hall_mat_booth')?.baseColor).toBe('#c084fc');
    expect(hotspot.interactions.hotspots[0]?.target).toBe('scene_panorama_foyer');
  });

  it('writes PBR texture slots and UV transform into the persisted material document', () => {
    const document: SceneDocument = {
      ...makeDocument(),
      assets: [{ id: 'asset_basecolor', kind: 'texture', url: '/uploads/basecolor.png' }],
      materials: [
        {
          id: 'mat_test',
          name: 'Test Material',
          pbr: {
            baseColor: '#8a9099'
          },
          type: 'pbr'
        }
      ]
    };

    const updated = updateMaterialBaseColor(document, 'mat_test', {
      baseColor: '#112233',
      metallic: 0.8,
      roughness: 0.25,
      textureSlots: {
        baseColor: { assetId: 'asset_basecolor', url: '/uploads/basecolor.png' },
        normal: { url: '/uploads/normal.png' }
      },
      uvTransform: {
        offset: [0.1, 0.2],
        repeat: [2, 3],
        rotation: 45
      }
    });
    const removedSlot = updateMaterialBaseColor(updated, 'mat_test', {
      textureSlots: {
        normal: null
      }
    });
    const material = removedSlot.materials.find((item) => item.id === 'mat_test');

    expect(material?.baseColor).toBe('#112233');
    expect(material?.metallic).toBe(0.8);
    expect(material?.pbr?.roughness).toBe(0.25);
    expect(material?.pbr?.textureSlots?.baseColor?.assetId).toBe('asset_basecolor');
    expect(material?.pbr?.textureSlots?.normal).toBeUndefined();
    expect(material?.pbr?.uvTransform).toEqual({ offset: [0.1, 0.2], repeat: [2, 3], rotation: 45 });
  });
});
