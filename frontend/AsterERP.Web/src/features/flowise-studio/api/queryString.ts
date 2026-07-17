export function buildFlowiseQuery(query: object = {}): string {
  const params = new URLSearchParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    params.set(key, String(value));
  });
  const queryString = params.toString();
  return queryString ? `?${queryString}` : '';
}
