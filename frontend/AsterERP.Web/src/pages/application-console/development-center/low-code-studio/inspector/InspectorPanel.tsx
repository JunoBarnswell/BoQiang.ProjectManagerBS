import { Undo2 } from 'lucide-react';
import { useMemo } from 'react';

import { translateLiteral, useI18n } from '../../../../../core/i18n/I18nProvider';
import { createBindValueCommand } from '../commands/createDesignerCommands';
import type { ComponentManifest } from '../components/ComponentManifest';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';
import type { ResourceRef } from '../document/ResourceRef';
import type { BindingDocument, DesignerVariableExpression } from '../expression/expressionTypes';
import { ResponsiveOverrideDiffPanel } from '../responsive/ResponsiveOverrideDiffPanel';

import type { ComponentInspectorDefinition, InspectorPropertyDescriptor } from './contract/InspectorPropertyDescriptor';
import type { InspectorSectionDefinition } from './contract/InspectorSectionDefinition';
import { commitInspectorBinding, commitInspectorValue } from './inspectorMutations';
import { InspectorPropertyHost } from './InspectorPropertyHost';
import { InspectorSection } from './InspectorSection';
import { areInspectorBatchDescriptorsCompatible } from './inspectorSemantics';
import type { InspectorPanelProps } from './inspectorTypes';
import { latestComponentInspectorRegistry } from './registry/latestComponentInspectorRegistry';

export function InspectorPanel({ document, selectedNodeIds, manifest, manifests, bindingDocument, commandBus, onDocumentChange, onExpressionEdit, responsiveBreakpoints, selectedBreakpoint, className }: InspectorPanelProps) {
  const { locale, translate } = useI18n();
  const nodes = useMemo(() => selectedNodeIds.map((id) => document.elements[id]).filter(Boolean), [document, selectedNodeIds]);
  const definition = useMemo(() => inspectorDefinitionForSelection(nodes, manifest, manifests), [manifest, manifests, nodes]);
  const properties = useMemo(() => propertiesForSelection(nodes, definition, manifests), [definition, manifests, nodes]);
  const unavailableProperties = useMemo(() => {
    if (nodes.length < 2 || !definition) return [];
    return definition.properties.filter((property) => property.batchPolicy !== 'editable' || !properties.some((candidate) => candidate.path === property.path));
  }, [definition, nodes.length, properties]);
  const sections = useMemo(() => sectionsForProperties(definition?.sections ?? [], properties), [definition, properties]);
  const inspectorClassName = ['page-studio__inspector', className].filter(Boolean).join(' ');

  if (nodes.length === 0) return <aside className={inspectorClassName} aria-label={translate('lowCode.pageStudio.inspector')}><div className="page-studio__inspector-empty">{translate('lowCode.pageStudio.selectComponentToInspect')}</div></aside>;

  const commitValue = (propertyPath: string, value: unknown) => {
    const property = properties.find((candidate) => candidate.path === propertyPath);
    if (property) commitInspectorValue(document, selectedNodeIds, property, value, commandBus, onDocumentChange, selectedBreakpoint, responsiveBreakpoints);
  };
  const commitBinding = (propertyPath: string, expression: DesignerVariableExpression | null) => {
    const property = properties.find((candidate) => candidate.path === propertyPath);
    if (property) commitInspectorBinding(document, selectedNodeIds, property, expression, commandBus, onDocumentChange, selectedBreakpoint, responsiveBreakpoints);
  };
  const commitResource = (propertyPath: string, resource: ResourceRef) => {
    if (!commandBus) return;
    const result = commandBus.executeTransaction(selectedNodeIds.map((nodeId) => createBindValueCommand(nodeId, propertyPath, resource)));
    if (result.changed) onDocumentChange?.(result.document);
  };

  return <aside className={inspectorClassName} aria-label={translate('lowCode.pageStudio.inspector')}>
    <header className="page-studio__inspector-header"><div>
      <p className="page-studio__inspector-eyebrow">{translate('lowCode.pageStudio.inspector')}</p>
      <h2 className="page-studio__inspector-title">{nodes[0].name ?? nodes[0].type}</h2>
      <p className="page-studio__inspector-type">{nodes.length > 1 ? `${translate('lowCode.pageStudio.selected')} ${nodes.length} ${translate('lowCode.pageStudio.components')}` : nodes[0].type}</p>
    </div></header>
    {unavailableProperties.length > 0 ? <div aria-label={translate('lowCode.pageStudio.batchUnavailable')} className="page-studio__batch-unavailable">
      {translate('lowCode.pageStudio.batchUnavailable')}: {unavailableProperties.map((property) => {
        const label = translate(property.labelKey);
         return (label === property.labelKey ? property.fallbackLabel : label) || translateLiteral(locale, property.fallbackLabel); }).join(', ')}{/*
      }).join('、')}
    */}</div> : null}
    {responsiveBreakpoints ? <ResponsiveOverrideDiffPanel breakpoints={responsiveBreakpoints} commandBus={commandBus} document={document} nodeIds={selectedNodeIds} onDocumentChange={onDocumentChange} selectedBreakpoint={selectedBreakpoint ?? null} /> : null}
    {sections.map((section, index) => <InspectorSection key={section.id} defaultOpen={index < 2} title={translate(section.labelKey)}>
      {section.properties.map((property) => <InspectorPropertyHost
        key={property.path}
        property={property}
        nodes={nodes}
        bindingDocument={bindingDocument}
        onValueChange={(value) => commitValue(property.path, value)}
        onBindingChange={(expression) => commitBinding(property.path, expression)}
        onResourceBind={(resource) => commitResource(property.path, resource)}
        onExpressionEdit={() => onExpressionEdit?.(nodes[0].id, property.path)}
        onEditEnd={() => commandBus?.endTransaction()}
      />)}
    </InspectorSection>)}
    {commandBus ? <footer className="page-studio__inspector-footer"><button type="button" className="secondary-button h-8" onClick={() => { const result = commandBus.undo(); if (result?.changed) onDocumentChange?.(result.document); }}><Undo2 aria-hidden="true" className="h-3.5 w-3.5" />{translate('lowCode.pageStudio.undo')}</button></footer> : null}
  </aside>;
}

interface InspectorSectionView extends InspectorSectionDefinition {
  properties: readonly InspectorPropertyDescriptor[];
}

function inspectorDefinitionForSelection(nodes: readonly DesignerDocumentNode[], manifest: ComponentManifest | null | undefined, manifests: InspectorPanelProps['manifests']): ComponentInspectorDefinition | undefined {
  return manifest?.editor.inspector ?? manifests?.get(nodes[0]?.type)?.editor.inspector ?? latestComponentInspectorRegistry.get(nodes[0]?.type ?? '');
}

function propertiesForSelection(nodes: readonly DesignerDocumentNode[], definition: ComponentInspectorDefinition | undefined, manifests: InspectorPanelProps['manifests']): readonly InspectorPropertyDescriptor[] {
  const fallback = definition?.properties ?? [];
  if (nodes.length < 2) return fallback;
  const definitions = nodes.map((node) => manifests?.get(node.type)?.editor.inspector ?? latestComponentInspectorRegistry.get(node.type));
  if (definitions.some((item) => !item)) return fallback;
  return fallback.filter((property) => property.batchPolicy === 'editable' && definitions.every((candidate) => {
    const other = candidate!.properties.find((item) => item.path === property.path);
    return other !== undefined && areInspectorBatchDescriptorsCompatible(other, property);
  }));
}

function sectionsForProperties(sections: readonly InspectorSectionDefinition[], properties: readonly InspectorPropertyDescriptor[]): InspectorSectionView[] {
  const stableSections = new Set(['content', 'layout', 'appearance', 'data', 'interaction', 'advanced']);
  return sections.map((section) => ({ ...section, properties: properties.filter((property) => property.section === section.id).sort((left, right) => left.order - right.order || left.path.localeCompare(right.path)) })).filter((section) => section.properties.length > 0 || stableSections.has(section.id)).sort((left, right) => left.order - right.order || left.id.localeCompare(right.id));
}

export type { BindingDocument, ComponentManifest, DesignerDocument };
