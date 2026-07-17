import { useCallback } from 'react';

import { translateCurrentLocale } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';
import {
  exportKnowledgeGraph,
  importKnowledgeGraph,
  rebuildKnowledgeGraph
} from '../../api/knowledgeGraph.api';
import type {
  KnowledgeGraphApiImportResult,
  KnowledgeGraphFilterState
} from '../types';
import {
  buildExportRequest,
  buildExportText,
  buildGraphQuery,
  buildImportRequest,
  downloadTextFile
} from '../utils/knowledgeGraphFormatters';

interface KnowledgeGraphExchangeCommandOptions {
  filters: KnowledgeGraphFilterState;
  message: {
    error: (content: string) => void;
    info: (content: string) => void;
    success: (content: string) => void;
  };
  refresh: () => Promise<void>;
}

interface ApiDataEnvelope {
  data: unknown;
}

export function useKnowledgeGraphExchangeCommands({
  filters,
  message,
  refresh
}: KnowledgeGraphExchangeCommandOptions) {
  const importMutation = useApiMutation({ mutationFn: importKnowledgeGraph });
  const exportMutation = useApiMutation({ mutationFn: exportKnowledgeGraph });
  const rebuildMutation = useApiMutation({ mutationFn: rebuildKnowledgeGraph });

  const importGraph = useCallback(async (content: string, fileName: string): Promise<KnowledgeGraphApiImportResult | null> => {
    if (!content.trim()) {
      message.info(translateCurrentLocale('kg.exchange.error.emptyImportContent'));
      return null;
    }
    try {
      const response = await importMutation.mutateAsync(buildImportRequest(content, fileName));
      message.success(translateCurrentLocale('kg.exchange.success.imported'));
      await refresh();
      return readEnvelopeData(response) as KnowledgeGraphApiImportResult;
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLocale('kg.exchange.error.importFailed')));
      return null;
    }
  }, [importMutation, message, refresh]);

  const exportGraph = useCallback(async (format: 'json' | 'mermaid') => {
    try {
      const response = await exportMutation.mutateAsync(buildExportRequest(filters, format));
      const file = buildExportText(readEnvelopeData(response));
      downloadTextFile(file.fileName, file.content, file.mimeType);
      message.success(translateCurrentLocale('kg.exchange.success.exported'));
      return file.content;
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLocale('kg.exchange.error.exportFailed')));
      return '';
    }
  }, [exportMutation, filters, message]);

  const rebuild = useCallback(async () => {
    try {
      await rebuildMutation.mutateAsync(buildGraphQuery(filters));
      message.success(translateCurrentLocale('kg.exchange.success.rebuildSubmitted'));
      await refresh();
    } catch (error) {
      message.error(getErrorMessage(error, translateCurrentLocale('kg.exchange.error.rebuildFailed')));
    }
  }, [filters, message, rebuildMutation, refresh]);

  return {
    commands: {
      exportGraph,
      importGraph,
      rebuild
    },
    mutations: {
      exporting: exportMutation.isPending,
      importing: importMutation.isPending,
      rebuilding: rebuildMutation.isPending
    }
  };
}

function readEnvelopeData(response: unknown): unknown {
  return (response as ApiDataEnvelope).data;
}
