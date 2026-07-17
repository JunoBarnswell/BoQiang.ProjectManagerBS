import type { ApplicationDataCenterObjectDetail } from '../../../api/application-data-center/applicationDataCenter.types';

export interface DataSourceWorkspaceContext {
  dataSource: ApplicationDataCenterObjectDetail | null;
  dataSourceId: string;
  loading: boolean;
}
