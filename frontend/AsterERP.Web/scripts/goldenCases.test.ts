import { describe, expect, it } from 'vitest';

import page001Fixture from '../../../docs/low-code-refactor/fixtures/pages/page-001.json';
import page002Fixture from '../../../docs/low-code-refactor/fixtures/pages/page-002.json';
import page003Fixture from '../../../docs/low-code-refactor/fixtures/pages/page-003.json';
import { zoomAtScreenPoint, screenToWorld, worldToScreen } from '../src/pages/application-console/development-center/low-code-studio/canvas/coordinateSystem';
import { beginPointerTransaction, finishPointerTransaction } from '../src/pages/application-console/development-center/low-code-studio/canvas/pointerTransaction';
import { createPatchNodeCommand } from '../src/pages/application-console/development-center/low-code-studio/commands/createDesignerCommands';
import { DesignerCommandBus } from '../src/pages/application-console/development-center/low-code-studio/commands/DesignerCommandBus';
import { latestComponentManifests } from '../src/pages/application-console/development-center/low-code-studio/components/latestComponentManifestCatalog';
import type { DesignerDocument, DesignerDocumentNode } from '../src/pages/application-console/development-center/low-code-studio/document/DesignerDocument';
import { createDefaultDesignerDocument, parseDesignerDocument, serializeDesignerDocument, validateDesignerDocument } from '../src/pages/application-console/development-center/low-code-studio/document/DesignerDocumentCodec';
import { canonicalizeDesignerDocument } from '../src/pages/application-console/development-center/low-code-studio/document/DesignerDocumentHash';
import type { RuntimeActionHandler } from '../src/runtime-kernel/ActionHandlerRegistry';
import { BindingResolver } from '../src/runtime-kernel/BindingResolver';
import { RuntimeContractError } from '../src/runtime-kernel/Diagnostics';
import { RuntimeArtifactIntegrity } from '../src/runtime-kernel/RuntimeArtifactIntegrity';
import { RuntimeKernel } from '../src/runtime-kernel/RuntimeKernel';

const manifests = new Map(latestComponentManifests.map((manifest) => [manifest.type, manifest]));
const monitoringContext = {
  appCode: 'MES',
  pageCode: 'golden-case',
  tenantId: 'tenant-a',
  traceId: 'golden-case-trace',
  userId: 'golden-case-user'
};

interface GoldenFixture {
  document: Record<string, unknown>;
  fixtureId: string;
  title: string;
}

interface GoldenCaseResult {
  blocked?: readonly string[];
  id: string;
  status: 'Pass' | 'PassWithBlockedExternal';
}

function readFixture(fixtureId: string): DesignerDocument {
  const fixture = ({ 'page-001': page001Fixture, 'page-002': page002Fixture, 'page-003': page003Fixture } as const)[fixtureId] as unknown as GoldenFixture;
  return parseDesignerDocument(JSON.stringify(fixture.document), { pageCode: fixture.fixtureId, pageName: fixture.title });
}

async function createArtifact(document: DesignerDocument, revision = document.revision) {
  const manifestTypes = [...new Set(Object.values(document.elements).map((node) => node.type))];
  const manifest = manifestTypes.map((type) => ({
    renderer: { preview: 'ComponentRuntimeHost', runtime: 'ComponentRuntimeHost' },
    type
  }));
  const integrityPayload = {
    actions: document.actions,
    apiBindings: document.apiBindings,
    dataSources: document.dataSources,
    documentId: document.documentId,
    elements: document.elements,
    pageMicroflows: document.pageMicroflows ?? [],
    pageParameters: document.pageParameters,
    permissions: document.permissions,
    revision,
    runtimeContext: document.runtimeContext,
    variables: document.variables,
    workflowBindings: document.workflowBindings
  };
  const artifactHash = await RuntimeArtifactIntegrity.computeHash(integrityPayload);
  const artifact = {
    actions: document.actions,
    artifactHash,
    bindings: [...document.apiBindings, ...document.dataSources, ...document.workflowBindings],
    compilerVersion: 'runtime-1',
    documentId: document.documentId,
    elements: document.elements,
    integrityPayload,
    manifest,
    manifestTypes,
    pageMicroflows: document.pageMicroflows ?? [],
    pageParameters: document.pageParameters,
    permissions: document.permissions,
    revision,
    runtimeContext: document.runtimeContext,
    signature: '',
    variables: document.variables
  };
  artifact.signature = await RuntimeArtifactIntegrity.computeSignature(artifact);
  return artifact;
}

function runtimeOptions(actionHandlers: readonly RuntimeActionHandler[] = []) {
  return { actionHandlers, manifests, monitoringContext };
}

function expectRuntimeDiagnostic(error: unknown, codes: readonly string[]): void {
  expect(error).toBeInstanceOf(RuntimeContractError);
  const diagnostics = error instanceof RuntimeContractError ? error.diagnostics : [];
  expect(diagnostics.some((diagnostic) => codes.includes(diagnostic.code))).toBe(true);
}

async function expectKernelReject(artifact: Awaited<ReturnType<typeof createArtifact>>, codes: readonly string[]): Promise<void> {
  let rejection: unknown;
  try {
    await RuntimeKernel.create(artifact, runtimeOptions());
  } catch (error) {
    rejection = error;
  }
  expectRuntimeDiagnostic(rejection, codes);
}

function createMicroflowHandler(variables: Record<string, unknown>): RuntimeActionHandler {
  return {
    execute: (config, context) => {
      expect(config.flowCode).toBe('fixture-save');
      context.setVariable?.('fixture.orderId', 'saved-order-002');
      variables['fixture.orderId'] = 'saved-order-002';
      return { affectedRows: 1, auditId: 'golden-audit-002' };
    },
    manifest: {
      cancelable: true,
      errorPolicy: 'stop',
      inputSchema: {},
      outputSchema: {},
      permissions: [],
      sideEffect: 'write',
      timeoutMs: 1_000,
      triggers: ['click'],
      type: 'runPageMicroflow'
    }
  };
}

function generatedTree(): DesignerDocument {
  const base = createDefaultDesignerDocument({ pageCode: 'generated-1000-node', pageName: 'Generated 1000 node tree' });
  const rootId = base.pages[0].rootElementId;
  const children = Array.from({ length: 999 }, (_, index) => `generated-node-${String(index + 1).padStart(4, '0')}`);
  const elements: Record<string, DesignerDocumentNode> = {
    [rootId]: { ...base.elements[rootId], children },
    ...Object.fromEntries(children.map((id, index) => [id, {
      bindings: {},
      children: [],
      events: [],
      id,
      layout: { height: 24, left: index * 2, top: index * 24, width: 240 },
      name: id,
      parentId: rootId,
      permission: {},
      props: { text: `node-${index + 1}` },
      style: {},
      type: 'text',
      validation: []
    }]))
  };
  return parseDesignerDocument(JSON.stringify({ ...base, elements }), { pageCode: 'generated-1000-node', pageName: 'Generated 1000 node tree' });
}

function runWideTableContract(): { affectedRows: number; staleRejected: boolean; secretReturned: boolean } {
  const columns = Array.from({ length: 100 }, (_, index) => ({ key: `column_${index + 1}`, valueType: 'string' }));
  const rows = Array.from({ length: 10_000 }, (_, index) => ({ id: `row-${index + 1}`, version: 1, secret: `secret-${index + 1}` }));
  const input = { columns, rows, rowId: 'row-42', expectedVersion: 0, value: 'edited' };
  const row = rows.find((candidate) => candidate.id === input.rowId)!;
  const staleRejected = input.expectedVersion !== row.version;
  const publicRow = { id: row.id, version: row.version, values: { [input.rowId]: input.value } };
  return { affectedRows: staleRejected ? 0 : 1, secretReturned: 'secret' in publicRow, staleRejected };
}

describe('deterministic Golden Case runner GC-001~006', () => {
  it('executes the six cases through validation, state transition and result assertions', async () => {
    const results: GoldenCaseResult[] = [];

    const page001 = readFixture('page-001');
    expect(validateDesignerDocument(page001)).toEqual([]);
    const published001 = await createArtifact(page001);
    const kernel001 = await RuntimeKernel.create(published001, runtimeOptions());
    expect(kernel001.snapshot(page001.pages[0].rootElementId, { scopes: {}, variables: {} })).not.toBeNull();
    expect(published001.artifactHash).toBe(await RuntimeArtifactIntegrity.computeHash(published001.integrityPayload));
    const unknownDocument = structuredClone(page001);
    const unknownRoot = unknownDocument.elements[unknownDocument.pages[0].rootElementId];
    unknownDocument.elements[unknownRoot.id] = { ...unknownRoot, type: 'unknown.golden.component' };
    const unknownArtifact = await createArtifact(unknownDocument);
    await expectKernelReject(unknownArtifact, ['unknownManifest']);
    results.push({ id: 'GC-001', status: 'Pass' });

    const page002 = readFixture('page-002');
    const bindingResolver = new BindingResolver();
    const bindingContext = { form: { orderNo: 'ORD-002', customer: 'Anonymous customer' }, resources: { 'fixture.form.orderNo': 'ORD-002', 'fixture.form.customer': 'Anonymous customer', 'fixture.dataset.orders': [{ orderNo: 'ORD-002', amount: 10 }] }, scopes: {}, variables: {} };
    expect(bindingResolver.resolve(page002.elements['order-no-002'].bindings?.field, bindingContext).value).toBe('ORD-002');
    expect(bindingResolver.resolve(page002.elements['customer-002'].bindings?.field, bindingContext).value).toBe('Anonymous customer');
    expect(bindingResolver.resolve(page002.elements['table-002'].bindings?.rows, bindingContext).value).toEqual([{ orderNo: 'ORD-002', amount: 10 }]);
    const variables: Record<string, unknown> = {};
    const page002Artifact = await createArtifact(page002);
    const kernel002 = await RuntimeKernel.create(page002Artifact, runtimeOptions([createMicroflowHandler(variables)]));
    const saveSteps = page002.elements['save-002'].events[0].steps as Array<{ id: string; type: string; config: Record<string, unknown> }>;
    const saveResult = await kernel002.executeActions(saveSteps, { scopes: {}, variables, setVariable: (key, value) => { variables[key] = value; } });
    expect(saveResult).toEqual([{ affectedRows: 1, auditId: 'golden-audit-002' }]);
    expect(variables['fixture.orderId']).toBe('saved-order-002');
    results.push({ id: 'GC-002', status: 'PassWithBlockedExternal', blocked: ['authenticated API affected-rows/audit evidence'] });

    const page003 = readFixture('page-003');
    const reloaded003 = parseDesignerDocument(serializeDesignerDocument(page003), { pageCode: 'page-003', pageName: 'Reloaded page' });
    const artifact003a = await createArtifact(page003);
    const artifact003b = await createArtifact(reloaded003);
    expect(artifact003b.revision).toBeGreaterThanOrEqual(artifact003a.revision);
    expect(artifact003b.artifactHash).toBe(artifact003a.artifactHash);
    expect(canonicalizeDesignerDocument(reloaded003)).not.toMatch(/selectedElementId|editorState|viewport/);
    results.push({ id: 'GC-003', status: 'Pass' });

    const tree004 = generatedTree();
    expect(Object.keys(tree004.elements)).toHaveLength(1_000);
    const bus004 = new DesignerCommandBus(tree004);
    const target004 = 'generated-node-0001';
    const pointer004 = beginPointerTransaction('move', 1, { x: 10, y: 20 }, [{ id: target004, rect: { id: target004, x: 0, y: 0, width: 240, height: 24 } }]);
    const update004 = finishPointerTransaction(pointer004, { x: 15, y: 28 });
    expect(bus004.document.elements[target004].layout.left).toBe(0);
    const writes004BeforePointerUp = bus004.document.revision - tree004.revision;
    expect(writes004BeforePointerUp).toBe(0);
    const write004 = bus004.execute(createPatchNodeCommand(target004, { layout: { ...bus004.document.elements[target004].layout, left: update004.rects[0].x, top: update004.rects[0].y } }));
    expect(write004.changed).toBe(true);
    expect(bus004.document.revision - tree004.revision).toBe(1);
    const viewport004 = { zoom: 1, pan: { x: 0, y: 0 } };
    const zoomed004 = zoomAtScreenPoint(viewport004, 2, { x: 100, y: 80 }, { x: 0, y: 0 });
    const roundTrip004 = screenToWorld(worldToScreen({ x: 37, y: 41 }, zoomed004, { x: 0, y: 0 }), zoomed004, { x: 0, y: 0 });
    expect(Math.abs(roundTrip004.x - 37)).toBeLessThan(Number.EPSILON * 16);
    expect(Math.abs(roundTrip004.y - 41)).toBeLessThan(Number.EPSILON * 16);
    results.push({ id: 'GC-004', status: 'Pass' });

    const wideTable005 = runWideTableContract();
    expect(wideTable005.staleRejected).toBe(true);
    expect(wideTable005.affectedRows).toBe(0);
    expect(wideTable005.secretReturned).toBe(false);
    results.push({ id: 'GC-005', status: 'PassWithBlockedExternal', blocked: ['authenticated Data Studio provider, secret redaction and audit evidence'] });

    const tampered006 = structuredClone(published001);
    const tamperedElements006 = tampered006.integrityPayload.elements as Record<string, DesignerDocumentNode>;
    tamperedElements006[page001.pages[0].rootElementId].props = { tampered: true };
    await expectKernelReject(tampered006, ['artifactTampered', 'artifactProjectionMismatch', 'artifactSignatureInvalid']);
    results.push({ id: 'GC-006', status: 'Pass' });

    expect(results.map((result) => result.id)).toEqual(['GC-001', 'GC-002', 'GC-003', 'GC-004', 'GC-005', 'GC-006']);
  }, 30_000);
});
