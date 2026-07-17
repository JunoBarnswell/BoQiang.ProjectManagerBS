import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

describe('latest-only runtime preview semantic guard', () => {
  it('covers the PageStudio preview kernel and real permission context', () => {
    const pageStudio = readSource('frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio/page-studio/PageStudioHost.tsx');
    const canvas = readSource('frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio/canvas/DesignerCanvas.tsx');

    expect(pageStudio).toContain('compileApplicationDevelopmentPreviewArtifact');
    expect(pageStudio).toContain('parseRuntimePageArtifact');
    expect(pageStudio).toContain('toRuntimeArtifact');
    expect(pageStudio).toContain('await onSave(nextDocument)');
    expect(pageStudio).not.toContain('durationMs: 0');
    expect(pageStudio).toContain('RuntimeKernel.create');
    expect(pageStudio).toContain('new Set(permissionCodes)');
    expect(pageStudio).toContain('isSystemAdmin: false');
    expect(pageStudio).not.toContain('isSystemAdmin: true');
    expect(pageStudio).not.toContain('DesignerRuntimeRenderer');
    expect(pageStudio).not.toContain('PageRenderer');
    expect(canvas).toContain('previewKernel');
    expect(canvas).not.toContain('new BindingResolver');
    expect(canvas).not.toContain('new LayoutResolver');
    expect(canvas).not.toContain('new PermissionEvaluator');
  });

  it('covers the single formal compile path and rejects retired preview compilers', () => {
    const pageStudio = readSource('frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio/page-studio/PageStudioHost.tsx');
    const publishCompiler = readSource('backend/AsterERP.Api/Application/ApplicationDevelopmentCenter/ApplicationDevelopmentSchemaCompiler.cs');

    expect(pageStudio).not.toContain('DesignerRuntimeArtifactCompiler');
    expect(pageStudio).not.toContain('shadowRender');
    expect(publishCompiler).toContain('ApplicationDevelopmentSchemaCompiler');
    expect(publishCompiler).toContain('RuntimeArtifactContractValidator.Validate(schema)');
  });
});

function readSource(relativePath: string): string {
  const path = join(findRepositoryRoot(), relativePath.replaceAll('/', '\\'));
  if (!existsSync(path)) throw new Error(`Required latest-only audit source is missing: ${relativePath}`);
  return readFileSync(path, 'utf8');
}

function findRepositoryRoot(): string {
  let directory = resolve(process.cwd());
  while (directory !== dirname(directory)) {
    if (existsSync(join(directory, 'AsterERP.sln'))) return directory;
    directory = dirname(directory);
  }
  throw new Error('AsterERP.sln was not found for the latest-only runtime audit.');
}
