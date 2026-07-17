import BpmnViewer from 'bpmn-js/lib/Viewer';
import minimapModule from 'diagram-js-minimap';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import 'bpmn-js/dist/assets/diagram-js.css';
import 'bpmn-js/dist/assets/bpmn-font/css/bpmn.css';
import 'diagram-js-minimap/assets/diagram-js-minimap.css';

import type { WorkflowBindingUpsertRequest, WorkflowFormResourceDto, WorkflowParticipantDto } from '../../api/workflow/workflows.api';
import { getWorkflowDeploymentResource, getWorkflowFormResources, getWorkflowModel, getWorkflowParticipants, getWorkflowProcessDefinitions, publishWorkflowModel, saveWorkflowBinding, saveWorkflowModelXml, validateWorkflowModel } from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../shared/feedback/useMessage';
import { AppIcon } from '../../shared/icons/AppIcon';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { createWorkflowAuthoritativeModel, type WorkflowAuthoritativeModel } from './designer/WorkflowAuthoritativeModel';
import { WorkflowBpmnImportDialog } from './designer/WorkflowBpmnImportDialog';
import { WorkflowBusinessCanvas } from './designer/WorkflowBusinessCanvas';
import { readBusinessDesign, type WorkflowBusinessDesign } from './designer/workflowBusinessModel';
import { WorkflowModelBindingPanel } from './designer/WorkflowModelBindingPanel';
import { WorkflowModelConfigPanel } from './designer/WorkflowModelConfigPanel';
import { WorkflowModelVersionPanel } from './designer/WorkflowModelVersionPanel';
import { WorkflowParticipantSelector } from './designer/WorkflowParticipantSelector';
import { buildWorkspaceRoute } from './workflowWorkspaceRoutes';

import './workflow-bpmn.css';

type DesignerLayer = 'business' | 'approver' | 'binding' | 'config' | 'bpmn' | 'versions';

export function WorkflowDesignerPage() {
  const { modelId = '' } = useParams();
  const navigate = useNavigate();
  const { translate } = useI18n();
  const message = useMessage();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const viewerRef = useRef<BpmnViewer | null>(null);
  const [activeLayer, setActiveLayer] = useState<DesignerLayer>('business');
  const [participantKeyword, setParticipantKeyword] = useState('');
  const [businessDesign, setBusinessDesign] = useState<WorkflowBusinessDesign>(() => readBusinessDesign(undefined, translate));
  const [businessModelMigrationError, setBusinessModelMigrationError] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [importDialogXml, setImportDialogXml] = useState<string | null>(null);

  const selectedNode = businessDesign.nodes.find((node) => node.id === businessDesign.selectedNodeId);
  const participantType = selectedNode?.participantType ?? 'user';

  const modelQuery = useApiQuery({
    enabled: Boolean(modelId),
    queryFn: ({ signal }) => getWorkflowModel(modelId, signal),
    queryKey: ['workflows', 'model-detail', modelId]
  });
  const participantsQuery = useApiQuery({
    queryFn: ({ signal }) => getWorkflowParticipants({ keyword: participantKeyword, type: participantType }, signal),
    queryKey: ['workflows', 'participants', participantType, participantKeyword]
  });
  const formResourcesQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowFormResources({ appCode: workspace?.appCode, pageIndex: 1, pageSize: 100, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'designer-form-resources', workspace?.tenantId, workspace?.appCode]
  });
  const definitionsQuery = useApiQuery({
    enabled: Boolean(modelQuery.data?.data.modelKey),
    queryFn: ({ signal }) => getWorkflowProcessDefinitions(modelQuery.data?.data.modelKey, signal),
    queryKey: ['workflows', 'designer-definitions', modelQuery.data?.data.modelKey]
  });

  const modelName = modelQuery.data?.data.name ?? translate('page.workflowDesigner.defaultTitle');
  const modelKey = modelQuery.data?.data.modelKey ?? modelId;
  const authoritativeModel = useMemo<WorkflowAuthoritativeModel>(
    () => createWorkflowAuthoritativeModel(businessDesign, modelKey, modelName, translate),
    [businessDesign, modelKey, modelName, translate]
  );

  const saveMutation = useApiMutation({
    mutationFn: ({ extension, xml }: { extension: string; xml: string }) => saveWorkflowModelXml(modelId, { bpmnXml: xml, extensionJson: extension })
  });
  const validateMutation = useApiMutation({ mutationFn: validateWorkflowModel });
  const publishMutation = useApiMutation({ mutationFn: publishWorkflowModel });
  const saveBindingMutation = useApiMutation({ mutationFn: saveWorkflowBinding });

  useEffect(() => {
    const model = modelQuery.data?.data;
    if (!model) {
      return;
    }

    try {
      setBusinessDesign(readBusinessDesign(model.extensionJson, translate));
      setBusinessModelMigrationError(null);
    } catch (error) {
      setBusinessModelMigrationError(getErrorMessage(error, translate('page.workflowDesigner.error.saveFailed')));
    }
  }, [modelQuery.data?.data, translate]);

  useEffect(() => {
    if (activeLayer !== 'bpmn' || !canvasRef.current || viewerRef.current) {
      return;
    }

    const viewer = new BpmnViewer({ additionalModules: [minimapModule], container: canvasRef.current });
    viewerRef.current = viewer;

    return () => {
      viewer.destroy();
      viewerRef.current = null;
    };
  }, [activeLayer]);

  useEffect(() => {
    const viewer = viewerRef.current;
    if (activeLayer !== 'bpmn' || !viewer || !authoritativeModel.bpmnXml) {
      return;
    }

    void viewer.importXML(authoritativeModel.bpmnXml).then(() => {
      const canvas = viewer.get<{ zoom(value: string): void }>('canvas');
      canvas.zoom('fit-viewport');
    }).catch((error) => {
      message.error(getErrorMessage(error, translate('page.workflowDesigner.error.canvasLoadFailed')));
    });
  }, [activeLayer, authoritativeModel.bpmnXml, message, translate]);

  const participants = participantsQuery.data?.data ?? [];
  const formResources = formResourcesQuery.data?.data.items ?? [];
  const definitions = definitionsQuery.data?.data ?? [];

  const saveDesigner = async (): Promise<boolean> => {
    if (businessModelMigrationError) {
      message.error(businessModelMigrationError);
      return false;
    }

    try {
      await saveMutation.mutateAsync({ extension: authoritativeModel.extensionJson, xml: authoritativeModel.bpmnXml });
      message.success(translate('page.workflowDesigner.success.save'));
      return true;
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowDesigner.error.saveFailed')));
      return false;
    }
  };

  const selectFormContext = (resourceCode: string) => {
    const resource = formResources.find((item) => item.resourceCode === resourceCode);
    setBusinessDesign((current) => ({
      ...current,
      formContext: resource ? toBusinessFormContext(resource) : null
    }));
  };

  const saveBinding = async (request: WorkflowBindingUpsertRequest) => {
    try {
      if (!await saveDesigner()) {
        throw new Error(translate('page.workflowDesigner.error.saveFailed'));
      }
      await saveBindingMutation.mutateAsync(request);
      message.success(translate('workflow.modelDesigner.binding.saved'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('workflow.modelDesigner.binding.saveFailed')));
      throw error;
    }
  };

  const validateDesigner = async () => {
    try {
      if (!await saveDesigner()) {
        return false;
      }
      const response = await validateMutation.mutateAsync(modelId);
      setValidationErrors(response.data.errors);
      if (response.data.isValid) {
        message.success(translate('page.workflowDesigner.success.validatePassed'));
        return true;
      }

      message.error(response.data.errors[0] ?? translate('page.workflowDesigner.error.validateFailed'));
      return false;
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowDesigner.error.validateRequestFailed')));
      return false;
    }
  };

  const publishDesigner = async () => {
    const isValid = await validateDesigner();
    if (!isValid) {
      return;
    }

    try {
      const response = await publishMutation.mutateAsync(modelId);
      message.success(formatMessage(translate('page.workflowDesigner.success.published'), { version: response.data.version }));
      void definitionsQuery.refetch();
      void modelQuery.refetch();
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowDesigner.error.publishFailed')));
    }
  };

  const openVersionRollback = async (definition: (typeof definitions)[number]) => {
    if (!definition.deploymentId) {
      message.error(translate('page.workflowDesigner.bpmnImport.rollbackUnavailable'));
      return;
    }

    try {
      const response = await getWorkflowDeploymentResource(definition.deploymentId, `${modelKey}.bpmn`);
      setImportDialogXml(response.data.content);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowDesigner.bpmnImport.rollbackFailed')));
    }
  };

  const downloadBpmn = () => {
    const blob = new Blob([authoritativeModel.bpmnXml], { type: 'application/xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${modelKey || 'workflow'}.bpmn`;
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <CrudPage
      title={modelName}
      actions={(
        <div className="flex flex-wrap items-center gap-2">
          <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50" type="button" onClick={() => navigate(buildWorkspaceRoute('/workflows/models', workspace))}>
            <AppIcon name="arrow-left" />
          </button>
          <div className="workflow-layer-tabs">
            <button className={activeLayer === 'business' ? 'active' : ''} type="button" onClick={() => setActiveLayer('business')}>{translate('page.workflowDesigner.tab.business')}</button>
            <button className={activeLayer === 'approver' ? 'active' : ''} type="button" onClick={() => setActiveLayer('approver')}>{translate('workflow.modelDesigner.tab.approver')}</button>
            <button className={activeLayer === 'binding' ? 'active' : ''} type="button" onClick={() => setActiveLayer('binding')}>{translate('workflow.modelDesigner.tab.binding')}</button>
            <button className={activeLayer === 'config' ? 'active' : ''} type="button" onClick={() => setActiveLayer('config')}>{translate('workflow.modelDesigner.tab.config')}</button>
            <button className={activeLayer === 'bpmn' ? 'active' : ''} type="button" onClick={() => setActiveLayer('bpmn')}>{translate('page.workflowDesigner.tab.bpmn')}</button>
            <button className={activeLayer === 'versions' ? 'active' : ''} type="button" onClick={() => setActiveLayer('versions')}>{translate('workflow.modelDesigner.tab.versions')}</button>
          </div>
          <PermissionButton code="workflow:model:edit" className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50" type="button" onClick={() => void saveDesigner()}>
            <AppIcon name="floppy-disk" /> {translate('page.workflowDesigner.action.save')}
          </PermissionButton>
          <PermissionButton code="workflow:model:edit" className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50" type="button" onClick={() => void validateDesigner()}>
            <AppIcon name="check-circle" /> {translate('page.workflowDesigner.action.validate')}
          </PermissionButton>
          <PermissionButton code="workflow:model:publish" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700" type="button" onClick={() => void publishDesigner()}>
            <AppIcon name="upload-simple" /> {translate('page.workflowDesigner.action.publish')}
          </PermissionButton>
        </div>
      )}
    >
      <div className="h-full min-h-0">
        {businessModelMigrationError ? (
          <div className="mb-3 rounded border border-red-300 bg-red-50 p-3 text-sm text-red-800" role="alert">
            {businessModelMigrationError}
          </div>
        ) : null}
        {activeLayer === 'business' ? (
          <WorkflowBusinessCanvas
            design={businessDesign}
            formFields={businessDesign.formContext?.fields ?? []}
            participantKeyword={participantKeyword}
            participants={participants}
            onChange={setBusinessDesign}
            onParticipantKeywordChange={setParticipantKeyword}
          />
        ) : null}

        {activeLayer === 'approver' ? (
          <ApproverWorkspace
            design={businessDesign}
            formFields={businessDesign.formContext?.fields ?? []}
            participantKeyword={participantKeyword}
            participants={participants}
            onChange={setBusinessDesign}
            onParticipantKeywordChange={setParticipantKeyword}
          />
        ) : null}

        {activeLayer === 'binding' ? (
          <WorkflowModelBindingPanel
            appCode={workspace?.appCode}
            definitions={definitions}
            formContext={businessDesign.formContext}
            modelId={modelId}
            modelKey={modelKey}
            modelName={modelName}
            saving={saveBindingMutation.isPending}
            tenantId={workspace?.tenantId}
            onSave={saveBinding}
          />
        ) : null}

        {activeLayer === 'config' ? (
          <WorkflowModelConfigPanel
            businessDesign={businessDesign}
            formResources={formResources}
            onSelectFormContext={selectFormContext}
          />
        ) : null}

        {activeLayer === 'bpmn' ? (
          <div className="grid grid-cols-[minmax(0,1fr)_280px] gap-3 h-full min-h-0">
            <section className="bg-white border border-gray-200 rounded-lg shadow-sm overflow-hidden min-h-0">
              <div ref={canvasRef} className="h-full w-full workflow-bpmn-canvas" />
            </section>
            <aside className="bg-white border border-gray-200 rounded-lg shadow-sm p-4 grid content-start gap-3 text-sm">
              <div className="font-semibold">{translate('page.workflowDesigner.bpmnReadOnly.title')}</div>
              <p className="text-gray-600">{translate('page.workflowDesigner.bpmnReadOnly.description')}</p>
              <PermissionButton code="workflow:model:edit" className="rounded border border-gray-300 px-3 py-1.5 text-left" type="button" onClick={() => setImportDialogXml('')}>
                {translate('page.workflowDesigner.bpmnImport.open')}
              </PermissionButton>
              <button className="rounded border border-gray-300 px-3 py-1.5 text-left" type="button" onClick={downloadBpmn}>
                {translate('page.workflowDesigner.bpmnReadOnly.download')}
              </button>
            </aside>
          </div>
        ) : null}

        {activeLayer === 'versions' ? (
          <WorkflowModelVersionPanel
            definitions={definitions}
            model={modelQuery.data?.data}
            publishing={publishMutation.isPending}
            validationErrors={validationErrors}
            onPublish={() => void publishDesigner()}
            onRollback={(definition) => void openVersionRollback(definition)}
          />
        ) : null}
      </div>
      <textarea className="sr-only" readOnly value={authoritativeModel.extensionJson} />
      {importDialogXml !== null ? (
        <WorkflowBpmnImportDialog
          currentDesign={businessDesign}
          initialXml={importDialogXml}
          onCancel={() => setImportDialogXml(null)}
          onConfirm={(design) => {
            setBusinessDesign(design);
            setImportDialogXml(null);
            message.success(translate('page.workflowDesigner.bpmnImport.replaced'));
          }}
        />
      ) : null}
    </CrudPage>
  );
}

function toBusinessFormContext(resource: WorkflowFormResourceDto): NonNullable<WorkflowBusinessDesign['formContext']> {
  return {
    businessType: resource.businessType,
    fields: resource.fields,
    keyField: resource.keyField,
    menuCode: resource.menuCode,
    modelCode: resource.modelCode,
    pageCode: resource.pageCode,
    resourceCode: resource.resourceCode,
    resourceName: resource.resourceName,
    routePath: resource.routePath ?? null
  };
}

function ApproverWorkspace({
  design,
  formFields,
  onChange,
  onParticipantKeywordChange,
  participantKeyword,
  participants
}: {
  design: WorkflowBusinessDesign;
  formFields: NonNullable<WorkflowBusinessDesign['formContext']>['fields'];
  onChange: (design: WorkflowBusinessDesign) => void;
  onParticipantKeywordChange: (keyword: string) => void;
  participantKeyword: string;
  participants: WorkflowParticipantDto[];
}) {
  const { translate } = useI18n();
  const approvalNodes = design.nodes.filter((node) => node.type === 'approval');
  const selectedNode = approvalNodes.find((node) => node.id === design.selectedNodeId) ?? approvalNodes[0];

  const updateNode = (updater: (node: WorkflowBusinessDesign['nodes'][number]) => WorkflowBusinessDesign['nodes'][number]) => {
    if (!selectedNode) {
      return;
    }

    onChange({
      ...design,
      selectedNodeId: selectedNode.id,
      nodes: design.nodes.map((node) => node.id === selectedNode.id ? updater(node) : node)
    });
  };

  return (
    <div className="workflow-approver-layout">
      <aside className="workflow-business-panel">
        <div className="workflow-panel-title">{translate('workflow.modelDesigner.approver.nodes')}</div>
        <div className="workflow-approver-node-list">
          {approvalNodes.map((node) => (
            <button
              key={node.id}
              className={selectedNode?.id === node.id ? 'active' : ''}
              type="button"
              onClick={() => onChange({ ...design, selectedNodeId: node.id })}
            >
              <strong>{node.label}</strong>
              <span>{node.participantName || node.groupKey || node.participantExpression || '-'}</span>
            </button>
          ))}
        </div>
      </aside>
      <section className="workflow-business-panel">
        {selectedNode ? (
          <WorkflowParticipantSelector
            formFields={formFields}
            node={selectedNode}
            participantKeyword={participantKeyword}
            participants={participants}
            onChange={updateNode}
            onParticipantKeywordChange={onParticipantKeywordChange}
          />
        ) : (
          <div className="workflow-config-empty">{translate('workflow.modelDesigner.approver.empty')}</div>
        )}
      </section>
    </div>
  );
}
