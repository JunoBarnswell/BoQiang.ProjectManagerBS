import type { QueryClient } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';

import { translateCurrentLocale } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import type { ConfirmOptions } from '../../../../shared/feedback/FeedbackProvider';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';
import {
  deleteKnowledgeGraphEdge,
  deleteKnowledgeGraphNode,
  saveKnowledgeGraphEdge,
  saveKnowledgeGraphNode
} from '../../api/knowledgeGraph.api';
import type { KnowledgeGraphUiState } from '../state/useKnowledgeGraphUiStore';
import type {
  KnowledgeGraphApiEdgeUpsertRequest,
  KnowledgeGraphApiNodeUpsertRequest,
  KnowledgeGraphEdgeFormValue,
  KnowledgeGraphSnapshotView
} from '../types';
import { canConnectKnowledgeGraph } from '../utils/knowledgeGraphFlow';
import {
  buildEdgeFormValue,
  buildEdgeUpsertRequest,
  buildNodeFormValue,
  buildNodePositionRequest,
  buildNodeUpsertRequest,
  defaultEdgeFormValue
} from '../utils/knowledgeGraphFormatters';

interface KnowledgeGraphEntityCommandOptions {
  confirm: (options: ConfirmOptions) => void;
  message: {
    error: (content: string) => void;
    info: (content: string) => void;
    success: (content: string) => void;
  };
  queryClient: QueryClient;
  refresh: () => Promise<void>;
  snapshot: KnowledgeGraphSnapshotView;
  store: KnowledgeGraphUiState;
}

export function useKnowledgeGraphEntityCommands({
  confirm,
  message,
  queryClient,
  refresh,
  snapshot,
  store
}: KnowledgeGraphEntityCommandOptions) {
  const [edgeFormSeed, setEdgeFormSeed] = useState<Partial<KnowledgeGraphEdgeFormValue> | null>(null);
  const nodeFormValue = useMemo(
    () => buildNodeFormValue(store.nodeFormId ? snapshot.nodes.find((node) => node.id === store.nodeFormId) : null),
    [snapshot.nodes, store.nodeFormId]
  );
  const edgeFormValue = useMemo(() => {
    const base = buildEdgeFormValue(store.edgeFormId ? snapshot.edges.find((edge) => edge.id === store.edgeFormId) : null);
    return edgeFormSeed ? { ...base, ...edgeFormSeed } : base;
  }, [edgeFormSeed, snapshot.edges, store.edgeFormId]);

  const saveNodeMutation = useApiMutation({
    mutationFn: (request: KnowledgeGraphApiNodeUpsertRequest) => saveKnowledgeGraphNode(request)
  });
  const saveEdgeMutation = useApiMutation({
    mutationFn: (request: KnowledgeGraphApiEdgeUpsertRequest) => saveKnowledgeGraphEdge(request)
  });
  const deleteNodeMutation = useApiMutation({ mutationFn: (nodeId: string) => deleteKnowledgeGraphNode(nodeId) });
  const deleteEdgeMutation = useApiMutation({ mutationFn: (edgeId: string) => deleteKnowledgeGraphEdge(edgeId) });

  const closeModal = useCallback(() => {
    setEdgeFormSeed(null);
    store.setActiveModal(null);
  }, [store]);

  const openCreateNode = useCallback(() => {
    store.setNodeFormId(null);
    store.setActiveModal('nodeForm');
  }, [store]);

  const openEditNode = useCallback((nodeId: string) => {
    store.setNodeFormId(nodeId);
    store.setActiveModal('nodeForm');
  }, [store]);

  const openCreateEdge = useCallback((seed?: Partial<KnowledgeGraphEdgeFormValue>) => {
    setEdgeFormSeed(seed ? { ...defaultEdgeFormValue, ...seed } : null);
    store.setEdgeFormId(null);
    store.setActiveModal('edgeForm');
  }, [store]);

  const openEditEdge = useCallback((edgeId: string) => {
    setEdgeFormSeed(null);
    store.setEdgeFormId(edgeId);
    store.setActiveModal('edgeForm');
  }, [store]);

  const saveNode = useCallback(async (value: Parameters<typeof buildNodeUpsertRequest>[0]) => {
    if (!value.nodeCode.trim() || !value.title.trim()) {
      message.info(translateCurrentLocale('kg.entity.error.nodeRequired'));
      return;
    }
    try {
      await saveNodeMutation.mutateAsync(buildNodeUpsertRequest(value));
      store.setActiveModal(null);
      message.success(translateCurrentLocale('kg.entity.success.nodeSaved'));
      await refresh();
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLocale('kg.entity.error.nodeSaveFailed')));
    }
  }, [message, refresh, saveNodeMutation, store]);

  const saveEdge = useCallback(async (value: KnowledgeGraphEdgeFormValue) => {
    if (!value.sourceNodeId || !value.targetNodeId || value.sourceNodeId === value.targetNodeId) {
      message.info(translateCurrentLocale('kg.entity.error.edgeEndpointsRequired'));
      return;
    }
    try {
      await saveEdgeMutation.mutateAsync(buildEdgeUpsertRequest(value));
      setEdgeFormSeed(null);
      store.setActiveModal(null);
      message.success(translateCurrentLocale('kg.entity.success.edgeSaved'));
      await refresh();
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLocale('kg.entity.error.edgeSaveFailed')));
    }
  }, [message, refresh, saveEdgeMutation, store]);

  const commitNodePosition = useCallback(async (nodeId: string, position: { x: number; y: number }) => {
    const node = snapshot.nodes.find((item) => item.id === nodeId);
    if (!node) {
      return;
    }
    store.updateLayoutOverride(nodeId, position);
    try {
      await saveNodeMutation.mutateAsync(buildNodePositionRequest(node, position.x, position.y));
      await queryClient.invalidateQueries({ queryKey: ['ai', 'knowledge-graph', 'snapshot'] });
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLocale('kg.entity.error.nodePositionSaveFailed')));
      await queryClient.invalidateQueries({ queryKey: ['ai', 'knowledge-graph', 'snapshot'] });
    }
  }, [message, queryClient, saveNodeMutation, snapshot.nodes, store]);

  const connectNodes = useCallback((sourceNodeId: string, targetNodeId: string) => {
    const valid = canConnectKnowledgeGraph(snapshot, {
      source: sourceNodeId,
      sourceHandle: null,
      target: targetNodeId,
      targetHandle: null
    });
    if (!valid) {
      message.info(translateCurrentLocale('kg.entity.error.invalidConnect'));
      return;
    }
    openCreateEdge({ sourceNodeId, targetNodeId });
  }, [message, openCreateEdge, snapshot]);

  const deleteNode = useCallback((nodeId: string) => {
    confirm({
      cancelText: translateCurrentLocale('common.cancel'),
      confirmText: translateCurrentLocale('common.delete'),
      content: translateCurrentLocale('kg.entity.confirm.deleteNode'),
      onConfirm: async () => {
        try {
          await deleteNodeMutation.mutateAsync(nodeId);
          store.setSelection(null);
          message.success(translateCurrentLocale('kg.entity.success.nodeDeleted'));
          await refresh();
        } catch (error) {
          message.error(getErrorMessage(error, translateCurrentLocale('kg.entity.error.nodeDeleteFailed')));
        }
      },
      title: translateCurrentLocale('kg.entity.confirm.deleteNodeTitle')
    });
  }, [confirm, deleteNodeMutation, message, refresh, store]);

  const deleteEdge = useCallback((edgeId: string) => {
    confirm({
      cancelText: translateCurrentLocale('common.cancel'),
      confirmText: translateCurrentLocale('common.delete'),
      content: translateCurrentLocale('kg.entity.confirm.deleteEdge'),
      onConfirm: async () => {
        try {
          await deleteEdgeMutation.mutateAsync(edgeId);
          store.setSelection(null);
          message.success(translateCurrentLocale('kg.entity.success.edgeDeleted'));
          await refresh();
        } catch (error) {
          message.error(getErrorMessage(error, translateCurrentLocale('kg.entity.error.edgeDeleteFailed')));
        }
      },
      title: translateCurrentLocale('kg.entity.confirm.deleteEdgeTitle')
    });
  }, [confirm, deleteEdgeMutation, message, refresh, store]);

  return {
    commands: {
      closeModal,
      connectNodes,
      deleteEdge,
      deleteNode,
      openCreateEdge,
      openCreateNode,
      openEditEdge,
      openEditNode,
      saveEdge,
      saveNode
    },
    edgeFormValue,
    mutations: {
      deletingEdge: deleteEdgeMutation.isPending,
      deletingNode: deleteNodeMutation.isPending,
      savingEdge: saveEdgeMutation.isPending,
      savingNode: saveNodeMutation.isPending
    },
    nodeFormValue,
    position: {
      commitNodePosition
    }
  };
}
