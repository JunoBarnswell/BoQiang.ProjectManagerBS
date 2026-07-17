import { INSPECTOR_EDITOR_KINDS } from '../contract/InspectorEditorKind';
import type { ComponentInspectorDefinition, InspectorPropertyDescriptor } from '../contract/InspectorPropertyDescriptor';
import type { ComponentInspectorDefinitionBase } from '../definitions/base/ComponentInspectorDefinitionBase';

export interface ComponentInspectorRegistryDiagnostic {
  readonly code: string;
  readonly componentType: string;
  readonly propertyPath?: string;
  readonly message: string;
}

export class ComponentInspectorRegistry {
  private readonly entries = new Map<string, ComponentInspectorDefinition>();

  public constructor(definitions: readonly ComponentInspectorDefinitionBase[] = []) {
    definitions.forEach((definition) => this.register(definition));
    this.assertValid();
  }

  public register(definition: ComponentInspectorDefinitionBase): void {
    const contract = definition.build();
    if (this.entries.has(contract.componentType)) throw new Error(`Duplicate inspector definition: ${contract.componentType}`);
    this.entries.set(contract.componentType, contract);
  }

  public get(type: string): ComponentInspectorDefinition | undefined {
    return this.entries.get(type);
  }

  public types(): readonly string[] {
    return [...this.entries.keys()];
  }

  public values(): readonly ComponentInspectorDefinition[] {
    return [...this.entries.values()];
  }

  public validate(expectedTypes?: readonly string[]): ComponentInspectorRegistryDiagnostic[] {
    const diagnostics = [...this.entries.values()].flatMap((definition) => validateDefinition(definition));
    if (expectedTypes) {
      const expected = new Set(expectedTypes);
      for (const type of expected) if (!this.entries.has(type)) diagnostics.push({ code: 'missingRegistration', componentType: type, message: 'component has no concrete inspector definition' });
      for (const type of this.entries.keys()) if (!expected.has(type)) diagnostics.push({ code: 'unexpectedRegistration', componentType: type, message: 'inspector definition is not a canonical component type' });
    }
    return diagnostics;
  }

  public assertValid(expectedTypes?: readonly string[]): void {
    const diagnostics = this.validate(expectedTypes);
    if (diagnostics.length > 0) throw new Error(diagnostics.map(formatDiagnostic).join('\n'));
  }

  public propertyDescriptors(type: string): readonly InspectorPropertyDescriptor[] {
    return this.entries.get(type)?.properties ?? [];
  }
}

function validateDefinition(definition: ComponentInspectorDefinition): ComponentInspectorRegistryDiagnostic[] {
  const diagnostics: ComponentInspectorRegistryDiagnostic[] = [];
  const add = (code: string, message: string, propertyPath?: string) => diagnostics.push({ code, componentType: definition.componentType, propertyPath, message });
  if (!definition.componentType.trim()) add('missingComponentType', 'component type is required');
  if (definition.ownerType !== definition.componentType) add('ownerTypeMismatch', 'definition ownerType must equal its canonical component type');
  const sectionIds = new Set<string>();
  for (const section of definition.sections) {
    if (!section.id.trim() || !section.labelKey.trim()) add('missingSectionI18n', 'section id and labelKey are required');
    if (!sectionIds.add(section.id)) add('duplicateSection', `duplicate section: ${section.id}`);
  }
  const paths = new Set<string>();
  const semanticIds = new Set<string>();
  for (const property of definition.properties) {
    if (paths.has(property.path)) add('duplicatePath', `duplicate property path: ${property.path}`, property.path);
    paths.add(property.path);
    if (!property.semanticId.trim()) add('missingSemanticId', 'semanticId is required', property.path);
    if (!semanticIds.add(property.semanticId)) add('duplicateSemanticId', `duplicate semantic id: ${property.semanticId}`, property.path);
    if (!property.id.trim()) add('missingPropertyId', 'property id is required', property.path);
    if (property.id !== `${definition.componentType}:${property.path}`) add('unstablePropertyId', 'property id must be derived from component type and path', property.path);
    if (!property.editor || !INSPECTOR_EDITOR_KINDS.includes(property.editor)) add('missingEditor', 'property editor is required', property.path);
    if (!property.labelKey.trim() || !property.helpKey.trim()) add('missingI18n', 'labelKey and helpKey are required', property.path);
    if (!property.runtimeConsumer.trim()) add('missingRuntimeConsumer', 'runtimeConsumer is required', property.path);
    if (property.ownerType !== definition.componentType) add('ownerTypeMismatch', 'property ownerType must equal the concrete component type', property.path);
    if (!sectionIds.has(property.section)) add('missingSection', `property section is not declared: ${property.section}`, property.path);
    if (property.validation.valueType !== property.valueType) add('validationTypeMismatch', 'validation valueType must match property valueType', property.path);
    if (property.bindingPolicy.enabled !== property.bindable) add('bindingPolicyMismatch', 'bindingPolicy.enabled must match bindable', property.path);
    if (property.bindingPolicy.acceptedSources.some((source) => !source.trim())) add('invalidBindingSource', 'accepted binding sources must be non-empty', property.path);
    if (!property.responsive.enabled && property.responsive.mode === 'override') add('invalidResponsivePolicy', 'disabled responsive properties must use inherit mode', property.path);
    if (!hasDefaultValueType(property.defaultValue, property.valueType)) add('invalidDefaultValue', `default value does not match ${property.valueType}`, property.path);
  }
  return diagnostics;
}

function hasDefaultValueType(value: unknown, valueType: InspectorPropertyDescriptor['valueType']): boolean {
  if (value === null || value === undefined) return true;
  if (valueType === 'array') return Array.isArray(value);
  if (valueType === 'object' || valueType === 'json') return typeof value === 'object' && !Array.isArray(value);
  if (valueType === 'number') return typeof value === 'number' && Number.isFinite(value);
  if (valueType === 'boolean') return typeof value === 'boolean';
  return typeof value === 'string';
}

function formatDiagnostic(diagnostic: ComponentInspectorRegistryDiagnostic): string {
  const path = diagnostic.propertyPath ? `.${diagnostic.propertyPath}` : '';
  return `${diagnostic.code} (${diagnostic.componentType}${path}): ${diagnostic.message}`;
}
