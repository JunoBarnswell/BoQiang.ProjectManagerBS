import { describe, expect, it } from 'vitest';

import { ActionHandlerRegistry, createRuntimeActionHandlers, type RuntimeActionHandler } from './ActionHandlerRegistry';
import { RuntimeDiagnostics } from './Diagnostics';

function handler(
  type: string,
  execute: RuntimeActionHandler['execute'],
  overrides: Partial<RuntimeActionHandler['manifest']> = {}
): RuntimeActionHandler {
  return {
    execute,
    manifest: {
      cancelable: true,
      errorPolicy: 'stop',
      inputSchema: {},
      outputSchema: {},
      permissions: [],
      sideEffect: 'none',
      timeoutMs: 100,
      triggers: ['click'],
      type,
      ...overrides
    }
  };
}

describe('ActionHandlerRegistry', () => {
  it('registers the complete host action set through one canonical factory', () => {
    const handlers = createRuntimeActionHandlers({ pageMicroflows: [], elements: {}, documentId: 'doc-1' } as never);
    const registry = new ActionHandlerRegistry();
    handlers.forEach((item) => registry.register(item));

    expect(handlers.map((item) => item.manifest.type)).toEqual([
      'navigate', 'openPrint', 'openModal', 'closeModal', 'setVariable',
      'openPageInvocation', 'refresh', 'runPageMicroflow'
    ]);
    expect(handlers.every((item) => registry.has(item.manifest.type))).toBe(true);
    expect(registry.manifest('navigate')).toMatchObject({
      inputSchema: { required: ['path'] },
      sideEffect: 'external',
      triggers: ['click']
    });
    expect(registry.manifest('runPageMicroflow')).toMatchObject({
      inputSchema: { required: ['bindingAlias'] },
      sideEffect: 'read'
    });
  });

  it('passes a per-step signal and rejects a parent cancellation even when the handler does not settle', async () => {
    const registry = new ActionHandlerRegistry();
    let receivedSignal: AbortSignal | undefined;
    registry.register(handler('refresh', (_config, context) => {
      receivedSignal = context.signal;
      return new Promise<never>(() => undefined);
    }));
    const controller = new AbortController();
    const diagnostics = new RuntimeDiagnostics();
    const execution = registry.execute([{ id: 'step-cancel', type: 'refresh' }], { diagnostics, scopes: {}, signal: controller.signal });

    await new Promise((resolve) => setTimeout(resolve, 5));
    controller.abort('test cancellation');
    await expect(execution).rejects.toMatchObject({ kind: 'cancelled', actionId: 'step-cancel', actionType: 'refresh' });
    expect(receivedSignal).toBeDefined();
    expect(receivedSignal?.aborted).toBe(true);
    expect(diagnostics.all.at(-1)).toMatchObject({
      code: 'actionCancelled',
      path: 'actions.step-cancel',
      details: { actionId: 'step-cancel', actionType: 'refresh', failureKind: 'cancelled' }
    });
  });

  it('enforces each handler timeout and continues only when the declared policy permits it', async () => {
    const registry = new ActionHandlerRegistry();
    let completed = false;
    registry.register(handler('refresh', () => new Promise<never>(() => undefined), { errorPolicy: 'continue', timeoutMs: 5 }));
    registry.register(handler('navigate', () => {
      completed = true;
      return 'done';
    }));
    const diagnostics = new RuntimeDiagnostics();

    await expect(registry.execute([
      { id: 'step-timeout', type: 'refresh' },
      { id: 'step-after', type: 'navigate' }
    ], { diagnostics, scopes: {} })).resolves.toEqual([undefined, 'done']);
    expect(completed).toBe(true);
    expect(diagnostics.all.at(-1)).toMatchObject({
      code: 'actionTimeout',
      details: { actionId: 'step-timeout', actionType: 'refresh', errorPolicy: 'continue', failureKind: 'timeout', timeoutMs: 5 }
    });
  });

  it('applies action-level errorPolicy to handler failures and does not run later steps when it is stop', async () => {
    const registry = new ActionHandlerRegistry();
    let ranAfterFailure = false;
    registry.register(handler('refresh', () => { throw new Error('expected failure'); }, { errorPolicy: 'continue' }));
    registry.register(handler('navigate', () => {
      ranAfterFailure = true;
      return 'unexpected';
    }));
    const diagnostics = new RuntimeDiagnostics();

    await expect(registry.execute([
      { id: 'step-fail', type: 'refresh' },
      { id: 'step-after', type: 'navigate' }
    ], { diagnostics, errorPolicy: 'stop', scopes: {} })).rejects.toMatchObject({ kind: 'failed' });
    expect(ranAfterFailure).toBe(false);
    expect(diagnostics.all.at(-1)).toMatchObject({
      code: 'actionFailed',
      details: { actionId: 'step-fail', actionType: 'refresh', errorPolicy: 'stop', failureKind: 'failed' }
    });
  });

  it('blocks unknown actions with a structured diagnostic regardless of error policy', async () => {
    const registry = new ActionHandlerRegistry();
    const diagnostics = new RuntimeDiagnostics();

    await expect(registry.execute([{ id: 'step-unknown', type: 'missing.action' }], {
      diagnostics,
      errorPolicy: 'continue',
      scopes: {}
    })).rejects.toMatchObject({ kind: 'unknown' });
    expect(diagnostics.all.at(-1)).toMatchObject({
      code: 'unknownAction',
      details: { actionId: 'step-unknown', actionType: 'missing.action', errorPolicy: 'stop', failureKind: 'unknown' }
    });
  });

  it('rejects handlers without a positive timeout contract', () => {
    const registry = new ActionHandlerRegistry();
    expect(() => registry.register(handler('refresh', () => undefined, { timeoutMs: 0 }))).toThrow('positive timeoutMs');
  });

  it('rejects handlers missing the latest action manifest fields', () => {
    const registry = new ActionHandlerRegistry();
    const fields = ['inputSchema', 'outputSchema', 'permissions', 'sideEffect', 'cancelable', 'timeoutMs', 'errorPolicy', 'triggers'] as const;
    for (const field of fields) {
      const invalid = handler('refresh', () => undefined);
      const manifest = Object.fromEntries(Object.entries(invalid.manifest).filter(([key]) => key !== field));
      expect(() => registry.register({ execute: invalid.execute, manifest })).toThrow(field);
    }
  });

  it('rejects malformed permissions, side effects, and triggers instead of defaulting them', () => {
    const registry = new ActionHandlerRegistry();
    expect(() => registry.register(handler('refresh', () => undefined, { permissions: ['orders.edit', 'orders.edit'] }))).toThrow('permissions');
    expect(() => registry.register(handler('refresh', () => undefined, { sideEffect: 'unknown' as never }))).toThrow('sideEffect');
    expect(() => registry.register(handler('refresh', () => undefined, { triggers: ['click', 'click'] }))).toThrow('triggers');
  });

  it('rejects handlers outside the canonical capability contract', () => {
    const registry = new ActionHandlerRegistry();
    expect(() => registry.register(handler('missing.action', () => undefined))).toThrow('canonical capability contract');
  });

  it('enforces handler manifest permissions before invoking the handler', async () => {
    const registry = new ActionHandlerRegistry();
    let invoked = false;
    registry.register(handler('setVariable', () => { invoked = true; }, { permissions: ['orders.edit'] }));
    const diagnostics = new RuntimeDiagnostics();

    await expect(registry.execute([{ id: 'step-write', type: 'setVariable' }], { diagnostics, scopes: {}, permissions: new Set() }))
      .rejects.toMatchObject({ kind: 'permissionDenied' });
    expect(invoked).toBe(false);
    expect(diagnostics.all.at(-1)).toMatchObject({ code: 'permissionDenied', details: { permission: 'orders.edit' } });
  });

  it('does not abort a non-cancelable handler when the parent signal is cancelled', async () => {
    const registry = new ActionHandlerRegistry();
    let receivedSignal: AbortSignal | undefined;
    registry.register(handler('refresh', (_config, context) => {
      receivedSignal = context.signal;
      return 'done';
    }, { cancelable: false }));
    const controller = new AbortController();
    const execution = registry.execute([{ id: 'step-non-cancelable', type: 'refresh' }], { scopes: {}, signal: controller.signal });
    controller.abort();

    await expect(execution).resolves.toEqual(['done']);
    expect(receivedSignal?.aborted).toBe(false);
  });
});
