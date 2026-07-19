import { createContext, useCallback, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react';

interface EscapeLayer {
  close: () => void;
  id: symbol;
}

interface ProjectManagementEscapeStackContextValue {
  register: (close: () => void) => () => void;
}

const ProjectManagementEscapeStackContext = createContext<ProjectManagementEscapeStackContextValue | null>(null);

export function ProjectManagementEscapeStack({ children }: { children: ReactNode }) {
  const layersRef = useRef<EscapeLayer[]>([]);

  const register = useCallback((close: () => void) => {
    const id = Symbol('project-management-escape-layer');
    layersRef.current.push({ close, id });
    return () => {
      layersRef.current = layersRef.current.filter((layer) => layer.id !== id);
    };
  }, []);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || event.defaultPrevented) return;
      const topLayer = layersRef.current.at(-1);
      if (!topLayer) return;
      event.preventDefault();
      topLayer.close();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);

  const value = useMemo(() => ({ register }), [register]);
  return <ProjectManagementEscapeStackContext.Provider value={value}>{children}</ProjectManagementEscapeStackContext.Provider>;
}

export function useProjectManagementEscapeLayer(isOpen: boolean, onEscape: () => void): void {
  const context = useContext(ProjectManagementEscapeStackContext);
  const onEscapeRef = useRef(onEscape);
  onEscapeRef.current = onEscape;

  useEffect(() => {
    if (!isOpen) return;
    if (!context) throw new Error('useProjectManagementEscapeLayer must be used within ProjectManagementEscapeStack');
    return context.register(() => onEscapeRef.current());
  }, [context, isOpen]);
}
