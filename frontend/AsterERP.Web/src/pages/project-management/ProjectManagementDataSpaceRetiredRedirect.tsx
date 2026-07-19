import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';

import { useMessage } from '../../shared/feedback/useMessage';

export function ProjectManagementDataSpaceRetiredRedirect() {
  const message = useMessage();
  const navigate = useNavigate();
  const completed = useRef(false);

  useEffect(() => {
    if (completed.current) return;
    completed.current = true;
    message.info('数据空间功能已移除，已返回项目中心。');
    navigate('/platform/project-management/projects', { replace: true });
  }, [message, navigate]);

  return null;
}
