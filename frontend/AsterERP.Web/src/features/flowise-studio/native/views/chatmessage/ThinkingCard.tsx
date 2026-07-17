import { Box, CircularProgress, Collapse, Typography } from '@mui/material';
import { useEffect, useState } from 'react';


import { AppIcon } from '../../../../../shared/icons/AppIcon';

interface ThinkingCardProps {
  isThinking: boolean;
  thinking: string;
  title: string;
}

export function ThinkingCard({ isThinking, thinking, title }: ThinkingCardProps) {
  const [expanded, setExpanded] = useState(false);

  useEffect(() => {
    if (isThinking) {
      setExpanded(true);
    }
  }, [isThinking]);

  if (!thinking.trim()) {
    return null;
  }

  const lines = thinking.split('\n').filter((line) => line.trim().length > 0);

  return (
    <Box className="flowise-thinking-card">
      <Box className="flowise-thinking-card__header" onClick={() => setExpanded((current) => !current)}>
        <AppIcon name={expanded ? 'chevronDown' : 'chevronRight'} />
        {isThinking ? <CircularProgress size={14} thickness={5} /> : <AppIcon name="activity" />}
        <Typography noWrap variant="body2">
          {title}
        </Typography>
      </Box>
      <Collapse in={expanded} unmountOnExit>
        <Box className="flowise-thinking-card__content">
          {lines.length > 0 ? (
            <Box component="ul">
              {lines.map((line, index) => (
                <Typography component="li" key={`${line}-${index}`} variant="body2">
                  {line}
                </Typography>
              ))}
            </Box>
          ) : (
            <Typography variant="body2">{thinking}</Typography>
          )}
        </Box>
      </Collapse>
    </Box>
  );
}
