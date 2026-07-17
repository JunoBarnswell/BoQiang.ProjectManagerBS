export interface ConstraintParentRect { height: number; width: number }
export interface ConstraintRect { height: number; width: number; x: number; y: number }
export interface ConstraintSpec {
  bottom?: number;
  centerX?: number;
  centerY?: number;
  left?: number;
  maxHeight?: number;
  maxWidth?: number;
  minHeight?: number;
  minWidth?: number;
  right?: number;
  stretchX?: boolean;
  stretchY?: boolean;
  top?: number;
}

export type ConstraintAnchor = 'left' | 'right' | 'top' | 'bottom' | 'centerX' | 'centerY';
export type ConstraintResizeEdge = 'north' | 'west' | 'east' | 'south' | 'northwest' | 'northeast' | 'southwest' | 'southeast';
export interface ConstraintDiagnostic { code: string; path: string; message: string }
export interface ConstraintResizeResult { rect: ConstraintRect; constraints: ConstraintSpec }

const HORIZONTAL_ANCHORS: readonly ConstraintAnchor[] = ['left', 'right', 'centerX'];
const VERTICAL_ANCHORS: readonly ConstraintAnchor[] = ['top', 'bottom', 'centerY'];
const NUMBER_FIELDS: readonly (keyof ConstraintSpec)[] = ['left', 'right', 'top', 'bottom', 'centerX', 'centerY', 'minWidth', 'maxWidth', 'minHeight', 'maxHeight'];

export function createConstraintAnchors(node: ConstraintRect, parent: ConstraintParentRect, anchors: readonly ConstraintAnchor[] = ['left', 'top']): ConstraintSpec {
  const selected = anchors.length > 0 ? anchors : ['left', 'top'] as const;
  const result: ConstraintSpec = {};
  if (selected.includes('left')) result.left = node.x;
  if (selected.includes('right')) result.right = parent.width - node.x - node.width;
  if (selected.includes('top')) result.top = node.y;
  if (selected.includes('bottom')) result.bottom = parent.height - node.y - node.height;
  if (selected.includes('centerX')) result.centerX = node.x + node.width / 2 - parent.width / 2;
  if (selected.includes('centerY')) result.centerY = node.y + node.height / 2 - parent.height / 2;
  return result;
}

export function diagnoseConstraintConflicts(value: unknown, path = 'constraints'): ConstraintDiagnostic[] {
  if (!isRecord(value)) return value === undefined ? [] : [{ code: 'CONSTRAINT_SPEC_INVALID', path, message: 'constraints must be an object.' }];
  const diagnostics: ConstraintDiagnostic[] = [];
  for (const field of NUMBER_FIELDS) {
    if (value[field] !== undefined && readNumber(value[field]) === undefined) diagnostics.push({ code: 'CONSTRAINT_VALUE_INVALID', path: `${path}.${field}`, message: `${field} must be a finite number.` });
  }
  for (const field of ['stretchX', 'stretchY'] as const) {
    if (value[field] !== undefined && typeof value[field] !== 'boolean') diagnostics.push({ code: 'CONSTRAINT_VALUE_INVALID', path: `${path}.${field}`, message: `${field} must be a boolean.` });
  }
  const horizontalEdge = readNumber(value.left) !== undefined || readNumber(value.right) !== undefined;
  const verticalEdge = readNumber(value.top) !== undefined || readNumber(value.bottom) !== undefined;
  if (readNumber(value.centerX) !== undefined && horizontalEdge) diagnostics.push({ code: 'CONSTRAINT_HORIZONTAL_ANCHOR_CONFLICT', path: `${path}.centerX`, message: 'centerX cannot be combined with left or right.' });
  if (readNumber(value.centerY) !== undefined && verticalEdge) diagnostics.push({ code: 'CONSTRAINT_VERTICAL_ANCHOR_CONFLICT', path: `${path}.centerY`, message: 'centerY cannot be combined with top or bottom.' });
  if (value.stretchX === true && !(readNumber(value.left) !== undefined && readNumber(value.right) !== undefined)) diagnostics.push({ code: 'CONSTRAINT_STRETCH_X_ANCHORS_REQUIRED', path: `${path}.stretchX`, message: 'stretchX requires both left and right anchors.' });
  if (value.stretchY === true && !(readNumber(value.top) !== undefined && readNumber(value.bottom) !== undefined)) diagnostics.push({ code: 'CONSTRAINT_STRETCH_Y_ANCHORS_REQUIRED', path: `${path}.stretchY`, message: 'stretchY requires both top and bottom anchors.' });
  if (readNumber(value.minWidth) !== undefined && readNumber(value.maxWidth) !== undefined && readNumber(value.minWidth)! > readNumber(value.maxWidth)!) diagnostics.push({ code: 'CONSTRAINT_WIDTH_RANGE_INVALID', path: `${path}.minWidth`, message: 'minWidth cannot exceed maxWidth.' });
  if (readNumber(value.minHeight) !== undefined && readNumber(value.maxHeight) !== undefined && readNumber(value.minHeight)! > readNumber(value.maxHeight)!) diagnostics.push({ code: 'CONSTRAINT_HEIGHT_RANGE_INVALID', path: `${path}.minHeight`, message: 'minHeight cannot exceed maxHeight.' });
  return diagnostics;
}

export function resizeConstraintRect(rect: ConstraintRect, parent: ConstraintParentRect, value: unknown, edge: ConstraintResizeEdge, delta: { x: number; y: number }, limits: Pick<ConstraintSpec, 'minHeight' | 'minWidth' | 'maxHeight' | 'maxWidth'> = {}): ConstraintResizeResult {
  const current = normalizeConstraintSpec(value);
  if (diagnoseConstraintConflicts({ ...current, ...limits }).some((diagnostic) => diagnostic.code.endsWith('_RANGE_INVALID'))) return { rect: { ...rect }, constraints: current };
  const next: ConstraintRect = { ...rect };
  const dx = finite(delta.x);
  const dy = finite(delta.y);
  if (edge.includes('north')) { next.y += dy; next.height -= dy; }
  if (edge.includes('west')) { next.x += dx; next.width -= dx; }
  if (edge.includes('east')) next.width += dx;
  if (edge.includes('south')) next.height += dy;
  const minWidth = Math.max(1, readNumber(limits.minWidth) ?? current.minWidth ?? 1);
  const minHeight = Math.max(1, readNumber(limits.minHeight) ?? current.minHeight ?? 1);
  const maxWidth = Math.max(minWidth, readNumber(limits.maxWidth) ?? current.maxWidth ?? Number.POSITIVE_INFINITY);
  const maxHeight = Math.max(minHeight, readNumber(limits.maxHeight) ?? current.maxHeight ?? Number.POSITIVE_INFINITY);
  next.width = clamp(next.width, minWidth, maxWidth);
  next.height = clamp(next.height, minHeight, maxHeight);
  if (edge.includes('west') && next.width !== rect.width - dx) next.x = rect.x + rect.width - next.width;
  if (edge.includes('north') && next.height !== rect.height - dy) next.y = rect.y + rect.height - next.height;
  const anchors = [
    ...(HORIZONTAL_ANCHORS.some((anchor) => current[anchor] !== undefined) ? HORIZONTAL_ANCHORS.filter((anchor) => current[anchor] !== undefined) : ['left']),
    ...(VERTICAL_ANCHORS.some((anchor) => current[anchor] !== undefined) ? VERTICAL_ANCHORS.filter((anchor) => current[anchor] !== undefined) : ['top'])
  ] as ConstraintAnchor[];
  return { rect: next, constraints: { ...current, ...createConstraintAnchors(next, parent, anchors) } };
}

export function resolveConstraintRect(layout: { constraints?: unknown; height?: unknown; width?: unknown; x?: unknown; y?: unknown }, parent: ConstraintParentRect): ConstraintRect {
  const constraints = normalizeConstraintSpec(layout.constraints);
  const width = resolveSize(layout.width, constraints, parent.width, 'width');
  const height = resolveSize(layout.height, constraints, parent.height, 'height');
  return { height, width, x: resolvePosition(constraints, parent.width, width, 'x', readNumber(layout.x) ?? 0), y: resolvePosition(constraints, parent.height, height, 'y', readNumber(layout.y) ?? 0) };
}

export function normalizeConstraintSpec(value: unknown): ConstraintSpec {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const source = value as Record<string, unknown>;
  const spec: ConstraintSpec = {};
  for (const key of ['left', 'right', 'top', 'bottom', 'centerX', 'centerY', 'minWidth', 'maxWidth', 'minHeight', 'maxHeight'] as const) {
    const number = readNumber(source[key]);
    if (number !== undefined) spec[key] = number;
  }
  if (source.stretchX === true) spec.stretchX = true;
  if (source.stretchY === true) spec.stretchY = true;
  return spec;
}

function resolveSize(value: unknown, constraints: ConstraintSpec, parentSize: number, axis: 'width' | 'height'): number {
  const min = Math.max(1, readNumber(constraints[axis === 'width' ? 'minWidth' : 'minHeight']) ?? 1);
  const max = Math.max(min, readNumber(constraints[axis === 'width' ? 'maxWidth' : 'maxHeight']) ?? Number.POSITIVE_INFINITY);
  const start = axis === 'width' ? constraints.left : constraints.top;
  const end = axis === 'width' ? constraints.right : constraints.bottom;
  const stretch = axis === 'width' ? constraints.stretchX : constraints.stretchY;
  const fallback = axis === 'width' ? 160 : 48;
  const dimension = resolveDimension(value, parentSize, fallback);
  const stretched = stretch && start !== undefined && end !== undefined ? parentSize - start - end : dimension;
  return Math.min(max, Math.max(min, stretched));
}

function resolvePosition(constraints: ConstraintSpec, parentSize: number, size: number, axis: 'x' | 'y', fallback: number): number {
  const start = axis === 'x' ? constraints.left : constraints.top;
  const end = axis === 'x' ? constraints.right : constraints.bottom;
  const center = axis === 'x' ? constraints.centerX : constraints.centerY;
  if (start !== undefined) return start;
  if (end !== undefined) return parentSize - size - end;
  if (center !== undefined) return (parentSize - size) / 2 + center;
  return fallback;
}

function readNumber(value: unknown): number | undefined { return typeof value === 'number' && Number.isFinite(value) ? value : undefined; }
function finite(value: number): number { return Number.isFinite(value) ? value : 0; }
function clamp(value: number, min: number, max: number): number { return Math.min(max, Math.max(min, value)); }

function resolveDimension(value: unknown, parentSize: number, fallback: number): number {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value !== 'string') return fallback;
  const trimmed = value.trim();
  if (trimmed.endsWith('%')) return parentSize * (readNumber(Number.parseFloat(trimmed.slice(0, -1))) ?? 100) / 100;
  if (trimmed.endsWith('px')) return readNumber(Number.parseFloat(trimmed.slice(0, -2))) ?? fallback;
  return fallback;
}

function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
