import { Button, Fab, IconButton, MenuItem, TextField } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useRef, useState, type MouseEvent, type RefObject } from 'react';


import { HttpError } from '../../../core/http/httpError';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { predictionsApi } from '../api/predictions.api';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { StarterPromptsCard } from '../native/ui-component/cards/StarterPromptsCard';
import { SourceDocDialog } from '../native/ui-component/dialog/SourceDocDialog';
import { AudioWaveform } from '../native/ui-component/extended/AudioWaveform';
import { FileUpload } from '../native/ui-component/file/File';
import { AgentExecutedDataCard } from '../native/views/chatmessage/AgentExecutedDataCard';
import { AgentReasoningCard } from '../native/views/chatmessage/AgentReasoningCard';
import { cancelAudioRecording, startAudioRecording, stopAudioRecording, supportsAudioRecording, type FlowiseAudioRecordingSession } from '../native/views/chatmessage/audio-recording';
import { ChatExpandDialog } from '../native/views/chatmessage/ChatExpandDialog';
import { persistChatInputHistory, readChatInputHistory, resolveChatInputHistoryNavigation, type ChatInputHistoryDirection } from '../native/views/chatmessage/ChatInputHistory';
import { ChatMessage } from '../native/views/chatmessage/ChatMessage';
import { speakTextMessage, stopTextToSpeech, supportsTextToSpeech, type FlowiseTextToSpeechPlayback } from '../native/views/chatmessage/text-to-speech';
import { ThinkingCard } from '../native/views/chatmessage/ThinkingCard';
import { ValidationPopUp } from '../native/views/chatmessage/ValidationPopUp';
import type { FlowiseCanvasValidationResult } from '../types/canvas.types';
import type { FlowiseChatflowDto } from '../types/chatflow.types';
import type {
  FlowiseAgentExecutedNodeDto,
  FlowiseAgentReasoningDto,
  FlowiseChatMessageDto,
  FlowiseLeadDto,
  FlowisePredictionStreamErrorPayload,
  FlowisePredictionUpload,
  FlowiseSourceDocumentDto,
  FlowiseUsedToolDto
} from '../types/prediction.types';

interface FlowiseChatTestPanelProps {
  chatflow?: FlowiseChatflowDto | null;
  open: boolean;
  resourceId: string;
  validation?: FlowiseCanvasValidationResult | null;
  onClose: () => void;
  onBeforeRun?: () => Promise<boolean>;
  onWorkflowNodeStatusClear?: () => void;
  onWorkflowNodeStatusUpdate?: (status: WorkflowRuntimeEvent) => void;
  onValidate?: () => void;
}

interface LeadDraft {
  email: string;
  name: string;
  phone: string;
}

interface FlowiseLeadCaptureConfig {
  email: boolean;
  name: boolean;
  phone: boolean;
  successMessage: string;
  title: string;
}

interface FlowiseStartFormInputParam {
  addOptions?: Array<{ option?: string }>;
  description?: string;
  label?: string;
  name: string;
  options?: Array<{ label?: string; name?: string; value?: string }>;
  placeholder?: string;
  required?: boolean;
  type?: string;
}

interface FlowiseStartFormConfig {
  description: string;
  inputs: FlowiseStartFormInputParam[];
  title: string;
}

interface WorkflowRuntimeEvent {
  error?: string;
  id: string;
  label: string;
  nodeId?: string;
  status: string;
  type: 'agentFlowEvent' | 'nextAgentFlow';
}

interface FlowiseHumanInputAction {
  data: {
    input?: string;
    nodeId?: string;
    nodeLabel?: string;
  };
  elements: Array<{
    label: string;
    type: string;
  }>;
  id: string;
  mapping: {
    approve?: string;
    reject?: string;
  };
}

const chatIdStorageKey = (resourceId: string) => `flowise:chat:${resourceId}:chatId`;
const leadStorageKey = (resourceId: string, chatId: string) => `flowise:chat:${resourceId}:${chatId}:lead`;

export function FlowiseChatTestPanel({
  chatflow,
  open,
  resourceId,
  validation,
  onWorkflowNodeStatusClear,
  onWorkflowNodeStatusUpdate,
  onBeforeRun,
  onClose,
  onValidate
}: FlowiseChatTestPanelProps) {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [chatOpen, setChatOpen] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [validationOpen, setValidationPopupOpen] = useState(false);
  const [question, setQuestion] = useState('');
  const [feedbackReason, setFeedbackReason] = useState('');
  const [leadDraft, setLeadDraft] = useState<LeadDraft>({ email: '', name: '', phone: '' });
  const [chatId, setChatId] = useState(() => resolveStoredChatId(resourceId));
  const [uploads, setUploads] = useState<FlowisePredictionUpload[]>([]);
  const [running, setRunning] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [inputHistory, setInputHistory] = useState<string[]>(() => readChatInputHistory(resourceId));
  const [historyCursor, setHistoryCursor] = useState<number | null>(null);
  const [recording, setRecording] = useState(false);
  const [speakingMessageId, setSpeakingMessageId] = useState<string | null>(null);
  const [messagesOverride, setMessagesOverride] = useState<FlowiseChatMessageDto[] | null>(null);
  const [streamingExecutedData, setStreamingExecutedData] = useState<FlowiseAgentExecutedNodeDto[]>([]);
  const [streamingRuntimeEvents, setStreamingRuntimeEvents] = useState<WorkflowRuntimeEvent[]>([]);
  const [streamingAnswer, setStreamingAnswer] = useState('');
  const [streamingFollowUpPrompts, setStreamingFollowUpPrompts] = useState<string[]>([]);
  const [pendingAction, setPendingAction] = useState<FlowiseHumanInputAction | null>(null);
  const [sourceDialogDocuments, setSourceDialogDocuments] = useState<FlowiseSourceDocumentDto[]>([]);
  const inputRef = useRef<HTMLTextAreaElement | null>(null);
  const predictionAbortRef = useRef<AbortController | null>(null);
  const audioRecordingRef = useRef<FlowiseAudioRecordingSession | null>(null);
  const textToSpeechRef = useRef<FlowiseTextToSpeechPlayback | null>(null);

  useEffect(() => {
    if (!open) {
      setChatOpen(false);
      setExpanded(false);
      return;
    }

    setChatOpen(true);
  }, [open]);

  useEffect(() => {
    setChatId(resolveStoredChatId(resourceId));
    setInputHistory(readChatInputHistory(resourceId));
    setHistoryCursor(null);
    setMessagesOverride(null);
  }, [resourceId]);

  useEffect(() => {
    return () => {
      predictionAbortRef.current?.abort();
      stopTextToSpeech();
      cancelAudioRecording(audioRecordingRef.current);
    };
  }, []);

  const messagesQuery = useApiQuery({
    enabled: chatOpen && Boolean(resourceId),
    queryKey: ['flowise', 'chat-popup', 'messages', resourceId, chatId],
    queryFn: ({ signal }) => predictionsApi.messages.list({ chatId, pageIndex: 1, pageSize: 100, resourceId }, signal)
  });
  const leadCaptureConfig = useMemo(() => resolveLeadCaptureConfig(chatflow?.chatbotConfig), [chatflow?.chatbotConfig]);
  const leadsQuery = useApiQuery({
    enabled: chatOpen && Boolean(resourceId) && Boolean(leadCaptureConfig),
    queryKey: ['flowise', 'chat-popup', 'leads', resourceId],
    queryFn: ({ signal }) => predictionsApi.leads.list({ pageIndex: 1, pageSize: 20, resourceId }, signal)
  });
  const chatFeedbackEnabled = useMemo(() => resolveChatFeedbackEnabled(chatflow?.chatbotConfig), [chatflow?.chatbotConfig]);
  const feedbackMutation = useApiMutation({
    mutationFn: ({ messageId, rating }: { messageId: string; rating: 'down' | 'up' }) =>
      predictionsApi.feedback(messageId, rating, feedbackReason.trim() || undefined),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.feedbackFailed))),
    onSuccess: async () => {
      setFeedbackReason('');
      await messagesQuery.refetch();
      message.success(translate(flowiseI18nKeys.messages.feedbackSaved));
    }
  });
  const leadMutation = useApiMutation({
    mutationFn: () =>
      predictionsApi.lead(
        resourceId,
        JSON.stringify({
          chatId,
          email: leadDraft.email.trim(),
          name: leadDraft.name.trim(),
          phone: leadDraft.phone.trim()
        })
      ),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.leadFailed))),
    onSuccess: async () => {
      localStorage.setItem(leadStorageKey(resourceId, chatId), JSON.stringify(leadDraft));
      setLeadDraft({ email: '', name: '', phone: '' });
      await leadsQuery.refetch();
      message.success(leadCaptureConfig?.successMessage || translate(flowiseI18nKeys.messages.leadSaved));
    }
  });
  const clearMutation = useApiMutation({
    mutationFn: () => predictionsApi.clear(resourceId, chatId),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.clearChatFailed))),
    onSuccess: async () => {
      const previousChatId = chatId;
      const nextChatId = createChatId();
      const previousMessagesKey = ['flowise', 'chat-popup', 'messages', resourceId, previousChatId];
      const nextMessagesKey = ['flowise', 'chat-popup', 'messages', resourceId, nextChatId];
      persistChatId(resourceId, nextChatId);
      localStorage.removeItem(leadStorageKey(resourceId, previousChatId));
      queryClient.setQueryData(previousMessagesKey, createEmptyMessagesResult());
      queryClient.removeQueries({ queryKey: nextMessagesKey });
      setChatId(nextChatId);
      setMessagesOverride([]);
      setUploads([]);
      setStreamingExecutedData([]);
      setStreamingRuntimeEvents([]);
      setStreamingAnswer('');
      setStreamingFollowUpPrompts([]);
      setPendingAction(null);
      message.success(translate(flowiseI18nKeys.messages.chatCleared));
    }
  });
  const messages = useMemo(
    () => [...(messagesOverride ?? messagesQuery.data?.data.items ?? [])].sort((left, right) => new Date(left.createdTime).getTime() - new Date(right.createdTime).getTime()),
    [messagesOverride, messagesQuery.data?.data.items]
  );
  const starterPrompts = useMemo(() => resolveStarterPrompts(chatflow?.chatbotConfig), [chatflow?.chatbotConfig]);
  const startFormConfig = useMemo(() => resolveStartFormConfig(chatflow?.flowData), [chatflow?.flowData]);
  const fileUploadEnabled = useMemo(() => resolveFileUploadEnabled(chatflow?.chatbotConfig), [chatflow?.chatbotConfig]);
  const followUpPromptsEnabled = useMemo(() => resolveFollowUpPromptsEnabled(chatflow?.chatbotConfig, chatflow?.followUpPrompts), [chatflow?.chatbotConfig, chatflow?.followUpPrompts]);
  const followUpPrompts = useMemo(
    () => (streamingFollowUpPrompts.length > 0 ? streamingFollowUpPrompts : resolveLatestFollowUpPrompts(messages)),
    [messages, streamingFollowUpPrompts]
  );
  const latestAction = useMemo(() => pendingAction ?? resolveLatestHumanInputAction(messages), [messages, pendingAction]);
  const speechToTextEnabled = useMemo(() => hasEnabledFeatureConfig(chatflow?.speechToText), [chatflow?.speechToText]);
  const textToSpeechEnabled = useMemo(() => hasEnabledFeatureConfig(chatflow?.textToSpeech), [chatflow?.textToSpeech]);
  const leadSaved = Boolean(leadCaptureConfig) && (Boolean(localStorage.getItem(leadStorageKey(resourceId, chatId))) || (leadsQuery.data?.data.items ?? []).some((lead) => leadMatchesChat(lead, chatId)));

  useEffect(() => {
    if (!fileUploadEnabled) {
      setUploads([]);
    }
  }, [fileUploadEnabled]);

  if (!open) {
    return null;
  }

  const toggleChat = () => {
    setChatOpen((current) => !current);
    if (chatOpen) {
      onClose();
    }
  };
  const clearChat = (event?: MouseEvent<HTMLButtonElement>) => {
    event?.preventDefault();
    event?.stopPropagation();

    if (!resourceId) {
      message.error(translate(flowiseI18nKeys.messages.saveBeforeAction));
      return;
    }

    confirm({
      title: translate(flowiseI18nKeys.messages.clearChatTitle),
      content: translate(flowiseI18nKeys.messages.clearChatConfirm),
      confirmText: translate(flowiseI18nKeys.actions.clear),
      onConfirm: () => clearMutation.mutate()
    });
  };
  const submit = async (promptOverride?: string, form?: Record<string, unknown>) => {
    const nextQuestion = promptOverride ?? question;
    const hasForm = form && Object.keys(form).length > 0;
    const requestUploads = fileUploadEnabled ? uploads : [];
    if ((!nextQuestion.trim() && !hasForm && requestUploads.length === 0) || running) {
      return;
    }

    if (!resourceId) {
      message.error(translate(flowiseI18nKeys.messages.saveBeforeAction));
      return;
    }

    if (onBeforeRun && !(await onBeforeRun())) {
      return;
    }

    if (nextQuestion.trim()) {
      const nextHistory = persistChatInputHistory(resourceId, nextQuestion, inputHistory);
      setInputHistory(nextHistory);
    }
    setHistoryCursor(null);
    setMessagesOverride(null);
    const controller = new AbortController();
    predictionAbortRef.current = controller;
    setRunning(true);
    setStopping(false);
    setStreamingExecutedData([]);
    setStreamingRuntimeEvents([]);
    setStreamingAnswer('');
    setStreamingFollowUpPrompts([]);
    setPendingAction(null);
    onWorkflowNodeStatusClear?.();
    try {
      await predictionsApi.stream(
        { chatId, form, question: nextQuestion, resourceId, sessionId: chatId, uploads: requestUploads },
        (event) => {
          if (event.event === 'token' && typeof event.data === 'string') {
            setStreamingAnswer((current) => `${current}${event.data}`);
          }

          if (event.event === 'metadata' && isStreamMetadata(event.data)) {
            persistChatId(resourceId, event.data.chatId);
            setChatId(event.data.chatId);
            setStreamingFollowUpPrompts(normalizeFollowUpPrompts(event.data.message?.followUpPrompts));
          }

          if (event.event === 'agentFlowExecutedData') {
            const nextExecutedData = normalizeExecutedDataEvent(event.data);
            if (nextExecutedData.length > 0) {
              setStreamingExecutedData((current) => mergeExecutedData(current, nextExecutedData));
            }
          }

          if (event.event === 'agentFlowEvent') {
            const runtimeEvent = normalizeWorkflowEvent(event.data);
            if (runtimeEvent) {
              setStreamingRuntimeEvents((current) => appendRuntimeEvent(current, runtimeEvent));
            }
          }

          if (event.event === 'nextAgentFlow') {
            const runtimeEvent = normalizeNextWorkflowEvent(event.data);
            if (runtimeEvent) {
              onWorkflowNodeStatusUpdate?.(runtimeEvent);
              setStreamingRuntimeEvents((current) => appendRuntimeEvent(current, runtimeEvent));
            }
          }

          if (event.event === 'action') {
            const action = normalizeHumanInputAction(event.data);
            if (action) {
              setPendingAction(action);
            }
          }

          if (event.event === 'abort') {
            const runtimeEvent = createWorkflowAbortEvent(event.data);
            setStreamingRuntimeEvents((current) => appendRuntimeEvent(current, runtimeEvent));
            throw new DOMException('Flowise stream aborted.', 'AbortError');
          }

          if (event.event === 'error') {
            throw createStreamHttpError(event.data, translate(flowiseI18nKeys.messages.streamFailed));
          }
        },
        controller.signal
      );
      setQuestion('');
      setUploads([]);
      await messagesQuery.refetch();
      message.success(translate(flowiseI18nKeys.messages.runCompleted));
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        message.success(translate(flowiseI18nKeys.messages.runStopped));
      } else {
        message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.chatTestFailed)));
      }
    } finally {
      if (predictionAbortRef.current === controller) {
        predictionAbortRef.current = null;
      }
      setStreamingExecutedData([]);
      setStreamingAnswer('');
      setStopping(false);
      setRunning(false);
    }
  };
  const submitAction = (action: FlowiseHumanInputAction, choice: 'approve' | 'reject') => {
    const label = choice === 'approve' ? action.mapping.approve || 'Proceed' : action.mapping.reject || 'Reject';
    const nodeLabel = action.data.nodeLabel || 'Human Input';
    setPendingAction(null);
    void submit(label, {
      action: {
        actionId: action.id,
        choice,
        label,
        nodeId: action.data.nodeId,
        nodeLabel
      }
    });
  };
  const stop = () => {
    if (stopping) {
      return;
    }

    setStopping(true);
    stopTextToSpeech();
    void predictionsApi.abort(resourceId, chatId).catch((error) => {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.runStopped)));
      setStopping(false);
    });
  };
  const openValidationPopup = () => {
    onValidate?.();
    setValidationPopupOpen(true);
  };
  const navigateInputHistory = (direction: ChatInputHistoryDirection) => {
    const next = resolveChatInputHistoryNavigation(inputHistory, historyCursor, direction);
    setHistoryCursor(next.cursor);
    setQuestion(next.value);
  };
  const startRecording = async () => {
    if (!speechToTextEnabled) {
      message.error(translate(flowiseI18nKeys.messages.speechToTextNotEnabled));
      return;
    }

    if (!supportsAudioRecording()) {
      message.error(translate(flowiseI18nKeys.messages.audioRecordingUnsupported));
      return;
    }

    try {
      audioRecordingRef.current = await startAudioRecording();
      setRecording(true);
    } catch (error) {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.audioRecordingFailed)));
    }
  };
  const stopRecording = async () => {
    const session = audioRecordingRef.current;
    if (!session || session.recorder.state === 'inactive') {
      return;
    }

    try {
      const upload = await stopAudioRecording(session, readUploadFile);
      setUploads((current) => [...current, upload].slice(0, 10));
    } catch (error) {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.audioRecordingFailed)));
    } finally {
      audioRecordingRef.current = null;
      setRecording(false);
    }
  };
  const toggleSpeech = (chatMessage: FlowiseChatMessageDto) => {
    if (!textToSpeechEnabled) {
      return;
    }

    if (!supportsTextToSpeech()) {
      message.error(translate(flowiseI18nKeys.messages.textToSpeechUnsupported));
      return;
    }

    if (speakingMessageId === chatMessage.id) {
      stopTextToSpeech();
      textToSpeechRef.current = null;
      setSpeakingMessageId(null);
      return;
    }

    stopTextToSpeech();
    textToSpeechRef.current = speakTextMessage({
      messageId: chatMessage.id,
      onEnd: () => {
        textToSpeechRef.current = null;
        setSpeakingMessageId(null);
      },
      onError: () => {
        textToSpeechRef.current = null;
        setSpeakingMessageId(null);
      },
      text: chatMessage.message
    });
    setSpeakingMessageId(chatMessage.id);
  };
  const addFiles = async (files: FileList | null) => {
    if (!fileUploadEnabled) {
      return;
    }

    if (!files || files.length === 0) {
      return;
    }

    try {
      const nextUploads = await Promise.all(Array.from(files).map(readUploadFile));
      setUploads((current) => [...current, ...nextUploads].slice(0, 10));
    } catch (error) {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.uploadFailed)));
    }
  };
  const chatContent = (
    <ChatContent
      chatId={chatId}
      chatFeedbackEnabled={chatFeedbackEnabled}
      feedbackReason={feedbackReason}
      fileUploadEnabled={fileUploadEnabled}
      followUpPrompts={followUpPromptsEnabled ? followUpPrompts : []}
      inputRef={inputRef}
      leadCaptureConfig={leadCaptureConfig}
      leadDraft={leadDraft}
      leadSaved={leadSaved}
      loading={messagesQuery.isLoading || running}
      messages={messages}
      question={question}
      recording={recording}
      running={running}
      stopping={stopping}
      speakingMessageId={speakingMessageId}
      speechToTextEnabled={speechToTextEnabled}
      startFormConfig={startFormConfig}
      starterPrompts={starterPrompts}
      streamingAnswer={streamingAnswer}
      streamingExecutedData={streamingExecutedData}
      pendingAction={latestAction}
      streamingRuntimeEvents={streamingRuntimeEvents}
      textToSpeechEnabled={textToSpeechEnabled}
      translate={translate}
      uploads={uploads}
      onAddFiles={addFiles}
      onFeedback={(messageId, rating) => {
        if (chatFeedbackEnabled) {
          feedbackMutation.mutate({ messageId, rating });
        }
      }}
      onHistoryNavigate={navigateInputHistory}
      onLeadChange={setLeadDraft}
      onLeadSubmit={() => {
        if (!resourceId) {
          message.error(translate(flowiseI18nKeys.messages.saveBeforeAction));
          return;
        }
        if (!leadCaptureConfig) {
          return;
        }

        leadMutation.mutate();
      }}
      onQuestionChange={setQuestion}
      onReasonChange={setFeedbackReason}
      onRemoveUpload={(index) => setUploads((current) => current.filter((_, itemIndex) => itemIndex !== index))}
      onStartRecording={startRecording}
      onFollowUpPrompt={(prompt) => void submit(prompt)}
      onHumanInputAction={submitAction}
      onStarterPrompt={(prompt) => void submit(prompt)}
      onStop={stop}
      onStopRecording={() => void stopRecording()}
      onSubmit={submit}
      onToggleSpeech={toggleSpeech}
      onViewSourceDocuments={setSourceDialogDocuments}
    />
  );

  return (
    <div className="flowise-chat-popup">
      <Fab className="flowise-chat-popup__fab" size="medium" title={translate(flowiseI18nKeys.actions.chatTest)} onClick={toggleChat}>
        <AppIcon name={chatOpen ? 'x' : 'chat-circle-text'} />
      </Fab>
      {chatOpen ? (
        <>
          <IconButton className="flowise-chat-popup__tool flowise-chat-popup__tool--clear" title={translate(flowiseI18nKeys.actions.clear)} onClick={clearChat}>
            <AppIcon name="trash" />
          </IconButton>
          <IconButton
            className="flowise-chat-popup__tool flowise-chat-popup__tool--expand"
            title={translate(flowiseI18nKeys.actions.expand)}
            onClick={(event) => {
              event.preventDefault();
              event.stopPropagation();
              setExpanded(true);
            }}
          >
            <AppIcon name="external" />
          </IconButton>
          {onValidate ? (
            <IconButton
              className="flowise-chat-popup__tool flowise-chat-popup__tool--validation"
              title={translate(flowiseI18nKeys.canvas.validation)}
              onClick={(event) => {
                event.preventDefault();
                event.stopPropagation();
                openValidationPopup();
              }}
            >
              <AppIcon name="check-circle" />
            </IconButton>
          ) : null}
          <aside className="flowise-chat-test-panel flowise-chat-test-panel--popup">
            <header>
              <strong>{translate(flowiseI18nKeys.actions.chatTest)}</strong>
              <IconButton aria-label={translate(flowiseI18nKeys.actions.close)} size="small" title={translate(flowiseI18nKeys.actions.close)} onClick={onClose}>
                <AppIcon name="x" />
              </IconButton>
            </header>
            {chatContent}
          </aside>
        </>
      ) : null}
      <ChatExpandDialog
        clearText={translate(flowiseI18nKeys.actions.clear)}
        open={expanded}
        title={translate(flowiseI18nKeys.actions.chatTest)}
        validationText={translate(flowiseI18nKeys.canvas.validation)}
        onClear={clearChat}
        onClose={() => setExpanded(false)}
        onValidate={onValidate ? openValidationPopup : undefined}
      >
        {chatContent}
      </ChatExpandDialog>
      {validationOpen ? <ValidationPopUp validation={validation ?? null} translate={translate} onClose={() => setValidationPopupOpen(false)} /> : null}
      <SourceDocDialog
        documents={sourceDialogDocuments}
        open={sourceDialogDocuments.length > 0}
        title={translate(flowiseI18nKeys.detail.sourceDocuments)}
        onClose={() => setSourceDialogDocuments([])}
      />
    </div>
  );
}

function ChatContent({
  chatId,
  chatFeedbackEnabled,
  feedbackReason,
  fileUploadEnabled,
  followUpPrompts,
  inputRef,
  leadCaptureConfig,
  leadDraft,
  leadSaved,
  loading,
  messages,
  pendingAction,
  question,
  recording,
  running,
  stopping,
  speakingMessageId,
  speechToTextEnabled,
  startFormConfig,
  starterPrompts,
  streamingAnswer,
  streamingExecutedData,
  streamingRuntimeEvents,
  textToSpeechEnabled,
  translate,
  uploads,
  onAddFiles,
  onFeedback,
  onHistoryNavigate,
  onLeadChange,
  onLeadSubmit,
  onQuestionChange,
  onReasonChange,
  onRemoveUpload,
  onStartRecording,
  onFollowUpPrompt,
  onHumanInputAction,
  onStarterPrompt,
  onStop,
  onStopRecording,
  onSubmit,
  onToggleSpeech,
  onViewSourceDocuments
}: {
  chatId: string;
  chatFeedbackEnabled: boolean;
  feedbackReason: string;
  fileUploadEnabled: boolean;
  followUpPrompts: string[];
  inputRef: RefObject<HTMLTextAreaElement | null>;
  leadCaptureConfig: FlowiseLeadCaptureConfig | null;
  leadDraft: LeadDraft;
  leadSaved: boolean;
  loading: boolean;
  messages: FlowiseChatMessageDto[];
  pendingAction: FlowiseHumanInputAction | null;
  question: string;
  recording: boolean;
  running: boolean;
  stopping: boolean;
  speakingMessageId: string | null;
  speechToTextEnabled: boolean;
  startFormConfig: FlowiseStartFormConfig | null;
  starterPrompts: string[];
  streamingAnswer: string;
  streamingExecutedData: FlowiseAgentExecutedNodeDto[];
  streamingRuntimeEvents: WorkflowRuntimeEvent[];
  textToSpeechEnabled: boolean;
  translate: (key: string) => string;
  uploads: FlowisePredictionUpload[];
  onAddFiles: (files: FileList | null) => void;
  onFeedback: (messageId: string, rating: 'down' | 'up') => void;
  onHistoryNavigate: (direction: 'next' | 'previous') => void;
  onLeadChange: (draft: LeadDraft) => void;
  onLeadSubmit: () => void;
  onQuestionChange: (value: string) => void;
  onReasonChange: (value: string) => void;
  onRemoveUpload: (index: number) => void;
  onStartRecording: () => void;
  onFollowUpPrompt: (prompt: string) => void;
  onHumanInputAction: (action: FlowiseHumanInputAction, choice: 'approve' | 'reject') => void;
  onStarterPrompt: (prompt: string) => void;
  onStop: () => void;
  onStopRecording: () => void;
  onSubmit: (promptOverride?: string, form?: Record<string, unknown>) => void;
  onToggleSpeech: (message: FlowiseChatMessageDto) => void;
  onViewSourceDocuments: (documents: FlowiseSourceDocumentDto[]) => void;
}) {
  if (startFormConfig && messages.length === 0) {
    return <StartFormInput config={startFormConfig} loading={loading} translate={translate} onSubmit={(form) => onSubmit('', form)} />;
  }

  return (
    <>
      <div className="flowise-chat-test-panel__messages">
        <div className="flowise-chat-bubble flowise-chat-bubble--assistant">{translate(flowiseI18nKeys.messages.chatGreeting)}</div>
        {messages.length === 0 && starterPrompts.length > 0 ? (
          <div className="flowise-starter-prompts">
            <span>{translate(flowiseI18nKeys.detail.starterPrompts)}</span>
            <StarterPromptsCard starterPrompts={starterPrompts.map((prompt) => ({ prompt }))} onPromptClick={(prompt) => onStarterPrompt(prompt)} />
          </div>
        ) : null}
        {messages.map((item) => (
          <ChatMessage
            chatFeedbackEnabled={chatFeedbackEnabled}
            feedbackReason={feedbackReason}
            key={item.id}
            message={item}
            speaking={speakingMessageId === item.id}
            textToSpeechEnabled={textToSpeechEnabled}
            translate={translate}
            renderAgentExecutedData={(message) => (message.agentExecutedData.length > 0 ? <AgentExecutedDataList items={message.agentExecutedData} translate={translate} /> : null)}
            renderAgentReasoning={(message) =>
              message.agentReasoning.length > 0 ? <AgentReasoningList items={message.agentReasoning} translate={translate} onViewSourceDocuments={onViewSourceDocuments} /> : null
            }
            renderArtifacts={(message) => (hasJsonArrayItems(message.artifactsJson) ? <JsonDetails json={message.artifactsJson} title={translate(flowiseI18nKeys.detail.artifacts)} /> : null)}
            renderFileUploads={(message) => (message.fileUploads.length > 0 ? <FileUploads uploads={message.fileUploads} translate={translate} /> : null)}
            renderSourceDocuments={(message) => (message.sourceDocuments.length > 0 ? <SourceDocuments documents={message.sourceDocuments} translate={translate} onView={onViewSourceDocuments} /> : null)}
            renderUsedTools={(message) => (message.usedTools.length > 0 ? <UsedTools tools={message.usedTools} translate={translate} /> : null)}
            onFeedback={onFeedback}
            onReasonChange={onReasonChange}
            onToggleSpeech={onToggleSpeech}
          />
        ))}
        {streamingExecutedData.length > 0 || streamingRuntimeEvents.length > 0 ? (
          <article className="flowise-chat-bubble flowise-chat-bubble--assistant flowise-chat-bubble--runtime">
            {streamingRuntimeEvents.length > 0 ? <WorkflowRuntimeTrail items={streamingRuntimeEvents} /> : null}
            {streamingExecutedData.length > 0 ? <AgentExecutedDataList items={streamingExecutedData} translate={translate} /> : null}
          </article>
        ) : null}
        {streamingAnswer ? <StreamingBubble answer={streamingAnswer} translate={translate} /> : null}
        {loading ? <div className="flowise-chat-thinking">{translate(flowiseI18nKeys.status.running)}</div> : null}
        {pendingAction ? <HumanInputActionCard action={pendingAction} loading={loading} onChoose={onHumanInputAction} /> : null}
        {!running && followUpPrompts.length > 0 ? (
          <div className="flowise-follow-up-prompts">
            <span>Try these prompts</span>
            <StarterPromptsCard starterPrompts={followUpPrompts.map((prompt) => ({ prompt }))} onPromptClick={(prompt) => onFollowUpPrompt(prompt)} />
          </div>
        ) : null}
        {leadCaptureConfig && !leadSaved ? <LeadCapture config={leadCaptureConfig} draft={leadDraft} translate={translate} onChange={onLeadChange} onSubmit={onLeadSubmit} /> : null}
      </div>
      <footer>
        <span className="flowise-chat-session">{chatId}</span>
        <TextField
          fullWidth
          inputRef={inputRef}
          multiline
          minRows={3}
          placeholder={translate(flowiseI18nKeys.search.askQuestion)}
          size="small"
          value={question}
          onChange={(event) => onQuestionChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && !event.shiftKey) {
              event.preventDefault();
              onSubmit();
              return;
            }

            if (event.key === 'ArrowUp' && !event.shiftKey && inputRef.current?.selectionStart === 0) {
              event.preventDefault();
              onHistoryNavigate('previous');
              return;
            }

            if (event.key === 'ArrowDown' && !event.shiftKey && inputRef.current?.selectionStart === question.length) {
              event.preventDefault();
              onHistoryNavigate('next');
            }
          }}
        />
        {uploads.length > 0 ? <FileUploads uploads={uploads} translate={translate} onRemove={onRemoveUpload} /> : null}
        <div className="flowise-chat-actions">
          {fileUploadEnabled ? <FileUpload disabled={running} label={translate(flowiseI18nKeys.actions.upload)} multiple onFilesSelected={onAddFiles} /> : null}
          {speechToTextEnabled ? (
            <Button className={recording ? 'active' : undefined} disabled={running} startIcon={<AppIcon name={recording ? 'stop' : 'mic'} />} variant="outlined" onClick={recording ? onStopRecording : onStartRecording}>
              {recording ? translate(flowiseI18nKeys.actions.stopRecording) : translate(flowiseI18nKeys.actions.startRecording)}
            </Button>
          ) : null}
          {running ? (
            <Button disabled={stopping} startIcon={<AppIcon name="x" />} variant="outlined" onClick={onStop}>
              {translate(stopping ? flowiseI18nKeys.actions.stopping : flowiseI18nKeys.actions.stop)}
            </Button>
          ) : (
            <Button
              aria-label={translate(flowiseI18nKeys.actions.run)}
              disabled={(!question.trim() && (!fileUploadEnabled || uploads.length === 0)) || loading}
              startIcon={<AppIcon name="paper-plane-tilt" />}
              title={translate(flowiseI18nKeys.actions.run)}
              type="button"
              variant="contained"
              onClick={() => onSubmit()}
            >
              {translate(flowiseI18nKeys.actions.run)}
            </Button>
          )}
        </div>
      </footer>
    </>
  );
}

function StreamingBubble({ answer, translate }: { answer: string; translate: (key: string) => string }) {
  return (
    <article className="flowise-chat-bubble flowise-chat-bubble--assistant flowise-chat-bubble--streaming">
      <ThinkingCard isThinking thinking={answer} title={translate(flowiseI18nKeys.status.running)} />
    </article>
  );
}

function FileUploads({
  uploads,
  translate,
  onRemove
}: {
  uploads: FlowisePredictionUpload[];
  translate: (key: string) => string;
  onRemove?: (index: number) => void;
}) {
  return (
    <div className="flowise-file-uploads">
      {uploads.map((item, index) => (
        <div key={`${item.name}-${index}`} className="flowise-file-upload">
          {item.mime.startsWith('image/') ? (
            <img alt={item.name} src={item.data} />
          ) : item.mime.startsWith('audio/') ? (
            <AudioWaveform audioSrc={item.data} />
          ) : (
            <span>
              <AppIcon name="file-text" /> {item.name}
            </span>
          )}
          {onRemove ? (
            <IconButton size="small" title={translate(flowiseI18nKeys.actions.remove)} onClick={() => onRemove(index)}>
              <AppIcon name="x" />
            </IconButton>
          ) : null}
        </div>
      ))}
    </div>
  );
}

function AgentReasoningList({
  items,
  translate,
  onViewSourceDocuments
}: {
  items: FlowiseAgentReasoningDto[];
  translate: (key: string) => string;
  onViewSourceDocuments: (documents: FlowiseSourceDocumentDto[]) => void;
}) {
  return (
    <details className="flowise-agent-reasoning">
      <summary>{translate(flowiseI18nKeys.detail.agentReasoning)}</summary>
      {items.map((item, index) => (
        <AgentReasoningCard
          agent={item}
          completedText={translate(flowiseI18nKeys.status.completed)}
          index={index}
          key={`${item.nodeName}-${index}`}
          renderArtifacts={(json) => (hasJsonArrayItems(json) ? <JsonDetails json={json} title={translate(flowiseI18nKeys.detail.artifacts)} /> : null)}
          renderSourceDocuments={(documents) => <SourceDocuments documents={documents} translate={translate} onView={onViewSourceDocuments} />}
          renderState={(json) => (hasJsonObjectItems(json) ? <JsonDetails json={json} title={translate(flowiseI18nKeys.detail.state)} /> : null)}
          renderUsedTools={(tools) => <UsedTools tools={tools} translate={translate} />}
          translate={translate}
        />
      ))}
    </details>
  );
}

function AgentExecutedDataList({ items, translate }: { items: FlowiseAgentExecutedNodeDto[]; translate: (key: string) => string }) {
  return (
    <details className="flowise-agent-executed">
      <summary>{translate(flowiseI18nKeys.detail.executedData)}</summary>
      <div className="flowise-agent-executed__cards">
        {items.map((item) => (
          <AgentExecutedDataCard key={item.nodeId} execution={item} />
        ))}
      </div>
    </details>
  );
}

function WorkflowRuntimeTrail({ items }: { items: WorkflowRuntimeEvent[] }) {
  return (
    <section className="flowise-agent-runtime-trail" aria-label="Workflow runtime events">
      <strong>Workflow runtime events</strong>
      <ol>
        {items.map((item) => (
          <li key={item.id}>
            <span>{item.label}</span>
            <b className={`flowise-agent-runtime-trail__status flowise-agent-runtime-trail__status--${item.status.toLowerCase()}`}>{item.status}</b>
            {item.error ? <small>{item.error}</small> : null}
          </li>
        ))}
      </ol>
    </section>
  );
}

function HumanInputActionCard({
  action,
  loading,
  onChoose
}: {
  action: FlowiseHumanInputAction;
  loading: boolean;
  onChoose: (action: FlowiseHumanInputAction, choice: 'approve' | 'reject') => void;
}) {
  const nodeLabel = action.data.nodeLabel || 'Human Input';
  const input = action.data.input || 'Please review and choose the next action.';
  return (
    <article className="flowise-human-input-action" aria-label="Human Input action">
      <div>
        <strong>{nodeLabel}</strong>
        <p>{input}</p>
      </div>
      <div className="flowise-human-input-action__buttons">
        <Button disabled={loading} size="small" variant="contained" onClick={() => onChoose(action, 'approve')}>
          {action.mapping.approve || 'Proceed'}
        </Button>
        <Button disabled={loading} size="small" variant="outlined" onClick={() => onChoose(action, 'reject')}>
          {action.mapping.reject || 'Reject'}
        </Button>
      </div>
    </article>
  );
}

function UsedTools({ tools, translate }: { tools: FlowiseUsedToolDto[]; translate: (key: string) => string }) {
  return (
    <div className="flowise-used-tools">
      <span>{translate(flowiseI18nKeys.detail.usedTools)}</span>
      {tools.map((tool, index) => (
        <details key={`${tool.tool}-${index}`}>
          <summary>
            <AppIcon name="wrench" /> {tool.tool}
          </summary>
          <JsonDetails json={tool.inputJson} title={translate(flowiseI18nKeys.fields.input)} />
          <JsonDetails json={tool.outputJson} title={translate(flowiseI18nKeys.common.output)} />
        </details>
      ))}
    </div>
  );
}

function JsonDetails({ json, title }: { json: string; title: string }) {
  return (
    <details className="flowise-json-details">
      <summary>{title}</summary>
      <pre>{formatJson(json)}</pre>
    </details>
  );
}

function SourceDocuments({
  documents,
  translate,
  onView
}: {
  documents: FlowiseSourceDocumentDto[];
  translate: (key: string) => string;
  onView: (documents: FlowiseSourceDocumentDto[]) => void;
}) {
  return (
    <details className="flowise-chat-source-docs">
      <summary>
        <Button size="small" variant="text" onClick={(event) => {
          event.preventDefault();
          onView(documents);
        }}>
          {translate(flowiseI18nKeys.detail.sourceDocuments)}
        </Button>
      </summary>
      {documents.map((document, index) => (
        <section key={`${document.sourceId ?? 'source'}-${index}`}>
          <p>{document.content}</p>
          <small>{formatSourceMeta(document)}</small>
        </section>
      ))}
    </details>
  );
}

function normalizeExecutedDataEvent(value: unknown): FlowiseAgentExecutedNodeDto[] {
  if (Array.isArray(value)) {
    return value.filter(isExecutedNodeEvent);
  }

  return isExecutedNodeEvent(value) ? [value] : [];
}

function isExecutedNodeEvent(value: unknown): value is FlowiseAgentExecutedNodeDto {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const record = value as Partial<FlowiseAgentExecutedNodeDto>;
  return (
    typeof record.nodeId === 'string' &&
    typeof record.nodeLabel === 'string' &&
    typeof record.status === 'string' &&
    typeof record.dataJson === 'string' &&
    Array.isArray(record.previousNodeIds)
  );
}

function mergeExecutedData(current: FlowiseAgentExecutedNodeDto[], incoming: FlowiseAgentExecutedNodeDto[]): FlowiseAgentExecutedNodeDto[] {
  const merged = new Map<string, FlowiseAgentExecutedNodeDto>();
  current.forEach((item) => merged.set(item.nodeId, item));
  incoming.forEach((item) => merged.set(item.nodeId, item));
  return Array.from(merged.values());
}

function normalizeWorkflowEvent(value: unknown): WorkflowRuntimeEvent | null {
  const record = readRuntimeEventRecord(value);
  if (!record.status) {
    return null;
  }

  return {
    error: record.error,
    id: `agent-flow-${record.status}-${Date.now()}-${Math.random().toString(36).slice(2)}`,
    label: 'Workflow',
    status: record.status,
    type: 'agentFlowEvent'
  };
}

function normalizeNextWorkflowEvent(value: unknown): WorkflowRuntimeEvent | null {
  const record = readRuntimeEventRecord(value);
  if (!record.status || !record.nodeId) {
    return null;
  }

  return {
    error: record.error,
    id: `${record.nodeId}-${record.status}-${Date.now()}-${Math.random().toString(36).slice(2)}`,
    label: record.nodeLabel || record.nodeId,
    nodeId: record.nodeId,
    status: record.status,
    type: 'nextAgentFlow'
  };
}

function createWorkflowAbortEvent(value: unknown): WorkflowRuntimeEvent {
  const record = readRuntimeEventRecord(value);
  return {
    error: record.error,
    id: `workflow-abort-${Date.now()}-${Math.random().toString(36).slice(2)}`,
    label: 'Workflow',
    status: 'TERMINATED',
    type: 'agentFlowEvent'
  };
}

function normalizeHumanInputAction(value: unknown): FlowiseHumanInputAction | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const record = value as Record<string, unknown>;
  const data = readRecord(record.data);
  const mapping = readRecord(record.mapping);
  const elements = Array.isArray(record.elements)
    ? record.elements
        .map((element) => readRecord(element))
        .map((element) => ({
          label: typeof element.label === 'string' ? element.label : '',
          type: typeof element.type === 'string' ? element.type : ''
        }))
        .filter((element) => element.label)
    : [];
  if (typeof record.id !== 'string' || elements.length === 0) {
    return null;
  }

  return {
    data: {
      input: typeof data.input === 'string' ? data.input : undefined,
      nodeId: typeof data.nodeId === 'string' ? data.nodeId : undefined,
      nodeLabel: typeof data.nodeLabel === 'string' ? data.nodeLabel : undefined
    },
    elements,
    id: record.id,
    mapping: {
      approve: typeof mapping.approve === 'string' ? mapping.approve : undefined,
      reject: typeof mapping.reject === 'string' ? mapping.reject : undefined
    }
  };
}

function resolveLatestHumanInputAction(messages: FlowiseChatMessageDto[]): FlowiseHumanInputAction | null {
  const message = [...messages].reverse().find((item) => item.role === 'assistant');
  if (!message?.actionJson || !message.agentExecutedData.some((node) => node.status.toUpperCase() === 'STOPPED')) {
    return null;
  }

  try {
    return normalizeHumanInputAction(JSON.parse(message.actionJson));
  } catch {
    return null;
  }
}

function readRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
}

function readRuntimeEventRecord(value: unknown): { error?: string; nodeId?: string; nodeLabel?: string; status?: string } {
  if (!value || typeof value !== 'object') {
    return {};
  }

  const record = value as Record<string, unknown>;
  return {
    error: typeof record.error === 'string' ? record.error : undefined,
    nodeId: typeof record.nodeId === 'string' ? record.nodeId : undefined,
    nodeLabel: typeof record.nodeLabel === 'string' ? record.nodeLabel : undefined,
    status: typeof record.status === 'string' ? record.status : undefined
  };
}

function appendRuntimeEvent(current: WorkflowRuntimeEvent[], incoming: WorkflowRuntimeEvent): WorkflowRuntimeEvent[] {
  return [...current, incoming].slice(-30);
}

function StartFormInput({
  config,
  loading,
  translate,
  onSubmit
}: {
  config: FlowiseStartFormConfig;
  loading: boolean;
  translate: (key: string) => string;
  onSubmit: (form: Record<string, unknown>) => void;
}) {
  const [form, setForm] = useState<Record<string, unknown>>({});
  const requiredMissing = config.inputs.some((input) => input.required && !String(form[input.name] ?? '').trim());

  return (
    <div className="flowise-start-form">
      <div className="flowise-start-form__card">
        <strong>{config.title || 'Please Fill Out The Form'}</strong>
        <p>{config.description || 'Complete all fields below to continue'}</p>
        {config.inputs.map((input) => (
          <StartFormField
            input={input}
            key={input.name}
            value={form[input.name]}
            onChange={(value) =>
              setForm((current) => ({
                ...current,
                [input.name]: value
              }))
            }
          />
        ))}
        <Button disabled={loading || requiredMissing} fullWidth startIcon={<AppIcon name="paper-plane-tilt" />} variant="contained" onClick={() => onSubmit(form)}>
          {loading ? translate(flowiseI18nKeys.status.running) : translate(flowiseI18nKeys.actions.submit)}
        </Button>
      </div>
    </div>
  );
}

function StartFormField({
  input,
  value,
  onChange
}: {
  input: FlowiseStartFormInputParam;
  value: unknown;
  onChange: (value: unknown) => void;
}) {
  const label = input.label || input.name;
  const helperText = input.description || undefined;
  const fieldType = input.type?.toLowerCase() ?? 'string';
  const options = resolveStartFormOptions(input);

  if (fieldType === 'options' || options.length > 0) {
    return (
      <TextField
        fullWidth
        helperText={helperText}
        label={label}
        required={input.required}
        select
        size="small"
        value={String(value ?? '')}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) => (
          <MenuItem key={option.value} value={option.value}>
            {option.label}
          </MenuItem>
        ))}
      </TextField>
    );
  }

  if (fieldType === 'boolean' || fieldType === 'checkbox') {
    return (
      <TextField
        fullWidth
        helperText={helperText}
        label={label}
        required={input.required}
        select
        size="small"
        value={String(Boolean(value))}
        onChange={(event) => onChange(event.target.value === 'true')}
      >
        <MenuItem value="false">False</MenuItem>
        <MenuItem value="true">True</MenuItem>
      </TextField>
    );
  }

  return (
    <TextField
      fullWidth
      helperText={helperText}
      label={label}
      multiline={fieldType === 'textarea' || fieldType === 'code'}
      placeholder={input.placeholder}
      required={input.required}
      size="small"
      type={fieldType === 'number' ? 'number' : fieldType === 'password' ? 'password' : 'text'}
      value={String(value ?? '')}
      onChange={(event) => onChange(fieldType === 'number' ? Number(event.target.value) : event.target.value)}
    />
  );
}

function LeadCapture({
  config,
  draft,
  translate,
  onChange,
  onSubmit
}: {
  config: FlowiseLeadCaptureConfig;
  draft: LeadDraft;
  translate: (key: string) => string;
  onChange: (draft: LeadDraft) => void;
  onSubmit: () => void;
}) {
  const enabledContactValues = [
    config.email ? draft.email : '',
    config.phone ? draft.phone : '',
    !config.email && !config.phone && config.name ? draft.name : ''
  ];
  const missingContact = enabledContactValues.every((value) => !value.trim());
  return (
    <div className="flowise-chat-lead">
      <strong>{config.title || translate(flowiseI18nKeys.detail.leadCapture)}</strong>
      {config.name ? <TextField size="small" placeholder={translate(flowiseI18nKeys.fields.name)} value={draft.name} onChange={(event) => onChange({ ...draft, name: event.target.value })} /> : null}
      {config.email ? <TextField size="small" placeholder={translate(flowiseI18nKeys.fields.email)} value={draft.email} onChange={(event) => onChange({ ...draft, email: event.target.value })} /> : null}
      {config.phone ? <TextField size="small" placeholder={translate(flowiseI18nKeys.fields.phone)} value={draft.phone} onChange={(event) => onChange({ ...draft, phone: event.target.value })} /> : null}
      <Button disabled={missingContact} startIcon={<AppIcon name="user" />} variant="outlined" onClick={onSubmit}>
        {translate(flowiseI18nKeys.actions.submit)}
      </Button>
    </div>
  );
}

function resolveStoredChatId(resourceId: string): string {
  const stored = localStorage.getItem(chatIdStorageKey(resourceId));
  if (stored) {
    return stored;
  }

  const next = createChatId();
  persistChatId(resourceId, next);
  return next;
}

function persistChatId(resourceId: string, chatId: string): void {
  localStorage.setItem(chatIdStorageKey(resourceId), chatId);
}

function createChatId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `chat-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function createEmptyMessagesResult() {
  return {
    code: 200,
    data: {
      items: [],
      total: 0
    },
    message: 'success',
    traceId: ''
  };
}

function leadMatchesChat(lead: FlowiseLeadDto, chatId: string): boolean {
  try {
    return JSON.parse(lead.contactJson).chatId === chatId;
  } catch {
    return false;
  }
}

function resolveLeadCaptureConfig(chatbotConfig?: string | null): FlowiseLeadCaptureConfig | null {
  const parsed = parseJsonObject(chatbotConfig);
  const leads = parsed?.leads;
  if (!leads || typeof leads !== 'object') {
    return null;
  }

  const record = leads as Record<string, unknown>;
  if (record.status !== true) {
    return null;
  }

  return {
    email: record.email === true,
    name: record.name === true,
    phone: record.phone === true,
    successMessage: typeof record.successMessage === 'string' ? record.successMessage : '',
    title: typeof record.title === 'string' ? record.title : ''
  };
}

function resolveChatFeedbackEnabled(chatbotConfig?: string | null): boolean {
  const parsed = parseJsonObject(chatbotConfig);
  const chatFeedback = parsed?.chatFeedback;
  return Boolean(chatFeedback && typeof chatFeedback === 'object' && (chatFeedback as Record<string, unknown>).status === true);
}

function resolveFileUploadEnabled(chatbotConfig?: string | null): boolean {
  const parsed = parseJsonObject(chatbotConfig);
  const fullFileUpload = parsed?.fullFileUpload;
  return Boolean(fullFileUpload && typeof fullFileUpload === 'object' && (fullFileUpload as Record<string, unknown>).status === true);
}

function resolveFollowUpPromptsEnabled(chatbotConfig?: string | null, followUpPromptsConfig?: string | null): boolean {
  const chatbot = parseJsonObject(chatbotConfig);
  const chatbotFollowUpPrompts = chatbot?.followUpPrompts;
  if (chatbotFollowUpPrompts && typeof chatbotFollowUpPrompts === 'object' && (chatbotFollowUpPrompts as Record<string, unknown>).status === true) {
    return true;
  }

  const config = parseJsonObject(followUpPromptsConfig);
  return Boolean(config?.status === true);
}

function resolveLatestFollowUpPrompts(messages: FlowiseChatMessageDto[]): string[] {
  const lastMessage = [...messages].reverse().find((message) => message.role === 'assistant' || message.role === 'system');
  return normalizeFollowUpPrompts(lastMessage?.followUpPrompts);
}

function normalizeFollowUpPrompts(value: unknown): string[] {
  if (!value) {
    return [];
  }

  if (typeof value === 'string') {
    try {
      return normalizeFollowUpPrompts(JSON.parse(value));
    } catch {
      return [];
    }
  }

  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map((item) => (typeof item === 'string' ? item : ''))
    .map((item) => item.trim())
    .filter(Boolean)
    .slice(0, 6);
}

function resolveStarterPrompts(chatbotConfig?: string | null): string[] {
  const parsed = parseJsonObject(chatbotConfig);
  const value = parsed?.starterPrompts;
  if (!value) {
    return [];
  }

  const prompts = Array.isArray(value) ? value : Object.values(value as Record<string, unknown>);
  return prompts
    .map((item) => {
      if (typeof item === 'string') {
        return item;
      }

      if (item && typeof item === 'object' && 'prompt' in item && typeof (item as { prompt?: unknown }).prompt === 'string') {
        return (item as { prompt: string }).prompt;
      }

      return '';
    })
    .map((item) => item.trim())
    .filter(Boolean)
    .slice(0, 12);
}

function resolveStartFormConfig(flowData?: string | null): FlowiseStartFormConfig | null {
  const parsed = parseJsonObject(flowData);
  const nodes = Array.isArray(parsed?.nodes) ? parsed.nodes : [];
  const startNode = nodes.find((node) => {
    if (!node || typeof node !== 'object') {
      return false;
    }

    const data = (node as { data?: unknown }).data;
    return Boolean(data && typeof data === 'object' && (data as { name?: unknown }).name === 'startAgentflow');
  });
  if (!startNode || typeof startNode !== 'object') {
    return null;
  }

  const data = (startNode as { data?: unknown }).data;
  const inputs = data && typeof data === 'object' ? (data as { inputs?: unknown }).inputs : null;
  if (!inputs || typeof inputs !== 'object') {
    return null;
  }

  const record = inputs as Record<string, unknown>;
  if (record.startInputType !== 'formInput' || !Array.isArray(record.formInputTypes)) {
    return null;
  }

  const formInputs = record.formInputTypes
    .map(normalizeStartFormInput)
    .filter((item): item is FlowiseStartFormInputParam => Boolean(item));
  if (formInputs.length === 0) {
    return null;
  }

  return {
    description: typeof record.formDescription === 'string' ? record.formDescription : '',
    inputs: formInputs,
    title: typeof record.formTitle === 'string' ? record.formTitle : ''
  };
}

function normalizeStartFormInput(value: unknown): FlowiseStartFormInputParam | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const record = value as Record<string, unknown>;
  const name = typeof record.name === 'string' ? record.name.trim() : '';
  if (!name) {
    return null;
  }

  return {
    addOptions: Array.isArray(record.addOptions) ? (record.addOptions as Array<{ option?: string }>) : undefined,
    description: typeof record.description === 'string' ? record.description : undefined,
    label: typeof record.label === 'string' ? record.label : undefined,
    name,
    options: Array.isArray(record.options) ? (record.options as Array<{ label?: string; name?: string; value?: string }>) : undefined,
    placeholder: typeof record.placeholder === 'string' ? record.placeholder : undefined,
    required: record.required === true,
    type: typeof record.type === 'string' ? record.type : undefined
  };
}

function resolveStartFormOptions(input: FlowiseStartFormInputParam): Array<{ label: string; value: string }> {
  const rawOptions = input.options && input.options.length > 0
    ? input.options.map((option) => option.value ?? option.name ?? option.label ?? '')
    : input.addOptions?.map((option) => option.option ?? '') ?? [];

  return rawOptions
    .map((option) => option.trim())
    .filter(Boolean)
    .map((option) => ({ label: option, value: option }));
}

function hasEnabledFeatureConfig(config?: string | null): boolean {
  const parsed = parseJsonObject(config);
  if (!parsed) {
    return false;
  }

  if (readBooleanStatus(parsed)) {
    return true;
  }

  return Object.values(parsed).some((value) => Boolean(value && typeof value === 'object' && readBooleanStatus(value as Record<string, unknown>)));
}

function readBooleanStatus(record: Record<string, unknown>): boolean {
  return record.status === true || record.enabled === true || record.isEnabled === true;
}

function parseJsonObject(value?: string | null): Record<string, unknown> | null {
  if (!value?.trim()) {
    return null;
  }

  try {
    const parsed = JSON.parse(value);
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

function formatSourceMeta(document: FlowiseSourceDocumentDto): string {
  const parts = [document.sourceId, document.score == null ? null : String(document.score)].filter(Boolean);
  return parts.join(' / ');
}

function formatJson(json: string): string {
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
}

function hasJsonArrayItems(json: string): boolean {
  try {
    const value = JSON.parse(json);
    return Array.isArray(value) && value.length > 0;
  } catch {
    return false;
  }
}

function hasJsonObjectItems(json: string): boolean {
  try {
    const value = JSON.parse(json);
    return Boolean(value && typeof value === 'object' && !Array.isArray(value) && Object.keys(value).length > 0);
  } catch {
    return false;
  }
}

function readUploadFile(file: File): Promise<FlowisePredictionUpload> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error());
    reader.onload = () =>
      resolve({
        data: String(reader.result ?? ''),
        mime: file.type || 'application/octet-stream',
        name: file.name,
        type: resolveUploadType(file.type)
      });
    reader.readAsDataURL(file);
  });
}

function resolveUploadType(mime: string): string {
  if (mime.startsWith('image/')) {
    return 'file:image';
  }

  if (mime.startsWith('audio/')) {
    return 'audio';
  }

  return 'file:full';
}

function isStreamMetadata(value: unknown): value is { chatId: string; message?: { followUpPrompts?: unknown } } {
  return Boolean(value && typeof value === 'object' && 'chatId' in value && typeof (value as { chatId?: unknown }).chatId === 'string');
}

function createStreamHttpError(value: unknown, fallbackMessage: string): HttpError {
  const payload = isFlowiseStreamErrorPayload(value) ? value : null;
  const message = payload?.message?.trim() || fallbackMessage;
  return new HttpError({
    code: undefined,
    data: value,
    message: payload?.errorCode?.trim() ? `${payload.errorCode.trim()}: ${message}` : message,
    status: 500,
    traceId: payload?.traceId?.trim() || undefined
  });
}

function isFlowiseStreamErrorPayload(value: unknown): value is FlowisePredictionStreamErrorPayload {
  return Boolean(
    value &&
      typeof value === 'object' &&
      ('message' in value || 'traceId' in value || 'errorCode' in value)
  );
}
