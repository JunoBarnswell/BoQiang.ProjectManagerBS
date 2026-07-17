import type { RuntimeArtifact } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';
import { resolveRuntimeValue } from '../shared/runtime/runtimeExpression';

import { RuntimeDiagnostics } from './Diagnostics';
import type { RuntimeActionManifest } from './runtime-contract/RuntimeActionManifest';
import { RUNTIME_CAPABILITY_CONTRACT } from './runtime-contract/RuntimeCapabilityContract';
import { RuntimeActionError, type RuntimeActionErrorKind } from './RuntimeActionError';
import { executeRuntimePageMicroflow } from './RuntimePageMicroflow';

export interface RuntimeActionContext {
  diagnostics?: RuntimeDiagnostics;
  errorPolicy?: 'continue' | 'stop';
  formValues?: Record<string, unknown>;
  mergeVariables?: (values: Record<string, unknown>) => void;
  closeModal?: () => void;
  navigate?: (path: string) => void;
  openModal?: (modalId: string, row?: Record<string, unknown> | null, pageInputs?: Record<string, unknown>) => void;
  openPageInvocation?: (config: Record<string, unknown>) => void;
  openPrint?: (request: Record<string, unknown>) => void;
  permissions?: ReadonlySet<string>;
  isSystemAdmin?: boolean;
  refresh?: () => Promise<void>;
  setVariable?: (path: string, value: unknown) => void;
  setFormValues?: (values: Record<string, unknown>) => void;
  signal?: AbortSignal;
  scopes: Record<string, Record<string, unknown>>;
}

export interface RuntimeActionHandler {
  execute: (config: Record<string, unknown>, context: RuntimeActionContext) => Promise<unknown> | unknown;
  manifest: RuntimeActionManifest;
}

export interface RuntimeActionHandlerInput {
  execute: RuntimeActionHandler['execute'];
  manifest: unknown;
}

export interface RuntimeActionStep {
  config?: Record<string, unknown>;
  id: string;
  type: string;
}

interface StepSignal {
  dispose: () => void;
  getAbortKind: () => Extract<RuntimeActionErrorKind, 'cancelled' | 'timeout'> | null;
  signal: AbortSignal;
}

export class ActionHandlerRegistry {
  private readonly handlers = new Map<string, RuntimeActionHandler>();

  public register(handler: RuntimeActionHandlerInput): void {
    const type = isRecord(handler.manifest) && typeof handler.manifest.type === 'string' ? handler.manifest.type.trim() : '';
    if (type && !RUNTIME_CAPABILITY_CONTRACT.actions.includes(type)) {
      throw new Error(`Runtime action is not part of the canonical capability contract: ${type}`);
    }
    validateManifest(handler.manifest, type);
    if (this.handlers.has(type)) throw new Error(`Duplicate action handler: ${type}`);
    this.handlers.set(type, { ...handler, manifest: { ...handler.manifest, type } });
  }

  public has(type: string): boolean {
    return this.handlers.has(type);
  }

  public manifest(type: string): RuntimeActionManifest | undefined {
    return this.handlers.get(type)?.manifest;
  }

  public async execute(steps: readonly RuntimeActionStep[], context: RuntimeActionContext): Promise<readonly unknown[]> {
    const diagnostics = context.diagnostics ?? new RuntimeDiagnostics();
    const executionContext = { ...context, diagnostics };
    const results: unknown[] = [];

    for (const step of steps) {
      if (context.signal?.aborted) {
        const error = new RuntimeActionError('Runtime action execution was cancelled.', 'cancelled', step.id, step.type, 0);
        this.recordFailure(diagnostics, step, error, 'stop', 0);
        throw error;
      }

      if (!RUNTIME_CAPABILITY_CONTRACT.actions.includes(step.type)) {
        const error = new RuntimeActionError(`Unknown runtime action: ${step.type}`, 'unknown', step.id, step.type, 0);
        this.recordFailure(diagnostics, step, error, 'stop', 0);
        throw error;
      }

      const handler = this.handlers.get(step.type);
      if (!handler) {
        const error = new RuntimeActionError(`Unknown runtime action: ${step.type}`, 'unknown', step.id, step.type, 0);
        this.recordFailure(diagnostics, step, error, 'stop', 0);
        throw error;
      }

      const missingPermission = handler.manifest.permissions.find((permission) =>
        !context.isSystemAdmin && !context.permissions?.has(permission));
      if (missingPermission) {
        const error = new RuntimeActionError(
          `Permission is required for runtime action: ${step.type}`,
          'permissionDenied',
          step.id,
          step.type,
          handler.manifest.timeoutMs
        );
        this.recordFailure(diagnostics, step, error, 'stop', 0, { permission: missingPermission });
        throw error;
      }

      const policy = context.errorPolicy ?? handler.manifest.errorPolicy;
      const startedAt = Date.now();
      try {
        results.push(await this.executeStep(handler, step, executionContext));
      } catch (cause) {
        const error = cause instanceof RuntimeActionError
          ? cause
          : new RuntimeActionError(
            `Runtime action failed: ${step.type}${cause instanceof Error && cause.message ? `: ${cause.message}` : ''}`,
            'failed',
            step.id,
            step.type,
            handler.manifest.timeoutMs,
            cause
          );
        const durationMs = Date.now() - startedAt;
        this.recordFailure(diagnostics, step, error, policy, durationMs);
        if (error.kind === 'cancelled' || error.kind === 'unknown' || policy === 'stop') throw error;
        results.push(undefined);
      }
    }

    return results;
  }

  private async executeStep(handler: RuntimeActionHandler, step: RuntimeActionStep, context: RuntimeActionContext): Promise<unknown> {
    const stepSignal = createStepSignal(context.signal, handler.manifest.timeoutMs, handler.manifest.cancelable);
    const actionContext = { ...context, diagnostics: context.diagnostics ?? new RuntimeDiagnostics(), signal: stepSignal.signal };
    try {
      const handlerPromise = Promise.resolve().then(() => handler.execute(step.config ?? {}, actionContext));
      return await waitForAbort(handlerPromise, stepSignal, step, handler.manifest.timeoutMs);
    } catch (cause) {
      if (cause instanceof RuntimeActionError) throw cause;
      const abortKind = stepSignal.getAbortKind();
      if (abortKind) {
        throw new RuntimeActionError(
          abortKind === 'timeout'
            ? `Runtime action timed out after ${handler.manifest.timeoutMs}ms: ${step.type}`
            : `Runtime action execution was cancelled: ${step.type}`,
          abortKind,
          step.id,
          step.type,
          handler.manifest.timeoutMs,
          cause
        );
      }
      throw cause;
    } finally {
      stepSignal.dispose();
    }
  }

  private recordFailure(
    diagnostics: RuntimeDiagnostics,
    step: RuntimeActionStep,
    error: RuntimeActionError,
    errorPolicy: 'continue' | 'stop',
    durationMs: number,
    extraDetails: Readonly<Record<string, unknown>> = {}
  ): void {
    const code = error.kind === 'cancelled'
      ? 'actionCancelled'
      : error.kind === 'timeout'
        ? 'actionTimeout'
        : error.kind === 'unknown'
          ? 'unknownAction'
          : error.kind === 'permissionDenied'
            ? 'permissionDenied'
            : 'actionFailed';
    diagnostics.error(code, error.message, `actions.${step.id}`, {
      actionId: step.id,
      actionType: step.type,
      durationMs,
      errorPolicy,
      failureKind: error.kind,
      timeoutMs: error.timeoutMs,
      ...extraDetails
    });
  }
}

function validateManifest(manifest: unknown, normalizedType: string): asserts manifest is RuntimeActionManifest {
  if (!normalizedType) throw new Error('Runtime action handler type is required.');
  if (!isRecord(manifest)) throw new Error(`Runtime action handler ${normalizedType} manifest is required.`);
  if (!isRecord(manifest.inputSchema)) throw new Error(`Runtime action handler ${normalizedType} must declare inputSchema.`);
  if (!isRecord(manifest.outputSchema)) throw new Error(`Runtime action handler ${normalizedType} must declare outputSchema.`);
  if (!Array.isArray(manifest.permissions) || !manifest.permissions.every(isNonEmptyString) || new Set(manifest.permissions).size !== manifest.permissions.length) {
    throw new Error(`Runtime action handler ${normalizedType} must declare unique permissions.`);
  }
  if (manifest.sideEffect !== 'none' && manifest.sideEffect !== 'read' && manifest.sideEffect !== 'write' && manifest.sideEffect !== 'external') {
    throw new Error(`Runtime action handler ${normalizedType} must declare a valid sideEffect.`);
  }
  if (typeof manifest.cancelable !== 'boolean') throw new Error(`Runtime action handler ${normalizedType} must declare cancelable.`);
  if (manifest.errorPolicy !== 'continue' && manifest.errorPolicy !== 'stop') {
    throw new Error(`Runtime action handler ${normalizedType} must declare errorPolicy.`);
  }
  if (typeof manifest.timeoutMs !== 'number' || !Number.isFinite(manifest.timeoutMs) || manifest.timeoutMs <= 0) {
    throw new Error(`Runtime action handler ${normalizedType} must declare a positive timeoutMs.`);
  }
  if (!Array.isArray(manifest.triggers) || !manifest.triggers.every(isNonEmptyString) || new Set(manifest.triggers).size !== manifest.triggers.length) {
    throw new Error(`Runtime action handler ${normalizedType} must declare unique triggers.`);
  }
}

export function createRuntimeActionHandlers(artifact: RuntimeArtifact): readonly RuntimeActionHandler[] {
  return [
    {
      manifest: actionManifest('navigate'),
      execute: (config, context) => {
        ensureRuntimeActionActive(context.signal);
        const path = typeof config.path === 'string' ? config.path : '';
        if (!path || !context.navigate) throw new Error('navigate requires a path and runtime navigation handler');
        context.navigate(path);
      }
    },
    {
      manifest: actionManifest('openPrint'),
      execute: (config, context) => {
        ensureRuntimeActionActive(context.signal);
        if (!context.openPrint) throw new Error('openPrint requires a runtime print handler');
        context.openPrint(config);
      }
    },
    {
      manifest: actionManifest('openModal'),
      execute: (config, context) => {
        ensureRuntimeActionActive(context.signal);
        const modalId = typeof config.modalId === 'string' ? config.modalId : '';
        if (!modalId || !context.openModal) throw new Error('openModal requires a modalId and runtime modal handler');
        context.openModal(modalId, asRecord(config.row), asRecord(config.pageInputs));
      }
    },
    {
      manifest: actionManifest('closeModal'),
      execute: (_config, context) => {
        ensureRuntimeActionActive(context.signal);
        if (!context.closeModal) throw new Error('closeModal requires a runtime modal handler');
        context.closeModal();
      }
    },
    {
      manifest: actionManifest('setVariable'),
      execute: (config, context) => {
        ensureRuntimeActionActive(context.signal);
        const target = typeof config.target === 'string' ? config.target : '';
        if (!target || !context.setVariable) throw new Error('setVariable requires a target and runtime state');
        context.setVariable(target, resolveRuntimeValue(config.value, {
          form: context.scopes.form,
          page: context.scopes.page,
          variables: context.scopes.variables
        }));
      }
    },
    {
      manifest: actionManifest('openPageInvocation'),
      execute: (config, context) => {
        ensureRuntimeActionActive(context.signal);
        if (!context.openPageInvocation) throw new Error('openPageInvocation requires a runtime navigation handler');
        context.openPageInvocation(config);
      }
    },
    {
      manifest: actionManifest('refresh'),
      execute: async (_config, context) => {
        ensureRuntimeActionActive(context.signal);
        if (!context.refresh) throw new Error('refresh requires a runtime refresh handler');
        await context.refresh();
        ensureRuntimeActionActive(context.signal);
      }
    },
    {
      manifest: actionManifest('runPageMicroflow'),
      execute: async (config, context) => {
        ensureRuntimeActionActive(context.signal);
        const alias = typeof config.bindingAlias === 'string' ? config.bindingAlias : '';
        return executeRuntimePageMicroflow(artifact, alias, context);
      }
    }
  ];
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}

function createStepSignal(parent: AbortSignal | undefined, timeoutMs: number, cancelable: boolean): StepSignal {
  const controller = new AbortController();
  let abortKind: Extract<RuntimeActionErrorKind, 'cancelled' | 'timeout'> | null = null;
  let timer: ReturnType<typeof setTimeout> | undefined;

  const abort = (kind: Extract<RuntimeActionErrorKind, 'cancelled' | 'timeout'>): void => {
    if (controller.signal.aborted) return;
    abortKind = kind;
    controller.abort(kind);
  };
  const onParentAbort = (): void => abort('cancelled');

  if (cancelable) {
    if (parent?.aborted) abort('cancelled');
    else parent?.addEventListener('abort', onParentAbort, { once: true });
  }
  timer = setTimeout(() => abort('timeout'), timeoutMs);

  return {
    dispose: () => {
      if (timer !== undefined) clearTimeout(timer);
      if (cancelable) parent?.removeEventListener('abort', onParentAbort);
    },
    getAbortKind: () => abortKind,
    signal: controller.signal
  };
}

async function waitForAbort<T>(promise: Promise<T>, stepSignal: StepSignal, step: RuntimeActionStep, timeoutMs: number): Promise<T> {
  if (stepSignal.signal.aborted) throw createAbortError(stepSignal, step, timeoutMs);
  let onAbort: (() => void) | undefined;
  const abortPromise = new Promise<never>((_, reject) => {
    onAbort = () => reject(createAbortError(stepSignal, step, timeoutMs));
    stepSignal.signal.addEventListener('abort', onAbort, { once: true });
  });
  try {
    return await Promise.race([promise, abortPromise]);
  } finally {
    if (onAbort) stepSignal.signal.removeEventListener('abort', onAbort);
  }
}

function createAbortError(stepSignal: StepSignal, step: RuntimeActionStep, timeoutMs: number): RuntimeActionError {
  const kind = stepSignal.getAbortKind() ?? 'cancelled';
  return new RuntimeActionError(
    kind === 'timeout' ? `Runtime action timed out after ${timeoutMs}ms: ${step.type}` : `Runtime action execution was cancelled: ${step.type}`,
    kind,
    step.id,
    step.type,
    timeoutMs
  );
}

function ensureRuntimeActionActive(signal: AbortSignal | undefined): void {
  if (signal?.aborted) throw new DOMException('Runtime action execution was cancelled.', 'AbortError');
}

function actionManifest(type: string): RuntimeActionManifest {
  const manifest = RUNTIME_CAPABILITY_CONTRACT.actionManifests[type];
  if (!manifest) throw new Error(`Runtime action manifest is not defined for latest action: ${type}`);
  return { ...manifest, type };
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return isRecord(value) ? value : undefined;
}
