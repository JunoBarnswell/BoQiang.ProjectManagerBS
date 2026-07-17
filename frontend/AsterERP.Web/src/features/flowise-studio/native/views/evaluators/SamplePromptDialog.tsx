import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';

interface SamplePromptDialogProps {
  open: boolean;
  onClose: () => void;
  onSelect: (promptTemplate: string) => void;
}

const samplePrompts = [
  {
    type: 'LLM',
    name: 'Answer correctness',
    prompt: 'You are evaluating whether the actual answer correctly answers the input. Return a JSON object with score and reason.'
  },
  {
    type: 'LLM',
    name: 'Faithfulness',
    prompt: 'Compare the actual answer against the expected output. Return a JSON object with score from 0 to 1 and a short reason.'
  },
  {
    type: 'LLM',
    name: 'JSON validity',
    prompt: 'Evaluate whether the actual output is valid JSON and satisfies the expected shape. Return score and reason.'
  }
];

export function SamplePromptDialog({ open, onClose, onSelect }: SamplePromptDialogProps) {
  const { translate } = useI18n();

  return (
    <Dialog fullWidth maxWidth="md" open={open} onClose={onClose}>
      <DialogTitle>{translate(flowiseI18nKeys.source.evaluators.samplePrompt)}</DialogTitle>
      <DialogContent>
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>{translate(flowiseI18nKeys.source.fields.type)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.name)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.details)}</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {samplePrompts.map((item) => (
                <TableRow hover key={item.name}>
                  <TableCell>{item.type}</TableCell>
                  <TableCell>{item.name}</TableCell>
                  <TableCell className="flowise-source-ellipsis">{item.prompt}</TableCell>
                  <TableCell align="right">
                    <Button size="small" variant="outlined" onClick={() => onSelect(item.prompt)}>
                      {translate(flowiseI18nKeys.common.save)}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
      </DialogActions>
    </Dialog>
  );
}
