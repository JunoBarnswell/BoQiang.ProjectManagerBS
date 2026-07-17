import type { ExpressionNode } from './expressionGraph';

export interface ExpressionEvaluationContext {
  resolveResource: (resourceId: string) => unknown;
}

export function evaluateExpressionNode(node: ExpressionNode | null, context: ExpressionEvaluationContext): unknown {
  if (!node) throw new Error('Expression root is empty.');
  switch (node.kind) {
    case 'literal': return node.value;
    case 'resourceRef': return context.resolveResource(node.resourceId);
    case 'conversion': return node.pipeline.reduce((value, step) => applyExpressionConversion(step.name, value), evaluateExpressionNode(node.input, context));
    case 'condition': return evaluateExpressionNode(node.when, context) === true ? evaluateExpressionNode(node.then, context) : evaluateExpressionNode(node.otherwise, context);
    case 'logic': {
      if (node.operator === 'not') return evaluateExpressionNode(node.args[0] ?? null, context) !== true;
      if (node.operator === 'and') return node.args.every((argument) => evaluateExpressionNode(argument, context) === true);
      return node.args.some((argument) => evaluateExpressionNode(argument, context) === true);
    }
    case 'defaultValue': {
      const value = evaluateExpressionNode(node.input, context);
      return value === undefined || value === null ? node.fallback : value;
    }
    case 'functionCall': return evaluateCall(node.functionId, node.args.map((argument) => evaluateExpressionNode(argument, context)));
    case 'object': return Object.fromEntries(Object.entries(node.properties).map(([key, value]) => [key, evaluateExpressionNode(value, context)]));
    case 'array': return node.items.map((item) => evaluateExpressionNode(item, context));
    case 'template': return node.items.map((item) => evaluateExpressionNode(item, context)).map((value) => value == null ? '' : String(value)).join('');
  }
}

function evaluateCall(name: string, args: unknown[]): unknown {
  switch (name) {
    case 'coalesce': return args.find((value) => value !== undefined && value !== null);
    case 'concat': return args.map((value) => String(value ?? '')).join('');
    case 'length': {
      const value = args[0];
      if (typeof value !== 'string' && !Array.isArray(value)) throw new Error('Length requires text or an array.');
      return value.length;
    }
    case 'lower': return requireString(args[0], name).toLowerCase();
    case 'upper': return requireString(args[0], name).toUpperCase();
    case 'trim': return requireString(args[0], name).trim();
    case 'contains': return requireString(args[0], name).includes(requireString(args[1], name));
    case 'startsWith': return requireString(args[0], name).startsWith(requireString(args[1], name));
    case 'endsWith': return requireString(args[0], name).endsWith(requireString(args[1], name));
    case 'add': return requireNumber(args[0], name) + requireNumber(args[1], name);
    case 'subtract': return requireNumber(args[0], name) - requireNumber(args[1], name);
    case 'multiply': return requireNumber(args[0], name) * requireNumber(args[1], name);
    case 'divide': {
      const divisor = requireNumber(args[1], name);
      if (divisor === 0) throw new Error('Division by zero is not allowed.');
      return requireNumber(args[0], name) / divisor;
    }
    case 'mod': {
      const divisor = requireNumber(args[1], name);
      if (divisor === 0) throw new Error('Modulo by zero is not allowed.');
      return requireNumber(args[0], name) % divisor;
    }
    case 'abs': return Math.abs(requireNumber(args[0], name));
    case 'round': return Math.round(requireNumber(args[0], name));
    case 'toString': return String(args[0] ?? '');
    case 'toNumber': {
      const value = typeof args[0] === 'number' ? args[0] : Number(requireString(args[0], name));
      if (!Number.isFinite(value)) throw new Error('To number requires a finite numeric value.');
      return value;
    }
    case 'isEmpty': return args[0] === null || args[0] === undefined || args[0] === '' || (Array.isArray(args[0]) && args[0].length === 0);
    case 'equals': return args[0] === args[1];
    case 'notEquals': return args[0] !== args[1];
    case 'greaterThan': return compare(args[0], args[1], name) > 0;
    case 'greaterThanOrEqual': return compare(args[0], args[1], name) >= 0;
    case 'lessThan': return compare(args[0], args[1], name) < 0;
    case 'lessThanOrEqual': return compare(args[0], args[1], name) <= 0;
    default: throw new Error(`Expression function is not registered: ${name}`);
  }
}

function applyExpressionConversion(name: string, value: unknown): unknown {
  switch (name) {
    case 'numberToString': if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error('numberToString requires a finite number.'); return String(value);
    case 'booleanToString': if (typeof value !== 'boolean') throw new Error('booleanToString requires a boolean.'); return String(value);
    case 'arrayToJson': if (!Array.isArray(value)) throw new Error('arrayToJson requires an array.'); return JSON.stringify(value);
    case 'objectToJson': if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('objectToJson requires an object.'); return JSON.stringify(value);
    case 'jsonToArray': { const parsed = parseJson(value); if (!Array.isArray(parsed)) throw new Error('jsonToArray requires a JSON array.'); return parsed; }
    case 'jsonToObject': { const parsed = parseJson(value); if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('jsonToObject requires a JSON object.'); return parsed; }
    case 'stringToBoolean': { const normalized = requireString(value, name).trim().toLowerCase(); if (normalized === 'true' || normalized === '1') return true; if (normalized === 'false' || normalized === '0') return false; throw new Error('stringToBoolean accepts true, false, 1, or 0.'); }
    case 'stringToNumber': { const parsed = Number(requireString(value, name)); if (!Number.isFinite(parsed)) throw new Error('stringToNumber requires a finite number.'); return parsed; }
    case 'stringToDate': { const date = new Date(requireString(value, name)); if (Number.isNaN(date.getTime())) throw new Error('stringToDate requires a valid date.'); return date; }
    case 'dateToIsoString': { const date = value instanceof Date ? value : new Date(requireString(value, name)); if (Number.isNaN(date.getTime())) throw new Error('dateToIsoString requires a valid date.'); return date.toISOString(); }
    default: throw new Error(`Expression conversion is not registered: ${name}`);
  }
}

function parseJson(value: unknown): unknown { if (typeof value !== 'string') return value; try { return JSON.parse(value) as unknown; } catch { throw new Error('Expression JSON conversion received invalid JSON.'); } }
function requireString(value: unknown, name: string): string { if (typeof value !== 'string') throw new Error(`${name} requires text.`); return value; }
function requireNumber(value: unknown, name: string): number { if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error(`${name} requires a finite number.`); return value; }
function compare(left: unknown, right: unknown, name: string): number { if ((typeof left !== 'number' && typeof left !== 'string') || typeof left !== typeof right) throw new Error(`${name} requires comparable values.`); const leftValue = left as string | number; const rightValue = right as string | number; return leftValue < rightValue ? -1 : leftValue > rightValue ? 1 : 0; }
