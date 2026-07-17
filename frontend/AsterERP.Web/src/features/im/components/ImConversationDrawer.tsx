import { AppIcon } from '@/shared/icons/AppIcon';

import { useImStore } from '../state/imStore';

import { ImWorkspace } from './ImWorkspace';

export function ImConversationDrawer() {
  const drawerOpen = useImStore((state) => state.drawerOpen);
  const closeDrawer = useImStore((state) => state.closeDrawer);

  if (!drawerOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-black/20">
      <div className="flex h-full w-full max-w-5xl flex-col bg-white shadow-xl">
        <div className="flex h-11 items-center justify-between border-b border-gray-200 px-4">
          <span className="text-sm font-semibold text-gray-900">站内信</span>
          <button className="text-gray-500 hover:text-gray-900" onClick={closeDrawer} title="关闭" type="button">
            <AppIcon name="x" />
          </button>
        </div>
        <ImWorkspace mode="drawer" />
      </div>
    </div>
  );
}
