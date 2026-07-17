import * as THREE from 'three';

import type {
  SceneActor,
  SceneComponent,
  SceneDocument,
  SceneEditableMeshPayload,
  SceneGeometry,
  SceneMaterial,
  SceneModifier,
  SceneRuntimeScene
} from '../../../model/types';

import { readColor, readNumber, readOptionalString, readPbrValue, readString, readVector } from './viewportValues';

export type ViewportRenderMode = 'player' | 'studio';

const previewModifierTypes = new Set(['array', 'editPoly', 'mirror', 'subdivide', 'uvwMap', 'xform']);

export interface ActorModelRequest {
  actorId: string;
  actorName: string;
  assetId: string | null;
  castShadow: boolean;
  receiveShadow: boolean;
  root: THREE.Object3D;
  selected: boolean;
  showWire: boolean;
  sourceUrl: string | null;
  modifiers: SceneModifier[];
}

export interface ActorObjectBuildResult {
  modelRequests: ActorModelRequest[];
  objects: THREE.Object3D[];
}

interface ActorObjectFactoryOptions {
  activeScene: SceneRuntimeScene | null;
  document: SceneDocument;
  mode: ViewportRenderMode;
  selectedActorId: string | null;
}

interface ActorRenderContext {
  actor: SceneActor;
  components: SceneComponent[];
  geometry?: SceneGeometry;
  material?: SceneMaterial;
  meshComponent?: SceneComponent;
  modifiers: SceneModifier[];
  selected: boolean;
}

interface ActorObjectFactoryResult {
  modelRequest?: ActorModelRequest;
  object: THREE.Object3D | null;
}

export class ActorObjectFactory {
  private readonly actorIds: Set<string>;
  private readonly componentMap: Map<string, SceneComponent>;
  private readonly geometryMap: Map<string, SceneGeometry>;
  private readonly materialMap: Map<string, SceneMaterial>;

  public constructor(private readonly options: ActorObjectFactoryOptions) {
    this.actorIds = new Set(options.activeScene?.actors ?? options.document.actors.map((actor) => actor.id));
    this.componentMap = new Map(options.document.components.map((component) => [component.id, component]));
    this.geometryMap = new Map(options.document.geometries.map((geometry) => [geometry.id, geometry]));
    this.materialMap = new Map(options.document.materials.map((material) => [material.id, material]));
  }

  public build(): ActorObjectBuildResult {
    const objects: THREE.Object3D[] = [];
    const modelRequests: ActorModelRequest[] = [];

    this.options.document.actors
      .filter((actor) => this.actorIds.has(actor.id))
      .filter((actor) => this.shouldRenderActor(actor))
      .forEach((actor) => {
        const context = this.createContext(actor);
        const result = this.createActorObject(context);
        if (!result.object) {
          return;
        }

        objects.push(result.object);
        if (result.modelRequest) {
          modelRequests.push(result.modelRequest);
        }
      });

    return { modelRequests, objects };
  }

  private shouldRenderActor(actor: SceneActor): boolean {
    if (actor.display?.hidden === true) {
      return false;
    }

    return this.options.mode === 'studio' || actor.flags?.renderable !== false;
  }

  private createContext(actor: SceneActor): ActorRenderContext {
    const components = actor.components.map((componentId) => this.componentMap.get(componentId)).filter((component): component is SceneComponent => Boolean(component));
    const meshComponent = components.find((component) => component.type === 'meshRenderer') ?? components.find((component) => component.type === 'mesh');
    const materialBinding = components.find((component) => component.type === 'materialBinding');
    const modifierStack = components.find((component) => component.type === 'modifierStack');

    return {
      actor,
      components,
      geometry: this.geometryMap.get(readString(meshComponent?.geometryId)),
      material: this.materialMap.get(readString(materialBinding?.materialId)),
      meshComponent,
      modifiers: readEnabledPreviewModifiers(modifierStack),
      selected: actor.id === this.options.selectedActorId
    };
  }

  private createActorObject(context: ActorRenderContext): ActorObjectFactoryResult {
    const transform = context.components.find((component) => component.type === 'transform');
    const light = context.components.find((component) => component.type === 'light');
    const camera = context.components.find((component) => component.type === 'camera');
    const helper = context.components.find((component) => component.type === 'helper' || component.type === 'collider' || component.type === 'hotspotAnchor');

    if (light) {
      const object = new THREE.PointLight(readColor(light.color, 0xffffff), readNumber(light.intensity, 1), readNumber(light.range, 8));
      setupActorObject(object, context.actor, transform);
      return { object };
    }

    if (camera) {
      const object = createCameraObject(context.selected);
      setupActorObject(object, context.actor, transform);
      return { object };
    }

    if (helper) {
      const object = createHelperObject(context.actor, helper, context.selected);
      setupActorObject(object, context.actor, transform);
      return { object };
    }

    if (context.meshComponent?.type === 'meshRenderer') {
      return this.createModelActorObject(context, transform);
    }

    const geometry = createGeometry(context.geometry);
    applyGeometryModifierPreview(geometry, context.modifiers);
    const material = createMaterial(context.material, context.selected, context.actor.display?.showWire === true);
    const mesh = new THREE.Mesh(geometry, material);
    computeBoundsTreeIfAvailable(geometry);
    mesh.castShadow = context.actor.flags?.castShadow !== false;
    mesh.receiveShadow = context.actor.flags?.receiveShadow !== false;
    setupActorObject(mesh, context.actor, transform);
    applyObjectModifierPreview(mesh, context.modifiers);
    return { object: mesh };
  }

  private createModelActorObject(context: ActorRenderContext, transform?: SceneComponent): ActorObjectFactoryResult {
    const root = new THREE.Group();
    const placeholder = new THREE.Mesh(
      createGeometry(context.geometry),
      createMaterial(context.material, context.selected, context.actor.display?.showWire === true)
    );
    placeholder.name = `${context.actor.name} Placeholder`;
    placeholder.userData.modelPlaceholder = true;
    placeholder.userData.selectable = false;
    computeBoundsTreeIfAvailable(placeholder.geometry);
    root.add(placeholder);

    setupActorObject(root, context.actor, transform);
    const assetId = readOptionalString(context.meshComponent?.assetId);
    const sourceUrl = readOptionalString(context.meshComponent?.sourceUrl);
    root.userData.modelAssetId = assetId;
    root.userData.modelSourceUrl = sourceUrl;

    return {
      modelRequest: {
        actorId: context.actor.id,
        actorName: context.actor.name,
        assetId,
        castShadow: context.actor.flags?.castShadow !== false,
        receiveShadow: context.actor.flags?.receiveShadow !== false,
        root,
        selected: context.selected,
        showWire: context.actor.display?.showWire === true,
        sourceUrl,
        modifiers: context.modifiers
      },
      object: root
    };
  }
}

export function createGeometry(geometry?: SceneGeometry): THREE.BufferGeometry {
  if (geometry?.type === 'editableMesh' && geometry.editableMeshRef?.inline) {
    return createEditableMeshGeometry(geometry.editableMeshRef.inline.vertices, geometry.editableMeshRef.inline.faces);
  }

  const parameters = geometry?.parameters ?? {};
  switch (geometry?.type) {
    case 'ceiling':
    case 'door':
    case 'wall':
    case 'window':
    case 'box':
      return new THREE.BoxGeometry(readNumber(parameters.width, 1), readNumber(parameters.height, 1), readNumber(parameters.depth, 1));
    case 'cone':
      return new THREE.ConeGeometry(readNumber(parameters.radius, 0.5), readNumber(parameters.height, 1), 32);
    case 'cylinder':
      return new THREE.CylinderGeometry(readNumber(parameters.radiusTop, 0.5), readNumber(parameters.radiusBottom, 0.5), readNumber(parameters.height, 1), 32);
    case 'plane':
      return new THREE.PlaneGeometry(readNumber(parameters.width, 1), readNumber(parameters.depth, 1));
    case 'sphere':
      return new THREE.SphereGeometry(readNumber(parameters.radius, 0.5), readNumber(parameters.segments, 32), 16);
    case 'torus':
      return new THREE.TorusGeometry(readNumber(parameters.radius, 0.5), readNumber(parameters.tube, 0.12), 16, 48);
    case 'tube':
      return new THREE.CylinderGeometry(readNumber(parameters.radius, 0.16), readNumber(parameters.radius, 0.16), readNumber(parameters.height, 1), 24);
    default:
      return new THREE.BoxGeometry(1, 1, 1);
  }
}

export function createMaterial(material?: SceneMaterial | Record<string, unknown>, selected = false, wireframe = false): THREE.MeshStandardMaterial {
  const pbr = material?.pbr && typeof material.pbr === 'object' ? (material.pbr as Record<string, unknown>) : undefined;
  const opacity = readNumber(pbr?.opacity ?? material?.opacity, 1);
  const next = new THREE.MeshStandardMaterial({
    color: selected ? 0x2563eb : readColor(pbr?.baseColor ?? material?.baseColor, 0x8a9099),
    emissive: readColor(pbr?.emissive ?? material?.emissive, 0x000000),
    metalness: readNumber(pbr?.metallic ?? material?.metallic, 0),
    opacity,
    roughness: readNumber(pbr?.roughness ?? material?.roughness, 0.68),
    transparent: opacity < 1,
    wireframe
  });

  if (pbr?.doubleSided === true) {
    next.side = THREE.DoubleSide;
  }

  applyTextureSlot(next, material, 'baseColor', (texture) => {
    texture.colorSpace = THREE.SRGBColorSpace;
    next.map = texture;
  });
  applyTextureSlot(next, material, 'normal', (texture) => {
    next.normalMap = texture;
  });
  applyTextureSlot(next, material, 'metallicRoughness', (texture) => {
    next.metalnessMap = texture;
    next.roughnessMap = texture;
  });
  applyTextureSlot(next, material, 'ao', (texture) => {
    next.aoMap = texture;
  });
  applyTextureSlot(next, material, 'emissive', (texture) => {
    texture.colorSpace = THREE.SRGBColorSpace;
    next.emissiveMap = texture;
  });
  applyTextureSlot(next, material, 'opacity', (texture) => {
    next.alphaMap = texture;
    next.transparent = true;
  });

  return next;
}

export function createHelperObject(actor: SceneActor, component: SceneComponent, selected: boolean): THREE.Object3D {
  const color = selected ? 0x2563eb : actor.type === 'hotspot' ? 0x0f766e : 0x64748b;
  const group = new THREE.Group();
  if (component.type === 'collider') {
    const mesh = new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshBasicMaterial({ color, opacity: 0.16, transparent: true, wireframe: true }));
    group.add(mesh);
  } else if (component.type === 'hotspotAnchor') {
    const mesh = new THREE.Mesh(new THREE.SphereGeometry(0.16, 16, 12), new THREE.MeshBasicMaterial({ color }));
    group.add(mesh);
  } else {
    const axes = new THREE.AxesHelper(0.6);
    group.add(axes);
  }

  return group;
}

function createCameraObject(selected: boolean): THREE.Object3D {
  const group = new THREE.Group();
  const body = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.24, 0.28), new THREE.MeshBasicMaterial({ color: selected ? 0x2563eb : 0x334155 }));
  const lens = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.12, 0.22, 20), new THREE.MeshBasicMaterial({ color: 0x0f172a }));
  lens.rotation.x = Math.PI / 2;
  lens.position.z = -0.25;
  group.add(body, lens);
  return group;
}

function createEditableMeshGeometry(vertices: SceneEditableMeshPayload['vertices'], faces: SceneEditableMeshPayload['faces']): THREE.BufferGeometry {
  const positions: number[] = [];
  const indices: number[] = [];
  vertices.forEach((vertex) => positions.push(vertex.x, vertex.y, vertex.z));
  faces.forEach((face) => {
    const faceVertices = Array.isArray(face) ? face : face.vertices;
    for (let index = 1; index < faceVertices.length - 1; index += 1) {
      indices.push(faceVertices[0], faceVertices[index], faceVertices[index + 1]);
    }
  });

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  geometry.computeBoundingBox();
  geometry.computeBoundingSphere();
  return geometry;
}

function setupActorObject(object: THREE.Object3D, actor: SceneActor, transform?: SceneComponent): void {
  object.name = actor.name;
  object.userData.actorId = actor.id;
  object.userData.selectable = actor.flags?.selectable !== false && actor.display?.frozen !== true;
  object.userData.animate = actor.type === 'exhibit' ? 'idle-rotate' : undefined;
  object.position.copy(readVector(transform?.position, { x: 0, y: 0, z: 0 }));
  const rotation = readVector(transform?.rotation, { x: 0, y: 0, z: 0 });
  object.rotation.set(rotation.x, rotation.y, rotation.z);
  object.scale.copy(readVector(transform?.scale, { x: 1, y: 1, z: 1 }));
}

export function applyLoadedModelModifierPreview(model: THREE.Object3D, modifiers: SceneModifier[]): void {
  model.traverse((object) => {
    if (object instanceof THREE.Mesh) {
      applyGeometryModifierPreview(object.geometry, modifiers);
    }
  });
  applyObjectModifierPreview(model, modifiers);
}

function applyObjectModifierPreview(object: THREE.Object3D, modifiers: SceneModifier[]): void {
  modifiers.forEach((modifier) => {
    if (modifier.type === 'xform') {
      object.position.x += readNumber(modifier.parameters.translateX, 0);
      object.position.y += readNumber(modifier.parameters.translateY, 0);
      object.position.z += readNumber(modifier.parameters.translateZ, 0);
      object.scale.x *= readNumber(modifier.parameters.scaleX, 1);
      object.scale.y *= readNumber(modifier.parameters.scaleY, 1);
      object.scale.z *= readNumber(modifier.parameters.scaleZ, 1);
      return;
    }

    if (modifier.type === 'mirror') {
      object.scale.x *= modifier.parameters.axisX === true ? -1 : 1;
      object.scale.y *= modifier.parameters.axisY === true ? -1 : 1;
      object.scale.z *= modifier.parameters.axisZ === true ? -1 : 1;
      return;
    }

    if (modifier.type === 'array') {
      addArrayPreviewClones(object, modifier);
    }
  });
}

function addArrayPreviewClones(object: THREE.Object3D, modifier: SceneModifier): void {
  const count = Math.min(64, Math.max(1, Math.round(readNumber(modifier.parameters.count, 1))));
  if (count <= 1) {
    return;
  }

  const source = object.clone(true);
  for (let index = 1; index < count; index += 1) {
    const clone = source.clone(true);
    clone.position.set(
      readNumber(modifier.parameters.offsetX, 0) * index,
      readNumber(modifier.parameters.offsetY, 0) * index,
      readNumber(modifier.parameters.offsetZ, 0) * index
    );
    clone.userData.selectable = false;
    object.add(clone);
  }
}

function applyGeometryModifierPreview(geometry: THREE.BufferGeometry, modifiers: SceneModifier[]): void {
  modifiers.forEach((modifier) => {
    if (modifier.type === 'uvwMap') {
      applyUvModifierPreview(geometry, modifier);
      return;
    }

    if (modifier.type === 'subdivide') {
      subdivideGeometryInPlace(geometry, Math.min(2, Math.max(1, Math.round(readNumber(modifier.parameters.iterations, 1)))));
    }
  });
}

function applyUvModifierPreview(geometry: THREE.BufferGeometry, modifier: SceneModifier): void {
  const uv = geometry.getAttribute('uv');
  if (!(uv instanceof THREE.BufferAttribute)) {
    return;
  }

  const offsetX = readNumber(modifier.parameters.offsetX, 0);
  const offsetY = readNumber(modifier.parameters.offsetY, 0);
  const repeatX = readNumber(modifier.parameters.repeatX, 1);
  const repeatY = readNumber(modifier.parameters.repeatY, 1);
  const rotation = THREE.MathUtils.degToRad(readNumber(modifier.parameters.rotation, 0));
  const cos = Math.cos(rotation);
  const sin = Math.sin(rotation);
  for (let index = 0; index < uv.count; index += 1) {
    const u = uv.getX(index) - 0.5;
    const v = uv.getY(index) - 0.5;
    uv.setXY(index, (u * cos - v * sin) * repeatX + 0.5 + offsetX, (u * sin + v * cos) * repeatY + 0.5 + offsetY);
  }
  uv.needsUpdate = true;
}

function subdivideGeometryInPlace(geometry: THREE.BufferGeometry, iterations: number): void {
  for (let iteration = 0; iteration < iterations; iteration += 1) {
    const source = geometry.index ? geometry.toNonIndexed() : geometry.clone();
    const position = source.getAttribute('position');
    const uv = source.getAttribute('uv');
    if (!(position instanceof THREE.BufferAttribute) || position.count < 3) {
      source.dispose();
      return;
    }

    const nextPositions: number[] = [];
    const nextUvs: number[] = [];
    for (let index = 0; index < position.count; index += 3) {
      const a = readAttributeVector3(position, index);
      const b = readAttributeVector3(position, index + 1);
      const c = readAttributeVector3(position, index + 2);
      const ab = midpoint3(a, b);
      const bc = midpoint3(b, c);
      const ca = midpoint3(c, a);
      pushTriangle3(nextPositions, a, ab, ca);
      pushTriangle3(nextPositions, ab, b, bc);
      pushTriangle3(nextPositions, ca, bc, c);
      pushTriangle3(nextPositions, ab, bc, ca);

      if (uv instanceof THREE.BufferAttribute) {
        const uva = readAttributeVector2(uv, index);
        const uvb = readAttributeVector2(uv, index + 1);
        const uvc = readAttributeVector2(uv, index + 2);
        const uvab = midpoint2(uva, uvb);
        const uvbc = midpoint2(uvb, uvc);
        const uvca = midpoint2(uvc, uva);
        pushTriangle2(nextUvs, uva, uvab, uvca);
        pushTriangle2(nextUvs, uvab, uvb, uvbc);
        pushTriangle2(nextUvs, uvca, uvbc, uvc);
        pushTriangle2(nextUvs, uvab, uvbc, uvca);
      }
    }

    geometry.setIndex(null);
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(nextPositions, 3));
    if (nextUvs.length > 0) {
      geometry.setAttribute('uv', new THREE.Float32BufferAttribute(nextUvs, 2));
    }
    geometry.computeVertexNormals();
    geometry.computeBoundingBox();
    geometry.computeBoundingSphere();
    source.dispose();
  }
}

function readEnabledPreviewModifiers(component?: SceneComponent): SceneModifier[] {
  return Array.isArray(component?.modifiers)
    ? component.modifiers
        .filter((modifier): modifier is SceneModifier => Boolean(modifier) && typeof modifier === 'object')
        .filter((modifier) => modifier.enabled !== false && modifier.previewSupported !== false && previewModifierTypes.has(modifier.type))
    : [];
}

function readAttributeVector3(attribute: THREE.BufferAttribute, index: number): THREE.Vector3 {
  return new THREE.Vector3(attribute.getX(index), attribute.getY(index), attribute.getZ(index));
}

function readAttributeVector2(attribute: THREE.BufferAttribute, index: number): THREE.Vector2 {
  return new THREE.Vector2(attribute.getX(index), attribute.getY(index));
}

function midpoint3(first: THREE.Vector3, second: THREE.Vector3): THREE.Vector3 {
  return new THREE.Vector3((first.x + second.x) / 2, (first.y + second.y) / 2, (first.z + second.z) / 2);
}

function midpoint2(first: THREE.Vector2, second: THREE.Vector2): THREE.Vector2 {
  return new THREE.Vector2((first.x + second.x) / 2, (first.y + second.y) / 2);
}

function pushTriangle3(target: number[], a: THREE.Vector3, b: THREE.Vector3, c: THREE.Vector3): void {
  target.push(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z);
}

function pushTriangle2(target: number[], a: THREE.Vector2, b: THREE.Vector2, c: THREE.Vector2): void {
  target.push(a.x, a.y, b.x, b.y, c.x, c.y);
}

function computeBoundsTreeIfAvailable(geometry: THREE.BufferGeometry): void {
  const maybeBvhGeometry = geometry as THREE.BufferGeometry & { computeBoundsTree?: () => void };
  maybeBvhGeometry.computeBoundsTree?.();
}

function readTextureUrl(material: Record<string, unknown> | undefined, slotName: string): string | null {
  const pbr = material?.pbr;
  if (!pbr || typeof pbr !== 'object') {
    return null;
  }

  const textureSlots = (pbr as Record<string, unknown>).textureSlots;
  if (!textureSlots || typeof textureSlots !== 'object') {
    return null;
  }

  const slot = (textureSlots as Record<string, unknown>)[slotName];
  if (!slot || typeof slot !== 'object') {
    return null;
  }

  const url = (slot as Record<string, unknown>).url;
  return typeof url === 'string' && url.length > 0 ? url : null;
}

function applyTextureSlot(
  material: THREE.MeshStandardMaterial,
  source: Record<string, unknown> | undefined,
  slotName: string,
  assign: (texture: THREE.Texture) => void
): void {
  const url = readTextureUrl(source, slotName);
  if (!url) {
    return;
  }

  new THREE.TextureLoader().load(
    url,
    (texture) => {
      if (material.userData.disposed === true) {
        texture.dispose();
        return;
      }

      applyUvTransform(texture, source);
      assign(texture);
      material.needsUpdate = true;
    },
    undefined,
    () => undefined
  );
}

function applyUvTransform(texture: THREE.Texture, material: Record<string, unknown> | undefined): void {
  const transform = readPbrValue(material, 'uvTransform');
  if (!transform || typeof transform !== 'object') {
    return;
  }

  const record = transform as Record<string, unknown>;
  const offset = Array.isArray(record.offset) ? record.offset : [0, 0];
  const repeat = Array.isArray(record.repeat) ? record.repeat : [1, 1];
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.offset.set(readNumber(offset[0], 0), readNumber(offset[1], 0));
  texture.repeat.set(readNumber(repeat[0], 1), readNumber(repeat[1], 1));
  texture.rotation = THREE.MathUtils.degToRad(readNumber(record.rotation, 0));
  texture.center.set(0.5, 0.5);
}
