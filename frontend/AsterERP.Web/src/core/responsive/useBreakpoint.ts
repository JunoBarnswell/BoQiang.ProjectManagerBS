import { isBreakpointAtLeast, isBreakpointBelow } from './breakpoint';
import { useResponsiveContext } from './ResponsiveProvider';

export function useBreakpoint() {
  const { breakpoint } = useResponsiveContext();

  return {
    breakpoint,
    isAtLeast: (target: Parameters<typeof isBreakpointAtLeast>[1]) => isBreakpointAtLeast(breakpoint, target),
    isBelow: (target: Parameters<typeof isBreakpointBelow>[1]) => isBreakpointBelow(breakpoint, target)
  };
}

