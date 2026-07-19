import { useState } from 'react';

import { exportProjectManagementSync } from '../../../api/project-management/projectManagement.api';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { saveBlob } from '../../../shared/file-preview/filePreviewUtils';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface ProjectManagementSyncPackageExportPanelProps {
  deviceId: string;
}

type ExportStatus =
  | { kind: 'success'; fileName: string }
  | { kind: 'failure'; message: string }
  | null;

export function ProjectManagementSyncPackageExportPanel({ deviceId }: ProjectManagementSyncPackageExportPanelProps) {
  const message = useMessage();
  const [includeAttachments, setIncludeAttachments] = useState(false);
  const [status, setStatus] = useState<ExportStatus>(null);
  const exportMutation = useApiMutation({
    mutationFn: () => exportProjectManagementSync({
      deviceId: deviceId.trim(),
      includeAttachments,
    }),
    onError: (error) => {
      const failureMessage = getErrorMessage(error, '同步包导出失败');
      setStatus({ kind: 'failure', message: failureMessage });
      message.error(failureMessage);
    },
    onSuccess: ({ blob, fileName }) => {
      saveBlob(blob, fileName);
      setStatus({ kind: 'success', fileName });
      message.success(`已生成 ${fileName}`);
    },
  });

  const startExport = () => {
    setStatus(null);
    exportMutation.mutate();
  };

  return (
    <div className="mt-3 space-y-3" aria-live="polite">
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-sm">
          <input
            checked={includeAttachments}
            disabled={exportMutation.isPending}
            type="checkbox"
            onChange={(event) => setIncludeAttachments(event.target.checked)}
          />
          包含附件内容
        </label>
        <PermissionButton
          code="project-management:sync:export"
          disabled={!deviceId.trim() || exportMutation.isPending}
          onClick={startExport}
        >
          {exportMutation.isPending ? '正在生成同步包…' : '导出 .bqsync'}
        </PermissionButton>
      </div>
      <p className="text-sm text-gray-500">
        导出范围由服务端按当前工作区与项目访问权限确定。包含附件会增加包体积；服务端生成期间请保持此页面打开。
      </p>
      {exportMutation.isPending ? (
        <div className="rounded border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900" role="status">
          正在由服务端汇总项目快照和同步水位，文件生成后将自动开始下载。
        </div>
      ) : null}
      {status?.kind === 'success' ? (
        <div className="rounded border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900" role="status">
          同步包已生成并开始下载：{status.fileName}。请妥善保管该文件，导入前仍需重新执行服务端校验与冲突预览。
        </div>
      ) : null}
      {status?.kind === 'failure' ? (
        <div className="rounded border border-red-200 bg-red-50 p-3 text-sm text-red-900" role="alert">
          <p>生成失败：{status.message}</p>
          <button className="mt-2" type="button" onClick={startExport}>重试导出</button>
        </div>
      ) : null}
    </div>
  );
}
