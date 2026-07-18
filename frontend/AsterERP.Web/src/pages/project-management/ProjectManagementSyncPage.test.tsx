// @vitest-environment jsdom

import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { ProjectManagementSyncPage } from "./ProjectManagementSyncPage";

const queryCalls = vi.hoisted(() => [] as Array<{ enabled?: boolean }>);
const permissionState = vi.hoisted(() => ({ canExport: false }));

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
  getProjectManagementSyncChanges: vi.fn(),
  getProjectManagementSyncWatermark: vi.fn(),
}));

vi.mock("../../core/auth/usePermission", () => ({
  usePermission: () => ({ hasPermission: permissionState.canExport }),
}));

vi.mock("../../core/query/useApiMutation", () => ({
  useApiMutation: () => ({ isPending: false, mutate: vi.fn() }),
}));

vi.mock("../../features/project-management/state/projectManagementWorkspaceScope", () => ({
  useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: "tenant-a", appCode: "MES" }),
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

describe("ProjectManagementSyncPage", () => {
  beforeEach(() => {
    queryCalls.length = 0;
    permissionState.canExport = false;
  });

  it("does not enable export-only queries for an import-only session", () => {
    render(<ProjectManagementSyncPage />);

    expect(screen.getByText("当前账号可导入同步包，但没有查看同步水位和变更记录的权限。")).toBeTruthy();
    expect(screen.getByText("同步包导入控件")).toBeTruthy();
    expect(queryCalls).toHaveLength(2);
    expect(queryCalls.every((call) => call.enabled === false)).toBe(true);
  });
});
