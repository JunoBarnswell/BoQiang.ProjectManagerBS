import { IconButton, type IconButtonProps } from '@mui/material';

import { usePermission } from '../../core/auth/usePermission';

export interface PermissionMuiIconButtonProps extends IconButtonProps {
  code: string | string[];
  fallback?: 'hide' | 'disable';
}

export function PermissionMuiIconButton({
  code,
  disabled,
  fallback = 'hide',
  ...buttonProps
}: PermissionMuiIconButtonProps) {
  const { hasPermission } = usePermission(code);

  if (!hasPermission && fallback === 'hide') {
    return null;
  }

  return <IconButton {...buttonProps} disabled={disabled || !hasPermission} />;
}
