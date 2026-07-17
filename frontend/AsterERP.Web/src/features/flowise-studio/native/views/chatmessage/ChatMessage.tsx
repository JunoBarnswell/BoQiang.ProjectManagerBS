import { Button, TextField } from '@mui/material';
import type { ReactNode } from 'react';


import { AppIcon } from '../../../../../shared/icons/AppIcon';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseChatMessageDto } from '../../../types/prediction.types';

interface ChatMessageProps {
  chatFeedbackEnabled: boolean;
  feedbackReason: string;
  message: FlowiseChatMessageDto;
  speaking: boolean;
  textToSpeechEnabled: boolean;
  translate: (key: string) => string;
  renderAgentExecutedData: (message: FlowiseChatMessageDto) => ReactNode;
  renderAgentReasoning: (message: FlowiseChatMessageDto) => ReactNode;
  renderArtifacts: (message: FlowiseChatMessageDto) => ReactNode;
  renderFileUploads: (message: FlowiseChatMessageDto) => ReactNode;
  renderSourceDocuments: (message: FlowiseChatMessageDto) => ReactNode;
  renderUsedTools: (message: FlowiseChatMessageDto) => ReactNode;
  onFeedback: (messageId: string, rating: 'down' | 'up') => void;
  onReasonChange: (value: string) => void;
  onToggleSpeech: (message: FlowiseChatMessageDto) => void;
}

export function ChatMessage({
  chatFeedbackEnabled,
  feedbackReason,
  message,
  renderAgentExecutedData,
  renderAgentReasoning,
  renderArtifacts,
  renderFileUploads,
  renderSourceDocuments,
  renderUsedTools,
  speaking,
  textToSpeechEnabled,
  translate,
  onFeedback,
  onReasonChange,
  onToggleSpeech
}: ChatMessageProps) {
  const assistant = message.role === 'assistant';

  return (
    <article className={`flowise-chat-bubble flowise-chat-bubble--${assistant ? 'assistant' : 'user'}`}>
      <pre>{message.message}</pre>
      {renderFileUploads(message)}
      {assistant ? (
        <div className="flowise-chat-bubble__tools">
          {chatFeedbackEnabled ? (
            <>
              <Button className={message.feedback?.rating === 'up' ? 'is-active' : undefined} size="small" startIcon={<AppIcon name="check" />} variant="text" onClick={() => onFeedback(message.id, 'up')}>
                {translate(flowiseI18nKeys.detail.feedback)}
              </Button>
              <Button className={message.feedback?.rating === 'down' ? 'is-active' : undefined} size="small" startIcon={<AppIcon name="x" />} variant="text" onClick={() => onFeedback(message.id, 'down')}>
                {translate(flowiseI18nKeys.detail.feedback)}
              </Button>
            </>
          ) : null}
          {textToSpeechEnabled ? (
            <Button className={speaking ? 'is-active' : undefined} size="small" startIcon={<AppIcon name={speaking ? 'stop' : 'volume'} />} variant="text" onClick={() => onToggleSpeech(message)}>
              {speaking ? translate(flowiseI18nKeys.actions.stopSpeech) : translate(flowiseI18nKeys.actions.playSpeech)}
            </Button>
          ) : null}
        </div>
      ) : null}
      {assistant && chatFeedbackEnabled ? (
        <TextField
          className="flowise-chat-feedback-reason"
          label={translate(flowiseI18nKeys.detail.feedbackReason)}
          size="small"
          value={feedbackReason}
          onChange={(event) => onReasonChange(event.target.value)}
        />
      ) : null}
      {assistant ? renderAgentReasoning(message) : null}
      {assistant ? renderAgentExecutedData(message) : null}
      {assistant ? renderUsedTools(message) : null}
      {assistant ? renderArtifacts(message) : null}
      {assistant ? renderSourceDocuments(message) : null}
    </article>
  );
}
