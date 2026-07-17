import { describe, expect, it, vi } from 'vitest';

import { resolveRuntimeValue } from '../shared/runtime/runtimeExpression';

const executeRuntimeMicroflowMock = vi.hoisted(() => vi.fn());

vi.mock('../api/application-data-center/applicationDataCenter.api', () => ({
  executeRuntimeMicroflow: executeRuntimeMicroflowMock
}));

import { executeRuntimePageMicroflow } from './RuntimePageMicroflow';
import { createRuntimePageFormValues } from './RuntimePageMicroflowFormDefaults';

describe('executeRuntimePageMicroflow', () => {
  it('skips missing optional inputs and maps array output fields into variables and form defaults', async () => {
    executeRuntimeMicroflowMock.mockResolvedValue({
      data: {
        result: [{ id: 'order-1', order_no: 'MES-001' }],
        trace: [],
        variables: {
          orderRows: [{ id: 'order-1', order_no: 'MES-001' }]
        }
      }
    });
    const variables: Record<string, unknown> = { currentRow: { id: 'order-1' } };
    let formValues: Record<string, unknown> = {};
    const artifact = {
      elements: {
        'order-no': {
          bindings: {
            data: {
              defaultResourceId: 'microflow::detail.form.order_no',
              field: 'orderNo'
            }
          }
        }
      },
      pageMicroflows: [{
        action: 'detail',
        alias: 'detail',
        flowCode: 'mes_order_detail_sql',
        inputMappings: [
          {
            required: true,
            sourceExpression: { dataType: 'string', kind: 'resourceRef', resourceId: 'variables:currentRow.id', version: 'latest' },
            targetVariable: 'orderId'
          },
          {
            sourceExpression: { dataType: 'string', kind: 'resourceRef', resourceId: 'form:missingFilter', version: 'latest' },
            targetVariable: 'keyword'
          }
        ],
        outputMappings: [
          {
            outputVariable: 'orderRows',
            resultPath: '0.order_no',
            writeTo: 'microflows.detail.form.order_no'
          }
        ]
      }],
      runtimeContext: { pageCode: 'page_mr7xi5jk' }
    } as never;

    await executeRuntimePageMicroflow(artifact, 'detail', {
      formValues,
      mergeVariables: (nextVariables) => Object.assign(variables, nextVariables),
      scopes: { variables },
      setFormValues: (nextFormValues) => { formValues = nextFormValues; }
    });

    expect(executeRuntimeMicroflowMock).toHaveBeenCalledWith('mes_order_detail_sql', expect.objectContaining({
      action: 'detail',
      pageCode: 'page_mr7xi5jk',
      variables: { currentRow: { id: 'order-1' }, orderId: 'order-1' }
    }), expect.anything());
    expect(variables).toMatchObject({
      microflows: { detail: { form: { order_no: 'MES-001' } } }
    });
    expect(formValues).toEqual({ orderNo: 'MES-001' });
  });
});

describe('runtime page form contract', () => {
  it('registers declared empty form fields and resolves them without treating them as missing resources', () => {
    const artifact = {
      elements: {
        orderNo: { bindings: { data: { field: 'orderNo' } } },
        customerName: { bindings: { data: { field: 'customerName' } } }
      }
    } as never;
    const form = createRuntimePageFormValues(artifact);

    expect(Object.prototype.hasOwnProperty.call(form, 'orderNo')).toBe(true);
    expect(resolveRuntimeValue({ version: 'latest', kind: 'resourceRef', dataType: 'string', resourceId: 'form:orderNo' }, { form })).toBeUndefined();
    expect(() => resolveRuntimeValue({ version: 'latest', kind: 'resourceRef', dataType: 'string', resourceId: 'form:notDeclared' }, { form })).toThrow('not registered');
  });
});
