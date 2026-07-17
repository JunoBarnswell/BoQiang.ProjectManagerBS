import type { ReactNode } from 'react';

interface ResponsivePanelProps {
  children: ReactNode;
  className?: string;
  compact?: boolean;
}

export function ResponsivePanel({ children, className, compact = false }: ResponsivePanelProps) {
  const baseClasses = 'flex min-w-0 min-h-0 flex-col border border-[var(--app-border)] bg-[color-mix(in_srgb,var(--app-card)_92%,transparent)] shadow-[var(--app-shadow)] rounded-[clamp(16px,2vw,24px)]';
  const sizeClasses = compact ? 'gap-[10px] p-[12px]' : 'gap-[12px] p-[clamp(14px,1.4vw,20px)]';
  
  return (
    <section className={[baseClasses, sizeClasses, className ?? ''].filter(Boolean).join(' ')}>
      {children}
    </section>
  );
}

