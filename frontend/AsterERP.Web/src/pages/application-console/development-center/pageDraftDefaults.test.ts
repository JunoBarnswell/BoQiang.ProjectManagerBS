import { describe, expect, it } from 'vitest';

import { buildPageDraftRequest, createPageCodeFromName, createPageFormWithName } from './pageDraftDefaults';

describe('pageDraftDefaults', () => {
  it('generates a safe readonly page code from an ascii page name', () => {
    expect(createPageCodeFromName('Inbound Order List', 'abc123')).toBe('inbound_order_list_abc123');
  });

  it('falls back to a page prefix for non-ascii names', () => {
    expect(createPageCodeFromName('入库明细', 'mr7w7nvj')).toBe('page_mr7w7nvj');
  });

  it('builds the minimal backend page create request', () => {
    const form = createPageFormWithName('入库明细', 'mr7w7nvj');
    const request = buildPageDraftRequest({
      form,
      moduleId: 'module-1',
      versionId: 'version-1'
    });

    expect(request).toEqual({
      moduleId: 'module-1',
      pageCode: 'page_mr7w7nvj',
      pageName: '入库明细',
      pageParameters: [],
      pageType: 'standard',
      parentPageId: undefined,
      sortOrder: 0,
      versionId: 'version-1'
    });
  });
});
