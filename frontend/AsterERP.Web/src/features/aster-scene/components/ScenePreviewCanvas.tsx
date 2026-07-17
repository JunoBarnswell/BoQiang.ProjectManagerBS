import { useEffect, useMemo, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

import { useI18n } from '@/core/i18n/I18nProvider';

import { buildActorObjects, disposeObject3D, ModelAssetLoader, readVector } from '../core/dcc/viewportEngine';
import type { RuntimeAssetVariant, RuntimeManifest, SceneDocument, SceneHotspot, SceneRuntimeScene } from '../model/types';

interface ScenePreviewCanvasProps {
  document?: SceneDocument | null;
  manifest?: RuntimeManifest | null;
  onHotspotActivate?: (hotspot: SceneHotspot) => void;
  selectedActorId?: string | null;
}

interface RuntimePanoramaAsset {
  assetId: string;
  url: string;
}

export function ScenePreviewCanvas({ document, manifest, onHotspotActivate, selectedActorId }: ScenePreviewCanvasProps) {
  const { translate: t } = useI18n();
  const rootRef = useRef<HTMLDivElement | null>(null);
  const sceneDocument = useMemo(() => document ?? manifest?.document ?? null, [document, manifest]);
  const [activeSceneId, setActiveSceneId] = useState<string | null>(null);

  useEffect(() => {
    const nextEntrySceneId = sceneDocument?.runtime.entrySceneId ?? null;
    setActiveSceneId(nextEntrySceneId);
  }, [sceneDocument]);

  useEffect(() => {
    const root = rootRef.current;
    if (!root || !sceneDocument) {
      return undefined;
    }

    let loadedActorModelCount = 0;
    const activeScene = resolveActiveScene(sceneDocument, activeSceneId);
    const runtimePanoramaAssets = resolveRuntimePanoramaAssets(sceneDocument, manifest);
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(activeScene?.type === 'panorama720' ? 0x070b15 : 0xf5f7fb, 1);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    root.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(activeScene?.type === 'panorama720' ? 0x070b15 : 0xf5f7fb);
    const camera = new THREE.PerspectiveCamera(activeScene?.environment?.fov ?? 55, 1, 0.1, 1000);
    configureCamera(camera, sceneDocument, activeScene);

    const ambient = new THREE.AmbientLight(0xffffff, 0.75);
    const key = new THREE.DirectionalLight(0xffffff, 1.15);
    key.position.set(6, 8, 5);
    scene.add(ambient, key);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.enablePan = activeScene?.type !== 'panorama720';
    controls.maxPolarAngle = activeScene?.type === 'panorama720' ? Math.PI : Math.PI * 0.48;
    controls.minDistance = activeScene?.type === 'panorama720' ? 0.01 : 3;
    controls.maxDistance = activeScene?.type === 'panorama720' ? 0.01 : 24;
    controls.target.copy(readVector(sceneDocument.runtime.camera.target, { x: 0, y: 1, z: 0 }));

    const modelAssetLoader = new ModelAssetLoader({
      document: sceneDocument,
      manifest,
      onLoaded: () => {
        loadedActorModelCount += 1;
      }
    });
    const renderables = new THREE.Group();
    const asyncDisposers: Array<() => void> = [];

    if (activeScene?.type === 'panorama720') {
      asyncDisposers.push(renderPanoramaScene(scene, activeScene, runtimePanoramaAssets));
    } else {
      const grid = new THREE.GridHelper(14, 28, 0x7b8794, 0xd3d9e3);
      scene.add(grid);
      const actorBuild = buildActorObjects(sceneDocument, activeScene, selectedActorId ?? null, { mode: 'player' });
      actorBuild.objects.forEach((object) => renderables.add(object));
      scene.add(renderables);
      modelAssetLoader.loadActorModels(actorBuild.modelRequests);
    }

    const hotspotSprites = renderHotspots(scene, sceneDocument, activeScene);
    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    const handlePointerDown = (event: PointerEvent) => {
      const rect = renderer.domElement.getBoundingClientRect();
      pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
      pointer.y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
      raycaster.setFromCamera(pointer, camera);
      const hit = raycaster.intersectObjects(hotspotSprites, true)[0];
      const hotspot = hit?.object.userData.hotspot as SceneHotspot | undefined;
      if (!hotspot) {
        return;
      }

      if ((hotspot.type ?? 'navigate') === 'navigate' && hotspot.target) {
        setActiveSceneId(hotspot.target);
      }
      onHotspotActivate?.(hotspot);
    };
    renderer.domElement.addEventListener('pointerdown', handlePointerDown);

    let frameId = 0;
    const resize = () => {
      const rect = root.getBoundingClientRect();
      const width = Math.max(320, rect.width);
      const height = Math.max(260, rect.height);
      renderer.setSize(width, height, false);
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
    };
    const render = () => {
      if (loadedActorModelCount === 0 && activeScene?.type !== 'panorama720') {
        renderables.children.forEach((object) => {
          if (object.userData.animate === 'idle-rotate') {
            object.rotation.y += 0.002;
          }
        });
      }
      controls.update();
      renderer.render(scene, camera);
      frameId = window.requestAnimationFrame(render);
    };

    resize();
    render();
    window.addEventListener('resize', resize);

    return () => {
      window.cancelAnimationFrame(frameId);
      window.removeEventListener('resize', resize);
      renderer.domElement.removeEventListener('pointerdown', handlePointerDown);
      asyncDisposers.forEach((dispose) => dispose());
      modelAssetLoader.dispose();
      controls.dispose();
      disposeObject3D(scene);
      renderer.dispose();
      renderer.domElement.remove();
    };
  }, [activeSceneId, manifest, onHotspotActivate, sceneDocument, selectedActorId]);

  return (
    <div className="as-canvas-shell">
      <div ref={rootRef} className="as-canvas" />
      <div className="as-canvas__badge">{resolveSceneLabel(sceneDocument, activeSceneId, t)}</div>
    </div>
  );
}

function resolveRuntimePanoramaAssets(document: SceneDocument, manifest?: RuntimeManifest | null): RuntimePanoramaAsset[] {
  const variantsByAssetId = manifest?.assetVariants ?? {};
  return document.assets
    .filter((asset) => asset.kind === 'panorama' || hasPanoramaExtension(asset.url))
    .map((asset) => {
      const variantUrl = resolvePanoramaVariantUrl(variantsByAssetId[asset.id]);
      return {
        assetId: asset.id,
        url: variantUrl ?? asset.url ?? ''
      };
    })
    .filter((asset): asset is RuntimePanoramaAsset => Boolean(asset.url) && hasPanoramaExtension(asset.url));
}

function resolvePanoramaVariantUrl(variants?: RuntimeAssetVariant[]): string | null {
  if (!variants || variants.length === 0) {
    return null;
  }

  const runtimeVariant = variants.find((variant) => hasPanoramaExtension(variant.runtimeUrl ?? variant.sourceUrl));
  return runtimeVariant?.runtimeUrl ?? runtimeVariant?.sourceUrl ?? null;
}

function hasPanoramaExtension(url?: string | null): boolean {
  if (!url) {
    return false;
  }

  const path = url.split('?')[0]?.toLowerCase() ?? '';
  return path.endsWith('.jpg') || path.endsWith('.jpeg') || path.endsWith('.png') || path.endsWith('.webp');
}

function resolveActiveScene(document: SceneDocument, activeSceneId: string | null): SceneRuntimeScene | null {
  return (
    document.runtime.scenes.find((scene) => scene.id === activeSceneId) ??
    document.runtime.scenes.find((scene) => scene.id === document.runtime.entrySceneId) ??
    document.runtime.scenes[0] ??
    null
  );
}

function configureCamera(camera: THREE.PerspectiveCamera, document: SceneDocument, activeScene: SceneRuntimeScene | null): void {
  if (activeScene?.type === 'panorama720') {
    const yaw = degreesToRadians(activeScene.environment?.initialYaw ?? 0);
    const pitch = degreesToRadians(activeScene.environment?.initialPitch ?? 0);
    camera.position.set(0, 0, 0.01);
    camera.lookAt(sphericalToVector(yaw, pitch, 1));
    return;
  }

  const position = readVector(document.runtime.camera.position, { x: 5.5, y: 3.4, z: 7.2 });
  camera.position.copy(position);
  camera.lookAt(readVector(document.runtime.camera.target, { x: 0, y: 1, z: 0 }));
}

function renderPanoramaScene(scene: THREE.Scene, activeScene: SceneRuntimeScene, panoramaAssets: RuntimePanoramaAsset[]): () => void {
  let disposed = false;
  const panoramaAssetId = activeScene.environment?.panoramaAssetId;
  const panorama = panoramaAssets.find((asset) => asset.assetId === panoramaAssetId) ?? panoramaAssets[0];
  const geometry = new THREE.SphereGeometry(60, 64, 32);
  geometry.scale(-1, 1, 1);
  const material = new THREE.MeshBasicMaterial({ color: 0x1e293b });
  const sphere = new THREE.Mesh(geometry, material);
  scene.add(sphere);

  if (!panorama?.url) {
    return () => {
      disposed = true;
    };
  }

  new THREE.TextureLoader().load(
    panorama.url,
    (texture) => {
      if (disposed) {
        texture.dispose();
        return;
      }

      texture.colorSpace = THREE.SRGBColorSpace;
      material.map = texture;
      material.color.setHex(0xffffff);
      material.needsUpdate = true;
    },
    undefined,
    () => {
      material.color.setHex(0x1e293b);
    }
  );

  return () => {
    disposed = true;
  };
}

function renderHotspots(scene: THREE.Scene, document: SceneDocument, activeScene: SceneRuntimeScene | null): THREE.Sprite[] {
  if (!activeScene) {
    return [];
  }

  return document.interactions.hotspots
    .filter((hotspot) => hotspot.enabled !== false && (!hotspot.sceneId || hotspot.sceneId === activeScene.id))
    .map((hotspot) => {
      const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: createHotspotTexture(hotspot.label), transparent: true }));
      sprite.name = hotspot.label;
      sprite.userData.hotspot = hotspot;
      sprite.scale.set(0.72, 0.32, 1);
      if (activeScene.type === 'panorama720' && hotspot.spherical) {
        sprite.position.copy(sphericalToVector(degreesToRadians(hotspot.spherical.yaw), degreesToRadians(hotspot.spherical.pitch), 18));
      } else {
        sprite.position.copy(readVector(hotspot.position, { x: 0, y: 1.6, z: 0 }));
      }
      scene.add(sprite);
      return sprite;
    });
}

function createHotspotTexture(label: string): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 96;
  const context = canvas.getContext('2d');
  if (context) {
    context.fillStyle = 'rgba(15, 118, 110, 0.95)';
    context.strokeStyle = '#ffffff';
    context.lineWidth = 4;
    roundRect(context, 8, 18, 240, 54, 16);
    context.fill();
    context.stroke();
    context.fillStyle = '#ffffff';
    context.font = '600 22px system-ui, sans-serif';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillText(label.slice(0, 18), 128, 45, 210);
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

function roundRect(context: CanvasRenderingContext2D, x: number, y: number, width: number, height: number, radius: number): void {
  context.beginPath();
  context.moveTo(x + radius, y);
  context.lineTo(x + width - radius, y);
  context.quadraticCurveTo(x + width, y, x + width, y + radius);
  context.lineTo(x + width, y + height - radius);
  context.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
  context.lineTo(x + radius, y + height);
  context.quadraticCurveTo(x, y + height, x, y + height - radius);
  context.lineTo(x, y + radius);
  context.quadraticCurveTo(x, y, x + radius, y);
  context.closePath();
}

function degreesToRadians(value: number): number {
  return (value * Math.PI) / 180;
}

function sphericalToVector(yaw: number, pitch: number, radius: number): THREE.Vector3 {
  return new THREE.Vector3(Math.sin(yaw) * Math.cos(pitch) * radius, Math.sin(pitch) * radius, -Math.cos(yaw) * Math.cos(pitch) * radius);
}

function resolveSceneLabel(document: SceneDocument | null, activeSceneId: string | null, t: (key: string) => string): string {
  if (!document) {
    return t('asterscene.canvas.noScene');
  }

  const scene = resolveActiveScene(document, activeSceneId);
  return scene ? `${scene.name} · ${scene.type ?? 'model3d'}` : t('asterscene.canvas.sceneUnavailable');
}
