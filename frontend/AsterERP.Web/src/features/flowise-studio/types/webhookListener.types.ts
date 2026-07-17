export interface FlowiseWebhookListenerRegistrationDto {
  chatflowId: string;
  createdAt: string;
  listenerId: string;
}

export interface FlowiseWebhookTriggerRequest {
  chatId?: string | null;
  inputJson: string;
  question?: string | null;
  sessionId?: string | null;
}

export interface FlowiseWebhookTriggerResponse {
  chatflowId: string;
  listenerId: string;
  status: string;
  traceId: string;
}

export interface FlowiseWebhookStreamEvent {
  data: unknown;
  event: string;
}
