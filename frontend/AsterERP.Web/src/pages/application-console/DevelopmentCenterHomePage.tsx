import { Navigate, useParams } from 'react-router-dom';

export function DevelopmentCenterHomePage() {
  const { appCode, tenantId } = useParams();
  const target = tenantId && appCode
    ? `/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/development-center/pages`
    : '/';

  return <Navigate replace to={target} />;
}
