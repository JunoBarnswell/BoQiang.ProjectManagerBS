import type { ReactNode } from 'react';

interface TaskWorkspaceContextPanelsProps {
  labels: ReactNode;
  projectConversation: ReactNode;
  savedViews: ReactNode;
  taskConversation?: ReactNode;
}

export function TaskWorkspaceContextPanels({ labels, projectConversation, savedViews, taskConversation }: TaskWorkspaceContextPanelsProps) {
  return <section aria-label="任务工作区辅助功能" className="responsive-toolbar">
    <details className="responsive-toolbar__more"><summary className="ghost-button">项目协作</summary><div className="responsive-toolbar__more-panel">{projectConversation}</div></details>
    <details className="responsive-toolbar__more"><summary className="ghost-button">保存视图</summary><div className="responsive-toolbar__more-panel">{savedViews}</div></details>
    <details className="responsive-toolbar__more"><summary className="ghost-button">标签管理</summary><div className="responsive-toolbar__more-panel">{labels}</div></details>
    {taskConversation ? <details className="responsive-toolbar__more"><summary className="ghost-button">当前任务协作</summary><div className="responsive-toolbar__more-panel">{taskConversation}</div></details> : null}
  </section>;
}
