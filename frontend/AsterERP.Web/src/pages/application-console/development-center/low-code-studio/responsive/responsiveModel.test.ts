import { describe, expect, it } from 'vitest';

import { applyDevicePreviewProfile, clearDevicePreview, createResponsivePreviewViewport, DEFAULT_DEVICE_PROFILES, deviceProfileForBreakpoint, resolveCanvasPreviewViewport, toDesignerDeviceSession } from './deviceProfiles';
import { DEFAULT_RESPONSIVE_BREAKPOINTS, cleanResponsiveOverride, createOverrideDiff, createOverridePatch, createResponsivePropertyDiff, createResponsivePropertyResetPatch, getInheritanceChain, hasOverrideDiff, mergeResponsiveOverride, normalizeResponsiveOverrideMap, resolveResponsiveInheritedSections, resolveResponsiveNode, upsertOverride, validateResponsiveOverrideMap, type ResponsiveLayoutMode, type ResponsiveOverrideMap } from './responsiveModel';

describe('responsive override diff', () => {
  const breakpoints = [{ id: 'phone', minWidth: 0 }, { id: 'tablet', minWidth: 768 }, { id: 'desktop', minWidth: 1200 }];

  it('inherits base then applies ordered breakpoint overrides', () => {
    const node = { base: { layout: { width: '100%', gap: 8 }, props: { title: 'base' }, style: { border: { color: 'red' } } }, responsiveOverrides: { tablet: { layout: { gap: 12 }, props: { title: 'tablet' } }, desktop: { layout: { width: 640 }, style: { border: { width: 1 } } } } };
    expect(resolveResponsiveNode(node, breakpoints[2], breakpoints)).toEqual({ layout: { width: 640, gap: 12 }, props: { title: 'tablet' }, style: { border: { color: 'red', width: 1 } } });
  });

  it('stores only changed values and identifies empty diffs', () => {
    expect(createOverrideDiff({ width: 10, color: 'red' }, { width: 20, color: 'red' })).toEqual({ width: 20 });
    expect(hasOverrideDiff(createOverrideDiff({ width: 10 }, { width: 10 }))).toBe(false);
  });

  it('keeps only changed nested values and compares arrays structurally', () => {
    const diff = createOverrideDiff(
      { style: { border: { color: 'red', width: 1 } }, columns: ['id', 'name'] },
      { style: { border: { color: 'blue', width: 1 } }, columns: ['id', 'name'] }
    );
    expect(diff).toEqual({ style: { border: { color: 'blue' } } });
    expect(hasOverrideDiff({ style: { border: {} } })).toBe(false);
    expect(upsertOverride({}, 'tablet', { style: { border: {} } })).toEqual({});
  });

  it('replaces an existing nested override against inherited values and emits explicit clears', () => {
    expect(createOverridePatch(
      { constraints: { left: 10, right: 20 }, width: 300 },
      { constraints: { left: 10, right: 20 }, width: 200 },
      { constraints: { left: 12, right: 20 }, width: 200 }
    )).toEqual({ constraints: { left: 12, right: undefined }, width: undefined });
  });

  it('canonicalizes empty sections and rejects empty in-memory overrides', () => {
    expect(normalizeResponsiveOverrideMap({ tablet: { layout: {} }, desktop: {} })).toEqual({ changed: true, errors: [], overrides: {} });
    expect(validateResponsiveOverrideMap({ tablet: { layout: {} } })).toEqual(['responsiveOverrides.tablet.layout must not be empty']);
  });

  it('canonicalizes legacy flat overrides and remains stable after save/reload', () => {
    const base = { layout: { width: 320, gap: 8 }, props: { title: 'base' } };
    const legacy = { tablet: { width: 640, x: 12 }, desktop: { layout: { gap: 16 } } } as unknown as ResponsiveOverrideMap;
    const normalized = normalizeResponsiveOverrideMap(legacy);

    expect(normalized).toEqual({
      changed: true,
      errors: [],
      overrides: { tablet: { layout: { width: 640, x: 12 } }, desktop: { layout: { gap: 16 } } }
    });
    expect(normalizeResponsiveOverrideMap(normalized.overrides)).toEqual({ changed: false, errors: [], overrides: normalized.overrides });
    expect(resolveResponsiveNode({ base, responsiveOverrides: legacy }, breakpoints[2], breakpoints)).toEqual({ layout: { width: 640, gap: 16, x: 12 }, props: { title: 'base' } });
    expect(base).toEqual({ layout: { width: 320, gap: 8 }, props: { title: 'base' } });
  });

  it('reports malformed override shapes without accepting them as canonical data', () => {
    expect(normalizeResponsiveOverrideMap({ tablet: { layout: null }, desktop: { width: 640, layout: { gap: 12 } } })).toEqual({
      changed: true,
      errors: [
        'responsiveOverrides.tablet.layout must be an object',
        'responsiveOverrides.desktop mixes flat layout fields with layout/props/style sections'
      ],
      overrides: {}
    });
    expect(validateResponsiveOverrideMap({ tablet: { width: 640 } })).toEqual(['responsiveOverrides.tablet.width must be inside layout, props, or style']);
  });

  it('uses the selected breakpoint range and removes empty overrides', () => {
    const ranges = [{ id: 'phone', minWidth: 0, maxWidth: 767 }, { id: 'tablet', minWidth: 768, maxWidth: 1199 }, { id: 'desktop', minWidth: 1200 }];
    expect(getInheritanceChain(ranges[1], ranges).map((item) => item.id)).toEqual(['phone', 'tablet']);
    expect(upsertOverride({ tablet: { layout: { gap: 12 } } }, 'tablet', {})).toEqual({});
  });

  it('deep-merges sections and clears fields back to inheritance', () => {
    const merged = mergeResponsiveOverride({ layout: { width: 640, x: 12 }, props: { title: 'Tablet' }, style: { border: { color: 'red' } } }, { layout: { x: 24, width: undefined }, props: null, style: { border: { width: 1 } } });
    expect(merged).toEqual({ layout: { x: 24 }, style: { border: { color: 'red', width: 1 } } });
  });

  it('maps device dimensions, orientation and safe area to one breakpoint', () => {
    const viewport = createResponsivePreviewViewport({ id: 'custom', name: 'Custom', width: 390, height: 844, pixelRatio: 3, orientation: 'portrait', safeArea: { top: 10, right: 2, bottom: 20, left: 2 } }, [{ id: 'phone', minWidth: 0, maxWidth: 767, orientation: 'portrait' }]);
    expect(viewport).toMatchObject({ width: 390, height: 844, breakpoint: { id: 'phone' }, safeArea: { top: 10, bottom: 20 } });
  });

  it('creates canonical device sessions for the toolbar preview profiles', () => {
    const profile = deviceProfileForBreakpoint('mobile');
    expect(profile).toMatchObject({ id: 'phone-portrait', width: 390, height: 844, pixelRatio: 3 });
    expect(toDesignerDeviceSession(profile!, 'mobile')).toMatchObject({ id: 'phone-portrait', breakpointId: 'mobile', width: 390, height: 844, pixelRatio: 3 });
    expect(applyDevicePreviewProfile('phone-large')).toMatchObject({ width: 430, height: 932, breakpoint: { id: 'mobile' } });
    expect(clearDevicePreview()).toEqual({ device: null, width: 1280, height: 720 });
  });

  it('defines a real mobile landscape profile without changing mobile breakpoint semantics', () => {
    const profile = DEFAULT_DEVICE_PROFILES.find((candidate) => candidate.id === 'phone-se-landscape');
    expect(profile).toEqual({ id: 'phone-se-landscape', name: 'Mobile · iPhone SE landscape', width: 667, height: 375, pixelRatio: 2, orientation: 'landscape', safeArea: { top: 0, right: 0, bottom: 0, left: 0 } });
  });

  it('materializes the mobile landscape profile into a canonical designer session', () => {
    const profile = DEFAULT_DEVICE_PROFILES.find((candidate) => candidate.id === 'phone-se-landscape');
    if (!profile) throw new Error('Expected the mobile landscape profile');

    expect(toDesignerDeviceSession(profile, 'mobile')).toEqual({ browserBar: { bottom: 0, top: 0 }, breakpointId: 'mobile', height: 375, id: 'phone-se-landscape', orientation: 'landscape', pixelRatio: 2, safeArea: { top: 0, right: 0, bottom: 0, left: 0 }, width: 667 });
  });

  it('resolves the mobile landscape viewport through the existing mobile breakpoint', () => {
    const profile = DEFAULT_DEVICE_PROFILES.find((candidate) => candidate.id === 'phone-se-landscape');
    if (!profile) throw new Error('Expected the mobile landscape profile');

    expect(createResponsivePreviewViewport(profile, DEFAULT_RESPONSIVE_BREAKPOINTS)).toEqual({ breakpoint: DEFAULT_RESPONSIVE_BREAKPOINTS[0], height: 375, safeArea: { top: 0, right: 0, bottom: 0, left: 0 }, width: 667 });
    expect(applyDevicePreviewProfile(profile.id)).toMatchObject({ breakpoint: { id: 'mobile' }, device: { id: profile.id, orientation: 'landscape', breakpointId: 'mobile' }, height: 375, width: 667 });
  });

  it('resolves the device viewport without changing document state', () => {
    const session = { canvas: { device: toDesignerDeviceSession(deviceProfileForBreakpoint('tablet')!, 'tablet') }, viewport: { width: 1024, height: 768 } } as never;
    expect(resolveCanvasPreviewViewport(session)).toMatchObject({ width: 1024, height: 768, device: { breakpointId: 'tablet' } });
  });

  it('applies an orientation-specific override after the generic breakpoint at the same width', () => {
    const candidates = [
      { id: 'tablet-landscape', minWidth: 768, orientation: 'landscape' as const },
      { id: 'tablet', minWidth: 768 }
    ];
    const node = { base: { layout: { gap: 8 } }, responsiveOverrides: { tablet: { layout: { gap: 12 } }, 'tablet-landscape': { layout: { gap: 16 } } } };
    expect(resolveResponsiveNode(node, candidates[0], candidates)).toEqual({ layout: { gap: 16 } });
    expect(createResponsivePreviewViewport({ id: 'tablet-test', name: 'Tablet', width: 1024, height: 768, pixelRatio: 2, orientation: 'landscape', safeArea: { top: 0, right: 0, bottom: 0, left: 0 } }, candidates).breakpoint.id).toBe('tablet-landscape');
  });

  it('includes a selected breakpoint even when the caller omitted it from the candidate list', () => {
    const selected = { id: 'custom', minWidth: 900 };
    expect(getInheritanceChain(selected, [{ id: 'phone', minWidth: 0 }]).map((item) => item.id)).toEqual(['phone', 'custom']);
    expect(resolveResponsiveNode({ base: { layout: { width: 320 } }, responsiveOverrides: { custom: { layout: { width: 640 } } } }, selected, [{ id: 'phone', minWidth: 0 }])).toEqual({ layout: { width: 640 } });
  });

  it('applies the base/mobile/tablet/desktop inheritance chain without mutating base', () => {
    const base = { layout: { width: 320, gap: 8 }, props: { title: 'base' } };
    const node = {
      base,
      responsiveOverrides: {
        mobile: { layout: { width: 390 } },
        tablet: { layout: { gap: 12 }, props: { title: 'tablet' } },
        desktop: { layout: { width: 1440 }, style: { color: 'blue' } }
      }
    };

    expect(resolveResponsiveNode(node, DEFAULT_RESPONSIVE_BREAKPOINTS[0], DEFAULT_RESPONSIVE_BREAKPOINTS)).toEqual({ layout: { width: 390, gap: 8 }, props: { title: 'base' } });
    expect(resolveResponsiveNode(node, DEFAULT_RESPONSIVE_BREAKPOINTS[1], DEFAULT_RESPONSIVE_BREAKPOINTS)).toEqual({ layout: { width: 390, gap: 12 }, props: { title: 'tablet' } });
    expect(resolveResponsiveNode(node, DEFAULT_RESPONSIVE_BREAKPOINTS[2], DEFAULT_RESPONSIVE_BREAKPOINTS)).toEqual({ layout: { width: 1440, gap: 12 }, props: { title: 'tablet' }, style: { color: 'blue' } });
    expect(base).toEqual({ layout: { width: 320, gap: 8 }, props: { title: 'base' } });
  });

  it('resolves inherited values without applying the selected breakpoint', () => {
    const node = { base: { layout: { width: 320, gap: 8 } }, responsiveOverrides: { tablet: { layout: { width: 640 } }, desktop: { layout: { gap: 16 } } } };
    expect(resolveResponsiveInheritedSections(node, breakpoints[2], breakpoints)).toEqual({ layout: { width: 640, gap: 8 } });
  });

  it('reports inherited/current values and the nearest override source per property', () => {
    const node = { base: { layout: { width: 320, gap: 8 } }, responsiveOverrides: { tablet: { layout: { width: 640 } }, desktop: { layout: { gap: 16 } } } };
    expect(createResponsivePropertyDiff(node, breakpoints[2], breakpoints)).toEqual(expect.arrayContaining([
      expect.objectContaining({ path: 'layout.width', inheritedValue: 640, currentValue: 640, hasOverride: false, sourceBreakpointId: 'tablet', changed: false }),
      expect.objectContaining({ path: 'layout.gap', inheritedValue: 8, currentValue: 16, hasOverride: true, sourceBreakpointId: 'desktop', overrideValue: 16, changed: true })
    ]));
  });

  it('creates a nested undefined patch that resets one selected breakpoint property', () => {
    expect(createResponsivePropertyResetPatch('layout.constraints.left')).toEqual({ layout: { constraints: { left: undefined } } });
  });

  it.each([
    { mode: 'free' as ResponsiveLayoutMode, forbidden: ['constraints', 'flexGrow', 'flexWrap', 'gridRow', 'gap', 'alignItems'] },
    { mode: 'flex' as ResponsiveLayoutMode, forbidden: ['constraints', 'gridRow', 'gridTemplateColumns', 'position', 'x', 'y'] },
    { mode: 'grid' as ResponsiveLayoutMode, forbidden: ['constraints', 'flexGrow', 'flexWrap', 'position', 'x', 'y'] },
    { mode: 'constraints' as ResponsiveLayoutMode, forbidden: ['flexGrow', 'flexWrap', 'gridRow', 'gridTemplateColumns', 'gap', 'alignItems', 'position', 'x', 'y'] }
  ])('creates the minimal structured $mode diff without stale mode fields', ({ mode, forbidden }) => {
    const base = { display: 'block', layoutMode: 'free', width: 320, x: 10, y: 20, position: 'absolute', flexGrow: 0, flexWrap: 'nowrap', gridRow: 1, gridTemplateColumns: '1fr', constraints: { left: 8 }, gap: 4, alignItems: 'start' };
    const current = { ...base, display: 'grid', layoutMode: 'grid', width: 640, x: 80, y: 40, position: 'relative', flexGrow: 2, flexWrap: 'wrap', gridRow: 2, gridTemplateColumns: 'repeat(2, 1fr)', constraints: { left: 16 }, gap: 12, alignItems: 'center' };
    const diff = createOverrideDiff(base, current, mode);

    expect(diff).toHaveProperty('width', 640);
    expect(diff).not.toHaveProperty('display');
    expect(diff).not.toHaveProperty('layoutMode');
    for (const field of forbidden) expect(diff).not.toHaveProperty(field);
  });

  it('cleans values equal to inherited sections and never mutates the base or inherited objects', () => {
    const inherited = { layout: { width: 320, constraints: { left: 8, right: 12 } }, props: { title: 'Base' }, style: { color: 'red' } };
    const override = { layout: { display: 'grid', width: 320, constraints: { left: 8, right: 24 } }, props: { title: 'Base' }, style: { color: 'blue' } };
    const inheritedSnapshot = structuredClone(inherited);
    const cleaned = cleanResponsiveOverride(override, inherited, 'constraints');

    expect(cleaned).toEqual({ layout: { constraints: { right: 24 } }, style: { color: 'blue' } });
    expect(inherited).toEqual(inheritedSnapshot);
    expect(override).toEqual({ layout: { display: 'grid', width: 320, constraints: { left: 8, right: 24 } }, props: { title: 'Base' }, style: { color: 'blue' } });
  });

  it('emits explicit clears without writing inherited values into the override patch', () => {
    const base = { width: 320, constraints: { left: 8, right: 12 } };
    const current = { width: 320, constraints: { left: 8, right: 24 } };
    expect(createOverridePatch({ width: 640, constraints: { left: 8, right: 16 } }, base, current, 'constraints')).toEqual({ width: undefined, constraints: { left: undefined, right: 24 } });
    expect(base).toEqual({ width: 320, constraints: { left: 8, right: 12 } });
  });
});
