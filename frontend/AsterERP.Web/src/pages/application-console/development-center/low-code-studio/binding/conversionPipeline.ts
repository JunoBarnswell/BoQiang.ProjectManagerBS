import type { DesignerValueType } from '../expression/expressionTypes';

import type { BindingConversionStep , BindingCompatibility } from './bindingTypes';

export interface ConversionPipeline {
  compatibility: BindingCompatibility;
  reason: string;
  steps: BindingConversionStep[];
  valid: boolean;
}

export function createConversionPipeline(from: DesignerValueType, to: DesignerValueType): ConversionPipeline {
  if (from === to || to === 'json') return { compatibility: 'exact', reason: '类型完全匹配', steps: [], valid: true };
  if (from === 'number' && to === 'string') return compatible(from, to, 'numberToString', 'safe', '可无损转换');
  if (from === 'boolean' && to === 'string') return compatible(from, to, 'booleanToString', 'safe', '可无损转换');
  if (from === 'date' && to === 'string') return compatible(from, to, 'dateToIsoString', 'safe', '可无损转换');
  if (from === 'string' && to === 'number') return compatible(from, to, 'stringToNumber', 'safe', '可无损转换');
  if (from === 'string' && to === 'boolean') return compatible(from, to, 'stringToBoolean', 'safe', '可无损转换');
  if (from === 'string' && to === 'date') return compatible(from, to, 'stringToDate', 'safe', '可无损转换');
  if (from === 'array' && to === 'string') return compatible(from, to, 'arrayToJson', 'lossy', '转换可能丢失信息');
  if (from === 'object' && to === 'string') return compatible(from, to, 'objectToJson', 'lossy', '转换可能丢失信息');
  if (from === 'json' && to === 'object') return compatible(from, to, 'jsonToObject', 'lossy', '转换可能丢失信息');
  if (from === 'json' && to === 'array') return compatible(from, to, 'jsonToArray', 'lossy', '转换可能丢失信息');
  return { compatibility: 'incompatible', reason: '没有安全的类型转换', steps: [], valid: false };
}

function compatible(from: DesignerValueType, to: DesignerValueType, name: string, compatibility: Exclude<BindingCompatibility, 'exact' | 'incompatible'>, reason: string): ConversionPipeline {
  return { compatibility, reason, steps: [{ from, name, to }], valid: true };
}
