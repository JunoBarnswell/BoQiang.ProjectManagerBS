import { Box, Typography } from '@mui/material';
import type { ReactNode } from 'react';

import { PmText } from './PmSurface';

export interface PmBreadcrumbItem {
  label: string;
  icon?: ReactNode;
  current?: boolean;
  onClick?: () => void;
}

export function PmBreadcrumbs({ items }: { items: PmBreadcrumbItem[] }) {
  return <Box aria-label="breadcrumb" component="nav" sx={{ display: 'flex', alignItems: 'center', minWidth: 0, gap: .5, minHeight: 40, overflow: 'hidden' }}>
    {items.map((item, index) => <Box key={`${item.label}-${index}`} sx={{ display: 'flex', alignItems: 'center', minWidth: 0, gap: .5 }}>
      {index > 0 && <Typography aria-hidden="true" color="text.disabled" component="span">/</Typography>}
      {item.onClick && !item.current ? <Box component="button" onClick={item.onClick} sx={{ display: 'inline-flex', alignItems: 'center', gap: .5, minWidth: 0, border: 0, background: 'transparent', color: 'text.secondary', cursor: 'pointer', p: 0, '&:hover': { color: 'text.primary' } }}>{item.icon}{<PmText noWrap fontSize=".78rem">{item.label}</PmText>}</Box> : <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: .5, minWidth: 0 }}>{item.icon}<PmText color={item.current ? 'text.primary' : 'text.secondary'} fontWeight={item.current ? 650 : 400} noWrap fontSize=".78rem">{item.label}</PmText></Box>}
    </Box>)}
  </Box>;
}
