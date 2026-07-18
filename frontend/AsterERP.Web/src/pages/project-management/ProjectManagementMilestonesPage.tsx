import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";

import {
  createProjectManagementMilestone,
  deleteProjectManagementMilestone,
  getProjectManagementMilestones,
  updateProjectManagementMilestone,
} from "../../api/project-management/projectManagement.api";
import type {
  ProjectManagementMilestone,
  ProjectManagementMilestoneUpsertRequest,
} from "../../api/project-management/projectManagement.types";
import { isHttpError } from "../../core/http/httpError";
import { projectManagementQueryKeys } from "../../core/query/projectManagementQueryKeys";
import { useApiMutation } from "../../core/query/useApiMutation";
import { useProjectManagementWorkspaceScope } from "../../features/project-management/state/projectManagementWorkspaceScope";
import { PermissionButton } from "../../shared/auth/PermissionButton";
import { PermissionGuard } from "../../shared/auth/PermissionGuard";
import { useMessage } from "../../shared/feedback/useMessage";
import { ResponsivePage } from "../../shared/responsive/ResponsivePage";
import { Page403 } from "../../shared/status/Page403";
import { PageError } from "../../shared/status/PageError";
import { PageLoading } from "../../shared/status/PageLoading";
import { getErrorMessage } from "../../shared/utils/errorMessage";

const emptyForm: ProjectManagementMilestoneUpsertRequest = {
  milestoneName: "",
  status: "Planned",
  progressPercent: 0,
  sortOrder: 0,
  versionNo: 0,
};
export function ProjectManagementMilestonesPage() {
  const scope = useProjectManagementWorkspaceScope();
  const { projectId = "" } = useParams<{ projectId: string }>();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const milestonesQuery = useQuery({
    queryKey: projectManagementQueryKeys.milestones(scope, projectId),
    queryFn: ({ signal }) => getProjectManagementMilestones(projectId, signal),
    enabled: scope.isAvailable && Boolean(projectId),
  });
  useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
      event.returnValue = "里程碑表单有未保存更改。";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty]);
  const refresh = () => queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.milestones(scope, projectId) });
  const saveMutation = useApiMutation({
    mutationFn: () =>
      editingId
        ? updateProjectManagementMilestone(projectId, editingId, form)
        : createProjectManagementMilestone(projectId, form),
    onError: (error) => message.error(getErrorMessage(error, editingId ? "里程碑保存失败" : "里程碑创建失败")),
    onSuccess: async () => {
      message.success(editingId ? "里程碑已更新" : "里程碑已创建");
      setForm(emptyForm);
      setEditingId(null);
      setDirty(false);
      await refresh();
    },
  });
  const deleteMutation = useApiMutation({
    mutationFn: (milestone: ProjectManagementMilestone) =>
      deleteProjectManagementMilestone(projectId, milestone.id, milestone.versionNo),
    onError: (error) => message.error(getErrorMessage(error, "里程碑删除失败")),
    onSuccess: async () => {
      message.success("里程碑已删除");
      await refresh();
    },
  });
  if (milestonesQuery.isLoading) return <PageLoading />;
  if (milestonesQuery.isError) {
    if (isHttpError(milestonesQuery.error) && milestonesQuery.error.status === 403) return <Page403 />;
    return (
      <PageError
        description="里程碑加载失败"
        action={
          <button type="button" onClick={() => void milestonesQuery.refetch()}>
            重试
          </button>
        }
      />
    );
  }
  const milestones = milestonesQuery.data?.data ?? [];
  const update = (next: ProjectManagementMilestoneUpsertRequest) => {
    setForm(next);
    setDirty(true);
  };
  return (
    <ResponsivePage
      title="里程碑管理"
      description="定义项目阶段目标、负责人和交付健康度。"
      eyebrow="ProjectManagement / Milestones"
    >
      <PermissionGuard code="project-management:milestone:manage" fallback={null}>
        <section className="mb-4 rounded-lg border border-gray-200 p-4">
          <h2 className="mb-3 font-semibold">{editingId ? "编辑里程碑" : "新建里程碑"}</h2>
          <div className="grid gap-2 md:grid-cols-4">
            <input
              aria-label="里程碑名称"
              value={form.milestoneName}
              onChange={(event) => update({ ...form, milestoneName: event.target.value })}
              placeholder="里程碑名称"
            />
            <select
              aria-label="里程碑状态"
              value={form.status}
              onChange={(event) => update({ ...form, status: event.target.value })}
            >
              {["Planned", "Active", "Completed", "Archived"].map((status) => (
                <option key={status}>{status}</option>
              ))}
            </select>
            <input
              aria-label="里程碑负责人"
              value={form.ownerUserId ?? ""}
              onChange={(event) => update({ ...form, ownerUserId: event.target.value || undefined })}
              placeholder="负责人用户 ID"
            />
            <input
              aria-label="里程碑进度"
              type="number"
              min={0}
              max={100}
              value={form.progressPercent ?? 0}
              onChange={(event) => update({ ...form, progressPercent: Number(event.target.value) })}
            />
            <input
              aria-label="目标日期"
              type="date"
              value={form.dueDate?.slice(0, 10) ?? ""}
              onChange={(event) => update({ ...form, dueDate: event.target.value || undefined })}
            />
            <input
              aria-label="排序"
              type="number"
              value={form.sortOrder ?? 0}
              onChange={(event) => update({ ...form, sortOrder: Number(event.target.value) })}
              placeholder="排序"
            />
            <textarea
              aria-label="里程碑描述"
              value={form.description ?? ""}
              onChange={(event) => update({ ...form, description: event.target.value || undefined })}
              placeholder="描述"
            />
          </div>
          <div className="mt-3 flex gap-2">
            <PermissionButton
              code="project-management:milestone:manage"
              disabled={!form.milestoneName.trim() || saveMutation.isPending}
              onClick={() => saveMutation.mutate()}
            >
              {saveMutation.isPending ? "提交中…" : editingId ? "保存修改" : "创建里程碑"}
            </PermissionButton>
            {editingId ? (
              <button
                type="button"
                onClick={() => {
                  setEditingId(null);
                  setForm(emptyForm);
                  setDirty(false);
                }}
              >
                取消编辑
              </button>
            ) : null}
          </div>
        </section>
      </PermissionGuard>
      {milestones.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
          暂无里程碑
        </div>
      ) : (
        <div className="space-y-2">
          {milestones.map((milestone) => (
            <article className="rounded-lg border border-gray-200 p-4" key={milestone.id}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h3 className="font-semibold">{milestone.milestoneName}</h3>
                  <p className="mt-1 text-sm text-gray-500">
                    {milestone.status} · 健康度 {milestone.healthStatus} · 负责人 {milestone.ownerUserId ?? "未设置"}
                  </p>
                  <p className="mt-1 text-sm text-gray-500">
                    目标日期 {milestone.dueDate?.slice(0, 10) ?? "未设置"} · 叶子任务 {milestone.completedLeafTaskCount}/{milestone.leafTaskCount}
                  </p>
                </div>
                <div className="flex gap-2">
                  <PermissionButton
                    code="project-management:milestone:manage"
                    onClick={() => {
                      setEditingId(milestone.id);
                      setForm({
                        milestoneName: milestone.milestoneName,
                        description: milestone.description,
                        ownerUserId: milestone.ownerUserId,
                        status: milestone.status,
                        startDate: milestone.startDate,
                        dueDate: milestone.dueDate,
                        progressPercent: milestone.progressPercent,
                        sortOrder: milestone.sortOrder,
                        versionNo: milestone.versionNo,
                      });
                      setDirty(false);
                    }}
                  >
                    编辑
                  </PermissionButton>
                  <PermissionButton
                    code="project-management:milestone:manage"
                    disabled={deleteMutation.isPending}
                    onClick={() => deleteMutation.mutate(milestone)}
                  >
                    删除
                  </PermissionButton>
                </div>
              </div>
              <div className="mt-3 h-2 overflow-hidden rounded bg-gray-100">
                <div
                  className="h-full bg-blue-600"
                  style={{ width: `${Math.min(100, Math.max(0, milestone.progressPercent))}%` }}
                />
              </div>
              <div className="mt-1 text-right text-xs text-gray-500">{milestone.progressPercent}%</div>
            </article>
          ))}
        </div>
      )}
    </ResponsivePage>
  );
}
