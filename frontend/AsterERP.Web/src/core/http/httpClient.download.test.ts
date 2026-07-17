// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest';

import { HttpClient } from './httpClient';

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
  vi.restoreAllMocks();
});

describe('HttpClient.postDownloadBlob', () => {
  it('posts JSON and returns the streamed blob with the server filename', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('id,name\n1,alpha\n', {
      headers: {
        'Content-Disposition': "attachment; filename*=UTF-8''rows.csv",
        'Content-Type': 'text/csv; charset=utf-8'
      },
      status: 200
    }));
    globalThis.fetch = fetchMock;

    const response = await new HttpClient().postDownloadBlob(
      '/application-data-center/data-sources/source/tables/orders/rows/export/stream',
      { limit: 100_000 },
      { auth: false, workspace: false }
    );

    expect(fetchMock).toHaveBeenCalledOnce();
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(init.method).toBe('POST');
    expect(init.body).toBe(JSON.stringify({ limit: 100_000 }));
    expect(new Headers(init.headers).get('Accept')).toBe('text/csv');
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json');
    expect(response.fileName).toBe('rows.csv');
    await expect(response.blob.text()).resolves.toBe('id,name\n1,alpha\n');
  });

  it('sends the CSRF cookie token and includes credentials for cookie sessions', async () => {
    document.cookie = 'astererp_csrf=csrf-token; Path=/';
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      code: 200,
      data: true,
      message: 'success',
      traceId: ''
    }), {
      headers: { 'Content-Type': 'application/json' },
      status: 200
    }));
    globalThis.fetch = fetchMock;

    await new HttpClient().request({
      auth: false,
      body: { name: 'page' },
      method: 'PUT',
      path: '/application-development-center/pages/page-1',
      workspace: false
    });

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(init.credentials).toBe('include');
    expect(new Headers(init.headers).get('X-CSRF-Token')).toBe('csrf-token');
  });
});
