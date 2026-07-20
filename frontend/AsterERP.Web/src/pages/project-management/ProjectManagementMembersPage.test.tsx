// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
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
  observedKeys: [] as unknown[][],
}));

const i18nMap: Record<string, string> = {
  "projectManagement.members.form.title": "添加成员",
  "projectManagement.members.form.editTitle": "编辑成员",
  "projectManagement.members.form.hint": "搜索并选择用户",
  "projectManagement.members.form.user": "成员用户",
  "projectManagement.members.form.searchPlaceholder": "搜索姓名、账号、邮箱或手机",
  "projectManagement.members.form.employment": "成员任职",
  "projectManagement.members.form.role": "成员角色",
  "projectManagement.members.form.scope": "根主题任务范围",
  "projectManagement.members.form.selectEmployment": "选择任职关系",
  "projectManagement.members.form.defaultEmployment": "默认任职",
  "projectManagement.members.form.employmentUnavailable": "任职关系暂不可用",
  "projectManagement.members.form.userUnavailable": "用户别名暂不可用",
  "projectManagement.members.form.scopeWholeProject": "整个项目（可选）",
  "projectManagement.members.form.scopeUnavailable": "任务范围暂不可用",
  "projectManagement.members.form.leadScopeHint": "Lead 范围提示",
  "projectManagement.members.form.save": "保存修改",
  "projectManagement.members.form.submitting": "提交中…",
  "projectManagement.members.form.cancelEdit": "取消编辑",
  "projectManagement.members.form.searching": "正在搜索人员…",
  "projectManagement.members.form.noResults": "没有匹配的人员",
  "projectManagement.members.form.loadFailed": "成员候选人加载失败",
  "projectManagement.members.form.candidatesFailed": "成员候选人加载失败，无法保存成员任职；现有成员仍可查看。",
  "projectManagement.members.form.topicRootsFailed": "根主题任务加载失败，Lead 只能保存为整个项目范围。",
  "projectManagement.members.form.retry": "重试",
  "projectManagement.home.members.add": "添加成员",
  "projectManagement.members.list.user": "用户",
  "projectManagement.members.list.role": "角色",
  "projectManagement.members.list.scope": "任务范围",
  "projectManagement.members.list.status": "状态",
  "projectManagement.members.list.actions": "操作",
  "projectManagement.members.list.active": "有效",
  "projectManagement.members.list.left": "已离开",
  "projectManagement.members.list.empty": "暂无项目成员",
  "projectManagement.members.list.scopeAll": "项目全部任务",
  "projectManagement.members.list.edit": "编辑",
  "projectManagement.members.list.remove": "移除",
};

vi.mock("@tanstack/react-query", () => ({
  useQuery: (options: { queryKey?: readonly unknown[] }) => {
    if (options.queryKey) queryState.observedKeys.push([...options.queryKey]);
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

vi.mock("../../features/project-management/projectManagementI18n", () => ({
  useProjectManagementI18n: () => ({
    locale: "zh-CN",
    t: (key: string) => i18nMap[key] ?? key,
    format: (key: string) => i18nMap[key] ?? key,
    date: () => "",
    dateTime: () => "",
    commentDateTime: () => "",
  }),
}));

vi.mock("../../shared/auth/PermissionButton", () => ({
  PermissionButton: ({ children, code, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { children: ReactNode; code: string }) => (
    <button {...props} data-permission-code={code}>
      {children}
    </button>
  ),
}));

vi.mock("../../shared/auth/PermissionGuard", () => ({
  PermissionGuard: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

vi.mock("../../shared/feedback/useMessage", () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}));

vi.mock("../../shared/responsive/ResponsivePage", () => ({
  ResponsivePage: ({ children, title }: { children: ReactNode; title: string }) => (
    <main>
      <h1>{title}</h1>
      {children}
    </main>
  ),
}));

vi.mock("../../shared/status/Page403", () => ({ Page403: () => <p>403</p> }));
vi.mock("../../shared/status/PageError", () => ({
  PageError: ({ description }: { description?: ReactNode }) => <p>{description}</p>,
}));
vi.mock("../../shared/status/PageLoading", () => ({ PageLoading: () => <p>loading</p> }));

function queryResult(data: unknown, isError = false) {
  return {
    data,
    error: isError ? new Error("query failed") : null,
    isError,
    isFetching: false,
    isLoading: false,
    refetch: vi.fn(),
  };
}

async function selectUserFromAutocomplete(displayName: string) {
  const input = screen.getByRole("combobox", { name: "成员用户" });
  fireEvent.mouseDown(input);
  const option = await screen.findByRole("option", { name: new RegExp(displayName) });
  fireEvent.click(option);
}

async function selectMuiOption(label: string, optionName: string | RegExp) {
  fireEvent.mouseDown(screen.getByLabelText(label));
  const option = await screen.findByRole("option", { name: optionName });
  fireEvent.click(option);
}

describe("ProjectManagementMembersPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    vi.clearAllMocks();
    queryState.observedKeys = [];
    api.createMember.mockResolvedValue({ data: {} });
    api.deleteMember.mockResolvedValue({ data: {} });
    api.updateMember.mockResolvedValue({ data: {} });
    queryState.members = queryResult({ data: { total: 0, items: [] } });
    queryState.candidates = queryResult({
      data: {
        total: 3,
        items: [
          {
            userId: "user-a",
            userName: "alice",
            displayName: "Alice",
            employmentId: "employment-a",
            employmentName: "研发一岗",
            status: "Enabled",
            isSelectable: true,
          },
          {
            userId: "user-a",
            userName: "alice",
            displayName: "Alice",
            employmentId: "employment-b",
            employmentName: "研发二岗",
            status: "Enabled",
            isSelectable: true,
          },
          {
            userId: "user-b",
            userName: "bob",
            displayName: "Bob",
            employmentId: "employment-c",
            employmentName: "产品岗",
            status: "Enabled",
            isSelectable: true,
          },
        ],
      },
    });
    queryState.tasks = queryResult({
      data: {
        total: 2,
        items: [
          {
            id: "root-a",
            projectId: "project-a",
            taskCode: "ROOT",
            title: "根主题",
            status: "Todo",
            priority: "Medium",
            progressPercent: 0,
            sortOrder: 0,
            depth: 0,
            versionNo: 1,
            blockedByCount: 0,
            canStart: true,
          },
          {
            id: "child-a",
            projectId: "project-a",
            parentTaskId: "root-a",
            taskCode: "CHILD",
            title: "子任务",
            status: "Todo",
            priority: "Medium",
            progressPercent: 0,
            sortOrder: 0,
            depth: 1,
            versionNo: 1,
            blockedByCount: 0,
            canStart: true,
          },
        ],
      },
    });
  });

  it("selects an exact employment and root topic instead of guessing by user id", async () => {
    render(<ProjectManagementMembersPage />);

    await selectUserFromAutocomplete("Alice");

    fireEvent.mouseDown(screen.getByLabelText("成员任职"));
    const employmentOptions = await screen.findAllByRole("option");
    expect(employmentOptions.map((option) => option.textContent)).toEqual([
      "选择任职关系",
      "研发一岗",
      "研发二岗",
    ]);
    // Still empty until an employment is chosen (multi-employment user).
    expect(screen.getByLabelText("成员任职").textContent).toContain("选择任职关系");
    fireEvent.click(screen.getByRole("option", { name: "研发二岗" }));

    await selectMuiOption("成员角色", "Lead");
    fireEvent.mouseDown(screen.getByLabelText("根主题任务范围"));
    expect(await screen.findByRole("option", { name: "ROOT · 根主题" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "CHILD · 子任务" })).toBeNull();
    fireEvent.click(screen.getByRole("option", { name: "ROOT · 根主题" }));

    fireEvent.click(screen.getByRole("button", { name: "添加成员" }));

    await waitFor(() =>
      expect(api.createMember).toHaveBeenCalledWith("project-a", {
        employmentId: "employment-b",
        roleCode: "Lead",
        scopeRootTaskId: "root-a",
        userId: "user-a",
        versionNo: 0,
      }),
    );
  });

  it("uses the fixed role set and exposes retries for candidate and root-task failures", async () => {
    queryState.candidates = queryResult(undefined, true);
    queryState.tasks = queryResult(undefined, true);

    render(<ProjectManagementMembersPage />);

    fireEvent.mouseDown(screen.getByLabelText("成员角色"));
    const roleOptions = await screen.findAllByRole("option");
    expect(roleOptions.map((option) => option.textContent)).toEqual([
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

  it("includes keyword in member-candidate search query keys after debounce", async () => {
    render(<ProjectManagementMembersPage />);
    const input = screen.getByRole("combobox", { name: "成员用户" });
    fireEvent.change(input, { target: { value: "Alice" } });

    await waitFor(
      () => {
        const matched = queryState.observedKeys.some(
          (key) => key.includes("member-candidates") && key.includes("Alice"),
        );
        expect(matched).toBe(true);
      },
      { timeout: 1000 },
    );
  });

  it("excludes users who are already active project members from the candidate list", async () => {
    queryState.members = queryResult({
      data: {
        total: 1,
        items: [
          {
            id: "member-a",
            projectId: "project-a",
            userId: "user-a",
            roleCode: "Member",
            isActive: true,
            joinedAt: "2026-01-01T00:00:00Z",
            versionNo: 1,
            displayName: "Alice",
          },
        ],
      },
    });

    render(<ProjectManagementMembersPage />);

    const input = screen.getByRole("combobox", { name: "成员用户" });
    fireEvent.mouseDown(input);

    await waitFor(() => {
      expect(screen.queryByRole("option", { name: /Alice/ })).toBeNull();
      expect(screen.getByRole("option", { name: /Bob/ })).toBeTruthy();
    });
  });
});
