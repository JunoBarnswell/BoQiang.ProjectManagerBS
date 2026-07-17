import { Box, Chip, Paper, Skeleton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField } from '@mui/material';
import { useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { managementApi } from '../../../api/management.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import usersEmptySvg from '../../assets/images/users_empty.svg';
import { buildSourceQuery, formatSourceDate, getSourcePageTotalPages, sourcePageSizeOptions } from '../common/sourcePageUtils';

export function FlowiseLoginActivityNativePage() {
  const { translate } = useI18n();
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const query = useApiQuery({ keepPreviousData: true, queryKey: ['flowise-source-login-activity', keyword, page, pageSize], queryFn: ({ signal }) => managementApi.loginActivity(buildSourceQuery(keyword, '', page, pageSize), signal) });
  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header"><div><h1>{translate(flowiseI18nKeys.pages.loginActivity)}</h1><p>{translate('flowise.native.loginActivity.description')}</p></div></header>
      <Stack className="flowise-source-toolbar"><TextField fullWidth placeholder={translate(flowiseI18nKeys.source.loginActivity.search)} size="small" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} /></Stack>
      {!query.isLoading && rows.length === 0 ? <Box className="flowise-source-empty"><img alt="users_empty" src={usersEmptySvg} /><div>{translate(flowiseI18nKeys.source.loginActivity.empty)}</div></Box> : (
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table><TableHead><TableRow><TableCell>{translate(flowiseI18nKeys.source.loginActivity.user)}</TableCell><TableCell>{translate(flowiseI18nKeys.fields.status)}</TableCell><TableCell>{translate(flowiseI18nKeys.source.loginActivity.ipAddress)}</TableCell><TableCell>{translate(flowiseI18nKeys.source.loginActivity.userAgent)}</TableCell><TableCell>{translate(flowiseI18nKeys.fields.created)}</TableCell></TableRow></TableHead>
            <TableBody>{query.isLoading ? [0, 1].map((index) => <TableRow key={index}>{Array.from({ length: 5 }).map((_, cellIndex) => <TableCell key={cellIndex}><Skeleton /></TableCell>)}</TableRow>) : rows.map((item) => (
              <TableRow hover key={item.id}><TableCell>{item.userName}</TableCell><TableCell><Chip label={item.status} size="small" /></TableCell><TableCell>{item.ipAddress || '-'}</TableCell><TableCell className="flowise-source-ellipsis">{item.userAgent || '-'}</TableCell><TableCell>{formatSourceDate(item.createdTime)}</TableCell></TableRow>
            ))}</TableBody></Table>
          <TablePagination component="div" count={total} page={Math.max(0, page - 1)} rowsPerPage={pageSize} rowsPerPageOptions={[...sourcePageSizeOptions]} onPageChange={(_, nextPage) => setPage(Math.min(totalPages, nextPage + 1))} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }} />
        </TableContainer>
      )}
    </section>
  );
}
