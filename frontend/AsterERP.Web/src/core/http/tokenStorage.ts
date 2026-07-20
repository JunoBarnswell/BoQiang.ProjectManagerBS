const legacyAccessTokenKey = "astererp.access-token";
const activeTokenSlotKey = "astererp.active-token-slot";
const platformAccessTokenKey = "astererp.platform-access-token";
const applicationAccessTokenKey = "astererp.application-access-token";
const storageSchemaVersionKey = "astererp.token-storage.schema-version";
const migrationMetricsKey = "astererp.token-storage.migration-metrics";

export const TOKEN_STORAGE_SCHEMA_VERSION = 2;
export type TokenSlot = "application" | "platform";

export interface TokenStorageMigrationMetrics {
  legacyReads: number;
  migrated: number;
  blockedAfterWindow: number;
}
export function getAccessToken(): string {
  ensureStorageSchema();
  const activeSlot = getActiveTokenSlot();
  return activeSlot === "application" ? getApplicationAccessToken() : getPlatformAccessToken();
}

export function setAccessToken(token: string): void {
  setPlatformAccessToken(token);
}

export function clearAccessToken(): void {
  ensureStorageSchema();
  const storage = getTokenStorage();
  if (!storage) return;
  storage.removeItem(legacyAccessTokenKey);
  storage.removeItem(platformAccessTokenKey);
  storage.removeItem(applicationAccessTokenKey);
  storage.removeItem(activeTokenSlotKey);
  storage.setItem(storageSchemaVersionKey, String(TOKEN_STORAGE_SCHEMA_VERSION));
}

export function getPlatformAccessToken(): string {
  ensureStorageSchema();
  return getTokenStorage()?.getItem(platformAccessTokenKey) ?? "";
}

export function setPlatformAccessToken(token: string): void {
  ensureStorageSchema();
  getTokenStorage()?.setItem(platformAccessTokenKey, token.trim());
  setActiveTokenSlot("platform");
}

export function getApplicationAccessToken(): string {
  ensureStorageSchema();
  return getTokenStorage()?.getItem(applicationAccessTokenKey) ?? "";
}

export function setApplicationAccessToken(token: string): void {
  ensureStorageSchema();
  getTokenStorage()?.setItem(applicationAccessTokenKey, token.trim());
  setActiveTokenSlot("application");
}

export function clearApplicationAccessToken(): void {
  ensureStorageSchema();
  getTokenStorage()?.removeItem(applicationAccessTokenKey);
  if (getActiveTokenSlot() === "application") {
    setActiveTokenSlot(getPlatformAccessToken() ? "platform" : "application");
  }
}

export function activatePlatformAccessToken(): boolean {
  ensureStorageSchema();
  if (!getPlatformAccessToken()) return false;
  setActiveTokenSlot("platform");
  return true;
}

export function activateApplicationAccessToken(): boolean {
  ensureStorageSchema();
  if (!getApplicationAccessToken()) return false;
  setActiveTokenSlot("application");
  return true;
}

export function hasPlatformAccessToken(): boolean {
  return Boolean(getPlatformAccessToken());
}

export function getActiveTokenSlot(): TokenSlot {
  ensureStorageSchema();
  return getTokenStorage()?.getItem(activeTokenSlotKey) === "application" ? "application" : "platform";
}

export function getTokenStorageMigrationMetrics(): TokenStorageMigrationMetrics {
  const raw = getTokenStorage()?.getItem(migrationMetricsKey);
  if (!raw) return { legacyReads: 0, migrated: 0, blockedAfterWindow: 0 };
  try {
    const parsed = JSON.parse(raw) as Partial<TokenStorageMigrationMetrics>;
    return {
      legacyReads: Number.isFinite(parsed.legacyReads) ? Number(parsed.legacyReads) : 0,
      migrated: Number.isFinite(parsed.migrated) ? Number(parsed.migrated) : 0,
      blockedAfterWindow: Number.isFinite(parsed.blockedAfterWindow) ? Number(parsed.blockedAfterWindow) : 0,
    };
  } catch {
    return { legacyReads: 0, migrated: 0, blockedAfterWindow: 0 };
  }
}

function ensureStorageSchema(): void {
  const storage = getTokenStorage();
  if (!storage) return;

  const current = storage.getItem(storageSchemaVersionKey);
  if (current === String(TOKEN_STORAGE_SCHEMA_VERSION)) return;

  if (isLegacyMigrationWindowOpen()) {
    migrateLegacyAccessTokenOnce();
  } else if (hasStorageKey(legacyAccessTokenKey)) {
    incrementMetrics("blockedAfterWindow");
  }

  storage.removeItem(legacyAccessTokenKey);
  storage.setItem(storageSchemaVersionKey, String(TOKEN_STORAGE_SCHEMA_VERSION));
  const activeSlot = storage.getItem(activeTokenSlotKey);
  if (activeSlot !== "application" && activeSlot !== "platform") {
    storage.removeItem(activeTokenSlotKey);
  }
}

function migrateLegacyAccessTokenOnce(): void {
  const storage = getTokenStorage();
  if (!storage) return;

  const legacyToken = storage.getItem(legacyAccessTokenKey);
  if (!legacyToken) return;

  incrementMetrics("legacyReads");
  if (storage.getItem(platformAccessTokenKey)) return;

  storage.setItem(platformAccessTokenKey, legacyToken.trim());
  incrementMetrics("migrated");
}

function hasStorageKey(expectedKey: string): boolean {
  const storage = getTokenStorage();
  if (!storage) return false;

  for (let index = 0; index < storage.length; index += 1) {
    if (storage.key(index) === expectedKey) return true;
  }

  return false;
}

function isLegacyMigrationWindowOpen(): boolean {
  const cutoff = import.meta.env.VITE_APP_TOKEN_LEGACY_MIGRATION_UNTIL_UTC;
  if (!cutoff) return false;
  const cutoffTime = Date.parse(cutoff);
  return Number.isFinite(cutoffTime) && Date.now() < cutoffTime;
}

function incrementMetrics(key: keyof TokenStorageMigrationMetrics): void {
  const storage = getTokenStorage();
  if (!storage) return;

  const metrics = getTokenStorageMigrationMetrics();
  metrics[key] += 1;
  storage.setItem(migrationMetricsKey, JSON.stringify(metrics));
}

function setActiveTokenSlot(slot: TokenSlot): void {
  getTokenStorage()?.setItem(activeTokenSlotKey, slot);
}

function getTokenStorage(): Storage | null {
  if (typeof window === "undefined") return null;

  try {
    return window.localStorage;
  } catch {
    return null;
  }
}
