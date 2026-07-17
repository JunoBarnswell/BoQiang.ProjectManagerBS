import { Box, Card, CardContent, Chip, Typography } from '@mui/material';
import type { ReactNode } from 'react';


import { AppIcon } from '../../../../../shared/icons/AppIcon';
import type { FlowiseAgentReasoningDto } from '../../../types/prediction.types';

interface AgentReasoningCardProps {
  agent: FlowiseAgentReasoningDto;
  index: number;
  renderArtifacts: (json: string) => ReactNode;
  renderSourceDocuments: (documents: FlowiseAgentReasoningDto['sourceDocuments']) => ReactNode;
  renderState: (json: string) => ReactNode;
  renderUsedTools: (tools: FlowiseAgentReasoningDto['usedTools']) => ReactNode;
  translate: (key: string) => string;
  completedText: string;
}

export function AgentReasoningCard({
  agent,
  completedText,
  index,
  renderArtifacts,
  renderSourceDocuments,
  renderState,
  renderUsedTools
}: AgentReasoningCardProps) {
  if (agent.nextAgent) {
    return (
      <Card className="flowise-agent-reasoning-card flowise-agent-reasoning-card--next" variant="outlined">
        <CardContent>
          <Box sx={{ alignItems: 'center', display: 'flex', gap: 1 }}>
            <AppIcon name="arrowRight" />
            <Typography variant="body2">{agent.nextAgent}</Typography>
          </Box>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="flowise-agent-reasoning-card" variant="outlined">
      <CardContent>
        <Box sx={{ alignItems: 'center', display: 'flex', gap: 1 }}>
          <AppIcon name="robot" />
          <Typography variant="subtitle2">{agent.agentName || agent.nodeName || `#${index + 1}`}</Typography>
          {agent.nodeName ? <Chip label={agent.nodeName} size="small" variant="outlined" /> : null}
        </Box>
        {agent.usedTools.length > 0 ? renderUsedTools(agent.usedTools) : null}
        {agent.messages.length > 0 ? (
          <Typography className="flowise-agent-reasoning-card__messages" component="pre" variant="body2">
            {agent.messages.join('\n')}
          </Typography>
        ) : agent.instructions ? (
          <Typography variant="body2">{agent.instructions}</Typography>
        ) : (
          <Typography color="text.secondary" variant="body2">
            {completedText}
          </Typography>
        )}
        {agent.instructions && agent.messages.length > 0 ? <Typography variant="body2">{agent.instructions}</Typography> : null}
        {agent.stateJson ? renderState(agent.stateJson) : null}
        {agent.artifactsJson ? renderArtifacts(agent.artifactsJson) : null}
        {agent.sourceDocuments.length > 0 ? renderSourceDocuments(agent.sourceDocuments) : null}
      </CardContent>
    </Card>
  );
}
