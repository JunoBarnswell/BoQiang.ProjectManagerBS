import * as THREE from 'three';
import { acceleratedRaycast, computeBoundsTree, disposeBoundsTree } from 'three-mesh-bvh';

import type { SceneDocument, SceneRuntimeScene } from '../../model/types';

import { ActorObjectFactory, type ActorObjectBuildResult, type ViewportRenderMode } from './viewport/ActorObjectFactory';

export { ModelAssetLoader, hasModelExtension, resolveModelAssetUrl } from './viewport/ModelAssetLoader';
export { createGeometry, createHelperObject, createMaterial, type ActorModelRequest, type ActorObjectBuildResult } from './viewport/ActorObjectFactory';
export { disposeMaterial, disposeObject3D, disposeObjectMaterial } from './viewport/threeObjectDisposal';
export { readColor, readNumber, readString, readVector } from './viewport/viewportValues';

let bvhInstalled = false;

export function installBvhRaycasting(): void {
  if (bvhInstalled) {
    return;
  }

  bvhInstalled = true;
  (THREE.BufferGeometry.prototype as unknown as { computeBoundsTree: typeof computeBoundsTree }).computeBoundsTree = computeBoundsTree;
  (THREE.BufferGeometry.prototype as unknown as { disposeBoundsTree: typeof disposeBoundsTree }).disposeBoundsTree = disposeBoundsTree;
  THREE.Mesh.prototype.raycast = acceleratedRaycast;
}

export function buildActorObjects(
  document: SceneDocument,
  activeScene: SceneRuntimeScene | null,
  selectedActorId: string | null,
  options: { mode?: ViewportRenderMode } = {}
): ActorObjectBuildResult {
  return new ActorObjectFactory({
    activeScene,
    document,
    mode: options.mode ?? 'studio',
    selectedActorId
  }).build();
}
