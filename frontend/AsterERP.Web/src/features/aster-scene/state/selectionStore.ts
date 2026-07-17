import { create } from 'zustand';

interface SelectionState {
  actorId: string | null;
  mode: 'asset' | 'inspect' | 'model' | 'publish';
  selectActor: (actorId: string | null) => void;
  setMode: (mode: SelectionState['mode']) => void;
}

export const useAsterSceneSelectionStore = create<SelectionState>((set) => ({
  actorId: null,
  mode: 'inspect',
  selectActor: (actorId) => set({ actorId }),
  setMode: (mode) => set({ mode })
}));
