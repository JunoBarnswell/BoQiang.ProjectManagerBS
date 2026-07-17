import { useMemo } from 'react';

import { useViewportSize } from './useViewportSize';

export interface AutoHeightOptions {
  enabled?: boolean;
  minHeight?: number;
  offset?: number;
}

export function useAutoHeight({ enabled = true, minHeight = 240, offset = 0 }: AutoHeightOptions = {}) {
  const { height } = useViewportSize();

  return useMemo(() => {
    if (!enabled) {
      return {
        height: undefined as number | undefined,
        style: undefined as undefined | { minHeight: string }
      };
    }

    const nextHeight = Math.max(minHeight, height - offset);

    return {
      height: nextHeight,
      style: {
        minHeight: `${nextHeight}px`
      }
    };
  }, [enabled, height, minHeight, offset]);
}

