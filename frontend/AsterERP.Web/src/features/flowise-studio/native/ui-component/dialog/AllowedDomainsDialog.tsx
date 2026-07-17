
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  OutlinedInput,
  Stack,
  Tooltip,
  Typography
} from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';

interface AllowedDomainsDialogProps {
  domains: string[];
  errorMessage: string;
  open: boolean;
  saving: boolean;
  title: string;
  onClose: () => void;
  onConfirm: (payload: { domains: string[]; errorMessage: string }) => void;
}

export function AllowedDomainsDialog({ domains, errorMessage, open, saving, title, onClose, onConfirm }: AllowedDomainsDialogProps) {
  const { translate } = useI18n();
  const [domainRows, setDomainRows] = useState<string[]>(normalizeDomainRows(domains));
  const [draftErrorMessage, setDraftErrorMessage] = useState(errorMessage);

  useEffect(() => {
    if (open) {
      setDomainRows(normalizeDomainRows(domains));
      setDraftErrorMessage(errorMessage);
    }
  }, [domains, errorMessage, open]);

  const updateDomain = (index: number, value: string) => {
    setDomainRows((current) => current.map((domain, domainIndex) => (domainIndex === index ? value : domain)));
  };

  const addDomain = () => {
    setDomainRows((current) => [...current, '']);
  };

  const removeDomain = (index: number) => {
    setDomainRows((current) => {
      const next = current.filter((_, domainIndex) => domainIndex !== index);
      return next.length > 0 ? next : [''];
    });
  };

  const saveDomains = () => {
    onConfirm({ domains: splitDomains(domainRows), errorMessage: draftErrorMessage });
  };

  return (
    <Dialog
      fullWidth
      maxWidth="sm"
      open={open}
      aria-describedby="flowise-allowed-domains-description"
      aria-labelledby="flowise-allowed-domains-title"
      onClose={onClose}
    >
      <DialogTitle id="flowise-allowed-domains-title" sx={{ fontSize: '1rem' }}>
        {title}
      </DialogTitle>
      <DialogContent id="flowise-allowed-domains-description">
        <Stack direction="column" spacing={2} sx={{ pt: 1, width: '100%' }}>
          <Stack direction="column" spacing={1}>
            <Typography component="div" sx={{ alignItems: 'center', display: 'flex', gap: 1 }}>
              {translate(flowiseI18nKeys.configuration.allowedDomains)}
              <Tooltip title={translate(flowiseI18nKeys.messages.allowedDomainsHelp)}>
                <Box component="span" sx={{ color: 'text.secondary', cursor: 'help', fontSize: 13 }}>
                  ?
                </Box>
              </Tooltip>
            </Typography>
            <Stack direction="column" spacing={1}>
              {domainRows.map((domain, index) => (
                <Box key={`${index}-${domainRows.length}`} sx={{ display: 'flex', gap: 1, width: '100%' }}>
                  <OutlinedInput
                    fullWidth
                    name="origin"
                    placeholder={translate(flowiseI18nKeys.messages.allowedDomainPlaceholder)}
                    size="small"
                    type="text"
                    value={domain}
                    endAdornment={(
                      <InputAdornment position="end">
                        {domainRows.length > 1 ? (
                          <IconButton
                            color="error"
                            edge="end"
                            size="small"
                            sx={{ height: 30, width: 30 }}
                            aria-label={translate(flowiseI18nKeys.actions.remove)}
                            onClick={() => removeDomain(index)}
                          >
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        ) : null}
                      </InputAdornment>
                    )}
                    onChange={(event) => updateDomain(index, event.target.value)}
                  />
                  {index === domainRows.length - 1 ? (
                    <IconButton color="primary" aria-label={translate(flowiseI18nKeys.actions.addNew)} onClick={addDomain}>
                      <AddIcon />
                    </IconButton>
                  ) : (
                    <Box sx={{ width: 40 }} />
                  )}
                </Box>
              ))}
            </Stack>
            <Typography color="text.secondary" variant="caption">
              {translate(flowiseI18nKeys.messages.oneDomainPerLine)}
            </Typography>
          </Stack>
          <Stack direction="column" spacing={1}>
            <Typography component="div" sx={{ alignItems: 'center', display: 'flex', gap: 1 }}>
              {translate(flowiseI18nKeys.configuration.allowedDomainsError)}
              <Tooltip title={translate(flowiseI18nKeys.messages.allowedDomainsErrorHelp)}>
                <Box component="span" sx={{ color: 'text.secondary', cursor: 'help', fontSize: 13 }}>
                  ?
                </Box>
              </Tooltip>
            </Typography>
            <OutlinedInput
              fullWidth
              placeholder={translate(flowiseI18nKeys.messages.unauthorizedDomainPlaceholder)}
              size="small"
              type="text"
              value={draftErrorMessage}
              onChange={(event) => setDraftErrorMessage(event.target.value)}
            />
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button color="inherit" type="button" onClick={onClose}>
          {translate(flowiseI18nKeys.common.cancel)}
        </Button>
        <Button disabled={saving} type="button" variant="contained" onClick={saveDomains}>
          {translate(flowiseI18nKeys.common.save)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function normalizeDomainRows(domains: string[]) {
  return domains.length > 0 ? domains : [''];
}

function splitDomains(rows: string[]) {
  return rows.flatMap((value) => value.split(/[\n,;]/)).map((item) => item.trim()).filter(Boolean);
}
