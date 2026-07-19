import type { ProjectManagementDashboardWorkload } from '../../../features/project-management/dashboard/projectManagementDashboard.api';

interface ProjectWorkloadTableProps {
  displayNames: Record<string, string>;
  isError: boolean;
  isLoading: boolean;
  onRetry: () => void;
  rows: ProjectManagementDashboardWorkload[];
}

export function ProjectWorkloadTable({ displayNames, isError, isLoading, onRetry, rows }: ProjectWorkloadTableProps) {
  const workloadRows = Array.isArray(rows) ? rows : [];
  if (isLoading) return <p className="pm-muted">工作量加载中…</p>;
  if (isError) return <div className="pm-inline-error"><p>人员工作量加载失败。</p><button type="button" onClick={onRetry}>重试</button></div>;
  if (workloadRows.length === 0) return <p className="pm-muted">暂无已分配任务或工时记录。</p>;
  return <div className="overflow-x-auto"><table className="min-w-full text-sm"><thead><tr><th className="px-2 py-2 text-left">人员</th><th className="px-2 py-2 text-right">待办</th><th className="px-2 py-2 text-right">进行中</th><th className="px-2 py-2 text-right">已完成</th><th className="px-2 py-2 text-right">逾期</th><th className="px-2 py-2 text-right">预计</th><th className="px-2 py-2 text-right">已登记</th></tr></thead><tbody>{workloadRows.map((person) => <tr className="border-t border-gray-100" key={person.userId}><td className="px-2 py-2 font-medium">{displayNames[person.userId] ?? person.userId}</td><td className="px-2 py-2 text-right">{person.todoTaskCount}</td><td className="px-2 py-2 text-right">{person.inProgressTaskCount}</td><td className="px-2 py-2 text-right">{person.completedTaskCount}</td><td className="px-2 py-2 text-right">{person.overdueTaskCount}</td><td className="px-2 py-2 text-right">{formatMinutes(person.estimatedMinutes)}</td><td className="px-2 py-2 text-right">{formatMinutes(person.loggedMinutes)}</td></tr>)}</tbody></table></div>;
}

function formatMinutes(value: number) {
  if (value < 60) return `${value} 分钟`;
  const hours = Math.floor(value / 60);
  const minutes = value % 60;
  return minutes === 0 ? `${hours} 小时` : `${hours} 小时 ${minutes} 分钟`;
}
