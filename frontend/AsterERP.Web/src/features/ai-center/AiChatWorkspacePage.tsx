import { useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  Bot,
  ChevronLeft,
  ChevronRight,
  CircleStop,
  ClipboardList,
  RefreshCw,
  Send,
  Settings,
  Star,
  Trash2
} from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';

import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useViewportSize } from '../../core/responsive/useViewportSize';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { AiStatusBadge } from '../../shared/components/ai-chat/AiStatusBadge';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import {
  aiChatApi,
  createAiStreamEventError,
  type AiConversationDto,
  type AiStreamEventDto,
  type AiTaskPlanDto,
  type AiTaskPlanItemDto
} from './api/aiCenter.api';
import { useAiChatStore } from './state/useAiChatStore';
import { AgentToolCapabilitySelector } from './workflow/AgentToolCapabilitySelector';
import { WorkflowToolInspectorSection } from './workflow/WorkflowToolInspectorSection';
import { defaultSettings, type AiMessageDraft, type StreamSettings } from './workspace/aiChatWorkspaceTypes';
import { applyModelDefaults, buildLocalMessage, buildStreamRequest, formatTime, readNumber, readText, resolveSelectedToolCodes } from './workspace/aiChatWorkspaceUtils';
import { AiMessageBubble } from './workspace/AiMessageBubble';
import { LabeledSelect } from './workspace/LabeledSelect';
import { ModeSegment } from './workspace/ModeSegment';
import { TaskPlanPanel } from './workspace/TaskPlanPanel';
import './styles/ai-center.css';

export function AiChatWorkspacePage() {
  const { translate } = useI18n();
  const queryClient = useQueryClient();
  const message = useMessage();
  const confirm = useConfirm();
  const messageListRef = useRef<HTMLDivElement | null>(null);
  const inspectorAutoCollapsedRef = useRef(false);
  const { width: viewportWidth } = useViewportSize();
  const {
    activeConversationId,
    draftByConversation,
    expandedTaskIds,
    rightPanelOpen,
    streamController,
    streamingRunId,
    workMode,
    setActiveConversationId,
    setDraft,
    setRightPanelOpen,
    setStreamController,
    setStreamingRunId,
    setWorkMode,
    toggleTaskExpanded
  } = useAiChatStore();

  const [keyword, setKeyword] = useState('');
  const [localMessages, setLocalMessages] = useState<AiMessageDraft[]>([]);
  const [settings, setSettings] = useState<StreamSettings>(defaultSettings);
  const [eventTrail, setEventTrail] = useState<AiStreamEventDto[]>([]);
  const [editingTaskId, setEditingTaskId] = useState<string | null>(null);
  const [editingTaskDraft, setEditingTaskDraft] = useState({ description: '', title: '' });
  const [taskActionDrafts, setTaskActionDrafts] = useState<Record<string, string>>({});

  useEffect(() => {
    if (viewportWidth <= 0) {
      return;
    }

    if (viewportWidth <= 1360 && !inspectorAutoCollapsedRef.current) {
      setRightPanelOpen(false);
      inspectorAutoCollapsedRef.current = true;
      return;
    }

    if (viewportWidth > 1360) {
      inspectorAutoCollapsedRef.current = false;
    }
  }, [setRightPanelOpen, viewportWidth]);

  const conversationsQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: ['ai', 'conversations', keyword],
    queryFn: ({ signal }) =>
      aiChatApi.conversations.list(
        {
          keyword,
          pageIndex: 1,
          pageSize: 50,
          status: 'Active'
        },
        signal
      )
  });

  const messagesQuery = useApiQuery({
    enabled: Boolean(activeConversationId),
    keepPreviousData: true,
    queryKey: ['ai', 'messages', activeConversationId],
    queryFn: ({ signal }) =>
      aiChatApi.conversations.messages(activeConversationId ?? '', {
        pageIndex: 1,
        pageSize: 200
      }, signal)
  });

  const taskPlansQuery = useApiQuery({
    enabled: Boolean(activeConversationId),
    queryKey: ['ai', 'task-plans', activeConversationId],
    queryFn: ({ signal }) => aiChatApi.taskPlans.list(activeConversationId ?? '', signal)
  });

  const modelOptionsQuery = useApiQuery({
    queryKey: ['ai', 'models', 'options'],
    queryFn: ({ signal }) => aiChatApi.models.options(signal)
  });

  const promptOptionsQuery = useApiQuery({
    queryKey: ['ai', 'prompts', 'options'],
    queryFn: ({ signal }) => aiChatApi.prompts.options(signal)
  });

  const toolsQuery = useApiQuery({
    queryKey: ['ai', 'tools'],
    queryFn: ({ signal }) => aiChatApi.tools.list(signal)
  });

  const workflowOverviewQuery = useApiQuery({
    enabled: Boolean(activeConversationId),
    keepPreviousData: true,
    queryKey: ['ai', 'workflow-overview', activeConversationId],
    queryFn: ({ signal }) => aiChatApi.workflow.overview(activeConversationId ?? '', signal)
  });

  const conversations = useMemo(() => conversationsQuery.data?.data.items ?? [], [conversationsQuery.data?.data.items]);
  const models = useMemo(() => modelOptionsQuery.data?.data ?? [], [modelOptionsQuery.data?.data]);
  const prompts = useMemo(() => promptOptionsQuery.data?.data ?? [], [promptOptionsQuery.data?.data]);
  const selectedModel = models.find((item) => item.id === settings.modelConfigId);
  const currentDraft = draftByConversation[activeConversationId ?? 'new'] ?? '';
  const activeConversation = conversations.find((item) => item.id === activeConversationId);
  const taskPlans = useMemo(() => taskPlansQuery.data?.data ?? [], [taskPlansQuery.data?.data]);
  const workflowOverview = workflowOverviewQuery.data?.data ?? null;
  const aiTools = useMemo(() => toolsQuery.data?.data ?? [], [toolsQuery.data?.data]);
  const selectedToolCodes = useMemo(
    () => resolveSelectedToolCodes(settings.enabledToolDomains, settings.enabledToolCodes, aiTools),
    [aiTools, settings.enabledToolCodes, settings.enabledToolDomains]
  );
  const activeTaskPlan = useMemo(() => taskPlans.find((item) => !['Archived', 'Cancelled'].includes(item.status)) ?? null, [taskPlans]);
  const approvedTaskPlan = useMemo(
    () => taskPlans.find((item) => item.status === 'Approved' || item.status === 'PartialCompleted') ?? null,
    [taskPlans]
  );
  const isStreaming = Boolean(streamController);
  const lastLocalMessage = localMessages.at(-1);
  const lastLocalMessageContent = lastLocalMessage?.content;
  const lastLocalMessageReasoningContent = lastLocalMessage?.reasoningContent;

  useEffect(() => {
    if (conversations.length === 0) {
      return;
    }

    if (!activeConversationId || !conversations.some((item) => item.id === activeConversationId)) {
      setActiveConversationId(conversations[0].id);
    }
  }, [activeConversationId, conversations, setActiveConversationId]);

  useEffect(() => {
    const firstModel = models[0];
    if (firstModel && !settings.modelConfigId) {
      setSettings((current) => ({
        ...current,
        modelConfigId: firstModel.id
      }));
    }
  }, [models, settings.modelConfigId]);

  useEffect(() => {
    if (!messagesQuery.data || isStreaming) {
      return;
    }

    setLocalMessages([...messagesQuery.data.data.items].sort((left, right) => left.seq - right.seq));
  }, [isStreaming, messagesQuery.data]);

  useEffect(() => {
    messageListRef.current?.scrollTo({
      behavior: 'smooth',
      top: messageListRef.current.scrollHeight
    });
  }, [lastLocalMessageContent, lastLocalMessageReasoningContent, localMessages.length]);

  const usageText = useMemo(() => {
    const usage = [...eventTrail].reverse().find((item) => item.event === 'usage')?.data;
    if (!usage || typeof usage !== 'object') {
      return translate('ai.chat.usage.waiting');
    }

    const total = readNumber(usage, 'totalTokens');
    const reasoning = readNumber(usage, 'reasoningTokens');
    return formatMessage(translate('ai.chat.usage.summary'), { reasoning, total });
  }, [eventTrail, translate]);

  const handleCreateConversation = async () => {
    const response = await aiChatApi.conversations.create({
      agentProfileIds: [],
      modelConfigId: settings.modelConfigId || null,
      promptTemplateId: settings.promptTemplateId || null,
      title: translate('ai.chat.conversation.newTitle')
    });
    setActiveConversationId(response.data.id);
    await queryClient.invalidateQueries({ queryKey: ['ai', 'conversations'] });
  };

  const handleArchiveConversation = (conversation: AiConversationDto) => {
    confirm({
      title: translate('ai.chat.confirm.archiveTitle'),
      content: formatMessage(translate('ai.chat.confirm.archiveContent'), { title: conversation.title }),
      onConfirm: async () => {
        await aiChatApi.conversations.updateStatus(conversation.id, 'Archived');
        if (activeConversationId === conversation.id) {
          setActiveConversationId(null);
        }
        await queryClient.invalidateQueries({ queryKey: ['ai', 'conversations'] });
      }
    });
  };

  const handleDeleteConversation = (conversation: AiConversationDto) => {
    confirm({
      title: translate('ai.chat.confirm.deleteTitle'),
      content: formatMessage(translate('ai.chat.confirm.deleteContent'), { title: conversation.title }),
      onConfirm: async () => {
        await aiChatApi.conversations.delete(conversation.id);
        if (activeConversationId === conversation.id) {
          setActiveConversationId(null);
        }
        await queryClient.invalidateQueries({ queryKey: ['ai', 'conversations'] });
      }
    });
  };

  const handleFavoriteConversation = async (conversation: AiConversationDto) => {
    await aiChatApi.conversations.update(conversation.id, {
      isFavorite: !conversation.isFavorite,
      title: conversation.title
    });
    await queryClient.invalidateQueries({ queryKey: ['ai', 'conversations'] });
  };

  const handleStop = async () => {
    streamController?.abort();
    if (streamingRunId) {
      await aiChatApi.stopRun(streamingRunId);
    }
    setStreamController(null);
    setStreamingRunId(null);
  };

  const handleSend = async () => {
    const content = currentDraft.trim();
    if (!content) {
      message.error(translate('ai.chat.error.emptyMessage'));
      return;
    }

    if (!settings.modelConfigId) {
      message.error(translate('ai.chat.error.selectModel'));
      return;
    }

    if (workMode === 'Agent' && !approvedTaskPlan) {
      message.error(translate('ai.chat.error.agentNeedsPlan'));
      return;
    }

    if (workMode === 'Agent' && selectedToolCodes.length === 0) {
      message.error(translate('ai.chat.error.agentNeedsTool'));
      return;
    }

    try {
      let conversationId = activeConversationId;
      if (!conversationId) {
        const createResponse = await aiChatApi.conversations.create({
          agentProfileIds: [],
          modelConfigId: settings.modelConfigId,
          promptTemplateId: settings.promptTemplateId || null,
          title: content.slice(0, 32)
        });
        conversationId = createResponse.data.id;
        setActiveConversationId(conversationId);
        await queryClient.invalidateQueries({ queryKey: ['ai', 'conversations'] });
      }

      const controller = new AbortController();
      const userMessage = buildLocalMessage(conversationId, 'user', content);
      const assistantMessage = buildLocalMessage(conversationId, 'assistant', '', true);
      setLocalMessages((current) => [...current, userMessage, assistantMessage]);
      setDraft(conversationId, '');
      setDraft('new', '');
      setEventTrail([]);
      setStreamController(controller);

      const request = buildStreamRequest(content, settings, workMode, approvedTaskPlan?.id ?? activeTaskPlan?.id ?? null, selectedToolCodes);
      await aiChatApi.stream(conversationId, {
        onEvent: (event) => handleStreamEvent(event, assistantMessage.id),
        request,
        signal: controller.signal
      });
    } catch (error) {
      if (!(error instanceof DOMException && error.name === 'AbortError')) {
        message.error(getErrorMessage(error, translate('ai.chat.error.requestFailed')));
      }
    } finally {
      setStreamController(null);
      setStreamingRunId(null);
      await queryClient.invalidateQueries({ queryKey: ['ai', 'messages'] });
      await queryClient.invalidateQueries({ queryKey: ['ai', 'conversations'] });
      await queryClient.invalidateQueries({ queryKey: ['ai', 'usage'] });
      await queryClient.invalidateQueries({ queryKey: ['ai', 'logs'] });
      await queryClient.invalidateQueries({ queryKey: ['ai', 'task-plans'] });
      await queryClient.invalidateQueries({ queryKey: ['ai', 'workflow-overview'] });
    }
  };

  const handleToolDomainToggle = (domain: string, selected: boolean) => {
    setSettings((current) => {
      const domains = new Set(current.enabledToolDomains);
      if (selected) {
        domains.add(domain);
      } else {
        domains.delete(domain);
      }

      return {
        ...current,
        enabledToolDomains: Array.from(domains)
      };
    });
  };

  const handleToolCodesChange = (toolCodes: string[]) => {
    setSettings((current) => ({
      ...current,
      enabledToolCodes: toolCodes
    }));
  };

  const handleStreamEvent = (event: AiStreamEventDto, assistantMessageId: string) => {
    setEventTrail((current) => [...current.slice(-24), event]);
    if (event.event === 'run_started') {
      setStreamingRunId(event.runId);
      return;
    }

    if (event.event === 'reasoning_delta') {
      updateAssistantMessage(assistantMessageId, (item) => ({
        ...item,
        reasoningContent: `${item.reasoningContent ?? ''}${readText(event.data, 'delta')}`,
        runId: event.runId,
        status: 'Running'
      }));
      return;
    }

    if (event.event === 'content_delta') {
      updateAssistantMessage(assistantMessageId, (item) => ({
        ...item,
        content: `${item.content}${readText(event.data, 'delta') || readText(event.data, 'content')}`,
        runId: event.runId,
        status: 'Running'
      }));
      return;
    }

    if (event.event === 'content_completed' || event.event === 'done') {
      updateAssistantMessage(assistantMessageId, (item) => ({
        ...item,
        pending: event.event !== 'done',
        runId: event.runId,
        status: event.event === 'done' ? readText(event.data, 'status') || 'Succeeded' : item.status
      }));
      if (event.event === 'done') {
        void queryClient.invalidateQueries({ queryKey: ['ai', 'task-plans'] });
      }
      return;
    }

    if (event.event.startsWith('plan_') || event.event.startsWith('task_')) {
      void queryClient.invalidateQueries({ queryKey: ['ai', 'task-plans'] });
    }

    if (event.event.startsWith('workflow_')) {
      void queryClient.invalidateQueries({ queryKey: ['ai', 'workflow-overview', activeConversationId] });
    }

    if (event.event === 'error') {
      const streamError = createAiStreamEventError(event);
      const errorMessage = getErrorMessage(streamError, translate('ai.chat.error.modelCallFailed'));
      updateAssistantMessage(assistantMessageId, (item) => ({
        ...item,
        content: item.content || errorMessage,
        pending: false,
        runId: event.runId,
        status: 'Failed'
      }));
      message.error(errorMessage);
    }
  };

  const updateAssistantMessage = (id: string, updater: (message: AiMessageDraft) => AiMessageDraft) => {
    setLocalMessages((current) => current.map((item) => (item.id === id ? updater(item) : item)));
  };

  const refreshTaskPlans = async () => {
    await queryClient.invalidateQueries({ queryKey: ['ai', 'task-plans', activeConversationId] });
  };

  const handleApprovePlan = async (plan: AiTaskPlanDto) => {
    await aiChatApi.taskPlans.approve(plan.id);
    await refreshTaskPlans();
    setWorkMode('Agent');
    message.success(translate('ai.chat.success.planApplied'));
  };

  const handleExecutePlan = async (plan: AiTaskPlanDto) => {
    if (isStreaming) {
      message.error(translate('ai.chat.error.runningTask'));
      return;
    }

    setWorkMode('Agent');
    setDraft(activeConversationId ?? 'new', formatMessage(translate('ai.chat.draft.executeApprovedPlan'), { title: plan.title }));
    message.success(translate('ai.chat.success.switchToAgent'));
  };

  const handleAddTask = async () => {
    if (!activeConversationId) {
      message.error(translate('ai.chat.error.selectConversation'));
      return;
    }

    const newItem = {
      acceptanceCriteriaJson: JSON.stringify([translate('ai.chat.task.acceptance.manualComplete')]),
      description: translate('ai.chat.task.description.manual'),
      executionHint: translate('ai.chat.task.executionHint.manual'),
      maxRetryCount: 3,
      ownerType: 'User',
      priority: 'P1',
      sortOrder: (activeTaskPlan?.items.length ?? 0) + 1,
      status: 'Pending',
      taskType: 'Manual',
      title: translate('ai.chat.task.title.manual')
    };

    if (!activeTaskPlan) {
      await aiChatApi.taskPlans.create(activeConversationId, {
        goal: activeConversation?.title ?? translate('ai.chat.task.goalFallback'),
        items: [newItem],
        metadataJson: JSON.stringify({
          overview: translate('ai.chat.task.plan.overview'),
          planMarkdown: [
            formatMessage(translate('ai.chat.task.plan.heading'), { title: activeConversation?.title ?? translate('ai.chat.task.goalFallback') }),
            '',
            formatMessage(translate('ai.chat.task.plan.section.goal'), { title: activeConversation?.title ?? translate('ai.chat.task.goalFallback') }),
            '',
            formatMessage(translate('ai.chat.task.plan.section.steps'), { title: translate('ai.chat.task.title.manual') }),
            '',
            translate('ai.chat.task.plan.section.acceptance')
          ].join('\n')
        }),
        mode: 'Plan',
        executionStrategy: 'Serial',
        status: 'Draft',
        title: translate('ai.chat.task.plan.title')
      });
    } else {
      await aiChatApi.taskPlans.addItem(activeTaskPlan.id, newItem);
    }

    await refreshTaskPlans();
  };

  const handlePatchTask = async (item: AiTaskPlanItemDto, status: string, userText?: string) => {
    const normalizedUserText = userText?.trim();
    if (status === 'Succeeded') {
      await aiChatApi.taskPlans.markComplete(item.id, {
        userResult: normalizedUserText || item.resultSummary || translate('ai.chat.task.result.manualSuccess'),
        expectedUpdatedTime: item.updatedTime
      });
    } else if (status === 'Skipped') {
      await aiChatApi.taskPlans.skip(item.id, {
        reason: normalizedUserText || translate('ai.chat.task.result.manualSkipped'),
        expectedUpdatedTime: item.updatedTime
      });
    } else if (status === 'Pending') {
      await aiChatApi.taskPlans.retry(item.id, {
        expectedUpdatedTime: item.updatedTime
      });
    } else {
      await aiChatApi.taskPlans.patchItem(item.id, {
        expectedUpdatedTime: item.updatedTime,
        status
      });
    }
    setTaskActionDrafts((current) => {
      const next = { ...current };
      delete next[item.id];
      return next;
    });
    await refreshTaskPlans();
  };

  const handleMoveTask = async (plan: AiTaskPlanDto, item: AiTaskPlanItemDto, direction: -1 | 1) => {
    const ordered = [...plan.items].sort((left, right) => left.sortOrder - right.sortOrder);
    const index = ordered.findIndex((candidate) => candidate.id === item.id);
    const target = ordered[index + direction];
    if (!target) {
      return;
    }

    await aiChatApi.taskPlans.moveItem(item.id, { parentItemId: item.parentItemId, sortOrder: target.sortOrder });
    await aiChatApi.taskPlans.moveItem(target.id, { parentItemId: target.parentItemId, sortOrder: item.sortOrder });
    await refreshTaskPlans();
  };

  const startEditTask = (item: AiTaskPlanItemDto) => {
    setEditingTaskId(item.id);
    setEditingTaskDraft({ description: item.description, title: item.title });
    if (!expandedTaskIds[item.id]) {
      toggleTaskExpanded(item.id);
    }
  };

  const saveTaskEdit = async (item: AiTaskPlanItemDto) => {
    await aiChatApi.taskPlans.patchItem(item.id, {
      description: editingTaskDraft.description,
      expectedUpdatedTime: item.updatedTime,
      title: editingTaskDraft.title
    });
    setEditingTaskId(null);
    await refreshTaskPlans();
  };

  const refreshWorkflowOverview = async () => {
    await queryClient.invalidateQueries({ queryKey: ['ai', 'workflow-overview', activeConversationId] });
    await queryClient.invalidateQueries({ queryKey: ['ai', 'tools', 'workflow'] });
  };

  return (
    <CrudPage
      actions={
        <div className="ai-page-actions">
          <button className="ghost-button" type="button" onClick={() => void conversationsQuery.refetch()}>
            <RefreshCw size={15} />
            {translate('common.refresh')}
          </button>
          <PermissionButton className="primary-button" code="ai:chat:create" type="button" onClick={() => void handleCreateConversation()}>
            {translate('ai.chat.actions.newConversation')}
          </PermissionButton>
        </div>
      }
      className="ai-chat-page"
      description={translate('ai.chat.description')}
      eyebrow={translate('ai.eyebrow')}
      title={translate('ai.chat.title')}
    >
      <div className="ai-chat-shell">
        <aside className="ai-conversation-list">
          <div className="ai-search-box">
            <input
              aria-label={translate('ai.chat.search.ariaLabel')}
              placeholder={translate('ai.chat.search.placeholder')}
              value={keyword}
              onChange={(event) => setKeyword(event.target.value)}
            />
          </div>
          <div className="ai-conversation-scroll">
            {conversations.map((conversation) => (
              <button
                className={`ai-conversation-item ${conversation.id === activeConversationId ? 'ai-conversation-item--active' : ''}`}
                key={conversation.id}
                type="button"
                onClick={() => setActiveConversationId(conversation.id)}
              >
                <span className="ai-conversation-title">
                  {conversation.isFavorite ? <Star fill="currentColor" size={13} /> : null}
                  {conversation.title}
                </span>
                <span className="ai-conversation-meta">
                  <AiStatusBadge status={conversation.lastRunStatus ?? conversation.status} />
                  <span>{formatTime(conversation.lastMessageAt ?? conversation.createdTime)}</span>
                </span>
              </button>
            ))}
            {conversations.length === 0 ? <div className="ai-empty-state">{translate('ai.chat.empty.conversations')}</div> : null}
          </div>
        </aside>

        <main className="ai-chat-main">
          <header className="ai-chat-main-header">
            <div>
              <h2>{activeConversation?.title ?? translate('ai.chat.conversation.newTitle')}</h2>
              <p>{selectedModel ? `${selectedModel.providerName} / ${selectedModel.modelCode}` : translate('ai.chat.model.unselected')}</p>
            </div>
            <div className="ai-chat-header-actions">
              {activeConversation ? (
                <>
                  <button aria-label={translate('ai.chat.actions.favoriteConversation')} className="icon-button" type="button" onClick={() => void handleFavoriteConversation(activeConversation)}>
                    <Star fill={activeConversation.isFavorite ? 'currentColor' : 'none'} size={17} />
                  </button>
                  <PermissionButton
                    aria-label={translate('ai.chat.actions.archiveConversation')}
                    className="icon-button"
                    code="ai:chat:archive"
                    iconStart={false}
                    type="button"
                    onClick={() => handleArchiveConversation(activeConversation)}
                  >
                    <Archive size={17} />
                  </PermissionButton>
                  <PermissionButton
                    aria-label={translate('ai.chat.actions.deleteConversation')}
                    className="icon-button"
                    code="ai:chat:delete"
                    iconStart={false}
                    type="button"
                    onClick={() => handleDeleteConversation(activeConversation)}
                  >
                    <Trash2 size={17} />
                  </PermissionButton>
                </>
              ) : null}
              <button className="icon-button" type="button" onClick={() => setRightPanelOpen(!rightPanelOpen)}>
                {rightPanelOpen ? <ChevronRight size={17} /> : <ChevronLeft size={17} />}
              </button>
            </div>
          </header>

          <div className="ai-message-list" ref={messageListRef}>
            {localMessages.map((item) => (
              <AiMessageBubble 
                key={item.id} 
                message={item} 
                workflowOverview={workflowOverview} 
                onExecutePlan={() => {
                  let planToExecute = approvedTaskPlan ?? activeTaskPlan;
                  if (!planToExecute) {
                    message.error(translate('ai.chat.error.noExecutablePlan'));
                    return;
                  }
                  
                  if (planToExecute.status === 'Draft' || planToExecute.status === 'PlanReady') {
                    void aiChatApi.taskPlans.approve(planToExecute.id).then(() => {
                      void refreshTaskPlans();
                    });
                  }
                  
                  setWorkMode('Agent');
                  setDraft(activeConversationId ?? 'new', formatMessage(translate('ai.chat.draft.executeApprovedPlan'), { title: planToExecute.title }));
                  message.success(translate('ai.chat.success.agentModeStarted'));
                }}
              />
            ))}
            {localMessages.length === 0 ? (
              <div className="ai-chat-placeholder">
                <Bot size={34} />
                <strong>{translate('ai.chat.placeholder.title')}</strong>
                <span>{translate('ai.chat.placeholder.description')}</span>
              </div>
            ) : null}
          </div>

          <footer className="ai-composer">
            <div className="ai-workmode-row">
              <ModeSegment value={workMode} onChange={setWorkMode} />
              <span>
                {workMode === 'Ask'
                  ? translate('ai.chat.workMode.ask')
                  : workMode === 'Plan'
                  ? translate('ai.chat.workMode.plan')
                  : approvedTaskPlan
                  ? formatMessage(translate('ai.chat.workMode.agentWithPlan'), { title: approvedTaskPlan.title })
                  : translate('ai.chat.workMode.agentNeedsPlan')}
              </span>
            </div>
            <textarea
              disabled={isStreaming}
              placeholder={
                workMode === 'Plan'
                  ? translate('ai.chat.placeholder.plan')
                  : workMode === 'Agent'
                    ? translate('ai.chat.placeholder.agent')
                    : translate('ai.chat.placeholder.ask')
              }
              value={currentDraft}
              onChange={(event) => setDraft(activeConversationId ?? 'new', event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && (event.metaKey || event.ctrlKey)) {
                  event.preventDefault();
                  void handleSend();
                }
              }}
            />
            <div className="ai-composer-actions">
              <span>{usageText}</span>
              {isStreaming ? (
                <button className="ghost-button" type="button" onClick={() => void handleStop()}>
                  <CircleStop size={15} />
                  {translate('ai.chat.actions.stop')}
                </button>
              ) : (
                <PermissionButton className="primary-button" code="ai:chat:create" type="button" onClick={() => void handleSend()}>
                  <Send size={15} />
                  {translate('ai.chat.actions.send')}
                </PermissionButton>
              )}
            </div>
          </footer>
        </main>

        <aside className={`ai-inspector ${rightPanelOpen ? 'ai-inspector--open' : ''}`}>
          <div className="ai-inspector-header">
            <span>{translate('ai.chat.inspector.title')}</span>
            <button aria-label={translate('ai.chat.actions.collapseInspector')} className="icon-button" type="button" onClick={() => setRightPanelOpen(false)}>
              <ChevronRight size={17} />
            </button>
          </div>
          <section>
            <h3>
              <ClipboardList size={16} />
              {translate('ai.chat.inspector.taskPlan')}
            </h3>
            <TaskPlanPanel
              editingTaskDraft={editingTaskDraft}
              editingTaskId={editingTaskId}
              expandedTaskIds={expandedTaskIds}
              isStreaming={isStreaming}
              plan={activeTaskPlan}
              workMode={workMode}
              onAddTask={() => void handleAddTask()}
              onApprove={(plan) => void handleApprovePlan(plan)}
              onCancelEdit={() => setEditingTaskId(null)}
              onEditTask={startEditTask}
              onExecute={(plan) => void handleExecutePlan(plan)}
              onMoveTask={(plan, item, direction) => void handleMoveTask(plan, item, direction)}
              onPatchTask={(item, status, userText) => void handlePatchTask(item, status, userText)}
              onReplan={() => {
                setWorkMode('Plan');
                setDraft(activeConversationId ?? 'new', activeTaskPlan ? formatMessage(translate('ai.chat.draft.replan'), { goal: activeTaskPlan.goal }) : currentDraft);
              }}
              onSaveEdit={(item) => void saveTaskEdit(item)}
              onTaskActionDraftChange={(taskId, value) => setTaskActionDrafts((current) => ({ ...current, [taskId]: value }))}
              onTaskDraftChange={setEditingTaskDraft}
              onToggleTask={toggleTaskExpanded}
              taskActionDrafts={taskActionDrafts}
            />
          </section>

          <AgentToolCapabilitySelector
            selectedDomains={settings.enabledToolDomains}
            selectedToolCodes={settings.enabledToolCodes}
            tools={aiTools}
            workMode={workMode}
            onToggleDomain={handleToolDomainToggle}
            onToolCodesChange={handleToolCodesChange}
          />

          <WorkflowToolInspectorSection
            loading={workflowOverviewQuery.isLoading || toolsQuery.isLoading}
            overview={workflowOverview}
            onRefresh={() => void refreshWorkflowOverview()}
          />

          <section>
            <h3>
              <Settings size={16} />
              {translate('ai.chat.inspector.modelSelection')}
            </h3>
            <LabeledSelect
              label={translate('ai.chat.inspector.model')}
              value={settings.modelConfigId}
              options={models.map((item) => ({ label: `${item.displayName} (${item.modelCode})`, value: item.id }))}
              onChange={(value) => applyModelDefaults(value, models, setSettings)}
            />
            <LabeledSelect
              label={translate('ai.chat.inspector.promptTemplate')}
              value={settings.promptTemplateId}
              options={[{ label: translate('ai.chat.inspector.defaultTemplate'), value: '' }, ...prompts.map((item) => ({ label: item.templateName, value: item.id }))]}
              onChange={(value) => setSettings((current) => ({ ...current, promptTemplateId: value }))}
            />
          </section>

        </aside>
      </div>
    </CrudPage>
  );
}
