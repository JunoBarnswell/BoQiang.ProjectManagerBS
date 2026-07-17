import { useResponsiveContext } from './ResponsiveProvider';

export function useDensity() {
  const { density, tokens } = useResponsiveContext();

  return {
    density,
    tokens
  };
}

