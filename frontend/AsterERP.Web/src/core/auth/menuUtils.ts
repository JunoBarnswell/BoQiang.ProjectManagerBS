import type { MenuTreeNodeDto } from '../../api/system/system.types';

export interface FlattenedMenuNode {
  children: FlattenedMenuNode[];
  componentName?: string | null;
  configJson?: string | null;
  icon?: string | null;
  id: string;
  menuCode: string;
  menuName: string;
  menuType: string;
  pageCode?: string | null;
  artifactId?: string | null;
  permissionCode?: string | null;
  routePath?: string | null;
  scopeType?: string | null;
  sortOrder: number;
  visible: boolean;
}

export function flattenMenuTree(menuTree: MenuTreeNodeDto[]): FlattenedMenuNode[] {
  return menuTree.map((menu) => ({
    children: flattenMenuTree(menu.children ?? []),
    componentName: menu.componentName ?? null,
    configJson: menu.configJson ?? null,
    icon: menu.icon ?? null,
    id: menu.id,
    menuCode: menu.menuCode,
    menuName: menu.menuName,
    menuType: menu.menuType,
    pageCode: menu.pageCode ?? null,
    artifactId: menu.artifactId ?? null,
    permissionCode: menu.permissionCode ?? null,
    routePath: menu.routePath ?? null,
    scopeType: menu.scopeType ?? null,
    sortOrder: menu.sortOrder,
    visible: menu.visible
  }));
}

export function flattenMenuNodes(menuTree: FlattenedMenuNode[]): FlattenedMenuNode[] {
  const result: FlattenedMenuNode[] = [];

  const visit = (node: FlattenedMenuNode) => {
    result.push(node);
    node.children.forEach(visit);
  };

  menuTree.forEach(visit);
  return result;
}

export function findMenuNodeByPath(menuTree: FlattenedMenuNode[], pathname: string): FlattenedMenuNode | null {
  const nodes = flattenMenuNodes(menuTree);
  const exact = nodes.find((node) => node.routePath === pathname);
  if (exact) {
    return exact;
  }

  const pathOnly = pathname.split('?')[0] ?? pathname;
  return nodes.find((node) => node.routePath === pathOnly) ?? null;
}

export function buildMenuPathLabel(pathname: string): string {
  const lastSegment = pathname.split('/').filter(Boolean).pop();
  if (!lastSegment) {
    return '/';
  }

  return lastSegment;
}
