import { describe, expect, it } from 'vitest';

import { isHighValueProjectManagementNotification } from './useProjectManagementBrowserNotifications';

describe('isHighValueProjectManagementNotification', () => {
  it('uses the persisted project notification type contract for system delivery', () => {
    expect(isHighValueProjectManagementNotification('task.reminder')).toBe(true);
    expect(isHighValueProjectManagementNotification('task.assigned')).toBe(true);
    expect(isHighValueProjectManagementNotification('task.status.changed')).toBe(true);
    expect(isHighValueProjectManagementNotification('task.due-date.changed')).toBe(true);
    expect(isHighValueProjectManagementNotification('milestone.risk.detected')).toBe(true);
    expect(isHighValueProjectManagementNotification('operation.failed')).toBe(true);
    expect(isHighValueProjectManagementNotification('task.reminder.sent')).toBe(false);
    expect(isHighValueProjectManagementNotification('project.excel-import')).toBe(false);
  });
});
