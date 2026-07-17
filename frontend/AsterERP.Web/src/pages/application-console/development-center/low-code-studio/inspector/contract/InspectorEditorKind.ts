export const INSPECTOR_EDITOR_KINDS = [
  'text',
  'textarea',
  'number',
  'boolean',
  'color',
  'select',
  'json'
] as const;

export type InspectorEditorKind = typeof INSPECTOR_EDITOR_KINDS[number];
