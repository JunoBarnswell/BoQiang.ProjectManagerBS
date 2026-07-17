/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_APP_API_BASE_URL?: string;
  readonly VITE_APP_DEFAULT_LOCALE?: string;
  readonly VITE_APP_DEFAULT_THEME?: string;
  readonly VITE_APP_REQUEST_TIMEOUT_MS?: string;
  readonly VITE_APP_TITLE?: string;
  readonly VITE_APP_TOKEN_LEGACY_MIGRATION_UNTIL_UTC?: string;
}
