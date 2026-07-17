import { describe, expect, it } from 'vitest';

import { DocumentTemplateInspectorDefinition } from './components/DocumentTemplateInspectorDefinition';
import { LayoutColumnInspectorDefinition } from './components/LayoutColumnInspectorDefinition';
import { LayoutContainerInspectorDefinition } from './components/LayoutContainerInspectorDefinition';
import { LayoutFormInspectorDefinition } from './components/LayoutFormInspectorDefinition';
import { LayoutFormItemInspectorDefinition } from './components/LayoutFormItemInspectorDefinition';
import { LayoutHtmlInspectorDefinition } from './components/LayoutHtmlInspectorDefinition';
import { LayoutPageInspectorDefinition } from './components/LayoutPageInspectorDefinition';
import { LayoutPrintInspectorDefinition } from './components/LayoutPrintInspectorDefinition';
import { LayoutResponsiveInspectorDefinition } from './components/LayoutResponsiveInspectorDefinition';
import { LayoutRowInspectorDefinition } from './components/LayoutRowInspectorDefinition';
import { LayoutSplitInspectorDefinition } from './components/LayoutSplitInspectorDefinition';
import { LayoutTableContainerInspectorDefinition } from './components/LayoutTableContainerInspectorDefinition';
import { LayoutTabsInspectorDefinition } from './components/LayoutTabsInspectorDefinition';
import { LayoutTemplateInspectorDefinition } from './components/LayoutTemplateInspectorDefinition';

const definitions = [
  ['layout.column', LayoutColumnInspectorDefinition],
  ['layout.container', LayoutContainerInspectorDefinition],
  ['layout.form', LayoutFormInspectorDefinition],
  ['layout.formItem', LayoutFormItemInspectorDefinition],
  ['layout.html', LayoutHtmlInspectorDefinition],
  ['layout.page', LayoutPageInspectorDefinition],
  ['layout.print', LayoutPrintInspectorDefinition],
  ['layout.responsive', LayoutResponsiveInspectorDefinition],
  ['layout.row', LayoutRowInspectorDefinition],
  ['layout.split', LayoutSplitInspectorDefinition],
  ['layout.tableContainer', LayoutTableContainerInspectorDefinition],
  ['layout.tabs', LayoutTabsInspectorDefinition],
  ['layout.template', LayoutTemplateInspectorDefinition],
  ['document.template', DocumentTemplateInspectorDefinition],
] as const;

describe('layout and document inspector definitions', () => {
  it('exposes all runtime-backed layout types through the shared visual descriptor', () => {
    for (const [componentType, Definition] of definitions) {
      const definition = new Definition().build();
      const paths = definition.properties.map((property) => property.path);

      expect(definition.componentType).toBe(componentType);
      expect(definition.onlyInherited).toBe(false);
      expect(new Set(paths).size).toBe(paths.length);
      expect(paths).toEqual(expect.arrayContaining([
        'props.content',
        'layout.layoutMode',
        'layout.display',
        'layout.flexDirection',
        'layout.gap',
        'layout.alignItems',
        'layout.justifyContent',
        'layout.columns',
        'layout.gridTemplateColumns',
        'layout.gridTemplateRows',
        'layout.constraints',
        'layout.position',
        'layout.x',
        'layout.y',
        'layout.zIndex',
      ]));
    }
  });

  it('keeps conditional controls aligned with the runtime layout modes', () => {
    const properties = new LayoutContainerInspectorDefinition().build().properties;
    const property = (path: string) => properties.find((candidate) => candidate.path === path);

    expect(property('layout.layoutMode')?.options?.map((option) => option.value)).toEqual(['free', 'flex', 'grid', 'constraints']);
    expect(property('layout.flexDirection')?.visibleWhen).toEqual({ path: 'layout.layoutMode', operator: 'equals', value: 'flex' });
    expect(property('layout.columns')?.visibleWhen).toEqual({ path: 'layout.layoutMode', operator: 'equals', value: 'grid' });
    expect(property('layout.constraints')?.visibleWhen).toEqual({ path: 'layout.layoutMode', operator: 'equals', value: 'constraints' });
    expect(properties.filter((candidate) => candidate.path.startsWith('props.')).map((candidate) => candidate.path)).toEqual(expect.arrayContaining(['props.content', 'props.visible']));
    expect(properties.filter((candidate) => candidate.path.startsWith('props.')).every((candidate) => candidate.runtimeConsumer.startsWith('runtime.'))).toBe(true);
  });
});
