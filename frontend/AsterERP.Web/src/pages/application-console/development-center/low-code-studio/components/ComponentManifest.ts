import type { ComponentInspectorDefinition } from '../inspector/contract/InspectorPropertyDescriptor';

import type { ComponentInteractionPolicy } from './componentInteractionPolicy';

export const CANONICAL_COMPONENT_CAPABILITY_FAMILIES = [
  'layout', 'text', 'action', 'input', 'choice', 'metric', 'chart', 'report',
  'table', 'tableElement', 'semantic', 'list', 'interaction', 'media', 'signature', 'modal'
] as const;

export type CanonicalComponentCapabilityFamily = typeof CANONICAL_COMPONENT_CAPABILITY_FAMILIES[number];

export function assertCanonicalComponentCapabilityFamily(value: string): CanonicalComponentCapabilityFamily {
  if ((CANONICAL_COMPONENT_CAPABILITY_FAMILIES as readonly string[]).includes(value)) return value as CanonicalComponentCapabilityFamily;
  throw new Error(`Unknown canonical component capability family: ${value}`);
}
export interface ComponentManifest {
  binding: ComponentBindingManifest;
  capability: ComponentCapabilityManifest;
  defaults: ComponentDefaultsManifest;
  editor: ComponentEditorManifest;
  editing?: ComponentEditingManifest;
  events: ComponentEventManifest[];
  /** Required by the runtime schema; registry validation reports missing metadata before use. */
  i18n: ComponentI18nManifest;
  /** Optional for backwards compatibility; declared policies are enforced by insertion and move decisions. */
  interaction?: ComponentInteractionPolicy;
  migrations: ComponentMigrationManifest[];
  responsive: ComponentResponsiveManifest;
  runtime: ComponentRuntimeManifest;
  security: ComponentSecurityManifest;
  type: string;
  validation: ComponentValidationManifest;
}

export interface ComponentEditingManifest {
  commitTriggers: readonly ('blur' | 'enter' | 'escape')[];
  enabled: boolean;
  primaryKeyNonEditable: boolean;
  supportsConflictResolution: boolean;
  supportedDataTypes: readonly string[];
}

export interface ComponentCapabilityManifest {
  acceptsChildren: boolean;
  capabilities: string[];
}

export interface ComponentEditorManifest {
  /** Runtime registry validation requires this field for published manifests. */
  inspector?: ComponentInspectorDefinition;
  inspectorSections: string[];
  previewRenderer: string;
  selectionMode: 'single' | 'multi' | 'none';
}

export interface ComponentRuntimeManifest {
  renderer: string;
  supportedScopes: readonly string[];
}

/** Stable message identifiers owned by the component catalog. Values are never user-facing literals. */
export interface ComponentI18nManifest {
  diagnosticKey: string;
  helpKey: string;
  labelKey: string;
}

export interface ComponentValidationManifest {
  schema: Record<string, unknown>;
  supportsDiagnostics: boolean;
}

export interface ComponentBindingManifest {
  acceptedSources: string[];
  acceptedTypes: string[];
  slots?: ComponentBindingSlot[];
  supportsConversion: boolean;
}

export interface ComponentBindingSlot {
  label: string;
  property: string;
  valueType: string;
}

export interface ComponentResponsiveManifest {
  supportedLayouts: string[];
  supportsOverrides: boolean;
}

export interface ComponentSecurityManifest {
  actionPermissions: string[];
  requiresPermission: boolean;
}

export interface ComponentEventManifest {
  name: string;
  payloadSchema: Record<string, unknown>;
  trigger: string;
}

export interface ComponentDefaultsManifest {
  layout: Record<string, unknown>;
  props: Record<string, unknown>;
  style: Record<string, unknown>;
}

export interface ComponentMigrationManifest {
  from: string;
  migrate: string;
}
