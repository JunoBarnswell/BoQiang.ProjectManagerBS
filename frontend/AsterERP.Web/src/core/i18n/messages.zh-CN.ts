import { flowiseMessagesZhCN } from '../../features/flowise-studio/i18n/flowiseMessages.zh-CN';

import { domainMessagesZhCN } from './catalogs/domainMessages.zh-CN';
import { sharedUiMessagesZhCN } from './catalogs/sharedUiMessages.zh-CN';
import { shellMessagesZhCN } from './catalogs/shellMessages.zh-CN';

export const messagesZhCN = {
  ...flowiseMessagesZhCN,
  ...sharedUiMessagesZhCN,
  ...domainMessagesZhCN,
  ...shellMessagesZhCN
};
