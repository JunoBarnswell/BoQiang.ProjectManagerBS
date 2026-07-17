import { readFileSync } from 'node:fs';

import { describe, expect, it } from 'vitest';

import { isApplicationDevelopmentDraftConflictCode } from '../../../api/application-development-center/applicationDevelopmentCenter.types';

describe('isApplicationDevelopmentDraftConflictCode', () => {
  it('recognizes the application draft conflict code', () => {
    expect(isApplicationDevelopmentDraftConflictCode(42301, 400)).toBe(true);
  });

  it('recognizes an HTTP 409 even when the gateway omits the domain code', () => {
    expect(isApplicationDevelopmentDraftConflictCode(undefined, 409)).toBe(true);
  });

  it('does not classify unrelated failures as conflicts', () => {
    expect(isApplicationDevelopmentDraftConflictCode(50001, 400)).toBe(false);
    expect(isApplicationDevelopmentDraftConflictCode(undefined, 500)).toBe(false);
  });
});

describe('Page Studio document ownership', () => {
  const pageListSource = readFileSync(new URL('./ApplicationDevelopmentPagesPage.tsx', import.meta.url), 'utf8');
  const pageSource = readFileSync(new URL('./DevelopmentCenterDesignerPage.tsx', import.meta.url), 'utf8');
  const hostSource = readFileSync(new URL('./low-code-studio/page-studio/PageStudioHost.tsx', import.meta.url), 'utf8');

  it('routes the development-center page list through the latest page resource chain', () => {
    expect(pageListSource).toContain('getApplicationDevelopmentWorkspace');
    expect(pageListSource).toContain('createApplicationDevelopmentPage');
    expect(pageListSource).toContain('publishApplicationDevelopmentPage');
    expect(pageListSource).toContain('ApplicationDevelopmentPagesPage');
    expect(pageListSource).not.toContain('businessObjectApi');
    expect(pageListSource).not.toContain('schema.sections');
    expect(pageListSource).not.toContain('BusinessObjectDesignPage');
  });

  it('keeps the live document in the CommandBus and passes only the loaded seed into the host', () => {
    expect(pageSource).toContain('initialDocument={document}');
    expect(pageSource).not.toContain('onDocumentChange={setDocument}');
    expect(hostSource).toContain('initialDocument: DesignerDocument');
    expect(hostSource).toContain('new DesignerCommandBus(initialDocument,');
    expect(hostSource).toContain('const currentDocument = useSyncExternalStore');
  });

  it('does not mirror CommandBus edits back into the parent document state', () => {
    expect(hostSource).not.toContain('onDocumentChange');
    expect(hostSource).not.toContain('notifyDocument');
    expect(hostSource).not.toContain('onCommandResult={(result) =>');
  });
});
