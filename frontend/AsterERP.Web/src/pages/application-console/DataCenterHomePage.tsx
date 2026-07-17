import { Navigate, useParams } from 'react-router-dom';

export function DataCenterHomePage() {
  const { appCode, tenantId } = useParams();
  const target = tenantId && appCode
    ? `/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/data-center/data-sources`
    : '/';

  return <Navigate replace to={target} />;
}
