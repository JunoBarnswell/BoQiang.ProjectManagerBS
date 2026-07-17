import { resolveConstraintRect, normalizeConstraintSpec } from '../pages/application-console/development-center/low-code-studio/layout/constraintModel';
import { resolveLayoutMode, resolveLayoutStyle, type LayoutMode } from '../pages/application-console/development-center/low-code-studio/layout/layoutOperations';

export type RuntimeLayoutRecord = Record<string, unknown>;

export interface RuntimeLayoutBox {
  height: number;
  width: number;
  x: number;
  y: number;
}

export interface RuntimeLayoutProjectionDiagnostic {
  code: 'invalid-constraint' | 'invalid-dimension' | 'invalid-position';
  field: string;
  message: string;
}

export interface RuntimeLayoutProjectionInput {
  box?: RuntimeLayoutBox;
  forceAbsolute?: boolean;
  layout: RuntimeLayoutRecord;
  parentBox?: Pick<RuntimeLayoutBox, 'height' | 'width'>;
  parentLayout?: RuntimeLayoutRecord;
  siblingIndex?: number;
  siblingLayouts?: readonly RuntimeLayoutRecord[];
  style?: RuntimeLayoutRecord;
}

export interface RuntimeLayoutProjection {
  box?: RuntimeLayoutBox;
  diagnostics: readonly RuntimeLayoutProjectionDiagnostic[];
  mode: LayoutMode;
  style: RuntimeLayoutRecord;
}

const DEFAULT_CHILD_HEIGHT = 48;
const DEFAULT_CHILD_WIDTH = 160;
const DIMENSION_FIELDS = ['height', 'maxHeight', 'maxWidth', 'minHeight', 'minWidth', 'width'] as const;
const POSITION_FIELDS = ['x', 'y'] as const;
const UNIT_LESS_STYLE_PROPERTIES = new Set(['animationIterationCount', 'aspectRatio', 'borderImageOutset', 'borderImageSlice', 'borderImageWidth', 'boxFlex', 'boxFlexGroup', 'boxOrdinalGroup', 'columnCount', 'columns', 'fillOpacity', 'flex', 'flexGrow', 'flexPositive', 'flexShrink', 'flexNegative', 'fontWeight', 'gridArea', 'gridColumn', 'gridRow', 'lineClamp', 'lineHeight', 'opacity', 'order', 'orphans', 'scale', 'stopOpacity', 'strokeDasharray', 'strokeDashoffset', 'strokeMiterlimit', 'strokeOpacity', 'strokeWidth', 'tabSize', 'widows', 'zIndex', 'zoom']);

export function projectRuntimeLayout(input: RuntimeLayoutProjectionInput): RuntimeLayoutProjection {
  const layout = toRuntimeLayout(input.layout ?? {});
  const style = input.style ?? {};
  const parentLayout = input.parentLayout ? toRuntimeLayout(input.parentLayout) : undefined;
  const mode = resolveLayoutMode(layout);
  const diagnostics = collectDiagnostics(layout);
  const box = input.box ?? (input.parentBox ? resolveRuntimeLayoutBox(layout, input.parentBox, parentLayout, input.siblingLayouts, input.siblingIndex ?? 0) : undefined);
  const parentMode = parentLayout ? resolveLayoutMode(parentLayout) : undefined;
  const absolute = input.forceAbsolute ?? shouldUseAbsolutePosition(layout, parentMode);
  const projected: RuntimeLayoutRecord = {
    ...resolveLayoutStyle(layout),
    ...projectCssValues(style),
    boxSizing: 'border-box'
  };
  applyLayoutVisualValues(projected, layout);
  if (mode === 'flex') applyFlexContainerStyle(projected, layout);
  if (parentMode === 'flex') applyFlexItemStyle(projected, layout);
  if (mode === 'grid') applyGridContainerStyle(projected, layout);
  if (parentMode === 'grid') applyGridChildPlacement(projected, layout, input.parentLayout ?? {}, input.siblingLayouts, input.siblingIndex ?? 0);

  if (absolute) {
    projected.position = 'absolute';
    applyAbsoluteBox(projected, layout, box);
    if (!box) applyConstraintPosition(projected, layout);
  } else {
    projected.position = 'relative';
    applyFlowBox(projected, layout, box);
  }
  return { box, diagnostics, mode, style: projected };
}

export function resolveRuntimeLayoutBox(
  layout: RuntimeLayoutRecord,
  parentBox: Pick<RuntimeLayoutBox, 'height' | 'width'>,
  parentLayout?: RuntimeLayoutRecord,
  siblingLayouts: readonly RuntimeLayoutRecord[] = [],
  siblingIndex = 0,
  root = false
): RuntimeLayoutBox {
  layout = toRuntimeLayout(layout);
  parentLayout = parentLayout ? toRuntimeLayout(parentLayout) : parentLayout;
  const parent = { height: safeSize(parentBox.height, 1), width: safeSize(parentBox.width, 1) };
  const parentMode = parentLayout ? resolveLayoutMode(parentLayout) : undefined;
  if (parentMode === 'flex') return resolveFlexBox(layout, parentLayout ?? {}, parent, siblingLayouts, siblingIndex);
  if (parentMode === 'grid') return resolveGridBox(layout, parentLayout ?? {}, parent, siblingLayouts, siblingIndex);
  if (parentMode === 'constraints') {
    const resolved = resolveConstraintRect({ ...layout, constraints: normalizeRuntimeConstraintSpec(layout.constraints) }, parent);
    return { height: resolved.height, width: resolved.width, x: resolved.x, y: resolved.y };
  }
  return {
    height: resolveDimension(layout.height, root ? readNumber(layout.minHeight) ?? parent.height : DEFAULT_CHILD_HEIGHT, parent.height),
    width: resolveDimension(layout.width, root ? parent.width : DEFAULT_CHILD_WIDTH, parent.width),
    x: readNumber(layout.x) ?? 0,
    y: readNumber(layout.y) ?? 0
  };
}

function toRuntimeLayout(layout: RuntimeLayoutRecord): RuntimeLayoutRecord {
  const protocol = readCanonicalLayoutProtocol(layout);
  if (!protocol) return layout;
  const mode = protocol.container.mode;
  const size = protocol.size;
  const result: RuntimeLayoutRecord = {
    display: mode === 'flex' ? 'flex' : mode === 'grid' ? 'grid' : mode === 'constraints' ? 'block' : 'block',
    layoutMode: mode
  };
  for (const key of ['height', 'width', 'minHeight', 'maxHeight', 'minWidth', 'maxWidth'] as const) {
    if (isRuntimeDimension(size[key])) result[key] = size[key];
  }
  if (mode === 'free') {
    const absolute = isRecord(protocol.placement.absolute) ? protocol.placement.absolute : {};
    result.position = 'absolute';
    result.x = absolute.x ?? 0;
    result.y = absolute.y ?? 0;
    if (absolute.zIndex !== undefined) result.zIndex = absolute.zIndex;
  }
  if (mode === 'constraints') result.constraints = protocol.placement.constrained ?? {};
  if (mode === 'flex') {
    const flex = protocol.container.flex;
    const item = isRecord(protocol.placement.flexItem) ? protocol.placement.flexItem : {};
    if (flex) {
      result.flexDirection = flex.direction;
      result.flexWrap = flex.wrap;
      result.gap = flex.gap;
      result.alignItems = flex.alignItems;
      result.justifyContent = flex.justifyContent;
    }
    if (item) {
      result.order = item.order;
      result.flexGrow = item.grow;
      result.flexShrink = item.shrink;
      result.flexBasis = item.basis;
      if (item.alignSelf !== undefined) result.alignSelf = item.alignSelf;
    }
  }
  if (mode === 'grid') {
    const grid = protocol.container.grid;
    const item = isRecord(protocol.placement.gridItem) ? protocol.placement.gridItem : {};
    if (grid) {
      result.gridTemplateColumns = Array.isArray(grid.columns) ? grid.columns.join(' ') : undefined;
      result.gridTemplateRows = Array.isArray(grid.rows) ? grid.rows.join(' ') : undefined;
      result.columnGap = grid.columnGap;
      result.rowGap = grid.rowGap;
      result.gridAutoFlow = grid.autoFlow;
    }
    if (item) {
      result.gridColumn = item.columnStart;
      result.gridRow = item.rowStart;
      result.gridColumnSpan = item.columnSpan;
      result.gridRowSpan = item.rowSpan;
      if (item.alignSelf !== undefined) result.alignSelf = item.alignSelf;
      if (item.justifySelf !== undefined) result.justifySelf = item.justifySelf;
    }
  }
  return result;
}

function readCanonicalLayoutProtocol(layout: RuntimeLayoutRecord): { container: { mode: LayoutMode; flex?: RuntimeLayoutRecord; grid?: RuntimeLayoutRecord }; placement: RuntimeLayoutRecord; size: RuntimeLayoutRecord } | null {
  const source = isRecord(layout.protocol) ? layout.protocol : isRecord(layout.layoutProtocol) ? layout.layoutProtocol : layout;
  if (!isRecord(source) || !isRecord(source.container) || !isRecord(source.placement) || !isRecord(source.size)) return null;
  const mode = source.container.mode;
  if (mode !== 'free' && mode !== 'flex' && mode !== 'grid' && mode !== 'constraints') return null;
  return { container: { mode, ...(isRecord(source.container.flex) ? { flex: source.container.flex } : {}), ...(isRecord(source.container.grid) ? { grid: source.container.grid } : {}) }, placement: source.placement, size: source.size };
}

type FlexDirection = 'row' | 'row-reverse' | 'column' | 'column-reverse';
type FlexWrap = 'nowrap' | 'wrap' | 'wrap-reverse';
type FlexAlignment = 'flex-start' | 'center' | 'flex-end' | 'stretch';

interface FlexItemBox {
  alignSelf: FlexAlignment | 'auto';
  crossSize: number;
  flexGrow: number;
  flexShrink: number;
  hasExplicitCrossSize: boolean;
  height: number;
  mainSize: number;
  order: number;
  sourceIndex: number;
  width: number;
}

interface FlexLine {
  crossSize: number;
  items: FlexItemBox[];
}

function resolveFlexBox(layout: RuntimeLayoutRecord, parentLayout: RuntimeLayoutRecord, parent: Pick<RuntimeLayoutBox, 'height' | 'width'>, siblings: readonly RuntimeLayoutRecord[], index: number): RuntimeLayoutBox {
  const direction = readFlexDirection(parentLayout.flexDirection);
  const wrap = readFlexWrap(parentLayout.flexWrap ?? parentLayout.wrap);
  const rowGap = resolveFlexGap(parentLayout.rowGap, parentLayout.gap);
  const columnGap = resolveFlexGap(parentLayout.columnGap, parentLayout.gap);
  const mainGap = direction.startsWith('row') ? columnGap : rowGap;
  const crossGap = direction.startsWith('row') ? rowGap : columnGap;
  const availableMain = direction.startsWith('row') ? parent.width : parent.height;
  const availableCross = direction.startsWith('row') ? parent.height : parent.width;
  const sourceLayouts = siblings.length > 0 ? siblings : [layout];
  const items = sourceLayouts.map((candidate, sourceIndex) => createFlexItem(candidate, sourceIndex, direction, parent, availableMain));
  const visualItems = [...items].sort((left, right) => left.order - right.order || left.sourceIndex - right.sourceIndex);
  const lines = createFlexLines(visualItems, availableMain, availableCross, mainGap, wrap);
  resolveFlexLineSizes(lines, availableMain, mainGap, direction);
  const linePositions = resolveFlexLinePositions(lines, availableCross, crossGap, parentLayout.alignContent, wrap === 'wrap-reverse');
  const target = items.find((item) => item.sourceIndex === index) ?? items[index] ?? createFlexItem({}, index, direction, parent, availableMain);
  const targetLine = lines.find((line) => line.items.includes(target));
  if (!targetLine) return { height: target.height, width: target.width, x: 0, y: 0 };
  const lineIndex = lines.indexOf(targetLine);
  const mainPosition = resolveFlexMainPosition(targetLine, target, availableMain, mainGap, parentLayout.justifyContent);
  const lineStart = linePositions[lineIndex] ?? 0;
  const crossPosition = resolveFlexCrossPosition(target, targetLine, lineStart, wrap === 'wrap-reverse', direction, parentLayout.alignItems);
  const reversedMain = direction === 'row-reverse' || direction === 'column-reverse';
  const mainCoordinate = reversedMain
    ? availableMain - mainPosition - target.mainSize
    : mainPosition;
  return direction.startsWith('row')
    ? { height: target.height, width: target.width, x: mainCoordinate, y: crossPosition }
    : { height: target.height, width: target.width, x: crossPosition, y: mainCoordinate };
}

function createFlexItem(layout: RuntimeLayoutRecord, sourceIndex: number, direction: FlexDirection, parent: Pick<RuntimeLayoutBox, 'height' | 'width'>, availableMain: number): FlexItemBox {
  const row = direction.startsWith('row');
  const width = resolveDimension(layout.width, DEFAULT_CHILD_WIDTH, parent.width);
  const height = resolveDimension(layout.height, DEFAULT_CHILD_HEIGHT, parent.height);
  const hasExplicitCrossSize = row ? layout.height !== undefined : layout.width !== undefined;
  const crossSize = Math.max(1, row ? height : width);
  const basisValue = readFlexBasis(layout.flexBasis, row ? width : height, availableMain);
  const minMain = resolveFlexLimit(row ? layout.minWidth : layout.minHeight, 1, availableMain);
  const maxMain = resolveFlexLimit(row ? layout.maxWidth : layout.maxHeight, Number.POSITIVE_INFINITY, availableMain);
  return {
    alignSelf: readFlexItemAlignment(layout.alignSelf),
    crossSize,
    flexGrow: readFlexGrow(layout),
    flexShrink: readFlexShrink(layout),
    hasExplicitCrossSize,
    height,
    mainSize: Math.max(minMain, Math.min(maxMain, basisValue)),
    order: readFlexOrder(layout.order),
    sourceIndex,
    width
  };
}

function createFlexLines(items: readonly FlexItemBox[], availableMain: number, availableCross: number, gap: number, wrap: FlexWrap): FlexLine[] {
  const lines: FlexLine[] = [];
  for (const item of items) {
    const current = lines.at(-1);
    const occupied = current ? current.items.reduce((sum, candidate) => sum + candidate.mainSize, 0) + gap * current.items.length : 0;
    if (current && wrap !== 'nowrap' && occupied + item.mainSize > availableMain) lines.push({ crossSize: item.crossSize, items: [item] });
    else if (current) { current.items.push(item); current.crossSize = Math.max(current.crossSize, item.crossSize); }
    else lines.push({ crossSize: item.crossSize, items: [item] });
  }
  if (wrap === 'nowrap' && lines[0]) lines[0].crossSize = availableCross;
  return lines;
}

function resolveFlexLineSizes(lines: readonly FlexLine[], availableMain: number, gap: number, direction: FlexDirection): void {
  for (const line of lines) {
    const total = line.items.reduce((sum, item) => sum + item.mainSize, 0) + gap * Math.max(0, line.items.length - 1);
    const freeSpace = availableMain - total;
    if (freeSpace > 0) {
      const totalGrow = line.items.reduce((sum, item) => sum + item.flexGrow, 0);
      if (totalGrow > 0) for (const item of line.items) item.mainSize += freeSpace * item.flexGrow / totalGrow;
    } else if (freeSpace < 0) {
      const totalShrink = line.items.reduce((sum, item) => sum + item.flexShrink * item.mainSize, 0);
      if (totalShrink > 0) for (const item of line.items) item.mainSize = Math.max(1, item.mainSize + freeSpace * item.flexShrink * item.mainSize / totalShrink);
    }
    for (const item of line.items) applyFlexMainSize(item, direction);
  }
}

function resolveFlexLinePositions(lines: readonly FlexLine[], availableCross: number, gap: number, alignContentValue: unknown, reverseCross: boolean): number[] {
  if (lines.length === 0) return [];
  const total = lines.reduce((sum, line) => sum + line.crossSize, 0) + gap * Math.max(0, lines.length - 1);
  const freeSpace = Math.max(0, availableCross - total);
  const alignContent = readFlexContentAlignment(alignContentValue);
  const offset = alignContent === 'center' ? freeSpace / 2 : alignContent === 'flex-end' ? freeSpace : 0;
  const extraGap = alignContent === 'space-between' && lines.length > 1 ? freeSpace / (lines.length - 1) : 0;
  const positions: number[] = [];
  let cursor = offset;
  for (const line of lines) {
    positions.push(reverseCross ? availableCross - cursor - line.crossSize : cursor);
    cursor += line.crossSize + gap + extraGap;
  }
  return positions;
}

function resolveFlexMainPosition(line: FlexLine, target: FlexItemBox, availableMain: number, gap: number, justifyValue: unknown): number {
  const total = line.items.reduce((sum, item) => sum + item.mainSize, 0) + gap * Math.max(0, line.items.length - 1);
  const freeSpace = Math.max(0, availableMain - total);
  const justify = readFlexJustify(justifyValue);
  const offset = justify === 'center' ? freeSpace / 2 : justify === 'flex-end' ? freeSpace : 0;
  const distributedGap = justify === 'space-between' && line.items.length > 1 ? gap + freeSpace / (line.items.length - 1) : gap;
  const targetIndex = line.items.indexOf(target);
  return offset + line.items.slice(0, targetIndex).reduce((sum, item) => sum + item.mainSize + distributedGap, 0);
}

function resolveFlexCrossPosition(item: FlexItemBox, line: FlexLine, lineStart: number, reverseCross: boolean, direction: FlexDirection, parentAlign: unknown): number {
  const alignment = item.alignSelf === 'auto' ? readFlexAlignment(parentAlign) : item.alignSelf;
  const crossSize = alignment === 'stretch' && !item.hasExplicitCrossSize ? line.crossSize : item.crossSize;
  if (direction.startsWith('row')) item.height = crossSize;
  else item.width = crossSize;
  const logicalOffset = alignment === 'center' ? (line.crossSize - crossSize) / 2 : alignment === 'flex-end' ? line.crossSize - crossSize : 0;
  return lineStart + (reverseCross ? line.crossSize - crossSize - logicalOffset : logicalOffset);
}

function applyFlexMainSize(item: FlexItemBox, direction: FlexDirection): void {
  if (direction.startsWith('row')) item.width = item.mainSize;
  else item.height = item.mainSize;
}

function applyFlexContainerStyle(projected: RuntimeLayoutRecord, layout: RuntimeLayoutRecord): void {
  projected.flexDirection = readFlexDirection(layout.flexDirection);
  projected.flexWrap = readFlexWrap(layout.flexWrap ?? layout.wrap);
  projected.rowGap = resolveFlexGap(layout.rowGap, layout.gap);
  projected.columnGap = resolveFlexGap(layout.columnGap, layout.gap);
  if (layout.alignItems !== undefined) projected.alignItems = readFlexAlignment(layout.alignItems);
  if (layout.justifyContent !== undefined) projected.justifyContent = readFlexJustify(layout.justifyContent);
  if (layout.alignContent !== undefined) projected.alignContent = readFlexContentAlignment(layout.alignContent);
}

function applyFlexItemStyle(projected: RuntimeLayoutRecord, layout: RuntimeLayoutRecord): void {
  if (layout.order !== undefined) projected.order = readFlexOrder(layout.order);
  if (layout.alignSelf !== undefined) {
    const alignSelf = readFlexItemAlignment(layout.alignSelf);
    if (alignSelf !== 'auto') projected.alignSelf = alignSelf;
  }
}

function resolveGridBox(layout: RuntimeLayoutRecord, parentLayout: RuntimeLayoutRecord, parent: Pick<RuntimeLayoutBox, 'height' | 'width'>, siblings: readonly RuntimeLayoutRecord[], index: number): RuntimeLayoutBox {
  const columnGap = resolveGridGap(parentLayout.columnGap, parentLayout.gap);
  const rowGap = resolveGridGap(parentLayout.rowGap, parentLayout.gap);
  const columnTracks = resolveGridTracks(parentLayout.gridTemplateColumns, parentLayout.columns, Math.max(1, trackCount(parentLayout.gridTemplateColumns, 1)), parent.width, columnGap);
  const rowTracks = resolveGridTracks(parentLayout.gridTemplateRows, parentLayout.rows, Math.max(1, Math.ceil(Math.max(1, siblings.length) / columnTracks.length)), parent.height, rowGap);
  const column = clampGridLine(readGridLine(layout.gridColumn), index % columnTracks.length + 1, columnTracks.length);
  const row = clampGridLine(readGridLine(layout.gridRow), Math.floor(index / columnTracks.length) + 1, rowTracks.length);
  const columnSpan = clampGridSpan(readGridSpan(layout.gridColumnSpan ?? layout.columnSpan), column, columnTracks.length);
  const rowSpan = clampGridSpan(readGridSpan(layout.gridRowSpan ?? layout.rowSpan), row, rowTracks.length);
  const x = sumTracks(columnTracks, 0, column - 1) + columnGap * (column - 1);
  const y = sumTracks(rowTracks, 0, row - 1) + rowGap * (row - 1);
  const width = sumTracks(columnTracks, column - 1, column - 1 + columnSpan) + columnGap * (columnSpan - 1);
  const height = sumTracks(rowTracks, row - 1, row - 1 + rowSpan) + rowGap * (rowSpan - 1);
  return { height: resolveDimension(layout.height, height, height), width: resolveDimension(layout.width, width, width), x, y };
}

function applyGridContainerStyle(projected: RuntimeLayoutRecord, layout: RuntimeLayoutRecord): void {
  const columnTemplate = gridTemplateValue(layout.gridTemplateColumns, layout.columns, 1);
  const rowTemplate = gridTemplateValue(layout.gridTemplateRows, layout.rows, 1);
  projected.gridTemplateColumns = columnTemplate.value;
  projected.gridTemplateRows = rowTemplate.value;
  const columnGap = resolveGridGap(layout.columnGap, layout.gap);
  const rowGap = resolveGridGap(layout.rowGap, layout.gap);
  projected.columnGap = columnGap;
  projected.rowGap = rowGap;
  delete projected.gap;
}

function applyGridChildPlacement(projected: RuntimeLayoutRecord, layout: RuntimeLayoutRecord, parentLayout: RuntimeLayoutRecord, siblings: readonly RuntimeLayoutRecord[] | undefined, index: number): void {
  const columnCount = gridTemplateValue(parentLayout.gridTemplateColumns, parentLayout.columns, 1).count;
  const rowCount = Math.max(1, gridTemplateValue(parentLayout.gridTemplateRows, parentLayout.rows, Math.ceil(Math.max(1, siblings?.length ?? 1) / columnCount)).count);
  const column = clampGridLine(readGridLine(layout.gridColumn), index % columnCount + 1, columnCount);
  const row = clampGridLine(readGridLine(layout.gridRow), Math.floor(index / columnCount) + 1, rowCount);
  const columnSpan = clampGridSpan(readGridSpan(layout.gridColumnSpan ?? layout.columnSpan), column, columnCount);
  const rowSpan = clampGridSpan(readGridSpan(layout.gridRowSpan ?? layout.rowSpan), row, rowCount);
  projected.gridColumn = `${column} / span ${columnSpan}`;
  projected.gridRow = `${row} / span ${rowSpan}`;
}

function resolveGridTracks(template: unknown, countValue: unknown, fallbackCount: number, parentSize: number, gap: number): number[] {
  const templateTracks = expandGridTemplate(template);
  const independentTracks = expandGridTemplate(countValue);
  const declaredTracks = templateTracks.length > 0 ? templateTracks : independentTracks;
  const count = Math.max(1, declaredTracks.length || positiveInteger(countValue) || fallbackCount);
  const tracks = declaredTracks.length > 0 ? declaredTracks : Array.from({ length: count }, () => '1fr');
  return resolveTrackSizes(tracks.slice(0, count), count, parentSize, gap);
}

function resolveTrackSizes(tracks: readonly string[], count: number, parentSize: number, gap: number): number[] {
  const values = Array.from({ length: count }, (_, index) => parseTrackSize(tracks[index] ?? '1fr', parentSize));
  const available = Math.max(0, parentSize - gap * Math.max(0, count - 1));
  const fixed = values.reduce((sum, value) => sum + value.fixed, 0);
  const flexible = values.reduce((sum, value) => sum + value.flex, 0);
  const remaining = Math.max(0, available - fixed);
  return values.map((value) => Math.max(1, value.fixed + (flexible > 0 ? remaining * value.flex / flexible : 0)));
}

function parseTrackSize(value: string, parentSize: number): { fixed: number; flex: number } {
  const trimmed = value.trim().toLowerCase();
  const minmax = trimmed.match(/^minmax\(\s*([^,]+),\s*([^)]+)\)$/);
  const source = minmax?.[2]?.trim() ?? trimmed;
  const minimum = minmax ? parseTrackLength(minmax[1].trim(), parentSize) : 0;
  if (source.endsWith('fr')) return { fixed: minimum, flex: Math.max(0.0001, Number.parseFloat(source) || 1) };
  const fixed = parseTrackLength(source, parentSize);
  return { fixed: Math.max(minimum, fixed), flex: 0 };
}

function parseTrackLength(value: string, parentSize: number): number {
  if (value === 'auto' || value === 'min-content' || value === 'max-content') return 0;
  if (value.endsWith('%')) return Math.max(0, parentSize * (Number.parseFloat(value) || 0) / 100);
  if (value.endsWith('px')) return Math.max(0, Number.parseFloat(value) || 0);
  return Math.max(0, readNumber(value) ?? 0);
}

function gridTemplateValue(template: unknown, countValue: unknown, fallbackCount: number): { count: number; value: string } {
  const tracks = expandGridTemplate(template).length > 0 ? expandGridTemplate(template) : expandGridTemplate(countValue);
  if (tracks.length > 0) return { count: tracks.length, value: tracks.join(' ') };
  const count = Math.max(1, positiveInteger(countValue) || fallbackCount);
  return { count, value: `repeat(${count}, minmax(0, 1fr))` };
}

function expandGridTemplate(value: unknown): string[] {
  if (Array.isArray(value)) return value.flatMap((item) => expandGridTemplate(item));
  if (typeof value !== 'string' || !value.trim()) return [];
  const tokens = splitGridTemplate(value.trim());
  return tokens.flatMap((token) => {
    const repeat = token.match(/^repeat\(\s*(\d+)\s*,\s*(.*)\)$/i);
    if (!repeat) return [token];
    const inner = expandGridTemplate(repeat[2]);
    return Array.from({ length: Math.max(1, Number.parseInt(repeat[1], 10)) }, () => inner).flat();
  });
}

function splitGridTemplate(value: string): string[] {
  const tokens: string[] = [];
  let depth = 0;
  let current = '';
  for (const character of value) {
    if (character === '(') depth += 1;
    if (character === ')') depth = Math.max(0, depth - 1);
    if (/\s/.test(character) && depth === 0) {
      if (current) tokens.push(current);
      current = '';
    } else current += character;
  }
  if (current) tokens.push(current);
  return tokens;
}

function resolveGridGap(primary: unknown, fallback: unknown): number {
  return Math.max(0, readNumber(primary) ?? readNumber(fallback) ?? 0);
}

function readGridLine(value: unknown): number | undefined {
  if (typeof value === 'string') {
    const match = value.trim().match(/^(\d+)/);
    if (match) return Number.parseInt(match[1], 10);
  }
  return readNumber(value);
}

function readGridSpan(value: unknown): number | undefined {
  const number = readNumber(value);
  return number === undefined ? undefined : Math.floor(number);
}

function clampGridLine(value: number | undefined, fallback: number, count: number): number {
  const line = Math.floor(value ?? fallback);
  return Math.max(1, Math.min(Number.isFinite(count) ? Math.max(1, count) : line, line));
}

function clampGridSpan(value: number | undefined, line: number, count: number): number {
  return Math.max(1, Math.min(Math.floor(value ?? 1), Math.max(1, count - line + 1)));
}

function sumTracks(tracks: readonly number[], start: number, end: number): number {
  return tracks.slice(Math.max(0, start), Math.max(start, end)).reduce((sum, value) => sum + value, 0);
}

function positiveInteger(value: unknown): number | undefined {
  const number = readNumber(value);
  return number !== undefined && Number.isInteger(number) && number > 0 ? number : undefined;
}

function applyAbsoluteBox(projected: RuntimeLayoutRecord, layout: RuntimeLayoutRecord, box: RuntimeLayoutBox | undefined): void {
  if (box) {
    projected.left = box.x;
    projected.top = box.y;
    projected.width = box.width;
    projected.height = box.height;
    return;
  }
  projected.left = readNumber(layout.x) ?? 0;
  projected.top = readNumber(layout.y) ?? 0;
  projected.width = normalizeDimensionStyleValue(layout.width) ?? DEFAULT_CHILD_WIDTH;
  projected.height = normalizeDimensionStyleValue(layout.height) ?? normalizeDimensionStyleValue(layout.minHeight) ?? DEFAULT_CHILD_HEIGHT;
  if (layout.minWidth !== undefined) projected.minWidth = normalizeDimensionStyleValue(layout.minWidth);
  if (layout.minHeight !== undefined) projected.minHeight = normalizeDimensionStyleValue(layout.minHeight);
}

function applyConstraintPosition(projected: RuntimeLayoutRecord, layout: RuntimeLayoutRecord): void {
  const constraints = normalizeConstraintSpec(normalizeRuntimeConstraintSpec(layout.constraints));
  if (constraints.left !== undefined) projected.left = constraints.left;
  if (constraints.right !== undefined) projected.right = constraints.right;
  if (constraints.top !== undefined) projected.top = constraints.top;
  if (constraints.bottom !== undefined) projected.bottom = constraints.bottom;
  if (constraints.centerX !== undefined && constraints.left === undefined && constraints.right === undefined) {
    projected.left = `calc(50% + ${constraints.centerX}px)`;
    projected.transform = appendTransform(String(projected.transform ?? ''), 'translateX(-50%)');
  }
  if (constraints.centerY !== undefined && constraints.top === undefined && constraints.bottom === undefined) {
    projected.top = `calc(50% + ${constraints.centerY}px)`;
    projected.transform = appendTransform(String(projected.transform ?? ''), 'translateY(-50%)');
  }
  if (constraints.stretchX && constraints.left !== undefined && constraints.right !== undefined) delete projected.width;
  if (constraints.stretchY && constraints.top !== undefined && constraints.bottom !== undefined) delete projected.height;
}

function applyFlowBox(projected: RuntimeLayoutRecord, layout: RuntimeLayoutRecord, box: RuntimeLayoutBox | undefined): void {
  if (box) {
    projected.width = box.width;
    projected.height = box.height;
    return;
  }
  if (layout.width !== undefined) projected.width = normalizeDimensionStyleValue(layout.width);
  if (layout.height !== undefined) projected.height = normalizeDimensionStyleValue(layout.height);
  if (layout.minWidth !== undefined) projected.minWidth = normalizeDimensionStyleValue(layout.minWidth);
  if (layout.minHeight !== undefined) projected.minHeight = normalizeDimensionStyleValue(layout.minHeight);
}

function applyLayoutVisualValues(projected: RuntimeLayoutRecord, layout: RuntimeLayoutRecord): void {
  for (const key of ['zIndex', 'transform'] as const) {
    const value = layout[key];
    if (projected[key] === undefined && isCssValue(value)) projected[key] = value;
  }
}

function projectCssValues(style: RuntimeLayoutRecord): RuntimeLayoutRecord {
  const projected: RuntimeLayoutRecord = {};
  for (const [key, value] of Object.entries(style)) {
    if (key === 'border' && isRecord(value)) {
      const width = value.width ?? value.size;
      const color = value.color;
      const borderStyle = value.style ?? 'solid';
      if (width !== undefined && color !== undefined) projected.border = `${formatLength(width)} ${String(borderStyle)} ${String(color)}`;
      continue;
    }
    if (isCssValue(value)) projected[key] = normalizeCssValue(key, value);
  }
  return projected;
}

function collectDiagnostics(layout: RuntimeLayoutRecord): RuntimeLayoutProjectionDiagnostic[] {
  const diagnostics: RuntimeLayoutProjectionDiagnostic[] = [];
  for (const field of DIMENSION_FIELDS) if (field in layout && layout[field] !== undefined && !isDimension(layout[field])) diagnostics.push({ code: 'invalid-dimension', field, message: `${field} must be a finite number, px, or percentage.` });
  for (const field of POSITION_FIELDS) if (field in layout && layout[field] !== undefined && readNumber(layout[field]) === undefined) diagnostics.push({ code: 'invalid-position', field, message: `${field} must be a finite number.` });
  if ('constraints' in layout && layout.constraints !== undefined) {
    if (!isRecord(layout.constraints)) diagnostics.push({ code: 'invalid-constraint', field: 'constraints', message: 'constraints must be an object.' });
    else for (const [field, value] of Object.entries(layout.constraints)) {
      if (['stretchX', 'stretchY'].includes(field) ? typeof value !== 'boolean' : ['left', 'right', 'top', 'bottom', 'centerX', 'centerY', 'minWidth', 'maxWidth', 'minHeight', 'maxHeight'].includes(field) && readNumber(value) === undefined) diagnostics.push({ code: 'invalid-constraint', field: `constraints.${field}`, message: `constraints.${field} has an invalid value.` });
    }
  }
  return diagnostics;
}

function shouldUseAbsolutePosition(layout: RuntimeLayoutRecord, parentMode: LayoutMode | undefined): boolean {
  return layout.position === 'absolute' || isRecord(layout.constraints) || parentMode === 'free' || parentMode === 'constraints' || (parentMode === undefined && (readNumber(layout.x) !== undefined || readNumber(layout.y) !== undefined));
}

function resolveDimension(value: unknown, fallback: number, parentSize: number): number {
  const numeric = readNumber(value);
  if (numeric !== undefined) return Math.max(1, numeric);
  if (typeof value !== 'string') return Math.max(1, fallback);
  const trimmed = value.trim();
  if (trimmed.endsWith('%')) return Math.max(1, parentSize * (Number.parseFloat(trimmed.slice(0, -1)) || 100) / 100);
  if (trimmed.endsWith('px')) return Math.max(1, Number.parseFloat(trimmed.slice(0, -2)) || fallback);
  return Math.max(1, fallback);
}

function isDimension(value: unknown): boolean {
  return readNumber(value) !== undefined || typeof value === 'string' && (/^\s*-?(?:\d+(?:\.\d+)?|\.\d+)(?:px|%)\s*$/).test(value);
}

function normalizeCssValue(key: string, value: string | number): string | number {
  if (typeof value !== 'number' || UNIT_LESS_STYLE_PROPERTIES.has(key)) return value;
  return `${value}px`;
}

function formatLength(value: unknown): string { return typeof value === 'number' ? `${value}px` : String(value); }
function appendTransform(current: string, addition: string): string { return current.trim() ? `${current} ${addition}` : addition; }
function normalizeDimensionStyleValue(value: unknown): string | number | undefined {
  const numeric = readNumber(value);
  if (numeric !== undefined) return numeric;
  return typeof value === 'string' && value.trim() ? value.trim() : undefined;
}
function normalizeRuntimeConstraintSpec(value: unknown): RuntimeLayoutRecord {
  if (!isRecord(value)) return {};
  return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, readNumber(item) ?? item]));
}
function readNumber(value: unknown): number | undefined {
  if (typeof value === 'number') return Number.isFinite(value) ? value : undefined;
  if (typeof value !== 'string' || !value.trim()) return undefined;
  const numeric = Number(value.trim());
  return Number.isFinite(numeric) ? numeric : undefined;
}
function safeSize(value: number, fallback: number): number { return Number.isFinite(value) && value > 0 ? value : fallback; }
function trackCount(value: unknown, fallback: number): number { if (typeof value !== 'string') return fallback; const repeat = value.match(/repeat\(\s*(\d+)/i); return repeat ? Number.parseInt(repeat[1], 10) : Math.max(1, value.split(/\s+/).filter(Boolean).length); }
function isCssValue(value: unknown): value is string | number { return typeof value === 'string' || typeof value === 'number'; }
function isRecord(value: unknown): value is RuntimeLayoutRecord { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
function isRuntimeDimension(value: unknown): boolean { return typeof value === 'number' && Number.isFinite(value) || typeof value === 'string' && (Number.isFinite(Number(value.trim())) || /^-?(?:\d+(?:\.\d+)?|\.\d+)(?:px|%)$/.test(value.trim())); }
function readFlexDirection(value: unknown): FlexDirection { return value === 'row-reverse' || value === 'column' || value === 'column-reverse' || value === 'row' ? value : 'row'; }
function readFlexWrap(value: unknown): FlexWrap { return value === 'wrap' || value === 'wrap-reverse' ? value : 'nowrap'; }
function resolveFlexGap(primary: unknown, fallback: unknown): number { return Math.max(0, readNumber(primary) ?? readNumber(fallback) ?? 0); }
function readFlexAlignment(value: unknown): FlexAlignment {
  if (value === 'center') return 'center';
  if (value === 'end' || value === 'flex-end') return 'flex-end';
  if (value === 'stretch') return 'stretch';
  return 'flex-start';
}
function readFlexItemAlignment(value: unknown): FlexAlignment | 'auto' {
  if (value === undefined || value === 'auto') return 'auto';
  return readFlexAlignment(value);
}
function readFlexContentAlignment(value: unknown): 'flex-start' | 'center' | 'flex-end' | 'space-between' {
  if (value === 'space-between') return 'space-between';
  const alignment = readFlexAlignment(value);
  return alignment === 'stretch' ? 'flex-start' : alignment;
}
function readFlexJustify(value: unknown): 'flex-start' | 'center' | 'flex-end' | 'space-between' {
  if (value === 'space-between') return 'space-between';
  const alignment = readFlexAlignment(value);
  return alignment === 'stretch' ? 'flex-start' : alignment;
}
function readFlexOrder(value: unknown): number { return readNumber(value) ?? 0; }
function readFlexShrink(layout: RuntimeLayoutRecord): number {
  if (typeof layout.flexShrink === 'number' && Number.isFinite(layout.flexShrink) && layout.flexShrink >= 0) return layout.flexShrink;
  if (typeof layout.flex === 'string') {
    const value = Number.parseFloat(layout.flex.trim().split(/\s+/)[1] ?? '');
    if (Number.isFinite(value) && value >= 0) return value;
  }
  return 1;
}
function readFlexBasis(value: unknown, fallback: number, parentSize: number): number {
  if (value === undefined || value === 'auto') return fallback;
  return resolveDimension(value, fallback, parentSize);
}
function resolveFlexLimit(value: unknown, fallback: number, parentSize: number): number {
  if (value === undefined) return fallback;
  return resolveDimension(value, fallback, parentSize);
}
function readFlexGrow(layout: RuntimeLayoutRecord): number {
  if (typeof layout.flexGrow === 'number' && Number.isFinite(layout.flexGrow) && layout.flexGrow >= 0) return layout.flexGrow;
  if (typeof layout.flex !== 'string') return 0;
  const first = Number.parseFloat(layout.flex.trim().split(/\s+/)[0] ?? '');
  return Number.isFinite(first) && first >= 0 ? first : 0;
}
