import { flowiseMessagesEnUS } from '../../features/flowise-studio/i18n/flowiseMessages.en-US';
import { flowiseMessagesZhCN } from '../../features/flowise-studio/i18n/flowiseMessages.zh-CN';
import type { AppLocale } from '../config/env';

import { domainMessagesEnUS, domainMessagesZhCN } from './catalogs/domainMessages';
import { sharedUiMessagesEnUS, sharedUiMessagesZhCN } from './catalogs/sharedUiMessages';
import { shellMessagesEnUS } from './catalogs/shellMessages.en-US';
import { shellMessagesZhCN } from './catalogs/shellMessages.zh-CN';

type MessageBag = Record<string, string>;

export const messages: Record<AppLocale, MessageBag> = {
  'en-US': {
    ...flowiseMessagesEnUS,
    ...sharedUiMessagesEnUS,
    ...domainMessagesEnUS,
    ...shellMessagesEnUS
  },
  'zh-CN': {
    ...flowiseMessagesZhCN,
    ...sharedUiMessagesZhCN,
    ...domainMessagesZhCN,
    ...shellMessagesZhCN
  }
};
