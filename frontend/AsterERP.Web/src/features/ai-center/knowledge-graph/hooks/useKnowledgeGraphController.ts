import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';

import { useConfirm } from '../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { useKnowledgeGraphUiStore } from '../state/useKnowledgeGraphUiStore';
import { findSelectedEdge, findSelectedNode } from '../utils/knowledgeGraphFlow';

import { useKnowledgeGraphAnalysisCommands } from './useKnowledgeGraphAnalysisCommands';
import { useKnowledgeGraphEntityCommands } from './useKnowledgeGraphEntityCommands';
import { useKnowledgeGraphExchangeCommands } from './useKnowledgeGraphExchangeCommands';
import { useKnowledgeGraphQueries } from './useKnowledgeGraphQueries';

export function useKnowledgeGraphController() {
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const store = useKnowledgeGraphUiStore();
  const queryState = useKnowledgeGraphQueries(store.filters);

  const refresh = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['ai', 'knowledge-graph', 'overview'] }),
      queryClient.invalidateQueries({ queryKey: ['ai', 'knowledge-graph', 'snapshot'] }),
      queryClient.invalidateQueries({ queryKey: ['ai', 'knowledge-graph', 'tasks'] })
    ]);
  }, [queryClient]);

  const entityState = useKnowledgeGraphEntityCommands({
    confirm,
    message,
    queryClient,
    refresh,
    snapshot: queryState.snapshot,
    store
  });
  const analysis = useKnowledgeGraphAnalysisCommands({ message, store });
  const exchangeState = useKnowledgeGraphExchangeCommands({
    filters: store.filters,
    message,
    refresh
  });

  const selectedNode = useMemo(
    () => findSelectedNode(queryState.snapshot, store.selection),
    [queryState.snapshot, store.selection]
  );
  const selectedEdge = useMemo(
    () => findSelectedEdge(queryState.snapshot, store.selection),
    [queryState.snapshot, store.selection]
  );

  return {
    ...store,
    analysis,
    canvas: {
      commitNodePosition: entityState.position.commitNodePosition,
      connectNodes: entityState.commands.connectNodes,
      layoutOverrides: store.layoutOverrides
    },
    commands: {
      ...entityState.commands,
      ...exchangeState.commands,
      refresh
    },
    edgeFormValue: entityState.edgeFormValue,
    mutations: {
      ...entityState.mutations,
      ...exchangeState.mutations
    },
    nodeFormValue: entityState.nodeFormValue,
    options: queryState.options,
    overviewQuery: queryState.overviewQuery,
    queries: queryState.queries,
    selectedEdge,
    selectedNode,
    snapshot: queryState.snapshot,
    tasks: queryState.tasks
  };
}
