import { describe, expect, it } from 'vitest';

import type { RuntimeArtifact, RuntimeArtifactManifestDeclaration } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';

import { RUNTIME_CAPABILITY_CONTRACT } from './runtime-contract/RuntimeCapabilityContract';
import { LATEST_RUNTIME_COMPILER_REVISION, RuntimeArtifactIntegrity } from './RuntimeArtifactIntegrity';

async function createArtifact(): Promise<RuntimeArtifact> {
  const integrityPayload: Record<string, unknown> = {
    actions: [],
    apiBindings: [],
    dataSources: [],
    documentId: 'document-1',
    elements: {},
    manifestTypes: ['layout.page'],
    pageMicroflows: [],
    pageParameters: [],
    permissions: {},
    revision: 1,
    runtimeContext: {},
    variables: [],
    workflowBindings: []
  };
  const artifact = {
    actions: [],
    artifactHash: await RuntimeArtifactIntegrity.computeHash(integrityPayload),
    bindings: [],
    compilerVersion: LATEST_RUNTIME_COMPILER_REVISION,
    documentId: 'document-1',
    elements: {},
    integrityPayload,
    manifest: [{ type: 'layout.page', renderer: { runtime: 'ComponentRuntimeHost', preview: 'ComponentRuntimeHost' } } satisfies RuntimeArtifactManifestDeclaration],
    manifestTypes: ['layout.page'],
    pageMicroflows: [],
    pageParameters: [],
    permissions: {},
    revision: 1,
    runtimeContext: {},
    signature: '',
    variables: []
  } satisfies RuntimeArtifact;
  artifact.signature = await RuntimeArtifactIntegrity.computeSignature(artifact);
  return artifact;
}

describe('RuntimeArtifactIntegrity formal contract', () => {
  it('derives supported compiler revisions from the canonical runtime capability contract', () => {
    expect(LATEST_RUNTIME_COMPILER_REVISION).toBe(RUNTIME_CAPABILITY_CONTRACT.compilerRevision);
  });

  it('rejects non-object transport input before reading artifact fields', async () => {
    await expect(RuntimeArtifactIntegrity.verify(null)).resolves.toMatchObject({ code: 'invalidArtifactShape' });
    await expect(RuntimeArtifactIntegrity.verify('runtime-artifact')).resolves.toMatchObject({ code: 'invalidArtifactShape' });
    await expect(RuntimeArtifactIntegrity.verify([])).resolves.toMatchObject({ code: 'invalidArtifactShape' });
  });

  it('accepts only the one latest compiler revision', async () => {
    const artifact = await createArtifact();
    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, compilerVersion: 'unsupported-runtime-compiler' }))
      .resolves.toMatchObject({ code: 'unsupportedCompilerVersion', details: { expected: LATEST_RUNTIME_COMPILER_REVISION } });
  });

  it('rejects null or absent formal signature and integrity payload', async () => {
    const artifact = await createArtifact();

    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, signature: null })).resolves.toMatchObject({ code: 'invalidArtifactSignature' });
    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, integrityPayload: null })).resolves.toMatchObject({ code: 'missingIntegrityPayload' });
    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, manifest: undefined })).resolves.toMatchObject({ code: 'missingManifestDeclarations' });
    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, manifest: [] })).resolves.toMatchObject({ code: 'missingManifestDeclarations' });
  });

  it('rejects a self-hashed payload when any formal field is missing', async () => {
    const artifact = await createArtifact();
    const fields = ['actions', 'apiBindings', 'dataSources', 'documentId', 'elements', 'pageMicroflows', 'pageParameters', 'permissions', 'revision', 'runtimeContext', 'variables', 'workflowBindings'];

    for (const field of fields) {
      const payload = { ...artifact.integrityPayload } as Record<string, unknown>;
      delete payload[field];
      const tampered = {
        ...artifact,
        artifactHash: await RuntimeArtifactIntegrity.computeHash(payload),
        integrityPayload: payload
      };
      tampered.signature = await RuntimeArtifactIntegrity.computeSignature(tampered);

      await expect(RuntimeArtifactIntegrity.verify(tampered), field).resolves.toMatchObject({
        code: 'missingFormalField',
        path: `integrityPayload.${field}`
      });
    }
  });

  it('rejects malformed formal field shapes before hash projection', async () => {
    const artifact = await createArtifact();
    const payload = { ...artifact.integrityPayload, permissions: [] } as Record<string, unknown>;
    const tampered = {
      ...artifact,
      artifactHash: await RuntimeArtifactIntegrity.computeHash(payload),
      integrityPayload: payload
    };
    tampered.signature = await RuntimeArtifactIntegrity.computeSignature(tampered);

    await expect(RuntimeArtifactIntegrity.verify(tampered)).resolves.toMatchObject({
      code: 'invalidFormalField',
      path: 'integrityPayload.permissions'
    });
  });

  it('rejects tampered artifact hash and signature', async () => {
    const artifact = await createArtifact();

    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, artifactHash: `sha256:${'0'.repeat(64)}` })).resolves.toMatchObject({ code: 'artifactTampered' });
    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, signature: '0'.repeat(64) })).resolves.toMatchObject({ code: 'artifactSignatureInvalid' });
  });

  it('rejects manifest declaration tampering even when the document hash is unchanged', async () => {
    const artifact = await createArtifact();
    const tampered = {
      ...artifact,
      manifest: [{ ...artifact.manifest[0], renderer: { runtime: 'tampered-renderer' } }]
    };

    await expect(RuntimeArtifactIntegrity.verify(tampered)).resolves.toMatchObject({ code: 'artifactSignatureInvalid' });
  });

  it('rejects manifest declarations that do not match manifestTypes', async () => {
    const artifact = await createArtifact();
    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, manifest: [{ type: 'unknown.component' }] })).resolves.toMatchObject({ code: 'manifestDeclarationsMismatch' });
  });

  it('rejects editor session state in the signed runtime payload', async () => {
    const artifact = await createArtifact();
    const payload = { ...artifact.integrityPayload, editorState: { selectedElementId: 'root' } };
    await expect(RuntimeArtifactIntegrity.verify({ ...artifact, integrityPayload: payload })).resolves.toMatchObject({ code: 'editorSessionInArtifact' });
  });

  it('matches complete artifact identity and rejects signature or manifest drift', async () => {
    const artifact = await createArtifact();

    await expect(RuntimeArtifactIntegrity.hasSameIdentity(artifact, { ...artifact })).resolves.toBe(true);
    await expect(RuntimeArtifactIntegrity.hasSameIdentity(artifact, { ...artifact, signature: '0'.repeat(64) })).resolves.toBe(false);
    await expect(RuntimeArtifactIntegrity.hasSameIdentity(artifact, {
      ...artifact,
       manifest: [{ ...artifact.manifest[0], renderer: { runtime: 'ComponentRuntimeHost', preview: 'UnknownRenderer' } }]
    })).resolves.toBe(false);
  });
});
