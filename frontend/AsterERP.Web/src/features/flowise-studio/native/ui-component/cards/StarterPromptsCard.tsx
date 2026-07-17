import { Box, Chip, type SxProps, type Theme } from '@mui/material';
import type { MouseEvent } from 'react';


interface StarterPromptItem {
  prompt: string;
}

interface StarterPromptsCardProps {
  isGrid?: boolean;
  starterPrompts: StarterPromptItem[];
  sx?: SxProps<Theme>;
  onPromptClick: (prompt: string, event: MouseEvent<HTMLDivElement>) => void;
}

export function StarterPromptsCard({ isGrid = false, starterPrompts, sx, onPromptClick }: StarterPromptsCardProps) {
  return (
    <Box
      className="flowise-starter-prompts-card"
      sx={{
        display: 'flex',
        flexWrap: 'wrap',
        gap: 1,
        maxWidth: isGrid ? 'inherit' : '400px',
        overflowX: 'auto',
        p: 1.5,
        scrollbarWidth: 'none',
        width: '100%',
        ...sx
      }}
    >
      {starterPrompts.map((starterPrompt, index) => (
        <Chip
          className="flowise-starter-prompts-card__button"
          key={`${starterPrompt.prompt}-${index}`}
          label={starterPrompt.prompt}
          onClick={(event) => onPromptClick(starterPrompt.prompt, event)}
        />
      ))}
    </Box>
  );
}
