import type { GridPageResult } from '@/api/shared.types';

export type AsterScenePage<T> = GridPageResult<T>;

export interface AsterSceneGridQuery {
  assetType?: string;
  creatorHandle?: string;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
  projectId?: string;
  status?: string;
  workId?: string;
}

export interface SceneDocument {
  actors: SceneActor[];
  assets: SceneAssetRef[];
  components: SceneComponent[];
  extensions: Record<string, unknown>;
  geometries: SceneGeometry[];
  identity: {
    documentId: string;
    locale: string;
    projectId: string;
  };
  interactions: {
    blueprints: SceneBlueprint[];
    hotspots: SceneHotspot[];
    nav: Record<string, unknown>;
  };
  materials: SceneMaterial[];
  meta: {
    createdAt?: string;
    product: 'AsterScene';
    title: string;
    updatedAt?: string;
  };
  publish: {
    license: string;
    slug?: string | null;
    visibility: string;
  };
  quality: Record<string, unknown>;
  revision: number;
  runtime: {
    camera: Record<string, unknown>;
    entrySceneId: string;
    scenes: SceneRuntimeScene[];
  };
  timeline: {
    currentFrame?: number;
    frameRate?: number;
    range?: {
      end: number;
      start: number;
    };
    sequences: SceneTimelineSequence[];
    tracks: SceneTimelineTrack[];
  };
  uv: {
    layouts: SceneUvLayout[];
  };
}

export interface SceneActor {
  components: string[];
  display?: SceneActorDisplay;
  flags?: SceneActorFlags;
  id: string;
  layerId?: string | null;
  name: string;
  parentId?: string | null;
  pivot?: SceneVector3;
  tags?: string[];
  type: 'camera' | 'exhibit' | 'hotspot' | 'light' | 'mesh' | 'structure' | string;
}

export interface SceneAssetRef {
  id: string;
  kind:
    | 'audio'
    | 'decal'
    | 'document'
    | 'hdri'
    | 'image'
    | 'material'
    | 'mesh'
    | 'model'
    | 'panorama'
    | 'prefab'
    | 'preset'
    | 'texture'
    | 'video'
    | string;
  metadata?: Record<string, unknown>;
  url?: string;
  version?: number;
}

export interface SceneComponent {
  id: string;
  type:
    | 'camera'
    | 'collider'
    | 'editableMesh'
    | 'helper'
    | 'hotspotAnchor'
    | 'label'
    | 'light'
    | 'materialBinding'
    | 'materialSlots'
    | 'mediaSurface'
    | 'mesh'
    | 'meshRenderer'
    | 'modifierStack'
    | 'transform'
    | 'uvMapping'
    | string;
  [key: string]: unknown;
}

export interface SceneGeometry {
  editableMeshRef?: SceneEditableMeshRef;
  generatedMeshRef?: SceneGeneratedMeshRef;
  id: string;
  parameters: Record<string, unknown>;
  topology?: SceneTopologySummary;
  type: SceneGeometryType | string;
}

export interface SceneMaterial {
  id: string;
  name: string;
  nodeGraph?: SceneMaterialNodeGraph;
  pbr?: ScenePbrMaterial;
  slots?: SceneMaterialSlot[];
  type: string;
  [key: string]: unknown;
}

export interface SceneBlueprint {
  api?: string;
  id: string;
  name: string;
  trigger: string;
}

export interface SceneHotspot {
  action?: SceneHotspotAction;
  enabled?: boolean;
  facing?: {
    pitch?: number;
    roll?: number;
    yaw?: number;
  };
  id: string;
  label: string;
  payload?: {
    body?: string;
    mediaAssetId?: string;
    title?: string;
    url?: string;
  };
  position?: SceneVector3;
  sceneId?: string;
  spherical?: {
    pitch: number;
    yaw: number;
  };
  target: string;
  trigger?: SceneHotspotTrigger;
  type?: 'action' | 'info' | 'media' | 'navigate' | 'url';
  visibility?: {
    distanceMax?: number;
    distanceMin?: number;
    frameEnd?: number;
    frameStart?: number;
  };
}

export interface SceneRuntimeScene {
  actors: string[];
  environment?: {
    fov?: number;
    initialPitch?: number;
    initialYaw?: number;
    panoramaAssetId?: string;
    projection?: 'equirectangular';
    stereo?: 'leftRight' | 'mono' | 'topBottom';
    yawOffset?: number;
  };
  id: string;
  name: string;
  type?: 'model3d' | 'panorama720';
}

export interface SceneVector3 {
  x: number;
  y: number;
  z: number;
}

export type SceneHotspotAction = 'action' | 'info' | 'media' | 'navigate' | 'url';

export type SceneGeometryType =
  | 'box'
  | 'ceiling'
  | 'cone'
  | 'cylinder'
  | 'door'
  | 'editableMesh'
  | 'generatedMesh'
  | 'plane'
  | 'sphere'
  | 'torus'
  | 'tube'
  | 'wall'
  | 'window';

export type SceneTransformMode = 'rotate' | 'scale' | 'translate';

export type SceneTransformSpace = 'local' | 'view' | 'world';

export type SceneViewportLayout = 'quad' | 'single';

export type SceneSubObjectMode = 'border' | 'edge' | 'element' | 'object' | 'polygon' | 'vertex';

export interface SceneActorDisplay {
  color?: string;
  frozen?: boolean;
  hidden?: boolean;
  selected?: boolean;
  showBox?: boolean;
  showWire?: boolean;
}

export interface SceneActorFlags {
  castShadow?: boolean;
  freezeTransform?: boolean;
  receiveShadow?: boolean;
  renderable?: boolean;
  selectable?: boolean;
}

export interface SceneEditableMeshRef {
  assetId?: string;
  inline?: SceneEditableMeshPayload;
  version?: number;
}

export interface SceneGeneratedMeshRef {
  assetId: string;
  version: number;
}

export interface SceneTopologySummary {
  boundaryEdges: number;
  edgeCount: number;
  faceCount: number;
  nonManifoldEdges: number;
  vertexCount: number;
}

export interface SceneEditableMeshPayload {
  edges?: [number, number][];
  faces: Array<SceneEditableMeshFace | number[]>;
  materialIndices?: number[];
  normals?: SceneVector3[];
  uvs?: [number, number][];
  vertices: SceneVector3[];
}

export interface SceneEditableMeshFace {
  materialIndex?: number;
  normal?: SceneVector3;
  smoothingGroup?: number;
  vertices: number[];
}

export interface SceneMaterialSlot {
  id: string;
  materialId: string;
  name: string;
}

export interface ScenePbrMaterial {
  alphaMode?: 'blend' | 'mask' | 'opaque';
  baseColor?: string;
  doubleSided?: boolean;
  emissive?: string;
  metallic?: number;
  opacity?: number;
  roughness?: number;
  textureSlots?: Partial<Record<ScenePbrTextureSlot, SceneTextureBinding>>;
  uvTransform?: SceneUvTransform;
}

export type ScenePbrTextureSlot = 'ao' | 'baseColor' | 'emissive' | 'metallicRoughness' | 'normal' | 'opacity';

export interface SceneTextureBinding {
  assetId?: string;
  channel?: number;
  url?: string;
}

export interface SceneMaterialPbrPatch {
  alphaMode?: ScenePbrMaterial['alphaMode'];
  baseColor?: string;
  doubleSided?: boolean;
  emissive?: string;
  metallic?: number;
  opacity?: number;
  roughness?: number;
  textureSlots?: Partial<Record<ScenePbrTextureSlot, SceneTextureBinding | null>>;
  uvTransform?: SceneUvTransform;
}

export interface SceneUvTransform {
  offset?: [number, number];
  repeat?: [number, number];
  rotation?: number;
}

export interface SceneMaterialNodeGraph {
  edges: Array<{ from: string; to: string }>;
  nodes: SceneMaterialNode[];
}

export interface SceneMaterialNode {
  id: string;
  inputs?: Record<string, unknown>;
  label: string;
  position?: { x: number; y: number };
  type: 'bitmap' | 'color' | 'math' | 'output' | 'pbr' | string;
}

export interface SceneTimelineSequence {
  endFrame: number;
  id: string;
  name: string;
  startFrame: number;
}

export interface SceneTimelineTrack {
  id: string;
  keyframes: SceneTimelineKeyframe[];
  property: string;
  targetId: string;
  type: 'camera' | 'hotspot' | 'material' | 'transform' | string;
}

export interface SceneTimelineKeyframe {
  easing?: 'bezier' | 'linear' | 'step';
  frame: number;
  tangentIn?: number;
  tangentOut?: number;
  value: unknown;
}

export interface SceneModifier {
  enabled: boolean;
  id: string;
  name: string;
  order?: number;
  parameters: Record<string, unknown>;
  previewSupported?: boolean;
  type:
    | 'array'
    | 'bend'
    | 'boolean'
    | 'editPoly'
    | 'mirror'
    | 'shell'
    | 'subdivide'
    | 'taper'
    | 'twist'
    | 'uvwMap'
    | 'xform'
    | string;
}

export interface SceneUvLayout {
  id: string;
  mapping: 'box' | 'cylindrical' | 'planar' | 'spherical' | 'unwrap';
  targetId: string;
  transform?: SceneUvTransform;
}

export interface SceneHotspotTrigger {
  event: 'click' | 'enter' | 'frame' | 'proximity';
  frame?: number;
  radius?: number;
}

export interface AsterSceneProject {
  coverAssetId?: string | null;
  createdTime: string;
  currentPublishCode?: string | null;
  currentRevision: number;
  description?: string | null;
  documentHash: string;
  id: string;
  projectCode: string;
  projectName: string;
  publishedVersion: number;
  status: string;
  updatedTime?: string | null;
  visibility: string;
}

export interface AsterSceneDocumentResponse {
  document: SceneDocument;
  documentHash: string;
  project: AsterSceneProject;
  revision: number;
  savedAt: string;
}

export interface AsterSceneCreateProjectRequest {
  clientMutationId?: string;
  description?: string | null;
  projectName: string;
  templateCode?: string | null;
  visibility: string;
}

export interface AsterSceneUpdateProjectRequest {
  clientMutationId: string;
  coverAssetId?: string | null;
  description?: string | null;
  projectName: string;
  visibility: string;
}

export interface AsterSceneSaveDocumentRequest {
  clientMutationId: string;
  document: SceneDocument;
  documentHash: string;
  expectedRevision: number;
  saveSource: string;
}

export interface AsterSceneSaveDocumentResponse {
  clientMutationId: string;
  documentHash: string;
  projectId: string;
  revision: number;
  savedAt: string;
}

export interface AsterSceneDocumentVersion {
  clientMutationId?: string | null;
  documentHash: string;
  isCurrent: boolean;
  projectId: string;
  revision: number;
  savedAt: string;
  savedBy: string;
  saveSource: string;
}

export interface AsterSceneRestoreDocumentVersionRequest {
  clientMutationId: string;
  expectedRevision: number;
}

export interface AsterSceneAsset {
  assetCode: string;
  assetType: string;
  checksum?: string | null;
  contentType?: string | null;
  createdTime: string;
  currentVersion: number;
  fileName: string;
  id: string;
  metadata?: Record<string, unknown> | null;
  projectId: string;
  runtimeUrl?: string | null;
  sizeBytes?: number | null;
  status: string;
  thumbnailUrl?: string | null;
}

export interface AsterSceneAssetRegisterRequest {
  assetType: string;
  checksum?: string | null;
  clientMutationId?: string;
  contentType?: string | null;
  fileName: string;
  metadata?: Record<string, unknown> | null;
  projectId: string;
  sizeBytes?: number | null;
  sourceUrl: string;
}

export interface AsterSceneGeneratedAssetRequest {
  assetType: 'material' | 'mesh' | 'preset';
  checksum?: string | null;
  clientMutationId: string;
  contentType?: string | null;
  fileName: string;
  metadata?: Record<string, unknown> | null;
  payload: Record<string, unknown> | SceneEditableMeshPayload;
  projectId: string;
}

export interface AsterSceneUploadSession {
  projectId: string;
  sizeBytes: number;
  status: string;
  totalChunks: number;
  uploadId: string;
  uploadedChunks: number;
}

export interface RuntimeManifest {
  analytics: Record<string, unknown>;
  assetVariants: Record<string, RuntimeAssetVariant[]>;
  capabilityPolicy: Record<string, unknown>;
  document: SceneDocument;
  documentHash: string;
  entrySceneId: string;
  lazyGroups: Record<string, unknown>;
  preload: Record<string, unknown>;
  publishCode: string;
  security: Record<string, unknown>;
}

export interface RuntimeAssetVariant {
  assetVersionId: string;
  checksum?: string | null;
  contentType?: string | null;
  runtimeUrl?: string | null;
  sizeBytes?: number | null;
  sourceUrl?: string | null;
  variantType: string;
  version: number;
}

export interface AsterSceneRuntimeEventRequest {
  clientEventId: string;
  eventType: string;
  hotspotId?: string | null;
  publishCode: string;
  sceneId?: string | null;
}

export interface AsterSceneRuntimeEventResponse {
  clientEventId: string;
  eventType: string;
  hotspotId?: string | null;
  ledgerId: string;
  occurredAt: string;
  publishCode: string;
  sceneId?: string | null;
}

export interface AsterScenePublishRequest {
  clientMutationId: string;
  documentHash: string;
  expectedRevision: number;
  qualityGateMode: string;
  visibility: string;
}

export interface AsterScenePublishVersion {
  documentHash: string;
  documentRevision: number;
  id: string;
  projectId: string;
  publishCode: string;
  publishedAt: string;
  status: string;
  version: number;
  visibility: string;
}

export interface AsterScenePublicWork {
  coverAssetId?: string | null;
  creatorHandle: string;
  favoriteCount: number;
  id: string;
  likeCount: number;
  publishCode: string;
  publishedAt: string;
  remixCount: number;
  slug: string;
  status: string;
  summary?: string | null;
  title: string;
  viewCount: number;
  visibility: string;
}

export interface AsterSceneCreatorProfile {
  avatarUrl?: string | null;
  bio?: string | null;
  displayName: string;
  followersCount: number;
  handle: string;
  worksCount: number;
}

export interface AsterSceneSubscriptionPlan {
  aiCreditsMonthly: number;
  planCode: string;
  planName: string;
  priceMonthly: number;
  publishedWorks: number;
  storageGb: number;
}

export interface AsterSceneSubscription {
  endsAt?: string | null;
  id: string;
  planCode: string;
  startedAt: string;
  status: string;
}

export interface AsterSceneUsageSummary {
  aiCreditsRemaining: number;
  planCode: string;
  publishedWorksLimit: number;
  publishedWorksUsed: number;
  storageGbLimit: number;
  storageGbUsed: number;
}

export interface AsterSceneUsageLedgerEntry {
  direction: string;
  id: string;
  occurredAt: string;
  quantity: number;
  sourceId: string;
  sourceType: string;
  unit: string;
  usageType: string;
}

export interface AsterSceneModerationCase {
  createdTime: string;
  decision?: string | null;
  id: string;
  projectId?: string | null;
  reasonCode: string;
  status: string;
  workId?: string | null;
}

export interface AsterSceneAppeal {
  appellantUserId: string;
  caseId: string;
  clientMutationId: string;
  createdTime: string;
  id: string;
  reason: string;
  status: string;
}

export interface AsterSceneSupportTicket {
  createdTime: string;
  id: string;
  projectId: string;
  severity: string;
  status: string;
  title: string;
}

export interface AsterSceneSupportComment {
  commentType: string;
  createdTime: string;
  id: string;
  message: string;
  statusAfter?: string | null;
  ticketId: string;
}

export interface AsterSceneSupportTicketDetail extends AsterSceneSupportTicket {
  comments: AsterSceneSupportComment[];
  diagnostics?: Record<string, unknown> | null;
}

export interface AsterSceneJob {
  createdTime: string;
  errorCode?: string | null;
  errorMessage?: string | null;
  id: string;
  jobCode: string;
  jobType: string;
  output?: Record<string, unknown> | null;
  progressPercent: number;
  status: string;
}
