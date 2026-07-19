import { appCommonMessagesZhCN } from './shell/zh-CN/appCommon';
import { asterSceneDccMessagesZhCN } from './shell/zh-CN/asterSceneDcc';
import { asterSceneStudioMessagesZhCN } from './shell/zh-CN/asterSceneStudio';
import { asterSceneSurfaceMessagesZhCN } from './shell/zh-CN/asterSceneSurface';
import { dashboardHomeMessagesZhCN } from './shell/zh-CN/dashboardHome';
import { navigationMessagesZhCN } from './shell/zh-CN/navigation';
import { projectManagementMessagesZhCN } from './shell/zh-CN/projectManagement';
import { systemPageMessagesZhCN } from './shell/zh-CN/systemPage';

export const shellMessagesZhCN = {
  ...appCommonMessagesZhCN,
  ...navigationMessagesZhCN,
  ...projectManagementMessagesZhCN,
  ...dashboardHomeMessagesZhCN,
  ...systemPageMessagesZhCN,
  ...asterSceneDccMessagesZhCN,
  ...asterSceneStudioMessagesZhCN,
  ...asterSceneSurfaceMessagesZhCN
};
