import { useMemo } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { AppIcon } from '../../../shared/icons/AppIcon';

import { KnowledgeGraphAnalysisPanel } from './components/KnowledgeGraphAnalysisPanel';
import { KnowledgeGraphCanvas } from './components/KnowledgeGraphCanvas';
import { KnowledgeGraphDetailsPanel } from './components/KnowledgeGraphDetailsPanel';
import { KnowledgeGraphEntityForms } from './components/KnowledgeGraphEntityForms';
import { KnowledgeGraphFilters } from './components/KnowledgeGraphFilters';
import { KnowledgeGraphImportExportModal } from './components/KnowledgeGraphImportExportModal';
import { KnowledgeGraphOverview } from './components/KnowledgeGraphOverview';
import { KnowledgeGraphTaskPanel } from './components/KnowledgeGraphTaskPanel';
import { useKnowledgeGraphController } from './hooks/useKnowledgeGraphController';
import type { KnowledgeGraphPanelKey, KnowledgeGraphSelection } from './types';

import './knowledgeGraph.css';

export function KnowledgeGraphWorkbench() {
  const { translate } = useI18n();
  const graph = useKnowledgeGraphController();
  const panelTabs = useMemo<Array<{ key: KnowledgeGraphPanelKey; label: string; icon: string }>>(
    () => [
      { key: 'details', label: translate('kg.workbench.tabs.details'), icon: 'list' },
      { key: 'analysis', label: translate('kg.workbench.tabs.analysis'), icon: 'activity' },
      { key: 'tasks', label: translate('kg.workbench.tabs.tasks'), icon: 'clipboard' }
    ],
    [translate]
  );

  const selectGraphItem = (selection: KnowledgeGraphSelection | null) => {
    graph.setSelection(selection);
    if (selection) {
      graph.setActivePanel('details');
    }
  };

  return (
    <div className="kg-workbench">
      <main className="kg-main">
        <KnowledgeGraphOverview
          loading={graph.queries.overview.isFetching}
          overview={graph.overviewQuery.data}
          rebuilding={graph.mutations.rebuilding}
          onRebuild={() => void graph.commands.rebuild()}
          onRefresh={() => void graph.commands.refresh()}
        />

        <div className="kg-topbar">
          <KnowledgeGraphFilters
            filters={graph.filters}
            nodeTypeOptions={graph.options.nodeTypeOptions}
            relationTypeOptions={graph.options.relationTypeOptions}
            sourceOptions={graph.options.sourceOptions}
            statusOptions={graph.options.statusOptions}
            onChange={graph.setFilters}
            onReset={graph.resetFilters}
          />
          <div className="kg-exchange-actions">
            <PermissionButton className="ghost-button" code={['ai:knowledge:graph:import', 'ai:knowledge:graph:export']} fallback="disable" type="button" onClick={() => graph.setActiveModal('exchange')}>
              {translate('kg.workbench.actions.exchange')}
            </PermissionButton>
          </div>
        </div>

        <KnowledgeGraphCanvas
          error={graph.queries.graph.error}
          layoutOverrides={graph.canvas.layoutOverrides}
          loading={graph.queries.graph.isFetching && !graph.queries.graph.data}
          selection={graph.selection}
          snapshot={graph.snapshot}
          onConnectNodes={graph.canvas.connectNodes}
          onCreateEdge={() => graph.commands.openCreateEdge()}
          onCreateNode={graph.commands.openCreateNode}
          onDeleteEdge={graph.commands.deleteEdge}
          onDeleteNode={graph.commands.deleteNode}
          onEditEdge={graph.commands.openEditEdge}
          onEditNode={graph.commands.openEditNode}
          onNodePositionCommit={(nodeId, position) => void graph.canvas.commitNodePosition(nodeId, position)}
          onRetry={() => void graph.commands.refresh()}
          onSelectionChange={selectGraphItem}
        />
      </main>

      <aside className="kg-sidebar">
        <nav className="kg-panel-tabs" aria-label={translate('kg.workbench.sidebarAria')}>
          {panelTabs.map((tab) => (
            <button
              key={tab.key}
              className={graph.activePanel === tab.key ? 'active' : ''}
              type="button"
              onClick={() => graph.setActivePanel(tab.key)}
            >
              <AppIcon name={tab.icon} />
              {tab.label}
            </button>
          ))}
        </nav>

        {graph.activePanel === 'details' ? (
          <KnowledgeGraphDetailsPanel
            selectedEdge={graph.selectedEdge}
            selectedNode={graph.selectedNode}
            onDeleteEdge={graph.commands.deleteEdge}
            onDeleteNode={graph.commands.deleteNode}
            onEditEdge={graph.commands.openEditEdge}
            onEditNode={graph.commands.openEditNode}
          />
        ) : null}

        {graph.activePanel === 'analysis' ? (
          <KnowledgeGraphAnalysisPanel
            impactDraft={graph.impactDraft}
            impactResults={graph.analysis.impactResults}
            impactRunning={graph.analysis.impactRunning}
            nodeOptions={graph.options.nodeOptions}
            pathDraft={graph.pathDraft}
            pathResults={graph.analysis.pathResults}
            pathRunning={graph.analysis.pathRunning}
            relationTypeOptions={graph.options.relationTypeOptions}
            onImpactDraftChange={graph.setImpactDraft}
            onPathDraftChange={graph.setPathDraft}
            onRunImpact={() => void graph.analysis.runImpactAnalysis()}
            onRunPath={() => void graph.analysis.runPathAnalysis()}
          />
        ) : null}

        {graph.activePanel === 'tasks' ? (
          <KnowledgeGraphTaskPanel
            error={graph.queries.tasks.error}
            loading={graph.queries.tasks.isFetching}
            tasks={graph.tasks}
            onRefresh={() => void graph.commands.refresh()}
          />
        ) : null}
      </aside>

      <KnowledgeGraphEntityForms
        activeModal={graph.activeModal}
        edgeFormValue={graph.edgeFormValue}
        nodeFormValue={graph.nodeFormValue}
        nodeOptions={graph.options.nodeOptions}
        savingEdge={graph.mutations.savingEdge}
        savingNode={graph.mutations.savingNode}
        onClose={graph.commands.closeModal}
        onSaveEdge={(value) => void graph.commands.saveEdge(value)}
        onSaveNode={(value) => void graph.commands.saveNode(value)}
      />

      <KnowledgeGraphImportExportModal
        activeModal={graph.activeModal}
        draft={graph.exchangeDraft}
        exporting={graph.mutations.exporting}
        importing={graph.mutations.importing}
        onChange={graph.setExchangeDraft}
        onClose={graph.commands.closeModal}
        onExport={graph.commands.exportGraph}
        onImport={graph.commands.importGraph}
      />
    </div>
  );
}
