export type RuntimeActionErrorKind = 'cancelled' | 'timeout' | 'failed' | 'unknown' | 'permissionDenied';

export class RuntimeActionError extends Error {
  public constructor(
    message: string,
    public readonly kind: RuntimeActionErrorKind,
    public readonly actionId: string,
    public readonly actionType: string,
    public readonly timeoutMs: number,
    public readonly cause?: unknown
  ) {
    super(message);
    this.name = 'RuntimeActionError';
  }
}
