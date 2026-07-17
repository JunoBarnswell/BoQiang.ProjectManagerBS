import { describe, expect, it } from 'vitest';

import { resolveRuntimeComponentValue } from './ComponentRuntimeHost';

describe('resolveRuntimeComponentValue', () => {
  it('renders a form-bound component from the current form scope after a microflow write', () => {
    const sourceElement = {
      bindings: {
        data: {
          field: 'customerName',
          resourceId: 'form:customerName'
        }
      }
    } as never;

    expect(resolveRuntimeComponentValue(
      sourceElement,
      { value: '' },
      { componentValues: {}, formValues: { customerName: 'Codex最终验收客户-已编辑' } },
      'customer-form'
    )).toBe('Codex最终验收客户-已编辑');
  });

  it('keeps a locally edited component value ahead of the form scope', () => {
    const sourceElement = {
      bindings: { data: { field: 'customerName', resourceId: 'form:customerName' } }
    } as never;

    expect(resolveRuntimeComponentValue(
      sourceElement,
      { value: '' },
      { componentValues: { 'customer-form': '本地编辑值' }, formValues: { customerName: '微流回填值' } },
      'customer-form'
    )).toBe('本地编辑值');
  });
});
