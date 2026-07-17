import type { DesignerDeviceSession, DesignerEditorSession } from '../document/DesignerEditorSession';

import { DEFAULT_RESPONSIVE_BREAKPOINTS } from './responsiveModel';
import type { ResponsiveBreakpoint } from './responsiveModel';

export { DEFAULT_RESPONSIVE_BREAKPOINTS } from './responsiveModel';

export interface SafeAreaInsets { top: number; right: number; bottom: number; left: number }
export interface ResponsiveDeviceProfile { id: string; name: string; width: number; height: number; pixelRatio: number; orientation: 'portrait' | 'landscape'; safeArea: SafeAreaInsets }
export interface ResponsivePreviewViewport { width: number; height: number; safeArea: SafeAreaInsets; breakpoint: ResponsiveBreakpoint }
export interface CanvasDevicePreview { device: DesignerDeviceSession; breakpoint: ResponsiveBreakpoint; width: number; height: number }

export const DEFAULT_DEVICE_PROFILES: readonly ResponsiveDeviceProfile[] = [
  { id: 'phone-se', name: 'Mobile · iPhone SE', width: 375, height: 667, pixelRatio: 2, orientation: 'portrait', safeArea: { top: 20, right: 0, bottom: 0, left: 0 } },
  { id: 'phone-se-landscape', name: 'Mobile · iPhone SE landscape', width: 667, height: 375, pixelRatio: 2, orientation: 'landscape', safeArea: { top: 0, right: 0, bottom: 0, left: 0 } },
  { id: 'phone-portrait', name: 'Mobile · iPhone 14', width: 390, height: 844, pixelRatio: 3, orientation: 'portrait', safeArea: { top: 47, right: 0, bottom: 34, left: 0 } },
  { id: 'phone-large', name: 'Mobile · iPhone 14 Pro Max', width: 430, height: 932, pixelRatio: 3, orientation: 'portrait', safeArea: { top: 47, right: 0, bottom: 34, left: 0 } },
  { id: 'android-compact', name: 'Mobile · Android compact', width: 360, height: 800, pixelRatio: 3, orientation: 'portrait', safeArea: { top: 24, right: 0, bottom: 0, left: 0 } },
  { id: 'tablet-portrait', name: 'Tablet · portrait', width: 768, height: 1024, pixelRatio: 2, orientation: 'portrait', safeArea: { top: 0, right: 0, bottom: 20, left: 0 } },
  { id: 'tablet-landscape', name: 'Tablet · landscape', width: 1024, height: 768, pixelRatio: 2, orientation: 'landscape', safeArea: { top: 0, right: 0, bottom: 20, left: 0 } },
  { id: 'desktop', name: 'Desktop · 1440', width: 1440, height: 900, pixelRatio: 1, orientation: 'landscape', safeArea: { top: 0, right: 0, bottom: 0, left: 0 } },
  { id: 'desktop-wide', name: 'Desktop · wide', width: 1920, height: 1080, pixelRatio: 1, orientation: 'landscape', safeArea: { top: 0, right: 0, bottom: 0, left: 0 } }
];

const BREAKPOINT_DEVICE_PROFILE_IDS: Record<string, string> = {
  mobile: 'phone-portrait',
  tablet: 'tablet-landscape',
  desktop: 'desktop'
};

export function deviceProfileForBreakpoint(breakpointId: string): ResponsiveDeviceProfile | null {
  const profileId = BREAKPOINT_DEVICE_PROFILE_IDS[breakpointId];
  return DEFAULT_DEVICE_PROFILES.find((profile) => profile.id === profileId) ?? null;
}

export function toDesignerDeviceSession(profile: ResponsiveDeviceProfile, breakpointId: string): DesignerDeviceSession {
  return {
    browserBar: { bottom: 0, top: profile.id === 'phone-portrait' ? 24 : 0 },
    breakpointId,
    height: profile.height,
    id: profile.id,
    orientation: profile.orientation,
    pixelRatio: profile.pixelRatio,
    safeArea: { ...profile.safeArea },
    width: profile.width
  };
}

export function resolveCanvasPreviewViewport(session: DesignerEditorSession): { height: number; width: number; device: DesignerDeviceSession | null } {
  if (!session.canvas.device) return { device: null, height: session.viewport.height, width: session.viewport.width };
  return { device: session.canvas.device, height: session.canvas.device.height, width: session.canvas.device.width };
}

export function applyDevicePreviewProfile(profileId: string, breakpoints: readonly ResponsiveBreakpoint[] = DEFAULT_RESPONSIVE_BREAKPOINTS): CanvasDevicePreview | null {
  const profile = DEFAULT_DEVICE_PROFILES.find((candidate) => candidate.id === profileId);
  if (!profile) return null;
  const viewport = createResponsivePreviewViewport(profile, breakpoints);
  return { breakpoint: viewport.breakpoint, device: toDesignerDeviceSession(profile, viewport.breakpoint.id), height: profile.height, width: profile.width };
}

export function clearDevicePreview(): { device: null; height: number; width: number } {
  return { device: null, height: 720, width: 1280 };
}

export function createResponsivePreviewViewport(profile: ResponsiveDeviceProfile, breakpoints: readonly ResponsiveBreakpoint[]): ResponsivePreviewViewport {
  const breakpoint = [...breakpoints].filter((candidate) => candidate.minWidth <= profile.width && (candidate.maxWidth === undefined || candidate.maxWidth >= profile.width) && (!candidate.orientation || candidate.orientation === profile.orientation)).sort((a, b) => b.minWidth - a.minWidth || specificity(b) - specificity(a) || a.id.localeCompare(b.id))[0];
  if (!breakpoint) throw new Error(`No responsive breakpoint matches device width ${profile.width}`);
  return { width: profile.width, height: profile.height, safeArea: { ...profile.safeArea }, breakpoint };
}

function specificity(breakpoint: ResponsiveBreakpoint): number { return breakpoint.orientation ? 1 : 0; }
