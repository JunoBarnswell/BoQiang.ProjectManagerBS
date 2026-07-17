import { sendApplicationMonitoringEvent } from '../../../../../api/application-development-center/applicationMonitoring.api';
import type {
  DesignerCommandContext as MonitoringDesignerCommandContext,
  RuntimeMonitoringContext,
  RuntimeMonitoringEvent
} from '../../../../../runtime-kernel/RuntimeMonitoringContract';
import type { DesignerDocument } from '../document/DesignerDocument';
import { DesignerDocumentStore } from '../document/DesignerDocumentStore';

import { hasBlockingDesignerDiagnostics, type DesignerCommand, type DesignerCommandResult } from './DesignerCommand';
import {
  createDesignerDocumentPatch,
  createDesignerDocumentPatchCommand,
  invertDesignerDocumentPatch,
  isDesignerDocumentPatchEmpty,
  isDesignerPatchCommand,
  type DesignerPatchCommand
} from './DesignerDocumentPatch';

interface HistoryEntry {
  command: DesignerPatchCommand;
  inverse: DesignerPatchCommand;
}

const COMMAND_MERGE_WINDOW_MS = 500;

export interface DesignerCommandBusOptions {
  monitoringContext?: RuntimeMonitoringContext;
  onMonitoringEvent?: (event: RuntimeMonitoringEvent) => void;
}

export class DesignerCommandBus {
  private readonly store: DesignerDocumentStore;
  private readonly past: HistoryEntry[] = [];
  private readonly future: HistoryEntry[] = [];
  private readonly monitoringContext: RuntimeMonitoringContext;
  private readonly onMonitoringEvent: (event: RuntimeMonitoringEvent) => void;
  private transaction: { key: string; before: DesignerDocument; lastAt: number } | null = null;

  public constructor(document: DesignerDocument, options: DesignerCommandBusOptions = {}) {
    this.store = new DesignerDocumentStore(document);
    this.monitoringContext = options.monitoringContext ?? {};
    this.onMonitoringEvent = options.onMonitoringEvent ?? ((event) => {
      void sendApplicationMonitoringEvent(event).catch(() => undefined);
    });
  }

  public get document(): DesignerDocument {
    return this.store.getSnapshot();
  }

  public get canUndo(): boolean {
    return this.transaction !== null || this.past.length > 0;
  }

  public get canRedo(): boolean {
    return this.future.length > 0;
  }

  public subscribe(listener: () => void): () => void {
    return this.store.subscribe(() => listener());
  }

  public execute(command: DesignerCommand): DesignerCommandResult {
    const startedAt = performance.now();
    const canMerge = Boolean(command.mergeKey)
      && this.transaction !== null
      && this.transaction.key === command.mergeKey
      && startedAt - this.transaction.lastAt <= COMMAND_MERGE_WINDOW_MS;
    if (!canMerge) this.flushTransaction();

    const before = this.document;
    const result = this.store.execute(command);
    if (!result.changed || hasBlockingDesignerDiagnostics(result.diagnostics)) {
      this.emitCommandEvent(command.id, command.id, result, startedAt);
      return result;
    }

    const patch = createDesignerDocumentPatch(before, result.document);
    const inverse = isDesignerPatchCommand(result.inverse)
      ? result.inverse
      : createDesignerDocumentPatchCommand(`${command.id}:inverse`, `Undo ${command.label}`, invertDesignerDocumentPatch(patch));
    const reversibleResult = { ...result, inverse };
    this.future.length = 0;
    if (command.mergeKey) {
      if (!canMerge) {
        this.transaction = { key: command.mergeKey, before, lastAt: startedAt };
      } else if (this.transaction) {
        this.transaction.lastAt = startedAt;
      }
    } else {
      this.flushTransaction();
      this.past.push({ command: createDesignerDocumentPatchCommand(command.id, command.label, patch), inverse });
    }
    this.emitCommandEvent(command.id, command.id, reversibleResult, startedAt);
    return reversibleResult;
  }

  public endTransaction(): void {
    this.flushTransaction();
  }

  public executeTransaction(commands: readonly DesignerCommand[]): DesignerCommandResult {
    const startedAt = performance.now();
    this.flushTransaction();
    const before = this.document;
    const result = this.store.executeTransaction(commands);
    if (result.changed) {
      this.future.length = 0;
      const patch = createDesignerDocumentPatch(before, result.document);
      const id = `transaction:${commands.map((command) => command.id).join('+')}`;
      const inverse = isDesignerPatchCommand(result.inverse)
        ? result.inverse
        : createDesignerDocumentPatchCommand(`${id}:inverse`, 'Undo transaction', invertDesignerDocumentPatch(patch));
      this.past.push({ command: createDesignerDocumentPatchCommand(id, 'transaction', patch), inverse });
      this.emitCommandEvent(id, 'transaction', { ...result, inverse }, startedAt);
      return { ...result, inverse };
    }
    const id = `transaction:${commands.map((command) => command.id).join('+')}`;
    this.emitCommandEvent(id, 'transaction', result, startedAt);
    return result;
  }

  public undo(): DesignerCommandResult | null {
    const startedAt = performance.now();
    this.flushTransaction();
    const entry = this.past.pop();
    if (!entry) return null;
    const result = this.store.execute(entry.inverse);
    if (result.changed && !hasBlockingDesignerDiagnostics(result.diagnostics)) this.future.unshift(entry);
    else this.past.push(entry);
    this.emitCommandEvent(entry.inverse.id, 'undo', result, startedAt);
    return result;
  }

  public redo(): DesignerCommandResult | null {
    const startedAt = performance.now();
    this.flushTransaction();
    const entry = this.future.shift();
    if (!entry) return null;
    const result = this.store.execute(entry.command);
    if (result.changed && !hasBlockingDesignerDiagnostics(result.diagnostics)) this.past.push(entry);
    else this.future.unshift(entry);
    this.emitCommandEvent(entry.command.id, 'redo', result, startedAt);
    return result;
  }

  private flushTransaction(): void {
    if (this.transaction) {
      const patch = createDesignerDocumentPatch(this.transaction.before, this.document);
      if (!isDesignerDocumentPatchEmpty(patch)) {
        const id = `transaction:${this.transaction.key}`;
        this.past.push({
          command: createDesignerDocumentPatchCommand(id, `transaction ${this.transaction.key}`, patch),
          inverse: createDesignerDocumentPatchCommand(`${id}:inverse`, `Undo transaction ${this.transaction.key}`, invertDesignerDocumentPatch(patch))
        });
      }
    }
    this.transaction = null;
  }

  private emitCommandEvent(commandId: string, commandType: string, result: DesignerCommandResult, startedAt: number): void {
    const outcome = result.changed && !hasBlockingDesignerDiagnostics(result.diagnostics) ? 'succeeded' : 'failed';
    const context: MonitoringDesignerCommandContext = {
      ...this.monitoringContext,
      commandId,
      commandType
    };
    const event = {
      cancellationRequested: false,
      context,
      durationMs: Math.max(0, performance.now() - startedAt),
      ...(outcome === 'failed' ? { errorCode: result.diagnostics[0] ?? 'commandRejected' } : {}),
      eventId: `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`,
      eventName: outcome === 'succeeded' ? 'designer.command' : 'designer.command.failed',
      occurredAt: new Date().toISOString(),
      outcome
    } as RuntimeMonitoringEvent;
    try {
      this.onMonitoringEvent(event);
    } catch {
      // Monitoring must never alter document transaction semantics.
    }
  }
}
