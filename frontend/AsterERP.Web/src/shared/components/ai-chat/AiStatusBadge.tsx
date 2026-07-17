const statusClassMap: Record<string, string> = {
  Active: 'ai-status-badge ai-status-badge--success',
  Archived: 'ai-status-badge',
  Cancelled: 'ai-status-badge',
  Completed: 'ai-status-badge ai-status-badge--success',
  Disabled: 'ai-status-badge',
  Enabled: 'ai-status-badge ai-status-badge--success',
  Failed: 'ai-status-badge ai-status-badge--danger',
  Running: 'ai-status-badge ai-status-badge--running',
  Succeeded: 'ai-status-badge ai-status-badge--success'
};

export function AiStatusBadge({ status }: { status?: string | null }) {
  const value = status || 'Unknown';
  return <span className={statusClassMap[value] ?? 'ai-status-badge'}>{value}</span>;
}
