import { isHttpError } from '../../core/http/httpError';

export interface FormattedHttpError {
  code?: number;
  details: string[];
  displayMessage: string;
  fieldErrors: Record<string, string[]>;
  message: string;
  status?: number;
  traceId?: string;
}

export function formatHttpError(error: unknown, fallback: string): FormattedHttpError {
  if (isHttpError(error)) {
    const message = error.message.trim() || fallback;
    return {
      code: error.code,
      details: error.details,
      displayMessage: appendTrace(message, error.traceId),
      fieldErrors: error.fieldErrors,
      message,
      status: error.status,
      traceId: error.traceId
    };
  }

  if (error instanceof Error && error.message.trim().length > 0) {
    return {
      details: [],
      displayMessage: error.message,
      fieldErrors: {},
      message: error.message
    };
  }

  if (typeof error === 'string' && error.trim().length > 0) {
    return {
      details: [],
      displayMessage: error,
      fieldErrors: {},
      message: error
    };
  }

  return {
    details: [],
    displayMessage: fallback,
    fieldErrors: {},
    message: fallback
  };
}

export function formatHttpErrorMessage(error: unknown, fallback: string): string {
  return formatHttpError(error, fallback).displayMessage;
}

function appendTrace(message: string, traceId?: string): string {
  return traceId ? `${message}（TraceId: ${traceId}）` : message;
}
