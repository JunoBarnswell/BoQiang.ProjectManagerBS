import { useResponsiveContext } from './ResponsiveProvider';

export function useViewportSize() {
  const { height, rawHeight, rawWidth, scalePercent, width } = useResponsiveContext();
  return { height, rawHeight, rawWidth, scalePercent, width };
}

