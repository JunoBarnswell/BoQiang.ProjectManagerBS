import { describe, expect, it } from 'vitest';

import { parseRuntimePageArtifact, toRuntimeArtifact } from './RuntimeArtifactCodec';
import { LATEST_RUNTIME_COMPILER_REVISION } from './RuntimeArtifactIntegrity';

describe('RuntimeArtifactCodec', () => {
  it('parses the latest signed artifact envelope and preserves its integrity payload', () => {
    const envelope = {
      artifactHash: 'sha256:' + 'a'.repeat(64),
      compilerVersion: LATEST_RUNTIME_COMPILER_REVISION,
      document: {
        actions: [],
        apiBindings: [],
        dataSources: [],
        documentId: 'doc-1',
        elements: { root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page' } },
        pageMicroflows: [],
        pageParameters: [],
        permissions: {},
        revision: 4,
        runtimeContext: { pageCode: 'home' },
        variables: [],
        workflowBindings: []
      },
      id: 'home',
      manifest: [{ type: 'layout.page', renderer: { preview: 'ComponentRuntimeHost', runtime: 'ComponentRuntimeHost' }, security: { requiresPermission: false } }],
      manifestTypes: ['layout.page'],
      revision: 4,
      signature: 'b'.repeat(64)
    };

    const parsed = parseRuntimePageArtifact(JSON.stringify(envelope));
    expect(parsed).toEqual(envelope);
    expect(toRuntimeArtifact(parsed!)).toMatchObject({
      artifactHash: envelope.artifactHash,
      documentId: 'doc-1',
      integrityPayload: envelope.document,
      manifest: envelope.manifest,
      manifestTypes: ['layout.page'],
      revision: 4,
      signature: envelope.signature
    });
  });

  it('rejects editor-shaped or incomplete envelopes before the runtime host is mounted', () => {
     expect(parseRuntimePageArtifact(JSON.stringify({ id: 'page-1', title: 'editor-shape', sections: [] }))).toBeNull();
    expect(parseRuntimePageArtifact(JSON.stringify({ id: 'page-1', document: {}, artifactHash: '', signature: '', compilerVersion: '', revision: 0, manifestTypes: [] }))).toBeNull();
     expect(parseRuntimePageArtifact(JSON.stringify({ id: 'page-1', document: {}, artifactHash: 'sha256:' + 'a'.repeat(64), signature: 'b'.repeat(64), compilerVersion: LATEST_RUNTIME_COMPILER_REVISION, revision: 1, manifestTypes: ['layout.page'] }))).toBeNull();
     expect(parseRuntimePageArtifact(JSON.stringify({ id: 'page-1', document: {}, artifactHash: 'sha256:' + 'a'.repeat(64), signature: 'b'.repeat(64), compilerVersion: LATEST_RUNTIME_COMPILER_REVISION, revision: 1, manifest: [], manifestTypes: ['layout.page'] }))).toBeNull();
  });

  it('rejects every missing formal document field instead of defaulting it', () => {
    const baseDocument: Record<string, unknown> = {
      actions: [],
      apiBindings: [],
      dataSources: [],
      documentId: 'doc-1',
      elements: { root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page' } },
      pageMicroflows: [],
      pageParameters: [],
      permissions: {},
      revision: 1,
      runtimeContext: {},
      variables: [],
      workflowBindings: []
    };
    const fields = ['actions', 'apiBindings', 'dataSources', 'documentId', 'elements', 'pageMicroflows', 'pageParameters', 'permissions', 'runtimeContext', 'variables', 'workflowBindings'];

    for (const field of fields) {
      const document = { ...baseDocument };
      delete document[field];
      const envelope = {
        artifactHash: 'sha256:' + 'a'.repeat(64),
        compilerVersion: LATEST_RUNTIME_COMPILER_REVISION,
        document,
        id: 'page-1',
        manifest: [{ type: 'layout.page', renderer: { preview: 'ComponentRuntimeHost', runtime: 'ComponentRuntimeHost' } }],
        manifestTypes: ['layout.page'],
        revision: 1,
        signature: 'b'.repeat(64)
      };

      expect(parseRuntimePageArtifact(JSON.stringify(envelope)), field).toBeNull();
    }
  });

  it('rejects a noncanonical compiler or renderer before the runtime host is mounted', () => {
    const base = {
      artifactHash: 'sha256:' + 'a'.repeat(64),
       compilerVersion: LATEST_RUNTIME_COMPILER_REVISION,
      document: {},
      id: 'page-1',
      manifestTypes: ['layout.page'],
      revision: 1,
      signature: 'b'.repeat(64)
    };

    expect(parseRuntimePageArtifact(JSON.stringify({
      ...base,
       compilerVersion: 'unsupported-runtime-compiler',
       manifest: [{ type: 'layout.page', renderer: { preview: 'ComponentRuntimeHost', runtime: 'ComponentRuntimeHost' } }]
    }))).toBeNull();
    expect(parseRuntimePageArtifact(JSON.stringify({
      ...base,
       manifest: [{ type: 'layout.page', renderer: { preview: 'UnknownRenderer', runtime: 'ComponentRuntimeHost' } }]
    }))).toBeNull();
    expect(parseRuntimePageArtifact(JSON.stringify({
      ...base,
      manifest: [{ type: 'layout.page' }]
    }))).toBeNull();
  });

  it('preserves every manifest declaration field instead of projecting only its type', () => {
    const declaration = { type: 'layout.page', renderer: { preview: 'ComponentRuntimeHost', runtime: 'ComponentRuntimeHost' }, customCapability: { enabled: true }, security: { requiresPermission: true } };
    const envelope = {
      artifactHash: 'sha256:' + 'a'.repeat(64),
      compilerVersion: LATEST_RUNTIME_COMPILER_REVISION,
      document: { actions: [], apiBindings: [], dataSources: [], documentId: 'doc-1', elements: { root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page' } }, pageMicroflows: [], pageParameters: [], permissions: {}, revision: 1, runtimeContext: {}, variables: [], workflowBindings: [] },
      id: 'home',
      manifest: [declaration],
      manifestTypes: ['layout.page'],
      revision: 1,
      signature: 'b'.repeat(64)
    };

    const parsed = parseRuntimePageArtifact(JSON.stringify(envelope));
    expect(parsed?.manifest[0]).toEqual(declaration);
    expect(toRuntimeArtifact(parsed!)?.manifest[0]).toEqual(declaration);
  });

  it('rejects an envelope that attempts to carry editor session state into runtime', () => {
    const envelope = {
      artifactHash: 'sha256:' + 'a'.repeat(64),
      compilerVersion: LATEST_RUNTIME_COMPILER_REVISION,
      document: {
        actions: [],
        apiBindings: [],
        dataSources: [],
        documentId: 'doc-1',
        editorSession: { selectedElementId: 'root' },
        elements: { root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page' } },
        pageMicroflows: [],
        pageParameters: [],
        permissions: {},
        revision: 1,
        variables: [],
        workflowBindings: []
      },
      id: 'page-1',
      manifest: [{ type: 'layout.page' }],
      manifestTypes: ['layout.page'],
      revision: 1,
      signature: 'b'.repeat(64)
    };

    expect(parseRuntimePageArtifact(JSON.stringify(envelope))).toBeNull();
  });
});
