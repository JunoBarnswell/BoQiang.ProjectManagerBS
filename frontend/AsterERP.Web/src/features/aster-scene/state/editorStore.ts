import { create } from 'zustand';

import type { SceneSubObjectMode, SceneTransformMode, SceneTransformSpace, SceneViewportLayout } from '../model/types';

interface EditorState {
  autoKey: boolean;
  currentFrame: number;
  selectedEdges: number[];
  selectedFaces: number[];
  selectedVertices: number[];
  snapEnabled: boolean;
  subObjectMode: SceneSubObjectMode;
  transformMode: SceneTransformMode;
  transformSpace: SceneTransformSpace;
  viewportLayout: SceneViewportLayout;
  clearSubObjectSelection: () => void;
  setAutoKey: (autoKey: boolean) => void;
  setCurrentFrame: (frame: number) => void;
  setSelectedEdges: (edges: number[]) => void;
  setSelectedFaces: (faces: number[]) => void;
  setSelectedVertices: (vertices: number[]) => void;
  setSnapEnabled: (enabled: boolean) => void;
  setSubObjectMode: (mode: SceneSubObjectMode) => void;
  setTransformMode: (mode: SceneTransformMode) => void;
  setTransformSpace: (space: SceneTransformSpace) => void;
  setViewportLayout: (layout: SceneViewportLayout) => void;
}

export const useAsterSceneEditorStore = create<EditorState>((set) => ({
  autoKey: false,
  currentFrame: 0,
  selectedEdges: [],
  selectedFaces: [],
  selectedVertices: [],
  snapEnabled: false,
  subObjectMode: 'object',
  transformMode: 'translate',
  transformSpace: 'world',
  viewportLayout: 'single',
  clearSubObjectSelection: () => set({ selectedEdges: [], selectedFaces: [], selectedVertices: [] }),
  setAutoKey: (autoKey) => set({ autoKey }),
  setCurrentFrame: (currentFrame) => set({ currentFrame: Math.max(0, Math.round(currentFrame)) }),
  setSelectedEdges: (selectedEdges) => set({ selectedEdges }),
  setSelectedFaces: (selectedFaces) => set({ selectedFaces }),
  setSelectedVertices: (selectedVertices) => set({ selectedVertices }),
  setSnapEnabled: (snapEnabled) => set({ snapEnabled }),
  setSubObjectMode: (subObjectMode) => set({ subObjectMode }),
  setTransformMode: (transformMode) => set({ transformMode }),
  setTransformSpace: (transformSpace) => set({ transformSpace }),
  setViewportLayout: (viewportLayout) => set({ viewportLayout })
}));
