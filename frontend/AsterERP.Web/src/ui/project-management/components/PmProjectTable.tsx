import { Box, ButtonBase, Checkbox } from '@mui/material';
import { styled } from '@mui/material/styles';
import { useLayoutEffect, useRef, type ReactNode } from 'react';

import { PmChip, PmIconButton } from './PmControls';
import { PmIcon } from './PmIcon';
import { PmRow, PmText } from './PmSurface';

export type PmProjectTableColumn = 'name' | 'health' | 'priority' | 'lead' | 'targetDate' | 'issues' | 'status';

export type PmProjectTableSort = {
  field: string;
  direction: 'asc' | 'desc';
};

export interface PmProjectTableRow {
  id: string;
  projectName: string;
  health?: string;
  healthLabel: string;
  priorityLabel: string;
  leadLabel: string;
  targetDateLabel: string;
  issueCount: number;
  statusLabel: string;
  status: string;
}

const columnWidths: Record<PmProjectTableColumn, string> = {
  name: 'minmax(320px, 1fr)',
  health: '130px',
  priority: '110px',
  lead: '110px',
  targetDate: '140px',
  issues: '90px',
  status: '110px',
};

const Header = styled(PmRow)(({ theme }) => ({
  position: 'sticky',
  top: 0,
  zIndex: 2,
  minHeight: 38,
  color: theme.palette.text.secondary,
  backgroundColor: theme.palette.background.paper,
  borderBottom: `1px solid ${theme.palette.divider}`,
}));

const HeaderButton = styled(ButtonBase)(({ theme }) => ({
  minWidth: 0,
  justifyContent: 'flex-start',
  padding: theme.spacing(.75, 0),
  color: 'inherit',
  fontSize: '.7rem',
  textAlign: 'left',
  '&:focus-visible': { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: 2 },
}));

const TableViewport = styled(Box)({
  minWidth: 0,
  overflow: 'auto',
  maxHeight: '100%',
});

const NameButton = styled(ButtonBase)(({ theme }) => ({
  minWidth: 0,
  justifyContent: 'flex-start',
  color: 'inherit',
  textAlign: 'left',
  '&:focus-visible': { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: 2 },
}));

export function PmProjectTable({
  rows,
  columns,
  density,
  selectedIds,
  sort,
  labels,
  onSort,
  onToggleAll,
  onToggleRow,
  onRowSelect,
  onOpen,
  onContext,
  onFavorite,
  onEdit,
  onArchive,
  onDelete,
  initialScrollTop = 0,
  onViewportScroll,
}: {
  rows: PmProjectTableRow[];
  columns: PmProjectTableColumn[];
  density: 'compact' | 'default' | 'comfortable';
  selectedIds: ReadonlySet<string>;
  sort: PmProjectTableSort;
  labels: Record<PmProjectTableColumn, string> & { selectAll: string; favorite: string; edit: string; archive: string; delete: string };
  onSort: (field: PmProjectTableColumn) => void;
  onToggleAll: () => void;
  onToggleRow: (id: string) => void;
  onRowSelect: (row: PmProjectTableRow) => void;
  onOpen: (row: PmProjectTableRow) => void;
  onContext: (row: PmProjectTableRow, event: React.MouseEvent) => void;
  onFavorite: (row: PmProjectTableRow) => void;
  onEdit?: (row: PmProjectTableRow) => void;
  onArchive?: (row: PmProjectTableRow) => void;
  onDelete?: (row: PmProjectTableRow) => void;
  initialScrollTop?: number;
  onViewportScroll?: (scrollTop: number) => void;
}) {
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const grid = columns.map(column => columnWidths[column]).join(' ');
  const allSelected = rows.length > 0 && rows.every(row => selectedIds.has(row.id));
  const densityHeight = density === 'compact' ? 42 : density === 'comfortable' ? 56 : 48;
  useLayoutEffect(() => {
    if (viewportRef.current && initialScrollTop > 0) viewportRef.current.scrollTop = initialScrollTop;
  }, [initialScrollTop, rows.length]);
  return <TableViewport ref={viewportRef} onScroll={event => onViewportScroll?.(event.currentTarget.scrollTop)} role="grid" aria-rowcount={rows.length + 1}>
    <Header sx={{ gridTemplateColumns: `40px ${grid}` }}>
      <Checkbox aria-label={labels.selectAll} checked={allSelected} indeterminate={selectedIds.size > 0 && !allSelected} onChange={onToggleAll} size="small" />
      {columns.map(column => <HeaderButton aria-sort={sort.field === column ? sort.direction === 'asc' ? 'ascending' : 'descending' : 'none'} key={column} onClick={() => onSort(column)}>
        <span>{labels[column]}</span>
        {sort.field === column ? <PmIcon name={sort.direction === 'asc' ? 'chevronUp' : 'chevronDown'} size={13} /> : null}
      </HeaderButton>)}
    </Header>
    {rows.map((row, rowIndex) => <ProjectTableRow
      key={row.id}
      columns={columns}
      densityHeight={densityHeight}
      labels={labels}
      rowIndex={rowIndex}
      onArchive={onArchive}
      onContext={event => onContext(row, event)}
      onDelete={onDelete}
      onEdit={onEdit}
      onFavorite={onFavorite}
      onOpen={onOpen}
      onRowSelect={onRowSelect}
      onToggleRow={onToggleRow}
      row={row}
      selected={selectedIds.has(row.id)}
    />)}
  </TableViewport>;
}

function ProjectTableRow({ row, rowIndex, columns, densityHeight, selected, labels, onToggleRow, onRowSelect, onOpen, onContext, onFavorite, onEdit, onArchive, onDelete }: {
  row: PmProjectTableRow;
  rowIndex: number;
  columns: PmProjectTableColumn[];
  densityHeight: number;
  selected: boolean;
  labels: Record<PmProjectTableColumn, string> & { selectAll: string; favorite: string; edit: string; archive: string; delete: string };
  onToggleRow: (id: string) => void;
  onRowSelect: (row: PmProjectTableRow) => void;
  onOpen: (row: PmProjectTableRow) => void;
  onContext: (event: React.MouseEvent) => void;
  onFavorite: (row: PmProjectTableRow) => void;
  onEdit?: (row: PmProjectTableRow) => void;
  onArchive?: (row: PmProjectTableRow) => void;
  onDelete?: (row: PmProjectTableRow) => void;
}) {
  const grid = columns.map(column => columnWidths[column]).join(' ');
  const cells: Record<PmProjectTableColumn, ReactNode> = {
    name: <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}><PmIcon name="folder" size={16} /><NameButton onClick={event => { event.stopPropagation(); onOpen(row); }}><PmText noWrap fontSize="inherit" fontWeight={600}>{row.projectName}</PmText></NameButton></Box>,
    health: <Box sx={{ display: 'flex', alignItems: 'center', gap: .5 }}><Box aria-hidden="true" sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: row.health === 'AtRisk' || row.health === 'OffTrack' ? 'error.main' : 'success.main' }} /><PmText fontSize="inherit">{row.healthLabel}</PmText></Box>,
    priority: <PmText fontSize="inherit">{row.priorityLabel}</PmText>,
    lead: <PmText noWrap fontSize="inherit">{row.leadLabel}</PmText>,
    targetDate: <PmText fontSize="inherit">{row.targetDateLabel}</PmText>,
    issues: <PmText color="primary.main" fontSize="inherit">{row.issueCount}</PmText>,
    status: <PmChip label={row.statusLabel} color={row.status === 'Completed' ? 'success' : row.status === 'Active' ? 'primary' : 'default'} />,
  };
  return <PmRow aria-selected={selected} data-home-row={rowIndex} data-project-row={row.id} onClick={() => onRowSelect(row)} onContextMenu={event => { event.preventDefault(); onContext(event); }} onKeyDown={event => {
    if (event.key === ' ') {
      event.preventDefault();
      onToggleRow(row.id);
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      onOpen(row);
    }
    if (event.key === 'F10' && event.shiftKey) {
      event.preventDefault();
      const target = event.currentTarget.getBoundingClientRect();
      onContext({ preventDefault: () => undefined, clientX: target.left, clientY: target.top } as React.MouseEvent);
    }
  }} role="row" tabIndex={0} sx={{ gridTemplateColumns: `40px ${grid}`, minHeight: densityHeight, fontSize: '.78rem', cursor: 'pointer', bgcolor: selected ? 'action.selected' : undefined, position: 'relative', '&:hover .pm-project-row-actions': { opacity: 1 } }}>
    <Checkbox aria-label={row.projectName} checked={selected} onChange={() => onToggleRow(row.id)} onClick={event => event.stopPropagation()} size="small" />
    {columns.map(column => <Box key={column} sx={{ minWidth: 0, overflow: 'hidden' }}>{cells[column]}</Box>)}
    <Box className="pm-project-row-actions" onClick={event => event.stopPropagation()} sx={{ display: 'flex', gap: .25, position: 'absolute', right: 4, opacity: 0, bgcolor: 'background.paper' }}>
      <PmIconButton aria-label={labels.favorite} onClick={() => onFavorite(row)} size="small"><PmIcon name="star" /></PmIconButton>
      {onEdit ? <PmIconButton aria-label={labels.edit} onClick={() => onEdit(row)} size="small"><PmIcon name="settings" /></PmIconButton> : null}
      {onArchive ? <PmIconButton aria-label={labels.archive} onClick={() => onArchive(row)} size="small"><PmIcon name="archive" /></PmIconButton> : null}
      {onDelete ? <PmIconButton aria-label={labels.delete} onClick={() => onDelete(row)} size="small"><PmIcon name="trash" /></PmIconButton> : null}
    </Box>
  </PmRow>;
}
