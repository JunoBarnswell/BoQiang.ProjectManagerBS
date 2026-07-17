import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';

interface TagDialogProps {
  initialCategory?: string | null;
  open: boolean;
  saving: boolean;
  onClose: () => void;
  onSubmit: (categories: string[]) => void;
}

export function TagDialog({ initialCategory, open, saving, onClose, onSubmit }: TagDialogProps) {
  const { translate } = useI18n();
  const [inputValue, setInputValue] = useState('');
  const [categoryValues, setCategoryValues] = useState<string[]>([]);

  useEffect(() => {
    if (open) {
      setInputValue('');
      setCategoryValues(splitCategory(initialCategory ?? ''));
    }
  }, [initialCategory, open]);

  const addInputTag = () => {
    const nextValue = inputValue.trim();

    if (!nextValue || categoryValues.includes(nextValue)) {
      setInputValue('');
      return categoryValues;
    }

    const nextValues = [...categoryValues, nextValue];
    setCategoryValues(nextValues);
    setInputValue('');
    return nextValues;
  };

  const handleSubmit = () => {
    const nextValues = addInputTag();
    onSubmit(nextValues);
  };

  return (
    <Dialog
      aria-describedby="flowise-category-dialog-description"
      aria-labelledby="flowise-category-dialog-title"
      fullWidth
      maxWidth="xs"
      open={open}
      onClose={onClose}
    >
      <DialogTitle id="flowise-category-dialog-title" sx={{ fontSize: '1rem' }}>
        {translate(flowiseI18nKeys.actions.updateCategory)}
      </DialogTitle>
      <DialogContent id="flowise-category-dialog-description">
        <Box>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              handleSubmit();
            }}
          >
            {categoryValues.length > 0 ? (
              <Box sx={{ mb: 1.25 }}>
                {categoryValues.map((category) => (
                  <Chip
                    key={category}
                    label={category}
                    sx={{ mb: 0.625, mr: 0.625 }}
                    onDelete={() => setCategoryValues((current) => current.filter((item) => item !== category))}
                  />
                ))}
              </Box>
            ) : null}
            <TextField
              fullWidth
              label={translate(flowiseI18nKeys.fields.tag)}
              sx={{ mt: 2 }}
              value={inputValue}
              variant="outlined"
              onChange={(event) => setInputValue(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && inputValue.trim()) {
                  event.preventDefault();
                  addInputTag();
                }
              }}
            />
            <Typography color="text.secondary" sx={{ fontStyle: 'italic', mt: 1 }} variant="body2">
              {translate(flowiseI18nKeys.messages.categoryTagHelp)}
            </Typography>
          </form>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <Button disabled={saving} variant="contained" onClick={handleSubmit}>
          {translate(flowiseI18nKeys.actions.submit)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function splitCategory(category: string) {
  return category.split(/[;,]/).map((item) => item.trim()).filter(Boolean);
}
