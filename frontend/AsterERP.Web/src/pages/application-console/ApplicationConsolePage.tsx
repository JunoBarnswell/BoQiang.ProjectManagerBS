import type { ApplicationConsoleDevelopmentShortcutDto } from '../../api/application-console/applicationConsole.types';

import { ApplicationConsolePageFrame } from './ApplicationConsolePageFrame';
import type { ApplicationConsolePageContext } from './ApplicationConsolePageFrame';
import type { ApplicationConsoleRecentVisitInput } from './applicationConsoleRecentVisits';
import { ConsoleEntryWorkbench } from './console-entry/ConsoleEntryWorkbench';

export function ApplicationConsolePage() {
  return (
    <ApplicationConsolePageFrame pageKey="console">
      {(context) => <ApplicationConsolePageContent {...context} />}
    </ApplicationConsolePageFrame>
  );
}

function ApplicationConsolePageContent({ summary }: ApplicationConsolePageContext) {
  return <ConsoleEntryWorkbench buildShortcutVisitState={buildShortcutVisitState} shortcuts={summary.developmentShortcuts} />;
}

function buildShortcutVisitState(shortcut: ApplicationConsoleDevelopmentShortcutDto) {
  return {
    recentVisit: {
      description: shortcut.description,
      kind: shortcut.visitKind,
      path: shortcut.routePath,
      pageId: shortcut.visitKind === 'designer' ? parsePageIdFromDesignerPath(shortcut.routePath) : null,
      section: resolveSectionLabel(shortcut.routePath),
      targetTitle: shortcut.recentTargetTitle ?? null,
      title: shortcut.title
    } satisfies ApplicationConsoleRecentVisitInput
  };
}

function parsePageIdFromDesignerPath(path: string) {
  const match = path.match(/pages\/([^/]+)\/designer/i);
  return match?.[1] ? decodeURIComponent(match[1]) : null;
}

function resolveSectionLabel(routePath: string) {
  if (routePath.includes('/data-center/entities-fields')) {
    return '数据中心 / 数据建模';
  }

  if (routePath.includes('/data-center/microflows')) {
    return '数据中心 / 微流管理';
  }

  if (routePath.includes('/workflows/models')) {
    return '开发中心 / 流程设计';
  }

  if (routePath.includes('/development-center/pages')) {
    return '开发中心 / 页面设计';
  }

  return '应用控制台';
}
