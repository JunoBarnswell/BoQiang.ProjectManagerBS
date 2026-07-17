export interface ResponsiveBreakpoint {
  id: string;
  label?: string;
  minWidth: number;
  maxWidth?: number;
  orientation?: 'portrait' | 'landscape';
  previewWidth?: number;
  previewHeight?: number;
}

export const DEFAULT_RESPONSIVE_BREAKPOINTS: readonly ResponsiveBreakpoint[] = [
  { id: 'mobile', label: 'Mobile', minWidth: 0, maxWidth: 767, previewWidth: 390, previewHeight: 844 },
  { id: 'tablet', label: 'Tablet', minWidth: 768, maxWidth: 1199, previewWidth: 1024, previewHeight: 768 },
  { id: 'desktop', label: 'Desktop', minWidth: 1200, previewWidth: 1440, previewHeight: 900 }
];

export const RESPONSIVE_SECTIONS = ['layout', 'props', 'style'] as const;
export type ResponsiveSection = typeof RESPONSIVE_SECTIONS[number];
export type ResponsiveSectionValue = Record<string, unknown>;
export interface ResponsiveSections {
  layout?: ResponsiveSectionValue;
  props?: ResponsiveSectionValue;
  style?: ResponsiveSectionValue;
}
export type ResponsiveOverride = ResponsiveSections;
export type ResponsiveOverridePatch = Partial<Record<ResponsiveSection, ResponsiveSectionValue | null | undefined>>;
export type ResponsiveOverrideMap = Readonly<Record<string, ResponsiveOverride>>;
export interface ResponsiveNode { base: ResponsiveSections; responsiveOverrides: ResponsiveOverrideMap }
export type ResponsiveLayoutMode = 'free' | 'flex' | 'grid' | 'constraints';

export interface ResponsivePropertyDiff {
  path: string;
  inheritedValue: unknown;
  currentValue: unknown;
  overrideValue: unknown;
  sourceBreakpointId: string | null;
  hasOverride: boolean;
  changed: boolean;
}

export interface ResponsiveOverrideNormalization {
  changed: boolean;
  errors: string[];
  overrides: ResponsiveOverrideMap;
}

export function resolveResponsiveNode(node: ResponsiveNode, breakpoint: ResponsiveBreakpoint, breakpoints: readonly ResponsiveBreakpoint[]): ResponsiveSections {
  const responsiveOverrides = normalizeResponsiveOverrideMap(node.responsiveOverrides).overrides;
  return mergeResponsiveSections(
    resolveResponsiveInheritedSections({ ...node, responsiveOverrides }, breakpoint, breakpoints),
    responsiveOverrides[breakpoint.id]
  );
}

export function resolveResponsiveInheritedSections(node: ResponsiveNode, breakpoint: ResponsiveBreakpoint, breakpoints: readonly ResponsiveBreakpoint[]): ResponsiveSections {
  const responsiveOverrides = normalizeResponsiveOverrideMap(node.responsiveOverrides).overrides;
  return getInheritanceChain(breakpoint, breakpoints)
    .filter((candidate) => candidate.id !== breakpoint.id)
    .reduce((resolved, candidate) => mergeResponsiveSections(resolved, responsiveOverrides[candidate.id]), cloneSections(node.base));
}

export function mergeResponsiveSections(base: ResponsiveSections, override: ResponsiveSections | undefined): ResponsiveSections {
  if (!override) return cloneSections(base);
  const result = cloneSections(base);
  for (const section of RESPONSIVE_SECTIONS) {
    const patch = override[section];
    if (patch === undefined) continue;
    const merged = mergeRecord(result[section] ?? {}, patch);
    if (Object.keys(merged).length > 0) result[section] = merged;
    else delete result[section];
  }
  return result;
}

export function mergeResponsiveOverride(current: ResponsiveOverride | undefined, patch: ResponsiveOverridePatch): ResponsiveOverride | undefined {
  const result = cloneSections(normalizeResponsiveOverrideValue(current) ?? {});
  for (const section of RESPONSIVE_SECTIONS) {
    const sectionPatch = patch[section];
    if (sectionPatch === undefined) continue;
    if (sectionPatch === null) {
      delete result[section];
      continue;
    }
    const merged = mergeRecord(result[section] ?? {}, sectionPatch);
    if (Object.keys(merged).length > 0) result[section] = merged;
    else delete result[section];
  }
  return Object.keys(result).length > 0 ? result : undefined;
}

export function normalizeResponsiveOverrideMap(value: unknown, path = 'responsiveOverrides'): ResponsiveOverrideNormalization {
  if (value === undefined) return { changed: false, errors: [], overrides: {} };
  if (!isRecord(value)) return { changed: false, errors: [`${path} must be an object`], overrides: {} };
  const errors: string[] = [];
  const overrides: Record<string, ResponsiveOverride> = {};
  let changed = false;
  for (const [breakpointId, rawOverride] of Object.entries(value)) {
    const breakpointPath = `${path}.${breakpointId}`;
    if (!breakpointId.trim()) {
      errors.push(`${path} contains an empty breakpoint id`);
      continue;
    }
    if (!isRecord(rawOverride)) {
      errors.push(`${breakpointPath} must be an object`);
      continue;
    }
    const keys = Object.keys(rawOverride);
    const sectionKeys = keys.filter((key): key is ResponsiveSection => (RESPONSIVE_SECTIONS as readonly string[]).includes(key));
    const flatKeys = keys.filter((key) => !(RESPONSIVE_SECTIONS as readonly string[]).includes(key));
    if (sectionKeys.length > 0 && flatKeys.length > 0) {
      errors.push(`${breakpointPath} mixes flat layout fields with layout/props/style sections`);
      continue;
    }
    if (sectionKeys.length === 0) {
      const layout = normalizeRecord(rawOverride);
      if (Object.keys(layout).length > 0) overrides[breakpointId] = { layout };
      if (keys.length > 0) changed = true;
      continue;
    }
    const normalized: ResponsiveOverride = {};
    for (const section of sectionKeys) {
      const sectionValue = rawOverride[section];
      if (!isRecord(sectionValue)) {
        errors.push(`${breakpointPath}.${section} must be an object`);
        continue;
      }
      const sectionRecord = normalizeRecord(sectionValue);
      if (Object.keys(sectionRecord).length > 0) normalized[section] = sectionRecord;
      else changed = true;
    }
    if (Object.keys(normalized).length > 0) overrides[breakpointId] = normalized;
    else if (keys.length > 0) changed = true;
  }
  return { changed, errors, overrides };
}

export function validateResponsiveOverrideMap(value: unknown, path = 'responsiveOverrides'): string[] {
  if (value === undefined) return [];
  if (!isRecord(value)) return [`${path} must be an object`];
  const errors: string[] = [];
  for (const [breakpointId, rawOverride] of Object.entries(value)) {
    const breakpointPath = `${path}.${breakpointId}`;
    if (!breakpointId.trim()) errors.push(`${path} contains an empty breakpoint id`);
    if (!isRecord(rawOverride)) {
      errors.push(`${breakpointPath} must be an object`);
      continue;
    }
    for (const key of Object.keys(rawOverride)) if (!(RESPONSIVE_SECTIONS as readonly string[]).includes(key)) errors.push(`${breakpointPath}.${key} must be inside layout, props, or style`);
    for (const section of RESPONSIVE_SECTIONS) {
      if (section in rawOverride && !isRecord(rawOverride[section])) errors.push(`${breakpointPath}.${section} must be an object`);
      if (isRecord(rawOverride[section]) && Object.keys(normalizeRecord(rawOverride[section])).length === 0) errors.push(`${breakpointPath}.${section} must not be empty`);
    }
    if (Object.keys(rawOverride).length === 0) errors.push(`${breakpointPath} must not be empty`);
  }
  return errors;
}

export function getInheritanceChain(selected: ResponsiveBreakpoint, breakpoints: readonly ResponsiveBreakpoint[]): ResponsiveBreakpoint[] {
  const candidates = new Map<string, ResponsiveBreakpoint>(breakpoints.map((breakpoint) => [breakpoint.id, breakpoint]));
  candidates.set(selected.id, selected);
  return [...candidates.values()]
    .filter((candidate) => candidate.minWidth <= selected.minWidth && compatibleOrientation(candidate, selected))
    .sort((a, b) => a.minWidth - b.minWidth || specificity(a) - specificity(b) || a.id.localeCompare(b.id));
}

export function createOverrideDiff(base: ResponsiveSectionValue, current: ResponsiveSectionValue, layoutMode?: ResponsiveLayoutMode): ResponsiveSectionValue {
  if (layoutMode) return diffRecord(normalizeLayoutForMode(layoutMode, base), normalizeLayoutForMode(layoutMode, current));
  return diffRecord(base, current);
}

export function createOverridePatch(existing: ResponsiveSectionValue, base: ResponsiveSectionValue, current: ResponsiveSectionValue, layoutMode?: ResponsiveLayoutMode): ResponsiveSectionValue {
  return replaceRecord(existing, createOverrideDiff(base, current, layoutMode));
}

export function cleanResponsiveOverride(override: ResponsiveOverride | undefined, inherited: ResponsiveSections, layoutMode: ResponsiveLayoutMode = 'free'): ResponsiveOverride | undefined {
  const normalizedOverride = normalizeResponsiveOverrideValue(override);
  if (!normalizedOverride) return undefined;
  const result: ResponsiveOverride = {};
  for (const section of RESPONSIVE_SECTIONS) {
    const value = normalizedOverride[section];
    if (!isRecord(value)) continue;
    const diff = createOverrideDiff(inherited[section] ?? {}, value, section === 'layout' ? layoutMode : undefined);
    if (Object.keys(diff).length > 0) result[section] = diff;
  }
  return Object.keys(result).length > 0 ? result : undefined;
}

export function hasOverrideDiff(patch: ResponsiveSectionValue): boolean { return Object.keys(normalizeRecord(patch)).length > 0; }

export function createResponsivePropertyDiff(node: ResponsiveNode, breakpoint: ResponsiveBreakpoint, breakpoints: readonly ResponsiveBreakpoint[]): ResponsivePropertyDiff[] {
  const responsiveOverrides = normalizeResponsiveOverrideMap(node.responsiveOverrides).overrides;
  const normalizedNode = { ...node, responsiveOverrides };
  const inherited = resolveResponsiveInheritedSections(normalizedNode, breakpoint, breakpoints);
  const current = resolveResponsiveNode(normalizedNode, breakpoint, breakpoints);
  const chain = getInheritanceChain(breakpoint, breakpoints);
  const paths = new Set<string>();
  for (const section of RESPONSIVE_SECTIONS) {
    collectPaths(section, inherited[section], paths);
    collectPaths(section, current[section], paths);
    collectPaths(section, responsiveOverrides[breakpoint.id]?.[section], paths);
  }
  return [...paths].sort().map((path) => {
    const [section, ...parts] = path.split('.');
    const inheritedValue = readPath(inherited[section as ResponsiveSection], parts);
    const currentValue = readPath(current[section as ResponsiveSection], parts);
    const overrideValue = readPath(responsiveOverrides[breakpoint.id]?.[section as ResponsiveSection], parts);
    const sourceBreakpointId = findOverrideSource(responsiveOverrides, path, chain);
    return { changed: !valuesEqual(inheritedValue, currentValue), currentValue, hasOverride: sourceBreakpointId === breakpoint.id, inheritedValue, overrideValue, path, sourceBreakpointId };
  }).filter((entry) => entry.changed || entry.sourceBreakpointId !== null);
}

export function createResponsivePropertyResetPatch(path: string): ResponsiveOverridePatch {
  const [section, ...parts] = path.split('.');
  if (!(RESPONSIVE_SECTIONS as readonly string[]).includes(section) || parts.length === 0) return {};
  return { [section]: writeUndefinedPath({}, parts) } as ResponsiveOverridePatch;
}

export function upsertOverride(overrides: ResponsiveOverrideMap, breakpointId: string, patch: ResponsiveOverride): ResponsiveOverrideMap {
  const next = { ...overrides };
  const normalized = normalizeResponsiveOverrideValue(patch) ?? {};
  if (Object.keys(normalized).length > 0) next[breakpointId] = normalized;
  else delete next[breakpointId];
  return next;
}

function diffRecord(base: ResponsiveSectionValue, current: ResponsiveSectionValue): ResponsiveSectionValue {
  const result: ResponsiveSectionValue = {};
  for (const [key, value] of Object.entries(current)) {
    if (value === undefined) continue;
    const baseValue = base[key];
    if (isRecord(value) && isRecord(baseValue)) {
      const nested = diffRecord(baseValue, value);
      if (Object.keys(nested).length > 0) result[key] = nested;
      continue;
    }
    if (!valuesEqual(baseValue, value)) result[key] = cloneValue(value);
  }
  return result;
}

function normalizeLayoutForMode(mode: ResponsiveLayoutMode, value: ResponsiveSectionValue): ResponsiveSectionValue {
  const result = cloneRecord(value);
  delete result.display;
  delete result.layoutMode;
  const flexFields = ['flex', 'flexBasis', 'flexDirection', 'flexGrow', 'flexShrink', 'flexWrap', 'order'];
  const gridFields = ['gridArea', 'gridColumn', 'gridColumnStart', 'gridColumnEnd', 'gridRow', 'gridRowStart', 'gridRowEnd', 'gridTemplateColumns', 'gridTemplateRows', 'gridAutoFlow', 'gridAutoColumns', 'gridAutoRows', 'justifyItems', 'justifySelf'];
  const sharedLayoutFields = ['gap', 'rowGap', 'columnGap', 'alignItems', 'alignContent', 'justifyContent', 'alignSelf'];
  const freePositionFields = ['position', 'x', 'y'];
  const fieldsToRemove = mode === 'free'
    ? ['constraints', ...flexFields, ...gridFields, ...sharedLayoutFields]
    : mode === 'flex'
      ? ['constraints', ...gridFields, ...freePositionFields]
      : mode === 'grid'
        ? ['constraints', ...flexFields, ...freePositionFields]
        : [...flexFields, ...gridFields, ...sharedLayoutFields, ...freePositionFields];
  fieldsToRemove.forEach((field) => delete result[field]);
  return result;
}

function collectPaths(prefix: string, value: ResponsiveSectionValue | undefined, paths: Set<string>): void {
  if (!value) return;
  for (const [key, item] of Object.entries(value)) {
    const path = `${prefix}.${key}`;
    paths.add(path);
    if (isRecord(item)) collectPaths(path, item, paths);
  }
}

function findOverrideSource(overrides: ResponsiveOverrideMap, path: string, chain: readonly ResponsiveBreakpoint[]): string | null {
  return [...chain].reverse().find((candidate) => readPath(overrides[candidate.id]?.[path.split('.')[0] as ResponsiveSection], path.split('.').slice(1)) !== undefined)?.id ?? null;
}

function readPath(value: ResponsiveSectionValue | undefined, path: readonly string[]): unknown {
  let current: unknown = value;
  for (const segment of path) {
    if (!isRecord(current) || !(segment in current)) return undefined;
    current = current[segment];
  }
  return current;
}

function writeUndefinedPath(source: ResponsiveSectionValue, path: readonly string[]): ResponsiveSectionValue {
  const [head, ...tail] = path;
  if (!head) return source;
  return { ...source, [head]: tail.length > 0 ? writeUndefinedPath({}, tail) : undefined };
}

function normalizeResponsiveOverrideValue(value: ResponsiveOverride | undefined): ResponsiveOverride | undefined {
  if (!value) return undefined;
  return normalizeResponsiveOverrideMap({ current: value }).overrides.current;
}

function normalizeRecord(value: ResponsiveSectionValue): ResponsiveSectionValue {
  const result: ResponsiveSectionValue = {};
  for (const [key, item] of Object.entries(value)) {
    if (item === undefined) continue;
    if (isRecord(item)) {
      const nested = normalizeRecord(item);
      if (Object.keys(nested).length > 0) result[key] = nested;
      continue;
    }
    result[key] = cloneValue(item);
  }
  return result;
}

function replaceRecord(existing: ResponsiveSectionValue, diff: ResponsiveSectionValue): ResponsiveSectionValue {
  const result: ResponsiveSectionValue = {};
  const keys = new Set([...Object.keys(existing), ...Object.keys(diff)]);
  for (const key of keys) {
    if (Object.prototype.hasOwnProperty.call(diff, key)) {
      const nextValue = diff[key];
      const existingValue = existing[key];
      if (isRecord(nextValue) && isRecord(existingValue)) {
        const nested = replaceRecord(existingValue, nextValue);
        if (Object.keys(nested).length > 0) result[key] = nested;
      } else {
        result[key] = cloneValue(nextValue);
      }
      continue;
    }
    result[key] = undefined;
  }
  return result;
}

function mergeRecord(base: ResponsiveSectionValue, patch: ResponsiveSectionValue, diffBase?: ResponsiveSectionValue): ResponsiveSectionValue {
  const result = cloneRecord(base);
  for (const [key, value] of Object.entries(patch)) {
    if (value === undefined) {
      delete result[key];
      continue;
    }
    if (isRecord(value) && isRecord(result[key])) {
      const nested = mergeRecord(result[key] as ResponsiveSectionValue, value, isRecord(diffBase?.[key]) ? diffBase[key] as ResponsiveSectionValue : undefined);
      if (Object.keys(nested).length > 0) result[key] = nested;
      else delete result[key];
      continue;
    }
    if (diffBase && Object.prototype.hasOwnProperty.call(diffBase, key) && Object.is(diffBase[key], value)) continue;
    result[key] = cloneValue(value);
  }
  return result;
}

function cloneSections(value: ResponsiveSections): ResponsiveSections {
  const result: ResponsiveSections = {};
  for (const section of RESPONSIVE_SECTIONS) if (isRecord(value[section])) result[section] = cloneRecord(value[section]);
  return result;
}

function cloneRecord(value: ResponsiveSectionValue): ResponsiveSectionValue { return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, cloneValue(item)])); }
function cloneValue(value: unknown): unknown { return isRecord(value) ? cloneRecord(value) : Array.isArray(value) ? value.map(cloneValue) : value; }
function valuesEqual(left: unknown, right: unknown): boolean {
  if (Object.is(left, right)) return true;
  if (Array.isArray(left) && Array.isArray(right)) return left.length === right.length && left.every((item, index) => valuesEqual(item, right[index]));
  if (isRecord(left) && isRecord(right)) {
    const leftKeys = Object.keys(left);
    const rightKeys = Object.keys(right);
    return leftKeys.length === rightKeys.length && leftKeys.every((key) => Object.prototype.hasOwnProperty.call(right, key) && valuesEqual(left[key], right[key]));
  }
  return false;
}
function compatibleOrientation(candidate: ResponsiveBreakpoint, selected: ResponsiveBreakpoint): boolean { return !candidate.orientation || !selected.orientation || candidate.orientation === selected.orientation; }
function specificity(breakpoint: ResponsiveBreakpoint): number { return breakpoint.orientation ? 1 : 0; }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
