import { useState } from 'react';
import { useParams } from 'react-router-dom';

import { exportProjectManagementReportCsv, exportProjectManagementReportExcel } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementReportQuery } from '../../api/project-management/projectManagement.types';
import { useApiMutation } from '../../core/query/useApiMutation';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { getErrorMessage } from '../../shared/utils/errorMessage';

const statuses = ['', 'Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'];

export function ProjectManagementReportsPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState('');
  const [pageSize, setPageSize] = useState(100);
  const query: ProjectManagementReportQuery = { pageIndex: 1, pageSize, keyword: keyword.trim() || undefined, status: status || undefined };
  const csvMutation = useApiMutation({
    mutationFn: () => exportProjectManagementReportCsv(query),
    onError: (error) => message.error(getErrorMessage(error, 'CSV 报表导出失败')),
    onSuccess: (result) => download(result.blob, result.fileName, message),
  });
  const excelMutation = useApiMutation({
    mutationFn: () => exportProjectManagementReportExcel(query),
    onError: (error) => message.error(getErrorMessage(error, 'Excel 报表导出失败')),
    onSuccess: (result) => download(result.blob, result.fileName, message),
  });

  return <ResponsivePage
    title="项目报表"
    eyebrow="ProjectManagement / Reports"
    description="当前后端只提供按工作区权限过滤的项目 CSV/XLSX 导出，不提供单项目报表预览 API；本页面不会把路由中的项目 ID 伪装成服务器已支持的筛选条件。"
    toolbar={<span className="text-sm text-gray-500">当前深链项目：{projectId ?? '未指定'} · 导出范围：当前授权工作区</span>}
  >
    <section className="max-w-3xl rounded-lg border border-gray-200 p-4">
      <h2 className="font-semibold">导出条件</h2>
      <div className="mt-3 grid gap-3 md:grid-cols-3">
        <label className="text-sm">关键字<input className="mt-1 w-full" maxLength={200} placeholder="项目编码或名称" value={keyword} onChange={(event) => setKeyword(event.target.value)} /></label>
        <label className="text-sm">项目状态<select className="mt-1 w-full" value={status} onChange={(event) => setStatus(event.target.value)}>{statuses.map((item) => <option key={item} value={item}>{item || '全部状态'}</option>)}</select></label>
        <label className="text-sm">最大导出行数<select className="mt-1 w-full" value={pageSize} onChange={(event) => setPageSize(Number(event.target.value))}><option value={100}>100</option><option value={500}>500</option></select></label>
      </div>
      <p className="mt-3 text-sm text-gray-500" role="status">导出文件由服务端生成，并对公式前缀进行安全处理。当前 API 的导出上限和数据权限由服务端执行。</p>
      <div className="mt-4 flex flex-wrap gap-2">
        <PermissionButton code="project-management:report:export" disabled={csvMutation.isPending || excelMutation.isPending} onClick={() => csvMutation.mutate()}>导出 CSV</PermissionButton>
        <PermissionButton code="project-management:report:export" disabled={csvMutation.isPending || excelMutation.isPending} onClick={() => excelMutation.mutate()}>导出 Excel</PermissionButton>
      </div>
    </section>
  </ResponsivePage>;
}

function download(blob: Blob, fileName: string, message: { success: (content: string) => void }) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
  message.success(`已生成 ${fileName}`);
}
