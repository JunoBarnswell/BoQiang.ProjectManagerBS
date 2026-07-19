import { useMemo } from 'react';
import { Link } from 'react-router-dom';

import type { MenuTreeNodeDto } from '../../api/system/system.types';
import { useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../../shared/icons/AppIcon';
import { resolveMenuLabel } from '../navigation/menuLabels';
import { formatWorkflowMenuGroupLabel, formatWorkflowMenuTitle, normalizeWorkflowMenuPath } from '../navigation/workflowMenuDisplay';

interface SidebarMenuProps {
  activePath: string;
  menuTree: MenuTreeNodeDto[];
  onClose: () => void;
  onNavigate: () => void;
  open: boolean;
  pinned: boolean;
  subtitle: string;
  title: string;
  onToggle: () => void;
  workspaceAppCode?: string | null;
  workspaceLevel?: 'application' | 'platform';
  workspaceTenantId?: string | null;
}

const iconAliases: Record<string, string> = {
  Activity: 'clock',
  AppWindow: 'module',
  AtSign: 'at',
  Boxes: 'package',
  Building2: 'buildings',
  BriefcaseBusiness: 'briefcase',
  CalendarDays: 'calendar-dots',
  ChartColumn: 'chart-bar',
  ChartNoAxesCombined: 'chart-line-up',
  CheckCheck: 'checks',
  ClipboardList: 'clipboard-text',
  Code2: 'code',
  DatabaseZap: 'database',
  Factory: 'factory',
  FileClock: 'file-clock',
  FilePenLine: 'file-text',
  FolderTree: 'tree-structure',
  FormInput: 'textbox',
  GitBranch: 'git-branch',
  LayoutDashboard: 'house',
  ListTree: 'book-bookmark',
  Megaphone: 'megaphone',
  PanelsTopLeft: 'squares-four',
  Radar: 'radar',
  ScrollText: 'scroll',
  Send: 'paper-plane-tilt',
  Settings: 'gear',
  Settings2: 'sliders-horizontal',
  ShieldCheck: 'shield-check',
  SlidersHorizontal: 'sliders-horizontal',
  Timer: 'timer',
  UserCog: 'user-gear',
  UserCheck: 'user-check',
  UserRoundCog: 'user-gear',
  Users: 'users',
  UsersRound: 'users',
  'ph ph-chart-line-up': 'chart-line-up',
  'ph ph-chat-centered-dots': 'chat-circle-text',
  'ph ph-chats-circle': 'chat-circle-text',
  'ph ph-cloud': 'hard-drives',
  'ph ph-database': 'database',
  'ph ph-dots-three-outline': 'module',
  'ph ph-files': 'file-text',
  'ph ph-git-branch': 'git-branch',
  'ph ph-gear': 'gear',
  'ph ph-identification-card': 'identification-card',
  'ph ph-key': 'key',
  'ph ph-list-magnifying-glass': 'magnifying-glass',
  'ph ph-list-checks': 'checks',
  'ph ph-lock-key': 'shield-check',
  'ph ph-node-tree': 'tree-structure',
  'ph ph-note-pencil': 'pencil-simple',
  'ph ph-robot': 'user-gear',
  'ph ph-shield-check': 'shield-check',
  'ph ph-sliders-horizontal': 'sliders-horizontal',
  'ph ph-sparkle': 'radar',
  'ph ph-stack': 'module-management',
  'ph ph-storefront': 'package',
  'ph ph-test-tube': 'activity',
  'ph ph-user-sound': 'user',
  'ph ph-users-three': 'users'
};

function getIconClass(icon?: string | null) {
  if (!icon) return 'folder';
  return iconAliases[icon] ?? icon;
}

function resolveMenuTargetPath(
  node: MenuTreeNodeDto,
  workspaceAppCode?: string | null,
  workspaceLevel?: 'application' | 'platform',
  workspaceTenantId?: string | null
): string | null {
  const prefix = workspaceLevel === 'application' && workspaceTenantId && workspaceAppCode
    ? `/tenants/${encodeURIComponent(workspaceTenantId)}/apps/${workspaceAppCode.toUpperCase()}/admin`
    : '';
  const applyPrefix = (path: string) => {
    if (!prefix || path.startsWith('/apps/') || path.startsWith('/tenants/')) {
      return path;
    }

    return `${prefix}${path}`;
  };

  if (node.routePath?.trim()) {
    return applyPrefix(normalizeMenuRoutePath(node.routePath.trim()));
  }

  if (node.pageCode?.trim()) {
    return applyPrefix(`/pages/${encodeURIComponent(node.pageCode.trim())}`);
  }

  return null;
}

function normalizeMenuRoutePath(routePath: string): string {
  return normalizeWorkflowMenuPath(routePath);
}

function pruneEmptyMenuDirectories(
  nodes: MenuTreeNodeDto[],
  workspaceAppCode?: string | null,
  workspaceLevel?: 'application' | 'platform',
  workspaceTenantId?: string | null
): MenuTreeNodeDto[] {
  return nodes.flatMap((node) => {
    const children = pruneEmptyMenuDirectories(node.children ?? [], workspaceAppCode, workspaceLevel, workspaceTenantId);
    const targetPath = resolveMenuTargetPath(node, workspaceAppCode, workspaceLevel, workspaceTenantId);
    if (!targetPath && children.length === 0 && !isApplicationDevelopmentRuntimeDirectory(node)) {
      return [];
    }

    return [{ ...node, children }];
  });
}

function isApplicationDevelopmentRuntimeDirectory(node: MenuTreeNodeDto): boolean {
  return node.menuType === 'Directory'
    && node.scopeType === 'ApplicationRuntime'
    && typeof node.configJson === 'string'
    && node.configJson.includes('application-development-center');
}

function isSystemMenu(node: MenuTreeNodeDto): boolean {
  const code = node.menuCode.trim().toLowerCase();
  return code === 'system' || code === 'system-management' || code === 'system:management' || node.menuName.trim() === '系统管理';
}

function isProjectManagementMenu(node: MenuTreeNodeDto): boolean {
  const code = node.menuCode.trim().toLowerCase();
  const routePath = node.routePath?.trim().toLowerCase() ?? '';
  return code === 'project-management' || code.startsWith('project-management:') || routePath.includes('/project-management');
}

function keepSystemAndProjectMenus(nodes: MenuTreeNodeDto[]): MenuTreeNodeDto[] {
  const systemMenus = nodes.filter(isSystemMenu);
  const projectMenus: MenuTreeNodeDto[] = [];
  const collectProjectMenus = (items: MenuTreeNodeDto[]) => {
    items.forEach((item) => {
      if (isProjectManagementMenu(item)) {
        projectMenus.push(item);
        return;
      }
      collectProjectMenus(item.children ?? []);
    });
  };
  collectProjectMenus(nodes.filter((item) => !isSystemMenu(item)));
  return [...systemMenus, ...projectMenus];
}

function renderMenuItems(
  nodes: MenuTreeNodeDto[],
  activePath: string,
  collapsed: boolean,
  onNavigate: () => void,
  translate: (key: string) => string,
  workspaceAppCode?: string | null,
  workspaceLevel?: 'application' | 'platform',
  workspaceTenantId?: string | null
) {
  return nodes.map((node) => {
    const targetPath = resolveMenuTargetPath(node, workspaceAppCode, workspaceLevel, workspaceTenantId);
    const isActive = targetPath ? activePath === targetPath : false;
    const iconClass = getIconClass(node.icon);
    const rawLabel = resolveMenuLabel(node, translate);
    const label = targetPath?.startsWith('/flowise/')
      ? formatWorkflowMenuTitle(targetPath, rawLabel)
      : formatWorkflowMenuGroupLabel(rawLabel);

    if (node.children && node.children.length > 0) {
      return (
        <li key={node.id} className="mt-1.5 pt-1.5 border-t border-gray-100">
          {!collapsed && <div className="px-2.5 py-1 text-xs font-semibold text-gray-400 uppercase">{label}</div>}
          <ul className="space-y-0.5 mt-0.5">
            {renderMenuItems(node.children, activePath, collapsed, onNavigate, translate, workspaceAppCode, workspaceLevel, workspaceTenantId)}
          </ul>
        </li>
      );
    }

    return (
      <li key={node.id}>
        {targetPath ? (
          <Link
            to={targetPath}
            onClick={onNavigate}
            title={collapsed ? label : undefined}
            className={`flex items-center ${collapsed ? 'justify-center px-2' : 'justify-between px-2.5'} py-1.5 rounded-md transition-colors relative ${
              isActive ? 'bg-primary-50 text-primary-600 font-medium' : 'hover:bg-gray-50 text-gray-700'
            }`}
          >
            <div className={`flex items-center ${collapsed ? 'justify-center' : 'gap-2.5'}`}>
              <AppIcon className={`text-sm ${isActive ? 'text-primary-500' : 'text-gray-400'}`} name={iconClass} />
              {!collapsed && <span>{label}</span>}
            </div>
            {isActive && <div className="w-1 h-4 bg-primary-500 absolute left-0 top-1/2 -translate-y-1/2 rounded-r"></div>}
          </Link>
        ) : (
          <div
            className={`flex items-center ${collapsed ? 'justify-center px-2' : 'justify-between px-2.5'} py-1.5 rounded-md text-gray-500`}
            title={collapsed ? label : undefined}
          >
            <div className={`flex items-center ${collapsed ? 'justify-center' : 'gap-2.5'}`}>
              <AppIcon className="text-sm text-gray-400" name={iconClass} />
              {!collapsed && <span>{label}</span>}
            </div>
          </div>
        )}
      </li>
    );
  });
}

export function SidebarMenu({ activePath, menuTree, onNavigate, open, onToggle, workspaceAppCode, workspaceLevel, workspaceTenantId }: SidebarMenuProps) {
  const { translate } = useI18n();
  const visibleMenuTree = useMemo(
    () => pruneEmptyMenuDirectories(keepSystemAndProjectMenus(menuTree), workspaceAppCode, workspaceLevel, workspaceTenantId),
    [menuTree, workspaceAppCode, workspaceLevel, workspaceTenantId]
  );
  const collapsed = !open;
  const appCode = workspaceAppCode?.toUpperCase();

  return (
    <aside className={`app-sidebar hidden h-full min-h-0 ${collapsed ? 'app-sidebar--collapsed' : 'app-sidebar--expanded'} shrink-0 flex-col border-r border-gray-200 bg-white shadow-[2px_0_5px_-2px_rgba(0,0,0,0.05)] z-10 md:flex`}>
      <div className={`${collapsed ? 'app-sidebar__workspace--collapsed px-2 justify-center' : 'app-sidebar__workspace px-3'} flex shrink-0 items-center border-b border-gray-100 text-gray-500 font-medium text-xs`}>
        {workspaceLevel === 'application' && appCode ? (
          collapsed ? (
            <div className="flex h-8 w-8 items-center justify-center rounded-md bg-primary-50 text-xs font-semibold text-primary-600" title={`${appCode} 应用`}>
              {appCode.slice(0, 3)}
            </div>
          ) : (
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-gray-800">{appCode} 应用</div>
              <div className="truncate text-xs font-normal text-gray-500">{translate('sidebar.application')}</div>
            </div>
          )
        ) : (
          <span className={collapsed ? 'sr-only' : 'uppercase tracking-wider'}>{translate('sidebar.management')}</span>
        )}
      </div>
      <nav className="min-h-0 flex-1 overflow-y-auto py-2">
        <ul className={`space-y-0.5 ${collapsed ? 'px-1.5' : 'px-1.5'} text-sm text-gray-700`}>
          {renderMenuItems(visibleMenuTree, activePath, collapsed, onNavigate, translate, workspaceAppCode, workspaceLevel, workspaceTenantId)}
        </ul>
      </nav>
      <div
        className="app-sidebar__toggle shrink-0 border-t border-gray-200 flex items-center justify-center cursor-pointer hover:bg-gray-50 text-gray-500 hover:text-primary-600 transition-colors"
        onClick={onToggle}
      >
        <AppIcon className="text-lg" name={collapsed ? 'sidebar-simple' : 'caret-line-left'} />
      </div>
    </aside>
  );
}
