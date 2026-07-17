import { create } from 'zustand';

import type {
  KnowledgeGraphExchangeDraft,
  KnowledgeGraphFilterState,
  KnowledgeGraphImpactDraft,
  KnowledgeGraphModalKey,
  KnowledgeGraphPanelKey,
  KnowledgeGraphPathDraft,
  KnowledgeGraphSelection
} from '../types';
import {
  defaultImpactDraft,
  defaultKnowledgeGraphFilters,
  defaultPathDraft
} from '../utils/knowledgeGraphFormatters';

export interface KnowledgeGraphUiState {
  activeModal: KnowledgeGraphModalKey;
  activePanel: KnowledgeGraphPanelKey;
  edgeFormId: string | null;
  exchangeDraft: KnowledgeGraphExchangeDraft;
  filters: KnowledgeGraphFilterState;
  impactDraft: KnowledgeGraphImpactDraft;
  layoutOverrides: Record<string, { x: number; y: number }>;
  nodeFormId: string | null;
  pathDraft: KnowledgeGraphPathDraft;
  selection: KnowledgeGraphSelection | null;
  setActiveModal: (modal: KnowledgeGraphModalKey) => void;
  setActivePanel: (panel: KnowledgeGraphPanelKey) => void;
  setEdgeFormId: (edgeId: string | null) => void;
  setExchangeDraft: (patch: Partial<KnowledgeGraphExchangeDraft>) => void;
  setFilters: (patch: Partial<KnowledgeGraphFilterState>) => void;
  setImpactDraft: (patch: Partial<KnowledgeGraphImpactDraft>) => void;
  setNodeFormId: (nodeId: string | null) => void;
  setPathDraft: (patch: Partial<KnowledgeGraphPathDraft>) => void;
  setSelection: (selection: KnowledgeGraphSelection | null) => void;
  updateLayoutOverride: (nodeId: string, position: { x: number; y: number }) => void;
  resetFilters: () => void;
  resetPathDraft: () => void;
  resetImpactDraft: () => void;
}

const defaultExchangeDraft: KnowledgeGraphExchangeDraft = {
  fileName: 'knowledge-graph.json',
  format: 'json',
  importContent: '',
  mode: 'export'
};

export const useKnowledgeGraphUiStore = create<KnowledgeGraphUiState>((set) => ({
  activeModal: null,
  activePanel: 'details',
  edgeFormId: null,
  exchangeDraft: defaultExchangeDraft,
  filters: defaultKnowledgeGraphFilters,
  impactDraft: defaultImpactDraft,
  layoutOverrides: {},
  nodeFormId: null,
  pathDraft: defaultPathDraft,
  selection: null,
  setActiveModal: (modal) => set({ activeModal: modal }),
  setActivePanel: (panel) => set({ activePanel: panel }),
  setEdgeFormId: (edgeId) => set({ edgeFormId: edgeId }),
  setExchangeDraft: (patch) => set((state) => ({ exchangeDraft: { ...state.exchangeDraft, ...patch } })),
  setFilters: (patch) => set((state) => ({ filters: { ...state.filters, ...patch } })),
  setImpactDraft: (patch) => set((state) => ({ impactDraft: { ...state.impactDraft, ...patch } })),
  setNodeFormId: (nodeId) => set({ nodeFormId: nodeId }),
  setPathDraft: (patch) => set((state) => ({ pathDraft: { ...state.pathDraft, ...patch } })),
  setSelection: (selection) => set({ selection }),
  updateLayoutOverride: (nodeId, position) =>
    set((state) => ({
      layoutOverrides: {
        ...state.layoutOverrides,
        [nodeId]: position
      }
    })),
  resetFilters: () => set({ filters: defaultKnowledgeGraphFilters }),
  resetImpactDraft: () => set({ impactDraft: defaultImpactDraft }),
  resetPathDraft: () => set({ pathDraft: defaultPathDraft })
}));
