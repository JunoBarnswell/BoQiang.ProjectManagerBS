import type { Viewport } from '@xyflow/react';
import { useMemo } from 'react';

import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { canvasApi } from '../api/canvas.api';
import { nativeChatflowsApi } from '../api/nativeChatflows.api';
import {
  buildCanvasUpsertRequest,
  flowTypeFromMode,
  normalizeFlowData
} from '../canvas/FlowiseCanvasModel';
import type { FlowiseCanvasEdge, FlowiseCanvasMode, FlowiseCanvasNode, FlowiseCanvasUpsertRequest } from '../types/canvas.types';
import type { FlowiseChatflowType } from '../types/chatflow.types';

function chatflowTypeFromMode(mode: FlowiseCanvasMode): FlowiseChatflowType {
  return mode.includes('agentflow') ? 'AGENTFLOW' : 'CHATFLOW';
}

export function useFlowiseCanvas(resourceId: string | undefined, mode: FlowiseCanvasMode) {
  const canvasQuery = useApiQuery({
    enabled: Boolean(resourceId),
    queryKey: ['flowise', 'canvas', resourceId, mode],
    queryFn: ({ signal }) => canvasApi.get(resourceId ?? '', signal)
  });

  const chatflowType = chatflowTypeFromMode(mode);
  const chatflowQuery = useApiQuery({
    enabled: Boolean(resourceId),
    queryKey: ['flowise', 'chatflow', chatflowType, resourceId],
    queryFn: ({ signal }) => nativeChatflowsApi.get(chatflowType, resourceId ?? '', signal)
  });

  const flowData = useMemo(() => normalizeFlowData(canvasQuery.data?.data), [canvasQuery.data?.data]);

  const saveMutation = useApiMutation({
    mutationFn: (request: FlowiseCanvasUpsertRequest) => canvasApi.save(request),
    onSuccess: async () => {
      await canvasQuery.refetch();
    }
  });

  const validateMutation = useApiMutation({
    mutationFn: (request: FlowiseCanvasUpsertRequest) => canvasApi.validate(request)
  });

  const buildRequest = (nodes: FlowiseCanvasNode[], edges: FlowiseCanvasEdge[], viewport?: Viewport) =>
    buildCanvasUpsertRequest(resourceId ?? '', canvasQuery.data?.data.flowType ?? flowTypeFromMode(mode), nodes, edges, viewport);

  return {
    buildRequest,
    canvas: canvasQuery.data?.data,
    canvasQuery,
    chatflow: chatflowQuery.data?.data,
    chatflowQuery,
    chatflowType,
    flowData,
    saveMutation,
    validateMutation
  };
}
