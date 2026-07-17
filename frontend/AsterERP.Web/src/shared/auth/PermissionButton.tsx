import React, { type ButtonHTMLAttributes, type ReactNode } from 'react';

import { usePermission } from '../../core/auth/usePermission';
import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../icons/AppIcon';


interface PermissionButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode;
  code: string | string[];
  fallback?: 'hide' | 'disable';
  iconEnd?: string | null;
  iconStart?: false | string | null;
}

export function PermissionButton({
  children,
  code,
  disabled,
  iconEnd,
  iconStart,
  fallback = 'hide',
  ...buttonProps
}: PermissionButtonProps) {
  const { hasPermission } = usePermission(code);
  const className = ['app-button-base', buttonProps.className].filter(Boolean).join(' ');
  const hasOpClass = String(buttonProps.className ?? '').includes('op');
  
  // Some callers still provide their own inline icon element (like AppIcon, i, svg, or other components).
  const hasIconChild = React.Children.toArray(children).some(
    (child) =>
      React.isValidElement(child) &&
      (typeof child.type === 'string'
        ? child.type === 'i' || child.type === 'svg'
        : typeof child.type === 'function' || typeof child.type === 'object')
  );

  const resolvedStartIcon =
    iconStart === undefined
      ? (hasOpClass || hasIconChild ? null : resolvePermissionIcon(code))
      : iconStart || null;

  if (!hasPermission && fallback === 'hide') {
    return null;
  }

  const translatedTitle = typeof buttonProps.title === 'string' ? translateCurrentLiteral(buttonProps.title) : buttonProps.title;
  const resolvedChildren = React.Children.map(children, (child) => (typeof child === 'string' ? translateCurrentLiteral(child) : child));

  return (
    <button {...buttonProps} className={className} title={translatedTitle} disabled={disabled || !hasPermission}>
      {resolvedStartIcon ? <AppIcon aria-hidden="true" name={resolvedStartIcon} size={16} /> : null}
      {resolvedChildren}
      {iconEnd ? <AppIcon aria-hidden="true" name={iconEnd} size={16} /> : null}
    </button>
  );
}

function resolvePermissionIcon(code: string | string[]) {
  const target = Array.isArray(code) ? code[0] : code;
  if (!target) {
    return null;
  }

  const normalized = target.toLowerCase();
  if (normalized.includes(':add') || normalized.includes(':create')) {
    return 'plus';
  }

  if (normalized.includes(':edit') || normalized.includes(':update')) {
    return 'edit';
  }

  if (normalized.includes(':delete') || normalized.includes(':remove')) {
    return 'trash';
  }

  if (normalized.includes(':query') || normalized.includes(':view') || normalized.includes(':list')) {
    return 'search';
  }

  if (normalized.includes(':grant') || normalized.includes(':permission')) {
    return 'shield';
  }

  if (normalized.includes(':reset') || normalized.includes(':refresh') || normalized.includes(':sync')) {
    return 'refresh';
  }

  return null;
}
