import type { DesignerDocumentNode } from './DesignerDocument';

export interface RuntimeArtifactPageMicroflow {
  action: string;
  alias: string;
  bindingId?: string | null;
  errorPolicy?: string;
  flowCode: string;
  inputMappings?: Record<string, unknown>[];
  outputMappings?: Record<string, unknown>[];
  refreshOnChangePaths?: string[];
  timeoutMs?: number | null;
  trigger?: string;
}

export interface RuntimeArtifact {
  actions: Record<string, unknown>[];
  artifactHash: string;
  bindings: Record<string, unknown>[];
  compilerVersion: string;
  documentId: string;
  elements: Record<string, DesignerDocumentNode>;
  /** Canonical document payload covered by artifactHash and signature. */
  integrityPayload: Record<string, unknown>;
  manifest: RuntimeArtifactManifestDeclaration[];
  manifestTypes: string[];
  pages?: Array<{ id: string; name: string; rootElementId: string }>;
  pageMicroflows: RuntimeArtifactPageMicroflow[];
  pageParameters: Record<string, unknown>[];
  permissions: Record<string, unknown>;
  revision: number;
  runtimeContext: Record<string, unknown>;
  /** Formal runtime artifacts are always signed; migration drafts use a separate type. */
  signature: string;
  variables: Record<string, unknown>[];
}

export interface RuntimeArtifactManifestDeclaration {
  readonly type: string;
  readonly [key: string]: unknown;
}

/** Untrusted transport shape accepted only before RuntimeArtifactIntegrity validation. */
export type RuntimeArtifactDraft = Partial<Omit<RuntimeArtifact, 'integrityPayload' | 'manifest' | 'signature'>> & {
  integrityPayload?: unknown;
  manifest?: unknown;
  signature?: unknown;
};
