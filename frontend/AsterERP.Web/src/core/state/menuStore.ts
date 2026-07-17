import { create } from 'zustand';

import type { MenuStoreState } from './types';

export const useMenuStore = create<MenuStoreState>((set) => ({
  menus: [],
  setMenus: (menus) => {
    set({
      menus: Array.isArray(menus) ? menus : []
    });
  }
}));

