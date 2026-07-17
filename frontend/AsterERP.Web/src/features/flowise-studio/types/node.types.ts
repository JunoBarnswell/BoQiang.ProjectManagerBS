export type FlowiseInputParamType =
  | 'string'
  | 'number'
  | 'boolean'
  | 'password'
  | 'options'
  | 'multiOptions'
  | 'json'
  | 'code'
  | 'file'
  | 'array'
  | 'grid'
  | 'date'
  | 'time'
  | 'credential';

export interface FlowiseNodeOption {
  description?: string | null;
  label: string;
  hide?: Record<string, unknown>;
  name?: string | null;
  show?: Record<string, unknown>;
  value?: string;
}

export interface FlowiseNodeInputParam {
  additionalParams?: boolean;
  acceptVariable?: boolean;
  default?: unknown;
  defaultJson?: string | null;
  description?: string | null;
  display?: boolean;
  hide?: Record<string, unknown>;
  id?: string | null;
  label: string;
  name: string;
  optional?: boolean;
  options?: FlowiseNodeOption[];
  placeholder?: string | null;
  rows?: number | null;
  show?: Record<string, unknown>;
  type: FlowiseInputParamType;
}

export interface FlowiseNodeAnchor {
  description?: string | null;
  id?: string | null;
  label: string;
  list?: boolean;
  name: string;
  type: string;
}

export interface FlowiseNodeDefinitionDto {
  baseClasses: string[];
  category: string;
  credential?: FlowiseNodeInputParam | null;
  description?: string | null;
  icon?: string | null;
  inputAnchors: FlowiseNodeAnchor[];
  inputParams: FlowiseNodeInputParam[];
  label: string;
  name: string;
  outputAnchors: FlowiseNodeAnchor[];
  tags: string[];
  type: string;
  version: number;
}

export interface FlowiseNodeCatalogItemDto {
  baseClasses?: string[];
  category: string;
  description: string;
  displayName: string;
  icon?: string | null;
  inputAnchors?: FlowiseNodeAnchor[];
  inputParams?: FlowiseNodeInputParam[];
  nodeType: string;
  outputAnchors?: FlowiseNodeAnchor[];
  tags?: string[];
  version?: number;
}
