import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ProjectManagementExcelImportPanel } from './ProjectManagementExcelImportPanel';

vi.mock('../../../core/auth/usePermission', () => ({
  usePermission: () => ({ hasPermission: true }),
}));

vi.mock('../../../core/query/useApiMutation', () => ({
  useApiMutation: () => ({ isPending: false, mutate: vi.fn() }),
}));

vi.mock('../../../shared/feedback/useMessage', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}));

describe('ProjectManagementExcelImportPanel', () => {
  it('renders template download, upload preview and permission-gated controls', () => {
    render(<ProjectManagementExcelImportPanel />);

    expect(screen.getByRole('button', { name: '下载 Excel 模板' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '上传并预览' })).toBeTruthy();
    expect(screen.getByLabelText('选择 Excel 文件')).toBeTruthy();
  });
});
