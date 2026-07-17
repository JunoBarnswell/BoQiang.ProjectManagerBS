import { create } from 'zustand';

import type { PermissionStoreState } from './types';

function normalizePermissionCodes(permissionCodes: string[]): string[] {
  return Array.from(
    new Set(
      permissionCodes
        .filter((code) => typeof code === 'string')
        .map((code) => code.trim())
        .filter((code) => code.length > 0)
    )
  );
}

export const usePermissionStore = create<PermissionStoreState>((set, get) => ({
  hasPermission: (permissionCode) => {
    const normalized = normalizePermissionCodes(get().permissionCodes);
    const requestCodes = Array.isArray(permissionCode) ? permissionCode : permissionCode ? [permissionCode] : [];

    return normalized.includes('*') || requestCodes.some((code) => code.length > 0 && normalized.includes(code));
  },
  permissionCodes: [],
  setPermissionCodes: (permissionCodes) => {
    set({
      permissionCodes: normalizePermissionCodes(permissionCodes)
    });
  }
}));

