import type { InspectorEditorKind } from './InspectorEditorKind';
import type { InspectorSectionDefinition } from './InspectorSectionDefinition';

export const INSPECTOR_VALUE_TYPES = [
  'array',
  'boolean',
  'date',
  'json',
  'number',
  'object',
  'string'
] as const;

export type InspectorValueType = typeof INSPECTOR_VALUE_TYPES[number];
export type InspectorBatchPolicy = 'editable' | 'readOnly' | 'hidden';
export type InspectorResetPolicy = 'default' | 'inherit' | 'none';

export interface InspectorCondition {
  readonly path: string;
  readonly operator: 'equals' | 'notEquals' | 'exists' | 'truthy' | 'falsy';
  readonly value?: unknown;
}

export interface InspectorResponsivePolicy {
  readonly enabled: boolean;
  readonly mode: 'inherit' | 'override';
}

export interface InspectorBindingPolicy {
  readonly enabled: boolean;
  readonly acceptedSources: readonly string[];
}

export interface InspectorAccessibilityMetadata {
  readonly role?: string;
  readonly labelPath?: string;
  readonly descriptionPath?: string;
}

export interface InspectorValidation {
  readonly valueType: InspectorValueType;
  readonly required?: boolean;
  readonly integer?: boolean;
  readonly min?: number;
  readonly max?: number;
  readonly minLength?: number;
  readonly maxLength?: number;
  readonly pattern?: string;
}

export interface InspectorPropertyDescriptor {
  readonly id: string;
  readonly semanticId: string;
  readonly path: string;
  readonly section: string;
  readonly order: number;
  readonly editor: InspectorEditorKind;
  readonly valueType: InspectorValueType;
  readonly defaultValue: unknown;
  readonly labelKey: string;
  readonly helpKey: string;
  readonly fallbackLabel: string;
  readonly bindable: boolean;
  readonly acceptedSources: readonly string[];
  readonly bindingPolicy: InspectorBindingPolicy;
  readonly responsive: InspectorResponsivePolicy;
  readonly batchPolicy: InspectorBatchPolicy;
  readonly visibleWhen?: InspectorCondition;
  readonly enabledWhen?: InspectorCondition;
  readonly validation: InspectorValidation;
  readonly resetPolicy: InspectorResetPolicy;
  readonly runtimeConsumer: string;
  readonly ownerType: string;
  readonly unit?: string;
  readonly accessibility?: InspectorAccessibilityMetadata;
  readonly options?: readonly { label: string; value: string }[];
  readonly placeholder?: string;
}

export interface ComponentInspectorDefinition {
  readonly componentType: string;
  readonly ownerType: string;
  readonly onlyInherited: boolean;
  readonly sections: readonly InspectorSectionDefinition[];
  readonly properties: readonly InspectorPropertyDescriptor[];
}
