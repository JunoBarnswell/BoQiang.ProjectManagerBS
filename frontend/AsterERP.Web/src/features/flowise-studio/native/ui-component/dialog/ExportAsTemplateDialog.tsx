import {
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  OutlinedInput,
  Stack,
  Typography
} from '@mui/material';
import { useEffect, useState, type ReactNode } from 'react';


import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseChatflowDto } from '../../../types/chatflow.types';

export interface ExportAsTemplatePayload {
  category?: string | null;
  description?: string | null;
  displayName: string;
  resourceKey: string;
}

interface ExportAsTemplateDialogProps {
  item: FlowiseChatflowDto;
  open: boolean;
  saving: boolean;
  onClose: () => void;
  onConfirm: (payload: ExportAsTemplatePayload) => void;
}

export function ExportAsTemplateDialog({ item, open, saving, onClose, onConfirm }: ExportAsTemplateDialogProps) {
  const { translate } = useI18n();
  const [resourceKey, setResourceKey] = useState(toTemplateKey(item.name));
  const [displayName, setDisplayName] = useState(item.name);
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState(item.category ?? '');
  const [categoryInput, setCategoryInput] = useState('');

  useEffect(() => {
    if (open) {
      setResourceKey(toTemplateKey(item.name));
      setDisplayName(item.name);
      setDescription('');
      setCategory(item.category ?? '');
      setCategoryInput('');
    }
  }, [item.category, item.name, open]);

  const categoryTags = splitCategory(category);

  const addCategory = () => {
    const next = categoryInput.trim();
    if (!next || categoryTags.includes(next)) {
      setCategoryInput('');
      return;
    }
    setCategory([...categoryTags, next].join(', '));
    setCategoryInput('');
  };

  const removeCategory = (tag: string) => {
    setCategory(categoryTags.filter((item) => item !== tag).join(', '));
  };

  const submitTemplate = () => {
    onConfirm({
      category: categoryTags.join(', ') || null,
      description: description.trim() || null,
      displayName: displayName.trim() || item.name,
      resourceKey: resourceKey.trim() || toTemplateKey(item.name)
    });
  };

  return (
    <Dialog
      fullWidth
      maxWidth="sm"
      open={open}
      aria-describedby="flowise-export-template-description"
      aria-labelledby="flowise-export-template-title"
      onClose={onClose}
    >
      <DialogTitle id="flowise-export-template-title" sx={{ fontSize: '1rem' }}>
        {translate(flowiseI18nKeys.detail.templateExport)}
      </DialogTitle>
      <DialogContent id="flowise-export-template-description">
        <Stack direction="column" spacing={2} sx={{ py: 2 }}>
          <TemplateField required label={translate(flowiseI18nKeys.fields.name)}>
            <OutlinedInput
              fullWidth
              id="flowise-template-name"
              name="name"
              size="small"
              type="text"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
            />
          </TemplateField>
          <TemplateField label={translate(flowiseI18nKeys.fields.key)}>
            <OutlinedInput
              fullWidth
              id="flowise-template-key"
              name="resourceKey"
              size="small"
              type="text"
              value={resourceKey}
              onChange={(event) => setResourceKey(event.target.value)}
            />
          </TemplateField>
          <TemplateField label={translate(flowiseI18nKeys.fields.description)}>
            <OutlinedInput
              fullWidth
              multiline
              id="flowise-template-description"
              name="description"
              rows={2}
              size="small"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </TemplateField>
          <TemplateField label={translate(flowiseI18nKeys.fields.category)}>
            <Stack direction="column" spacing={1}>
              {categoryTags.length > 0 ? (
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75 }}>
                  {categoryTags.map((tag) => (
                    <Chip key={tag} label={tag} onDelete={() => removeCategory(tag)} />
                  ))}
                </Box>
              ) : null}
              <OutlinedInput
                fullWidth
                id="flowise-template-category"
                name="category"
                size="small"
                type="text"
                value={categoryInput}
                onBlur={addCategory}
                onChange={(event) => setCategoryInput(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    addCategory();
                  }
                }}
              />
              <Typography color="text.secondary" sx={{ fontStyle: 'italic' }} variant="body2">
                {translate(flowiseI18nKeys.messages.categoryTagHelp)}
              </Typography>
            </Stack>
          </TemplateField>
          <Box
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 1,
              display: 'grid',
              gap: 1,
              gridTemplateColumns: '120px 1fr',
              p: 1.5
            }}
          >
            <Typography color="text.secondary" variant="body2">{translate(flowiseI18nKeys.fields.name)}</Typography>
            <Typography sx={{ fontWeight: 600 }} variant="body2">{item.name}</Typography>
            <Typography color="text.secondary" variant="body2">{translate(flowiseI18nKeys.fields.category)}</Typography>
            <Typography sx={{ fontWeight: 600 }} variant="body2">{item.category || translate(flowiseI18nKeys.common.none)}</Typography>
            <Typography color="text.secondary" variant="body2">{translate(flowiseI18nKeys.fields.type)}</Typography>
            <Typography sx={{ fontWeight: 600 }} variant="body2">{item.type}</Typography>
          </Box>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button color="inherit" type="button" onClick={onClose}>
          {translate(flowiseI18nKeys.common.cancel)}
        </Button>
        <Button disabled={saving || !displayName.trim()} type="button" variant="contained" onClick={submitTemplate}>
          {translate(flowiseI18nKeys.actions.saveAsTemplate)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

interface TemplateFieldProps {
  children: ReactNode;
  label: string;
  required?: boolean;
}

function TemplateField({ children, label, required = false }: TemplateFieldProps) {
  return (
    <Box sx={{ alignItems: 'flex-start', display: 'flex', flexDirection: 'column' }}>
      <Typography sx={{ mb: 1 }}>
        {label}
        {required ? <Box component="span" sx={{ color: 'error.main' }}>&nbsp;*</Box> : null}
      </Typography>
      {children}
    </Box>
  );
}

function toTemplateKey(name: string) {
  const normalized = name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9\u4e00-\u9fa5]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return normalized || `flow-template-${Date.now()}`;
}

function splitCategory(value: string) {
  return value.split(/[,;，；]/).map((item) => item.trim()).filter(Boolean);
}
