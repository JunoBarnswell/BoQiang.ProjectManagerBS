import { Eye, EyeOff, LockKeyhole, UserRound } from 'lucide-react';
import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import { recoverInitialAdminPassword } from '../../api/platform/auth.api';
import type { LoginRequest, WorkspaceDto } from '../../api/platform/auth.types';
import { LoginLayout } from '../../app/layout/LoginLayout';
import { isHttpError } from '../../core/http/httpError';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useAuthStore, useWorkspaceStore } from '../../core/state';
import { getErrorMessage } from '../../shared/utils/errorMessage';
import './LoginPage.css';

type LoginFormState = LoginRequest;

interface InitialAdminPasswordRecoveryFormState {
  confirmPassword: string;
  password: string;
  recoveryCode: string;
  userName: string;
}

const defaultFormState: LoginFormState = {
  password: '',
  userName: ''
};

const defaultRecoveryFormState: InitialAdminPasswordRecoveryFormState = {
  confirmPassword: '',
  password: '',
  recoveryCode: '',
  userName: ''
};

export function LoginPage() {
  const { translate } = useI18n();
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const isLoading = useAuthStore((state) => state.isLoading);
  const login = useAuthStore((state) => state.login);
  const switchWorkspace = useAuthStore((state) => state.switchWorkspace);
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const location = useLocation();
  const navigate = useNavigate();

  const [formState, setFormState] = useState<LoginFormState>(defaultFormState);
  const [errorMessage, setErrorMessage] = useState('');
  const [isPasswordVisible, setIsPasswordVisible] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isRecoveryOpen, setIsRecoveryOpen] = useState(false);
  const [isRecoverySubmitting, setIsRecoverySubmitting] = useState(false);
  const [recoveryErrorMessage, setRecoveryErrorMessage] = useState('');
  const [recoveryFormState, setRecoveryFormState] = useState<InitialAdminPasswordRecoveryFormState>(defaultRecoveryFormState);
  const [recoverySuccessMessage, setRecoverySuccessMessage] = useState('');
  const fromPath = useMemo(() => (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/home', [location.state]);
  const targetPath = useMemo(() => (fromPath === '/login' || fromPath === '/workspace' ? '/home' : fromPath), [fromPath]);

  useEffect(() => {
    if (isAuthenticated && !isLoading && currentWorkspace) {
      navigate(targetPath, { replace: true });
      return;
    }

    if (isAuthenticated && !isLoading && !currentWorkspace) {
      navigate('/workspace', { replace: true });
    }
  }, [currentWorkspace, isAuthenticated, isLoading, navigate, targetPath]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage('');
    if (isSubmitting) {
      return;
    }

    const userName = formState.userName.trim();
    const password = formState.password.trim();
    if (!userName || !password) {
      setErrorMessage(translate('page.login.required'));
      return;
    }

    try {
      setIsSubmitting(true);
      const response = await login({
        password,
        userName
      });
      await enterAfterLogin(response.availableWorkspaces);
    } catch (error) {
      if (isHttpError(error) && error.code === 41002) {
        setRecoveryFormState((current) => ({ ...current, userName }));
        setRecoveryErrorMessage('');
        setRecoverySuccessMessage('');
        setIsRecoveryOpen(true);
        setErrorMessage(translate('page.login.recovery.passwordResetRequired'));
        return;
      }
      setErrorMessage(getErrorMessage(error, translate('page.login.failed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  const openRecovery = () => {
    setRecoveryFormState((current) => ({ ...current, userName: current.userName || formState.userName.trim() }));
    setRecoveryErrorMessage('');
    setRecoverySuccessMessage('');
    setIsRecoveryOpen(true);
  };

  const closeRecovery = () => {
    setIsRecoveryOpen(false);
    setRecoveryErrorMessage('');
    setRecoveryFormState(defaultRecoveryFormState);
  };

  const handleRecoverySubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (isRecoverySubmitting) {
      return;
    }

    const userName = recoveryFormState.userName.trim();
    const password = recoveryFormState.password.trim();
    const confirmPassword = recoveryFormState.confirmPassword.trim();
    if (!userName || !recoveryFormState.recoveryCode || !password || !confirmPassword) {
      setRecoveryErrorMessage(translate('page.login.recovery.required'));
      return;
    }
    if (password.length < 6) {
      setRecoveryErrorMessage(translate('page.login.recovery.passwordTooShort'));
      return;
    }
    if (password !== confirmPassword) {
      setRecoveryErrorMessage(translate('page.login.recovery.passwordMismatch'));
      return;
    }

    try {
      setIsRecoverySubmitting(true);
      await recoverInitialAdminPassword({
        password,
        recoveryCode: recoveryFormState.recoveryCode,
        userName
      });
      setFormState({ password: '', userName });
      setRecoveryFormState(defaultRecoveryFormState);
      setRecoveryErrorMessage('');
      setRecoverySuccessMessage(translate('page.login.recovery.success'));
      setIsRecoveryOpen(false);
    } catch (error) {
      setRecoveryErrorMessage(getErrorMessage(error, translate('page.login.recovery.failed')));
    } finally {
      setIsRecoverySubmitting(false);
    }
  };

  const enterAfterLogin = async (workspaces: WorkspaceDto[]) => {
    const availableSystems = workspaces.filter((workspace) => workspace.isAvailable !== false);
    if (availableSystems.length === 1) {
      const [system] = availableSystems;
      await switchWorkspace({ appCode: system.appCode, tenantId: system.tenantId });
      navigate(targetPath, { replace: true });
      return;
    }

    navigate('/workspace', { replace: true });
  };

  return (
    <LoginLayout>
      <div className="login-page">
        <div className="login-shell">
        <main className="login-main">
          <div className="login-main__brand">
            <span className="login-brand-mark">A</span>
            <strong>{translate('app.title')}</strong>
          </div>

          {isRecoveryOpen ? (
          <form className="login-form login-form--product" onSubmit={handleRecoverySubmit}>
            <div className="login-form__heading">
              <h1>{translate('page.login.recovery.title')}</h1>
              <p>{translate('page.login.recovery.description')}</p>
            </div>

            <label className="login-field">
              <span>{translate('page.login.userName')}</span>
              <div className="login-input-wrap">
                <UserRound size={18} />
                <input
                  autoComplete="username"
                  value={recoveryFormState.userName}
                  onChange={(event) => setRecoveryFormState((current) => ({ ...current, userName: event.target.value }))}
                />
              </div>
            </label>

            <label className="login-field">
              <span>{translate('page.login.recovery.code')}</span>
              <div className="login-input-wrap">
                <LockKeyhole size={18} />
                <input
                  autoComplete="one-time-code"
                  type="password"
                  value={recoveryFormState.recoveryCode}
                  onChange={(event) => setRecoveryFormState((current) => ({ ...current, recoveryCode: event.target.value }))}
                />
              </div>
            </label>

            <label className="login-field">
              <span>{translate('page.login.recovery.newPassword')}</span>
              <div className="login-input-wrap">
                <LockKeyhole size={18} />
                <input
                  autoComplete="new-password"
                  type="password"
                  value={recoveryFormState.password}
                  onChange={(event) => setRecoveryFormState((current) => ({ ...current, password: event.target.value }))}
                />
              </div>
            </label>

            <label className="login-field">
              <span>{translate('page.login.recovery.confirmPassword')}</span>
              <div className="login-input-wrap">
                <LockKeyhole size={18} />
                <input
                  autoComplete="new-password"
                  type="password"
                  value={recoveryFormState.confirmPassword}
                  onChange={(event) => setRecoveryFormState((current) => ({ ...current, confirmPassword: event.target.value }))}
                />
              </div>
            </label>

            {recoveryErrorMessage ? <div className="login-form__error">{recoveryErrorMessage}</div> : null}

            <button className="primary-button login-form__submit" disabled={isRecoverySubmitting} type="submit">
              {isRecoverySubmitting ? translate('page.login.recovery.submitting') : translate('page.login.recovery.submit')}
            </button>
            <button className="login-form__secondary-action" type="button" onClick={closeRecovery}>
              {translate('page.login.recovery.backToLogin')}
            </button>
          </form>
          ) : (
          <form className="login-form login-form--product" onSubmit={handleSubmit}>
            <div className="login-form__heading">
              <h1>{translate('page.login.title')}</h1>
              <p>{translate('page.login.description')}</p>
            </div>

            <label className="login-field">
              <span>{translate('page.login.userName')}</span>
              <div className="login-input-wrap">
                <UserRound size={18} />
                <input
                  autoComplete="username"
                  value={formState.userName}
                  onChange={(event) => setFormState((current) => ({ ...current, userName: event.target.value }))}
                />
              </div>
            </label>

            <label className="login-field">
              <span>{translate('page.login.password')}</span>
              <div className="login-input-wrap">
                <LockKeyhole size={18} />
                <input
                  autoComplete="current-password"
                  type={isPasswordVisible ? 'text' : 'password'}
                  value={formState.password}
                  onChange={(event) => setFormState((current) => ({ ...current, password: event.target.value }))}
                />
                <button
                  aria-label={isPasswordVisible ? translate('page.login.hidePassword') : translate('page.login.showPassword')}
                  className="login-icon-button"
                  type="button"
                  onClick={() => setIsPasswordVisible((current) => !current)}
                >
                  {isPasswordVisible ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </label>

            {errorMessage ? <div className="login-form__error">{errorMessage}</div> : null}
            {recoverySuccessMessage ? <div className="login-form__success">{recoverySuccessMessage}</div> : null}

            <button className="primary-button login-form__submit" disabled={isLoading || isSubmitting} type="submit">
              {isLoading || isSubmitting ? translate('page.login.submitting') : translate('page.login.submit')}
            </button>
            <button className="login-form__secondary-action" type="button" onClick={openRecovery}>
              {translate('page.login.recovery.open')}
            </button>
          </form>
          )}

          <footer className="login-main__footer">{formatMessage(translate('page.login.footer'), { year: 2026 })}</footer>
        </main>
        </div>
      </div>
    </LoginLayout>
  );
}

export default LoginPage;
