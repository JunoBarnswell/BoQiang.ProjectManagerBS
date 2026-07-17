import {
  componentCapabilityFor,
  RUNTIME_CAPABILITY_CONTRACT,
} from "../../../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract";
import { latestComponentInspectorRegistry } from "../inspector/registry/latestComponentInspectorRegistry";

import { COMPONENT_PARENT_LAYOUTS, COMPONENT_RESIZE_HANDLES, validateComponentInteractionPolicy, type ComponentInteractionPolicy } from './componentInteractionPolicy';
import { assertCanonicalComponentCapabilityFamily, type ComponentManifest } from './ComponentManifest';
import { ComponentRegistry } from "./ComponentRegistry";

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

const LATEST_COMPONENT_TYPES = [...RUNTIME_CAPABILITY_CONTRACT.components];
const CONTAINER_TYPES = LATEST_COMPONENT_TYPES.filter((type) => componentCapabilityFor(type).acceptsChildren);
const CHILD_TYPES = LATEST_COMPONENT_TYPES.filter((type) => type !== 'layout.page');
const ALL_PARENT_LAYOUTS = [...COMPONENT_PARENT_LAYOUTS];
const RESIZABLE_HANDLES = [...COMPONENT_RESIZE_HANDLES];

function explicitInteractionFor(type: string): ComponentInteractionPolicy {
  const capability = componentCapabilityFor(type);
  const allowedParents = type === 'layout.page' ? ['*'] : CONTAINER_TYPES.filter((parentType) => parentType !== type);
  switch (assertCanonicalComponentCapabilityFamily(capability.id)) {
    case 'layout':
      return {
        allowedChildren: CHILD_TYPES,
        allowedParents,
        contentModel: 'children',
        geometryModel: 'intrinsic',
        resizeHandles: [],
        supportedParentLayouts: ALL_PARENT_LAYOUTS
      };
    case 'text':
      return {
        allowedChildren: [],
        allowedParents,
        contentModel: 'text',
        geometryModel: 'intrinsic',
        resizeHandles: [],
        supportedParentLayouts: ALL_PARENT_LAYOUTS
      };
    case 'table':
      return {
        allowedChildren: ['table.caption', 'table.colgroup', 'table.col', 'table.thead', 'table.tbody', 'table.tfoot'],
        allowedParents,
        contentModel: 'children',
        geometryModel: 'intrinsic',
        resizeHandles: [],
        supportedParentLayouts: ALL_PARENT_LAYOUTS
      };
    case 'tableElement':
      return {
        allowedChildren: type === 'table.tr' ? ['table.th', 'table.td'] : type === 'table.colgroup' ? ['table.col'] : type.startsWith('table.thead') || type.startsWith('table.tbody') || type.startsWith('table.tfoot') ? ['table.tr'] : [],
        allowedParents,
        contentModel: type === 'table.th' || type === 'table.td' ? 'mixed' : 'children',
        geometryModel: 'intrinsic',
        resizeHandles: [],
        supportedParentLayouts: ALL_PARENT_LAYOUTS
      };
    case 'semantic':
    case 'list':
    case 'interaction':
    case 'media':
    case 'modal':
      return {
        allowedChildren: CHILD_TYPES,
        allowedParents,
        contentModel: 'mixed',
        geometryModel: 'intrinsic',
        resizeHandles: [],
        supportedParentLayouts: ALL_PARENT_LAYOUTS
      };
    case 'action':
    case 'input':
    case 'choice':
    case 'metric':
    case 'chart':
    case 'report':
    case 'signature':
      return {
        allowedChildren: [],
        allowedParents,
        contentModel: 'void',
        geometryModel: 'absolute',
        resizeHandles: RESIZABLE_HANDLES,
        supportedParentLayouts: ['free', 'constraints']
      };
    default:
      throw new Error(`Interaction policy is not declared for runtime capability family: ${capability.id}`);
  }
}

const EXPLICIT_INTERACTION_POLICIES: Readonly<Record<string, ComponentInteractionPolicy>> = Object.fromEntries(
  LATEST_COMPONENT_TYPES.map((type) => [type, explicitInteractionFor(type)])
);

function interactionFor(type: string): ComponentInteractionPolicy {
  const policy = EXPLICIT_INTERACTION_POLICIES[type];
  if (!policy) throw new Error(`Interaction policy is not declared for latest component: ${type}`);
  return clone(policy);
}

export interface LatestComponentManifestDiagnostic {
  code: string;
  message: string;
}

export function validateLatestComponentManifest(
  manifest: ComponentManifest,
  capability: { readonly acceptsChildren: boolean; readonly id: string }
): readonly LatestComponentManifestDiagnostic[] {
  assertCanonicalComponentCapabilityFamily(capability.id);
  const diagnostics: LatestComponentManifestDiagnostic[] = [];
  const add = (code: string, message: string) => diagnostics.push({ code, message });
  const interaction = manifest.interaction;
  if (!interaction) {
    add('missingInteractionPolicy', 'latest runtime components must declare interaction policy');
    return diagnostics;
  }

  for (const diagnostic of validateComponentInteractionPolicy(manifest)) add(diagnostic.code, diagnostic.message);
  if (capability.acceptsChildren === false && interaction.contentModel !== 'void') add('contentModelCapabilityMismatch', 'non-container runtime capabilities must use void content model');
  if (capability.acceptsChildren && interaction.contentModel === 'void') add('contentModelCapabilityMismatch', 'container runtime capabilities cannot use void content model');

  const expectedLayouts = interaction.geometryModel === 'absolute'
    ? ['free', 'constraints']
    : interaction.geometryModel === 'flow'
      ? ['flex', 'grid']
      : [...COMPONENT_PARENT_LAYOUTS];
  if (!sameValues(interaction.supportedParentLayouts, expectedLayouts)) add('unsupportedLayoutDeclaration', `${manifest.type} geometry ${interaction.geometryModel} must explicitly support ${expectedLayouts.join(', ')}`);

  const expectedHandles = interaction.geometryModel === 'absolute' ? [...COMPONENT_RESIZE_HANDLES] : [];
  if (!sameValues(interaction.resizeHandles, expectedHandles)) add('resizeCapabilityMismatch', `${manifest.type} geometry ${interaction.geometryModel} must declare ${expectedHandles.length} resize handles`);

  validateTypeRules('allowedChildren', interaction.allowedChildren, add);
  validateTypeRules('allowedParents', interaction.allowedParents, add);
  return diagnostics;
}

function validateTypeRules(name: string, rules: readonly string[], add: (code: string, message: string) => void): void {
  for (const rule of rules) {
    const normalized = rule.trim();
    if (normalized === '*' || normalized.endsWith('*')) {
      const prefix = normalized.slice(0, -1);
      if (!prefix || LATEST_COMPONENT_TYPES.some((type) => type.startsWith(prefix))) continue;
    } else if (LATEST_COMPONENT_TYPES.includes(normalized)) continue;
    add('unknownInteractionTypeRule', `${name} references unknown component type rule: ${rule}`);
  }
}

function sameValues(left: readonly string[], right: readonly string[]): boolean {
  return left.length === right.length && right.every((value) => left.includes(value));
}

function manifestFor(type: string): ComponentManifest {
  const capability = componentCapabilityFor(type);
  const inspector = latestComponentInspectorRegistry.get(type);
  if (!inspector) throw new Error(`Inspector definition is not registered for manifest type: ${type}`);
  const keyType = type.replaceAll(".", "_");
  const security = clone(capability.security);
  security.actionPermissions = security.actionPermissions.map((permission) => permission.replace("{type}", type));
  return {
    binding: clone(capability.binding),
    capability: { acceptsChildren: capability.acceptsChildren, capabilities: [...capability.capabilities] },
    defaults: clone(capability.defaults),
    editor: {
      inspector,
      inspectorSections: inspector.sections.map((section) => section.id),
      previewRenderer: capability.previewRenderer,
      selectionMode: "single",
    },
    ...(capability.editing ? { editing: clone(capability.editing) } : {}),
    events: clone([...capability.events]),
    interaction: interactionFor(type),
    i18n: {
      diagnosticKey: `lowCode.component.${keyType}.diagnostic`,
      helpKey: `lowCode.component.${keyType}.help`,
      labelKey: `lowCode.component.${keyType}.label`,
    },
    migrations: [{ from: `${type}@0`, migrate: `${type}:normalize-v1` }],
    responsive: clone(capability.responsive),
    runtime: { renderer: capability.renderer, supportedScopes: [...capability.supportedScopes] },
    security,
    type,
    validation: {
      schema: propertySchemaForInspector(inspector),
      supportsDiagnostics: true,
    },
  };
}

function propertySchemaForInspector(inspector: NonNullable<ComponentManifest['editor']['inspector']>): Record<string, unknown> {
  const schema: Record<string, unknown> = { type: 'object', properties: {}, additionalProperties: false };
  for (const property of inspector.properties) {
    if (!property.path.startsWith('props.')) continue;
    const parts = property.path.split('.').slice(1);
    if (parts.length === 0) continue;
    let cursor = schema;
    for (const part of parts.slice(0, -1)) {
      const properties = cursor.properties as Record<string, unknown>;
      const child = properties[part] as Record<string, unknown> | undefined;
      if (child) cursor = child;
      else {
        const next: Record<string, unknown> = { type: 'object', properties: {}, additionalProperties: false };
        properties[part] = next;
        cursor = next;
      }
    }
    const properties = cursor.properties as Record<string, unknown>;
    properties[parts[parts.length - 1]] = schemaForDescriptor(property);
  }
  return schema;
}

function schemaForDescriptor(property: NonNullable<ComponentManifest['editor']['inspector']>['properties'][number]): Record<string, unknown> {
  const type = property.valueType === 'json' ? ['object', 'array'] : property.valueType === 'date' ? 'string' : property.valueType;
  const schema: Record<string, unknown> = { type, default: clone(property.defaultValue) };
  if (property.options?.length) schema.enum = property.options.map((option) => option.value);
  if (property.validation.min !== undefined) schema.minimum = property.validation.min;
  if (property.validation.max !== undefined) schema.maximum = property.validation.max;
  if (property.validation.minLength !== undefined) schema.minLength = property.validation.minLength;
  if (property.validation.maxLength !== undefined) schema.maxLength = property.validation.maxLength;
  if (property.validation.pattern) schema.pattern = property.validation.pattern;
  return schema;
}

export const latestComponentManifests: readonly ComponentManifest[] =
  RUNTIME_CAPABILITY_CONTRACT.components.map(manifestFor);

const LATEST_COMPONENT_DIAGNOSTICS = latestComponentManifests.flatMap((manifest) => {
  const capability = componentCapabilityFor(manifest.type);
  return validateLatestComponentManifest(manifest, capability).map((diagnostic) => `${diagnostic.code} (${manifest.type}): ${diagnostic.message}`);
});
if (LATEST_COMPONENT_DIAGNOSTICS.length > 0) throw new Error(LATEST_COMPONENT_DIAGNOSTICS.join('\n'));

export const latestComponentRegistry = new ComponentRegistry(latestComponentManifests);
