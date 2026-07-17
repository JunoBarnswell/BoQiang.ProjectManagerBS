import { History, PanelRightClose, Plus, Settings, X } from 'lucide-react';
import type { ReactNode } from 'react';

import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';

interface AssistantConversationHeaderProps {
  children: ReactNode;
  dataSourceName?: string;
  historyOpen: boolean;
  settingsOpen: boolean;
  onClose?: () => void;
  onNewConversation: () => void;
  onToggleSettings: () => void;
  onToggleHistory: () => void;
}

export function AssistantConversationHeader({
  children,
  dataSourceName,
  historyOpen,
  settingsOpen,
  onClose,
  onNewConversation,
  onToggleSettings,
  onToggleHistory
}: AssistantConversationHeaderProps) {
  return (
    <>
      <header className="flex h-10 shrink-0 items-center justify-between border-b border-slate-200 bg-slate-100/95 px-2">
        <div className="flex h-full items-end">
          <div className="flex h-9 items-center gap-2 rounded-t-lg bg-white px-3 text-sm font-medium text-slate-800 shadow-sm">{translateCurrentLiteral("AI 助理")}<button className="rounded p-0.5 text-slate-400 transition hover:bg-slate-100 hover:text-slate-700" type="button" onClick={onClose} aria-label="关闭 AI 助理页签">
              <X className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>
        <button className="icon-button h-8 w-8 text-slate-500 hover:text-slate-900" type="button" onClick={onClose} aria-label="收起 AI 助理">
          <PanelRightClose className="h-4 w-4" />
        </button>
      </header>

      <div className="shrink-0 border-b border-slate-100 bg-white px-3 py-2">
        <div className="flex items-center justify-between gap-2">
          <div className="min-w-0">
            <h2 className="truncate text-sm font-semibold text-slate-950">{translateCurrentLiteral("数据中心 AI 助理")}</h2>
            <p className="truncate text-[11px] leading-4 text-slate-500">{dataSourceName ? `当前库：${dataSourceName}` : '围绕当前数据库执行操作'}</p>
          </div>
          <div className="flex shrink-0 items-center gap-1">
            <AssistantIconButton label="新会话" onClick={onNewConversation}><Plus className="h-4 w-4" /></AssistantIconButton>
            <AssistantIconButton active={historyOpen} label="历史会话" onClick={onToggleHistory}><History className="h-4 w-4" /></AssistantIconButton>
            <AssistantIconButton active={settingsOpen} label="助手设置" onClick={onToggleSettings}><Settings className="h-4 w-4" /></AssistantIconButton>
          </div>
        </div>
        <div className="mt-2 flex min-w-0 items-center gap-2">{children}</div>
      </div>
    </>
  );
}

function AssistantIconButton({ active, children, label, onClick }: { active?: boolean; children: ReactNode; label: string; onClick: () => void }) {
  return (
    <button
      className={[
        'inline-flex h-8 w-8 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100 hover:text-slate-900',
        active ? 'bg-primary-50 text-primary-700' : ''
      ].join(' ')}
      aria-label={label}
      title={label}
      type="button"
      onClick={onClick}
    >
      {children}
    </button>
  );
}
