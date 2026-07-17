import { useNavigate } from 'react-router-dom';

import { AppIcon } from '@/shared/icons/AppIcon';

import { useImStore } from '../state/imStore';

interface ImUnreadEntryProps {
  openMode?: 'drawer' | 'page';
}

export function ImUnreadEntry({ openMode = 'drawer' }: ImUnreadEntryProps) {
  const navigate = useNavigate();
  const openDrawer = useImStore((state) => state.openDrawer);
  const totalUnread = useImStore((state) => state.unreadSummary.totalUnread);

  const open = () => {
    if (openMode === 'page') {
      navigate('/im/messages');
      return;
    }

    openDrawer();
  };

  return (
    <button className="relative hidden hover:text-white transition-colors cursor-pointer sm:block" onClick={open} title="站内信" type="button">
      <AppIcon className="text-lg" name="mail" />
      {totalUnread > 0 ? (
        <span className="absolute -right-2 -top-2 min-w-4 rounded-full bg-red-500 px-1 text-[10px] font-semibold leading-4 text-white">
          {totalUnread > 99 ? '99+' : totalUnread}
        </span>
      ) : null}
    </button>
  );
}
