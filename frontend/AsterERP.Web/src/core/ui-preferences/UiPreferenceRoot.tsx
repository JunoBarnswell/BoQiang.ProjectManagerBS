import { useEffect, type ReactNode } from 'react';

import { useUiPreferenceStore } from '../state/uiPreferenceStore';

import { getUiScaleRatio, resolveUiFontFamilyCssValue } from './uiPreferenceOptions';

export function UiPreferenceRoot({ children }: { children: ReactNode }) {
  const fontFamilyKey = useUiPreferenceStore((state) => state.fontFamilyKey);
  const scalePercent = useUiPreferenceStore((state) => state.scalePercent);

  useEffect(() => {
    const root = document.documentElement;
    const scaleRatio = getUiScaleRatio(scalePercent);

    root.dataset.uiFont = fontFamilyKey;
    root.dataset.uiScale = String(scalePercent);
    root.style.setProperty('--app-ui-scale', String(scaleRatio));
    root.style.setProperty('--app-ui-scale-percent', String(scalePercent));
    root.style.setProperty('--app-ui-font-family', resolveUiFontFamilyCssValue(fontFamilyKey));
    root.style.setProperty('--app-root-font-size', `${13 * scaleRatio}px`);
    root.style.setProperty('--app-text-2xs', `${10 * scaleRatio}px`);
    root.style.setProperty('--app-text-3xs', `${11 * scaleRatio}px`);
    root.style.setProperty('--app-text-xs', `${12 * scaleRatio}px`);
    root.style.setProperty('--app-text-sm', `${13 * scaleRatio}px`);
    root.style.setProperty('--app-text-base', `${14 * scaleRatio}px`);
    root.style.setProperty('--app-text-lg', `${16 * scaleRatio}px`);
  }, [fontFamilyKey, scalePercent]);

  return <>{children}</>;
}
