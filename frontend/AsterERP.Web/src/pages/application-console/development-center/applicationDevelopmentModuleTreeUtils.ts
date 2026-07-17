import type {
  ApplicationDevelopmentModuleTreeNode,
  ApplicationDevelopmentPageListItem
} from '../../../api/application-development-center/applicationDevelopmentCenter.types';

export function getModuleChildren(module: ApplicationDevelopmentModuleTreeNode): ApplicationDevelopmentModuleTreeNode[] {
  const children = (module as { children?: unknown }).children;
  return Array.isArray(children) ? children as ApplicationDevelopmentModuleTreeNode[] : [];
}

export function normalizeModuleTree(modules: ApplicationDevelopmentModuleTreeNode[]): ApplicationDevelopmentModuleTreeNode[] {
  return modules.map((module) => ({
    ...module,
    children: normalizeModuleTree(getModuleChildren(module))
  }));
}

export function flattenModuleTree(modules: ApplicationDevelopmentModuleTreeNode[]): ApplicationDevelopmentModuleTreeNode[] {
  return modules.flatMap((module) => [module, ...flattenModuleTree(getModuleChildren(module))]);
}

export function findModuleById(
  modules: ApplicationDevelopmentModuleTreeNode[],
  moduleId: string
): ApplicationDevelopmentModuleTreeNode | null {
  for (const module of modules) {
    if (module.id === moduleId) {
      return module;
    }

    const match = findModuleById(getModuleChildren(module), moduleId);
    if (match) {
      return match;
    }
  }

  return null;
}

export function collectModuleSubtreeIds(modules: ApplicationDevelopmentModuleTreeNode[], moduleId: string): Set<string> {
  const ids = new Set<string>();
  collectModuleSubtreeIdsRecursive(modules, moduleId, ids);
  return ids;
}

export function hasPagesInModuleSubtree(
  modules: ApplicationDevelopmentModuleTreeNode[],
  pages: ApplicationDevelopmentPageListItem[],
  moduleId: string
): boolean {
  const visibleModuleIds = collectModuleSubtreeIds(modules, moduleId);
  return pages.some((page) => page.moduleId ? visibleModuleIds.has(page.moduleId) : false);
}

function collectModuleSubtreeIdsRecursive(
  modules: ApplicationDevelopmentModuleTreeNode[],
  moduleId: string,
  ids: Set<string>
): void {
  if (ids.has(moduleId)) {
    return;
  }

  ids.add(moduleId);
  const module = findModuleById(modules, moduleId);
  if (!module) {
    return;
  }

  getModuleChildren(module).forEach((child) => collectModuleSubtreeIdsRecursive(modules, child.id, ids));
}
