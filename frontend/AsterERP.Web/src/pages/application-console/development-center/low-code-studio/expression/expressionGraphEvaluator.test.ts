import { describe, expect, it } from 'vitest';

import { evaluateExpressionNode } from './expressionGraphEvaluator';

describe('expression graph evaluator', () => {
  const context = { resolveResource: (resourceId: string) => ({ 'variables:name': 'Ada', 'variables:count': 2 }[resourceId]) };

  it('evaluates functions, conditionals, defaults and resources', () => {
    expect(evaluateExpressionNode({ kind: 'functionCall', functionId: 'concat', valueType: 'string', args: [{ kind: 'literal', value: 'Hi ', valueType: 'string' }, { kind: 'resourceRef', resourceId: 'variables:name', valueType: 'string' }] }, context)).toBe('Hi Ada');
    expect(evaluateExpressionNode({ kind: 'condition', valueType: 'number', when: { kind: 'functionCall', functionId: 'equals', valueType: 'boolean', args: [{ kind: 'literal', value: 2, valueType: 'number' }, { kind: 'resourceRef', resourceId: 'variables:count', valueType: 'number' }] }, then: { kind: 'literal', value: 10, valueType: 'number' }, otherwise: { kind: 'literal', value: 0, valueType: 'number' } }, context)).toBe(10);
    expect(evaluateExpressionNode({ kind: 'defaultValue', valueType: 'string', input: { kind: 'resourceRef', resourceId: 'missing', valueType: 'string' }, fallback: 'fallback' }, context)).toBe('fallback');
  });

  it('fails closed for unknown functions and unsafe arithmetic', () => {
    expect(() => evaluateExpressionNode({ kind: 'functionCall', functionId: 'unknown', valueType: 'string', args: [] }, context)).toThrow(/not registered/);
    expect(() => evaluateExpressionNode({ kind: 'functionCall', functionId: 'divide', valueType: 'number', args: [{ kind: 'literal', value: 1, valueType: 'number' }, { kind: 'literal', value: 0, valueType: 'number' }] }, context)).toThrow(/zero/);
  });
});
