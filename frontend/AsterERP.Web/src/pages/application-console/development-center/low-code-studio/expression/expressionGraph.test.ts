import { describe, expect, it } from 'vitest';

import { diagnoseExpressionGraph, parseExpressionGraph, serializeExpressionGraph, validateExpressionGraph, type ExpressionGraph, type ExpressionNode } from './expressionGraph';

describe('latest expression graph', () => {
  it('round-trips a resource graph and validates its target type', () => {
    const graph: ExpressionGraph = { root: { kind: 'resourceRef', resourceId: 'variables:count', valueType: 'number' } };
    expect(parseExpressionGraph(JSON.parse(serializeExpressionGraph(graph)))).toEqual(graph);
    expect(validateExpressionGraph(graph, 'number')).toEqual([]);
  });
  it('rejects empty, incompatible, and invalid conversion roots', () => {
    expect(validateExpressionGraph({ root: null }, 'string')).toHaveLength(1);
    expect(validateExpressionGraph({ root: { kind: 'literal', value: 1, valueType: 'number' } }, 'string')).toHaveLength(1);
    expect(validateExpressionGraph({ root: { kind: 'literal', value: 1, valueType: 'number' } }, 'json')).toEqual([]);
    expect(validateExpressionGraph({ root: { kind: 'conversion', pipeline: [{ from: 'number', name: 'numberToString', to: 'boolean' }], input: { kind: 'resourceRef', resourceId: 'variables:count', valueType: 'number' }, valueType: 'boolean' } }, 'boolean')).toHaveLength(1);
  });
  it('validates condition, logic, default, object, array, and template nodes', () => {
    const graph: ExpressionGraph = { root: { kind: 'condition', valueType: 'string', when: { kind: 'logic', operator: 'and', valueType: 'boolean', args: [{ kind: 'literal', value: true, valueType: 'boolean' }, { kind: 'literal', value: false, valueType: 'boolean' }] }, then: { kind: 'defaultValue', valueType: 'string', input: { kind: 'literal', value: 'ok', valueType: 'string' }, fallback: '' }, otherwise: { kind: 'literal', value: 'no', valueType: 'string' } } };
    expect(validateExpressionGraph(graph, 'string')).toEqual([]);
    expect(validateExpressionGraph({ root: { kind: 'logic', operator: 'not', valueType: 'boolean', args: [] } }, 'boolean')).toHaveLength(1);
    expect(validateExpressionGraph({ root: { kind: 'template', valueType: 'string', items: [{ kind: 'literal', value: 'x', valueType: 'string' }] } }, 'string')).toEqual([]);
  });
  it('rejects malformed, unknown, cyclic, and oversized graphs', () => {
    expect(parseExpressionGraph({ root: { kind: 'condition', valueType: 'string' } })).toBeNull();
    expect(diagnoseExpressionGraph({ root: { kind: 'functionCall', functionId: 'unknown', args: [], valueType: 'string' } }, 'string')[0]?.code).toBe('unknown-function');
    const cyclic = { kind: 'defaultValue', valueType: 'string', fallback: '', input: undefined as unknown as ExpressionNode } as ExpressionNode & { input: ExpressionNode }; cyclic.input = cyclic;
    expect(diagnoseExpressionGraph({ root: cyclic }, 'string')[0]?.code).toBe('cycle');
  });
});
