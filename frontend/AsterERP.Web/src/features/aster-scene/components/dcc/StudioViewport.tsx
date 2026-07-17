import { useEffect, useMemo, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { TransformControls } from 'three/examples/jsm/controls/TransformControls.js';

import type { PrimitiveDefinition, PrimitivePlacement } from '../../core/dcc/sceneDocumentDcc';
import { buildActorObjects, disposeObject3D, installBvhRaycasting, ModelAssetLoader, readVector } from '../../core/dcc/viewportEngine';
import type { SceneDocument, SceneVector3 } from '../../model/types';
import { useAsterSceneEditorStore } from '../../state/editorStore';

interface StudioViewportProps {
  activePrimitive: PrimitiveDefinition | null;
  document: SceneDocument;
  onActorSelect: (actorId: string | null) => void;
  onPlacementCancel: () => void;
  onPrimitivePlace: (definition: PrimitiveDefinition, placement: PrimitivePlacement) => void;
  onTransformCommit: (actorId: string, transform: { position: SceneVector3; rotation: SceneVector3; scale: SceneVector3 }) => void;
  selectedActorId: string | null;
  t: (key: string) => string;
}

interface ViewportPane {
  cameraMode: 'front' | 'perspective' | 'top' | 'left';
  labelKey: string;
}

interface PrimitivePlacementDraft {
  current: THREE.Vector3;
  object: THREE.Object3D;
  pointerId: number;
  start: THREE.Vector3;
}

const viewportPanes: ViewportPane[] = [
  { cameraMode: 'perspective', labelKey: 'asterscene.dcc.viewport.perspective' },
  { cameraMode: 'top', labelKey: 'asterscene.dcc.viewport.top' },
  { cameraMode: 'front', labelKey: 'asterscene.dcc.viewport.front' },
  { cameraMode: 'left', labelKey: 'asterscene.dcc.viewport.left' }
];

export function StudioViewport({ activePrimitive, document, onActorSelect, onPlacementCancel, onPrimitivePlace, onTransformCommit, selectedActorId, t }: StudioViewportProps) {
  const rootRef = useRef<HTMLDivElement | null>(null);
  const { setSnapEnabled, setTransformMode, setTransformSpace, snapEnabled, transformMode, transformSpace, viewportLayout } = useAsterSceneEditorStore();
  const panes = useMemo(() => (viewportLayout === 'quad' ? viewportPanes : [viewportPanes[0]]), [viewportLayout]);

  useEffect(() => {
    installBvhRaycasting();
  }, []);

  useEffect(() => {
    const root = rootRef.current;
    if (!root) {
      return undefined;
    }

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setClearColor(0x202733, 1);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    root.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x202733);
    const grid = new THREE.GridHelper(40, 40, 0x596579, 0x354052);
    scene.add(grid);
    scene.add(new THREE.AmbientLight(0xffffff, 0.72));
    const key = new THREE.DirectionalLight(0xffffff, 1.2);
    key.position.set(6, 9, 7);
    scene.add(key);

    const activeScene = document.runtime.scenes.find((sceneItem) => sceneItem.id === document.runtime.entrySceneId) ?? document.runtime.scenes[0] ?? null;
    const actorBuild = buildActorObjects(document, activeScene, selectedActorId, { mode: 'studio' });
    const actorObjects = actorBuild.objects;
    actorObjects.forEach((object) => scene.add(object));
    const modelAssetLoader = new ModelAssetLoader({ document });
    modelAssetLoader.loadActorModels(actorBuild.modelRequests);
    const selectableObjects = actorObjects.filter((object) => object.userData.selectable !== false);

    const cameras = panes.map((pane) => {
      const paneCamera = new THREE.PerspectiveCamera(55, 1, 0.1, 1000);
      configureCamera(paneCamera, pane.cameraMode, document);
      return paneCamera;
    });
    const activeCamera = cameras[0];
    const orbit = new OrbitControls(activeCamera, renderer.domElement);
    orbit.enableDamping = true;
    orbit.dampingFactor = 0.08;
    orbit.target.copy(readVector(document.runtime.camera.target, { x: 0, y: 0.8, z: 0 }));

    const transform = new TransformControls(activeCamera, renderer.domElement);
    transform.setMode(transformMode);
    transform.setSpace(transformSpace === 'view' ? 'world' : transformSpace);
    transform.setTranslationSnap(snapEnabled ? 0.25 : null);
    transform.setRotationSnap(snapEnabled ? THREE.MathUtils.degToRad(5) : null);
    transform.setScaleSnap(snapEnabled ? 0.1 : null);
    scene.add(transform.getHelper());

    const selectedObject = actorObjects.find((object) => object.userData.actorId === selectedActorId && object.userData.selectable !== false) ?? null;
    if (selectedObject) {
      transform.attach(selectedObject);
    }

    transform.addEventListener('dragging-changed', (event) => {
      orbit.enabled = !event.value;
      if (!event.value && selectedObject?.userData.actorId) {
        onTransformCommit(selectedObject.userData.actorId as string, {
          position: vectorToRecord(selectedObject.position),
          rotation: eulerToRecord(selectedObject.rotation),
          scale: vectorToRecord(selectedObject.scale)
        });
      }
    });

    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    const groundPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    let placementDraft: PrimitivePlacementDraft | null = null;

    const readGroundPoint = (event: PointerEvent): THREE.Vector3 | null => {
      const rect = renderer.domElement.getBoundingClientRect();
      const paneIndex = resolvePaneIndex(event, rect, panes.length);
      const paneBounds = resolvePaneBounds(rect, paneIndex, panes.length);
      pointer.x = ((event.clientX - paneBounds.left) / paneBounds.width) * 2 - 1;
      pointer.y = -(((event.clientY - paneBounds.top) / paneBounds.height) * 2 - 1);
      raycaster.setFromCamera(pointer, cameras[paneIndex] ?? activeCamera);
      const point = new THREE.Vector3();
      return raycaster.ray.intersectPlane(groundPlane, point) ? snapVector(point, snapEnabled) : null;
    };

    const onPointerDown = (event: PointerEvent) => {
      if (transform.dragging) {
        return;
      }

      if (event.button === 0 && activePrimitive) {
        const start = readGroundPoint(event);
        if (!start) {
          return;
        }

        orbit.enabled = false;
        event.preventDefault();
        event.stopPropagation();
        renderer.domElement.setPointerCapture(event.pointerId);
        const placement = derivePrimitivePlacement(activePrimitive, start, start, event.shiftKey);
        const preview = createPlacementPreview(activePrimitive, placement);
        scene.add(preview);
        placementDraft = {
          current: start.clone(),
          object: preview,
          pointerId: event.pointerId,
          start
        };
        return;
      }

      const rect = renderer.domElement.getBoundingClientRect();
      const paneIndex = resolvePaneIndex(event, rect, panes.length);
      const paneBounds = resolvePaneBounds(rect, paneIndex, panes.length);
      pointer.x = ((event.clientX - paneBounds.left) / paneBounds.width) * 2 - 1;
      pointer.y = -(((event.clientY - paneBounds.top) / paneBounds.height) * 2 - 1);
      raycaster.setFromCamera(pointer, cameras[paneIndex] ?? activeCamera);
      const hit = raycaster.intersectObjects(selectableObjects, true).find((intersection) => findActorId(intersection.object));
      onActorSelect(hit ? findActorId(hit.object) : null);
    };

    const onPointerMove = (event: PointerEvent) => {
      if (!activePrimitive || !placementDraft) {
        return;
      }

      const current = readGroundPoint(event);
      if (!current) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
      placementDraft.current = current;
      updatePlacementPreview(
        placementDraft.object,
        activePrimitive,
        derivePrimitivePlacement(activePrimitive, placementDraft.start, current, event.shiftKey)
      );
    };

    const onPointerUp = (event: PointerEvent) => {
      if (!activePrimitive || !placementDraft || placementDraft.pointerId !== event.pointerId) {
        return;
      }

      const current = readGroundPoint(event) ?? placementDraft.current;
      const placement = derivePrimitivePlacement(activePrimitive, placementDraft.start, current, event.shiftKey);
      scene.remove(placementDraft.object);
      disposeObject3D(placementDraft.object);
      renderer.domElement.releasePointerCapture(event.pointerId);
      placementDraft = null;
      orbit.enabled = true;
      event.preventDefault();
      event.stopPropagation();
      onPrimitivePlace(activePrimitive, placement);
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.target instanceof HTMLInputElement || event.target instanceof HTMLSelectElement || event.target instanceof HTMLTextAreaElement) {
        return;
      }

      if (event.key.toLowerCase() === 'w') {
        setTransformMode('translate');
      } else if (event.key.toLowerCase() === 'e') {
        setTransformMode('rotate');
      } else if (event.key.toLowerCase() === 'r') {
        setTransformMode('scale');
      } else if (event.key.toLowerCase() === 'q') {
        setTransformSpace(transformSpace === 'local' ? 'world' : 'local');
      } else if (event.key === 'Shift') {
        setSnapEnabled(true);
      } else if (event.key === 'Escape') {
        if (placementDraft) {
          scene.remove(placementDraft.object);
          disposeObject3D(placementDraft.object);
          placementDraft = null;
        }
        transform.detach();
        onPlacementCancel();
        onActorSelect(null);
      }
    };
    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key === 'Shift') {
        setSnapEnabled(false);
      }
    };

    renderer.domElement.addEventListener('pointerdown', onPointerDown);
    renderer.domElement.addEventListener('pointermove', onPointerMove);
    renderer.domElement.addEventListener('pointerup', onPointerUp);
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);

    let frameId = 0;
    const resize = () => {
      const rect = root.getBoundingClientRect();
      renderer.setSize(Math.max(320, rect.width), Math.max(260, rect.height), false);
      updateCameraAspects(cameras, rect, panes.length);
    };
    const render = () => {
      transform.setMode(transformMode);
      transform.setSpace(transformSpace === 'view' ? 'world' : transformSpace);
      transform.setTranslationSnap(snapEnabled ? 0.25 : null);
      transform.setRotationSnap(snapEnabled ? THREE.MathUtils.degToRad(5) : null);
      transform.setScaleSnap(snapEnabled ? 0.1 : null);
      orbit.update();
      renderPanes(renderer, scene, cameras, panes.length, root.getBoundingClientRect());
      frameId = requestAnimationFrame(render);
    };

    resize();
    render();
    window.addEventListener('resize', resize);

    return () => {
      cancelAnimationFrame(frameId);
      window.removeEventListener('resize', resize);
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
      renderer.domElement.removeEventListener('pointerdown', onPointerDown);
      renderer.domElement.removeEventListener('pointermove', onPointerMove);
      renderer.domElement.removeEventListener('pointerup', onPointerUp);
      modelAssetLoader.dispose();
      transform.dispose();
      orbit.dispose();
      disposeObject3D(scene);
      renderer.dispose();
      renderer.domElement.remove();
    };
  }, [activePrimitive, document, onActorSelect, onPlacementCancel, onPrimitivePlace, onTransformCommit, panes, selectedActorId, setSnapEnabled, setTransformMode, setTransformSpace, snapEnabled, transformMode, transformSpace]);

  return (
    <div className={`as-dcc-viewport as-dcc-viewport--${panes.length === 4 ? 'quad' : 'single'}${activePrimitive ? ' as-dcc-viewport--placing' : ''}`} ref={rootRef}>
      <div className="as-dcc-viewport__label">
        {panes.length === 4 ? t('asterscene.dcc.viewport.quad') : t(panes[0].labelKey)} · {t(`asterscene.dcc.transform.${transformMode}`)} · {t(`asterscene.dcc.space.${transformSpace}`)}
      </div>
      {activePrimitive ? (
        <div className="as-dcc-viewport__placement">
          <strong>{t(activePrimitive.labelKey)}</strong>
          <span>{t('asterscene.dcc.create.dragHint')}</span>
        </div>
      ) : null}
      {panes.length === 4 ? (
        <div className="as-dcc-viewport__pane-labels">
          {panes.map((pane) => (
            <span key={pane.cameraMode}>{t(pane.labelKey)}</span>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function configureCamera(camera: THREE.PerspectiveCamera, mode: ViewportPane['cameraMode'], document: SceneDocument): void {
  if (mode === 'top') {
    camera.position.set(0, 18, 0.001);
    camera.lookAt(0, 0, 0);
    return;
  }

  if (mode === 'front') {
    camera.position.set(0, 3, 16);
    camera.lookAt(0, 1, 0);
    return;
  }

  if (mode === 'left') {
    camera.position.set(-16, 3, 0);
    camera.lookAt(0, 1, 0);
    return;
  }

  camera.position.copy(readVector(document.runtime.camera.position, { x: 6, y: 4, z: 7 }));
  camera.lookAt(readVector(document.runtime.camera.target, { x: 0, y: 1, z: 0 }));
}

function renderPanes(renderer: THREE.WebGLRenderer, scene: THREE.Scene, cameras: THREE.PerspectiveCamera[], paneCount: number, rect: DOMRect): void {
  if (paneCount !== 4) {
    renderer.setScissorTest(false);
    renderer.render(scene, cameras[0]);
    return;
  }

  const width = Math.max(320, rect.width);
  const height = Math.max(260, rect.height);
  const halfWidth = Math.floor(width / 2);
  const halfHeight = Math.floor(height / 2);
  const panes = [
    { x: 0, y: halfHeight, width: halfWidth, height: height - halfHeight },
    { x: halfWidth, y: halfHeight, width: width - halfWidth, height: height - halfHeight },
    { x: 0, y: 0, width: halfWidth, height: halfHeight },
    { x: halfWidth, y: 0, width: width - halfWidth, height: halfHeight }
  ];

  renderer.setScissorTest(true);
  panes.forEach((pane, index) => {
    renderer.setViewport(pane.x, pane.y, pane.width, pane.height);
    renderer.setScissor(pane.x, pane.y, pane.width, pane.height);
    renderer.render(scene, cameras[index] ?? cameras[0]);
  });
  renderer.setScissorTest(false);
}

function resolvePaneIndex(event: PointerEvent, rect: DOMRect, paneCount: number): number {
  if (paneCount !== 4) {
    return 0;
  }

  const localX = event.clientX - rect.left;
  const localY = event.clientY - rect.top;
  const right = localX >= rect.width / 2;
  const bottom = localY >= rect.height / 2;
  if (!right && !bottom) {
    return 0;
  }
  if (right && !bottom) {
    return 1;
  }
  if (!right && bottom) {
    return 2;
  }

  return 3;
}

function resolvePaneBounds(rect: DOMRect, paneIndex: number, paneCount: number): Pick<DOMRect, 'height' | 'left' | 'top' | 'width'> {
  if (paneCount !== 4) {
    return rect;
  }

  const halfWidth = rect.width / 2;
  const halfHeight = rect.height / 2;
  const right = paneIndex === 1 || paneIndex === 3;
  const bottom = paneIndex === 2 || paneIndex === 3;
  return {
    height: bottom ? rect.height - halfHeight : halfHeight,
    left: rect.left + (right ? halfWidth : 0),
    top: rect.top + (bottom ? halfHeight : 0),
    width: right ? rect.width - halfWidth : halfWidth
  };
}

function updateCameraAspects(cameras: THREE.PerspectiveCamera[], rect: DOMRect, paneCount: number): void {
  const width = Math.max(1, rect.width);
  const height = Math.max(1, rect.height);
  const aspect = paneCount === 4 ? Math.max(1, width / 2) / Math.max(1, height / 2) : width / height;
  cameras.forEach((camera) => {
    camera.aspect = aspect;
    camera.updateProjectionMatrix();
  });
}

function createPlacementPreview(definition: PrimitiveDefinition, placement: PrimitivePlacement): THREE.Object3D {
  const mesh = new THREE.Mesh(
    createPreviewGeometry(definition, placement.parameters ?? definition.parameters),
    new THREE.MeshStandardMaterial({
      color: 0x38bdf8,
      depthWrite: false,
      emissive: 0x06323d,
      opacity: 0.38,
      transparent: true,
      wireframe: false
    })
  );
  mesh.userData.selectable = false;
  mesh.position.set(placement.position.x, placement.position.y, placement.position.z);
  return mesh;
}

function updatePlacementPreview(object: THREE.Object3D, definition: PrimitiveDefinition, placement: PrimitivePlacement): void {
  object.position.set(placement.position.x, placement.position.y, placement.position.z);
  if (object instanceof THREE.Mesh) {
    object.geometry.dispose();
    object.geometry = createPreviewGeometry(definition, placement.parameters ?? definition.parameters);
  }
}

function createPreviewGeometry(definition: PrimitiveDefinition, parameters: Record<string, unknown>): THREE.BufferGeometry {
  const type = definition.geometryType ?? definition.code;
  switch (type) {
    case 'sphere':
      return new THREE.SphereGeometry(readNumber(parameters.radius, 0.55), readNumber(parameters.segments, 32), 16);
    case 'cylinder':
      return new THREE.CylinderGeometry(readNumber(parameters.radiusTop, readNumber(parameters.radius, 0.5)), readNumber(parameters.radiusBottom, readNumber(parameters.radius, 0.5)), readNumber(parameters.height, 1), 32);
    case 'cone':
      return new THREE.ConeGeometry(readNumber(parameters.radius, 0.5), readNumber(parameters.height, 1), 32);
    case 'torus':
      return new THREE.TorusGeometry(readNumber(parameters.radius, 0.5), readNumber(parameters.tube, 0.12), 16, 48);
    case 'tube':
      return new THREE.CylinderGeometry(readNumber(parameters.radius, 0.16), readNumber(parameters.radius, 0.16), readNumber(parameters.height, 1), 24);
    case 'plane':
      return new THREE.BoxGeometry(readNumber(parameters.width, 1), 0.03, readNumber(parameters.depth, 1));
    default:
      return new THREE.BoxGeometry(readNumber(parameters.width, 1), readNumber(parameters.height, 1), readNumber(parameters.depth, 1));
  }
}

function derivePrimitivePlacement(definition: PrimitiveDefinition, start: THREE.Vector3, current: THREE.Vector3, constrainSquare: boolean): PrimitivePlacement {
  const defaultHeight = readNumber(definition.parameters.height, 1);
  const deltaX = current.x - start.x;
  const deltaZ = current.z - start.z;
  const distance = Math.sqrt(deltaX * deltaX + deltaZ * deltaZ);
  const defaultWidth = readNumber(definition.parameters.width, readNumber(definition.parameters.radius, 0.55) * 2);
  const defaultDepth = readNumber(definition.parameters.depth, defaultWidth);
  const smallDrag = distance < 0.08;

  if (definition.code === 'sphere') {
    const radius = Math.max(0.18, smallDrag ? readNumber(definition.parameters.radius, 0.55) : distance);
    return {
      parameters: {
        ...definition.parameters,
        radius
      },
      position: { x: start.x, y: radius, z: start.z }
    };
  }

  if (definition.code === 'cylinder' || definition.code === 'cone' || definition.code === 'torus' || definition.code === 'tube') {
    const radius = Math.max(0.16, smallDrag ? readNumber(definition.parameters.radius, 0.5) : distance);
    const height = definition.code === 'torus' ? 0.08 : defaultHeight;
    return {
      parameters: {
        ...definition.parameters,
        radius,
        radiusBottom: definition.code === 'cylinder' ? radius : definition.parameters.radiusBottom,
        radiusTop: definition.code === 'cylinder' ? radius : definition.parameters.radiusTop
      },
      position: { x: start.x, y: Math.max(0.02, height / 2), z: start.z }
    };
  }

  let width = Math.max(0.18, smallDrag ? defaultWidth : Math.abs(deltaX));
  let depth = Math.max(0.18, smallDrag ? defaultDepth : Math.abs(deltaZ));
  if (constrainSquare) {
    const size = Math.max(width, depth);
    width = size;
    depth = size;
  }

  const height = definition.geometryType === 'plane' ? 0.03 : defaultHeight;
  const centerX = smallDrag ? start.x : start.x + Math.sign(deltaX || 1) * width / 2;
  const centerZ = smallDrag ? start.z : start.z + Math.sign(deltaZ || 1) * depth / 2;
  return {
    parameters: {
      ...definition.parameters,
      depth,
      height,
      width
    },
    position: {
      x: centerX,
      y: Math.max(0.015, height / 2),
      z: centerZ
    }
  };
}

function snapVector(value: THREE.Vector3, enabled: boolean): THREE.Vector3 {
  if (!enabled) {
    return value;
  }

  const step = 0.25;
  return new THREE.Vector3(
    Math.round(value.x / step) * step,
    Math.round(value.y / step) * step,
    Math.round(value.z / step) * step
  );
}

function eulerToRecord(value: THREE.Euler): SceneVector3 {
  return { x: value.x, y: value.y, z: value.z };
}

function findActorId(object: THREE.Object3D | null): string | null {
  let current: THREE.Object3D | null = object;
  while (current) {
    if (typeof current.userData.actorId === 'string') {
      return current.userData.actorId;
    }

    current = current.parent;
  }

  return null;
}

function vectorToRecord(value: THREE.Vector3): SceneVector3 {
  return { x: value.x, y: value.y, z: value.z };
}

function readNumber(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}
