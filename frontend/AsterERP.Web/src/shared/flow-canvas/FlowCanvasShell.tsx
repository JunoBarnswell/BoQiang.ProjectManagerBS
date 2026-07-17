import type { ReactNode } from 'react';

interface FlowCanvasShellProps {
  bodyClassName?: string;
  chatPanel?: ReactNode;
  className?: string;
  header?: ReactNode;
  inspector?: ReactNode;
  palette?: ReactNode;
  stage: ReactNode;
  stageClassName?: string;
}

export function FlowCanvasShell({
  bodyClassName = 'flow-canvas-shell__body',
  chatPanel,
  className = 'flow-canvas-shell',
  header,
  inspector,
  palette,
  stage,
  stageClassName = 'flow-canvas-stage'
}: FlowCanvasShellProps) {
  return (
    <div className={className}>
      {header}
      <div className={bodyClassName}>
        {palette}
        <section className={stageClassName}>{stage}</section>
        {inspector}
        {chatPanel}
      </div>
    </div>
  );
}
