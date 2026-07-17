import { AppIcon } from '@/shared/icons/AppIcon';

import { useImStore } from '../state/imStore';

import { ImConversationDrawer } from './ImConversationDrawer';

export function ImChatLauncher() {
  const openDrawer = useImStore((state) => state.openDrawer);
  const totalUnread = useImStore((state) => state.unreadSummary.totalUnread);

  return (
    <>
      <button
        className="fixed bottom-6 right-6 z-40 inline-flex h-12 w-12 items-center justify-center rounded-full bg-blue-600 text-white shadow-lg hover:bg-blue-700"
        onClick={openDrawer}
        title="站内信"
        type="button"
      >
        <AppIcon name="mail" />
        {totalUnread > 0 ? <span className="absolute -right-1 -top-1 min-w-5 rounded-full bg-red-500 px-1 text-xs font-semibold leading-5">{totalUnread > 99 ? '99+' : totalUnread}</span> : null}
      </button>
      <ImConversationDrawer />
    </>
  );
}
