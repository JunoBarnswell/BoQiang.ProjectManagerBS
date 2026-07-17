// @vitest-environment jsdom

import { beforeEach, describe, expect, it, vi } from "vitest";

describe("versioned token storage", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.unstubAllEnvs();
  });

  it("blocks and removes the legacy key when no migration window is configured", async () => {
    const storage = await loadStorage();
    localStorage.setItem("astererp.access-token", "legacy-secret");

    expect(storage.getAccessToken()).toBe("");
    expect(localStorage.getItem("astererp.access-token")).toBeNull();
    expect(storage.getTokenStorageMigrationMetrics()).toEqual({ legacyReads: 0, migrated: 0, blockedAfterWindow: 1 });
    expect(localStorage.getItem("astererp.token-storage.schema-version")).toBe(
      String(storage.TOKEN_STORAGE_SCHEMA_VERSION),
    );
  });

  it("migrates the old key exactly once during the configured window", async () => {
    vi.stubEnv("VITE_APP_TOKEN_LEGACY_MIGRATION_UNTIL_UTC", "2099-01-01T00:00:00.000Z");
    const storage = await loadStorage();
    localStorage.setItem("astererp.access-token", "legacy-secret");

    expect(storage.getPlatformAccessToken()).toBe("legacy-secret");
    expect(localStorage.getItem("astererp.access-token")).toBeNull();
    expect(storage.getTokenStorageMigrationMetrics()).toEqual({ legacyReads: 1, migrated: 1, blockedAfterWindow: 0 });

    localStorage.removeItem("astererp.token-storage.schema-version");
    expect(storage.getPlatformAccessToken()).toBe("legacy-secret");
    expect(storage.getTokenStorageMigrationMetrics()).toEqual({ legacyReads: 1, migrated: 1, blockedAfterWindow: 0 });
  });

  it("keeps application/platform slots isolated and clears both on logout", async () => {
    const storage = await loadStorage();
    storage.setPlatformAccessToken("platform-secret");
    storage.setApplicationAccessToken("application-secret");
    expect(storage.getAccessToken()).toBe("application-secret");
    expect(storage.activatePlatformAccessToken()).toBe(true);
    expect(storage.getAccessToken()).toBe("platform-secret");

    storage.clearAccessToken();
    expect(storage.getAccessToken()).toBe("");
    expect(localStorage.getItem("astererp.platform-access-token")).toBeNull();
    expect(localStorage.getItem("astererp.application-access-token")).toBeNull();
  });
});

async function loadStorage() {
  vi.resetModules();
  return import("./tokenStorage");
}
