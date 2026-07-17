import { CheckCircle2, Loader2, Send, ShieldAlert, Sparkles, WandSparkles } from 'lucide-react';
import { type ReactNode, useState } from 'react';

import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { WorkbenchJsonVisualViewer } from '../../../pages/application-console/data-center/workbench/components/WorkbenchJsonVisualViewer';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { aiChatApi, type AiToolInvokeResponse } from '../api/aiCenter.api';

export interface AiToolConversationCommand {
  arguments: Record<string, unknown>;
  confirmedRiskAccepted?: boolean;
  requiresConfirmation?: boolean;
  summary: string;
  toolCode: string;
}

interface AiToolConversationPanelProps {
  argumentsBuilder?: () => Record<string, unknown>;
  confirmed?: boolean;
  contextLabel?: string;
  disabled?: boolean;
  emptyPrompt?: string;
  intro?: ReactNode;
  placeholder?: string;
  quickPrompts?: string[];
  resolveCommand?: (message: string) => AiToolConversationCommand | null;
  variant?: 'card' | 'assistant';
  toolCode?: string;
  onError?: (message: string) => void;
  onSuccess?: (response: AiToolInvokeResponse) => void;
}

interface ConversationMessage {
  content: string;
  id: string;
  role: 'assistant' | 'result' | 'user';
}

export function AiToolConversationPanel({
  argumentsBuilder,
  confirmed = false,
  contextLabel,
  disabled,
  emptyPrompt = '描述你想执行的数据操作，我会转换为已授权工具并在确认后执行。',
  intro,
  placeholder = '例如：创建表 ai_orders 字段 id TEXT 主键, name TEXT',
  quickPrompts = [],
  resolveCommand,
  variant = 'card',
  toolCode,
  onError,
  onSuccess
}: AiToolConversationPanelProps) {
  const [loading, setLoading] = useState(false);
  const [draft, setDraft] = useState('');
  const [deepThinking, setDeepThinking] = useState(false);
  const [confirmedRisk, setConfirmedRisk] = useState(confirmed);
  const [pendingCommand, setPendingCommand] = useState<AiToolConversationCommand | null>(null);
  const [result, setResult] = useState<AiToolInvokeResponse | null>(null);
  const [messages, setMessages] = useState<ConversationMessage[]>([
    { content: emptyPrompt, id: 'welcome', role: 'assistant' }
  ]);
  const visibleMessages = variant === 'assistant' && messages.length === 1 && messages[0]?.id === 'welcome' ? [] : messages;

  if (variant === 'assistant') {
    return (
      <div className="flex min-h-0 flex-1 flex-col">
        <div className="min-h-0 flex-1 space-y-2.5 overflow-y-auto px-4 py-3">
          {intro}

          {quickPrompts.length > 0 ? (
            <div className="grid grid-cols-2 gap-1.5">
              {quickPrompts.map((prompt) => (
                <button
                  className="min-w-0 truncate rounded-lg border border-slate-200 bg-white/85 px-2.5 py-1.5 text-left text-[12px] leading-5 text-slate-600 shadow-sm transition hover:border-primary-200 hover:bg-primary-50 hover:text-primary-700 disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={disabled || loading}
                  key={prompt}
                  title={prompt}
                  type="button"
                  onClick={() => setDraft(prompt)}
                >
                  <span className="block truncate">{prompt}</span>
                </button>
              ))}
            </div>
          ) : null}

          {visibleMessages.length > 0 ? (
            <div className="space-y-2">
              {visibleMessages.map((message) => (
                <div className={getAssistantMessageClass(message.role)} key={message.id}>
                  {message.content}
                </div>
              ))}
            </div>
          ) : null}

          {pendingCommand?.requiresConfirmation ? renderRiskConfirm() : null}
          {result ? <WorkbenchJsonVisualViewer data={readResult(result)} title={result.resultSummary || '工具执行结果'} /> : null}
        </div>

        <div className="shrink-0 border-t border-white/70 bg-white/75 px-4 pb-3 pt-2.5 backdrop-blur">
          <div className="rounded-xl border border-primary-200 bg-white shadow-[0_14px_32px_rgba(37,99,235,0.12)] transition focus-within:border-primary-400 focus-within:ring-4 focus-within:ring-primary-100">
            <textarea
              className="min-h-16 w-full resize-none rounded-xl border-0 bg-transparent px-3 py-2.5 text-sm leading-6 text-slate-800 outline-none placeholder:text-slate-400 disabled:cursor-not-allowed disabled:bg-transparent disabled:text-slate-400"
              disabled={disabled || loading}
              placeholder={placeholder}
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault();
                  void submitMessage();
                }
              }}
            />
            <div className="flex items-center justify-between gap-2 px-3 pb-2.5">
              <button
                className={[
                  'inline-flex h-8 items-center gap-1.5 rounded-lg border px-3 text-xs font-medium transition',
                  deepThinking ? 'border-primary-200 bg-primary-50 text-primary-700' : 'border-slate-200 bg-white text-slate-700 hover:border-primary-200 hover:text-primary-700'
                ].join(' ')}
                type="button"
                onClick={() => setDeepThinking((value) => !value)}
              >
                <Sparkles className="h-3.5 w-3.5" />{translateCurrentLiteral("深度思考")}</button>
              <button className="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-primary-500 text-white shadow-sm transition hover:bg-primary-600 disabled:cursor-not-allowed disabled:bg-primary-200" disabled={disabled || loading || !draft.trim()} title={translateCurrentLiteral("发送")} type="button" onClick={() => void submitMessage()}>
                {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
              </button>
            </div>
          </div>
          <div className="mt-2 text-center text-[11px] leading-5 text-slate-400">{translateCurrentLiteral("内容由 AI 生成，仅供参考。写入数据库前会要求人工确认。")}</div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
      <div className="flex items-start gap-2 rounded-lg border border-primary-100 bg-white px-3 py-2">
        <WandSparkles className="mt-0.5 h-4 w-4 text-primary-600" />
        <div className="min-w-0">
          <div className="text-sm font-semibold text-slate-900">{translateCurrentLiteral("AI 工具对话")}</div>
          {contextLabel ? <div className="mt-1 truncate text-xs text-slate-500">{contextLabel}</div> : null}
        </div>
      </div>

      {quickPrompts.length > 0 ? (
        <div className="flex flex-wrap gap-2">
          {quickPrompts.map((prompt) => (
            <button className="secondary-button h-8 max-w-full truncate text-xs" disabled={disabled || loading} key={prompt} type="button" onClick={() => setDraft(prompt)}>
              {prompt}
            </button>
          ))}
        </div>
      ) : null}

      <div className="max-h-72 space-y-2 overflow-y-auto rounded-lg border border-slate-200 bg-white p-3">
        {messages.map((message) => (
          <div className={message.role === 'user' ? 'ml-8 rounded-lg bg-primary-600 px-3 py-2 text-sm text-white' : message.role === 'result' ? 'rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900' : 'mr-8 rounded-lg bg-slate-100 px-3 py-2 text-sm text-slate-700'} key={message.id}>
            {message.content}
          </div>
        ))}
      </div>

      {pendingCommand?.requiresConfirmation ? renderRiskConfirm() : null}

      <div className="flex gap-2">
        <textarea
          className="form-input min-h-20 flex-1"
          disabled={disabled || loading}
          placeholder={placeholder}
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
              event.preventDefault();
              void submitMessage();
            }
          }}
        />
        <button className="primary-button h-20 w-12 px-0" disabled={disabled || loading || !draft.trim()} title={translateCurrentLiteral("发送")} type="button" onClick={() => void submitMessage()}>
          {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
        </button>
      </div>

      {result ? <WorkbenchJsonVisualViewer data={readResult(result)} title={result.resultSummary || '工具执行结果'} /> : null}
    </div>
  );

  function renderRiskConfirm() {
    if (!pendingCommand?.requiresConfirmation) {
      return null;
    }

    return (
      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
        <div className="flex items-start gap-2">
          <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
          <div>
            <div className="font-semibold">{translateCurrentLiteral("需要确认高风险操作")}</div>
            <div className="mt-1 text-amber-800">{pendingCommand.summary}</div>
          </div>
        </div>
        <label className="mt-3 flex items-center gap-2 text-xs font-medium">
          <input checked={confirmedRisk} type="checkbox" onChange={(event) => setConfirmedRisk(event.target.checked)} />{translateCurrentLiteral("我确认该操作会写入数据库或生成配置对象，允许执行。")}</label>
        <button className="primary-button mt-3 w-full" disabled={loading || !confirmedRisk} type="button" onClick={() => invoke(pendingCommand)}>
          {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
          {translateCurrentLiteral("确认并执行")}
        </button>
      </div>
    );
  }

  async function submitMessage() {
    const content = draft.trim();
    if (!content) {
      return;
    }

    setDraft('');
    setMessages((current) => [...current, { content, id: crypto.randomUUID(), role: 'user' }]);
    const command = resolveCommand?.(content) ?? buildDirectCommand(content);
    if (!command) {
      setMessages((current) => [...current, { content: '没有识别到可执行的数据中心工具。可以说：查询表清单、查询当前表数据、创建表 表名 字段 id TEXT 主键, name TEXT。', id: crypto.randomUUID(), role: 'assistant' }]);
      return;
    }

    setMessages((current) => [...current, { content: `已识别：${command.summary}`, id: crypto.randomUUID(), role: 'assistant' }]);
    if (command.requiresConfirmation && !command.confirmedRiskAccepted && !confirmedRisk) {
      setPendingCommand(command);
      return;
    }

    await invoke(command);
  }

  function buildDirectCommand(summary: string): AiToolConversationCommand | null {
    if (!toolCode || !argumentsBuilder) {
      return null;
    }

    return {
      arguments: argumentsBuilder(),
      confirmedRiskAccepted: confirmed || confirmedRisk,
      requiresConfirmation: confirmed,
      summary,
      toolCode
    };
  }

  async function invoke(command: AiToolConversationCommand) {
    setLoading(true);
    try {
      const response = await aiChatApi.tools.invoke(command.toolCode, {
        arguments: command.arguments,
        confirmedRiskAccepted: command.confirmedRiskAccepted || confirmedRisk,
        workMode: 'Agent'
      });
      setResult(response.data);
      setPendingCommand(null);
      setConfirmedRisk(false);
      setMessages((current) => [...current, { content: response.data.resultSummary || '工具执行完成。', id: crypto.randomUUID(), role: 'result' }]);
      onSuccess?.(response.data);
    } catch (error) {
      const message = getErrorMessage(error, 'AI 工具执行失败');
      setMessages((current) => [...current, { content: message, id: crypto.randomUUID(), role: 'assistant' }]);
      onError?.(message);
    } finally {
      setLoading(false);
    }
  }
}

function readResult(response: AiToolInvokeResponse) {
  try {
    return response.content ? JSON.parse(response.content) as unknown : response;
  } catch {
    return response;
  }
}

function getAssistantMessageClass(role: ConversationMessage['role']) {
  const base = 'w-fit max-w-[88%] whitespace-pre-wrap break-words rounded-xl px-3 py-2 text-[13px] leading-5 shadow-sm';
  if (role === 'user') {
    return `${base} ml-auto bg-primary-600 text-white`;
  }

  if (role === 'result') {
    return `${base} max-w-[94%] border border-emerald-200 bg-emerald-50/95 text-emerald-900`;
  }

  return `${base} mr-auto bg-white/90 text-slate-700 ring-1 ring-slate-100`;
}
