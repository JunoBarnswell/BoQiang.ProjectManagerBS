import { Box, Chip, Paper, Skeleton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField } from '@mui/material';
import { useState } from 'react';

import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { managementApi } from '../../../api/management.api';
import logsEmptySvg from '../../assets/images/logs_empty.svg';
import { buildSourceQuery, formatSourceDate, getSourcePageTotalPages, sourcePageSizeOptions } from '../common/sourcePageUtils';

export function FlowiseLogsNativePage() {
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const query = useApiQuery({ keepPreviousData: true, queryKey: ['flowise-source-logs', keyword, page, pageSize], queryFn: ({ signal }) => managementApi.logs(buildSourceQuery(keyword, '', page, pageSize), signal) });
  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header"><div><h1>Logs</h1><p>Inspect Flowise server and audit log records</p></div></header>
      <Stack className="flowise-source-toolbar"><TextField fullWidth placeholder="Search Logs" size="small" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} /></Stack>
      {!query.isLoading && rows.length === 0 ? <Box className="flowise-source-empty"><img alt="LogsEmptySVG" src={logsEmptySvg} /><div>No Logs Yet</div></Box> : (
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table><TableHead><TableRow><TableCell>Event</TableCell><TableCell>Resource</TableCell><TableCell>Detail</TableCell><TableCell>Created</TableCell></TableRow></TableHead>
            <TableBody>{query.isLoading ? [0, 1].map((index) => <TableRow key={index}>{Array.from({ length: 4 }).map((_, cellIndex) => <TableCell key={cellIndex}><Skeleton /></TableCell>)}</TableRow>) : rows.map((item) => (
              <TableRow hover key={item.id}><TableCell><Chip label={item.eventType} size="small" /></TableCell><TableCell>{item.resourceType}<br /><small>{item.resourceId || '-'}</small></TableCell><TableCell className="flowise-source-ellipsis"><code>{item.detailJson}</code></TableCell><TableCell>{formatSourceDate(item.createdTime)}</TableCell></TableRow>
            ))}</TableBody></Table>
          <TablePagination component="div" count={total} page={Math.max(0, page - 1)} rowsPerPage={pageSize} rowsPerPageOptions={[...sourcePageSizeOptions]} onPageChange={(_, nextPage) => setPage(Math.min(totalPages, nextPage + 1))} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }} />
        </TableContainer>
      )}
    </section>
  );
}
