import type { ComponentManifest } from '../../pages/application-console/development-center/low-code-studio/components/ComponentManifest';
import type { ComponentInspectorDefinition } from '../../pages/application-console/development-center/low-code-studio/inspector/contract/InspectorPropertyDescriptor';
import { latestComponentInspectorRegistry } from '../../pages/application-console/development-center/low-code-studio/inspector/registry/latestComponentInspectorRegistry';

import canonicalContract from './runtime-capability.latest.generated.json';
import type { RuntimeActionManifest } from './RuntimeActionManifest';

export interface RuntimeComponentCapabilityDefinition {
  readonly id: string;
  readonly types: readonly string[];
  readonly renderer: string;
  readonly previewRenderer: string;
  readonly acceptsChildren: boolean;
  readonly capabilities: readonly string[];
  readonly binding: ComponentManifest['binding'];
  readonly defaults: ComponentManifest['defaults'];
  readonly events: readonly ComponentManifest['events'][number][];
  readonly security: ComponentManifest['security'];
  readonly responsive: ComponentManifest['responsive'];
  readonly inspectorSections: readonly string[];
  readonly supportedScopes: readonly string[];
  readonly editing?: ComponentManifest['editing'];
}

export interface RuntimeCapabilityContractDefinition {
  readonly contractRevision: string;
  readonly compilerRevision: string;
  readonly migrationRevision: string;
  readonly renderer: string;
  readonly components: readonly string[];
  readonly inspectorComponentTypes: readonly string[];
  readonly componentCapabilities: readonly RuntimeComponentCapabilityDefinition[];
  readonly actions: readonly string[];
  readonly actionManifests: Readonly<Record<string, Omit<RuntimeActionManifest, 'type'>>>;
  readonly converters: readonly string[];
  readonly scopes: readonly string[];
}

export const RUNTIME_CAPABILITY_CONTRACT = canonicalContract as RuntimeCapabilityContractDefinition;

export const RUNTIME_INSPECTOR_CONTRACT = latestComponentInspectorRegistry;

export function componentCapabilityFor(type: string): RuntimeComponentCapabilityDefinition {
  const capability = RUNTIME_CAPABILITY_CONTRACT.componentCapabilities.find((item) => item.types.includes(type));
  if (!capability) throw new Error(`Runtime component capability is not declared: ${type}`);
  return capability;
}

export function componentInspectorFor(type: string): ComponentInspectorDefinition {
  const definition = RUNTIME_INSPECTOR_CONTRACT.get(type);
  if (!definition) throw new Error(`Runtime component inspector definition is not declared: ${type}`);
  return definition;
}
