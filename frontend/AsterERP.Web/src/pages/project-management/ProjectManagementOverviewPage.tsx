import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";

import {
  getProjectManagementActivities,
  getProjectManagementOverview,
} from "../../api/project-management/projectManagement.api";
import { isHttpError } from "../../core/http/httpError";
import { projectManagementQueryKeys } from "../../core/query/projectManagementQueryKeys";
import { useProjectManagementWorkspaceScope } from "../../features/project-management/state/projectManagementWorkspaceScope";
import { ResponsivePage } from "../../shared/responsive/ResponsivePage";
import { Page403 } from "../../shared/status/Page403";
import { PageError } from "../../shared/status/PageError";
import { PageLoading } from "../../shared/status/PageLoading";

export function ProjectManagementOverviewPage() {
  const scope = useProjectManagementWorkspaceScope();
  const { projectId = "" } = useParams<{ projectId: string }>();
  const overviewQuery = useQuery({
    queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }),
    queryFn: ({ signal }) => getProjectManagementOverview({ projectId, pageIndex: 1, pageSize: 1 }, signal),
    enabled: scope.isAvailable && Boolean(projectId),
  });
  const overview = overviewQuery.data?.data?.items[0];
  const project = overview?.project;
  const activitiesQuery = useQuery({
    queryKey: projectManagementQueryKeys.activities(scope, projectId, 20),
    queryFn: ({ signal }) => getProjectManagementActivities(projectId, 20, signal),
    enabled: scope.isAvailable && Boolean(overview),
  });

  if (overviewQuery.isLoading) return <PageLoading />;
  if (overviewQuery.isError || activitiesQuery.isError) {
    const error = overviewQuery.error ?? activitiesQuery.error;
    if (isHttpError(error) && error.status === 403) return <Page403 />;
    return (
      <PageError
        description="项目概览加载失败"
        action={<button type="button" onClick={() => void overviewQuery.refetch()}>重试</button>}
      />
    );
  }
  if (!overview || !project) return <PageError description="项目不存在或当前账号无权访问" />;

  return (
    <ResponsivePage
      title={project.projectName}
      description={project.description ?? "项目概览"}
      eyebrow="ProjectManagement / Overview"
    >
      <div className="grid gap-3 md:grid-cols-5">
        {[
          ["进度", `${overview.taskProgressPercent}%`],
          ["任务", overview.taskCount],
          ["逾期", overview.overdueTaskCount],
          ["阻塞", overview.blockedTaskCount],
          ["状态", project.status],
        ].map(([label, value]) => (
          <div className="rounded-lg border border-gray-200 p-4" key={String(label)}>
            <div className="text-sm text-gray-500">{label}</div>
            <div className="mt-1 text-xl font-semibold">{value}</div>
          </div>
        ))}
      </div>
      <section className="mt-4 rounded-lg border border-gray-200 p-4">
        <h2 className="font-semibold">里程碑健康</h2>
        {overview.milestones.length === 0 ? (
          <p className="mt-2 text-sm text-gray-500">暂无里程碑</p>
        ) : (
          <ul className="mt-2 space-y-2 text-sm">
            {overview.milestones.map((milestone) => (
              <li className="flex items-center justify-between gap-3 border-b border-gray-100 pb-2" key={milestone.id}>
                <span>{milestone.name}</span>
                <span className="text-gray-500">
                  {milestone.healthStatus} · {milestone.progressPercent}%
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
      <section className="mt-4 rounded-lg border border-gray-200 p-4">
        <h2 className="font-semibold">最近活动</h2>
        {(activitiesQuery.data?.data ?? []).length === 0 ? (
          <p className="mt-2 text-sm text-gray-500">暂无活动</p>
        ) : (
          <ul className="mt-2 space-y-2 text-sm">
            {(activitiesQuery.data?.data ?? []).map((item) => (
              <li className="border-b border-gray-100 pb-2" key={item.id}>
                {item.summary ?? item.activityType} · {new Date(item.createdTime).toLocaleString()}
              </li>
            ))}
          </ul>
        )}
      </section>
    </ResponsivePage>
  );
}
