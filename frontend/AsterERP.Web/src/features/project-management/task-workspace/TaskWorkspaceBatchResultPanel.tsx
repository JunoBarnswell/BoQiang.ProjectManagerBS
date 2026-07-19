import type { ProjectManagementTaskBatchExecutionResult } from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';

import { taskBatchResultStatusLabel } from './taskBatchExecutionModel';

interface TaskWorkspaceBatchResultPanelProps {
  onClose: () => void;
  onDownload: () => void;
  result: ProjectManagementTaskBatchExecutionResult;
}

export function TaskWorkspaceBatchResultPanel({ onClose, onDownload, result }: TaskWorkspaceBatchResultPanelProps) {
  return (
    <section aria-labelledby="task-batch-result-title" className="pm-batch-result">
      <div className="pm-batch-result__heading">
        <div>
          <h2 id="task-batch-result-title">批量操作结果</h2>
          <p>操作 {result.operationId} · 共 {result.requestedCount} 项，成功 {result.succeededCount}，跳过 {result.skippedCount}，失败 {result.failedCount}，冲突 {result.conflictCount}</p>
        </div>
        <div className="pm-batch-result__actions">
          <PermissionButton code="project-management:report:export" onClick={onDownload}>下载明细 CSV</PermissionButton>
          <button type="button" onClick={onClose}>关闭</button>
        </div>
      </div>
      <div className="pm-batch-result__items" role="list">
        {result.items.map((item) => (
          <div className="pm-batch-result__item" key={item.taskId} role="listitem">
            <strong>{item.taskCode || item.taskId}</strong>
            <span>{taskBatchResultStatusLabel(item.status)}</span>
            <span>{item.message || '已完成'}</span>
          </div>
        ))}
      </div>
    </section>
  );
}
