import type { ButtonHTMLAttributes, ReactNode } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { getResponsiveToolbarVisibleCount } from '../../core/responsive/breakpoint';
import { useBreakpoint } from '../../core/responsive/useBreakpoint';
import { useViewportSize } from '../../core/responsive/useViewportSize';
import { PermissionButton } from '../auth/PermissionButton';

export interface ResponsiveToolbarAction {
  code?: string;
  iconEnd?: string | null;
  iconStart?: string | null;
  disabled?: boolean;
  onClick?: () => void;
  priority?: number;
  text: ReactNode;
  type?: ButtonHTMLAttributes<HTMLButtonElement>['type'];
  variant?: 'ghost' | 'primary';
}

interface ResponsiveToolbarProps {
  actions?: ResponsiveToolbarAction[];
  title?: ReactNode;
}

function ActionButton({ action }: { action: ResponsiveToolbarAction }) {
  const buttonClassName = action.variant === 'primary' ? 'primary-button' : 'ghost-button';

  if (action.code) {
    return (
      <PermissionButton
        className={buttonClassName}
        code={action.code}
        disabled={action.disabled}
        iconEnd={action.iconEnd}
        iconStart={action.iconStart}
        onClick={action.onClick}
        type={action.type}
      >
        {action.text}
      </PermissionButton>
    );
  }

  return (
    <button className={buttonClassName} disabled={action.disabled} type={action.type ?? 'button'} onClick={action.onClick}>
      {action.text}
    </button>
  );
}

export function ResponsiveToolbar({ actions = [], title }: ResponsiveToolbarProps) {
  const { translate } = useI18n();
  const { breakpoint } = useBreakpoint();
  const { width } = useViewportSize();
  const orderedActions = [...actions].sort((left, right) => (right.priority ?? 0) - (left.priority ?? 0));
  const visibleCount = getResponsiveToolbarVisibleCount(width, orderedActions.length);
  const visibleActions = orderedActions.slice(0, visibleCount);
  const overflowActions = orderedActions.slice(visibleCount);

  return (
    <div className="responsive-toolbar">
      {title ? <div className="responsive-toolbar__title">{title}</div> : <span />}

      <div className="responsive-toolbar__actions">
        {visibleActions.map((action, index) => (
          <ActionButton key={`visible-${index}-${action.priority ?? 0}`} action={action} />
        ))}

        {overflowActions.length > 0 ? (
          <details className="responsive-toolbar__more">
            <summary className="ghost-button">{breakpoint === 'xs' ? translate('toolbar.moreMobile') : translate('toolbar.moreActions')}</summary>
            <div className="responsive-toolbar__more-panel">
              {overflowActions.map((action, index) => (
                <ActionButton key={`overflow-${index}-${action.priority ?? 0}`} action={action} />
              ))}
            </div>
          </details>
        ) : null}
      </div>
    </div>
  );
}
