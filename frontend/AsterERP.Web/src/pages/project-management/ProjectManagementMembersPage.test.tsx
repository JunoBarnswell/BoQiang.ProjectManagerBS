// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ButtonHTMLAttributes, ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ProjectManagementMembersPage } from "./ProjectManagementMembersPage";

const api = vi.hoisted(() => ({
  createMember: vi.fn(),
  deleteMember: vi.fn(),
  updateMember: vi.fn(),
}));
const queryState = vi.hoisted(() => ({
  candidates: {} as Record<string, unknown>,
  members: {} as Record<string, unknown>,
  tasks: {} as Record<string, unknown>,
}));

vi.mock("@tanstack/react-query", () => ({
  useQuery: (options: { queryKey?: readonly unknown[] }) => {
    if (options.queryKey?.includes("member-candidates")) return queryState.candidates;
    if (options.queryKey?.includes("tasks")) return queryState.tasks;
    return queryState.members;
  },
  useQueryClient: () => ({ invalidateQueries: vi.fn() }),
}));

vi.mock("react-router-dom", () => ({ useParams: () => ({ projectId: "project-a" }) }));

vi.mock("../../api/project-management/projectManagement.api", () => ({
  createProjectManagementMember: (...args: unknown[]) => api.createMember(...args),
  deleteProjectManagementMember: (...args: unknown[]) => api.deleteMember(...args),
  getProjectManagementMemberCandidates: vi.fn(),
  getProjectManagementMembers: vi.fn(),
  getProjectManagementTasks: vi.fn(),
  updateProjectManagementMember: (...args: unknown[]) => api.updateMember(...args),
}));

vi.mock("../../core/query/useApiMutation", () => ({
  useApiMutation: (options: { mutationFn: (value?: unknown) => Promise<unknown> }) => ({
    isPending: false,
    mutate: (value?: unknown) => void options.mutationFn(value),
  }),
}));

vi.mock("../../features/project-management/state/projectManagementWorkspaceScope", () => ({
  useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: "tenant-a", appCode: "SYSTEM" }),
}));

vi.mock("../../shared/auth/PermissionButton", () => ({
  PermissionButton: ({ children, code, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { children: ReactNode; code: string }) => <button {...props} data-permission-code={code}>{children}</button>,
}));

vi.mock("../../shared/auth/PermissionGuard", () => ({
  PermissionGuard: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

vi.mock("../../shared/feedback/useMessage", () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}));

vi.mock("../../shared/responsive/ResponsivePage", () => ({
  ResponsivePage: ({ children, title }: { children: ReactNode; title: string }) => <main><h1>{title}</h1>{children}</main>,
}));

vi.mock("../../shared/status/Page403", () => ({ Page403: () => <p>403</p> }));
vi.mock("../../shared/status/PageError", () => ({ PageError: ({ description }: { description?: ReactNode }) => <p>{description}</p> }));
vi.mock("../../shared/status/PageLoading", () => ({ PageLoading: () => <p>loading</p> }));

function queryResult(data: unknown, isError = false) {
  return {
    data,
    error: isError ? new Error("query failed") : null,
    isError,
    isLoading: false,
    refetch: vi.fn(),
  };
}

describe("ProjectManagementMembersPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    vi.clearAllMocks();
    api.createMember.mockResolvedValue({ data: {} });
    api.deleteMember.mockResolvedValue({ data: {} });
    api.updateMember.mockResolvedValue({ data: {} });
    queryState.members = queryResult({ data: { total: 0, items: [] } });
    queryState.candidates = queryResult({
      data: {
        total: 3,
        items: [
          { userId: "user-a", userName: "alice", displayName: "Alice", employmentId: "employment-a", employmentName: "研发一岗", status: "Enabled", isSelectable: true },
          { userId: "user-a", userName: "alice", displayName: "Alice", employmentId: "employment-b", employmentName: "研发二岗", status: "Enabled", isSelectable: true },
          { userId: "user-b", userName: "bob", displayName: "Bob", employmentId: "employment-c", employmentName: "产品岗", status: "Enabled", isSelectable: true },
        ],
      },
    });
    queryState.tasks = queryResult({
      data: {
        total: 2,
        items: [
          { id: "root-a", projectId: "project-a", taskCode: "ROOT", title: "根主题", status: "Todo", priority: "Medium", progressPercent: 0, sortOrder: 0, depth: 0, versionNo: 1, blockedByCount: 0, canStart: true },
          { id: "child-a", projectId: "project-a", parentTaskId: "root-a", taskCode: "CHILD", title: "子任务", status: "Todo", priority: "Medium", progressPercent: 0, sortOrder: 0, depth: 1, versionNo: 1, blockedByCount: 0, canStart: true },
        ],
      },
    });
  });

  it("selects an exact employment and root topic instead of guessing by user id", async () => {
    render(<ProjectManagementMembersPage />);

    fireEvent.change(screen.getByLabelText("成员用户"), { target: { value: "user-a" } });
    const employmentSelect = screen.getByLabelText("成员任职");
    expect(within(employmentSelect).getAllByRole("option")).toHaveLength(3);
    expect((employmentSelect as HTMLSelectElement).value).toBe("");

    fireEvent.change(employmentSelect, { target: { value: "employment-b" } });
    fireEvent.change(screen.getByLabelText("成员角色"), { target: { value: "Lead" } });
    const rootSelect = screen.getByLabelText("根主题任务范围");
    expect(within(rootSelect).getByText("ROOT · 根主题")).toBeTruthy();
    expect(within(rootSelect).queryByText("CHILD · 子任务")).toBeNull();
    fireEvent.change(rootSelect, { target: { value: "root-a" } });
    fireEvent.click(screen.getByRole("button", { name: "添加成员" }));

    await waitFor(() => expect(api.createMember).toHaveBeenCalledWith("project-a", {
      employmentId: "employment-b",
      roleCode: "Lead",
      scopeRootTaskId: "root-a",
      userId: "user-a",
      versionNo: 0,
    }));
  });

  it("uses the fixed role set and exposes retries for candidate and root-task failures", () => {
    queryState.candidates = queryResult(undefined, true);
    queryState.tasks = queryResult(undefined, true);

    render(<ProjectManagementMembersPage />);

    const roleSelect = screen.getByLabelText("成员角色");
    expect(within(roleSelect).getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Owner",
      "Manager",
      "Lead",
      "Member",
      "Viewer",
    ]);
    expect(screen.getByText(/成员候选人加载失败/)).toBeTruthy();
    expect(screen.getByText(/根主题任务加载失败/)).toBeTruthy();
    expect(screen.getAllByRole("button", { name: "重试" })).toHaveLength(2);
  });
});
