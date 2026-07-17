import { useEffect, useMemo, useState } from 'react';

import { useImStore } from '../state/imStore';
import type { ImConversation } from '../types/imTypes';

import { ImChatPanel } from './ImChatPanel';
import { useImContext } from './ImProvider';

interface ImMiniConversationProps {
  height?: number;
  readonly?: boolean;
  sourceAppCode?: string;
  targetUserId: string;
}

export function ImMiniConversation({ height = 420, readonly, sourceAppCode, targetUserId }: ImMiniConversationProps) {
  const { adapter } = useImContext();
  const mergeConversation = useImStore((state) => state.mergeConversation);
  const [conversation, setConversation] = useState<ImConversation | undefined>();

  useEffect(() => {
    if (!targetUserId) return;
    let disposed = false;
    void adapter.createDirectConversation(targetUserId).then((next) => {
      if (disposed) return;
      mergeConversation(next);
      setConversation(next);
    });
    return () => {
      disposed = true;
    };
  }, [adapter, mergeConversation, targetUserId]);

  const style = useMemo(() => ({ height }), [height]);

  return (
    <div className="flex min-h-0 overflow-hidden rounded border border-gray-200 bg-white" style={style}>
      <ImChatPanel conversation={conversation} readonly={readonly} sourceAppCode={sourceAppCode} />
    </div>
  );
}
