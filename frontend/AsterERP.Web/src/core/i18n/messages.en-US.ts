import { flowiseMessagesEnUS } from '../../features/flowise-studio/i18n/flowiseMessages.en-US';

import { domainMessagesEnUS } from './catalogs/domainMessages.en-US';
import { sharedUiMessagesEnUS } from './catalogs/sharedUiMessages.en-US';
import { shellMessagesEnUS } from './catalogs/shellMessages.en-US';

export const messagesEnUS = {
  ...flowiseMessagesEnUS,
  ...sharedUiMessagesEnUS,
  ...domainMessagesEnUS,
  ...shellMessagesEnUS
};
