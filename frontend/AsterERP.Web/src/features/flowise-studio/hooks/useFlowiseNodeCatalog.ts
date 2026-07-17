import { useApiQuery } from '../../../core/query/useApiQuery';
import { canvasApi } from '../api/canvas.api';

export function useFlowiseNodeCatalog() {
  return useApiQuery({
    queryKey: ['flowise', 'nodes', 'catalog'],
    queryFn: ({ signal }) => canvasApi.nodes(signal),
    staleTimeMs: 5 * 60 * 1000
  });
}
