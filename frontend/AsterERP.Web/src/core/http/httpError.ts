export type HttpErrorKind = 'api-result' | 'problem-details' | 'validation' | 'network' | 'timeout' | 'unknown';

export class HttpError extends Error {
  public readonly code?: number;

  public readonly data?: unknown;

  public readonly details: string[];

  public readonly fieldErrors: Record<string, string[]>;

  public readonly kind: HttpErrorKind;

  public readonly raw?: unknown;

  public readonly traceId?: string;

  public readonly status: number;

  constructor(options: {
    code?: number;
    data?: unknown;
    details?: string[];
    fieldErrors?: Record<string, string[]>;
    kind?: HttpErrorKind;
    message: string;
    raw?: unknown;
    status: number;
    traceId?: string;
  }) {
    super(options.message);
    this.name = 'HttpError';
    this.code = options.code;
    this.data = options.data;
    this.details = options.details ?? [];
    this.fieldErrors = options.fieldErrors ?? {};
    this.kind = options.kind ?? 'unknown';
    this.raw = options.raw;
    this.traceId = options.traceId;
    this.status = options.status;
  }
}

export function isHttpError(error: unknown): error is HttpError {
  return error instanceof HttpError;
}
