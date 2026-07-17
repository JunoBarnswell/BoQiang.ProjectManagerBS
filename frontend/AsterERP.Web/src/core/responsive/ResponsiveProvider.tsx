import type { ReactNode } from 'react';
import { createContext, useContext, useEffect, useMemo, useState } from 'react';

import { useUiPreferenceStore } from '../state/uiPreferenceStore';
import { getUiScaleRatio, type UiScalePercent } from '../ui-preferences/uiPreferenceOptions';

import { getBreakpoint, getDensity } from './breakpoint';
import { getResponsiveTokens, scaleResponsiveTokens } from './responsiveTokens';

export interface ViewportSize {
  breakpoint: ReturnType<typeof getBreakpoint>;
  density: ReturnType<typeof getDensity>;
  height: number;
  rawHeight: number;
  rawWidth: number;
  scalePercent: UiScalePercent;
  width: number;
}

interface ResponsiveContextValue extends ViewportSize {
  tokens: ReturnType<typeof getResponsiveTokens>;
}

const ResponsiveContext = createContext<ResponsiveContextValue | null>(null);

function getViewportSnapshot(scalePercent: UiScalePercent): ViewportSize {
  const rawWidth = typeof window === 'undefined' ? 1440 : window.innerWidth;
  const rawHeight = typeof window === 'undefined' ? 900 : window.innerHeight;
  const scaleRatio = getUiScaleRatio(scalePercent);
  const width = Math.max(1, Math.round(rawWidth / scaleRatio));
  const height = Math.max(1, Math.round(rawHeight / scaleRatio));

  return {
    breakpoint: getBreakpoint(width),
    density: getDensity(width),
    height,
    rawHeight,
    rawWidth,
    scalePercent,
    width
  };
}

export function ResponsiveProvider({ children }: { children: ReactNode }) {
  const scalePercent = useUiPreferenceStore((state) => state.scalePercent);
  const [viewport, setViewport] = useState<ViewportSize>(() => getViewportSnapshot(scalePercent));

  useEffect(() => {
    let frameId = 0;

    const updateViewport = () => {
      cancelAnimationFrame(frameId);
      frameId = window.requestAnimationFrame(() => {
        setViewport(getViewportSnapshot(scalePercent));
      });
    };

    updateViewport();

    window.addEventListener('resize', updateViewport, { passive: true });
    window.addEventListener('orientationchange', updateViewport, { passive: true });
    window.visualViewport?.addEventListener('resize', updateViewport, { passive: true });

    return () => {
      cancelAnimationFrame(frameId);
      window.removeEventListener('resize', updateViewport);
      window.removeEventListener('orientationchange', updateViewport);
      window.visualViewport?.removeEventListener('resize', updateViewport);
    };
  }, [scalePercent]);

  const tokens = useMemo(
    () => scaleResponsiveTokens(getResponsiveTokens(viewport.width), getUiScaleRatio(viewport.scalePercent)),
    [viewport.scalePercent, viewport.width]
  );

  useEffect(() => {
    const root = document.documentElement;

    root.dataset.breakpoint = viewport.breakpoint;
    root.dataset.density = viewport.density;
    root.style.setProperty('--erp-viewport-width', `${viewport.width}px`);
    root.style.setProperty('--erp-viewport-height', `${viewport.height}px`);
    root.style.setProperty('--erp-raw-viewport-width', `${viewport.rawWidth}px`);
    root.style.setProperty('--erp-raw-viewport-height', `${viewport.rawHeight}px`);
    root.style.setProperty('--erp-effective-viewport-width', `${viewport.width}px`);
    root.style.setProperty('--erp-effective-viewport-height', `${viewport.height}px`);
    root.style.setProperty('--erp-page-padding', `${tokens.pagePadding}px`);
    root.style.setProperty('--erp-section-gap', `${tokens.sectionGap}px`);
    root.style.setProperty('--erp-content-gap', `${tokens.contentGap}px`);
    root.style.setProperty('--erp-toolbar-gap', `${tokens.toolbarGap}px`);
    root.style.setProperty('--erp-toolbar-height', `${tokens.toolbarHeight}px`);
    root.style.setProperty('--erp-header-height', `${tokens.headerHeight}px`);
    root.style.setProperty('--erp-card-radius', `${tokens.cardRadius}px`);
    root.style.setProperty('--erp-table-row-height', `${tokens.tableRowHeight}px`);
    root.style.setProperty('--erp-modal-width', `${tokens.modalWidth}px`);
  }, [tokens, viewport.breakpoint, viewport.density, viewport.height, viewport.rawHeight, viewport.rawWidth, viewport.width]);

  return <ResponsiveContext.Provider value={{ ...viewport, tokens }}>{children}</ResponsiveContext.Provider>;
}

export function useResponsiveContext() {
  const context = useContext(ResponsiveContext);

  if (!context) {
    throw new Error('useResponsiveContext must be used inside ResponsiveProvider');
  }

  return context;
}

