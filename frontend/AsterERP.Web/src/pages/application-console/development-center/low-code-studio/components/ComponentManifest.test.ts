import { describe, expect, it } from 'vitest';

import type { ComponentManifest } from './ComponentManifest';

const manifest: ComponentManifest = {
  binding: { acceptedSources: ['variables'], acceptedTypes: ['string'], supportsConversion: true },
  capability: { acceptsChildren: false, capabilities: ['valueBinding'] },
  defaults: { layout: {}, props: {}, style: {} },
  editor: { inspector: { componentType: 'text.paragraph', ownerType: 'text.paragraph', onlyInherited: true, sections: [], properties: [] }, inspectorSections: ['content'], previewRenderer: 'textPreview', selectionMode: 'single' },
  events: [],
  i18n: { diagnosticKey: 'test.text.paragraph.diagnostic', helpKey: 'test.text.paragraph.help', labelKey: 'test.text.paragraph.label' },
  migrations: [],
  responsive: { supportedLayouts: ['block'], supportsOverrides: true },
  runtime: { renderer: 'textRuntime', supportedScopes: ['page', 'component'] },
  security: { actionPermissions: [], requiresPermission: false },
  type: 'text.paragraph',
  validation: { schema: {}, supportsDiagnostics: true }
};

describe('ComponentManifest', () => {
  it('requires every design and runtime capability boundary', () => {
    expect(Object.keys(manifest)).toEqual([
      'binding', 'capability', 'defaults', 'editor', 'events', 'i18n', 'migrations', 'responsive', 'runtime', 'security', 'type', 'validation'
    ]);
    expect(manifest.runtime.renderer).toBe('textRuntime');
  });
});
