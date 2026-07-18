import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { getProjectManagementImConversationTarget } from '../../../api/project-management/projectManagement.api';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface ProjectManagementImConversationTargetLinkProps {
  conversationId: string;
}

export function ProjectManagementImConversationTargetLink({ conversationId }: ProjectManagementImConversationTargetLinkProps) {
  const message = useMessage();
  const navigate = useNavigate();
  const [opening, setOpening] = useState(false);

  const openTarget = async () => {
    if (opening) return;
    setOpening(true);
    try {
      const target = (await getProjectManagementImConversationTarget(conversationId)).data;
      if (!target?.isAvailable || !target.targetRoute) {
        message.error('关联项目或任务已不可访问');
        return;
      }
      navigate(target.targetRoute);
    } catch (error) {
      message.error(getErrorMessage(error, '关联目标打开失败'));
    } finally {
      setOpening(false);
    }
  };

  return <button className="shrink-0 text-xs text-blue-600 disabled:text-gray-400" disabled={opening} onClick={() => void openTarget()} type="button">{opening ? '正在打开…' : '查看关联任务'}</button>;
}
