import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';

export interface ProjectManagementWorkbenchContextValue {
  activePanelKeys: ReadonlySet<string>;
  markPanelVisited: (panelKey: string) => void;
}

const ProjectManagementWorkbenchContext = createContext<ProjectManagementWorkbenchContextValue | undefined>(undefined);

export function ProjectManagementWorkbenchProvider({ children }: { children: ReactNode }) {
  const [activePanelKeys, setActivePanelKeys] = useState<ReadonlySet<string>>(() => new Set());
  const markPanelVisited = useCallback((panelKey: string) => {
    setActivePanelKeys((current) => current.has(panelKey) ? current : new Set([...current, panelKey]));
  }, []);
  const value = useMemo(() => ({ activePanelKeys, markPanelVisited }), [activePanelKeys, markPanelVisited]);

  return <ProjectManagementWorkbenchContext.Provider value={value}>{children}</ProjectManagementWorkbenchContext.Provider>;
}

export function useProjectManagementWorkbenchContext(): ProjectManagementWorkbenchContextValue {
  const context = useContext(ProjectManagementWorkbenchContext);
  if (!context) throw new Error('ProjectManagementWorkbenchContext must be used inside ProjectManagementWorkbenchProvider.');
  return context;
}
