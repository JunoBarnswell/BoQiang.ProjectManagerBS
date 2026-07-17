import { create } from 'zustand';

interface FlowiseStudioCanvasState {
  chatPanelOpen: boolean;
  dirty: boolean;
  inspectorTab: 'details' | 'additional' | 'info' | 'run';
  paletteOpen: boolean;
  selectedEdgeId: string | null;
  selectedNodeId: string | null;
  validationOpen: boolean;
  setChatPanelOpen: (open: boolean) => void;
  setDirty: (dirty: boolean) => void;
  setInspectorTab: (tab: 'details' | 'additional' | 'info' | 'run') => void;
  setPaletteOpen: (open: boolean) => void;
  setSelectedEdgeId: (edgeId: string | null) => void;
  setSelectedNodeId: (nodeId: string | null) => void;
  setValidationOpen: (open: boolean) => void;
}

export const useFlowiseStudioStore = create<FlowiseStudioCanvasState>((set) => ({
  chatPanelOpen: false,
  dirty: false,
  inspectorTab: 'details',
  paletteOpen: true,
  selectedEdgeId: null,
  selectedNodeId: null,
  validationOpen: false,
  setChatPanelOpen: (chatPanelOpen) => set({ chatPanelOpen }),
  setDirty: (dirty) => set({ dirty }),
  setInspectorTab: (inspectorTab) => set({ inspectorTab }),
  setPaletteOpen: (paletteOpen) => set({ paletteOpen }),
  setSelectedEdgeId: (selectedEdgeId) => set(selectedEdgeId ? { selectedEdgeId, selectedNodeId: null } : { selectedEdgeId: null }),
  setSelectedNodeId: (selectedNodeId) => set({ selectedEdgeId: selectedNodeId ? null : null, selectedNodeId }),
  setValidationOpen: (validationOpen) => set({ validationOpen })
}));
