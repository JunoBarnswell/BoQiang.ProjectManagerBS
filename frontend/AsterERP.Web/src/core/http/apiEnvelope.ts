export interface ApiEnvelope<T> {
  code: number;
  message: string;
  data: T;
  traceId: string;
  messageKey?: string;
  messageArguments?: Record<string, string>;
}
