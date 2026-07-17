import type { ComponentManifest } from './ComponentManifest';

export type ComponentGeometryModel = 'flow' | 'absolute' | 'intrinsic';
export type ComponentContentModel = 'void' | 'text' | 'children' | 'mixed';
export type ComponentParentLayout = 'free' | 'flex' | 'grid' | 'constraints';
export type ComponentResizeHandle = 'north' | 'west' | 'east' | 'south' | 'northwest' | 'northeast' | 'southwest' | 'southeast';

export interface ComponentInteractionPolicy {
  allowedChildren: readonly string[];
  allowedParents: readonly string[];
  contentModel: ComponentContentModel;
  geometryModel: ComponentGeometryModel;
  resizeHandles: readonly ComponentResizeHandle[];
  supportedParentLayouts: readonly ComponentParentLayout[];
}

export interface ComponentInteractionDiagnostic {
  code: string;
  message: string;
}

export const COMPONENT_PARENT_LAYOUTS: readonly ComponentParentLayout[] = ['free', 'flex', 'grid', 'constraints'];
export const COMPONENT_RESIZE_HANDLES: readonly ComponentResizeHandle[] = ['north', 'west', 'east', 'south', 'northwest', 'northeast', 'southwest', 'southeast'];

const FLOW_PARENT_LAYOUTS: readonly ComponentParentLayout[] = ['flex', 'grid'];
const ABSOLUTE_PARENT_LAYOUTS: readonly ComponentParentLayout[] = ['free', 'constraints'];

/**
 * Legacy manifests predate CMP-01. Their missing policy means no new type/layout
 * restriction, while their existing acceptsChildren flag remains authoritative.
 */
export function resolveComponentInteractionPolicy(manifest: ComponentManifest): ComponentInteractionPolicy {
  if (manifest.interaction) {
    const interaction = manifest.interaction as unknown as Record<string, unknown>;
    return {
      allowedChildren: readStringArray(interaction.allowedChildren),
      allowedParents: readStringArray(interaction.allowedParents),
      contentModel: isContentModel(interaction.contentModel) ? interaction.contentModel : 'void',
      geometryModel: isGeometryModel(interaction.geometryModel) ? interaction.geometryModel : 'intrinsic',
      resizeHandles: readResizeHandles(interaction.resizeHandles),
      supportedParentLayouts: readParentLayouts(interaction.supportedParentLayouts)
    };
  }

  return {
    allowedChildren: manifest.capability.acceptsChildren ? ['*'] : [],
    allowedParents: ['*'],
    contentModel: manifest.capability.acceptsChildren ? 'children' : 'void',
    geometryModel: 'intrinsic',
    resizeHandles: [...COMPONENT_RESIZE_HANDLES],
    supportedParentLayouts: [...COMPONENT_PARENT_LAYOUTS]
  };
}

export function canContainComponent(parent: ComponentManifest, child: ComponentManifest, parentLayout: Record<string, unknown>): boolean {
  if (parent.type === child.type) return false;
  if (validateComponentInteractionPolicy(parent).length > 0 || validateComponentInteractionPolicy(child).length > 0) return false;

  const parentPolicy = resolveComponentInteractionPolicy(parent);
  const childPolicy = resolveComponentInteractionPolicy(child);
  if (!parent.capability.acceptsChildren || parentPolicy.contentModel === 'void' || parentPolicy.contentModel === 'text') return false;
  if (!matchesTypeRule(parentPolicy.allowedChildren, child.type) || !matchesTypeRule(childPolicy.allowedParents, parent.type)) return false;

  const layout = resolveParentLayout(parentLayout);
  if (!childPolicy.supportedParentLayouts.includes(layout)) return false;
  return isGeometryCompatible(childPolicy.geometryModel, layout);
}

export function canResizeComponent(manifest: ComponentManifest, handle: ComponentResizeHandle): boolean {
  if (validateComponentInteractionPolicy(manifest).length > 0) return false;
  return resolveComponentInteractionPolicy(manifest).resizeHandles.includes(handle);
}

export function validateComponentInteractionPolicy(manifest: ComponentManifest): ComponentInteractionDiagnostic[] {
  const policy = manifest.interaction;
  if (!policy) return [];
  const raw = policy as unknown as Record<string, unknown>;
  const diagnostics: ComponentInteractionDiagnostic[] = [];
  const add = (code: string, message: string) => diagnostics.push({ code, message });
  const allowedChildren = readUnknownArray(raw.allowedChildren);
  const allowedParents = readUnknownArray(raw.allowedParents);
  const resizeHandles = readUnknownArray(raw.resizeHandles);
  const supportedParentLayouts = readUnknownArray(raw.supportedParentLayouts);

  if (!allowedChildren) add('missingAllowedChildren', 'allowed children declaration is required');
  if (!allowedParents) add('missingAllowedParents', 'allowed parents declaration is required');
  if (!resizeHandles) add('missingResizeHandles', 'resize handles declaration is required');
  if (!supportedParentLayouts || supportedParentLayouts.length === 0) add('missingParentLayouts', 'at least one supported parent layout is required');
  if (!isGeometryModel(raw.geometryModel)) add('invalidGeometryModel', `unsupported geometry model: ${String(raw.geometryModel)}`);
  if (!isContentModel(raw.contentModel)) add('invalidContentModel', `unsupported content model: ${String(raw.contentModel)}`);

  if (allowedChildren) validateTypeRules('allowedChildren', allowedChildren, add);
  if (allowedParents) {
    if (allowedParents.length === 0) add('missingAllowedParents', 'at least one allowed parent rule is required');
    validateTypeRules('allowedParents', allowedParents, add);
  }
  if (supportedParentLayouts) {
    const seen = new Set<unknown>();
    for (const layout of supportedParentLayouts) {
      if (!isParentLayout(layout)) add('invalidParentLayout', `unsupported parent layout: ${String(layout)}`);
      else if (!seen.add(layout)) add('duplicateParentLayout', `duplicate parent layout: ${layout}`);
    }
  }
  if (resizeHandles) {
    const seen = new Set<unknown>();
    for (const handle of resizeHandles) {
      if (!isResizeHandle(handle)) add('invalidResizeHandle', `unsupported resize handle: ${String(handle)}`);
      else if (!seen.add(handle)) add('duplicateResizeHandle', `duplicate resize handle: ${handle}`);
    }
  }

  const contentModel = raw.contentModel;
  const acceptsChildren = manifest.capability?.acceptsChildren === true;
  if (contentModel === 'void' && allowedChildren && allowedChildren.length > 0) add('voidComponentChildren', 'void components cannot declare allowed children');
  if (contentModel === 'text' && allowedChildren && allowedChildren.length > 0) add('textComponentChildren', 'text components cannot declare allowed children');
  if ((contentModel === 'children' || contentModel === 'mixed') && !acceptsChildren) add('nonContainerContentModel', 'children content models require acceptsChildren');
  if (raw.geometryModel === 'intrinsic' && resizeHandles && resizeHandles.length > 0) add('intrinsicResizeHandles', 'intrinsic geometry cannot expose resize handles');
  return diagnostics;
}

export function matchesTypeRule(rules: readonly string[], type: string): boolean {
  return rules.some((rule) => {
    if (typeof rule !== 'string') return false;
    const normalized = rule.trim();
    if (!normalized) return false;
    if (normalized === '*') return true;
    if (normalized.endsWith('*')) return type.startsWith(normalized.slice(0, -1));
    return normalized === type;
  });
}

export function resolveParentLayout(layout: Record<string, unknown>): ComponentParentLayout {
  if (layout.layoutMode === 'flex' || layout.display === 'flex') return 'flex';
  if (layout.layoutMode === 'grid' || layout.display === 'grid') return 'grid';
  if (layout.layoutMode === 'constraints' || layout.display === 'constraints') return 'constraints';
  return 'free';
}

function isGeometryCompatible(model: ComponentGeometryModel, parentLayout: ComponentParentLayout): boolean {
  if (model === 'flow') return FLOW_PARENT_LAYOUTS.includes(parentLayout);
  if (model === 'absolute') return ABSOLUTE_PARENT_LAYOUTS.includes(parentLayout);
  return true;
}

function validateTypeRules(name: string, rules: readonly unknown[], add: (code: string, message: string) => void): void {
  const seen = new Set<string>();
  for (const rule of rules) {
    if (typeof rule !== 'string' || !rule.trim()) add('invalidInteractionTypeRule', `${name} rules must be non-empty strings`);
    else if (seen.has(rule)) add('duplicateInteractionTypeRule', `duplicate ${name} rule: ${rule}`);
    else seen.add(rule);
  }
}

function readUnknownArray(value: unknown): readonly unknown[] | null {
  return Array.isArray(value) ? value : null;
}

function readStringArray(value: unknown): readonly string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
}

function readResizeHandles(value: unknown): readonly ComponentResizeHandle[] {
  return Array.isArray(value) ? value.filter(isResizeHandle) : [];
}

function readParentLayouts(value: unknown): readonly ComponentParentLayout[] {
  return Array.isArray(value) ? value.filter(isParentLayout) : [];
}

function isContentModel(value: unknown): value is ComponentContentModel { return value === 'void' || value === 'text' || value === 'children' || value === 'mixed'; }
function isGeometryModel(value: unknown): value is ComponentGeometryModel { return value === 'flow' || value === 'absolute' || value === 'intrinsic'; }
function isParentLayout(value: unknown): value is ComponentParentLayout { return value === 'free' || value === 'flex' || value === 'grid' || value === 'constraints'; }
function isResizeHandle(value: unknown): value is ComponentResizeHandle { return COMPONENT_RESIZE_HANDLES.includes(value as ComponentResizeHandle); }
