import { Bot, DatabaseZap, RefreshCw, Settings, SlidersHorizontal } from 'lucide-react';

import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';

interface AssistantSettingsPanelProps {
  autoExecuteReadOnly: boolean;
  canManageModels: boolean;
  canOpenAiSettings: boolean;
  canOpenWorkbench: boolean;
  loading?: boolean;
  modelCount: number;
  selectedModelName?: string;
  toolCount: number;
  onClearConversation: () => void;
  onManageModels: () => void;
  onOpenAiSettings: () => void;
  onOpenWorkbench: () => void;
  onRefresh: () => void;
  onToggleAutoExecuteReadOnly: (value: boolean) => void;
}

export function AssistantSettingsPanel({
  autoExecuteReadOnly,
  canManageModels,
  canOpenAiSettings,
  canOpenWorkbench,
  loading,
  modelCount,
  selectedModelName,
  toolCount,
  onClearConversation,
  onManageModels,
  onOpenAiSettings,
  onOpenWorkbench,
  onRefresh,
  onToggleAutoExecuteReadOnly
}: AssistantSettingsPanelProps) {
  return (
    <section className="shrink-0 border-b border-slate-200 bg-slate-50 px-3 py-3">
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center justify-between gap-2 border-b border-slate-100 px-3 py-2">
          <div className="min-w-0">
            <div className="text-sm font-semibold text-slate-900">{translateCurrentLiteral("助手设置")}</div>
            <div className="mt-0.5 truncate text-[11px] text-slate-500">
              {selectedModelName ? `当前模型：${selectedModelName}` : '当前未选择可用模型'}
            </div>
          </div>
          <button className="icon-button h-8 w-8" disabled={loading} title={translateCurrentLiteral("刷新模型和工具")} type="button" onClick={onRefresh}>
            <RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
          </button>
        </div>

        <div className="grid grid-cols-2 gap-2 px-3 py-3">
          <button
            className="secondary-button h-8 justify-start text-xs disabled:cursor-not-allowed disabled:opacity-55"
            disabled={!canManageModels}
            title={canManageModels ? '进入模型配置' : '缺少权限：ai:model:view'}
            type="button"
            onClick={onManageModels}
          >
            <DatabaseZap className="h-3.5 w-3.5" />{translateCurrentLiteral("模型维护")}</button>
          <button
            className="secondary-button h-8 justify-start text-xs disabled:cursor-not-allowed disabled:opacity-55"
            disabled={!canOpenAiSettings}
            title={canOpenAiSettings ? '进入 AI 设置' : '缺少权限：ai:settings:view'}
            type="button"
            onClick={onOpenAiSettings}
          >
            <Settings className="h-3.5 w-3.5" />{translateCurrentLiteral("全局设置")}</button>
          <button
            className="secondary-button h-8 justify-start text-xs disabled:cursor-not-allowed disabled:opacity-55"
            disabled={!canOpenWorkbench}
            title={canOpenWorkbench ? '进入 AI 工作台' : '缺少权限：ai:workbench:view'}
            type="button"
            onClick={onOpenWorkbench}
          >
            <Bot className="h-3.5 w-3.5" />{translateCurrentLiteral("AI 工作台")}</button>
          <button className="secondary-button h-8 justify-start text-xs" type="button" onClick={onClearConversation}>
            <SlidersHorizontal className="h-3.5 w-3.5" />{translateCurrentLiteral("重置会话")}</button>
        </div>

        <label className="flex items-start justify-between gap-3 border-t border-slate-100 px-3 py-2.5">
          <span className="min-w-0">
            <span className="block text-xs font-medium text-slate-800">{translateCurrentLiteral("自动执行只读意图")}</span>
            <span className="mt-0.5 block text-[11px] leading-4 text-slate-500">{translateCurrentLiteral("查询表清单、预览 SQL 等低风险工具可自动执行；写入类操作仍必须确认。")}</span>
          </span>
          <input
            checked={autoExecuteReadOnly}
            className="mt-0.5 h-4 w-4 shrink-0 accent-primary-600"
            type="checkbox"
            onChange={(event) => onToggleAutoExecuteReadOnly(event.target.checked)}
          />
        </label>

        <div className="flex items-center gap-2 border-t border-slate-100 px-3 py-2 text-[11px] text-slate-500">
          <span className="rounded-full bg-slate-100 px-2 py-0.5">{modelCount} 个启用模型</span>
          <span className="rounded-full bg-slate-100 px-2 py-0.5">{toolCount} 个数据中心工具</span>
        </div>
        {!canManageModels ? (
          <div className="border-t border-amber-100 bg-amber-50 px-3 py-2 text-[11px] leading-5 text-amber-800">{translateCurrentLiteral("当前账号缺少模型维护权限 ai:model:view。请在角色权限中授予“模型配置/查看”，或者让管理员配置并启用默认模型。")}</div>
        ) : null}
      </div>
    </section>
  );
}
