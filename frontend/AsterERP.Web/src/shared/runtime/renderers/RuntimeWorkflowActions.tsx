import { ExternalLink, Workflow } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { WorkflowBusinessActions } from '../../../pages/workflows/WorkflowBusinessActions';

interface RuntimeWorkflowActionsProps {
  businessKey?: string | null;
  businessKeyExpression?: string | null;
  businessType?: string | null;
  formResourceCode?: string | null;
  menuCode?: string | null;
  modelCode?: string | null;
  pageCode?: string | null;
  title?: string | null;
  titleTemplate?: string | null;
  variables?: Record<string, unknown> | null;
}

export function RuntimeWorkflowActions({
  businessKey,
  businessKeyExpression,
  businessType,
  formResourceCode,
  menuCode,
  modelCode,
  pageCode,
  title,
  titleTemplate,
  variables
}: RuntimeWorkflowActionsProps) {
  const navigate = useNavigate();
  const normalizedBusinessKey = String(businessKey ?? '').trim();
  const normalizedBusinessType = String(businessType ?? modelCode ?? pageCode ?? '').trim();
  const normalizedMenuCode = String(menuCode ?? `${pageCode ?? ''}-menu`).trim();

  if (normalizedBusinessKey && normalizedBusinessType && normalizedMenuCode) {
    return (
      <WorkflowBusinessActions
        businessKey={normalizedBusinessKey}
        businessType={normalizedBusinessType}
        menuCode={normalizedMenuCode}
        title={String(title ?? titleTemplate ?? normalizedBusinessKey)}
        variables={variables ?? undefined}
      />
    );
  }

  const bindingUrl = buildBindingUrl({
    businessType: normalizedBusinessType,
    formResourceCode,
    menuCode: normalizedMenuCode,
    modelCode,
    pageCode
  });

  return (
    <div className="flex flex-col gap-2 rounded-md border border-amber-200 bg-amber-50/80 p-3 md:flex-row md:items-center md:justify-between">
      <div className="min-w-0">
        <div className="flex items-center gap-2 text-sm font-semibold text-amber-900">
          <Workflow className="h-4 w-4" />
          {title || '工作流触发'}
        </div>
        <div className="mt-1 text-xs leading-5 text-amber-700">
          当前是页面级流程入口，行上下文请通过微流页面变量或当前行变量传入；本入口用于发布后跳转配置审批绑定。
          {businessKeyExpression ? ` 业务 Key 表达式：${businessKeyExpression}` : ''}
        </div>
      </div>
      <button className="secondary-button h-8 shrink-0 text-xs" type="button" onClick={() => navigate(bindingUrl)}>
        <ExternalLink className="h-3.5 w-3.5" />{translateCurrentLiteral("配置流程")}</button>
    </div>
  );
}

function buildBindingUrl({
  businessType,
  formResourceCode,
  menuCode,
  modelCode,
  pageCode
}: {
  businessType?: string | null;
  formResourceCode?: string | null;
  menuCode?: string | null;
  modelCode?: string | null;
  pageCode?: string | null;
}) {
  const resourceCode = formResourceCode || [menuCode, pageCode, modelCode || businessType].filter(Boolean).join('::');
  const query = new URLSearchParams();
  if (resourceCode) {
    query.set('formResourceCode', resourceCode);
  }
  if (pageCode) {
    query.set('pageCode', pageCode);
  }
  if (menuCode) {
    query.set('menuCode', menuCode);
  }
  return `/workflows/bindings${query.toString() ? `?${query.toString()}` : ''}`;
}
