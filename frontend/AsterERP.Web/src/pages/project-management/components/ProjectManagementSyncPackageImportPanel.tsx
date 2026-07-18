import { useState } from "react";

import {
  applyProjectManagementSync,
  previewProjectManagementSync,
} from "../../../api/project-management/projectManagement.api";
import type {
  ProjectManagementSyncImportResponse,
  ProjectManagementSyncPreviewResponse,
} from "../../../api/project-management/projectManagement.types";
import { usePermission } from "../../../core/auth/usePermission";
import { useApiMutation } from "../../../core/query/useApiMutation";
import { PermissionButton } from "../../../shared/auth/PermissionButton";
import { useMessage } from "../../../shared/feedback/useMessage";
import { getErrorMessage } from "../../../shared/utils/errorMessage";

type ConflictStrategy = "Skip" | "Overwrite" | "Reject";

export function ProjectManagementSyncPackageImportPanel() {
  const { hasPermission: canImport } = usePermission("project-management:sync:import");
  const message = useMessage();
  const [packageFile, setPackageFile] = useState<File | null>(null);
  const [password, setPassword] = useState("");
  const [confirmRisk, setConfirmRisk] = useState(false);
  const [conflictStrategy, setConflictStrategy] = useState<ConflictStrategy>("Skip");
  const [preview, setPreview] = useState<ProjectManagementSyncPreviewResponse | null>(null);
  const [importResult, setImportResult] = useState<ProjectManagementSyncImportResponse | null>(null);

  const previewMutation = useApiMutation({
    mutationFn: () => {
      if (!packageFile) throw new Error("请先选择同步包");
      return previewProjectManagementSync(packageFile);
    },
    onError: (error) => message.error(getErrorMessage(error, "同步包预览失败")),
    onSuccess: (result) => {
      setPreview(result.data);
      setImportResult(null);
      message.success(result.data?.isCompatible ? "同步包校验通过，请查看预览结果" : "同步包不兼容，无法导入");
    },
  });
  const applyMutation = useApiMutation({
    mutationFn: () => {
      if (!packageFile) throw new Error("请先选择同步包");
      return applyProjectManagementSync(packageFile, { currentPassword: password, confirmRisk, conflictStrategy });
    },
    onError: (error) => message.error(getErrorMessage(error, "同步包导入失败")),
    onSuccess: (result) => {
      const imported = result.data;
      message.success(
        `同步包已导入：新增 ${imported?.inserted ?? 0}，更新 ${imported?.updated ?? 0}，跳过 ${imported?.skipped ?? 0}`,
      );
      setImportResult(imported?.warnings.length ? imported : null);
      setPreview(null);
      setPackageFile(null);
    },
  });

  if (!canImport) return null;

  return (
    <div className="mt-3 space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <input
          aria-label="选择同步包"
          type="file"
          accept=".bqsync,application/zip"
          onChange={(event) => {
            setPackageFile(event.target.files?.[0] ?? null);
            setPreview(null);
            setImportResult(null);
          }}
        />
        <PermissionButton
          code="project-management:sync:import"
          disabled={!packageFile || previewMutation.isPending || applyMutation.isPending}
          onClick={() => previewMutation.mutate()}
        >
          {previewMutation.isPending ? "校验中…" : "预览同步包"}
        </PermissionButton>
      </div>
      {preview ? (
        <div className="rounded bg-slate-50 p-3 text-sm">
          <div className="font-medium">
            预览结果 · {preview.isCompatible ? "可导入" : "不可导入"} · {preview.packageId}
          </div>
          <div className="mt-2 grid gap-2 sm:grid-cols-3">
            <span>项目 {preview.projectCount}</span>
            <span>任务 {preview.taskCount}</span>
            <span>附件 {preview.attachmentCount}</span>
            <span>成员 {preview.memberCount}</span>
            <span>里程碑 {preview.milestoneCount}</span>
            <span>冲突 {preview.conflicts.length}</span>
          </div>
          {preview.conflicts.length ? (
            <div className="mt-2 text-amber-700">冲突：{preview.conflicts.join("；")}</div>
          ) : null}
          {preview.warnings.length ? (
            <div className="mt-2 text-amber-700">提示：{preview.warnings.join("；")}</div>
          ) : null}
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <select
              aria-label="冲突处理策略"
              value={conflictStrategy}
              onChange={(event) => setConflictStrategy(event.target.value as ConflictStrategy)}
            >
              <option value="Skip">跳过冲突</option>
              <option value="Overwrite">覆盖冲突</option>
              <option value="Reject">遇到冲突即拒绝</option>
            </select>
            <input
              className="rounded border border-gray-300 px-3 py-2"
              type="password"
              aria-label="导入当前密码"
              placeholder="导入当前密码"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={confirmRisk} onChange={(event) => setConfirmRisk(event.target.checked)} />
              确认执行高风险导入
            </label>
            <PermissionButton
              code="project-management:sync:import"
              disabled={!preview.isCompatible || !password || !confirmRisk || applyMutation.isPending}
              onClick={() => applyMutation.mutate()}
            >
              {applyMutation.isPending ? "导入中…" : "执行导入"}
            </PermissionButton>
          </div>
        </div>
      ) : null}
      {importResult ? (
        <section className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900" aria-live="polite">
          <h3 className="font-medium">导入完成，但有需要关注的结果</h3>
          <div className="mt-1">
            包 {importResult.packageId} · 策略 {importResult.strategy} · 新增 {importResult.inserted} · 更新{" "}
            {importResult.updated} · 跳过 {importResult.skipped} · 已导入附件 {importResult.attachmentsImported}
          </div>
          <ul className="mt-2 list-disc space-y-1 pl-5">
            {importResult.warnings.map((warning) => (
              <li key={warning}>{warning}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}
