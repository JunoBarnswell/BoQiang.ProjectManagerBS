import { Box, Stack, type BoxProps, type StackProps } from '@mui/material';
import { styled } from '@mui/material/styles';

export const PmPage = styled(Box)(({ theme }) => ({
  display: 'flex',
  minWidth: 0,
  minHeight: 0,
  height: '100%',
  backgroundColor: theme.palette.background.default,
  color: theme.palette.text.primary,
}));

export const PmPane = styled(Box, { shouldForwardProp: (prop) => prop !== 'paneWidth' })<BoxProps & { paneWidth?: number }>(({ theme, paneWidth }) => ({
  minWidth: 0,
  minHeight: 0,
  flex: paneWidth ? `0 0 ${paneWidth}px` : '1 1 auto',
  overflow: 'auto',
  borderColor: theme.palette.divider,
}));

export const PmStack = (props: StackProps) => <Stack {...props} />;
export const PmInline = styled(Box)({ display: 'flex', alignItems: 'center', minWidth: 0 });
export const PmBox = Box;
export const PmContent = styled(Box)({ position: 'relative', display: 'flex', flex: 1, minWidth: 0, minHeight: 0, overflow: 'hidden' });
export const PmMobileTrigger = styled('button')(({ theme }) => ({ display: 'none', position: 'absolute', top: 10, left: 10, zIndex: 5, width: 32, height: 32, border: `1px solid ${theme.palette.divider}`, borderRadius: theme.shape.borderRadius, background: theme.palette.background.paper, color: theme.palette.text.primary, cursor: 'pointer', '@media (max-width: 899px)': { display: 'inline-grid', placeItems: 'center' } }));
