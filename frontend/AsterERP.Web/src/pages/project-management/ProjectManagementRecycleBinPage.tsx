import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

import {
  getProjectManagementRecycle,
  purgeProjectManagementProject,
  restoreProjectManagementProject,
  restoreProjectManagementTask,
} from "../../api/project-management/projectManagement.api";
import type { ProjectManagementRecycleProjectItem, ProjectManagementRecycleTaskItem } from "../../api/project-management/projectManagement.types";
import { isHttpError } from "../../core/http/httpError";
import { projectManagementQueryKeys } from "../../core/query/projectManagementQueryKeys";
import { useApiMutation } from "../../core/query/useApiMutation";
import { useProjectManagementWorkspaceScope } from "../../features/project-management/state/projectManagementWorkspaceScope";
import { PermissionButton } from "../../shared/auth/PermissionButton";
import { PermissionGuard } from "../../shared/auth/PermissionGuard";
import { useConfirm } from "../../shared/feedback/useConfirm";
import { useMessage } from "../../shared/feedback/useMessage";
import { ResponsivePage } from "../../shared/responsive/ResponsivePage";
import { Page403 } from "../../shared/status/Page403";
import { PageError } from "../../shared/status/PageError";
import { PageLoading } from "../../shared/status/PageLoading";
import { getErrorMessage } from "../../shared/utils/errorMessage";

export function ProjectManagementRecycleBinPage() {
  const scope = useProjectManagementWorkspaceScope();
  const queryClient = useQueryClient();
  const message = useMessage();
  const confirm = useConfirm();
  const [keyword, setKeyword] = useState("");
  const query = { pageIndex: 1, pageSize: 100, keyword: keyword.trim() || undefined };
  const recycleQuery = useQuery({
    queryKey: projectManagementQueryKeys.recycle(scope, query),
    queryFn: ({ signal }) => getProjectManagementRecycle(query, signal),
    enabled: scope.isAvailable,
  });
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.recycle(scope, query) });
    await queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.projects(scope, {}) });
  };
  const restoreProjectMutation = useApiMutation({
    mutationFn: (item: ProjectManagementRecycleProjectItem) => restoreProjectManagementProject(item.id, item.versionNo),
    onError: (error) => message.error(getErrorMessage(error, "项目恢复失败")),
    onSuccess: async () => { message.success("项目已恢复"); await refresh(); },
  });
  const restoreTaskMutation = useApiMutation({
    mutationFn: ({ item, restoreDescendants }: { item: ProjectManagementRecycleTaskItem; restoreDescendants: boolean }) => restoreProjectManagementTask(item.id, item.versionNo, restoreDescendants),
    onError: (error) => message.error(getErrorMessage(error, "任务恢复失败")),
    onSuccess: async () => { message.success("任务已恢复"); await refresh(); },
  });
  const purgeProjectMutation = useApiMutation({
    mutationFn: (item: ProjectManagementRecycleProjectItem) => purgeProjectManagementProject(item.id, item.versionNo),
    onError: (error) => message.error(getErrorMessage(error, "项目永久删除失败")),
    onSuccess: async () => { message.success("项目已永久删除"); await refresh(); },
  });

  if (recycleQuery.isLoading) return <PageLoading />;
  if (recycleQuery.isError) {
    if (isHttpError(recycleQuery.error) && recycleQuery.error.status === 403) return <Page403 />;
    return <PageError description="回收站加载失败" action={<button type="button" onClick={() => void recycleQuery.refetch()}>重试</button>} />;
  }
  const recycle = recycleQuery.data?.data;
  const projects = recycle?.projects.items ?? [];
  const tasks = recycle?.tasks.items ?? [];

  return (
    <PermissionGuard code="project-management:project:view">
      <ResponsivePage
        title="项目回收站"
        description="按当前工作区和项目成员范围查看已删除对象；恢复和永久删除会再次由服务端校验对象权限与版本。"
        eyebrow="ProjectManagement / Recycle Bin"
      >
        <div className="mb-4 flex flex-wrap gap-2">
          <input aria-label="搜索已删除对象" className="min-w-56 rounded border border-gray-300 px-3 py-2" value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="按项目编码、项目名或任务搜索" />
          <button type="button" onClick={() => void recycleQuery.refetch()}>刷新</button>
        </div>
        <section className="mb-6">
          <h2 className="mb-3 font-semibold">已删除项目（{recycle?.projects.total ?? 0}）</h2>
          <RecycleTable emptyText="暂无已删除项目" headers={["项目", "状态", "删除时间", "删除人", "操作"]}>
            {projects.map((item) => <tr className="border-t border-gray-100" key={item.id}><td className="px-3 py-2"><div className="font-medium">{item.projectName}</div><div className="text-xs text-gray-500">{item.projectCode}</div></td><td className="px-3 py-2">{item.status}</td><td className="px-3 py-2">{formatDate(item.deletedTime)}</td><td className="px-3 py-2">{item.deletedBy ?? "-"}</td><td className="px-3 py-2"><div className="flex gap-2"><PermissionButton code="project-management:project:restore" disabled={!item.canRestore || restoreProjectMutation.isPending} onClick={() => confirm({ title: "恢复项目", content: `恢复“${item.projectName}”后将重新计算项目进度。`, confirmText: "恢复", onConfirm: () => restoreProjectMutation.mutate(item) })}>恢复</PermissionButton><PermissionButton code="project-management:project:purge" disabled={!item.canPurge || purgeProjectMutation.isPending} onClick={() => confirm({ title: "永久删除项目", content: `永久删除“${item.projectName}”不可撤销；存在成员、里程碑或任务引用时服务端会拒绝操作。`, confirmText: "永久删除", onConfirm: () => purgeProjectMutation.mutate(item) })}>永久删除</PermissionButton></div></td></tr>)}
          </RecycleTable>
        </section>
        <section>
          <h2 className="mb-3 font-semibold">已删除任务（{recycle?.tasks.total ?? 0}）</h2>
          <RecycleTable emptyText="暂无已删除任务" headers={["任务", "项目", "状态", "删除时间", "删除人", "操作"]}>
            {tasks.map((item) => <tr className="border-t border-gray-100" key={item.id}><td className="px-3 py-2"><div className="font-medium">{item.title}</div><div className="text-xs text-gray-500">{item.taskCode}</div></td><td className="px-3 py-2 font-mono text-xs">{item.projectId}</td><td className="px-3 py-2">{item.status}</td><td className="px-3 py-2">{formatDate(item.deletedTime)}</td><td className="px-3 py-2">{item.deletedBy ?? "-"}</td><td className="px-3 py-2"><div className="flex gap-2"><PermissionButton code="project-management:task:restore" disabled={!item.canRestore || restoreTaskMutation.isPending} onClick={() => confirm({ title: "恢复任务", content: `恢复“${item.title}”。若所属项目、父任务或里程碑未恢复，服务端会明确拒绝。`, confirmText: "仅恢复任务", onConfirm: () => restoreTaskMutation.mutate({ item, restoreDescendants: false }) })}>仅恢复</PermissionButton><PermissionButton code="project-management:task:restore" disabled={!item.canRestore || restoreTaskMutation.isPending} onClick={() => confirm({ title: "恢复任务及子树", content: `恢复“${item.title}”及其所有已删除后代；任一任务的父任务、里程碑或编码冲突都会使整个操作失败。`, confirmText: "恢复子树", onConfirm: () => restoreTaskMutation.mutate({ item, restoreDescendants: true }) })}>恢复子树</PermissionButton></div></td></tr>)}
          </RecycleTable>
        </section>
      </ResponsivePage>
    </PermissionGuard>
  );
}

function RecycleTable({ children, emptyText, headers }: { children: ReactNode; emptyText: string; headers: string[] }) {
  const rows = Array.isArray(children) ? children : [children];
  return <div className="overflow-x-auto rounded-lg border border-gray-200"><table className="min-w-full text-left text-sm"><thead className="bg-gray-50"><tr>{headers.map((header) => <th className="px-3 py-2" key={header}>{header}</th>)}</tr></thead><tbody>{rows.length === 0 ? <tr><td className="px-3 py-8 text-center text-gray-500" colSpan={headers.length}>{emptyText}</td></tr> : children}</tbody></table></div>;
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : "-";
}
