import { Eye, EyeOff, LockKeyhole, UserRound } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';


import {
  saveInitialApplicationDatabaseBinding,
  testInitialApplicationDatabaseBinding
} from '../../api/application-console/applicationConsole.api';
import type { ApplicationConsoleSummaryDto } from '../../api/application-console/applicationConsole.types';
import { getApplicationLoginBootstrap } from '../../api/platform/auth.api';
import type { ApplicationLoginBootstrapDto, ApplicationLoginRequest } from '../../api/platform/auth.types';
import { LoginLayout } from '../../app/layout/LoginLayout';
import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useAuthStore } from '../../core/state';
import { Page404 } from '../../shared/status/Page404';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';
import { ApplicationDatabaseBindingPanel } from '../application-console/ApplicationDatabaseBindingPanel';
import './LoginPage.css';

const defaultFormState: ApplicationLoginRequest = {
  password: '',
  userName: ''
};

function buildInitialBindingSummary(bootstrap: ApplicationLoginBootstrapDto): ApplicationConsoleSummaryDto {
  const now = new Date().toISOString();
  return {
    application: {
      tenantId: bootstrap.tenantId,
      tenantName: bootstrap.tenantName,
      appCode: bootstrap.appCode,
      appName: bootstrap.appName,
      systemName: bootstrap.systemName,
      defaultRoutePath: `/tenants/${bootstrap.tenantId}/apps/${bootstrap.appCode}/admin/home`,
      status: bootstrap.status,
      appType: 'Business',
      createdTime: now,
      updatedTime: null,
      workspaceLevel: 'application'
    },
    databaseBinding: bootstrap.databaseBinding,
    metrics: [],
    capabilityCounts: {
      dataModelCount: 0,
      menuCount: 0,
      pageCount: 0,
      permissionCount: 0,
      publishedPageCount: 0,
      publishTaskCount: 0,
      rootMenuCount: 0,
      workflowModelCount: 0
    },
    recentPublishes: [],
    recentAudits: [],
    entryTree: [],
    developmentShortcuts: [],
    recentDevelopmentItems: [],
    versionContext: {
      draftVersionCount: 0,
      publishedVersionCount: 0,
      latestDraftVersion: null,
      latestPublishedVersion: null,
      latestPublishTime: null,
      summary: ''
    },
    draftSignals: {
      totalRiskCount: 0,
      hasPendingPublishRisk: false,
      items: []
    }
  };
}

export function ApplicationLoginPage() {
  const navigate = useNavigate();
  const params = useParams();
  const tenantId = params.tenantId?.trim() ?? '';
  const appCode = params.appCode?.trim().toUpperCase() ?? '';
  const applicationLogin = useAuthStore((state) => state.applicationLogin);
  const enterApplicationBackend = useAuthStore((state) => state.enterApplicationBackend);
  const isLoading = useAuthStore((state) => state.isLoading);
  const [formState, setFormState] = useState<ApplicationLoginRequest>(defaultFormState);
  const [errorMessage, setErrorMessage] = useState('');
  const [isPasswordVisible, setIsPasswordVisible] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const bootstrapQuery = useApiQuery({
    enabled: Boolean(tenantId && appCode),
    queryFn: () => getApplicationLoginBootstrap(tenantId, appCode).then((response) => response.data),
    queryKey: [...queryKeys.all, 'application-login', tenantId, appCode] as const,
    staleTimeMs: 15_000
  });

  if (!tenantId || !appCode) {
    return <Page404 />;
  }

  if (bootstrapQuery.isLoading) {
    return <PageLoading />;
  }

  if (bootstrapQuery.isError || !bootstrapQuery.data) {
    return (
      <LoginLayout>
        <div className="login-page">
          <main className="login-main">
            <div className="login-form login-form--product">
              <div className="login-form__heading">
                <h1>{translateCurrentLiteral("应用登录不可用")}</h1>
                <p>{translateCurrentLiteral("无法读取应用登录配置，请稍后重试。")}</p>
              </div>
              <button className="primary-button login-form__submit" type="button" onClick={() => void bootstrapQuery.refetch()}>{translateCurrentLiteral("重试")}</button>
            </div>
          </main>
        </div>
      </LoginLayout>
    );
  }

  const bootstrap = bootstrapQuery.data;
  const binding = bootstrap.databaseBinding;
  const shellClassName = binding.isBound ? 'login-shell' : 'login-shell login-shell--binding';
  const initialBindingSummary = buildInitialBindingSummary(bootstrap);

  const enterBackendAfterBinding = async () => {
    await bootstrapQuery.refetch();
    const response = await enterApplicationBackend(appCode, {
      source: 'application-login-initial-binding',
      tenantId
    });
    navigate(response.defaultRoutePath || response.currentWorkspace.defaultRoutePath || `/tenants/${tenantId}/apps/${appCode}/admin/home`, { replace: true });
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage('');
    if (isSubmitting) {
      return;
    }

    const userName = formState.userName.trim();
    const password = formState.password.trim();
    if (!userName || !password) {
      setErrorMessage('请输入用户名和密码');
      return;
    }

    try {
      setIsSubmitting(true);
      const response = await applicationLogin(tenantId, appCode, { password, userName });
      navigate(response.defaultRoutePath || response.currentWorkspace.defaultRoutePath || `/tenants/${tenantId}/apps/${appCode}/admin/home`, { replace: true });
    } catch (error) {
      setErrorMessage(getErrorMessage(error, '应用登录失败'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <LoginLayout>
      <div className="login-page">
        <div className={shellClassName}>
          <main className="login-main">
            <div className="login-main__brand">
              <span className="login-brand-mark">{appCode.slice(0, 1)}</span>
              <strong>{bootstrap.systemName}</strong>
            </div>

            {!binding.isBound ? (
              binding.canManage ? (
                <div className="w-full max-w-5xl px-4">
                  <ApplicationDatabaseBindingPanel
                    summary={initialBindingSummary}
                    onReload={enterBackendAfterBinding}
                    onSaveBinding={(request) => saveInitialApplicationDatabaseBinding(tenantId, appCode, request)}
                    onTestBinding={(request) => testInitialApplicationDatabaseBinding(tenantId, appCode, request)}
                  />
                </div>
              ) : (
                <div className="login-form login-form--product">
                  <div className="login-form__heading">
                    <h1>{translateCurrentLiteral("应用登录暂不可用")}</h1>
                    <p>{translateCurrentLiteral("应用数据库尚未完成绑定，请联系平台管理员完成应用初始化。")}</p>
                  </div>
                </div>
              )
            ) : null}

            {binding.isBound && !binding.isReachable ? (
              binding.canManage ? (
                <div className="w-full max-w-5xl px-4">
                  <ApplicationDatabaseBindingPanel
                    summary={initialBindingSummary}
                    onReload={enterBackendAfterBinding}
                    onSaveBinding={(request) => saveInitialApplicationDatabaseBinding(tenantId, appCode, request)}
                    onTestBinding={(request) => testInitialApplicationDatabaseBinding(tenantId, appCode, request)}
                  />
                </div>
              ) : (
                <div className="login-form login-form--product">
                  <div className="login-form__heading">
                    <h1>{translateCurrentLiteral("应用数据库连接失败")}</h1>
                    <p>{translateCurrentLiteral("应用当前不可登录，请联系应用管理员检查数据库绑定状态。")}</p>
                  </div>
                </div>
              )
            ) : null}

            {binding.isBound && binding.isReachable ? (
              <form className="login-form login-form--product" onSubmit={handleSubmit}>
                <div className="login-form__heading">
                  <h1>{bootstrap.appName} 登录</h1>
                  <p>{bootstrap.tenantName} / {bootstrap.appCode}</p>
                </div>

                <label className="login-field">
                  <span>{translateCurrentLiteral("用户名")}</span>
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
                  <span>{translateCurrentLiteral("密码")}</span>
                  <div className="login-input-wrap">
                    <LockKeyhole size={18} />
                    <input
                      autoComplete="current-password"
                      type={isPasswordVisible ? 'text' : 'password'}
                      value={formState.password}
                      onChange={(event) => setFormState((current) => ({ ...current, password: event.target.value }))}
                    />
                    <button
                      aria-label={isPasswordVisible ? '隐藏密码' : '显示密码'}
                      className="login-icon-button"
                      type="button"
                      onClick={() => setIsPasswordVisible((current) => !current)}
                    >
                      {isPasswordVisible ? <EyeOff size={18} /> : <Eye size={18} />}
                    </button>
                  </div>
                </label>

                {errorMessage ? <div className="login-form__error">{errorMessage}</div> : null}

                <button className="primary-button login-form__submit" disabled={isLoading || isSubmitting} type="submit">
                  {isLoading || isSubmitting ? '登录中' : '进入应用'}
                </button>
              </form>
            ) : null}
          </main>
        </div>
      </div>
    </LoginLayout>
  );
}

export default ApplicationLoginPage;
