import type { ReactNode } from 'react';

interface PageStateShellProps {
  action?: ReactNode;
  description?: ReactNode;
  title: ReactNode;
}

export function PageStateShell({ action, description, title }: PageStateShellProps) {
  return (
    <section className="flex flex-col items-center justify-center min-h-[200px] h-full p-8 text-center text-gray-500">
      <h2 className="text-lg font-medium text-gray-700 mb-2">{title}</h2>
      {description ? <p className="text-sm max-w-md mx-auto mb-6 leading-relaxed">{description}</p> : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </section>
  );
}
