import { useContext } from 'react';

import { FeedbackContext } from './FeedbackProvider';

export function useConfirm() {
  const context = useContext(FeedbackContext);
  if (!context) {
    throw new Error('useConfirm must be used within FeedbackProvider');
  }

  return context.showConfirm;
}
