import { useEffect, type ReactNode } from 'react';

import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { getResponsiveModalMode, getResponsiveModalWidth, type ResponsiveModalMode } from '../../core/responsive/breakpoint';
import { useViewportSize } from '../../core/responsive/useViewportSize';
import { getUiScaleRatio } from '../../core/ui-preferences/uiPreferenceOptions';

interface ResponsiveModalProps {
  bodyClassName?: string;
  children: ReactNode;
  className?: string;
  closeOnEscape?: boolean;
  description?: ReactNode;
  mode?: ResponsiveModalMode;
  onClose: () => void;
  open: boolean;
  title: string;
  footer?: ReactNode;
  fullscreenOnSmall?: boolean;
  maxWidth?: number | string;
}

export function ResponsiveModal({
  bodyClassName,
  children,
  className,
  closeOnEscape = true,
  description,
  footer,
  fullscreenOnSmall = false,
  maxWidth,
  mode = 'auto',
  onClose,
  open,
  title
}: ResponsiveModalProps) {
  const { rawWidth, scalePercent, width } = useViewportSize();
  const scaleRatio = getUiScaleRatio(scalePercent);
  const resolvedTitle = translateCurrentLiteral(title);
  const resolvedDescription = typeof description === 'string' ? translateCurrentLiteral(description) : description;
  const baseMode = mode === 'drawer' ? 'drawer' : getResponsiveModalMode(width, mode);
  const resolvedMode = fullscreenOnSmall && width < 768 ? 'fullscreen' : baseMode;
  const modalWidth = Math.round(getResponsiveModalWidth(width) * scaleRatio);
  const resolvedModalWidth = typeof maxWidth === 'number' ? `${Math.round(maxWidth * scaleRatio)}px` : maxWidth ?? `${modalWidth}px`;
  const availableModalWidth = `calc(${rawWidth}px - calc(48px * var(--app-ui-scale)))`;

  useEffect(() => {
    if (!open || !closeOnEscape) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [closeOnEscape, onClose, open]);

  if (!open) {
    return null;
  }

  return (
    <div className="responsive-modal-overlay" data-mode={resolvedMode} role="presentation" onClick={onClose}>
      <div
        aria-label={resolvedTitle}
        aria-modal="true"
        className={`responsive-modal-surface responsive-modal-surface--${resolvedMode}${className ? ` ${className}` : ''}`}
        role="dialog"
        style={resolvedMode === 'modal' ? { width: `min(${availableModalWidth}, ${resolvedModalWidth})` } : undefined}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="responsive-modal-header">
          <div>
            <h3>{resolvedTitle}</h3>
            {resolvedDescription ? <div className="responsive-modal-description">{resolvedDescription}</div> : null}
          </div>
          <button className="icon-button" type="button" onClick={onClose}>
            ×
          </button>
        </div>

        <div className={`responsive-modal-body${bodyClassName ? ` ${bodyClassName}` : ''}`}>{children}</div>

        {footer ? <div className="responsive-modal-footer">{footer}</div> : null}
      </div>
    </div>
  );
}
