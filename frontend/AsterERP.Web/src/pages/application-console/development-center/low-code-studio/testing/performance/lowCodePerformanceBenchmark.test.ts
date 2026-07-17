import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { performance } from 'node:perf_hooks';

import { describe, expect, it } from 'vitest';

import { parseRuntimePageArtifact, toRuntimeArtifact } from '../../../../../../runtime-kernel/RuntimeArtifactCodec';
import { RuntimeArtifactIntegrity , LATEST_RUNTIME_COMPILER_REVISION } from '../../../../../../runtime-kernel/RuntimeArtifactIntegrity';
import { RuntimeKernel } from '../../../../../../runtime-kernel/RuntimeKernel';
import { createPatchNodeCommand } from '../../commands/createDesignerCommands';
import { DesignerCommandBus } from '../../commands/DesignerCommandBus';
import type { ComponentManifest } from '../../components/ComponentManifest';
import type { DesignerDocument, DesignerDocumentNode } from '../../document/DesignerDocument';
import { parseDesignerDocument, validateDesignerDocument } from '../../document/DesignerDocumentCodec';
import { serializeDesignerDocument } from '../../document/DesignerDocumentHash';
import type { RuntimeArtifact } from '../../document/RuntimeArtifact';

const NODE_COUNTS = [100, 500, 1000, 2000] as const;
const SAMPLE_COUNT = 5;
const WARMUP_COUNT = 1;
const BUDGETS = {
  saveP95Ms: { 100: 100, 500: 250, 1000: 500, 2000: 1000 },
  undoP95Ms: { 100: 100, 500: 250, 1000: 500, 2000: 1000 },
  runtimeFirstScreenP95Ms: { 100: 100, 500: 250, 1000: 500, 2000: 1000 }
} as const;

const manifest: ComponentManifest = {
  binding: { acceptedSources: ['variables'], acceptedTypes: ['string'], supportsConversion: true },
  capability: { acceptsChildren: true, capabilities: [] },
  defaults: { layout: {}, props: {}, style: {} },
  editor: { inspectorSections: ['content'], previewRenderer: 'text', selectionMode: 'single' },
  events: [],
  i18n: { diagnosticKey: 'performance.test.component.diagnostic', helpKey: 'performance.test.component.help', labelKey: 'performance.test.component.label' },
  migrations: [],
  responsive: { supportedLayouts: ['block'], supportsOverrides: true },
  runtime: { renderer: 'text', supportedScopes: ['page'] },
  security: { actionPermissions: [], requiresPermission: false },
  type: 'text.paragraph',
  validation: { schema: {}, supportsDiagnostics: true }
};

const monitoringContext = {
  appCode: 'MES',
  pageCode: 'performance-benchmark',
  tenantId: 'tenant-a',
  traceId: 'trace-performance-benchmark',
  userId: 'user-a'
};

type BenchmarkSample = {
  nodeCount: number;
  loadMs: number[];
  firstInteractiveMs: number[];
  selectMs: number[];
  editMs: number[];
  serializeMs: number[];
  saveMs: number[];
  undoMs: number[];
  compileMs: number[];
  runtimeFirstScreenMs: number[];
  runtimeArtifactBytes: number[];
  peakWorkingSetBytes: number;
};

describe('Low-code performance benchmark', () => {
  it('measures the real document store and runtime kernel at every required node count', async () => {
    const samples: BenchmarkSample[] = [];

    for (const nodeCount of NODE_COUNTS) {
      const document = createDocument(nodeCount);
      const source = serializeDesignerDocument(document);
      const loadMs: number[] = [];
      const firstInteractiveMs: number[] = [];
      const selectMs: number[] = [];
      const editMs: number[] = [];
      const serializeMs: number[] = [];
      const saveMs: number[] = [];
      const undoMs: number[] = [];
      const compileMs: number[] = [];
      const runtimeFirstScreenMs: number[] = [];
      const runtimeArtifactBytes: number[] = [];

      for (let run = 0; run < WARMUP_COUNT + SAMPLE_COUNT; run += 1) {
        const loadStartedAt = performance.now();
        const loaded = parseDesignerDocument(source, { pageCode: document.documentId, pageName: 'Performance' });
        const loadElapsed = performance.now() - loadStartedAt;

        const interactiveStartedAt = performance.now();
        expect(validateDesignerDocument(loaded)).toEqual([]);
        const interactiveElapsed = performance.now() - interactiveStartedAt;

        const selectStartedAt = performance.now();
        const selectedId = Object.keys(loaded.elements).at(-1);
        expect(selectedId ? loaded.elements[selectedId] : undefined).toBeDefined();
        const selectElapsed = performance.now() - selectStartedAt;

        const commandBus = new DesignerCommandBus(loaded);
        const editStartedAt = performance.now();
        commandBus.execute(createPatchNodeCommand('node-1', { props: { value: `saved-${run}` } }));
        const editElapsed = performance.now() - editStartedAt;

        const saveStartedAt = performance.now();
        const serialized = serializeDesignerDocument(commandBus.document);
        expect(serialized.length).toBeGreaterThan(0);
        const saveElapsed = performance.now() - saveStartedAt;

        const serializeStartedAt = performance.now();
        const serializedAgain = serializeDesignerDocument(commandBus.document);
        expect(serializedAgain).toBe(serialized);
        const serializeElapsed = performance.now() - serializeStartedAt;

        const undoStartedAt = performance.now();
        const undo = commandBus.undo();
        expect(undo?.changed).toBe(true);
        expect(commandBus.document.elements['node-1']?.props.value).toBe('value-1');
        const redo = commandBus.redo();
        expect(redo?.changed).toBe(true);
        expect(commandBus.document.elements['node-1']?.props.value).toBe(`saved-${run}`);
        const undoElapsed = performance.now() - undoStartedAt;

        const compileStartedAt = performance.now();
        const artifact = await createRuntimeArtifact(commandBus.document);
        const artifactJson = serializeRuntimeArtifactEnvelope(artifact);
        const parsedEnvelope = parseRuntimePageArtifact(artifactJson);
        expect(parsedEnvelope).not.toBeNull();
        const compiledArtifact = parsedEnvelope ? toRuntimeArtifact(parsedEnvelope) : null;
        expect(compiledArtifact).not.toBeNull();
        const compileElapsed = performance.now() - compileStartedAt;

        const runtimeStartedAt = performance.now();
        const kernel = await RuntimeKernel.create(compiledArtifact!, { manifests: new Map([[manifest.type, manifest]]), monitoringContext });
        const snapshot = kernel.snapshot('root', { scopes: {}, variables: {} });
        expect(snapshot?.id).toBe('root');
        const runtimeElapsed = performance.now() - runtimeStartedAt;

        if (run >= WARMUP_COUNT) {
          loadMs.push(loadElapsed);
          firstInteractiveMs.push(interactiveElapsed);
          selectMs.push(selectElapsed);
          editMs.push(editElapsed);
          serializeMs.push(serializeElapsed);
          saveMs.push(saveElapsed);
          undoMs.push(undoElapsed);
          compileMs.push(compileElapsed);
          runtimeFirstScreenMs.push(runtimeElapsed);
          runtimeArtifactBytes.push(new TextEncoder().encode(artifactJson).byteLength);
        }
      }

      const result = {
        nodeCount,
        loadMs,
        firstInteractiveMs,
        selectMs,
        editMs,
        serializeMs,
        saveMs,
        undoMs,
        runtimeFirstScreenMs,
        compileMs,
        runtimeArtifactBytes,
        failureRate: 0,
        statistics: {
          loadMs: statistics(loadMs),
          firstInteractiveMs: statistics(firstInteractiveMs),
          selectMs: statistics(selectMs),
          editMs: statistics(editMs),
          serializeMs: statistics(serializeMs),
          saveMs: statistics(saveMs),
          undoMs: statistics(undoMs),
          compileMs: statistics(compileMs),
          runtimeFirstScreenMs: statistics(runtimeFirstScreenMs),
          runtimeArtifactBytes: statistics(runtimeArtifactBytes)
        },
        loadP95Ms: percentile(loadMs, 0.95),
        firstInteractiveP95Ms: percentile(firstInteractiveMs, 0.95),
        selectP95Ms: percentile(selectMs, 0.95),
        editP95Ms: percentile(editMs, 0.95),
        serializeP95Ms: percentile(serializeMs, 0.95),
        saveP95Ms: percentile(saveMs, 0.95),
        undoP95Ms: percentile(undoMs, 0.95),
        compileP95Ms: percentile(compileMs, 0.95),
        runtimeFirstScreenP95Ms: percentile(runtimeFirstScreenMs, 0.95),
        runtimeArtifactBytesP95: percentile(runtimeArtifactBytes, 0.95),
        peakWorkingSetBytes: process.memoryUsage().heapUsed,
        rawCommand: 'npm run test -- --run src/pages/application-console/development-center/low-code-studio/testing/performance/lowCodePerformanceBenchmark.test.ts',
        capturedAt: new Date().toISOString()
      };
      samples.push(result);

      expect(result.saveP95Ms).toBeLessThanOrEqual(BUDGETS.saveP95Ms[nodeCount]);
      expect(result.undoP95Ms).toBeLessThanOrEqual(BUDGETS.undoP95Ms[nodeCount]);
      expect(result.runtimeFirstScreenP95Ms).toBeLessThanOrEqual(BUDGETS.runtimeFirstScreenP95Ms[nodeCount]);
    }

    const outputPath = process.env.LOW_CODE_PERFORMANCE_OUTPUT;
    if (outputPath) {
      mkdirSync(dirname(outputPath), { recursive: true });
      writeFileSync(outputPath, JSON.stringify({
        format: 'astererp.low-code.performance.evidence',
        status: 'Measured',
        sampleCount: SAMPLE_COUNT,
        warmupCount: WARMUP_COUNT,
        capturedAt: new Date().toISOString(),
        scenarios: samples
      }, null, 2));
    }
    process.stdout.write(`${JSON.stringify({ status: 'Measured', scenarios: samples })}\n`);
  }, 120_000);
});

function createDocument(nodeCount: number): DesignerDocument {
  const elements: Record<string, DesignerDocumentNode> = {
    root: node('root', null, [])
  };
  for (let index = 1; index < nodeCount; index += 1) {
    const parentId = index <= 2 ? 'root' : `node-${Math.floor(index / 2)}`;
    const id = `node-${index}`;
    elements[id] = node(id, parentId, []);
    elements[parentId].children.push(id);
  }
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: `performance-${nodeCount}`, elements,
    metadata: {}, modals: [], pageParameters: [], pages: [{ id: 'page-1', name: 'Performance', rootElementId: 'root' }], pageType: 'standard',
    permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
  };
}

function node(id: string, parentId: string | null, children: string[]): DesignerDocumentNode {
  return { children, events: [], id, layout: { display: 'block' }, parentId, props: { value: `value-${id.replace('node-', '')}` }, type: 'text.paragraph' };
}

async function createRuntimeArtifact(document: DesignerDocument): Promise<RuntimeArtifact & { integrityPayload: unknown }> {
  const integrityPayload = {
    actions: document.actions,
    apiBindings: document.apiBindings,
    dataSources: document.dataSources,
    documentId: document.documentId,
    elements: document.elements,
    manifestTypes: [manifest.type],
    pageMicroflows: document.pageMicroflows ?? [],
    pageParameters: document.pageParameters,
    permissions: document.permissions,
    revision: document.revision,
    runtimeContext: document.runtimeContext,
    variables: document.variables,
    workflowBindings: document.workflowBindings
  };
  const artifact = {
    actions: [], artifactHash: await RuntimeArtifactIntegrity.computeHash(integrityPayload), bindings: [], compilerVersion: LATEST_RUNTIME_COMPILER_REVISION,
    documentId: document.documentId, elements: document.elements, integrityPayload, manifestTypes: [manifest.type],
    manifest: [{ renderer: { preview: 'ComponentRuntimeHost', runtime: 'ComponentRuntimeHost' }, type: manifest.type }],
    pageMicroflows: [], pageParameters: [], permissions: {}, revision: document.revision, runtimeContext: {},
    signature: '', variables: []
  } as RuntimeArtifact & { integrityPayload: unknown };
  artifact.signature = await RuntimeArtifactIntegrity.computeSignature(artifact);
  return artifact;
}

function serializeRuntimeArtifactEnvelope(artifact: RuntimeArtifact & { integrityPayload: unknown }): string {
  return JSON.stringify({
    artifactHash: artifact.artifactHash,
    compilerVersion: artifact.compilerVersion,
    document: artifact.integrityPayload,
    id: artifact.documentId,
    manifest: artifact.manifest,
    manifestTypes: artifact.manifestTypes,
    revision: artifact.revision,
    signature: artifact.signature
  });
}

function percentile(values: readonly number[], quantile: number): number {
  const sorted = [...values].sort((left, right) => left - right);
  const index = Math.min(sorted.length - 1, Math.ceil(sorted.length * quantile) - 1);
  return Number(sorted[index].toFixed(3));
}

function statistics(values: readonly number[]): { p50: number; p95: number; p99: number } {
  return {
    p50: percentile(values, 0.5),
    p95: percentile(values, 0.95),
    p99: percentile(values, 0.99)
  };
}
