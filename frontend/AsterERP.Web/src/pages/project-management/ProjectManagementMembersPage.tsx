import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";

import {
  createProjectManagementMember,
  deleteProjectManagementMember,
  getProjectManagementMemberCandidates,
  getProjectManagementMembers,
  updateProjectManagementMember,
} from "../../api/project-management/projectManagement.api";
import type {
  ProjectManagementMember,
  ProjectManagementMemberUpsertRequest,
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

const emptyForm: ProjectManagementMemberUpsertRequest = { userId: "", roleCode: "Member", versionNo: 0 };
export function ProjectManagementMembersPage() {
  const scope = useProjectManagementWorkspaceScope();
  const { projectId = "" } = useParams<{ projectId: string }>();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const membersQuery = useQuery({
    queryKey: projectManagementQueryKeys.members(scope, projectId),
    queryFn: ({ signal }) => getProjectManagementMembers(projectId, signal),
    enabled: scope.isAvailable && Boolean(projectId),
  });
  const candidatesQuery = useQuery({
    queryKey: projectManagementQueryKeys.memberCandidates(scope, { pageIndex: 1, pageSize: 100 }),
    queryFn: ({ signal }) => getProjectManagementMemberCandidates({ pageIndex: 1, pageSize: 100 }, signal),
    enabled: scope.isAvailable && Boolean(projectId),
  });

  useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
      event.returnValue = "成员表单有未保存更改。";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.members(scope, projectId) });
  const saveMutation = useApiMutation({
    mutationFn: () =>
      editingId
        ? updateProjectManagementMember(projectId, editingId, form)
        : createProjectManagementMember(projectId, form),
    onError: (error) => message.error(getErrorMessage(error, editingId ? "成员保存失败" : "成员添加失败")),
    onSuccess: async () => {
      message.success(editingId ? "成员已更新" : "成员已添加");
      setForm(emptyForm);
      setEditingId(null);
      setDirty(false);
      await refresh();
    },
  });
  const deleteMutation = useApiMutation({
    mutationFn: (member: ProjectManagementMember) =>
      deleteProjectManagementMember(projectId, member.id, member.versionNo),
    onError: (error) => message.error(getErrorMessage(error, "成员移除失败")),
    onSuccess: async () => {
      message.success("成员已移除");
      await refresh();
    },
  });

  if (membersQuery.isLoading) return <PageLoading />;
  if (membersQuery.isError) {
    if (isHttpError(membersQuery.error) && membersQuery.error.status === 403) return <Page403 />;
    return (
      <PageError
        description="成员列表加载失败"
        action={
          <button type="button" onClick={() => void membersQuery.refetch()}>
            重试
          </button>
        }
      />
    );
  }
  const members = membersQuery.data?.data ?? [];
  const candidates = candidatesQuery.data?.data?.items ?? [];
  const updateForm = (next: ProjectManagementMemberUpsertRequest) => {
    setForm(next);
    setDirty(true);
  };

  return (
    <ResponsivePage title="项目成员" description="维护项目成员、角色与任务范围。" eyebrow="ProjectManagement / Members">
      <PermissionGuard code="project-management:member:manage" fallback={null}>
        <section className="mb-4 rounded-lg border border-gray-200 p-4">
          <h2 className="mb-3 font-semibold">{editingId ? "编辑成员" : "添加成员"}</h2>
          <div className="grid gap-2 md:grid-cols-3">
            <select
              aria-label="成员用户"
              value={form.userId}
              disabled={Boolean(editingId)}
              onChange={(event) => updateForm({ ...form, userId: event.target.value })}
            >
              <option value="">选择用户</option>
              {candidates
                .filter((candidate) => candidate.isSelectable || candidate.userId === form.userId)
                .map((candidate) => (
                  <option key={candidate.userId} value={candidate.userId}>
                    {candidate.displayName || candidate.userName} · {candidate.userId}
                  </option>
                ))}
            </select>
            <input
              aria-label="成员角色"
              value={form.roleCode ?? ""}
              onChange={(event) => updateForm({ ...form, roleCode: event.target.value })}
              placeholder="角色编码"
            />
            <input
              aria-label="任务范围根节点"
              value={form.scopeRootTaskId ?? ""}
              onChange={(event) => updateForm({ ...form, scopeRootTaskId: event.target.value || undefined })}
              placeholder="任务范围根节点（可选）"
            />
          </div>
          <div className="mt-3 flex gap-2">
            <PermissionButton
              code="project-management:member:manage"
              disabled={!form.userId.trim() || saveMutation.isPending}
              onClick={() => saveMutation.mutate()}
            >
              {saveMutation.isPending ? "提交中…" : editingId ? "保存修改" : "添加成员"}
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
          {candidatesQuery.isError ? (
            <p className="mt-2 text-sm text-amber-700">成员候选人加载失败，无法新增成员；现有成员仍可查看。</p>
          ) : null}
        </section>
      </PermissionGuard>
      {members.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
          暂无项目成员
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-3 py-2">用户</th>
                <th className="px-3 py-2">角色</th>
                <th className="px-3 py-2">任务范围</th>
                <th className="px-3 py-2">状态</th>
                <th className="px-3 py-2">操作</th>
              </tr>
            </thead>
            <tbody>
              {members.map((member) => (
                <tr className="border-t border-gray-100" key={member.id}>
                  <td className="px-3 py-2">{member.userId}</td>
                  <td className="px-3 py-2">{member.roleCode}</td>
                  <td className="px-3 py-2">{member.scopeRootTaskId ?? "项目全部任务"}</td>
                  <td className="px-3 py-2">{member.isActive ? "有效" : "已离开"}</td>
                  <td className="px-3 py-2">
                    <div className="flex gap-2">
                      <PermissionButton
                        code="project-management:member:manage"
                        onClick={() => {
                          setEditingId(member.id);
                          setForm({
                            userId: member.userId,
                            employmentId: member.employmentId,
                            roleCode: member.roleCode,
                            scopeRootTaskId: member.scopeRootTaskId,
                            versionNo: member.versionNo,
                          });
                          setDirty(false);
                        }}
                      >
                        编辑
                      </PermissionButton>
                      <PermissionButton
                        code="project-management:member:manage"
                        disabled={deleteMutation.isPending}
                        onClick={() => deleteMutation.mutate(member)}
                      >
                        移除
                      </PermissionButton>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </ResponsivePage>
  );
}
