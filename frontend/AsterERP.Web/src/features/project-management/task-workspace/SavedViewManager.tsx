import { useState } from 'react';

import type { ProjectManagementSavedView, ProjectManagementSavedViewUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';

interface SavedViewManagerProps {
  onCopy: (view: ProjectManagementSavedView, name: string) => void;
  onDelete: (view: ProjectManagementSavedView) => void;
  onUpdate: (view: ProjectManagementSavedView, request: ProjectManagementSavedViewUpsertRequest) => void;
  pending: boolean;
  views: ProjectManagementSavedView[];
}

export function SavedViewManager({ onCopy, onDelete, onUpdate, pending, views }: SavedViewManagerProps) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState('');
  if (views.length === 0) return null;
  return <div className="flex flex-wrap items-center gap-2 text-sm">{views.map((view) => <div className="flex items-center gap-1 rounded border border-gray-200 p-1" key={view.id}>{editingId === view.id ? <><input aria-label="重命名保存视图" className="w-28" onChange={(event) => setName(event.target.value)} value={name} /><PermissionButton code="project-management:task:edit" disabled={!name.trim() || pending} onClick={() => { onUpdate(view, { isDefault: view.isDefault, isShared: view.isShared, queryJson: view.queryJson, versionNo: view.versionNo, viewKey: view.viewKey, viewName: name.trim() }); setEditingId(null); }}>保存</PermissionButton></> : <><span>{view.viewName}{view.isDefault ? ' · 默认' : ''}</span><PermissionButton code="project-management:task:edit" disabled={pending} onClick={() => { setEditingId(view.id); setName(view.viewName); }}>重命名</PermissionButton><PermissionButton code="project-management:task:edit" disabled={pending} onClick={() => onCopy(view, `${view.viewName} 副本`)}>复制</PermissionButton><PermissionButton code="project-management:task:edit" disabled={pending || view.isDefault} onClick={() => onUpdate(view, { isDefault: true, isShared: view.isShared, queryJson: view.queryJson, versionNo: view.versionNo, viewKey: view.viewKey, viewName: view.viewName })}>设默认</PermissionButton><PermissionButton code="project-management:task:edit" disabled={pending} onClick={() => onDelete(view)}>删除</PermissionButton></>}</div>)}</div>;
}
