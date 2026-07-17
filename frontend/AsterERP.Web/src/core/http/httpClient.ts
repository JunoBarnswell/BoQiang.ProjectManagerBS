import { appEnv } from '../config/env';
import { formatMessage } from '../i18n/formatMessage';
import { translateCurrentLocale } from '../i18n/I18nProvider';

import type { ApiEnvelope } from './apiEnvelope';
import { dispatchAuthExpired } from './authEvents';
import { HttpError, type HttpErrorKind } from './httpError';
import { parseJsonSseFrame, readSseStream, type SseFrame } from './sseClient';
import { getAccessToken } from './tokenStorage';
import { getStoredWorkspace } from './workspaceStorage';

export interface HttpRequestOptions {
  auth?: boolean;
  headers?: HeadersInit;
  signal?: AbortSignal;
  timeoutMs?: number;
  workspace?: boolean;
}

interface RequestOptions<TBody> extends HttpRequestOptions {
  body?: TBody;
  method: 'DELETE' | 'GET' | 'PATCH' | 'POST' | 'PUT';
  path: string;
}

interface StreamSseRequestOptions<TBody, TEvent> extends HttpRequestOptions {
  body?: TBody;
  errorMessage?: string;
  method?: 'GET' | 'POST';
  onEvent: (event: TEvent) => void;
  parseEvent?: (frame: SseFrame) => TEvent | null;
  path: string;
  traceId?: boolean;
}

function buildUrl(path: string): string {
  const baseUrl = appEnv.apiBaseUrl.replace(/\/+$/, '');
  const requestPath = path.startsWith('/') ? path : `/${path}`;
  return `${baseUrl}${requestPath}`;
}

function notifyAuthExpiredIfNeeded(path: string, status: number, authorizationHeader: string | null): void {
  if (status !== 401 || path === '/auth/login' || !authorizationHeader) {
    return;
  }

  if (authorizationHeader === `Bearer ${getAccessToken()}`) {
    dispatchAuthExpired();
  }
}

function isEnvelope<T>(value: unknown): value is ApiEnvelope<T> {
  return Boolean(
    value &&
      typeof value === 'object' &&
      'code' in value &&
      'message' in value &&
      'data' in value
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) {
    return null;
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

export class HttpClient {
  public async request<TResponse, TBody = undefined>(
    options: RequestOptions<TBody>
  ): Promise<ApiEnvelope<TResponse>> {
    const controller = new AbortController();
    const timeout = options.timeoutMs ?? appEnv.requestTimeoutMs;
    let timedOut = false;
    const timeoutHandle = window.setTimeout(() => {
      timedOut = true;
      controller.abort();
    }, timeout);
    const abortExternalRequest = () => controller.abort();

    if (options.signal?.aborted) {
      controller.abort();
    } else {
      options.signal?.addEventListener('abort', abortExternalRequest, { once: true });
    }

    try {
      const isFormDataBody = options.body instanceof FormData;
      const headers = buildRequestHeaders({
        accept: 'application/json',
        auth: options.auth,
        contentType: options.body !== undefined && !isFormDataBody ? 'application/json' : undefined,
        headers: options.headers,
        path: options.path,
        workspace: options.workspace
      });
      const authorizationHeader = headers.get('Authorization');

      let requestBody: BodyInit | undefined;
      if (isFormDataBody) {
        requestBody = options.body as FormData;
      } else if (options.body !== undefined) {
        requestBody = JSON.stringify(options.body);
      }

      const response = await fetch(buildUrl(options.path), {
        body: requestBody,
        credentials: 'include',
        headers,
        method: options.method,
        signal: controller.signal
      });

      const payload = await readJson(response);
      const envelope = isEnvelope<TResponse>(payload) ? payload : null;
      const traceId = envelope?.traceId ?? response.headers.get('X-Trace-Id') ?? '';

      if (!response.ok) {
        notifyAuthExpiredIfNeeded(options.path, response.status, authorizationHeader);
        throw createHttpError(response, payload, formatMessage(translateCurrentLocale('http.requestFailed'), { status: response.status }));
      }

      if (envelope) {
        if (envelope.code >= 400) {
          notifyAuthExpiredIfNeeded(options.path, response.status, authorizationHeader);
          throw new HttpError({
            code: envelope.code,
            data: envelope.data,
            kind: 'api-result',
            message: envelope.message,
            traceId: envelope.traceId,
            status: response.status
          });
        }

        return envelope;
      }

      return {
        code: response.status,
        data: payload as TResponse,
        message: response.ok ? 'success' : 'error',
        traceId
      };
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        if (!timedOut) {
          throw error;
        }

        throw new HttpError({ kind: 'timeout', message: translateCurrentLocale('http.timeout'), status: 408 });
      }

      if (error instanceof HttpError) {
        throw error;
      }

      throw new HttpError({ kind: 'network', message: translateCurrentLocale('http.networkFailed'), status: 0 });
    } finally {
      window.clearTimeout(timeoutHandle);
      options.signal?.removeEventListener('abort', abortExternalRequest);
    }
  }

  public get<TResponse>(
    path: string,
    timeoutMs?: number | HttpRequestOptions,
    signal?: AbortSignal
  ): Promise<ApiEnvelope<TResponse>> {
    return this.request<TResponse>({ ...normalizeRequestOptions(timeoutMs, signal), method: 'GET', path });
  }

  public post<TResponse, TBody>(
    path: string,
    body: TBody,
    timeoutMs?: number | HttpRequestOptions,
    signal?: AbortSignal
  ): Promise<ApiEnvelope<TResponse>> {
    return this.request<TResponse, TBody>({ ...normalizeRequestOptions(timeoutMs, signal), body, method: 'POST', path });
  }

  public postForm<TResponse>(
    path: string,
    body: FormData,
    timeoutMs?: number | HttpRequestOptions,
    signal?: AbortSignal
  ): Promise<ApiEnvelope<TResponse>> {
    return this.request<TResponse, FormData>({ ...normalizeRequestOptions(timeoutMs, signal), body, method: 'POST', path });
  }

  public put<TResponse, TBody>(
    path: string,
    body: TBody,
    timeoutMs?: number | HttpRequestOptions,
    signal?: AbortSignal
  ): Promise<ApiEnvelope<TResponse>> {
    return this.request<TResponse, TBody>({ ...normalizeRequestOptions(timeoutMs, signal), body, method: 'PUT', path });
  }

  public delete<TResponse>(
    path: string,
    timeoutMs?: number | HttpRequestOptions,
    signal?: AbortSignal
  ): Promise<ApiEnvelope<TResponse>> {
    return this.request<TResponse>({ ...normalizeRequestOptions(timeoutMs, signal), method: 'DELETE', path });
  }

  public async downloadBlob(
    path: string,
    timeoutMs?: number | HttpRequestOptions,
    signal?: AbortSignal
  ): Promise<{ blob: Blob; fileName: string }> {
    const requestOptions = normalizeRequestOptions(timeoutMs, signal);
    const controller = new AbortController();
    const timeout = requestOptions.timeoutMs ?? appEnv.requestTimeoutMs;
    let timedOut = false;
    const timeoutHandle = window.setTimeout(() => {
      timedOut = true;
      controller.abort();
    }, timeout);
    const abortExternalRequest = () => controller.abort();

    if (requestOptions.signal?.aborted) {
      controller.abort();
    } else {
      requestOptions.signal?.addEventListener('abort', abortExternalRequest, { once: true });
    }

    try {
      const headers = buildRequestHeaders({
        auth: requestOptions.auth,
        headers: requestOptions.headers,
        path,
        workspace: requestOptions.workspace
      });
      const authorizationHeader = headers.get('Authorization');

      const response = await fetch(buildUrl(path), {
        credentials: 'include',
        headers,
        method: 'GET',
        signal: controller.signal
      });

      if (!response.ok) {
        notifyAuthExpiredIfNeeded(path, response.status, authorizationHeader);
        throw createHttpError(response, await readJson(response), formatMessage(translateCurrentLocale('http.downloadFailed'), { status: response.status }));
      }

      const blob = await response.blob();
      return {
        blob,
        fileName: resolveDownloadFileName(response.headers.get('Content-Disposition')) ?? 'download.bin'
      };
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        if (!timedOut) {
          throw error;
        }

        throw new HttpError({ kind: 'timeout', message: translateCurrentLocale('http.timeout'), status: 408 });
      }

      if (error instanceof HttpError) {
        throw error;
      }

      throw new HttpError({ kind: 'network', message: translateCurrentLocale('http.networkFailed'), status: 0 });
    } finally {
      window.clearTimeout(timeoutHandle);
      requestOptions.signal?.removeEventListener('abort', abortExternalRequest);
    }
  }

  public async postDownloadBlob<TBody>(
    path: string,
    body: TBody,
    timeoutMs?: number | HttpRequestOptions,
    signal?: AbortSignal
  ): Promise<{ blob: Blob; fileName: string }> {
    const requestOptions = normalizeRequestOptions(timeoutMs, signal);
    const controller = new AbortController();
    const timeout = requestOptions.timeoutMs ?? appEnv.requestTimeoutMs;
    let timedOut = false;
    const timeoutHandle = window.setTimeout(() => {
      timedOut = true;
      controller.abort();
    }, timeout);
    const abortExternalRequest = () => controller.abort();

    if (requestOptions.signal?.aborted) {
      controller.abort();
    } else {
      requestOptions.signal?.addEventListener('abort', abortExternalRequest, { once: true });
    }

    try {
      const headers = buildRequestHeaders({
        accept: 'text/csv',
        auth: requestOptions.auth,
        contentType: 'application/json',
        headers: requestOptions.headers,
        path,
        workspace: requestOptions.workspace
      });
      const authorizationHeader = headers.get('Authorization');
      const response = await fetch(buildUrl(path), {
        body: JSON.stringify(body),
        credentials: 'include',
        headers,
        method: 'POST',
        signal: controller.signal
      });

      if (!response.ok) {
        notifyAuthExpiredIfNeeded(path, response.status, authorizationHeader);
        throw createHttpError(response, await readJson(response), formatMessage(translateCurrentLocale('http.downloadFailed'), { status: response.status }));
      }

      return {
        blob: await response.blob(),
        fileName: resolveDownloadFileName(response.headers.get('Content-Disposition')) ?? 'download.bin'
      };
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        if (!timedOut) throw error;
        throw new HttpError({ kind: 'timeout', message: translateCurrentLocale('http.timeout'), status: 408 });
      }
      if (error instanceof HttpError) throw error;
      throw new HttpError({ kind: 'network', message: translateCurrentLocale('http.networkFailed'), status: 0 });
    } finally {
      window.clearTimeout(timeoutHandle);
      requestOptions.signal?.removeEventListener('abort', abortExternalRequest);
    }
  }

  public async streamSse<TEvent, TBody = undefined>(
    options: StreamSseRequestOptions<TBody, TEvent>
  ): Promise<void> {
    const headers = buildRequestHeaders({
      accept: 'text/event-stream',
      auth: options.auth,
      contentType: options.body !== undefined ? 'application/json' : undefined,
      headers: options.headers,
      path: options.path,
      traceId: options.traceId,
      workspace: options.workspace
    });
    const authorizationHeader = headers.get('Authorization');

    const response = await fetch(buildUrl(options.path), {
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      credentials: 'include',
      headers,
      method: options.method ?? 'GET',
      signal: options.signal
    });

    if (!response.ok) {
      notifyAuthExpiredIfNeeded(options.path, response.status, authorizationHeader);
      throw createHttpError(response, await readJson(response), options.errorMessage ?? formatMessage(translateCurrentLocale('http.streamFailed'), { status: response.status }));
    }

    if (!response.body) {
      throw new HttpError({
        kind: 'unknown',
        message: options.errorMessage ?? translateCurrentLocale('http.streamEmpty'),
        status: response.status,
        traceId: response.headers.get('X-Trace-Id') ?? ''
      });
    }

    await readSseStream(response, (frame) => {
      const parsed = options.parseEvent
        ? options.parseEvent(frame)
        : parseJsonSseFrame<TEvent>(frame, frame.event);
      if (parsed) {
        options.onEvent(parsed);
      }
    });
  }
}

export const httpClient = new HttpClient();

function resolveDownloadFileName(contentDisposition: string | null): string | null {
  if (!contentDisposition) {
    return null;
  }

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }

  const asciiMatch = /filename="?([^";]+)"?/i.exec(contentDisposition);
  return asciiMatch?.[1] ?? null;
}

function buildRequestHeaders(options: {
  accept?: string;
  auth?: boolean;
  contentType?: string;
  headers?: HeadersInit;
  path: string;
  traceId?: boolean;
  workspace?: boolean;
}): Headers {
  const headers = new Headers(options.headers);

  if (options.accept && !headers.has('Accept')) {
    headers.set('Accept', options.accept);
  }

  if (options.contentType && !headers.has('Content-Type')) {
    headers.set('Content-Type', options.contentType);
  }

  if (options.traceId && !headers.has('X-Trace-Id')) {
    headers.set('X-Trace-Id', createTraceId());
  }

  if (options.auth !== false) {
    const token = getAccessToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
  }

  if (!headers.has('X-CSRF-Token')) {
    const csrfToken = readCookie('astererp_csrf');
    if (csrfToken) {
      headers.set('X-CSRF-Token', csrfToken);
    }
  }

  if (options.workspace !== false && options.path !== '/auth/login' && !options.path.startsWith('/application-auth/')) {
    const workspace = getStoredWorkspace();
    if (workspace) {
      headers.set('X-Tenant-Id', workspace.tenantId);
      headers.set('X-App-Code', workspace.appCode);
      headers.set('X-Workspace-Level', workspace.workspaceLevel);
    }
  }

  return headers;
}

function normalizeRequestOptions(timeoutMs?: number | HttpRequestOptions, signal?: AbortSignal): HttpRequestOptions {
  if (typeof timeoutMs === 'object' && timeoutMs !== null) {
    return signal ? { ...timeoutMs, signal } : timeoutMs;
  }

  return { signal, timeoutMs };
}

export function normalizeHttpErrorPayload(response: Response, payload: unknown, fallbackMessage: string): {
  code?: number;
  data?: unknown;
  details?: string[];
  fieldErrors?: Record<string, string[]>;
  kind: HttpErrorKind;
  message: string;
  raw?: unknown;
  traceId?: string;
} {
  const envelope = isEnvelope<unknown>(payload) ? payload : null;
  if (envelope) {
    return {
      code: envelope.code,
      data: envelope.data,
      kind: 'api-result',
      message: resolveMessage(envelope.message, fallbackMessage),
      traceId: envelope.traceId ?? response.headers.get('X-Trace-Id') ?? ''
    };
  }

  if (typeof payload === 'string') {
    return {
      kind: 'unknown',
      message: resolveMessage(payload, fallbackMessage),
      raw: payload,
      traceId: response.headers.get('X-Trace-Id') ?? ''
    };
  }

  if (!isRecord(payload)) {
    return {
      kind: 'unknown',
      message: fallbackMessage,
      raw: payload,
      traceId: response.headers.get('X-Trace-Id') ?? ''
    };
  }

  const abpError = isRecord(payload.error) ? payload.error : null;
  if (abpError) {
    const validationErrors = readAbpValidationErrors(abpError.validationErrors);
    return {
      code: readNumber(abpError.code),
      data: payload.data,
      details: readDetails(abpError.details),
      fieldErrors: validationErrors,
      kind: Object.keys(validationErrors).length > 0 ? 'validation' : 'problem-details',
      message: resolveMessage(readString(abpError.message), fallbackMessage),
      raw: payload,
      traceId: readString(payload.traceId) ?? response.headers.get('X-Trace-Id') ?? ''
    };
  }

  const validationProblemErrors = readValidationProblemErrors(payload.errors);
  if (Object.keys(validationProblemErrors).length > 0) {
    return {
      data: payload,
      fieldErrors: validationProblemErrors,
      kind: 'validation',
      message: resolveMessage(readString(payload.detail) ?? readString(payload.title), fallbackMessage),
      raw: payload,
      traceId: readString(payload.traceId) ?? response.headers.get('X-Trace-Id') ?? ''
    };
  }

  const problemMessage = readString(payload.detail) ?? readString(payload.title) ?? readString(payload.message);
  return {
    code: readNumber(payload.code),
    data: payload.data,
    details: readDetails(payload.details),
    kind: problemMessage ? 'problem-details' : 'unknown',
    message: resolveMessage(problemMessage, fallbackMessage),
    raw: payload,
    traceId: readString(payload.traceId) ?? response.headers.get('X-Trace-Id') ?? ''
  };
}

function createHttpError(response: Response, payload: unknown, fallbackMessage: string): HttpError {
  const normalized = normalizeHttpErrorPayload(response, payload, fallbackMessage);
  return new HttpError({
    code: normalized.code,
    data: normalized.data,
    details: normalized.details,
    fieldErrors: normalized.fieldErrors,
    kind: normalized.kind,
    message: normalized.message,
    raw: normalized.raw,
    traceId: normalized.traceId,
    status: response.status
  });
}

function readString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
}

function readNumber(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined;
}

function resolveMessage(value: string | undefined, fallback: string): string {
  return value?.trim() || fallback;
}

function readDetails(value: unknown): string[] {
  if (typeof value === 'string' && value.trim().length > 0) {
    return [value.trim()];
  }

  if (Array.isArray(value)) {
    return value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0).map((item) => item.trim());
  }

  return [];
}

function appendFieldError(target: Record<string, string[]>, field: string, message: string): void {
  const normalizedField = field.trim() || '_';
  const normalizedMessage = message.trim();
  if (!normalizedMessage) {
    return;
  }

  target[normalizedField] = [...(target[normalizedField] ?? []), normalizedMessage];
}

function readValidationProblemErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {};
  }

  const errors: Record<string, string[]> = {};
  Object.entries(value).forEach(([field, messages]) => {
    if (Array.isArray(messages)) {
      messages.forEach((message) => {
        if (typeof message === 'string') {
          appendFieldError(errors, field, message);
        }
      });
      return;
    }

    if (typeof messages === 'string') {
      appendFieldError(errors, field, messages);
    }
  });
  return errors;
}

function readAbpValidationErrors(value: unknown): Record<string, string[]> {
  if (!Array.isArray(value)) {
    return {};
  }

  const errors: Record<string, string[]> = {};
  value.forEach((item) => {
    if (!isRecord(item)) {
      return;
    }

    const message = readString(item.message);
    if (!message) {
      return;
    }

    const members = Array.isArray(item.members)
      ? item.members.filter((member): member is string => typeof member === 'string')
      : [];
    if (members.length === 0) {
      appendFieldError(errors, '_', message);
      return;
    }

    members.forEach((member) => appendFieldError(errors, member, message));
  });
  return errors;
}

function createTraceId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}`;
}

function readCookie(name: string): string | null {
  if (typeof document === 'undefined' || !document.cookie) {
    return null;
  }

  const cookie = document.cookie
    .split(';')
    .map((item) => item.trim())
    .find((item) => item.startsWith(`${name}=`));
  if (!cookie) {
    return null;
  }

  const value = cookie.slice(name.length + 1);
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}
