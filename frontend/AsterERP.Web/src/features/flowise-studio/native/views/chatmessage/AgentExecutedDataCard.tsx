import { Box, Card, CardContent, Chip, Divider, Typography } from '@mui/material';

import { AppIcon } from '../../../../../shared/icons/AppIcon';
import type { FlowiseAgentExecutedNodeDto } from '../../../types/prediction.types';

interface AgentExecutedDataCardProps {
  execution: FlowiseAgentExecutedNodeDto;
}

export function AgentExecutedDataCard({ execution }: AgentExecutedDataCardProps) {
  const executionData = parseExecutionData(execution.dataJson);

  return (
    <Card className="flowise-agent-executed-card" variant="outlined">
      <CardContent>
        <Box className="flowise-agent-executed-card__header">
          <Box className="flowise-agent-executed-card__title">
            <AppIcon name={statusIcon(execution.status)} />
            <Box>
              <Typography variant="subtitle2">{execution.nodeLabel || execution.nodeId}</Typography>
              <Typography color="text.secondary" variant="caption">
                {executionData?.nodeType ?? execution.nodeId}
              </Typography>
            </Box>
          </Box>
          <Chip className={`flowise-agent-executed__status flowise-agent-executed__status--${execution.status.toLowerCase()}`} label={execution.status} size="small" />
        </Box>
        {execution.previousNodeIds.length > 0 ? (
          <Typography className="flowise-agent-executed-card__chain" color="text.secondary" variant="caption">
            {execution.previousNodeIds.join(' -> ')}
          </Typography>
        ) : null}
        {executionData ? <ExecutionDataSections data={executionData} rawJson={execution.dataJson} /> : <JsonBlock title="Metadata" value={execution.dataJson} />}
      </CardContent>
    </Card>
  );
}

function ExecutionDataSections({ data, rawJson }: { data: ParsedExecutionData; rawJson: string }) {
  const compactMetadata = {
    data: data.Data,
    executedAt: data.executedAt,
    nextNodeIds: data.nextNodeIds,
    nodeId: data.nodeId,
    nodeType: data.nodeType,
    previousNodeIds: data.previousNodeIds,
    status: data.status
  };

  return (
    <div className="flowise-agent-executed-card__sections">
      {data.input ? <JsonBlock title="Input" value={data.input} /> : null}
      {data.output ? <JsonBlock title="Output" value={data.output} /> : null}
      {data.metrics ? <JsonBlock title="Metrics" value={data.metrics} /> : null}
      <Divider />
      <JsonBlock title="Metadata" value={compactMetadata} />
      <JsonBlock collapsed title="Raw" value={rawJson} />
    </div>
  );
}

function JsonBlock({ collapsed = false, title, value }: { collapsed?: boolean; title: string; value: unknown }) {
  return (
    <details className="flowise-agent-executed-card__json" open={!collapsed}>
      <summary>{title}</summary>
      <pre>{formatJsonValue(value)}</pre>
    </details>
  );
}

function parseExecutionData(json: string): ParsedExecutionData | null {
  try {
    const parsed = JSON.parse(json) as ParsedExecutionData;
    return parsed && typeof parsed === 'object' ? parsed : null;
  } catch {
    return null;
  }
}

function formatJsonValue(value: unknown): string {
  if (typeof value === 'string') {
    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
      return value;
    }
  }

  return JSON.stringify(value, null, 2);
}

function statusIcon(status: string): 'checkCircle' | 'circleAlert' | 'loader' | 'gitBranch' {
  const normalized = status.toUpperCase();
  if (normalized === 'FINISHED') {
    return 'checkCircle';
  }

  if (normalized === 'INPROGRESS') {
    return 'loader';
  }

  if (['ERROR', 'TIMEOUT', 'TERMINATED', 'STOPPED'].includes(normalized)) {
    return 'circleAlert';
  }

  return 'gitBranch';
}

interface ParsedExecutionData {
  Data?: unknown;
  executedAt?: string;
  input?: unknown;
  metrics?: unknown;
  nextNodeIds?: string[];
  nodeId?: string;
  nodeType?: string;
  output?: unknown;
  previousNodeIds?: string[];
  status?: string;
}
