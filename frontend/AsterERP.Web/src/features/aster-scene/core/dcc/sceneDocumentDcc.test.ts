import { describe, expect, it } from 'vitest';

import type { AsterSceneAsset, SceneDocument } from '../../model/types';

import { createMaterialFromAsset, createModifier, placeAssetActor, upsertModifier } from './sceneDocumentDcc';

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
      title: 'Factory'
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

function makeAsset(overrides: Partial<AsterSceneAsset>): AsterSceneAsset {
  return {
    assetCode: 'ASSET-001',
    assetType: 'model',
    createdTime: '2026-01-01T00:00:00.000Z',
    currentVersion: 2,
    fileName: 'product.glb',
    id: 'asset_model',
    projectId: 'project-1',
    runtimeUrl: '/uploads/product.glb',
    status: 'Ready',
    ...overrides
  };
}

describe('sceneDocumentDcc document mutations', () => {
  it('places model assets with actor/component/asset references that are internally consistent', () => {
    const result = placeAssetActor(makeDocument(), makeAsset({}));
    const actor = result.document.actors.find((item) => item.id === result.actorId);
    const renderer = result.document.components.find((component) => component.id === `${result.actorId}_meshRenderer`);
    const binding = result.document.components.find((component) => component.id === `${result.actorId}_materialBinding`);
    const slots = result.document.components.find((component) => component.id === `${result.actorId}_slots`);
    const stack = result.document.components.find((component) => component.id === `${result.actorId}_modifiers`);
    const geometry = result.document.geometries.find((item) => item.id === `${result.actorId}_geometry`);
    const material = result.document.materials.find((item) => item.id === `${result.actorId}_material`);

    expect(actor?.components).toEqual([
      `${result.actorId}_transform`,
      `${result.actorId}_meshRenderer`,
      `${result.actorId}_materialBinding`,
      `${result.actorId}_slots`,
      `${result.actorId}_modifiers`
    ]);
    expect(result.document.assets).toEqual([
      {
        id: 'asset_model',
        kind: 'model',
        metadata: undefined,
        url: '/uploads/product.glb',
        version: 2
      }
    ]);
    expect(renderer?.geometryId).toBe(geometry?.id);
    expect(renderer?.assetId).toBe('asset_model');
    expect(binding?.materialId).toBe(material?.id);
    expect(slots?.slots).toEqual([{ id: `${material?.id}_slot`, materialId: material?.id, name: 'Slot 1' }]);
    expect(stack?.modifiers).toEqual([]);
    expect(geometry?.generatedMeshRef).toEqual({ assetId: 'asset_model', version: 2 });
    expect(result.document.runtime.scenes[0]?.actors).toContain(result.actorId);
  });

  it('upserts texture material assets without duplicate asset references', () => {
    const texture = makeAsset({
      assetType: 'texture',
      fileName: 'basecolor.png',
      id: 'asset_texture',
      runtimeUrl: '/uploads/basecolor.png'
    });
    const first = createMaterialFromAsset(makeDocument(), texture, 'Base Color');
    const second = createMaterialFromAsset(first, texture, 'Base Color Copy');

    expect(second.assets.filter((asset) => asset.id === 'asset_texture')).toHaveLength(1);
    expect(second.materials[0]?.pbr?.textureSlots?.baseColor?.assetId).toBe('asset_texture');
    expect(second.materials[1]?.pbr?.textureSlots?.baseColor?.url).toBe('/uploads/basecolor.png');
  });

  it('stores modifier parameters and disables modifiers that are not supported by preview', () => {
    const unsupported = upsertModifier(makeDocument(), 'actor_floor', {
      enabled: true,
      id: 'modifier_bend_1',
      name: 'Bend',
      parameters: {
        angle: 35
      },
      type: 'bend'
    });
    const stack = unsupported.components.find((component) => component.type === 'modifierStack');
    const bend = Array.isArray(stack?.modifiers) ? stack.modifiers[0] : null;

    expect(unsupported.actors[0]?.components).toContain(stack?.id);
    expect(bend).toMatchObject({
      enabled: false,
      parameters: {
        angle: 35
      },
      previewSupported: false,
      type: 'bend'
    });

    const editPoly = createModifier('editPoly', 'Edit Poly');
    const supported = upsertModifier(unsupported, 'actor_floor', {
      ...editPoly,
      parameters: {
        selectionMode: 'edge'
      }
    });
    const supportedStack = supported.components.find((component) => component.type === 'modifierStack');
    const storedEditPoly = Array.isArray(supportedStack?.modifiers)
      ? supportedStack.modifiers.find((modifier) => modifier.type === 'editPoly')
      : null;

    expect(storedEditPoly).toMatchObject({
      enabled: true,
      parameters: {
        selectionMode: 'edge'
      },
      previewSupported: true,
      type: 'editPoly'
    });
  });
});
