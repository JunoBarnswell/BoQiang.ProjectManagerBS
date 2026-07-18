import { create } from 'zustand';

export type ProjectManagementView = 'overview' | 'tasks' | 'board' | 'gantt' | 'calendar';

interface ProjectManagementWorkspaceState {
  filter: {
    assigneeId?: string;
    keyword: string;
    status?: string;
  };
  milestoneId: string | null;
  projectId: string | null;
  setFilter: (filter: Partial<ProjectManagementWorkspaceState['filter']>) => void;
  setMilestoneId: (milestoneId: string | null) => void;
  setProjectId: (projectId: string | null) => void;
  setView: (view: ProjectManagementView) => void;
  version: string;
  view: ProjectManagementView;
}

export const useProjectManagementWorkspaceStore = create<ProjectManagementWorkspaceState>((set) => ({
  filter: { keyword: '' },
  milestoneId: null,
  projectId: null,
  setFilter: (filter) => set((state) => ({ filter: { ...state.filter, ...filter } })),
  setMilestoneId: (milestoneId) => set({ milestoneId }),
  setProjectId: (projectId) => set({ projectId }),
  setView: (view) => set({ view }),
  version: 'draft',
  view: 'overview'
}));
