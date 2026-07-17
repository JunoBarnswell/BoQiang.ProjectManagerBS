import type { ReactNode } from 'react';

interface AssistantSafeFloatingPanelProps {
  children: ReactNode;
}

export function AssistantSafeFloatingPanel({ children }: AssistantSafeFloatingPanelProps) {
  return (
    <div className="pointer-events-none fixed bottom-3 left-3 right-3 top-14 z-50 flex justify-end sm:bottom-5 sm:left-auto sm:right-4 sm:top-16 sm:w-[clamp(380px,34vw,500px)]">
      <aside className="pointer-events-auto h-full w-full min-w-0">{children}</aside>
    </div>
  );
}
