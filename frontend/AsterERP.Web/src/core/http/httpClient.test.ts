import { describe, expect, it } from 'vitest';

import { normalizeHttpErrorPayload } from './httpClient';

function response(status: number, traceId = 'trace-from-header'): Response {
  return new Response(null, {
    headers: {
      'X-Trace-Id': traceId
    },
    status
  });
}

describe('normalizeHttpErrorPayload', () => {
  it('uses ApiResult message and trace id first', () => {
    const normalized = normalizeHttpErrorPayload(response(401), {
      code: 41001,
      data: null,
      message: '密码错误',
      traceId: 'trace-from-body'
    }, '请求失败，状态码 401');

    expect(normalized.kind).toBe('api-result');
    expect(normalized.code).toBe(41001);
    expect(normalized.message).toBe('密码错误');
    expect(normalized.traceId).toBe('trace-from-body');
  });

  it('reads ABP remote service errors without falling back to status text', () => {
    const normalized = normalizeHttpErrorPayload(response(500), {
      error: {
        details: 'password verification failed',
        message: '密码错误',
        validationErrors: [
          { members: ['password'], message: '密码不能为空' }
        ]
      }
    }, '请求失败，状态码 500');

    expect(normalized.kind).toBe('validation');
    expect(normalized.message).toBe('密码错误');
    expect(normalized.details).toEqual(['password verification failed']);
    expect(normalized.fieldErrors).toEqual({ password: ['密码不能为空'] });
  });

  it('extracts ValidationProblemDetails field errors', () => {
    const normalized = normalizeHttpErrorPayload(response(400), {
      errors: {
        Password: ['密码不能为空'],
        UserName: ['用户名不能为空']
      },
      title: '参数校验失败',
      traceId: 'trace-validation'
    }, '请求失败，状态码 400');

    expect(normalized.kind).toBe('validation');
    expect(normalized.message).toBe('参数校验失败');
    expect(normalized.fieldErrors).toEqual({
      Password: ['密码不能为空'],
      UserName: ['用户名不能为空']
    });
    expect(normalized.traceId).toBe('trace-validation');
  });

  it('uses ProblemDetails detail or title', () => {
    const normalized = normalizeHttpErrorPayload(response(409), {
      detail: '菜单编码已存在',
      title: 'Conflict'
    }, '请求失败，状态码 409');

    expect(normalized.kind).toBe('problem-details');
    expect(normalized.message).toBe('菜单编码已存在');
  });

  it('falls back only when the payload has no usable message', () => {
    const normalized = normalizeHttpErrorPayload(response(500), {}, '请求失败，状态码 500');

    expect(normalized.kind).toBe('unknown');
    expect(normalized.message).toBe('请求失败，状态码 500');
  });
});
