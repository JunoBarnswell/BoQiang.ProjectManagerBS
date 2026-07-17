import { useContext } from 'react';

import { FeedbackContext } from './FeedbackProvider';

export function useMessage() {
  const context = useContext(FeedbackContext);
  if (!context) {
    throw new Error('useMessage must be used within FeedbackProvider');
  }

  return {
    success: (content: string) => context.showMessage(content, 'success'),
    error: (content: string) => context.showMessage(content, 'error'),
    info: (content: string) => context.showMessage(content, 'info'),
  };
}
