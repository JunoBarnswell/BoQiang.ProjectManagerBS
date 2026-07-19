import { useRef, useState } from 'react';

import { downloadProjectManagementExcelTemplate, previewProjectManagementExcel } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementExcelImportPreview } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { formatFileSize } from '../../../shared/file-preview/filePreviewUtils';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

type FailedAction = 'preview' | null;

export function ProjectManagementExcelImportPanel() {
  const { hasPermission: canImport } = usePermission('project-management:sync:import');
  const message = useMessage();
  const abortController = useRef<AbortController | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProjectManagementExcelImportPreview | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [failedAction, setFailedAction] = useState<FailedAction>(null);
  const templateMutation = useApiMutation({
    mutationFn: () => downloadProjectManagementExcelTemplate(),
    onError: (error) => message.error(getErrorMessage(error, 'Excel 模板下载失败')),
    onSuccess: (result) => download(result.blob, result.fileName, message),
  });
  const previewMutation = useApiMutation({
    mutationFn: (selectedFile: File) => {
      const controller = new AbortController();
      abortController.current = controller;
      return previewProjectManagementExcel(selectedFile, controller.signal);
    },
    onError: (error) => {
      if (error instanceof DOMException && error.name === 'AbortError') return;
      const text = getErrorMessage(error, 'Excel 预览失败');
      setFailure(text);
      setFailedAction('preview');
      message.error(text);
    },
    onSuccess: (result) => {
      if (!result.data) return;
      setPreview(result.data);
      setFailure(null);
      setFailedAction(null);
      message.success(result.data.status === 'Completed' ? 'Excel 解析完成' : 'Excel 解析完成，请处理错误行');
    },
    onSettled: () => { abortController.current = null; },
  });

  if (!canImport) return null;

  const startPreview = () => {
    if (!file) return;
    setFailure(null);
    setFailedAction(null);
    setPreview(null);
    previewMutation.mutate(file);
  };

  return (
    <section className="mt-4 rounded-lg border border-gray-200 p-4" aria-label="Excel 导入预览">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="font-semibold">Excel 模板与导入预览</h2>
          <p className="mt-1 text-sm text-gray-500">模板版本 v1.0，支持项目、任务和成员 Sheet。预览只读，不写入业务数据。</p>
        </div>
        <PermissionButton code="project-management:sync:import" disabled={templateMutation.isPending} onClick={() => templateMutation.mutate()}>
          {templateMutation.isPending ? '模板下载中…' : '下载 Excel 模板'}
        </PermissionButton>
      </div>
      <div className="mt-3 flex flex-wrap items-center gap-2">
        <input
          aria-label="选择 Excel 文件"
          accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          type="file"
          onChange={(event) => {
            setFile(event.target.files?.[0] ?? null);
            setPreview(null);
            setFailure(null);
            setFailedAction(null);
          }}
        />
        <PermissionButton code="project-management:sync:import" disabled={!file || previewMutation.isPending} onClick={startPreview}>
          {previewMutation.isPending ? '解析预览中…' : '上传并预览'}
        </PermissionButton>
        {previewMutation.isPending ? <button type="button" onClick={() => abortController.current?.abort()}>取消解析</button> : null}
      </div>
      {file ? <p className="mt-2 text-sm text-gray-500">已选择：{file.name}（{formatFileSize(file.size)}）。文件大小上限 10 MB，Sheet 上限 4 个，数据行上限 5000 行。</p> : null}
      {previewMutation.isPending ? <p className="mt-2 rounded bg-blue-50 p-3 text-sm text-blue-900" role="status">正在读取 Excel、校验引用和检测层级/依赖循环，可取消本次请求。</p> : null}
      {preview ? <PreviewResult preview={preview} /> : null}
      {failure ? <section className="mt-3 rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900" role="alert"><p>预览失败：{failure}</p><button className="mt-2" disabled={!file || previewMutation.isPending} type="button" onClick={failedAction === 'preview' ? startPreview : undefined}>重试预览</button></section> : null}
    </section>
  );
}

function PreviewResult({ preview }: { preview: ProjectManagementExcelImportPreview }) {
  const message = useMessage();
  const canDownloadErrors = preview.errors.length > 0;
  return (
    <section className={`mt-3 rounded border p-3 text-sm ${preview.errorRows ? 'border-amber-300 bg-amber-50 text-amber-900' : 'border-emerald-300 bg-emerald-50 text-emerald-900'}`} aria-live="polite">
      <div className="font-medium">预览 {preview.status === 'Completed' ? '通过' : '包含错误'} · 模板 v{preview.templateVersion} · {preview.previewId}</div>
      <div className="mt-2 grid gap-2 sm:grid-cols-4">
        <span>总行数 {preview.totalRows}</span><span>可导入 {preview.importableRows}</span><span>新增 {preview.newRows}</span><span>更新 {preview.updatedRows}</span>
        <span>重复 {preview.duplicateRows}</span><span>错误 {preview.errorRows}</span><span>警告 {preview.warningRows}</span><span>跳过 {preview.skippedRows}</span>
      </div>
      {preview.errorsTruncated ? <p className="mt-2">错误详情已达到显示上限，请拆分文件后重试。</p> : null}
      {canDownloadErrors ? <button className="mt-3 rounded border px-3 py-1" type="button" onClick={() => downloadErrors(preview, message)}>下载错误行 CSV</button> : null}
      {preview.errors.length ? <div className="mt-3 max-h-64 overflow-auto rounded border bg-white/70 p-2"><table className="w-full text-left"><thead><tr><th>Sheet</th><th>行</th><th>稳定 ID</th><th>错误</th></tr></thead><tbody>{preview.errors.slice(0, 50).map((error, index) => <tr key={`${error.sheetName}-${error.rowNumber}-${error.code}-${index}`}><td>{error.sheetName}</td><td>{error.rowNumber}</td><td>{error.stableId ?? '-'}</td><td>{error.message}</td></tr>)}</tbody></table></div> : null}
    </section>
  );
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

function downloadErrors(preview: ProjectManagementExcelImportPreview, message: { success: (content: string) => void }) {
  const header = ['SheetName', 'RowNumber', 'StableId', 'Code', 'Message', 'Severity'];
  const rows = preview.errors.map(error => [error.sheetName, String(error.rowNumber), error.stableId ?? '', error.code, error.message, error.severity]);
  const content = `\uFEFF${[header, ...rows].map(row => row.map(csvEscape).join(',')).join('\r\n')}\r\n`;
  download(new Blob([content], { type: 'text/csv;charset=utf-8' }), `project-management-excel-errors-${preview.previewId}.csv`, message);
}

function csvEscape(value: string): string {
  return `"${value.replaceAll('"', '""')}"`;
}
