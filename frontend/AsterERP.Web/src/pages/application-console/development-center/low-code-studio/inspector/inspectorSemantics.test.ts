import { describe, expect, it } from 'vitest';

import type { InspectorPropertyDescriptor } from './contract/InspectorPropertyDescriptor';
import { areInspectorBatchDescriptorsCompatible, validateInspectorValue } from './inspectorSemantics';

function descriptor(overrides: Partial<InspectorPropertyDescriptor> = {}): InspectorPropertyDescriptor {
  return { id: 'test:props.value', semanticId: 'props.value', path: 'props.value', section: 'content', order: 1, editor: 'number', valueType: 'number', defaultValue: 0, labelKey: 'value', helpKey: 'value', fallbackLabel: 'Value', bindable: false, acceptedSources: [], bindingPolicy: { enabled: false, acceptedSources: [] }, responsive: { enabled: false, mode: 'inherit' }, batchPolicy: 'editable', validation: { valueType: 'number', min: 0 }, resetPolicy: 'default', runtimeConsumer: 'runtime.test.value', ownerType: 'test', ...overrides };
}

describe('Inspector semantics', () => {
  it('rejects invalid values before command creation', () => {
    expect(validateInspectorValue(-1, descriptor())).toMatchObject({ valid: false });
    expect(validateInspectorValue(2, descriptor())).toMatchObject({ valid: true });
  });

  it('does not batch fields with different semantic metadata', () => {
    expect(areInspectorBatchDescriptorsCompatible(descriptor(), descriptor())).toBe(true);
    expect(areInspectorBatchDescriptorsCompatible(descriptor(), descriptor({ unit: 'px' }))).toBe(false);
  });
});
