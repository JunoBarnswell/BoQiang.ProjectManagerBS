import type { ComponentType } from 'react';

import type { InspectorEditorKind } from '../contract/InspectorEditorKind';
import type { InspectorPropertyDescriptor, InspectorValueType } from '../contract/InspectorPropertyDescriptor';
import { InspectorComplexConfigEditor } from '../editor/InspectorComplexConfigEditor';
import { InspectorPrimitiveEditor } from '../editor/InspectorPrimitiveEditor';

export interface InspectorEditorProps {
  descriptor: InspectorPropertyDescriptor;
  label: string;
  options?: ReadonlyArray<{ label: string; value: string }>;
  placeholder?: string;
  value: unknown;
  mixed: boolean;
  mixedValueLabel: string;
  selectOptionLabel: string;
  complexLabels: {
    addItem: string;
    addProperty: string;
    noItems: string;
    noProperties: string;
    propertyName: string;
    remove: (label: string) => string;
  };
  onChange: (value: unknown) => void;
}

export type InspectorEditorComponent = ComponentType<InspectorEditorProps>;
export type InspectorEditorCategory = 'primitive' | 'complex';

export interface InspectorEditorRegistration {
  readonly category: InspectorEditorCategory;
  readonly component: InspectorEditorComponent;
}

export class InspectorEditorRegistry {
  private readonly entries = new Map<InspectorEditorKind, InspectorEditorRegistration>();
  private readonly complexValueTypes = new Map<InspectorValueType, InspectorEditorRegistration>();

  public registerPrimitive(kind: Exclude<InspectorEditorKind, 'json'>, component: InspectorEditorComponent): this {
    this.entries.set(kind, { category: 'primitive', component });
    return this;
  }

  public registerComplex(valueType: Extract<InspectorValueType, 'array' | 'object' | 'json'>, component: InspectorEditorComponent): this {
    const registration = { category: 'complex' as const, component };
    this.complexValueTypes.set(valueType, registration);
    return this;
  }

  public get(descriptor: InspectorPropertyDescriptor): InspectorEditorRegistration | undefined {
    if (descriptor.editor === 'json' || descriptor.valueType === 'array' || descriptor.valueType === 'object' || descriptor.valueType === 'json') {
      return this.complexValueTypes.get(descriptor.valueType) ?? this.complexValueTypes.get('json');
    }
    return this.entries.get(descriptor.editor);
  }

  public assertValid(descriptors: readonly InspectorPropertyDescriptor[]): void {
    const missing = descriptors.filter((descriptor) => !this.get(descriptor)).map((descriptor) => descriptor.path);
    if (missing.length > 0) throw new Error(`Missing inspector editors: ${missing.join(', ')}`);
  }
}

export const inspectorEditorRegistry = new InspectorEditorRegistry()
  .registerPrimitive('text', InspectorPrimitiveEditor)
  .registerPrimitive('textarea', InspectorPrimitiveEditor)
  .registerPrimitive('number', InspectorPrimitiveEditor)
  .registerPrimitive('boolean', InspectorPrimitiveEditor)
  .registerPrimitive('color', InspectorPrimitiveEditor)
  .registerPrimitive('select', InspectorPrimitiveEditor)
  .registerComplex('array', InspectorComplexConfigEditor)
  .registerComplex('object', InspectorComplexConfigEditor)
  .registerComplex('json', InspectorComplexConfigEditor);
