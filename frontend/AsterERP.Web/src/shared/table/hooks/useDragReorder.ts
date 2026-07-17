import { type DragEvent, useCallback, useState } from 'react';

interface UseDragReorderOptions {
  enabled: boolean;
  onDrop: (sourceKey: string, targetKey: string) => void | Promise<void>;
}

interface UseDragReorderResult {
  dragOverKey: string | null;
  draggingKey: string | null;
  getDragSourceProps: (key: string) => {
    draggable: boolean;
    onDragEnd: () => void;
    onDragStart: (event: DragEvent) => void;
  };
  getDropTargetProps: (key: string) => {
    onDragEnter: (event: DragEvent) => void;
    onDragLeave: () => void;
    onDragOver: (event: DragEvent) => void;
    onDrop: (event: DragEvent) => void;
  };
  isPending: boolean;
}

export function useDragReorder({ enabled, onDrop }: UseDragReorderOptions): UseDragReorderResult {
  const [draggingKey, setDraggingKey] = useState<string | null>(null);
  const [dragOverKey, setDragOverKey] = useState<string | null>(null);
  const [isPending, setIsPending] = useState(false);

  const reset = useCallback(() => {
    setDraggingKey(null);
    setDragOverKey(null);
  }, []);

  const getDragSourceProps = useCallback(
    (key: string) => ({
      draggable: enabled && !isPending,
      onDragEnd: reset,
      onDragStart: (event: DragEvent) => {
        if (!enabled || isPending) {
          event.preventDefault();
          return;
        }

        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', key);
        setDraggingKey(key);
      }
    }),
    [enabled, isPending, reset]
  );

  const getDropTargetProps = useCallback(
    (key: string) => ({
      onDragEnter: (event: DragEvent) => {
        if (!enabled || !draggingKey || draggingKey === key) {
          return;
        }

        event.preventDefault();
        setDragOverKey(key);
      },
      onDragLeave: () => {
        if (!enabled) {
          return;
        }

        setDragOverKey((current) => current === key ? null : current);
      },
      onDragOver: (event: DragEvent) => {
        if (!enabled || !draggingKey || draggingKey === key) {
          return;
        }

        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        setDragOverKey(key);
      },
      onDrop: (event: DragEvent) => {
        if (!enabled || !draggingKey || draggingKey === key) {
          reset();
          return;
        }

        event.preventDefault();
        const sourceKey = draggingKey;
        setIsPending(true);
        void Promise.resolve(onDrop(sourceKey, key))
          .catch(() => undefined)
          .finally(() => {
            setIsPending(false);
            reset();
          });
      }
    }),
    [draggingKey, enabled, onDrop, reset]
  );

  return {
    dragOverKey,
    draggingKey,
    getDragSourceProps,
    getDropTargetProps,
    isPending
  };
}
