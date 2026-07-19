import { useRef, useState } from 'react';

import { confirmProjectManagementExcel, downloadProjectManagementExcelTemplate, previewProjectManagementExcel } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementExcelImportPreview } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { formatFileSize, saveBlob } from '../../../shared/file-preview/filePreviewUtils';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

type FailedAction = 'preview' | null;

export function ProjectManagementExcelImportPanel() {
  const { hasPermission: canImport } = usePermission('project-management:sync:import');
  const confirm = useConfirm();
  const message = useMessage();
  const fileInput = useRef<HTMLInputElement | null>(null);
  const abortController = useRef<AbortController | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProjectManagementExcelImportPreview | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [failedAction, setFailedAction] = useState<FailedAction>(null);
  const templateMutation = useApiMutation({
    mutationFn: () => downloadProjectManagementExcelTemplate(),
    onError: (error) => message.error(getErrorMessage(error, 'Excel 模板下载失败')),
    onSuccess: (result) => { saveBlob(result.blob, result.fileName); message.success(`已生成 ${result.fileName}`); },
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
      message.success(result.data.status === 'Completed' ? 'Excel 解析完成，可以确认导入' : 'Excel 解析完成，请先处理错误行');
    },
    onSettled: () => { abortController.current = null; },
  });
  const importMutation = useApiMutation({
    mutationFn: () => {
      if (!file || !preview) return Promise.reject(new Error('请先完成 Excel 预览'));
      return confirmProjectManagementExcel(file, { previewId: preview.previewId, idempotencyKey: crypto.randomUUID() });
    },
    onError: (error) => message.error(getErrorMessage(error, 'Excel 导入失败')),
    onSuccess: (result) => message.success(`导入完成：新增 ${result.data?.addedRows ?? 0} 行，更新 ${result.data?.updatedRows ?? 0} 行。`),
  });

  if (!canImport) return null;

  const selectFile = (selectedFile: File | null) => {
    setFile(selectedFile);
    setPreview(null);
    setFailure(null);
    setFailedAction(null);
  };
  const startPreview = () => { if (file) previewMutation.mutate(file); };
  const canConfirmImport = Boolean(file && preview?.status === 'Completed' && preview.errorRows === 0);

  return (
    <section className="mt-4 rounded-lg border border-gray-200 p-4" aria-label="Excel 导入预览">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div><h2 className="font-semibold">Excel 模板与导入预览</h2><p className="mt-1 text-sm text-gray-500">预览只读；确认导入后才写入项目、任务和成员数据。</p></div>
        <PermissionButton code="project-management:sync:import" disabled={templateMutation.isPending} onClick={() => templateMutation.mutate()}>{templateMutation.isPending ? '模板下载中…' : '下载 Excel 模板'}</PermissionButton>
      </div>
      <div
        className="mt-4 cursor-pointer rounded-lg border-2 border-dashed border-blue-200 bg-blue-50/40 p-6 text-center transition-colors hover:border-blue-400"
        role="button"
        tabIndex={0}
        onClick={() => fileInput.current?.click()}
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => { event.preventDefault(); selectFile(event.dataTransfer.files.item(0)); }}
        onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); fileInput.current?.click(); } }}
      >
        <strong>拖放 Excel 文件到此处，或点击选择文件</strong>
        <p className="mt-1 text-sm text-gray-500">支持 .xlsx，文件不超过 10 MB；最多 4 个 Sheet、5000 条数据行。</p>
        <input ref={fileInput} aria-label="选择 Excel 文件" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" className="sr-only" type="file" onChange={(event) => selectFile(event.target.files?.[0] ?? null)} />
      </div>
      {file ? <div className="mt-3 flex flex-wrap items-center justify-between gap-2 rounded border border-gray-200 bg-gray-50 p-3 text-sm"><span>已选择：<strong>{file.name}</strong>（{formatFileSize(file.size)}）</span><div className="flex gap-2"><button type="button" onClick={() => fileInput.current?.click()}>替换文件</button><PermissionButton code="project-management:sync:import" disabled={previewMutation.isPending} onClick={startPreview}>{previewMutation.isPending ? '解析预览中…' : '上传并预览'}</PermissionButton></div></div> : null}
      {previewMutation.isPending ? <div className="mt-3 rounded bg-blue-50 p-3 text-sm text-blue-900" role="status"><div className="h-1 overflow-hidden rounded bg-blue-100"><div className="h-full w-2/3 animate-pulse bg-blue-600" /></div><p className="mt-2">正在读取 Excel、校验引用并检测层级/依赖循环，可取消本次请求。</p><button className="mt-2 underline" type="button" onClick={() => abortController.current?.abort()}>取消解析</button></div> : null}
      {preview ? <PreviewResult preview={preview} canConfirmImport={canConfirmImport} importing={importMutation.isPending} onConfirm={() => confirm({ title: '确认导入 Excel', content: `将根据预览 ${preview.previewId} 写入 ${preview.importableRows} 条可导入数据。请确认文件未被替换。`, confirmText: '确认导入', onConfirm: () => importMutation.mutate() })} /> : null}
      {failure ? <section className="mt-3 rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900" role="alert"><p>预览失败：{failure}</p><button className="mt-2 underline" disabled={!file || previewMutation.isPending} type="button" onClick={failedAction === 'preview' ? startPreview : undefined}>重试预览</button></section> : null}
    </section>
  );
}

function PreviewResult({ preview, canConfirmImport, importing, onConfirm }: { preview: ProjectManagementExcelImportPreview; canConfirmImport: boolean; importing: boolean; onConfirm: () => void }) {
  const message = useMessage();
  const canDownloadErrors = preview.errors.length > 0;
  return (
    <section className={`mt-3 rounded border p-3 text-sm ${preview.errorRows ? 'border-amber-300 bg-amber-50 text-amber-900' : 'border-emerald-300 bg-emerald-50 text-emerald-900'}`} aria-live="polite">
      <div className="font-medium">预览 {preview.status === 'Completed' ? '通过' : '包含错误'} · 模板 v{preview.templateVersion}</div>
      <div className="mt-2 grid gap-2 sm:grid-cols-4"><span>总行数 {preview.totalRows}</span><span>可导入 {preview.importableRows}</span><span>新增 {preview.newRows}</span><span>更新 {preview.updatedRows}</span><span>重复 {preview.duplicateRows}</span><span>错误 {preview.errorRows}</span><span>警告 {preview.warningRows}</span><span>跳过 {preview.skippedRows}</span></div>
      {preview.errorsTruncated ? <p className="mt-2">错误详情已达到显示上限，请拆分文件后重试。</p> : null}
      <div className="mt-3 flex flex-wrap gap-2">
        {canDownloadErrors ? <button className="rounded border border-current px-3 py-1" type="button" onClick={() => downloadErrors(preview, message)}>下载错误行 CSV</button> : null}
        {canConfirmImport ? <PermissionButton code="project-management:sync:import" disabled={importing} onClick={onConfirm}>{importing ? '正在导入…' : '确认导入'}</PermissionButton> : <span className="text-sm">{preview.errorRows ? '修复错误后重新上传预览，才可确认导入。' : '预览未通过，暂不能导入。'}</span>}
      </div>
      {preview.errors.length ? <div className="mt-3 max-h-64 overflow-auto rounded border bg-white/70 p-2"><table className="w-full text-left"><thead><tr><th>级别</th><th>Sheet</th><th>行</th><th>稳定 ID</th><th>说明</th></tr></thead><tbody>{preview.errors.slice(0, 50).map((error, index) => <tr className={error.severity.toLowerCase() === 'warning' ? 'bg-amber-50 text-amber-900' : 'bg-red-50 text-red-900'} key={`${error.sheetName}-${error.rowNumber}-${error.code}-${index}`}><td>{error.severity.toLowerCase() === 'warning' ? '警告' : '错误'}</td><td>{error.sheetName}</td><td>{error.rowNumber}</td><td>{error.stableId ?? '-'}</td><td>{error.message}</td></tr>)}</tbody></table></div> : null}
    </section>
  );
}

function downloadErrors(preview: ProjectManagementExcelImportPreview, message: { success: (content: string) => void }) {
  const header = ['SheetName', 'RowNumber', 'StableId', 'Code', 'Message', 'Severity'];
  const rows = preview.errors.map((error) => [error.sheetName, String(error.rowNumber), error.stableId ?? '', error.code, error.message, error.severity]);
  const content = `\uFEFF${[header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\r\n')}\r\n`;
  const fileName = `project-management-excel-errors-${preview.previewId}.csv`;
  saveBlob(new Blob([content], { type: 'text/csv;charset=utf-8' }), fileName);
  message.success(`已生成 ${fileName}`);
}

function csvEscape(value: string): string { return `"${value.replaceAll('"', '""')}"`; }
