import { useMemo } from 'react';

import { useApiQuery } from '../../../../core/query/useApiQuery';
import {
  fetchKnowledgeGraphOverview,
  fetchKnowledgeGraphSnapshot,
  fetchKnowledgeGraphTasks
} from '../../api/knowledgeGraph.api';
import type { KnowledgeGraphFilterState } from '../types';
import { getKnowledgeGraphOptions } from '../utils/knowledgeGraphFlow';
import {
  buildGraphQuery,
  buildTaskQuery,
  normalizeKnowledgeGraphOverview,
  normalizeKnowledgeGraphSnapshot,
  normalizeKnowledgeGraphTasks
} from '../utils/knowledgeGraphFormatters';

interface ApiDataEnvelope {
  data: unknown;
}

export function useKnowledgeGraphQueries(filters: KnowledgeGraphFilterState) {
  const graphQueryPayload = useMemo(() => buildGraphQuery(filters), [filters]);
  const taskQueryPayload = useMemo(() => buildTaskQuery(filters), [filters]);

  const overviewQuery = useApiQuery({
    queryKey: ['ai', 'knowledge-graph', 'overview'],
    queryFn: ({ signal }) => fetchKnowledgeGraphOverview(signal),
    select: (response) => normalizeKnowledgeGraphOverview(readEnvelopeData(response))
  });
  const graphQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: ['ai', 'knowledge-graph', 'snapshot', graphQueryPayload],
    queryFn: ({ signal }) => fetchKnowledgeGraphSnapshot(graphQueryPayload, signal),
    select: (response) => normalizeKnowledgeGraphSnapshot(readEnvelopeData(response))
  });
  const tasksQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: ['ai', 'knowledge-graph', 'tasks', taskQueryPayload],
    queryFn: ({ signal }) => fetchKnowledgeGraphTasks(taskQueryPayload, signal),
    select: (response) => normalizeKnowledgeGraphTasks(readEnvelopeData(response))
  });

  const snapshot = graphQuery.data ?? normalizeKnowledgeGraphSnapshot(null);
  const options = useMemo(() => getKnowledgeGraphOptions(snapshot), [snapshot]);

  return {
    options,
    overviewQuery,
    queries: {
      graph: graphQuery,
      overview: overviewQuery,
      tasks: tasksQuery
    },
    snapshot,
    tasks: tasksQuery.data ?? []
  };
}

function readEnvelopeData(response: unknown): unknown {
  return (response as ApiDataEnvelope).data;
}
