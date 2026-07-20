import { useMediaQuery } from '@mui/material';

export function usePmMediaQuery(query: string): boolean {
  return useMediaQuery(query);
}
