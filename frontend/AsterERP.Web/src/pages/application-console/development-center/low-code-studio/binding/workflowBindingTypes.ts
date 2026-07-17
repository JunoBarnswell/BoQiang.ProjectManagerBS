import type { DesignerDocument } from '../document/DesignerDocument';

export interface WorkflowPageContext {
  appCode?: string | null;
  businessType?: string | null;
  keyField?: string | null;
  menuCode?: string | null;
  pageCode: string;
  pageName: string;
  tenantId?: string | null;
}

export interface WorkflowDefinitionOption {
  id: string;
  key: string;
  name: string;
  version: number;
}

export type WorkflowSyncStatus = 'pendingPublish' | 'published' | 'synced' | 'syncFailed';

export interface WorkflowBindingDraft {
  appCode: string;
  businessKeyExpression: string;
  businessType: string;
  enabled: boolean;
  menuCode: string;
  processDefinitionId: string;
  processDefinitionKey: string;
  processDefinitionName: string;
  processDefinitionVersion: number | null;
  syncStatus: WorkflowSyncStatus;
  tenantId: string;
  titleTemplate: string;
}

export interface WorkflowBindingChangeResult {
  document: DesignerDocument;
  errors: string[];
}
