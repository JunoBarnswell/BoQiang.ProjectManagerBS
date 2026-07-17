import { CheckCircle2, Loader2, ShieldAlert, Sparkles } from 'lucide-react';

import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import type { AiDataCenterAssistantToolIntentDto } from '../../../../features/ai-center/api/aiCenter.api';

interface AssistantIntentCardProps {
  completed?: boolean;
  executing?: boolean;
  intent: AiDataCenterAssistantToolIntentDto;
  onExecute: (intent: AiDataCenterAssistantToolIntentDto) => void;
}

export function AssistantIntentCard({ completed, executing, intent, onExecute }: AssistantIntentCardProps) {
  return (
    <div className="mt-2 overflow-hidden rounded-xl border border-primary-100 bg-white shadow-sm">
      <div className="flex items-start justify-between gap-2 border-b border-slate-100 bg-primary-50/60 px-3 py-2">
        <div className="min-w-0">
          <div className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
            {intent.requiresConfirmation ? <ShieldAlert className="h-4 w-4 text-amber-500" /> : <Sparkles className="h-4 w-4 text-primary-500" />}
            <span className="truncate">{intent.toolName}</span>
          </div>
          <p className="mt-1 break-words text-xs leading-5 text-slate-600">{intent.summary}</p>
        </div>
        <span className="shrink-0 rounded-full bg-white px-2 py-0.5 text-[11px] font-medium text-slate-600">{intent.riskLevel}</span>
      </div>
      <div className="space-y-2 px-3 py-2">
        <div className="rounded-lg bg-slate-50 px-2.5 py-2 text-[12px] leading-5 text-slate-600">
          <div className="font-medium text-slate-700">{translateCurrentLiteral("工具编码")}</div>
          <div className="mt-0.5 break-all font-mono">{intent.toolCode}</div>
        </div>
        <button
          className={intent.requiresConfirmation ? 'primary-button h-8 w-full text-xs' : 'secondary-button h-8 w-full text-xs'}
          disabled={executing || completed}
          type="button"
          onClick={() => onExecute(intent)}
        >
          {executing ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <CheckCircle2 className="h-3.5 w-3.5" />}
          {completed ? '已执行' : intent.requiresConfirmation ? '确认并执行' : '执行查询'}
        </button>
      </div>
    </div>
  );
}
