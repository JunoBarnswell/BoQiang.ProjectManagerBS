import { describe, expect, it } from 'vitest';

import { InspectorEditorRegistry, inspectorEditorRegistry } from './InspectorEditorRegistry';

describe('InspectorEditorRegistry', () => {
  it('keeps primitive and complex editor registrations separate', () => {
    const text = inspectorEditorRegistry.get({ editor: 'text', valueType: 'string' } as never);
    const object = inspectorEditorRegistry.get({ editor: 'json', valueType: 'object' } as never);

    expect(text?.category).toBe('primitive');
    expect(object?.category).toBe('complex');
  });

  it('validates that every registered property has a visual editor', () => {
    expect(() => inspectorEditorRegistry.assertValid([
      { editor: 'text', valueType: 'string', path: 'props.title' },
      { editor: 'json', valueType: 'array', path: 'props.options' },
    ] as never)).not.toThrow();
    expect(() => new InspectorEditorRegistry().assertValid([{ editor: 'text', valueType: 'string', path: 'props.title' }] as never)).toThrow('Missing inspector editors');
  });
});
