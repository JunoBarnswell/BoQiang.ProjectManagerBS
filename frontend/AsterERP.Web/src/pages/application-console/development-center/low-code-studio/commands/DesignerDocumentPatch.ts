import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

import type { DesignerCommand } from './DesignerCommand';

const CONTENT_FIELDS = [
  'actions',
  'apiBindings',
  'dataSources',
  'documentId',
  'metadata',
  'modals',
  'pageMicroflows',
  'pageParameters',
  'pages',
  'pageType',
  'permissions',
  'runtimeContext',
  'styleTokens',
  'variables',
  'workflowBindings'
] as const satisfies readonly (keyof DesignerDocument)[];

type DesignerDocumentContentField = (typeof CONTENT_FIELDS)[number];

export interface DesignerDocumentNodeChange {
  readonly id: string;
  readonly before: DesignerDocumentNode | null;
  readonly after: DesignerDocumentNode | null;
}

export interface DesignerDocumentFieldChange {
  readonly key: DesignerDocumentContentField;
  readonly beforePresent: boolean;
  readonly afterPresent: boolean;
  readonly before: unknown;
  readonly after: unknown;
}

export interface DesignerDocumentPatch {
  readonly nodeChanges: readonly DesignerDocumentNodeChange[];
  readonly fieldChanges: readonly DesignerDocumentFieldChange[];
}

export interface DesignerPatchCommand extends DesignerCommand {
  readonly patch: DesignerDocumentPatch;
}

export interface DesignerPatchApplication {
  readonly changed: boolean;
  readonly diagnostics: readonly string[];
  readonly document?: DesignerDocument;
}

export function createDesignerDocumentPatch(before: DesignerDocument, after: DesignerDocument): DesignerDocumentPatch {
  const nodeIds = new Set([...Object.keys(before.elements), ...Object.keys(after.elements)]);
  const nodeChanges: DesignerDocumentNodeChange[] = [];
  for (const id of nodeIds) {
    const left = before.elements[id] ?? null;
    const right = after.elements[id] ?? null;
    if (left !== right && !sameValue(left, right)) {
      nodeChanges.push({ id, before: cloneNode(left), after: cloneNode(right) });
    }
  }

  const fieldChanges: DesignerDocumentFieldChange[] = [];
  const beforeFields = before as unknown as Record<string, unknown>;
  const afterFields = after as unknown as Record<string, unknown>;
  for (const key of CONTENT_FIELDS) {
    const beforePresent = Object.prototype.hasOwnProperty.call(beforeFields, key);
    const afterPresent = Object.prototype.hasOwnProperty.call(afterFields, key);
    if (beforePresent === afterPresent && (beforeFields[key] === afterFields[key] || sameValue(beforeFields[key], afterFields[key]))) continue;
    fieldChanges.push({
      key,
      beforePresent,
      afterPresent,
      before: cloneValue(beforeFields[key]),
      after: cloneValue(afterFields[key])
    });
  }
  return { fieldChanges, nodeChanges };
}

export function isDesignerDocumentPatchEmpty(patch: DesignerDocumentPatch): boolean {
  return patch.nodeChanges.length === 0 && patch.fieldChanges.length === 0;
}

export function isDesignerPatchCommand(command: DesignerCommand | undefined): command is DesignerPatchCommand {
  return Boolean(command && 'patch' in command && command.patch !== undefined);
}

export function invertDesignerDocumentPatch(patch: DesignerDocumentPatch): DesignerDocumentPatch {
  return {
    fieldChanges: patch.fieldChanges.map((change) => ({
      key: change.key,
      beforePresent: change.afterPresent,
      afterPresent: change.beforePresent,
      before: cloneValue(change.after),
      after: cloneValue(change.before)
    })),
    nodeChanges: patch.nodeChanges.map((change) => ({
      id: change.id,
      before: cloneNode(change.after),
      after: cloneNode(change.before)
    }))
  };
}

export function createInverseDesignerCommand(
  before: DesignerDocument,
  after: DesignerDocument,
  commandId: string,
  label: string
): DesignerPatchCommand {
  const patch = invertDesignerDocumentPatch(createDesignerDocumentPatch(before, after));
  return createDesignerDocumentPatchCommand(`${commandId}:inverse`, `Undo ${label}`, patch);
}

export function createDesignerDocumentPatchCommand(
  id: string,
  label: string,
  patch: DesignerDocumentPatch
): DesignerPatchCommand {
  return {
    id,
    label,
    patch,
    execute: ({ document }) => {
      const result = applyDesignerDocumentPatch(document, patch);
      return result.document
        ? { changed: result.changed, diagnostics: result.diagnostics, document: result.document }
        : { changed: false, diagnostics: result.diagnostics, document };
    }
  };
}

export function applyDesignerDocumentPatch(document: DesignerDocument, patch: DesignerDocumentPatch): DesignerPatchApplication {
  const diagnostics: string[] = [];
  const fields = document as unknown as Record<string, unknown>;
  for (const change of patch.fieldChanges) {
    const present = Object.prototype.hasOwnProperty.call(fields, change.key);
    if (present !== change.beforePresent || !sameValue(fields[change.key], change.before)) {
      diagnostics.push(`Document field conflict: ${change.key}`);
    }
  }
  for (const change of patch.nodeChanges) {
    const current = document.elements[change.id] ?? null;
    if (!sameValue(current, change.before)) diagnostics.push(`Node conflict: ${change.id}`);
  }
  if (diagnostics.length > 0) return { changed: false, diagnostics };

  const elements = { ...document.elements };
  for (const change of patch.nodeChanges) {
    if (change.after === null) delete elements[change.id];
    else elements[change.id] = cloneNode(change.after)!;
  }

  const next = { ...document, elements };
  const mutable = next as unknown as Record<string, unknown>;
  for (const change of patch.fieldChanges) {
    if (change.afterPresent) mutable[change.key] = cloneValue(change.after);
    else delete mutable[change.key];
  }
  return { changed: patch.nodeChanges.length > 0 || patch.fieldChanges.length > 0, diagnostics: [], document: next };
}

function cloneNode(node: DesignerDocumentNode | null): DesignerDocumentNode | null {
  return node === null ? null : structuredClone(node);
}

function cloneValue(value: unknown): unknown {
  return value === undefined ? undefined : structuredClone(value);
}

function sameValue(left: unknown, right: unknown): boolean {
  return stableValue(left) === stableValue(right);
}

function stableValue(value: unknown, active = new WeakSet<object>()): string {
  if (value === undefined) return 'undefined';
  if (value === null || typeof value !== 'object') return JSON.stringify(value);
  if (active.has(value)) return '[Circular]';
  active.add(value);
  if (Array.isArray(value)) {
    const result = `[${value.map((item) => stableValue(item, active)).join(',')}]`;
    active.delete(value);
    return result;
  }
  const record = value as Record<string, unknown>;
  const result = `{${Object.keys(record).sort().map((key) => `${JSON.stringify(key)}:${stableValue(record[key], active)}`).join(',')}}`;
  active.delete(value);
  return result;
}
