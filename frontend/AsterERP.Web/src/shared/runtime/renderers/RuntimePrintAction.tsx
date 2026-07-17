import { Printer } from 'lucide-react';

import { usePrintLauncher } from '../../../features/print-center/hooks/usePrintLauncher';
import type { PrintScene } from '../../../features/print-center/types';
import { PageError } from '../../status/PageError';

interface RuntimePrintActionProps {
  buttonText?: string | null;
  detailId?: string | null;
  menuCode?: string | null;
  pageSize?: number | null;
  scene?: PrintScene | string | null;
  title?: string | null;
}

export function RuntimePrintAction({ buttonText, detailId, menuCode, pageSize, scene, title }: RuntimePrintActionProps) {
  const printLauncher = usePrintLauncher();
  const normalizedMenuCode = String(menuCode ?? '').trim();
  const normalizedScene = scene === 'detail' ? 'detail' : 'list';

  if (!normalizedMenuCode) {
    return <PageError description="打印组件缺少 menuCode，请先发布页面菜单或在设计器中指定打印目标。" />;
  }

  return (
    <div className="flex flex-col gap-2 rounded-md border border-slate-200 bg-slate-50/70 p-3 md:flex-row md:items-center md:justify-between">
      <div className="min-w-0">
        <div className="text-sm font-semibold text-slate-950">{title || '打印动作'}</div>
        <div className="mt-0.5 text-xs text-slate-500">目标 {normalizedMenuCode} · 场景 {normalizedScene === 'detail' ? '详情' : '列表'}</div>
      </div>
      <button
        className="primary-button h-8 text-xs"
        type="button"
        onClick={() => printLauncher.open({
          conditions: [],
          detailId: detailId || null,
          menuCode: normalizedMenuCode,
          pageIndex: 1,
          pageSize: Math.max(1, Number(pageSize ?? 20)),
          scene: normalizedScene,
          selectedIds: [],
          sorts: []
        })}
      >
        <Printer className="h-3.5 w-3.5" />
        {buttonText || '打印'}
      </button>
      {printLauncher.dialog}
    </div>
  );
}
