import { useEffect, useMemo, useRef } from 'react';

import { expressionValueToGraph } from '../../../../../api/runtime/expressionValue';
import { translateLiteral, useI18n } from '../../../../../core/i18n/I18nProvider';
import { BindingPopover } from '../binding/BindingPopover';
import { resourceReferenceFor } from '../binding/bindingTypes';
import { createConversionPipeline } from '../binding/conversionPipeline';
import { listStableResources } from '../binding/resourceExplorerStore';
import { RESOURCE_DROP_EVENT } from '../binding/resourcePointerDrag';
import type { DesignerDocumentNode } from '../document/DesignerDocument';
import { isExpressionValue, isResourceRef } from '../document/PropertyValue';
import type { ResourceRef } from '../document/ResourceRef';
import { ExpressionGraphEditor } from '../expression/ExpressionGraphEditor';
import type { BindingDocument, DesignerValueType, DesignerVariableExpression } from '../expression/expressionTypes';

import type { InspectorPropertyDefinition } from './inspectorTypes';
import { isMixedPropertyValue, readPropertyValue } from './propertyTransactions';
import { inspectorEditorRegistry } from './registry/InspectorEditorRegistry';

export interface InspectorPropertyHostProps {
  property: InspectorPropertyDefinition;
  nodes: readonly DesignerDocumentNode[];
  bindingDocument?: BindingDocument | null;
  onValueChange: (value: unknown) => void;
  onBindingChange: (expression: DesignerVariableExpression | null) => void;
  onResourceBind?: (resource: ResourceRef) => void;
  onExpressionEdit?: () => void;
  onEditEnd?: () => void;
}

export function InspectorPropertyHost({ property, nodes, bindingDocument, onValueChange, onBindingChange, onResourceBind, onExpressionEdit, onEditEnd }: InspectorPropertyHostProps) {
  const { locale, translate } = useI18n();
  const dropTargetRef = useRef<HTMLDivElement>(null);
  const mixed = isMixedPropertyValue(nodes, property.path);
  const rawValue = mixed ? undefined : readPropertyValue(nodes[0] ?? { id: '' }, property.path);
  const binding = nodes.length === 1 ? asExpression(readBindingValue(nodes[0], property.path) ?? rawValue) : null;
  const value = binding ? undefined : rawValue;
  const registration = inspectorEditorRegistry.get(property);
  const translatedLabel = translate(property.labelKey);
  const label = translatedLabel === property.labelKey ? property.fallbackLabel : translatedLabel;
  const options = useMemo(() => property.options?.map((option) => ({ ...option, label: translateLiteral(locale, option.label) })), [locale, property.options]);

  useEffect(() => {
    const target = dropTargetRef.current;
    if (!target || !property.bindable || !bindingDocument) return undefined;
    const handleResourceDrop = (event: Event) => {
      const resourceId = (event as CustomEvent<{ resourceId?: string }>).detail?.resourceId;
      const resource = resourceId ? listStableResources(bindingDocument).find((item) => item.id === resourceId) : undefined;
      if (!resource) return;
      const conversion = createConversionPipeline(resource.valueType, property.valueType as DesignerValueType);
      if (!conversion.valid) return;
      event.stopPropagation();
      onResourceBind?.(resourceReferenceFor(resource, property.valueType as DesignerValueType));
    };
    target.addEventListener(RESOURCE_DROP_EVENT, handleResourceDrop);
    return () => target.removeEventListener(RESOURCE_DROP_EVENT, handleResourceDrop);
  }, [bindingDocument, onResourceBind, property.bindable, property.valueType]);

  if (!registration) return <div className="page-studio__inspector-editor-error">{property.path}</div>;
  const Editor = registration.component;
  const editor = <Editor
    complexLabels={{
      addItem: translate('lowCode.pageStudio.addItem'),
      addProperty: translate('lowCode.pageStudio.addProperty'),
      noItems: translate('lowCode.pageStudio.noComplexItems'),
      noProperties: translate('lowCode.pageStudio.noComplexProperties'),
      propertyName: translate('lowCode.pageStudio.complexPropertyName'),
      remove: (itemLabel) => `${translate('lowCode.pageStudio.remove')} ${itemLabel}`,
    }}
    descriptor={property}
    label={label}
    mixed={mixed}
    mixedValueLabel={translate('lowCode.pageStudio.mixedValue')}
    onChange={onValueChange}
    options={options}
    placeholder={property.placeholder ? translateLiteral(locale, property.placeholder) : undefined}
    selectOptionLabel={translate('lowCode.pageStudio.selectOption')}
    value={value}
  />;

  return <div className="page-studio__inspector-field" data-field-key={property.path} data-editor-category={registration.category}>
    <div className="page-studio__inspector-field-header"><label>{label}</label>{mixed ? <span className="page-studio__mixed-value" aria-live="polite">{translate('lowCode.pageStudio.mixedValue')}</span> : null}</div>
    <div ref={dropTargetRef} data-resource-drop-target={property.bindable ? 'true' : undefined} className="page-studio__inspector-field-row" onBlur={onEditEnd}>
      <div className="page-studio__inspector-field-control">{editor}</div>
      {property.bindable ? <div className="page-studio__inspector-binding"><BindingPopover document={bindingDocument} expectedType={property.valueType as DesignerValueType} expression={binding} onBindValue={onResourceBind} onChange={onBindingChange} /></div> : null}
    </div>
    {property.bindable && binding ? <ExpressionGraphEditor document={bindingDocument} graph={binding.graph ?? bindingToGraph(binding)} expectedType={property.valueType as DesignerValueType} onChange={(graph) => onBindingChange({ ...binding, conversionPipeline: conversionPipelineForGraph(graph, property.valueType as DesignerValueType), graph })} onOpen={onExpressionEdit} compact /> : null}
  </div>;
}

function asExpression(value: unknown): DesignerVariableExpression | null {
  if (isExpressionValue(value)) return { expectedType: value.dataType, fallback: value.fallback, graph: expressionValueToGraph(value), helpers: [] };
  if (!isResourceRef(value)) return null;
  return { expectedType: value.expectedType, fallback: value.fallback, graph: { root: { kind: 'resourceRef', resourceId: value.resourceId, valueType: value.valueType } }, helpers: [], resourceId: value.resourceId, resourceType: value.resourceType };
}

function readBindingValue(node: DesignerDocumentNode | undefined, fieldKey: string): unknown {
  if (!fieldKey.startsWith('bindings.')) return undefined;
  return fieldKey.replace(/^bindings\./, '').split('.').filter(Boolean).reduce<unknown>((current, part) => current && typeof current === 'object' ? (current as Record<string, unknown>)[part] : undefined, node?.bindings);
}

function conversionPipelineForGraph(graph: { root: { kind: string; valueType: DesignerValueType; input?: { valueType: DesignerValueType }; name?: string } | null }, expectedType: DesignerValueType) {
  if (!graph.root) return [];
  if (graph.root.kind === 'conversion' && graph.root.input && graph.root.name) return [{ from: graph.root.input.valueType, name: graph.root.name, to: graph.root.valueType }];
  return createConversionPipeline(graph.root.valueType, expectedType).steps;
}

function bindingToGraph(expression: DesignerVariableExpression) {
  return expression.graph ?? { root: { kind: 'resourceRef' as const, resourceId: expression.resourceId ?? 'resource:*', valueType: expression.expectedType ?? 'json' } };
}
