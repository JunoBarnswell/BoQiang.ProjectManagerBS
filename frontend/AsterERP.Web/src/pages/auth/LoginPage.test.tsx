// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { HttpError } from '../../core/http/httpError';

import { LoginPage } from './LoginPage';

const loginMock = vi.fn();
const recoverInitialAdminPasswordMock = vi.fn();
const switchWorkspaceMock = vi.fn();

vi.mock('../../api/platform/auth.api', () => ({
  recoverInitialAdminPassword: (...args: unknown[]) => recoverInitialAdminPasswordMock(...args)
}));

vi.mock('../../core/i18n/I18nProvider', () => ({
  useI18n: () => ({ translate: (key: string) => key })
}));

vi.mock('../../core/state', () => ({
  useAuthStore: (selector: (state: unknown) => unknown) => selector({
    isAuthenticated: false,
    isLoading: false,
    login: loginMock,
    switchWorkspace: switchWorkspaceMock
  }),
  useWorkspaceStore: (selector: (state: unknown) => unknown) => selector({ currentWorkspace: null })
}));

describe('LoginPage initial administrator password recovery', () => {
  beforeEach(() => {
    loginMock.mockReset();
    recoverInitialAdminPasswordMock.mockReset();
    switchWorkspaceMock.mockReset();
  });

  afterEach(() => cleanup());

  it('opens the recovery form and retains the account after PasswordResetRequired', async () => {
    loginMock.mockRejectedValue(new HttpError({
      code: 41002,
      message: 'Password reset required.',
      status: 428
    }));
    renderPage();

    fireEvent.change(screen.getByLabelText('page.login.userName'), { target: { value: 'admin' } });
    fireEvent.change(screen.getByLabelText('page.login.password'), { target: { value: 'old-password' } });
    fireEvent.click(screen.getByRole('button', { name: 'page.login.submit' }));

    expect(await screen.findByRole('heading', { name: 'page.login.recovery.title' })).not.toBeNull();
    expect((screen.getByLabelText('page.login.userName') as HTMLInputElement).value).toBe('admin');
  });

  it('blocks mismatched passwords before calling the recovery API', () => {
    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'page.login.recovery.open' }));
    fillRecoveryForm({ confirmPassword: 'different-password', password: 'new-password' });
    fireEvent.click(screen.getByRole('button', { name: 'page.login.recovery.submit' }));

    expect(screen.getByText('page.login.recovery.passwordMismatch')).not.toBeNull();
    expect(recoverInitialAdminPasswordMock).not.toHaveBeenCalled();
  });

  it('returns to sign-in with a prefilled account after successful recovery', async () => {
    recoverInitialAdminPasswordMock.mockResolvedValue({ data: true });
    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'page.login.recovery.open' }));
    fillRecoveryForm({ confirmPassword: 'new-password', password: 'new-password' });
    fireEvent.click(screen.getByRole('button', { name: 'page.login.recovery.submit' }));

    await waitFor(() => expect(recoverInitialAdminPasswordMock).toHaveBeenCalledWith({
      password: 'new-password',
      recoveryCode: 'deployment-code',
      userName: 'admin'
    }));
    expect(await screen.findByText('page.login.recovery.success')).not.toBeNull();
    expect((screen.getByLabelText('page.login.userName') as HTMLInputElement).value).toBe('admin');
  });
});

function renderPage() {
  return render(<MemoryRouter><LoginPage /></MemoryRouter>);
}

function fillRecoveryForm({ confirmPassword, password }: { confirmPassword: string; password: string }) {
  fireEvent.change(screen.getByLabelText('page.login.userName'), { target: { value: 'admin' } });
  fireEvent.change(screen.getByLabelText('page.login.recovery.code'), { target: { value: 'deployment-code' } });
  fireEvent.change(screen.getByLabelText('page.login.recovery.newPassword'), { target: { value: password } });
  fireEvent.change(screen.getByLabelText('page.login.recovery.confirmPassword'), { target: { value: confirmPassword } });
}
