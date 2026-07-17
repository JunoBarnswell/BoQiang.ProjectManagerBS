import { describe, expect, it } from 'vitest';

import { BindingResolver } from '../../../../../../runtime-kernel/BindingResolver';
import { LayoutResolver } from '../../../../../../runtime-kernel/LayoutResolver';
import { RuntimeArtifactIntegrity } from '../../../../../../runtime-kernel/RuntimeArtifactIntegrity';
import { RuntimeKernel } from '../../../../../../runtime-kernel/RuntimeKernel';
import type { ComponentManifest } from '../../components/ComponentManifest';
import type { DesignerDocument, DesignerDocumentNode } from '../../document/DesignerDocument';
import { canonicalizeDesignerDocument, computeDesignerDocumentHash } from '../../document/DesignerDocumentHash';
import type { RuntimeArtifact } from '../../document/RuntimeArtifact';

const textManifest: ComponentManifest = {
  binding: { acceptedSources: ['page', 'variables'], acceptedTypes: ['string', 'number'], supportsConversion: true },
  capability: { acceptsChildren: true, capabilities: ['valueBinding'] },
  defaults: { layout: {}, props: {}, style: {} },
  editor: { inspectorSections: ['content'], previewRenderer: 'text', selectionMode: 'single' },
  events: [],
  i18n: { diagnosticKey: 'offline.test.component.diagnostic', helpKey: 'offline.test.component.help', labelKey: 'offline.test.component.label' },
  migrations: [],
  responsive: { supportedLayouts: ['block'], supportsOverrides: true },
  runtime: { renderer: 'text', supportedScopes: ['page', 'variables'] },
  security: { actionPermissions: [], requiresPermission: false },
  type: 'text.paragraph',
  validation: { schema: {}, supportsDiagnostics: true }
};

const actionManifest = {
  cancelable: true,
  errorPolicy: 'stop' as const,
  inputSchema: {},
  outputSchema: {},
  permissions: [],
  sideEffect: 'none' as const,
  timeoutMs: 1_000,
  triggers: ['click'],
  type: 'setVariable'
};

const monitoringContext = {
  appCode: 'MES',
  pageCode: 'offline-parity',
  tenantId: 'tenant-a',
  traceId: 'trace-offline-parity',
  userId: 'user-a'
};

describe('offline low-code parity regression', () => {
  it('keeps preview and published runtime equal for props, bindings, layout, and actions', async () => {
    const document = createParityDocument();
    const canonical = canonicalizeDesignerDocument(document);
    const documentHash = computeDesignerDocumentHash(document);
    const artifact = await createArtifact(document);
    const previewKernel = await createKernel(artifact);
    const runtimeKernel = await createKernel(await createArtifact(document));

    expect(documentHash).toMatch(/^sha256:[0-9a-f]{64}$/);
    expect(canonical).toBe(canonicalizeDesignerDocument(document));
    expect(artifact.integrityPayload).toEqual(JSON.parse(canonicalArtifactPayload(document)));
    expect(artifact.artifactHash).toBe(await RuntimeArtifactIntegrity.computeHash(artifact.integrityPayload));
    expect(artifact.artifactHash).toBe((await createArtifact(document)).artifactHash);
    expect(artifact.manifest).toEqual((await createArtifact(document)).manifest);

    const state = { scopes: { page: { customer: { name: 'Ada' } } }, variables: {} };
    const preview = previewProjection(document.elements.root, state);
    const runtime = runtimeKernel.snapshot('root', state, { breakpoint: 'desktop', viewport: { width: 1280, height: 720 } });

    expect(runtime).not.toBeNull();
    expect(runtime).toMatchObject({
      id: preview.id,
      type: preview.type,
      props: preview.props,
      layout: preview.layout,
      children: preview.children
    });

    const previewActionResult = await executeAction(previewKernel, document.elements.root.events[0]);
    const runtimeActionResult = await runtimeKernel.executeActions(
      document.elements.root.events[0].steps as { id: string; type: string; config?: Record<string, unknown> }[],
      { scopes: state.scopes }
    );
    expect(runtimeActionResult).toEqual([previewActionResult]);
  });

  it.each([
    ['unknown component', async () => RuntimeKernel.create(await createArtifact(createParityDocument({ type: 'unknown.component' })), { manifests: new Map(), monitoringContext }), 'unknownManifest'],
    ['unknown action', async () => RuntimeKernel.create(await createArtifact(createParityDocument({ actionType: 'missing.action' })), { manifests: new Map([[textManifest.type, textManifest]]), monitoringContext }), 'unknownAction'],
    ['unknown binding', async () => RuntimeKernel.create(await createArtifact(createParityDocument({ bindingSource: 'legacy' })), { manifests: new Map([[textManifest.type, textManifest]]), monitoringContext }), 'invalidBinding']
  ])('fails closed for %s', async (_caseName, createKernel, diagnosticCode) => {
    await expect(createKernel()).rejects.toMatchObject({
      diagnostics: expect.arrayContaining([expect.objectContaining({ code: diagnosticCode })])
    });
  });

  it('rejects a tampered artifact before RuntimeKernel validation or rendering', async () => {
    const valid = await createArtifact(createParityDocument());
    const tampered = {
      ...valid,
      integrityPayload: {
        ...(valid.integrityPayload as Record<string, unknown>),
        elements: { ...valid.elements, root: { ...valid.elements.root, props: { value: 'tampered' } } }
      }
    };

    await expect(RuntimeKernel.create(tampered, { actionHandlers: [{ manifest: actionManifest, execute: () => undefined }], manifests: new Map([[textManifest.type, textManifest]]), monitoringContext }))
      .rejects.toMatchObject({ diagnostics: [expect.objectContaining({ code: 'artifactTampered' })] });
  });
});

function createParityDocument(options: { actionType?: string; bindingSource?: string; type?: string } = {}): DesignerDocument {
  const root: DesignerDocumentNode = {
    children: ['child'],
    events: [{ id: 'action-1', trigger: 'click', steps: [{ id: 'step-1', type: options.actionType ?? 'setVariable', config: { target: 'lastAction', value: 'saved' } }] }],
    id: 'root',
    layout: { display: 'block', width: '100%' },
    parentId: null,
    props: { value: options.bindingSource === 'legacy' ? { path: 'customer.name', source: 'page' } : { kind: 'expression', expectedType: 'string', graph: { root: { kind: 'literal', value: 'Ada', valueType: 'string' } } } },
    responsiveOverrides: { desktop: { layout: { width: '960px' }, style: { minHeight: 240 } } },
    style: { color: '#123456' },
    type: options.type ?? textManifest.type
  };
  const child: DesignerDocumentNode = { children: [], events: [], id: 'child', layout: {}, parentId: 'root', props: { value: 'child' }, type: textManifest.type };
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'offline-parity', elements: { root, child }, metadata: {},
    modals: [], pageParameters: [], pages: [{ id: 'page-1', name: 'Parity', rootElementId: 'root' }], permissions: {},
    runtimeContext: {}, revision: 7, styleTokens: {}, variables: [], workflowBindings: []
  };
}

function previewProjection(node: DesignerDocumentNode, state: { scopes: Record<string, Record<string, unknown>>; variables: Record<string, unknown> }) {
  return {
    children: [...node.children],
    id: node.id,
    layout: new LayoutResolver().resolve(node, { breakpoint: 'desktop', viewport: { width: 1280, height: 720 } }),
    props: new BindingResolver().resolveRecord(node.props, {
      page: state.scopes.page,
      scopes: state.scopes,
      variables: state.variables
    }),
    type: node.type
  };
}

async function createArtifact(document: DesignerDocument): Promise<RuntimeArtifact & { integrityPayload: unknown }> {
  const integrityPayload = JSON.parse(canonicalArtifactPayload(document));
  const artifact = {
    actions: document.actions,
    artifactHash: await RuntimeArtifactIntegrity.computeHash(integrityPayload),
    bindings: [...document.apiBindings, ...document.dataSources, ...document.workflowBindings],
    compilerVersion: 'runtime-1',
    documentId: document.documentId,
    elements: document.elements,
    integrityPayload,
    manifest: [...new Set(Object.values(document.elements).map((node) => node.type))].map((type) => ({
      renderer: { preview: 'ComponentRuntimeHost', runtime: 'ComponentRuntimeHost' },
      type
    })),
    manifestTypes: [...new Set(Object.values(document.elements).map((node) => node.type))],
    pageMicroflows: [], pageParameters: [], permissions: {}, revision: document.revision, runtimeContext: {}, variables: [], signature: ''
  } as RuntimeArtifact & { integrityPayload: unknown };
  artifact.signature = await RuntimeArtifactIntegrity.computeSignature(artifact);
  return artifact;
}

async function createKernel(artifact: RuntimeArtifact & { integrityPayload: unknown }): Promise<RuntimeKernel> {
  return RuntimeKernel.create(artifact, {
    actionHandlers: [{ manifest: actionManifest, execute: (config, context) => {
      const target = String(config.target ?? '');
      const value = config.value;
      context.setVariable?.(target, value);
      return { target, value };
    } }],
    manifests: new Map([[textManifest.type, textManifest]]),
    monitoringContext
  });
}

function canonicalArtifactPayload(document: DesignerDocument): string {
  return JSON.stringify({
    actions: document.actions,
    apiBindings: document.apiBindings,
    dataSources: document.dataSources,
    documentId: document.documentId,
    elements: document.elements,
    manifestTypes: [...new Set(Object.values(document.elements).map((node) => node.type))],
    pageMicroflows: document.pageMicroflows ?? [],
    pageParameters: document.pageParameters,
    permissions: document.permissions,
    revision: document.revision,
    runtimeContext: document.runtimeContext,
    variables: document.variables,
    workflowBindings: document.workflowBindings
  }, (_key, value) => value, 0);
}

async function executeAction(kernel: RuntimeKernel, action: Record<string, unknown>): Promise<unknown> {
  return (await kernel.executeActions(action.steps as { id: string; type: string; config?: Record<string, unknown> }[], { scopes: {} }))[0];
}
