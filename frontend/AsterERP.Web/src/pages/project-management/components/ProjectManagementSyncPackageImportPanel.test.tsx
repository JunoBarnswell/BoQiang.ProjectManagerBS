// @vitest-environment jsdom

import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ProjectManagementSyncPackageImportPanel } from "./ProjectManagementSyncPackageImportPanel";

interface ImportResult {
  data?: {
    attachmentsImported: number;
    inserted: number;
    packageId: string;
    skipped: number;
    strategy: string;
    updated: number;
    warnings: string[];
  };
}

const mutationOptions = vi.hoisted(() => [] as Array<{ onSuccess?: (result: ImportResult) => void }>);
const permissionState = vi.hoisted(() => ({ canImport: true }));
const messages = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));

vi.mock("../../../core/auth/usePermission", () => ({
  usePermission: () => ({ hasPermission: permissionState.canImport }),
}));

vi.mock("../../../core/query/useApiMutation", () => ({
  useApiMutation: (options: { onSuccess?: (result: ImportResult) => void }) => {
    mutationOptions.push(options);
    return { isPending: false, mutate: vi.fn() };
  },
}));

vi.mock("../../../shared/auth/PermissionButton", () => ({
  PermissionButton: ({
    children,
    code,
    ...props
  }: React.ButtonHTMLAttributes<HTMLButtonElement> & { code: string }) => {
    void code;
    return <button {...props}>{children}</button>;
  },
}));

vi.mock("../../../shared/feedback/useMessage", () => ({
  useMessage: () => messages,
}));

describe("ProjectManagementSyncPackageImportPanel", () => {
  afterEach(() => {
    cleanup();
  });

  beforeEach(() => {
    mutationOptions.length = 0;
    permissionState.canImport = true;
    messages.error.mockReset();
    messages.success.mockReset();
  });

  it("keeps import warnings visible after a successful apply", () => {
    render(<ProjectManagementSyncPackageImportPanel />);

    act(() => {
      mutationOptions[1]?.onSuccess?.({
        data: {
          attachmentsImported: 0,
          inserted: 2,
          packageId: "package-1",
          skipped: 3,
          strategy: "Skip",
          updated: 1,
          warnings: ["未包含附件内容，附件记录已跳过"],
        },
      });
    });

    expect(screen.getByText("导入完成，但有需要关注的结果")).toBeTruthy();
    expect(screen.getByText("未包含附件内容，附件记录已跳过")).toBeTruthy();
    expect(messages.success).toHaveBeenCalledWith("同步包已导入：新增 2，更新 1，跳过 3");
  });

  it("does not render a partial-result panel when apply succeeds without warnings", () => {
    render(<ProjectManagementSyncPackageImportPanel />);

    act(() => {
      mutationOptions[1]?.onSuccess?.({
        data: {
          attachmentsImported: 1,
          inserted: 1,
          packageId: "package-2",
          skipped: 0,
          strategy: "Overwrite",
          updated: 0,
          warnings: [],
        },
      });
    });

    expect(screen.queryByText("导入完成，但有需要关注的结果")).toBeNull();
    expect(messages.success).toHaveBeenCalledWith("同步包已导入：新增 1，更新 0，跳过 0");
  });
});
