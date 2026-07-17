import { describe, expect, it } from 'vitest';

import { BindingResolver, loadRuntimeResourceContext } from './BindingResolver';

describe('BindingResolver runtime resource context', () => {
  it('executes a canonical conversion pipeline after resolving a resource', () => {
    const resolver = new BindingResolver();

    const value = resolver.resolve(
      {
        conversionPipeline: [{ from: 'number', name: 'numberToString', to: 'string' }],
        resourceId: 'orders.total',
        valueType: 'string'
      },
      { resources: { 'orders.total': 42 }, scopes: {}, variables: {} }
    );

    expect(value.value).toBe('42');
  });

  it('executes canonical conversion pipelines for ExpressionValue', () => {
    const resolver = new BindingResolver();

    const value = resolver.resolve(
      {
        version: 'latest',
        kind: 'conversion',
        dataType: 'number',
        input: { version: 'latest', kind: 'resourceRef', dataType: 'string', resourceId: 'page:total' },
        pipeline: [{ from: 'string', name: 'stringToNumber', to: 'number' }]
      },
      { resources: { 'page:total': '42' }, scopes: {}, variables: {} }
    );

    expect(value.value).toBe(42);
  });

  it('resolves canonical resourceRef nodes before expression evaluation', () => {
    const resolver = new BindingResolver();

    const value = resolver.resolve(
      { version: 'latest', kind: 'resourceRef', dataType: 'string', resourceId: 'form:orderNo' },
      {
        resourceProviders: new Map([['form', ({ resourceId }) => resourceId === 'form:orderNo' ? 'MES-001' : undefined]]),
        scopes: { form: {} },
        variables: {}
      }
    );

    expect(value).toEqual({ resourceId: 'form:orderNo', source: 'resource', value: 'MES-001' });
  });

  it('evaluates the same function AST used by the visual editor and rejects unknown calls', () => {
    const resolver = new BindingResolver();
    expect(resolver.resolve({ version: 'latest', kind: 'functionCall', dataType: 'string', functionId: 'concat', args: [{ version: 'latest', kind: 'literal', dataType: 'string', value: 'A' }, { version: 'latest', kind: 'literal', dataType: 'string', value: 'B' }] }, { scopes: {}, variables: {} }).value).toBe('AB');
    expect(() => resolver.resolve({ version: 'latest', kind: 'functionCall', dataType: 'string', functionId: 'unregistered', args: [] }, { scopes: {}, variables: {} })).toThrow('not registered');
  });

  it('fails closed for unknown converters and invalid conversion values', () => {
    const resolver = new BindingResolver();

    expect(() => resolver.resolve({
      conversionPipeline: [{ from: 'number', name: 'missingConverter', to: 'string' }],
      resourceId: 'orders.total',
      valueType: 'string'
    }, { resources: { 'orders.total': 42 }, scopes: {}, variables: {} })).toThrow('canonical capability contract');
    expect(() => resolver.resolve({
      conversionPipeline: [{ from: 'string', name: 'stringToNumber', to: 'number' }],
      resourceId: 'orders.total',
      valueType: 'number'
    }, { resources: { 'orders.total': 'not-a-number' }, scopes: {}, variables: {} })).toThrow('finite numeric string');
  });

  it('resolves a provider/resourceId value from the loaded runtime context before fallback', () => {
    const resolver = new BindingResolver();
    const value = resolver.resolve(
      { displayName: 'Order rows', resourceId: 'runtime.data-model:orders:rows', valueType: 'array' },
      {
        resources: { 'runtime.data-model:orders:rows': [{ id: 'order-1' }] },
        scopes: {},
        variables: {}
      }
    );

    expect(value).toEqual({
      resourceId: 'runtime.data-model:orders:rows',
      source: 'resource',
      value: [{ id: 'order-1' }]
    });
  });

  it('uses the persisted resource type instead of deriving a provider from a mutable-looking id', () => {
    const resolver = new BindingResolver();
    const value = resolver.resolve(
      { resourceId: 'opaque-order-status', resourceType: 'variables', valueType: 'string' },
      {
        resourceProviders: new Map([['variables', ({ resourceId }) => resourceId === 'opaque-order-status' ? 'Open' : undefined]]),
        scopes: {},
        variables: {}
      }
    );

    expect(value.value).toBe('Open');
  });

  it('loads each declared provider/resourceId binding and preserves provider-shaped resources', async () => {
    const resources = await loadRuntimeResourceContext(
      [{ provider: 'runtime.data-model', resourceId: 'orders', modelCode: 'orders' }],
      new Map([
        ['runtime.data-model', ({ binding }) => ({
          [`${binding?.resourceId}.rows`]: [{ id: 'order-1' }],
          [`${binding?.resourceId}.total`]: 1
        })]
      ])
    );

    expect(resources).toEqual({
      'orders.rows': [{ id: 'order-1' }],
      'orders.total': 1
    });
  });

  it('passes the real scope context and cancellation signal to providers', async () => {
    const controller = new AbortController();
    let received: { context: unknown; signal?: AbortSignal } | undefined;
    const resources = await loadRuntimeResourceContext(
      [{ provider: 'runtime.data-model', resourceId: 'orders', modelCode: 'orders' }],
      new Map([
        ['runtime.data-model', ({ context, signal }) => {
          received = { context, signal };
          return { 'orders.rows': [] };
        }]
      ]),
      { scopes: { page: { tenantId: 'tenant-a' } }, variables: { userId: 'user-a' } },
      controller.signal
    );

    expect(resources).toEqual({ 'orders.rows': [] });
    expect(received?.signal).toBe(controller.signal);
    expect(received?.context).toMatchObject({
      scopes: { page: { tenantId: 'tenant-a' } },
      variables: { userId: 'user-a' }
    });
  });

  it('does not publish provider results after cancellation', async () => {
    const controller = new AbortController();
    const pending = loadRuntimeResourceContext(
      [{ provider: 'runtime.data-model', resourceId: 'orders' }],
      new Map([['runtime.data-model', async () => {
        await Promise.resolve();
        controller.abort();
        return [{ id: 'late-result' }];
      }]]),
      { scopes: {}, variables: {} },
      controller.signal
    );

    await expect(pending).rejects.toThrow('cancelled');
  });
});
