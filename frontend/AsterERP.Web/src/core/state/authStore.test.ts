// @vitest-environment jsdom

import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { SessionResponseDto } from '../../api/platform/auth.types';
import { setApplicationAccessToken } from '../http/tokenStorage';

import { useAuthStore } from './authStore';
import { useMenuStore } from './menuStore';
import { usePermissionStore } from './permissionStore';
import { useTabStore } from './tabStore';
import { useWorkspaceStore } from './workspaceStore';

const getSessionMock = vi.fn<() => Promise<{ data: SessionResponseDto }>>();

vi.mock('../../api/platform/auth.api', () => ({
  applicationLogin: vi.fn(),
  getSession: () => getSessionMock(),
  login: vi.fn(),
  switchPlatformWorkspace: vi.fn(),
  switchWorkspace: vi.fn()
}));

describe('authStore', () => {
  beforeEach(() => {
    getSessionMock.mockReset();
    localStorage.clear();
    setApplicationAccessToken('test-token');
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      user: null
    });
    useMenuStore.setState({ menus: [] });
    usePermissionStore.setState({ permissionCodes: [] });
    useWorkspaceStore.setState({
      availableWorkspaces: [],
      branding: null,
      currentWorkspace: null
    });
    useTabStore.setState({
      activeWorkspaceKey: 'tenant-a:MES:application',
      openTabs: [
        {
          id: 'designer-tab',
          label: '页面设计工作区',
          path: '/tenants/tenant-a/apps/MES/admin/development-center/pages'
        }
      ],
      pageCache: {},
      workspaceTabGroups: {
        'tenant-a:MES:application': [
          {
            id: 'designer-tab',
            label: '页面设计工作区',
            path: '/tenants/tenant-a/apps/MES/admin/development-center/pages'
          }
        ]
      }
    });
  });

  it('refreshes menus and permissions without resetting open tabs when preserveTabs is enabled', async () => {
    getSessionMock.mockResolvedValue({
      data: createSession()
    });

    await useAuthStore.getState().refreshSession({ preserveTabs: true });

    expect(useMenuStore.getState().menus).toHaveLength(1);
    expect(useMenuStore.getState().menus[0]?.menuCode).toBe('app-module:inventory');
    expect(usePermissionStore.getState().permissionCodes).toContain('app:runtime:inventory:view');
    expect(useWorkspaceStore.getState().availableWorkspaces).toEqual([
      expect.objectContaining({
        appCode: 'WMS',
        isDatabaseBound: false,
        canManageInitialDatabaseBinding: true
      })
    ]);
    expect(useTabStore.getState().openTabs).toEqual([
      {
        id: 'designer-tab',
        label: '页面设计工作区',
        path: '/tenants/tenant-a/apps/MES/admin/development-center/pages'
      }
    ]);
  });
});

function createSession(): SessionResponseDto {
  return {
    availableWorkspaces: [
      {
        appCode: 'WMS',
        appName: '仓储管理',
        canManageInitialDatabaseBinding: true,
        description: null,
        defaultRoutePath: '/tenants/tenant-a/apps/WMS/admin/home',
        disabledReason: null,
        isAvailable: true,
        isDatabaseBound: false,
        isDefault: false,
        logoFileId: null,
        status: 'Enabled',
        systemCode: 'WMS',
        systemId: 'tenant-a:WMS',
        systemName: '客户A 仓储管理',
        tenantId: 'tenant-a',
        tenantName: '客户A',
        workspaceId: 'tenant-a:WMS',
        workspaceLevel: 'application'
      }
    ],
    branding: {
      primaryColor: '#2563eb',
      systemName: 'MES'
    },
    currentWorkspace: {
      appCode: 'MES',
      appName: 'MES',
      systemCode: 'MES',
      systemId: 'system-mes',
      systemName: 'MES',
      tenantId: 'tenant-a',
      tenantName: '客户A',
      workspaceId: 'tenant-a:MES',
      workspaceLevel: 'application'
    },
    menus: [
      {
        children: [],
        appCode: 'MES',
        componentName: null,
        configJson: null,
        icon: 'FolderTree',
        id: 'menu-inventory',
        menuCode: 'app-module:inventory',
        menuName: '库存管理',
        menuType: 'Directory',
        pageCode: null,
        artifactId: null,
        permissionCode: null,
        routePath: null,
        scopeType: 'ApplicationRuntime',
        sortOrder: 1,
        tenantId: 'tenant-a',
        visible: true
      }
    ],
    permissionCodes: ['app:runtime:inventory:view'],
    user: {
      dataScope: 'All',
      displayName: 'System Admin',
      isAdmin: true,
      isPlatformAdmin: true,
      isTenantAdmin: true,
      permissionCodes: ['app:runtime:inventory:view'],
      roleIds: [],
      userId: 'admin',
      userName: 'admin'
    }
  };
}
