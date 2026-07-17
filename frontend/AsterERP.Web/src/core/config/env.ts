export type AppLocale = 'zh-CN' | 'en-US';

export type ThemeMode = 'light' | 'dark' | 'brand' | 'kingdee' | 'yonyou';

interface AppEnvironment {
  appName: string;
  apiBaseUrl: string;
  basePath: string;
  defaultLocale: AppLocale;
  defaultTheme: ThemeMode;
  mode: string;
  requestTimeoutMs: number;
  targetAppCode: string;
}

function readString(name: string, fallback: string): string {
  const value = import.meta.env[name];
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : fallback;
}

function readNumber(name: string, fallback: number): number {
  const value = Number(import.meta.env[name]);
  return Number.isFinite(value) && value > 0 ? value : fallback;
}

function readLocale(name: string, fallback: AppLocale): AppLocale {
  const value = readString(name, fallback);
  return value === 'en-US' ? 'en-US' : 'zh-CN';
}

function readTheme(name: string, fallback: ThemeMode): ThemeMode {
  const value = readString(name, fallback);
  return value === 'dark' || value === 'brand' || value === 'kingdee' || value === 'yonyou' ? value : 'light';
}

const modeDefaults: Record<string, string> = {
  development: '/api',
  test: 'http://127.0.0.1:5100/api',
  production: 'https://api.astererp.example/api'
};

export const appEnv: AppEnvironment = {
  appName: readString('VITE_APP_TITLE', 'AsterERP'),
  apiBaseUrl: readString('VITE_APP_API_BASE_URL', modeDefaults[import.meta.env.MODE] ?? '/api'),
  basePath: readString('VITE_APP_BASE_PATH', '/'),
  defaultLocale: readLocale('VITE_APP_DEFAULT_LOCALE', 'zh-CN'),
  defaultTheme: readTheme('VITE_APP_DEFAULT_THEME', 'brand'),
  mode: import.meta.env.MODE,
  requestTimeoutMs: readNumber('VITE_APP_REQUEST_TIMEOUT_MS', 10_000),
  targetAppCode: readString('VITE_APP_TARGET_APP_CODE', '')
};
