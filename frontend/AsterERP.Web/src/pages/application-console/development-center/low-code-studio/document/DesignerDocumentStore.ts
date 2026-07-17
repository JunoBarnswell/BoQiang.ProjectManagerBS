import { hasBlockingDesignerDiagnostics, type DesignerCommand, type DesignerCommandExecutor, type DesignerCommandResult } from '../commands/DesignerCommand';
import { createDesignerDocumentPatch, createInverseDesignerCommand, isDesignerDocumentPatchEmpty } from '../commands/DesignerDocumentPatch';

import type { DesignerDocument } from './DesignerDocument';
import { validateDesignerDocument, validateDesignerDocumentBoundary } from './DesignerDocumentCodec';
import { canonicalizeDesignerDocument, computeDesignerDocumentHash } from './DesignerDocumentHash';
import { DesignerDocumentParseError } from './DesignerDocumentParseError';

export type DesignerDocumentSelector<T> = (document: DesignerDocument) => T;
export type DesignerDocumentListener = (result: DesignerCommandResult) => void;

export class DesignerDocumentStore implements DesignerCommandExecutor {
  private current: DesignerDocument;
  private readonly listeners = new Set<DesignerDocumentListener>();

  public constructor(document: DesignerDocument) {
    const boundaryErrors = validateDesignerDocumentBoundary(document);
    const diagnostics = [...boundaryErrors, ...validateDesignerDocument(document)];
    if (diagnostics.length > 0) throw new DesignerDocumentParseError(null, [...new Set(diagnostics)]);
    this.current = freezeDocument(withHash(document));
  }

  public getSnapshot(): DesignerDocument {
    return this.current;
  }

  public getCanonicalSnapshot(): string {
    return canonicalizeDesignerDocument(this.current);
  }

  public select<T>(selector: DesignerDocumentSelector<T>): T {
    return selector(this.current);
  }

  public subscribe(listener: DesignerDocumentListener): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  public execute(command: DesignerCommand): DesignerCommandResult {
    const result = command.execute({ document: this.current });
    if (!result.changed || hasBlockingDesignerDiagnostics(result.diagnostics)) {
      return { ...result, document: this.current };
    }
    if (isDesignerDocumentPatchEmpty(createDesignerDocumentPatch(this.current, result.document))) {
      return { changed: false, diagnostics: [], document: this.current };
    }

    return this.commit(result, this.current.revision + 1);
  }

  public executeTransaction(commands: readonly DesignerCommand[]): DesignerCommandResult {
    if (commands.length === 0) {
      return { changed: false, diagnostics: [], document: this.current };
    }
    const previous = this.current;
    let candidate = previous;
    for (const command of commands) {
      const result = command.execute({ document: candidate });
      if (!result.changed || hasBlockingDesignerDiagnostics(result.diagnostics)) {
        return { changed: false, diagnostics: result.diagnostics, document: this.current };
      }
      candidate = result.document;
    }
    if (isDesignerDocumentPatchEmpty(createDesignerDocumentPatch(previous, candidate))) {
      return { changed: false, diagnostics: [], document: this.current };
    }
    const inverse = createInverseDesignerCommand(previous, candidate, `transaction:${commands.map((command) => command.id).join('+')}`, 'transaction');
    return this.commit({ changed: true, diagnostics: [], document: candidate, inverse }, previous.revision + 1);
  }

  private commit(result: DesignerCommandResult, revision: number): DesignerCommandResult {
    const candidate = { ...result.document, revision };
    const diagnostics = validateDesignerDocument(candidate);
    if (diagnostics.length > 0) return { changed: false, diagnostics, document: this.current };
    this.current = freezeDocument(withHash(candidate));
    const committed = { ...result, document: this.current };
    this.listeners.forEach((listener) => listener(committed));
    return committed;
  }
}

function freezeDocument(document: DesignerDocument): DesignerDocument {
  const copy = structuredClone(document);
  return deepFreeze(copy);
}

function withHash(document: DesignerDocument): DesignerDocument {
  return { ...document, documentHash: computeDesignerDocumentHash(document) };
}

function deepFreeze<T>(value: T): T {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.freeze(value);
    for (const child of Object.values(value as Record<string, unknown>)) deepFreeze(child);
  }
  return value;
}
