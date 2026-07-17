import type { RouteObject } from 'react-router-dom';

export interface AppRouteMeta {
  breadcrumbKey: string;
  cachePolicy?: 'none' | 'tab-alive';
  iconKey: string;
  labelKey: string;
  layoutVariant?: 'app' | 'login';
  path: string;
  presentation?: 'drawer' | 'modal' | 'page' | 'subtab';
  tabMode?: 'detail' | 'menu' | 'none';
}

interface AppRouteHandle {
  routeMeta?: AppRouteMeta;
}

export function routeMeta(meta: AppRouteMeta): AppRouteHandle {
  return { routeMeta: meta };
}

export function collectRouteMeta(routes: RouteObject[]): AppRouteMeta[] {
  return routes.flatMap((route) => {
    const handle = route.handle as AppRouteHandle | undefined;
    return [
      ...(handle?.routeMeta ? [handle.routeMeta] : []),
      ...(route.children ? collectRouteMeta(route.children) : [])
    ];
  });
}

export function findMatchingRouteMeta(routes: AppRouteMeta[], pathname: string): AppRouteMeta {
  return routes.find((route) => route.path === pathname || matchesRoutePattern(route.path, pathname)) ?? routes[0];
}

function matchesRoutePattern(pattern: string, pathname: string): boolean {
  if (!pattern.includes(':')) {
    return false;
  }

  const patternParts = pattern.split('/').filter(Boolean);
  const pathParts = pathname.split('/').filter(Boolean);
  if (patternParts.length !== pathParts.length) {
    return false;
  }

  return patternParts.every((part, index) => part.startsWith(':') || part === pathParts[index]);
}
