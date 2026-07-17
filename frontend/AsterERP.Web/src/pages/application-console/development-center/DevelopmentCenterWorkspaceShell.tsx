interface DevelopmentCenterWorkspaceShellProps {
  children: React.ReactNode;
  context?: React.ReactNode;
}

export function DevelopmentCenterWorkspaceShell({
  children,
  context
}: DevelopmentCenterWorkspaceShellProps) {
  return (
    <div className={`grid min-h-0 gap-2 ${context ? 'xl:grid-cols-[minmax(0,1fr)_280px]' : ''}`}>
      <main className="min-w-0 space-y-2">
        {children}
      </main>
      {context ? <aside className="min-w-0">{context}</aside> : null}
    </div>
  );
}
