import type { RuntimeArtifact, RuntimeArtifactManifestDeclaration } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';

import type { RuntimeDiagnostic } from './Diagnostics';
import { RUNTIME_CAPABILITY_CONTRACT } from './runtime-contract/RuntimeCapabilityContract';

/** Untrusted runtime transport input. It is narrowed only after formal validation. */
export type RuntimeArtifactIntegrityInput = unknown;
export type RuntimeManifestDeclaration = RuntimeArtifactManifestDeclaration;
export type SignedRuntimeArtifact = RuntimeArtifact;

export const LATEST_RUNTIME_COMPILER_REVISION = RUNTIME_CAPABILITY_CONTRACT.compilerRevision;

export class RuntimeArtifactIntegrity {
  public static async computeHash(payload: unknown): Promise<string> {
    const subtle = globalThis.crypto?.subtle;
    if (!subtle) throw new Error('Runtime artifact integrity requires Web Crypto SHA-256 support.');
    const serialized = canonicalJson(payload);
    const bytes = new TextEncoder().encode(serialized);
    const digest = await subtle.digest('SHA-256', bytes);
    const hex = Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('');
    return `sha256:${hex}`;
  }

  public static async computeSignature(artifact: Pick<RuntimeArtifact, 'documentId' | 'artifactHash' | 'manifest' | 'manifestTypes' | 'compilerVersion' | 'revision'>): Promise<string> {
    const manifestHash = await this.computeManifestHash(artifact);
    const payload = [artifact.documentId, artifact.artifactHash, manifestHash.slice('sha256:'.length), artifact.compilerVersion, String(artifact.revision)].join('\n');
    return (await this.computeHash(payload)).slice('sha256:'.length);
  }

  public static async hasSameIdentity(
    left: Pick<RuntimeArtifact, 'artifactHash' | 'compilerVersion' | 'documentId' | 'manifest' | 'manifestTypes' | 'revision' | 'signature'>,
    right: Pick<RuntimeArtifact, 'artifactHash' | 'compilerVersion' | 'documentId' | 'manifest' | 'manifestTypes' | 'revision' | 'signature'>
  ): Promise<boolean> {
    if (left.artifactHash !== right.artifactHash ||
        left.compilerVersion !== right.compilerVersion ||
        left.documentId !== right.documentId ||
        left.revision !== right.revision ||
        left.signature !== right.signature) {
      return false;
    }

    const [leftManifestHash, rightManifestHash] = await Promise.all([
      this.computeManifestHash(left),
      this.computeManifestHash(right)
    ]);
    return leftManifestHash === rightManifestHash;
  }

  public static async verify(input: RuntimeArtifactIntegrityInput): Promise<RuntimeDiagnostic | null> {
    if (!isRecord(input)) return invalidArtifactShapeDiagnostic();
    const artifact = input as Partial<RuntimeArtifact> & Record<string, unknown>;
    if (!isCanonicalSha256Hash(artifact.artifactHash)) {
      return {
        code: 'invalidArtifactHash',
        message: 'Runtime artifact hash must be a SHA-256 digest.',
        path: 'artifactHash',
        severity: 'error'
      };
    }
    if (!isCanonicalSignature(artifact.signature)) {
      return { code: 'invalidArtifactSignature', message: 'Runtime artifact signature is required.', path: 'signature', severity: 'error' };
    }
    if (artifact.compilerVersion !== LATEST_RUNTIME_COMPILER_REVISION) {
      return {
        code: 'unsupportedCompilerVersion',
        details: { compilerVersion: artifact.compilerVersion, expected: LATEST_RUNTIME_COMPILER_REVISION },
        message: 'Runtime artifact compilerVersion must match the latest runtime compiler revision.',
        path: 'compilerVersion',
        severity: 'error'
      };
    }
    const manifestDiagnostic = validateManifestDeclarations(artifact.manifest, artifact.manifestTypes);
    if (manifestDiagnostic) return manifestDiagnostic;

    if (!isRecord(artifact.integrityPayload) || Object.keys(artifact.integrityPayload).length === 0) {
      return {
        code: 'missingIntegrityPayload',
        message: 'Runtime artifact integrity payload is required.',
        path: 'integrityPayload',
        severity: 'error'
      };
    }
    const formalPayloadDiagnostic = validateFormalIntegrityPayload(artifact.integrityPayload);
    if (formalPayloadDiagnostic) return formalPayloadDiagnostic;
    if (containsEditorSessionState(artifact.integrityPayload)) {
      return {
        code: 'editorSessionInArtifact',
        message: 'Editor session state must not be included in a runtime artifact.',
        path: 'integrityPayload',
        severity: 'error'
      };
    }

    try {
      const actualHash = await this.computeHash(artifact.integrityPayload);
      if (actualHash.toLowerCase() !== artifact.artifactHash.trim().toLowerCase()) {
        return {
            code: 'artifactTampered',
            details: { expectedHash: artifact.artifactHash, actualHash },
            message: 'Runtime artifact integrity verification failed.',
            path: 'artifactHash',
            severity: 'error'
          };
      }
      const projectionDiagnostic = await this.verifyRuntimeProjection(artifact);
      if (projectionDiagnostic) return projectionDiagnostic;
      if (!this.isSignedArtifact(artifact)) {
        return {
          code: 'invalidArtifactShape',
          message: 'Runtime artifact failed formal shape validation.',
          path: 'artifact',
          severity: 'error'
        };
      }
      const manifestHash = await this.computeHash({ types: artifact.manifestTypes, declarations: artifact.manifest });
      const expectedSignature = await this.computeSignature(artifact);
      return expectedSignature.toLowerCase() === artifact.signature.trim().toLowerCase()
        ? null
        : {
          code: 'artifactSignatureInvalid',
          details: {
            actualSignature: artifact.signature,
            documentId: artifact.documentId,
            expectedSignature,
            manifestHash,
            manifestTypes: artifact.manifestTypes
          },
          message: 'Runtime artifact signature verification failed.',
          path: 'signature',
          severity: 'error'
        };
    } catch (error) {
      return {
        code: 'integrityUnavailable',
        details: { cause: error instanceof Error ? error.message : String(error) },
        message: 'Runtime artifact integrity verification is unavailable.',
        path: 'artifactHash',
        severity: 'error'
      };
    }
  }

  public static isSignedArtifact(input: RuntimeArtifactIntegrityInput): input is SignedRuntimeArtifact {
    if (!isRecord(input)) return false;
    const artifact = input as Partial<RuntimeArtifact> & Record<string, unknown>;
    return isCanonicalSha256Hash(artifact.artifactHash) &&
      artifact.compilerVersion === LATEST_RUNTIME_COMPILER_REVISION &&
      typeof artifact.documentId === 'string' &&
      typeof artifact.revision === 'number' && Number.isInteger(artifact.revision) && artifact.revision > 0 &&
      Array.isArray(artifact.actions) &&
      Array.isArray(artifact.bindings) &&
      isRecord(artifact.elements) &&
      isRecord(artifact.integrityPayload) &&
      Array.isArray(artifact.manifest) &&
      Array.isArray(artifact.manifestTypes) &&
      Array.isArray(artifact.pageMicroflows) &&
      Array.isArray(artifact.pageParameters) &&
      isRecord(artifact.permissions) &&
      isRecord(artifact.runtimeContext) &&
      isCanonicalSignature(artifact.signature) &&
      Array.isArray(artifact.variables);
  }

  private static async verifyRuntimeProjection(artifact: Partial<RuntimeArtifact> & Record<string, unknown>): Promise<RuntimeDiagnostic | null> {
    if (!isRecord(artifact.integrityPayload)) return null;
    const payload = artifact.integrityPayload;

    const projectionFields: Array<[keyof RuntimeArtifact, string]> = [
      ['actions', 'actions'],
      ['documentId', 'documentId'],
      ['elements', 'elements'],
      ['manifestTypes', 'manifestTypes'],
      ['pageMicroflows', 'pageMicroflows'],
      ['pageParameters', 'pageParameters'],
      ['permissions', 'permissions'],
      ['revision', 'revision'],
      ['runtimeContext', 'runtimeContext'],
      ['variables', 'variables']
    ];
    for (const [payloadField, path] of projectionFields) {
      const actualValue = artifact[payloadField];
      if (!Object.prototype.hasOwnProperty.call(artifact.integrityPayload, payloadField)) continue;
      const expectedHash = await this.computeHash(artifact.integrityPayload[payloadField]);
      const actualHash = await this.computeHash(actualValue);
      if (expectedHash.toLowerCase() !== actualHash.toLowerCase()) {
        return {
          code: 'artifactProjectionMismatch',
          details: { field: payloadField, expectedHash, actualHash },
          message: 'Runtime artifact derived fields do not match the signed integrity payload.',
          path,
          severity: 'error'
        };
      }
    }
    const bindingPayload = [payload.apiBindings, payload.dataSources, payload.workflowBindings].flat();
    const expectedBindingHash = await this.computeHash(bindingPayload);
    const actualBindingHash = await this.computeHash(artifact.bindings);
    if (expectedBindingHash.toLowerCase() !== actualBindingHash.toLowerCase()) {
      return {
        code: 'artifactProjectionMismatch',
        details: { expectedHash: expectedBindingHash, actualHash: actualBindingHash, field: 'bindings' },
        message: 'Runtime artifact derived bindings do not match the signed DesignerDocument payload.',
        path: 'bindings',
        severity: 'error'
      };
    }
    return null;
  }

  private static computeManifestHash(
    artifact: Pick<RuntimeArtifact, 'manifest' | 'manifestTypes'>
  ): Promise<string> {
    return this.computeHash({ types: artifact.manifestTypes, declarations: artifact.manifest });
  }
}

function validateManifestDeclarations(
  declarations: unknown,
  manifestTypes: unknown
): RuntimeDiagnostic | null {
  if (!Array.isArray(declarations) || declarations.length === 0) {
    return {
      code: 'missingManifestDeclarations',
      message: 'Runtime artifact manifest declarations are required and cannot be empty.',
      path: 'manifest',
      severity: 'error'
    };
  }

  const declarationTypes: string[] = [];
  for (const [index, declaration] of declarations.entries()) {
    if (!isRecord(declaration) || !isCanonicalNonEmptyString(declaration.type)) {
      return {
        code: 'invalidManifestDeclaration',
        message: 'Runtime artifact manifest declarations must contain a non-empty type.',
        path: `manifest.${index}`,
        severity: 'error'
      };
    }
    declarationTypes.push(declaration.type);
  }

  if (new Set(declarationTypes).size !== declarationTypes.length ||
      !Array.isArray(manifestTypes) ||
      manifestTypes.length !== declarationTypes.length ||
      manifestTypes.some((type) => !isCanonicalNonEmptyString(type) || !declarationTypes.includes(type))) {
    return {
      code: 'manifestDeclarationsMismatch',
      details: { declarationTypes, manifestTypes },
      message: 'Runtime artifact manifest declarations must exactly match manifestTypes.',
      path: 'manifest',
      severity: 'error'
    };
  }

  return null;
}

function canonicalJson(value: unknown): string {
  const serialized = JSON.stringify(canonicalize(value));
  if (serialized === undefined) throw new Error('Runtime artifact integrity payload must be JSON serializable.');
  return serialized;
}

function canonicalize(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonicalize);
  if (!isRecord(value)) return value;
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, canonicalize(value[key])]));
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}

function invalidArtifactShapeDiagnostic(): RuntimeDiagnostic {
  return {
    code: 'invalidArtifactShape',
    message: 'Runtime artifact must be an object.',
    path: 'artifact',
    severity: 'error'
  };
}

function validateFormalIntegrityPayload(payload: Record<string, unknown>): RuntimeDiagnostic | null {
  const requiredFields = [
    'actions',
    'apiBindings',
    'dataSources',
    'documentId',
    'elements',
    'pageMicroflows',
    'pageParameters',
    'permissions',
    'revision',
    'runtimeContext',
    'variables',
    'workflowBindings'
  ];
  const missingField = requiredFields.find((field) => !Object.prototype.hasOwnProperty.call(payload, field));
  if (missingField) {
    return {
      code: 'missingFormalField',
      details: { field: missingField },
      message: `Runtime artifact integrity payload is missing formal field: ${missingField}.`,
      path: `integrityPayload.${missingField}`,
      severity: 'error'
    };
  }

  const arrayFields = ['actions', 'apiBindings', 'dataSources', 'pageMicroflows', 'pageParameters', 'variables', 'workflowBindings'];
  const invalidArrayField = arrayFields.find((field) => !Array.isArray(payload[field]) || !(payload[field] as unknown[]).every(isRecord));
  if (invalidArrayField) return invalidFormalField(invalidArrayField, 'an array of objects');
  if (!isCanonicalNonEmptyString(payload.documentId)) return invalidFormalField('documentId', 'a canonical non-empty string');
  if (!Number.isInteger(payload.revision) || (payload.revision as number) < 1) return invalidFormalField('revision', 'a positive integer');

  const recordFields = ['elements', 'permissions', 'runtimeContext'];
  const invalidRecordField = recordFields.find((field) => !isRecord(payload[field]));
  if (invalidRecordField) return invalidFormalField(invalidRecordField, 'an object');
  return null;
}

function invalidFormalField(field: string, expected: string): RuntimeDiagnostic {
  return {
    code: 'invalidFormalField',
    details: { expected, field },
    message: `Runtime artifact integrity payload formal field ${field} must be ${expected}.`,
    path: `integrityPayload.${field}`,
    severity: 'error'
  };
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

function isCanonicalNonEmptyString(value: unknown): value is string {
  return isNonEmptyString(value) && value === value.trim();
}

function isCanonicalSha256Hash(value: unknown): value is string {
  return typeof value === 'string' && value === value.trim() && /^sha256:[0-9a-f]{64}$/i.test(value);
}

function isCanonicalSignature(value: unknown): value is string {
  return typeof value === 'string' && value === value.trim() && /^[0-9a-f]{64}$/i.test(value);
}

function containsEditorSessionState(payload: Record<string, unknown>): boolean {
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
  if (Object.keys(payload).some((key) => sessionKeys.has(key))) return true;
  return isRecord(payload.metadata) && Object.keys(payload.metadata).some((key) => sessionKeys.has(key));
}
