import { useCallback, useState } from 'react';

import { translateCurrentLocale } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';
import {
  analyzeKnowledgeGraphImpact,
  analyzeKnowledgeGraphPath
} from '../../api/knowledgeGraph.api';
import type { KnowledgeGraphUiState } from '../state/useKnowledgeGraphUiStore';
import type { KnowledgeGraphImpactView, KnowledgeGraphPathView } from '../types';
import {
  buildImpactRequest,
  buildPathRequest,
  normalizeImpactAnalysisResult,
  normalizePathAnalysisResult
} from '../utils/knowledgeGraphFormatters';

interface KnowledgeGraphAnalysisCommandOptions {
  message: {
    error: (content: string) => void;
    info: (content: string) => void;
  };
  store: KnowledgeGraphUiState;
}

interface ApiDataEnvelope {
  data: unknown;
}

export function useKnowledgeGraphAnalysisCommands({ message, store }: KnowledgeGraphAnalysisCommandOptions) {
  const [pathResults, setPathResults] = useState<KnowledgeGraphPathView[]>([]);
  const [impactResults, setImpactResults] = useState<KnowledgeGraphImpactView[]>([]);
  const pathMutation = useApiMutation({ mutationFn: analyzeKnowledgeGraphPath });
  const impactMutation = useApiMutation({ mutationFn: analyzeKnowledgeGraphImpact });

  const runPathAnalysis = useCallback(async () => {
    if (!store.pathDraft.sourceNodeId || !store.pathDraft.targetNodeId) {
      message.info(translateCurrentLocale('kg.analysis.error.selectPathEndpoints'));
      return;
    }
    try {
      const response = await pathMutation.mutateAsync(buildPathRequest(store.pathDraft));
      setPathResults(normalizePathAnalysisResult(readEnvelopeData(response)));
      store.setActivePanel('analysis');
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLocale('kg.analysis.error.pathFailed')));
    }
  }, [message, pathMutation, store]);

  const runImpactAnalysis = useCallback(async () => {
    if (!store.impactDraft.nodeId) {
      message.info(translateCurrentLocale('kg.analysis.error.selectImpactNode'));
      return;
    }
    try {
      const response = await impactMutation.mutateAsync(buildImpactRequest(store.impactDraft));
      setImpactResults(normalizeImpactAnalysisResult(readEnvelopeData(response)));
      store.setActivePanel('analysis');
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLocale('kg.analysis.error.impactFailed')));
    }
  }, [impactMutation, message, store]);

  return {
    impactResults,
    impactRunning: impactMutation.isPending,
    pathResults,
    pathRunning: pathMutation.isPending,
    runImpactAnalysis,
    runPathAnalysis
  };
}

function readEnvelopeData(response: unknown): unknown {
  return (response as ApiDataEnvelope).data;
}
