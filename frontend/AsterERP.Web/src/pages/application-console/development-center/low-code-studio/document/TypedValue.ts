export type DesignerValueKind = 'array' | 'boolean' | 'date' | 'json' | 'number' | 'object' | 'string';

export interface TypedValue {
  kind: DesignerValueKind;
  value: unknown;
}
