import { create } from 'zustand';

interface ViewportState {
  deviceTier: 'B1' | 'B2' | 'B3';
  fps: number;
  orbitEnabled: boolean;
  setDeviceTier: (deviceTier: ViewportState['deviceTier']) => void;
  setFps: (fps: number) => void;
  setOrbitEnabled: (orbitEnabled: boolean) => void;
}

export const useAsterSceneViewportStore = create<ViewportState>((set) => ({
  deviceTier: 'B2',
  fps: 60,
  orbitEnabled: true,
  setDeviceTier: (deviceTier) => set({ deviceTier }),
  setFps: (fps) => set({ fps }),
  setOrbitEnabled: (orbitEnabled) => set({ orbitEnabled })
}));
