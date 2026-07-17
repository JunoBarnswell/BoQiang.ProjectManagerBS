import { describe, expect, it } from 'vitest';

import type { ComponentManifest } from '../pages/application-console/development-center/low-code-studio/components/ComponentManifest';
import type { RuntimeArtifact, RuntimeArtifactManifestDeclaration } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';

import { RuntimeContractError } from './Diagnostics';
import { RUNTIME_CAPABILITY_CONTRACT } from './runtime-contract/RuntimeCapabilityContract';
import { RuntimeArtifactIntegrity } from './RuntimeArtifactIntegrity';
import { RuntimeKernel as RuntimeKernelImplementation, type RuntimeKernelOptions } from './RuntimeKernel';
import { validateRuntimeMonitoringEvent } from './RuntimeMonitoringContract';

type TestRuntimeArtifact = RuntimeArtifact;
type TestArtifactOverrides = Partial<TestRuntimeArtifact> & { manifest?: RuntimeArtifactManifestDeclaration[] };
type TestKernelOptions = Omit<RuntimeKernelOptions, 'monitoringContext'> & { monitoringContext?: RuntimeKernelOptions['monitoringContext'] };

const defaultMonitoringContext = {
  appCode: 'MES',
  pageCode: 'runtime-test',
  tenantId: 'tenant-a',
  traceId: 'trace-runtime-test',
  userId: 'user-a'
};

const RuntimeKernel = {
  create: (runtimeArtifact: unknown, options: TestKernelOptions) =>
    RuntimeKernelImplementation.create(runtimeArtifact as TestRuntimeArtifact, {
      ...options,
      monitoringContext: options.monitoringContext ?? defaultMonitoringContext
    })
};

const manifest: ComponentManifest = {
  binding: { acceptedSources: ['variables'], acceptedTypes: ['string'], supportsConversion: true },
  capability: { acceptsChildren: false, capabilities: ['valueBinding'] },
  defaults: { layout: {}, props: {}, style: {} },
  editor: { inspectorSections: ['content'], previewRenderer: 'text', selectionMode: 'single' },
  events: [],
  i18n: { diagnosticKey: 'runtime.test.component.diagnostic', helpKey: 'runtime.test.component.help', labelKey: 'runtime.test.component.label' },
  migrations: [],
  responsive: { supportedLayouts: ['block'], supportsOverrides: true },
  runtime: { renderer: 'text', supportedScopes: ['page'] },
  security: { actionPermissions: [], requiresPermission: false },
  type: 'text.paragraph',
  validation: { schema: {}, supportsDiagnostics: true }
};

async function artifact(overrides: TestArtifactOverrides = {}): Promise<TestRuntimeArtifact> {
  const manifestTypes = overrides.manifestTypes ?? ['text.paragraph'];
  const base: TestRuntimeArtifact = {
    actions: [],
    artifactHash: '',
    signature: '',
    bindings: [],
    compilerVersion: RUNTIME_CAPABILITY_CONTRACT.compilerRevision,
    documentId: 'document-1',
    elements: {
      root: { children: ['child'], events: [], id: 'root', layout: { display: 'block' }, parentId: null, props: { value: { resourceId: 'customer.name', valueType: 'string', displayName: 'Name' } }, type: 'text.paragraph' },
      child: { children: [], events: [], id: 'child', layout: {}, parentId: 'root', props: {}, type: 'text.paragraph' }
    },
    manifestTypes: ['text.paragraph'],
    pageMicroflows: [],
    pageParameters: [],
    permissions: {},
    revision: 1,
    runtimeContext: {},
    integrityPayload: {},
    manifest: manifestTypes.map((type) => ({ type, renderer: { runtime: 'ComponentRuntimeHost', preview: 'ComponentRuntimeHost' } })),
    variables: [],
    ...overrides,
  };
  const integrityPayload = overrides.integrityPayload ?? {
    actions: base.actions,
    apiBindings: [],
    dataSources: [],
    documentId: base.documentId,
    elements: base.elements,
    manifestTypes: base.manifestTypes,
    pageMicroflows: base.pageMicroflows,
    pageParameters: base.pageParameters,
    permissions: base.permissions,
    revision: base.revision,
    runtimeContext: base.runtimeContext,
    variables: base.variables,
    workflowBindings: []
  };
  const signedArtifact = {
    ...base,
    manifest: overrides.manifest ?? base.manifest,
    integrityPayload,
    artifactHash: overrides.artifactHash ?? await RuntimeArtifactIntegrity.computeHash(integrityPayload)
  } as TestRuntimeArtifact;
  signedArtifact.signature = Object.prototype.hasOwnProperty.call(overrides, 'signature')
    ? overrides.signature ?? ''
    : await RuntimeArtifactIntegrity.computeSignature(signedArtifact);
  return signedArtifact;
}

describe('RuntimeKernel', () => {
  it('resolves canonical ResourceRef bindings in the Kernel snapshot consumed by renderers', async () => {
    const runtimeArtifact = await artifact({
      elements: {
        root: {
          children: ['child'],
          events: [],
          id: 'root',
          layout: { display: 'block' },
          bindings: { data: { displayName: 'Orders', resourceId: 'page::orders', valueType: 'array' } },
          parentId: null,
          props: {},
          type: 'text.paragraph'
        },
        child: { children: [], events: [], id: 'child', layout: {}, parentId: 'root', props: {}, type: 'text.paragraph' }
      }
    });
    const kernel = await RuntimeKernel.create(runtimeArtifact, { manifests: new Map([[manifest.type, manifest]]) });

    expect(kernel.snapshot('root', { resources: { 'page::orders': [{ id: 'order-1' }] }, scopes: { page: {} }, variables: {} })?.bindings).toEqual({ data: [{ id: 'order-1' }] });
  });

  it('accepts only revisioned artifacts with registered manifest types and resolves a snapshot', () => {
    return artifact().then((runtimeArtifact) => RuntimeKernel.create(runtimeArtifact, { manifests: new Map([[manifest.type, manifest]]) }).then((kernel) => {
    const snapshot = kernel.snapshot('root', { resources: { 'customer.name': 'Ada' }, scopes: {}, variables: {} });
    expect(snapshot?.props.value).toBe('Ada');
    expect(kernel.affectedNodes('customer.name')).toEqual(['root']);
    expect(kernel.diagnostics.artifact).toMatchObject({ documentId: 'document-1', revision: 1 });
    expect(kernel.diagnostics.metrics.artifactLoadSuccesses).toBe(1);
    expect(kernel.diagnostics.metrics.artifactLoadDurationMs).toBeGreaterThanOrEqual(0);
    expect(kernel.diagnostics.monitoringEvents.every((event) => validateRuntimeMonitoringEvent(event).valid)).toBe(true);
    expect(kernel.diagnostics.monitoringEvents[0]?.context).toMatchObject({
      appCode: 'MES',
      artifactHash: runtimeArtifact.artifactHash,
      documentId: 'document-1',
      pageCode: 'runtime-test',
      revision: 1,
      tenantId: 'tenant-a',
      traceId: 'trace-runtime-test',
      userId: 'user-a'
    });
    })
  );
  });

  it('fails closed when the runtime host omits monitoring identity context', async () => {
    const valid = await artifact();
    await expect(RuntimeKernelImplementation.create(valid, { manifests: new Map([[manifest.type, manifest]]), monitoringContext: {} }))
      .rejects.toMatchObject({ diagnostics: [{ code: 'missingMonitoringContext' }] });
  });

  it('uses canonical JSON for cross-runtime integrity hashes', async () => {
    const first = await RuntimeArtifactIntegrity.computeHash({ z: 1, nested: { b: true, a: 'x' }, a: 2 });
    const second = await RuntimeArtifactIntegrity.computeHash({ a: 2, nested: { a: 'x', b: true }, z: 1 });
    expect(first).toBe(second);
  });

  it('blocks unknown manifest types before rendering', () => {
    return expect(artifact({ manifestTypes: ['unknown'], manifest: [{ type: 'unknown' }] }).then((runtimeArtifact) => RuntimeKernel.create(runtimeArtifact, { manifests: new Map() }))).rejects.toThrow(RuntimeContractError);
  });

  it('blocks artifacts compiled by an unknown runtime compiler before rendering', async () => {
    const valid = await artifact({ compilerVersion: 'unsupported-runtime-compiler' });
    await expect(RuntimeKernel.create(valid, { manifests: new Map([[manifest.type, manifest]]) }))
      .rejects.toMatchObject({ diagnostics: [expect.objectContaining({ code: 'unsupportedCompilerVersion' })] });
  });

  it('rejects a formal artifact with missing or empty manifest declarations before rendering', async () => {
    const valid = await artifact();
    await expect(RuntimeKernel.create({ ...valid, manifest: undefined }, { manifests: new Map([[manifest.type, manifest]]) })).rejects.toMatchObject({ diagnostics: [{ code: 'missingManifestDeclarations' }] });
    await expect(RuntimeKernel.create({ ...valid, manifest: [] }, { manifests: new Map([[manifest.type, manifest]]) })).rejects.toMatchObject({ diagnostics: [{ code: 'missingManifestDeclarations' }] });
  });

  it('rejects an unknown component manifest declaration at load time', async () => {
    const valid = await artifact({ manifestTypes: ['unknown'], manifest: [{ type: 'unknown' }] });
    await expect(RuntimeKernel.create(valid, { manifests: new Map() })).rejects.toMatchObject({ diagnostics: expect.arrayContaining([expect.objectContaining({ code: 'unknownManifest' })]) });
  });

  it('rejects a signed artifact with an unknown manifest renderer contract', async () => {
    const valid = await artifact({ manifest: [{ type: manifest.type, renderer: { runtime: 'UnknownRenderer', preview: 'ComponentRuntimeHost' } }] });
    await expect(RuntimeKernel.create(valid, { manifests: new Map([[manifest.type, manifest]]) }))
      .rejects.toMatchObject({ diagnostics: expect.arrayContaining([expect.objectContaining({ code: 'unknownRuntimeRenderer' })]) });
  });

  it('rejects a signed artifact with a missing manifest renderer contract', async () => {
    const valid = await artifact({ manifest: [{ type: manifest.type }] });
    await expect(RuntimeKernel.create(valid, { manifests: new Map([[manifest.type, manifest]]) }))
      .rejects.toMatchObject({ diagnostics: expect.arrayContaining([expect.objectContaining({ code: 'unknownRuntimeRenderer' })]) });
  });

  it('blocks unknown actions and records a diagnostic', async () => {
    const kernel = await RuntimeKernel.create(await artifact(), { manifests: new Map([[manifest.type, manifest]]) });
    await expect(kernel.executeActions([{ id: 'step-1', type: 'missing.action' }], { scopes: {} })).rejects.toThrow('Unknown runtime action');
    expect(kernel.diagnostics.all.some((item) => item.code === 'unknownAction')).toBe(true);
  });

  it('does not return a snapshot for a component denied by runtime permission', async () => {
    const kernel = await RuntimeKernel.create(await artifact({
      elements: {
        root: {
          children: [],
          events: [],
          id: 'root',
          layout: {},
          parentId: null,
          permission: { code: 'orders.read' },
          props: { value: 'secret' },
          type: 'text.paragraph'
        }
      }
    }), {
      manifests: new Map([[manifest.type, manifest]]),
      permissions: { granted: new Set(), isSystemAdmin: false }
    });

    expect(kernel.snapshot('root', { scopes: {}, variables: {} })).toBeNull();
    expect(kernel.diagnostics.all).toContainEqual(expect.objectContaining({ code: 'permissionDenied', path: 'elements.root' }));
  });

  it('does not treat props.permission as an authorization contract', async () => {
    const kernel = await RuntimeKernel.create(await artifact({
      elements: {
        root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: { permission: { code: 'orders.read' }, value: 'public' }, type: 'text.paragraph' }
      }
    }), { manifests: new Map([[manifest.type, manifest]]), permissions: { granted: new Set(), isSystemAdmin: false } });

    expect(kernel.snapshot('root', { scopes: {}, variables: {} })?.props.value).toBe('public');
    expect(kernel.diagnostics.all).not.toContainEqual(expect.objectContaining({ code: 'permissionDenied' }));
  });

  it('resolves responsive property sections and runtime state without a bindings.props overlay', async () => {
    const kernel = await RuntimeKernel.create(await artifact({
      elements: {
        root: {
          children: [],
          events: [],
          id: 'root',
          layout: { display: 'flex', width: 320 },
          style: { color: 'red', padding: 4 },
          responsiveOverrides: { tablet: { layout: { width: 640 }, props: { title: 'tablet title' }, style: { color: 'blue' } } },
          bindings: {},
          parentId: null,
          props: { title: { resourceId: 'page.title' }, visible: { resourceId: 'page.visible' }, disabled: true, readOnly: true },
          type: 'text.paragraph'
        }
      }
    }), { manifests: new Map([[manifest.type, manifest]]) });

    const snapshot = kernel.snapshot('root', {
      resources: { 'page.title': 'bound title', 'page.visible': false },
      scopes: {},
      variables: {},
      loading: { root: true }
    }, { breakpoint: 'tablet' });

    expect(snapshot).toMatchObject({
      disabled: true,
      layout: { display: 'flex', width: 640 },
      loading: true,
      props: { title: 'tablet title', visible: false },
      readOnly: true,
      style: { color: 'blue', padding: 4 },
      visible: false
    });
  });

  it('rejects an action handler that is absent from the canonical capability contract', async () => {
    const valid = await artifact({
      actions: [{ id: 'action-1', steps: [{ id: 'step-1', type: 'custom.action' }] }]
    });
    await expect(RuntimeKernel.create(valid, {
      actionHandlers: [{
        manifest: {
          cancelable: true,
          errorPolicy: 'stop',
          inputSchema: {},
          outputSchema: {},
          permissions: [],
          sideEffect: 'none',
          timeoutMs: 1000,
          triggers: ['click'],
          type: 'custom.action'
        },
        execute: () => undefined
      }],
      manifests: new Map([[manifest.type, manifest]])
    })).rejects.toMatchObject({ diagnostics: expect.arrayContaining([expect.objectContaining({ code: 'unknownAction' })]) });
  });

  it('validates and executes handlers supplied by the runtime host', async () => {
    const values: string[] = [];
    const kernel = await RuntimeKernel.create(await artifact({
      actions: [{ id: 'action-1', steps: [{ id: 'step-1', type: 'setVariable', config: { value: 'ok' } }] }]
    }), {
      actionHandlers: [{
        manifest: {
          cancelable: true,
          errorPolicy: 'stop',
          inputSchema: {},
          outputSchema: {},
          permissions: [],
          sideEffect: 'none',
          timeoutMs: 1000,
          triggers: ['click'],
          type: 'setVariable'
        },
        execute: (config) => values.push(String(config.value))
      }],
      manifests: new Map([[manifest.type, manifest]])
    });

    await kernel.executeActions([{ id: 'step-1', type: 'setVariable', config: { value: 'ok' } }], { scopes: {} });
    expect(values).toEqual(['ok']);
    expect(kernel.diagnostics.metrics.actionSuccesses).toBe(1);
  });

  it('does not allow an action caller to elevate beyond the kernel permission context', async () => {
    let executed = false;
    const kernel = await RuntimeKernel.create(await artifact(), {
      actionHandlers: [{
        manifest: {
          cancelable: true,
          errorPolicy: 'stop',
          inputSchema: {},
          outputSchema: {},
          permissions: ['orders.edit'],
          sideEffect: 'write',
          timeoutMs: 1000,
          triggers: ['click'],
          type: 'setVariable'
        },
        execute: () => { executed = true; }
      }],
      manifests: new Map([[manifest.type, manifest]]),
      permissions: { granted: new Set<string>(), isSystemAdmin: false }
    });

    await expect(kernel.executeActions([{ id: 'secure-step', type: 'setVariable' }], {
      isSystemAdmin: true,
      permissions: new Set(['orders.edit']),
      scopes: {}
    })).rejects.toMatchObject({ kind: 'permissionDenied' });
    expect(executed).toBe(false);
  });

  it('rejects a tampered content payload even when the hash format is valid', async () => {
    const valid = await artifact();
    const tampered = { ...valid, integrityPayload: { ...(valid.integrityPayload as Record<string, unknown>), elements: {} } };
    await expect(RuntimeKernel.create(tampered, { manifests: new Map([[manifest.type, manifest]]) })).rejects.toMatchObject({ diagnostics: [{ code: 'artifactTampered' }] });
  });

  it('rejects tampered runtime fields when the signed payload is unchanged', async () => {
    const valid = await artifact();
    const tampered = {
      ...valid,
      elements: { ...valid.elements, root: { ...valid.elements.root, props: { value: 'tampered' } } }
    };
    await expect(RuntimeKernel.create(tampered, { manifests: new Map([[manifest.type, manifest]]) }))
      .rejects.toMatchObject({ diagnostics: [{ code: 'artifactProjectionMismatch', path: 'elements' }] });
  });

  it('rejects an artifact without a signature', async () => {
    const unsigned = await artifact({ signature: undefined });
    await expect(RuntimeKernel.create(unsigned, { manifests: new Map([[manifest.type, manifest]]) })).rejects.toMatchObject({ diagnostics: [{ code: 'invalidArtifactSignature' }] });
  });

  it('rejects an artifact with a mismatched signature', async () => {
    const signed = await artifact();
    await expect(RuntimeKernel.create({ ...signed, signature: '0'.repeat(64) }, { manifests: new Map([[manifest.type, manifest]]) }))
      .rejects.toMatchObject({ diagnostics: [{ code: 'artifactSignatureInvalid' }] });
  });

  it('rejects a component type omitted from manifestTypes', async () => {
    await expect(RuntimeKernel.create(await artifact({ manifestTypes: [] }), { manifests: new Map([[manifest.type, manifest]]) })).rejects.toMatchObject({ diagnostics: [{ code: 'missingManifestDeclarations' }] });
  });

  it('rejects invalid path bindings and unknown runtime renderers', async () => {
    const runtimeArtifact = await artifact({
      elements: {
        root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: { value: { path: 'name', source: 'unknown' } }, type: 'text.paragraph' }
      }
    });
    await expect(RuntimeKernel.create(runtimeArtifact, {
      manifests: new Map([[manifest.type, manifest]]),
      resolveRenderer: () => false
    })).rejects.toMatchObject({ diagnostics: expect.arrayContaining([
      expect.objectContaining({ code: 'invalidBinding' }),
      expect.objectContaining({ code: 'unknownRuntimeRenderer' })
    ]) });
  });

  it('rejects unknown binding converters before rendering', async () => {
    const runtimeArtifact = await artifact({
      elements: {
        root: {
          children: [],
          events: [],
          id: 'root',
          layout: {},
          parentId: null,
          props: {
            value: {
              conversionPipeline: [{ from: 'number', name: 'missingConverter', to: 'string' }],
              resourceId: 'orders.total',
              valueType: 'string'
            }
          },
          type: 'text.paragraph'
        }
      }
    });
    await expect(RuntimeKernel.create(runtimeArtifact, { manifests: new Map([[manifest.type, manifest]]) }))
      .rejects.toMatchObject({ diagnostics: expect.arrayContaining([expect.objectContaining({ code: 'unknownConverter' })]) });
  });

  it('rejects unknown scope state instead of silently falling back', async () => {
    const kernel = await RuntimeKernel.create(await artifact(), { manifests: new Map([[manifest.type, manifest]]) });
    expect(kernel.snapshot('root', { resources: {}, scopes: { unsupportedScope: {} }, variables: {} })).toBeNull();
    expect(kernel.diagnostics.all).toContainEqual(expect.objectContaining({ code: 'unknownRuntimeScope' }));
  });

  it('tracks resource dependencies declared by the canonical data binding and rejects unsupported component scopes', async () => {
    const scopedManifest = { ...manifest, runtime: { renderer: 'text', supportedScopes: ['page'] as const } };
    const runtimeArtifact = await artifact({
      elements: {
        root: {
          children: [],
          bindings: { data: { source: 'row', path: 'name' } },
          events: [],
          id: 'root',
          layout: {},
          parentId: null,
          props: {},
          type: 'text.paragraph'
        }
      }
    });
    await expect(RuntimeKernel.create(runtimeArtifact, { manifests: new Map([[manifest.type, scopedManifest]]) }))
      .rejects.toMatchObject({ diagnostics: expect.arrayContaining([expect.objectContaining({ code: 'invalidBinding' })]) });

    const permissiveKernel = await RuntimeKernel.create(await artifact({
      elements: {
        root: {
          children: [],
          bindings: { data: { resourceId: 'orders.rows' } },
          events: [],
          id: 'root',
          layout: {},
          parentId: null,
          props: {},
          type: 'text.paragraph'
        }
      }
    }), { manifests: new Map([[manifest.type, { ...manifest, runtime: { renderer: 'text', supportedScopes: ['page', 'row'] } }]]) });
    expect(permissiveKernel.affectedNodes('orders.rows')).toEqual(['root']);
  });

  it('recomputes a batch of changed resources without touching unrelated nodes', async () => {
    const kernel = await RuntimeKernel.create(await artifact({
      elements: {
        root: { children: ['child'], events: [], id: 'root', layout: {}, parentId: null, props: { value: { resourceId: 'customer.name' } }, type: 'text.paragraph' },
        child: { children: [], events: [], id: 'child', layout: {}, parentId: 'root', props: { value: { resourceId: 'orders.total' } }, type: 'text.paragraph' }
      }
    }), { manifests: new Map([[manifest.type, manifest]]) });

    const snapshots = await kernel.recompute(
      { resources: { 'customer.name': 'Ada', 'orders.total': 42 }, scopes: {}, variables: {} },
      ['orders.total', 'missing']
    );
    expect([...snapshots.keys()]).toEqual(['child']);
    expect(snapshots.get('child')?.props.value).toBe(42);
  });

  it('cancels recomputation before evaluating a node and records a diagnostic', async () => {
    const kernel = await RuntimeKernel.create(await artifact(), { manifests: new Map([[manifest.type, manifest]]) });
    const controller = new AbortController();
    controller.abort();

    await expect(kernel.recompute(
      { resources: { 'customer.name': 'Ada' }, scopes: {}, variables: {} },
      ['customer.name'],
      {},
      controller.signal
    )).rejects.toThrow('cancelled');
    expect(kernel.diagnostics.all).toContainEqual(expect.objectContaining({ code: 'recomputeCancelled' }));
  });

  it('rejects a legacy path binding instead of resolving it from an implicit scope', async () => {
    await expect(RuntimeKernel.create(await artifact({
      elements: {
        root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: { value: { path: 'tenant.id', source: 'component' } }, type: 'text.paragraph' }
      }
    }), { manifests: new Map([[manifest.type, { ...manifest, runtime: { renderer: 'text', supportedScopes: ['component'] } }]]) })).rejects.toMatchObject({
      diagnostics: expect.arrayContaining([expect.objectContaining({ code: 'invalidBinding' })])
    });
  });

  it('fails closed when a synchronous snapshot encounters an asynchronous resource provider', async () => {
    const kernel = await RuntimeKernel.create(await artifact({
      elements: {
        root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: { value: { resourceId: 'orders:rows' } }, type: 'text.paragraph' }
      }
    }), { manifests: new Map([[manifest.type, manifest]]) });

    expect(kernel.snapshot('root', {
      resourceProviders: new Map([['orders', async () => [{ id: 'late' }]]]),
      resources: {},
      scopes: {},
      variables: {}
    })).toBeNull();
    expect(kernel.diagnostics.all).toContainEqual(expect.objectContaining({ code: 'bindingResolutionFailed', path: 'elements.root' }));
  });
});
