import type { RuntimeArtifact } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';

import { RUNTIME_CAPABILITY_CONTRACT } from './runtime-contract/RuntimeCapabilityContract';
import { LATEST_RUNTIME_COMPILER_REVISION, type RuntimeManifestDeclaration } from './RuntimeArtifactIntegrity';

export interface LatestRuntimeArtifactEnvelope {
  artifactHash: string;
  compilerVersion: string;
  document: Record<string, unknown>;
  id: string;
  manifest: RuntimeManifestDeclaration[];
  manifestTypes: string[];
  revision: number;
  signature: string;
}

export function parseRuntimePageArtifact(artifactJson: string): LatestRuntimeArtifactEnvelope | null {
  try {
    const value = JSON.parse(artifactJson) as unknown;
    if (!isLatestRuntimeArtifactEnvelope(value)) return null;
    if (containsEditorSessionState(value.document)) return null;
    const manifest = parseManifestDeclarations(value.manifest);
    if (!manifest || !matchesManifestTypes(manifest, value.manifestTypes)) return null;
    return { ...value, manifest };
  } catch {
    return null;
  }
}

export function toRuntimeArtifact(envelope: LatestRuntimeArtifactEnvelope): (RuntimeArtifact & { manifest: RuntimeManifestDeclaration[] }) | null {
  if (!isLatestRuntimeArtifactEnvelope(envelope)) return null;
  const { document } = envelope;
  if (containsEditorSessionState(document)) return null;
  const manifest = parseManifestDeclarations(envelope.manifest);
  if (!manifest || !matchesManifestTypes(manifest, envelope.manifestTypes)) return null;
  const elements = isRecord(document.elements) ? document.elements : null;
  const actions = strictRecords(document.actions);
  const apiBindings = strictRecords(document.apiBindings);
  const dataSourceBindings = strictRecords(document.dataSources);
  const workflowBindings = strictRecords(document.workflowBindings);
  const pageMicroflows = strictRecords(document.pageMicroflows);
  const pageParameters = strictRecords(document.pageParameters);
  const pages = strictRecords(document.pages) ?? undefined;
  const variables = strictRecords(document.variables);
  const permissions = isRecord(document.permissions) ? document.permissions : null;
  const runtimeContext = isRecord(document.runtimeContext) ? document.runtimeContext : null;
  if (!elements || !actions || !apiBindings || !dataSourceBindings || !workflowBindings || !pageMicroflows || !pageParameters || !variables || !isCanonicalNonEmptyString(document.documentId) || !permissions || !runtimeContext) return null;

  return {
    actions,
    artifactHash: envelope.artifactHash,
    bindings: [...apiBindings, ...dataSourceBindings, ...workflowBindings],
    compilerVersion: envelope.compilerVersion,
    documentId: document.documentId,
    elements: elements as RuntimeArtifact['elements'],
    integrityPayload: document,
    manifest,
    manifestTypes: envelope.manifestTypes,
    pages: pages as RuntimeArtifact['pages'],
    pageMicroflows: pageMicroflows as unknown as RuntimeArtifact['pageMicroflows'],
    pageParameters,
    permissions,
    revision: envelope.revision,
    runtimeContext,
    signature: envelope.signature,
    variables
  } as RuntimeArtifact & { integrityPayload: Record<string, unknown>; manifest: RuntimeManifestDeclaration[] };
}

function parseManifestDeclarations(value: unknown): RuntimeManifestDeclaration[] | null {
  if (!Array.isArray(value) || value.length === 0 || !value.every(isRecord)) return null;
  const declarations = value as RuntimeManifestDeclaration[];
  return declarations.every((declaration) => isCanonicalNonEmptyString(declaration.type) && hasCanonicalRenderer(declaration)) ? declarations : null;
}

function hasCanonicalRenderer(declaration: RuntimeManifestDeclaration): boolean {
  const renderer = declaration.renderer;
  return isRecord(renderer) &&
    renderer.runtime === RUNTIME_CAPABILITY_CONTRACT.renderer &&
    renderer.preview === RUNTIME_CAPABILITY_CONTRACT.renderer;
}

function hasFormalRuntimeDocument(document: Record<string, unknown>): boolean {
  const requiredRecordFields = ['elements', 'permissions', 'runtimeContext'];
  const requiredArrayFields = ['actions', 'apiBindings', 'dataSources', 'pageMicroflows', 'pageParameters', 'variables', 'workflowBindings'];
  return isCanonicalNonEmptyString(document.documentId) &&
    isPositiveInteger(document.revision) &&
    requiredRecordFields.every((field) => isRecord(document[field])) &&
    requiredArrayFields.every((field) => strictRecords(document[field]) !== null);
}

function isLatestRuntimeArtifactEnvelope(value: unknown): value is LatestRuntimeArtifactEnvelope {
  if (!isRecord(value) ||
      !isCanonicalNonEmptyString(value.id) ||
      !isRecord(value.document) ||
      !hasFormalRuntimeDocument(value.document) ||
      value.document.revision !== value.revision ||
      value.compilerVersion !== LATEST_RUNTIME_COMPILER_REVISION ||
      !isSha256Hash(value.artifactHash) ||
      !isSignature(value.signature) ||
      !isPositiveInteger(value.revision) ||
      !Array.isArray(value.manifest) ||
      !Array.isArray(value.manifestTypes) ||
      value.manifestTypes.length === 0 ||
      !value.manifestTypes.every(isCanonicalNonEmptyString) ||
      new Set(value.manifestTypes).size !== value.manifestTypes.length) {
    return false;
  }
  return true;
}

function matchesManifestTypes(declarations: readonly RuntimeManifestDeclaration[], manifestTypes: readonly unknown[]): boolean {
  const declarationTypes = declarations.map((declaration) => declaration.type.trim());
  const normalizedTypes = manifestTypes.map((type) => typeof type === 'string' ? type.trim() : '');
  return new Set(declarationTypes).size === declarationTypes.length &&
    declarationTypes.length === normalizedTypes.length &&
    normalizedTypes.every((type) => type.length > 0 && declarationTypes.includes(type));
}

function strictRecords(value: unknown): Record<string, unknown>[] | null {
  return Array.isArray(value) && value.every(isRecord) ? value : null;
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

function isCanonicalNonEmptyString(value: unknown): value is string {
  return isNonEmptyString(value) && value === value.trim();
}

function isPositiveInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value > 0;
}

function isSha256Hash(value: unknown): value is string {
  return typeof value === 'string' && value === value.trim() && /^sha256:[0-9a-f]{64}$/i.test(value);
}

function isSignature(value: unknown): value is string {
  return typeof value === 'string' && value === value.trim() && /^[0-9a-f]{64}$/i.test(value);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}

function containsEditorSessionState(document: Record<string, unknown>): boolean {
  const sessionKeys = new Set([
    'anchorNodeId',
    'editorState',
    'editorSession',
    'history',
    'historyIndex',
    'primaryNodeId',
    'selectedElementId',
    'selectedNodeIds',
    'tree',
    'viewport'
  ]);
  if (Object.keys(document).some((key) => sessionKeys.has(key))) return true;
  return isRecord(document.metadata) && Object.keys(document.metadata).some((key) => sessionKeys.has(key));
}
