import { describe, expect, it } from 'vitest';

import { RUNTIME_CAPABILITY_CONTRACT } from '../../../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract';
import { projectRuntimeLayout } from '../../../../../runtime-kernel/RuntimeLayoutProjection';
import type { DesignerDocument } from '../document/DesignerDocument';

import { resolveComponentInsertionTarget } from './componentInsertionTarget';
import { COMPONENT_PARENT_LAYOUTS, COMPONENT_RESIZE_HANDLES } from './componentInteractionPolicy';
import { latestComponentManifests, latestComponentRegistry } from './latestComponentManifestCatalog';

function documentWithPageRoot(layoutMode: string): DesignerDocument {
  return {
    actions: [],
    apiBindings: [],
    dataSources: [],
    documentId: `layout-matrix-${layoutMode}`,
    elements: {
      root: { children: [], events: [], id: 'root', layout: { layoutMode }, parentId: null, props: {}, type: 'layout.page' }
    },
    metadata: {},
    modals: [],
    pageParameters: [],
    pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }],
    permissions: {},
    revision: 1,
    runtimeContext: {},
    styleTokens: {},
    variables: [],
    workflowBindings: []
  };
}

describe('latest component x four-layout runtime matrix', () => {
  it('covers every canonical component and keeps catalog types complete', () => {
    expect(latestComponentManifests.map((manifest) => manifest.type).sort()).toEqual([...RUNTIME_CAPABILITY_CONTRACT.components].sort());
    expect(latestComponentManifests).toHaveLength(RUNTIME_CAPABILITY_CONTRACT.components.length);
  });

  it('matches supported parent layouts with containment and insertion decisions', () => {
    for (const manifest of latestComponentManifests) {
      const interaction = manifest.interaction;
      expect(interaction, `${manifest.type}.interaction`).toBeDefined();
      if (!interaction) continue;
      for (const layoutMode of COMPONENT_PARENT_LAYOUTS) {
        const expected = manifest.type !== 'layout.page' && interaction.supportedParentLayouts.includes(layoutMode);
        const document = documentWithPageRoot(layoutMode);
        const containment = latestComponentRegistry.canContain('layout.page', manifest.type, { layoutMode });
        const target = resolveComponentInsertionTarget({ component: manifest, document, dropTargetNodeId: 'root', manifests: latestComponentRegistry });

        expect(containment, `${manifest.type}.${layoutMode}.containment`).toBe(expected);
        expect(Boolean(target), `${manifest.type}.${layoutMode}.insertion`).toBe(expected);
        if (expected) expect(target).toMatchObject({ parentId: 'root', placement: 'inside', targetNodeId: 'root' });
      }
    }
  });

  it('matches every generated resize capability across all eight handles', () => {
    for (const manifest of latestComponentManifests) {
      const interaction = manifest.interaction;
      expect(interaction, `${manifest.type}.interaction`).toBeDefined();
      if (!interaction) continue;
      for (const handle of COMPONENT_RESIZE_HANDLES) {
        expect(latestComponentRegistry.canResize(manifest.type, handle), `${manifest.type}.${handle}`).toBe(interaction.resizeHandles.includes(handle));
      }
    }
  });

  it('projects every component default layout without runtime diagnostics in all four parent modes', () => {
    for (const manifest of latestComponentManifests) {
      for (const layoutMode of COMPONENT_PARENT_LAYOUTS) {
        const projection = projectRuntimeLayout({
          layout: { ...manifest.defaults.layout },
          parentBox: { height: 480, width: 640 },
          parentLayout: { layoutMode },
          siblingIndex: 0,
          siblingLayouts: [{ ...manifest.defaults.layout }]
        });

        expect(projection.diagnostics, `${manifest.type}.${layoutMode}.diagnostics`).toEqual([]);
        expect(projection.box, `${manifest.type}.${layoutMode}.box`).toBeDefined();
      }
    }
  });
});
