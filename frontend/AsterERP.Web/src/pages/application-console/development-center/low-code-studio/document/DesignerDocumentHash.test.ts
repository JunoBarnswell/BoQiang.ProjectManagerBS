import { readFileSync } from 'node:fs';

import { describe, expect, it } from 'vitest';

import type { DesignerDocument } from './DesignerDocument';
import { canonicalizeDesignerContent, computeDesignerDocumentHash, serializeDesignerDocument } from './DesignerDocumentHash';

describe('DesignerDocumentHash', () => {
  it('matches the shared backend fixture and excludes only the top-level documentHash', () => {
    const fixture = JSON.parse(readFileSync(
      new URL('../../../../../../../../docs/low-code-refactor/fixtures/designer-document-hash-fixture.json', import.meta.url),
      'utf8'
    )) as { document: DesignerDocument; expectedHash: string };

    expect(computeDesignerDocumentHash(fixture.document)).toBe(fixture.expectedHash);
    expect(computeDesignerDocumentHash({ ...fixture.document, documentHash: 'sha256:different' })).toBe(fixture.expectedHash);

    const changed = structuredClone(fixture.document);
    changed.elements.root.props.value = { a: 1, documentHash: 'sha256:changed-nested-value', z: 2 };
    expect(computeDesignerDocumentHash(changed)).not.toBe(fixture.expectedHash);
  });

  it('rejects editor session state from the legacy serializer entry', () => {
    const fixture = JSON.parse(readFileSync(
      new URL('../../../../../../../../docs/low-code-refactor/fixtures/designer-document-hash-fixture.json', import.meta.url),
      'utf8'
    )) as { document: DesignerDocument };
    const withSession = { ...fixture.document, viewport: { width: 1280, height: 720, zoom: 1 } };
    expect(() => serializeDesignerDocument(withSession)).toThrow(/viewport belongs to DesignerEditorSession/);
  });

  it('uses semantic content instead of revision identity for dirty-state comparison', () => {
    const fixture = JSON.parse(readFileSync(
      new URL('../../../../../../../../docs/low-code-refactor/fixtures/designer-document-hash-fixture.json', import.meta.url),
      'utf8'
    )) as { document: DesignerDocument };
    expect(canonicalizeDesignerContent({ ...fixture.document, revision: fixture.document.revision + 9 })).toBe(canonicalizeDesignerContent(fixture.document));
    expect(canonicalizeDesignerContent({ ...fixture.document, documentHash: 'sha256:history-only' })).toBe(canonicalizeDesignerContent(fixture.document));
    expect(canonicalizeDesignerContent({ ...fixture.document, metadata: { ...fixture.document.metadata, changed: true } })).not.toBe(canonicalizeDesignerContent(fixture.document));
  });
});
