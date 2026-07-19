import { appCommonMessagesEnUS } from './shell/en-US/appCommon';
import { asterSceneDccMessagesEnUS } from './shell/en-US/asterSceneDcc';
import { asterSceneStudioMessagesEnUS } from './shell/en-US/asterSceneStudio';
import { asterSceneSurfaceMessagesEnUS } from './shell/en-US/asterSceneSurface';
import { dashboardHomeMessagesEnUS } from './shell/en-US/dashboardHome';
import { navigationMessagesEnUS } from './shell/en-US/navigation';
import { projectManagementMessagesEnUS } from './shell/en-US/projectManagement';
import { systemPageMessagesEnUS } from './shell/en-US/systemPage';

export const shellMessagesEnUS = {
  ...appCommonMessagesEnUS,
  ...navigationMessagesEnUS,
  ...projectManagementMessagesEnUS,
  ...dashboardHomeMessagesEnUS,
  ...systemPageMessagesEnUS,
  ...asterSceneDccMessagesEnUS,
  ...asterSceneStudioMessagesEnUS,
  ...asterSceneSurfaceMessagesEnUS
};
