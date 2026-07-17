import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';
import type { LayoutProtocol, LayoutMode } from '../layout/LayoutProtocol';
import { layoutProtocolDiagnosticCodes } from '../layout/LayoutProtocolDiagnostics';
import { validateLayoutProtocol } from '../layout/LayoutProtocolValidator';

export interface PageStudioLayoutDiagnostic {
  code: string;
  path: string;
  message: string;
  severity: 'error';
}

export const pageStudioLayoutDiagnosticCodes = {
  containerModeInvalid: 'LAYOUT_CONTAINER_MODE_INVALID',
  constraintValueInvalid: 'LAYOUT_CONSTRAINT_VALUE_INVALID',
  legacyField: 'LAYOUT_LEGACY_FIELD',
  missingParent: 'LAYOUT_PARENT_MISSING',
  parentCycle: 'LAYOUT_PARENT_CYCLE',
  parentChildMismatch: 'LAYOUT_PARENT_CHILD_MISMATCH',
  placementValueInvalid: 'LAYOUT_PLACEMENT_VALUE_INVALID',
  protocolSectionInvalid: 'LAYOUT_PROTOCOL_SECTION_INVALID',
  requiredSizeField: 'LAYOUT_SIZE_FIELD_REQUIRED'
} as const;

const LEGACY_LAYOUT_FIELDS = new Set([
  'alignContent',
  'alignItems',
  'alignSelf',
  'columnGap',
  'constraints',
  'display',
  'flex',
  'flexBasis',
  'flexDirection',
  'flexGrow',
  'flexShrink',
  'flexWrap',
  'gap',
  'gridColumn',
  'gridRow',
  'gridTemplateColumns',
  'gridTemplateRows',
  'height',
  'justifyContent',
  'justifyItems',
  'layoutMode',
  'maxHeight',
  'maxWidth',
  'minHeight',
  'minWidth',
  'position',
  'order',
  'rowGap',
  'wrap',
  'width',
  'x',
  'y'
]);
const LAYOUT_PROTOCOL_KEYS = ['container', 'placement', 'size'] as const;
const PLACEMENT_PAYLOAD_KEYS = ['absolute', 'flexItem', 'gridItem', 'constrained'] as const;
const CONSTRAINT_NUMBER_KEYS = ['left', 'right', 'top', 'bottom', 'centerX', 'centerY'] as const;
const FLEX_ALIGNMENT_VALUES = new Set(['auto', 'start', 'center', 'end', 'stretch', 'baseline']);
const GRID_ALIGNMENT_VALUES = new Set(['auto', 'start', 'center', 'end', 'stretch']);

export function diagnosePageStudioLayoutNode(document: DesignerDocument, node: DesignerDocumentNode): PageStudioLayoutDiagnostic[] {
  const diagnostics: PageStudioLayoutDiagnostic[] = [];
  const layout = asRecord(node.layout);
  const protocolSource = resolveProtocolSource(layout);

  diagnostics.push(...diagnoseParentLink(document, node));
  diagnostics.push(...diagnoseLegacyFields(node, layout));
  if (protocolSource) diagnostics.push(...diagnoseLayoutProtocol(protocolSource));

  return diagnostics;
}

function resolveProtocolSource(layout: Record<string, unknown>): ProtocolSource | null {
  if (Object.prototype.hasOwnProperty.call(layout, 'protocol') || Object.prototype.hasOwnProperty.call(layout, 'layoutProtocol')) {
    const key = Object.prototype.hasOwnProperty.call(layout, 'protocol') ? 'protocol' : 'layoutProtocol';
    const value = layout[key];
    return { path: `layout.${key}`, value: isRecord(value) ? value : {}, wrapper: true, invalid: !isRecord(value) };
  }
  if (LAYOUT_PROTOCOL_KEYS.some((key) => Object.prototype.hasOwnProperty.call(layout, key))) {
    return { path: 'layout', value: layout, wrapper: false, invalid: false };
  }
  return null;
}

function diagnoseLayoutProtocol(source: ProtocolSource): PageStudioLayoutDiagnostic[] {
  const diagnostics: PageStudioLayoutDiagnostic[] = [];
  const raw = source.value;
  if (source.invalid) {
    diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.protocolSectionInvalid, path: source.path, message: '布局协议必须是对象。', severity: 'error' });
  }

  for (const section of LAYOUT_PROTOCOL_KEYS) {
    if (!isRecord(raw[section])) {
      diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.protocolSectionInvalid, path: `${source.path}.${section}`, message: `布局协议缺少有效的 ${section}。`, severity: 'error' });
    }
  }

  const container = isRecord(raw.container) ? raw.container : {};
  const placement = isRecord(raw.placement) ? raw.placement : {};
  const size = isRecord(raw.size) ? raw.size : {};
  if (!isLayoutMode(container.mode)) {
    diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.containerModeInvalid, path: `${source.path}.container.mode`, message: `布局容器 mode 无效：${String(container.mode)}`, severity: 'error' });
  }
  if (!Object.prototype.hasOwnProperty.call(size, 'width')) {
    diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.requiredSizeField, path: `${source.path}.size.width`, message: '布局 size.width 为必填字段。', severity: 'error' });
  }
  if (!Object.prototype.hasOwnProperty.call(size, 'height')) {
    diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.requiredSizeField, path: `${source.path}.size.height`, message: '布局 size.height 为必填字段。', severity: 'error' });
  }

  diagnostics.push(...validatePlacementValues(placement, source.path));
  diagnostics.push(...validateGridValues(container, source.path));
  diagnostics.push(...validateConstraintValues(container, source.path));

  const protocol = createSafeLayoutProtocol(raw);
  try {
    diagnostics.push(...validateLayoutProtocol(protocol).map((diagnostic) => ({
      code: diagnostic.code,
      path: `${source.path}.${diagnostic.path}`,
      message: `布局协议无效：${diagnostic.path}`,
      severity: 'error' as const
    })));
  } catch {
    diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.protocolSectionInvalid, path: source.path, message: '布局协议结构无法验证。', severity: 'error' });
  }
  return diagnostics;
}

function diagnoseParentLink(document: DesignerDocument, node: DesignerDocumentNode): PageStudioLayoutDiagnostic[] {
  const diagnostics: PageStudioLayoutDiagnostic[] = [];
  if (node.parentId) {
    const parent = document.elements[node.parentId];
    if (!parent) {
      diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.missingParent, path: `elements.${node.id}.parentId`, message: `父节点不存在：${node.parentId}`, severity: 'error' });
    } else if (!parent.children.includes(node.id)) {
      diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.parentChildMismatch, path: `elements.${node.id}.parentId`, message: `父节点 ${node.parentId} 未包含子节点 ${node.id}。`, severity: 'error' });
    }
  }

  const visited = new Set<string>();
  let current: string | null = node.id;
  while (current) {
    if (visited.has(current)) {
      diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.parentCycle, path: `elements.${node.id}.parentId`, message: '父节点引用形成循环。', severity: 'error' });
      break;
    }
    visited.add(current);
    current = document.elements[current]?.parentId ?? null;
  }
  return diagnostics;
}

function diagnoseLegacyFields(node: DesignerDocumentNode, layout: Record<string, unknown>): PageStudioLayoutDiagnostic[] {
  const diagnostics: PageStudioLayoutDiagnostic[] = [];
  for (const field of LEGACY_LAYOUT_FIELDS) {
    if (Object.prototype.hasOwnProperty.call(layout, field)) {
      diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.legacyField, path: `layout.${field}`, message: `布局字段 ${field} 已废弃，请使用 LayoutProtocol。`, severity: 'error' });
    }
  }

  const rawNode = node as unknown as Record<string, unknown>;
  if (Object.prototype.hasOwnProperty.call(rawNode, 'dataBinding')) {
    diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.legacyField, path: `elements.${node.id}.dataBinding`, message: 'dataBinding 是旧字段，请迁移到 bindings。', severity: 'error' });
  }
  return diagnostics;
}

function validatePlacementValues(placement: Record<string, unknown>, path: string): PageStudioLayoutDiagnostic[] {
  const diagnostics: PageStudioLayoutDiagnostic[] = [];
  for (const payloadKey of PLACEMENT_PAYLOAD_KEYS) {
    const payload = placement[payloadKey];
    if (!isRecord(payload)) continue;
    const numericKeys = payloadKey === 'absolute' ? ['x', 'y', 'zIndex'] : payloadKey === 'flexItem' ? ['order', 'grow', 'shrink'] : payloadKey === 'constrained' ? CONSTRAINT_NUMBER_KEYS : [];
    for (const key of numericKeys) {
      const value = payload[key];
      if (value !== undefined && !isFiniteNumber(value)) {
        diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.placementValueInvalid, path: `${path}.placement.${payloadKey}.${key}`, message: `placement.${payloadKey}.${key} 必须是有限数字。`, severity: 'error' });
      }
    }
    if (payloadKey === 'flexItem') {
      for (const key of ['grow', 'shrink']) {
        const value = payload[key];
        if (value !== undefined && isFiniteNumber(value) && value < 0) diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.placementValueInvalid, path: `${path}.placement.flexItem.${key}`, message: `placement.flexItem.${key} 必须是非负有限数字。`, severity: 'error' });
      }
      if (payload.basis !== undefined && !isDimensionValue(payload.basis)) diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.placementValueInvalid, path: `${path}.placement.flexItem.basis`, message: 'placement.flexItem.basis 必须是有效尺寸。', severity: 'error' });
      if (payload.alignSelf !== undefined && !FLEX_ALIGNMENT_VALUES.has(String(payload.alignSelf))) diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.placementValueInvalid, path: `${path}.placement.flexItem.alignSelf`, message: 'placement.flexItem.alignSelf 无效。', severity: 'error' });
    }
    if (payloadKey === 'gridItem') {
      for (const key of ['rowStart', 'columnStart']) {
        const value = payload[key];
        if (value !== undefined && value !== 'auto' && (!isFiniteNumber(value) || !Number.isInteger(value) || value < 1)) {
          diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.placementValueInvalid, path: `${path}.placement.${payloadKey}.${key}`, message: `placement.${payloadKey}.${key} 必须是 auto 或有限数字。`, severity: 'error' });
        }
      }
      for (const key of ['alignSelf', 'justifySelf']) {
        const value = payload[key];
        if (value !== undefined && !GRID_ALIGNMENT_VALUES.has(String(value))) diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.placementValueInvalid, path: `${path}.placement.${payloadKey}.${key}`, message: `placement.${payloadKey}.${key} 无效。`, severity: 'error' });
      }
    }
    if (payloadKey === 'constrained') {
      for (const key of ['stretchX', 'stretchY']) {
        const value = payload[key];
        if (value !== undefined && typeof value !== 'boolean') diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.constraintValueInvalid, path: `${path}.placement.constrained.${key}`, message: `placement.constrained.${key} 必须是布尔值。`, severity: 'error' });
      }
    }
  }
  return diagnostics;
}

function validateGridValues(container: Record<string, unknown>, path: string): PageStudioLayoutDiagnostic[] {
  const diagnostics: PageStudioLayoutDiagnostic[] = [];
  const grid = container.grid;
  if (isRecord(grid)) {
    for (const key of ['columnGap', 'rowGap']) {
      const value = grid[key];
      if (value !== undefined && (typeof value !== 'number' || !Number.isFinite(value) || value < 0)) {
        diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidContainerPayload, path: `${path}.container.grid.${key}`, message: `container.grid.${key} 必须是非负有限数字。`, severity: 'error' });
      }
    }
    for (const key of ['columns', 'rows']) {
      const value = grid[key];
      if (!Array.isArray(value) || value.length === 0 || value.some((item) => typeof item !== 'string' || !item.trim())) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidContainerPayload, path: `${path}.container.grid.${key}`, message: `container.grid.${key} 必须是非空字符串数组。`, severity: 'error' });
    }
    if (grid.autoFlow !== undefined && !['row', 'column', 'dense', 'row-dense', 'column-dense'].includes(String(grid.autoFlow))) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidContainerPayload, path: `${path}.container.grid.autoFlow`, message: 'container.grid.autoFlow 无效。', severity: 'error' });
  }
  return diagnostics;
}

function validateConstraintValues(container: Record<string, unknown>, path: string): PageStudioLayoutDiagnostic[] {
  const diagnostics: PageStudioLayoutDiagnostic[] = [];
  const constraints = container.constraints;
  if (!isRecord(constraints)) return diagnostics;
  for (const key of CONSTRAINT_NUMBER_KEYS) {
    const value = constraints[key];
    if (value !== undefined && (typeof value !== 'number' || !Number.isFinite(value))) {
      diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.constraintValueInvalid, path: `${path}.container.constraints.${key}`, message: `container.constraints.${key} 必须是有限数字。`, severity: 'error' });
    }
  }
  for (const key of ['stretchX', 'stretchY']) {
    const value = constraints[key];
    if (value !== undefined && typeof value !== 'boolean') {
      diagnostics.push({ code: pageStudioLayoutDiagnosticCodes.constraintValueInvalid, path: `${path}.container.constraints.${key}`, message: `container.constraints.${key} 必须是布尔值。`, severity: 'error' });
    }
  }
  return diagnostics;
}

function createSafeLayoutProtocol(raw: Record<string, unknown>): LayoutProtocol {
  const rawContainer = isRecord(raw.container) ? raw.container : {};
  const mode: LayoutMode = isLayoutMode(rawContainer.mode) ? rawContainer.mode : 'free';
  const container: Record<string, unknown> = { mode };
  if (isRecord(rawContainer.flex)) container.flex = rawContainer.flex;
  if (isRecord(rawContainer.grid)) {
    container.grid = { ...rawContainer.grid, columns: Array.isArray(rawContainer.grid.columns) ? rawContainer.grid.columns : [], rows: Array.isArray(rawContainer.grid.rows) ? rawContainer.grid.rows : [] };
  }
  if (isRecord(rawContainer.constraints)) container.constraints = rawContainer.constraints;

  const rawPlacement = isRecord(raw.placement) ? raw.placement : {};
  const expectedKind = mode === 'free' ? 'absolute' : mode === 'flex' ? 'flex-item' : mode === 'grid' ? 'grid-item' : 'constrained';
  const placement: Record<string, unknown> = { kind: typeof rawPlacement.kind === 'string' ? rawPlacement.kind : expectedKind };
  for (const key of PLACEMENT_PAYLOAD_KEYS) if (isRecord(rawPlacement[key])) placement[key] = rawPlacement[key];

  // The validator must receive a structurally safe protocol even when the document is malformed.
  // Keep the runtime values narrowed to records first, then cross the typed protocol boundary
  // explicitly so malformed values can still be reported by the diagnostics above.
  const size = isRecord(raw.size) ? raw.size : {};
  return {
    container: container as unknown as LayoutProtocol['container'],
    placement: placement as unknown as LayoutProtocol['placement'],
    size: size as unknown as LayoutProtocol['size']
  };
}

function isLayoutMode(value: unknown): value is LayoutMode { return value === 'free' || value === 'flex' || value === 'grid' || value === 'constraints'; }
function asRecord(value: unknown): Record<string, unknown> { return isRecord(value) ? value : {}; }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
function isFiniteNumber(value: unknown): value is number { return typeof value === 'number' && Number.isFinite(value); }
function isDimensionValue(value: unknown): boolean {
  return value === 'auto' || value === 'min-content' || value === 'max-content' || value === 'fit-content' || isFiniteNumber(value) && value >= 0 || typeof value === 'string' && /^(?:\d+(?:\.\d+)?)(?:%|px)$/.test(value.trim());
}

interface ProtocolSource {
  path: string;
  value: Record<string, unknown>;
  wrapper: boolean;
  invalid: boolean;
}
