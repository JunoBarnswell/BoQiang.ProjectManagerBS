import type { ProjectManagementOperation } from '../../../api/project-management/projectManagement.types';
import { ProjectManagementOperationProgress } from '../components/ProjectManagementOperationProgress';
import { PermissionButton } from '../../../shared/auth/PermissionButton';

interface ProjectManagementSnapshotJobStatusProps {
  completedSnapshot: ProjectManagementOperation | null;
  downloading: boolean;
  operationId: string;
  retrying: boolean;
  onDownload: () => void;
  onRetry: () => void;
  onTerminal: (operation: ProjectManagementOperation) => void;
  onTrackingEnded: () => void;
}

const steps = ['已进入队列', '正在生成', '可下载'];

export function ProjectManagementSnapshotJobStatus({
  completedSnapshot,
  downloading,
  operationId,
  retrying,
  onDownload,
  onRetry,
  onTerminal,
  onTrackingEnded,
}: ProjectManagementSnapshotJobStatusProps) {
  const currentStep = completedSnapshot?.status === 'Succeeded' ? 2 : completedSnapshot ? 1 : 1;

  return (
    <section className="mt-4 max-w-5xl rounded-lg border border-sky-200 bg-sky-50/40 p-4" aria-labelledby="report-snapshot-status-title">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 id="report-snapshot-status-title" className="font-semibold">后台快照任务</h2>
          <p className="mt-1 text-sm text-slate-600">快照在服务端生成并保留；关闭页面后仍可从同一工作区继续跟踪。</p>
        </div>
        <span className="text-sm text-slate-600">任务 {operationId.slice(0, 8)}</span>
      </div>
      <ol className="mt-4 grid gap-2 sm:grid-cols-3" aria-label="快照生成阶段">
        {steps.map((step, index) => (
          <li className={`rounded border px-3 py-2 text-sm ${index <= currentStep ? 'border-sky-300 bg-white text-sky-900' : 'border-slate-200 bg-slate-50 text-slate-500'}`} key={step}>
            <span className="mr-2 font-semibold">{index + 1}</span>{step}
          </li>
        ))}
      </ol>
      <div className="mt-3">
        <ProjectManagementOperationProgress
          clearOnTerminal={false}
          operationId={operationId}
          onTerminal={onTerminal}
          onTrackingEnded={onTrackingEnded}
        />
      </div>
      {completedSnapshot?.status === 'Succeeded' ? (
        <div className="mt-3 flex flex-wrap items-center gap-3 text-sm text-emerald-800">
          <span>快照已生成，可在有效期内重复下载。</span>
          <PermissionButton code="project-management:report:export" disabled={downloading} onClick={onDownload}>{downloading ? '下载中…' : '下载快照'}</PermissionButton>
        </div>
      ) : null}
      {completedSnapshot?.status === 'Failed' ? (
        <div className="mt-3 flex flex-wrap items-center gap-3 text-sm text-red-700">
          <span>生成失败：{completedSnapshot.errorMessage ?? '后台服务未提供失败原因'}</span>
          <PermissionButton code="project-management:report:export" disabled={retrying} onClick={onRetry}>{retrying ? '重试中…' : '重试生成'}</PermissionButton>
        </div>
      ) : null}
      {completedSnapshot?.status === 'Canceled' ? <p className="mt-3 text-sm text-amber-700">该快照任务已取消，未保留可下载文件。</p> : null}
    </section>
  );
}
