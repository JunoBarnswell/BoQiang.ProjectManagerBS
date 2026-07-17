export interface ApiEnvelope<T> {
  code: number;
  message: string;
  data: T;
  traceId: string;
}
