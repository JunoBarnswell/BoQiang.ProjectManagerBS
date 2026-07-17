import type { ReactNode } from 'react';

interface LoginLayoutProps {
  children: ReactNode;
}

export function LoginLayout({ children }: LoginLayoutProps) {
  return (
    <div className="login-layout">
      <div className="login-layout__viewport">{children}</div>
    </div>
  );
}
