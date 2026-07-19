// @vitest-environment jsdom

import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { ProjectManagementSyncPage } from "./ProjectManagementSyncPage";

const queryCalls = vi.hoisted(() => [] as Array<{ enabled?: boolean }>);
const permissionState = vi.hoisted(() => ({ canExport: false, canImport: false }));

vi.mock("@tanstack/react-query", () => ({
  useQuery: (options: { enabled?: boolean }) => {
    queryCalls.push(options);
    return { data: { data: [] }, error: null, isError: false, isLoading: false, refetch: vi.fn() };
  },
  useQueryClient: () => ({ invalidateQueries: vi.fn() }),
}));

vi.mock("react-router-dom", () => ({
  Link: ({ children }: { children: React.ReactNode }) => <a href="/project-data-space">{children}</a>,
}));

vi.mock("../../api/project-management/projectManagement.api", () => ({
  acknowledgeProjectManagementSync: vi.fn(),
  exportProjectManagementSync: vi.fn(),
  getProjectManagementSyncChanges: vi.fn(),
  getProjectManagementSyncWatermark: vi.fn(),
}));

vi.mock("../../core/auth/usePermission", () => ({
  usePermission: (code: string) => ({
    hasPermission: code === "project-management:sync:export" ? permissionState.canExport : permissionState.canImport,
  }),
}));

vi.mock("../../core/query/useApiMutation", () => ({
  useApiMutation: () => ({ isPending: false, mutate: vi.fn() }),
}));

vi.mock("../../features/project-management/state/projectManagementWorkspaceScope", () => ({
  useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: "tenant-a", appCode: "SYSTEM" }),
}));

vi.mock("../../shared/auth/PermissionButton", () => ({
  PermissionButton: ({ children }: { children: React.ReactNode }) => <button>{children}</button>,
}));

vi.mock("../../shared/feedback/useMessage", () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}));

vi.mock("../../shared/responsive/ResponsivePage", () => ({
  ResponsivePage: ({
    children,
    description,
    title,
  }: {
    children: React.ReactNode;
    description: string;
    title: string;
  }) => (
    <main>
      <h1>{title}</h1>
      <p>{description}</p>
      {children}
    </main>
  ),
}));

vi.mock("../../shared/status/Page403", () => ({ Page403: () => <p>403</p> }));
vi.mock("../../shared/status/PageError", () => ({
  PageError: ({ description }: { description?: React.ReactNode }) => <p>{description}</p>,
}));
vi.mock("../../shared/status/PageLoading", () => ({ PageLoading: () => <p>loading</p> }));

vi.mock("./components/ProjectManagementSyncPackageImportPanel", () => ({
  ProjectManagementSyncPackageImportPanel: () => <p>同步包导入控件</p>,
}));

vi.mock("./components/ProjectManagementSyncPackageExportPanel", () => ({
  ProjectManagementSyncPackageExportPanel: () => <p>同步包导出控件</p>,
}));

describe("ProjectManagementSyncPage", () => {
  beforeEach(() => {
    queryCalls.length = 0;
    permissionState.canExport = false;
    permissionState.canImport = false;
  });

  it("does not enable export-only queries for an import-only session", () => {
    permissionState.canImport = true;
    render(<ProjectManagementSyncPage />);

    expect(screen.getByText("当前账号可导入同步包，但没有查看同步水位和变更记录的权限。")).toBeTruthy();
    expect(screen.getByText("同步包导入控件")).toBeTruthy();
    expect(queryCalls).toHaveLength(4);
    expect(queryCalls.slice(0, 2).every((call) => call.enabled === false)).toBe(true);
    expect(queryCalls[2]?.enabled).toBe(true);
    expect(queryCalls[3]?.enabled).toBe(false);
  });

  it("enables export queries but hides import confirmations for export-only access", () => {
    permissionState.canExport = true;
    render(<ProjectManagementSyncPage />);

    expect(queryCalls).toHaveLength(4);
    expect(queryCalls.slice(0, 2).every((call) => call.enabled)).toBe(true);
    expect(queryCalls.slice(2).every((call) => call.enabled === false)).toBe(true);
    expect(screen.getByText("同步包导出控件")).toBeTruthy();
    expect(screen.queryByText("确认当前水位")).toBeNull();
  });

  it("shows confirmations when both sync permissions are granted", () => {
    permissionState.canExport = true;
    permissionState.canImport = true;
    render(<ProjectManagementSyncPage />);

    expect(queryCalls.slice(0, 3).every((call) => call.enabled)).toBe(true);
    expect(queryCalls[3]?.enabled).toBe(false);
    expect(screen.getByText("确认当前水位")).toBeTruthy();
  });

  it("defensively renders 403 without either sync permission", () => {
    render(<ProjectManagementSyncPage />);

    expect(queryCalls.every((call) => call.enabled === false)).toBe(true);
    expect(screen.getByText("403")).toBeTruthy();
  });
});
