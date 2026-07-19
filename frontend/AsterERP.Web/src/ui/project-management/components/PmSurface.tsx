import { Box, Divider, Skeleton, Typography } from '@mui/material';
import { styled } from '@mui/material/styles';
import type { ComponentProps, ReactNode } from 'react';

export const PmSurface = styled(Box)(({ theme }) => ({ padding: theme.spacing(2.5, 3), minWidth: 0 }));
export const PmSection = styled(Box)(({ theme }) => ({ padding: theme.spacing(2, 0), minWidth: 0 }));
export const PmRow = styled(Box)(({ theme }) => ({ display: 'grid', alignItems: 'center', minHeight: 52, borderBottom: `1px solid ${theme.palette.divider}`, '&:hover': { backgroundColor: theme.palette.action.hover } }));
export function PmHeading({ title, action }: { title: string; action?: ReactNode }) { return <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}><PmText variant="h2" fontSize="1rem" fontWeight={650}>{title}</PmText>{action}</Box>; }
export const PmDivider = styled(Divider)({});
export function PmText({ fontSize, fontWeight, ...props }: ComponentProps<typeof Typography> & { fontSize?: string; fontWeight?: number }) { return <Typography {...props} sx={{ ...props.sx, fontSize, fontWeight }} />; }
export const PmHelperText = styled('p')(({ theme }) => ({ margin: 0, color: theme.palette.text.secondary, fontSize: '.75rem' }));
export function PmSkeletonRows({ count = 6 }: { count?: number }) { return <Box sx={{ display: 'grid', gap: 1 }}>{Array.from({ length: count }, (_, index) => <Skeleton key={index} variant="rounded" height={44} />)}</Box>; }
