import { TextField } from '@mui/material';
import type { NodeProps } from '@xyflow/react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseCanvasNode as FlowiseCanvasNodeType } from '../../../types/canvas.types';

export function StickyNote({ data, id, selected }: NodeProps<FlowiseCanvasNodeType>) {
  const { translate } = useI18n();
  return (
    <div className={['flowise-sticky-note', selected ? 'flowise-sticky-note--selected' : ''].filter(Boolean).join(' ')}>
      <strong>{data.displayName}</strong>
      <TextField
        className="flowise-sticky-note__input"
        fullWidth
        multiline
        minRows={4}
        variant="standard"
        value={String(data.config?.text ?? '')}
        placeholder={translate(flowiseI18nKeys.canvas.stickyNotePlaceholder)}
        slotProps={{
          input: {
            disableUnderline: true
          }
        }}
        onChange={(event) => data.onStickyTextChange?.(id, event.target.value)}
      />
    </div>
  );
}
