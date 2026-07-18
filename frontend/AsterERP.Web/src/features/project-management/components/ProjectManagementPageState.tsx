import type { ProjectManagementPageState } from '../../../api/project-management/projectManagement.types';
import { PageStateShell } from '../../../shared/status/PageStateShell';

const stateContent: Record<ProjectManagementPageState, { description: string; title: string }> = {
  loading: { title: '正在加载项目管理', description: '正在读取当前工作区的项目上下文。' },
  empty: { title: '还没有项目', description: '当前工作区暂无项目，创建项目后即可开始计划和协作。' },
  error: { title: '项目管理加载失败', description: '项目管理数据暂时不可用，请稍后重试或联系管理员。' },
  forbidden: { title: '无权访问项目管理', description: '当前账号没有项目管理查看权限，请联系项目管理员授权。' }
};

export function ProjectManagementPageStateView({ state }: { state: ProjectManagementPageState }) {
  const content = stateContent[state];
  return <PageStateShell title={content.title} description={content.description} />;
}
