import { useEffect, useRef, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useUiPreferenceStore } from '../../core/state/uiPreferenceStore';
import {
  uiFontFamilyOptions,
  uiScaleOptions,
  type UiFontFamilyKey,
  type UiScalePercent
} from '../../core/ui-preferences/uiPreferenceOptions';
import { AppIcon } from '../../shared/icons/AppIcon';

export function DisplayPreferenceControl() {
  const { translate } = useI18n();
  const fontFamilyKey = useUiPreferenceStore((state) => state.fontFamilyKey);
  const resetPreferences = useUiPreferenceStore((state) => state.resetPreferences);
  const scalePercent = useUiPreferenceStore((state) => state.scalePercent);
  const setFontFamilyKey = useUiPreferenceStore((state) => state.setFontFamilyKey);
  const setScalePercent = useUiPreferenceStore((state) => state.setScalePercent);
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    };

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  return (
    <div className="display-preference-control" ref={rootRef}>
      <button
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label={translate('display.title')}
        className="display-preference-control__trigger"
        title={translate('display.title')}
        type="button"
        onClick={() => setOpen((current) => !current)}
      >
        <AppIcon name="sliders-horizontal" />
        <span>{scalePercent}%</span>
      </button>

      {open ? (
        <div className="display-preference-control__panel" role="menu">
          <div className="display-preference-control__header">
            <span>{translate('display.title')}</span>
            <button className="display-preference-control__reset" type="button" onClick={resetPreferences}>
              {translate('display.reset')}
            </button>
          </div>

          <div className="display-preference-control__group">
            <div className="display-preference-control__label">{translate('display.scale')}</div>
            <div className="display-preference-control__scale-grid">
              {uiScaleOptions.map((option) => (
                <button
                  aria-pressed={option === scalePercent}
                  className="display-preference-control__scale-option"
                  key={option}
                  type="button"
                  onClick={() => setScalePercent(option as UiScalePercent)}
                >
                  {option}%
                </button>
              ))}
            </div>
          </div>

          <label className="display-preference-control__group">
            <span className="display-preference-control__label">{translate('display.font')}</span>
            <select
              className="display-preference-control__select"
              value={fontFamilyKey}
              onChange={(event) => setFontFamilyKey(event.target.value as UiFontFamilyKey)}
            >
              {uiFontFamilyOptions.map((option) => (
                <option key={option.key} value={option.key}>
                  {translate(option.labelKey)}
                </option>
              ))}
            </select>
          </label>
        </div>
      ) : null}
    </div>
  );
}
