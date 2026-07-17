import { Button, type ButtonProps } from '@mui/material';

import { usePermission } from '../../core/auth/usePermission';

export interface PermissionMuiButtonProps extends ButtonProps {
  code: string | string[];
  fallback?: 'hide' | 'disable';
}

export function PermissionMuiButton({
  code,
  disabled,
  fallback = 'hide',
  ...buttonProps
}: PermissionMuiButtonProps) {
  const { hasPermission } = usePermission(code);

  if (!hasPermission && fallback === 'hide') {
    return null;
  }

  return <Button {...buttonProps} disabled={disabled || !hasPermission} />;
}
