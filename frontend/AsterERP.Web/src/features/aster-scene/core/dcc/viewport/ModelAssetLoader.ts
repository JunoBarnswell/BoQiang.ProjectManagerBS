import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

import type { RuntimeAssetVariant, RuntimeManifest, SceneAssetRef, SceneDocument } from '../../../model/types';

import { applyLoadedModelModifierPreview, type ActorModelRequest } from './ActorObjectFactory';
import { disposeObject3D } from './threeObjectDisposal';

interface ModelAssetLoaderOptions {
  document: SceneDocument;
  manifest?: RuntimeManifest | null;
  onFailed?: (request: ActorModelRequest, error: unknown) => void;
  onLoaded?: (request: ActorModelRequest) => void;
}

interface MountedModel {
  model: THREE.Object3D;
  request: ActorModelRequest;
  selectionHelper: THREE.Object3D | null;
}

export class ModelAssetLoader {
  private disposed = false;
  private readonly loader = new GLTFLoader();
  private readonly mountedModels: MountedModel[] = [];

  public constructor(private readonly options: ModelAssetLoaderOptions) {}

  public loadActorModels(requests: ActorModelRequest[]): void {
    requests.forEach((request) => this.loadActorModel(request));
  }

  public dispose(): void {
    this.disposed = true;
    this.mountedModels.splice(0).forEach((mounted) => {
      mounted.request.root.remove(mounted.model);
      if (mounted.selectionHelper) {
        mounted.request.root.remove(mounted.selectionHelper);
      }
      disposeObject3D(mounted.model);
      if (mounted.selectionHelper) {
        disposeObject3D(mounted.selectionHelper);
      }
    });
  }

  private loadActorModel(request: ActorModelRequest): void {
    const url = resolveModelAssetUrl(request, this.options.document, this.options.manifest);
    if (!url) {
      request.root.userData.modelLoadState = 'missing-url';
      return;
    }

    request.root.userData.modelLoadState = 'loading';
    this.loader.load(
      url,
      (gltf) => {
        if (this.disposed) {
          disposeObject3D(gltf.scene);
          return;
        }

        this.mountLoadedModel(request, gltf.scene);
        request.root.userData.modelLoadState = 'loaded';
        this.options.onLoaded?.(request);
      },
      undefined,
      (error) => {
        if (this.disposed) {
          return;
        }

        request.root.userData.modelLoadState = 'failed';
        this.options.onFailed?.(request, error);
      }
    );
  }

  private mountLoadedModel(request: ActorModelRequest, model: THREE.Object3D): void {
    model.name = `${request.actorName} Model`;
    prepareLoadedModel(model, request);
    removePlaceholder(request.root);

    const selectionHelper = request.selected ? createSelectionBounds(model) : null;
    request.root.add(model);
    if (selectionHelper) {
      request.root.add(selectionHelper);
    }

    this.mountedModels.push({ model, request, selectionHelper });
  }
}

export function resolveModelAssetUrl(request: ActorModelRequest, document: SceneDocument, manifest?: RuntimeManifest | null): string | null {
  if (request.assetId) {
    const manifestUrl = resolveManifestModelUrl(manifest?.assetVariants[request.assetId]);
    if (manifestUrl) {
      return manifestUrl;
    }
  }

  if (request.sourceUrl) {
    return request.sourceUrl;
  }

  if (request.assetId) {
    const assetRef = document.assets.find((asset) => asset.id === request.assetId);
    const assetUrl = resolveDocumentAssetUrl(assetRef);
    if (assetUrl) {
      return assetUrl;
    }
  }

  return null;
}

export function hasModelExtension(url?: string | null): boolean {
  if (!url) {
    return false;
  }

  const path = url.split('?')[0]?.toLowerCase() ?? '';
  return path.endsWith('.glb') || path.endsWith('.gltf');
}

function resolveManifestModelUrl(variants?: RuntimeAssetVariant[]): string | null {
  if (!variants || variants.length === 0) {
    return null;
  }

  const modelVariant = variants.find((variant) => hasModelExtension(variant.runtimeUrl ?? variant.sourceUrl));
  const fallbackVariant = variants.find((variant) => Boolean(variant.runtimeUrl ?? variant.sourceUrl));
  return modelVariant?.runtimeUrl ?? modelVariant?.sourceUrl ?? fallbackVariant?.runtimeUrl ?? fallbackVariant?.sourceUrl ?? null;
}

function resolveDocumentAssetUrl(asset?: SceneAssetRef): string | null {
  if (!asset?.url) {
    return null;
  }

  return asset.kind === 'model' || asset.kind === 'mesh' || hasModelExtension(asset.url) ? asset.url : null;
}

function prepareLoadedModel(model: THREE.Object3D, request: ActorModelRequest): void {
  applyLoadedModelModifierPreview(model, request.modifiers);
  model.traverse((object) => {
    if (!(object instanceof THREE.Mesh)) {
      return;
    }

    object.castShadow = request.castShadow;
    object.receiveShadow = request.receiveShadow;
    object.userData.selectable = false;
    computeBoundsTreeIfAvailable(object.geometry);
    applyWireframe(object.material, request.showWire);
  });
}

function removePlaceholder(root: THREE.Object3D): void {
  const placeholders = root.children.filter((child) => child.userData.modelPlaceholder === true);
  placeholders.forEach((placeholder) => {
    root.remove(placeholder);
    disposeObject3D(placeholder);
  });
}

function createSelectionBounds(model: THREE.Object3D): THREE.Object3D | null {
  model.updateMatrixWorld(true);
  const bounds = new THREE.Box3().setFromObject(model);
  if (bounds.isEmpty()) {
    return null;
  }

  const size = bounds.getSize(new THREE.Vector3());
  if (!Number.isFinite(size.x) || !Number.isFinite(size.y) || !Number.isFinite(size.z) || Math.max(size.x, size.y, size.z) <= 0) {
    return null;
  }

  const center = bounds.getCenter(new THREE.Vector3());
  const geometry = new THREE.EdgesGeometry(new THREE.BoxGeometry(size.x, size.y, size.z));
  const material = new THREE.LineBasicMaterial({ color: 0x2563eb });
  const helper = new THREE.LineSegments(geometry, material);
  helper.name = `${model.name} Selection`;
  helper.position.copy(center);
  helper.userData.selectable = false;
  return helper;
}

function computeBoundsTreeIfAvailable(geometry: THREE.BufferGeometry): void {
  const maybeBvhGeometry = geometry as THREE.BufferGeometry & { computeBoundsTree?: () => void };
  maybeBvhGeometry.computeBoundsTree?.();
}

function applyWireframe(material: THREE.Material | THREE.Material[], wireframe: boolean): void {
  if (!wireframe) {
    return;
  }

  const materials = Array.isArray(material) ? material : [material];
  materials.forEach((item) => {
    if ('wireframe' in item) {
      (item as THREE.Material & { wireframe: boolean }).wireframe = true;
      item.needsUpdate = true;
    }
  });
}
