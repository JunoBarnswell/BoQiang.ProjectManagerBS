import { useSearchParams } from 'react-router-dom';

import { MicroflowConsolePage } from './microflows/MicroflowConsolePage';

export function MicroflowsPage() {
  const [searchParams] = useSearchParams();

  return <MicroflowConsolePage contextDataSourceId={searchParams.get('dataSourceId') ?? undefined} />;
}
