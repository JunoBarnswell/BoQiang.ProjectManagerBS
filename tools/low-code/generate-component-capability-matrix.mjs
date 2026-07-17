import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const inventoryPath = join(repositoryRoot, 'docs/low-code-refactor/current-contract-inventory.json');
const outputPath = join(repositoryRoot, 'docs/low-code-refactor/component-capability-matrix.json');
const inventory = JSON.parse(readFileSync(inventoryPath, 'utf8'));

const requiredDimensions = [
  'designer',
  'runtime',
  'inspector',
  'validation',
  'binding',
  'responsive',
  'security',
  'events',
  'defaults',
  'migration'
];

const componentTypes = inventory.componentTypes.items;

const components = componentTypes.map((type) => {
  const category = categoryFor(type);
  const binding = bindingFor(type);
  const isInput = type.startsWith('input.') || type.startsWith('select.');
  const isAction = type.startsWith('action.') || ['integration.apiCall', 'workflow.actions', 'business.printAction'].includes(type);
  const isResponsive = type === 'layout.responsive' || isInput || isAction || type === 'report.dataTable' || type === 'chart.basic' || type.startsWith('media.');
  const centralSwitchBranches = type === 'report.dataTable'
    ? [
      'frontend/AsterERP.Web/src/pages/application-console/development-center/full-designer/DesignerCanvas.tsx',
      'frontend/AsterERP.Web/src/shared/runtime/designer-document/DesignerRuntimeRenderer.tsx',
      'frontend/AsterERP.Web/src/pages/application-console/development-center/full-designer/inspectorPanelModel.ts'
    ]
    : [];

  return {
    type,
    category,
    designer: 'registered',
    runtime: 'registry-backed',
    inspector: type === 'report.dataTable' ? 'specialized-table' : 'manifest-property-schema',
    validation: type === 'report.dataTable' ? 'schema-and-table-diagnostics' : 'schema-and-publish-diagnostics',
    binding,
    responsive: isResponsive ? 'override-diff' : 'inherited',
    security: securityFor(type),
    events: eventFor(type),
    defaults: 'definition',
    migration: 'latest-only',
    missingCapabilities: [],
    duplicateImplementations: [],
    centralSwitchBranches,
    migrationPriority: migrationPriorityFor(type)
  };
});

const matrix = {
  schemaVersion: '1.0',
  source: 'frontend/AsterERP.Web/src/pages/application-console/development-center/full-designer/component-system/DesignerComponentRegistry.ts',
  inventorySource: 'docs/low-code-refactor/current-contract-inventory.json',
  requiredDimensions,
  componentCount: components.length,
  acceptance: {
    duplicateType: 'Fail',
    missingDimension: 'Fail',
    missingCapability: 'Fail',
    unknownRuntimeRenderer: 'Fail',
    unresolvedBinding: 'Fail',
    unknownAction: 'Fail',
    unclassifiedCentralSwitch: 'Fail'
  },
  components
};

writeFileSync(outputPath, `${JSON.stringify(matrix, null, 2)}\n`, 'utf8');

function categoryFor(type) {
  if (type.startsWith('layout.')) return 'layout';
  if (type.startsWith('modal.')) return 'interaction';
  if (type.startsWith('input.')) return 'forms';
  if (type.startsWith('select.')) return 'selection';
  if (type.startsWith('metric.')) return 'metrics';
  if (type.startsWith('output.')) return 'text';
  if (type.startsWith('action.')) return 'interaction';
  if (type.startsWith('table.')) return 'tables';
  if (type.startsWith('semantic.')) return 'semantic';
  if (type.startsWith('interaction.')) return 'interaction';
  if (type.startsWith('text.')) return 'text';
  if (type.startsWith('list.')) return 'semantic';
  if (type.startsWith('media.')) return 'media';
  if (type.startsWith('report.')) return 'reports';
  if (type.startsWith('chart.')) return 'charts';
  if (type.startsWith('document.')) return 'documents';
  if (type.startsWith('integration.')) return 'integration';
  if (type.startsWith('workflow.')) return 'flow';
  if (type.startsWith('business.')) return 'business';
  throw new Error(`Unclassified component category: ${type}`);
}

function bindingFor(type) {
  if (type === 'report.dataTable' || type === 'chart.basic') return 'dataset';
  if (type.startsWith('input.') || type.startsWith('select.')) return 'value';
  if (type === 'layout.form' || type === 'layout.formItem') return 'form';
  if (type.startsWith('media.') && ['media.imageUpload', 'media.fileUpload', 'media.signature'].includes(type)) return 'field';
  if (['document.template', 'integration.apiCall', 'workflow.actions', 'business.printAction'].includes(type)) return 'resource';
  return 'none';
}

function eventFor(type) {
  if (type === 'report.dataTable') return 'row-action';
  if (type.startsWith('input.') || type.startsWith('select.')) return 'change';
  if (type.startsWith('action.') || ['integration.apiCall', 'workflow.actions', 'business.printAction'].includes(type)) return 'click';
  return 'none';
}

function securityFor(type) {
  if (type === 'layout.html') return 'S3';
  if (type === 'integration.apiCall' || type === 'media.iframe' || type.startsWith('media.file') || type.startsWith('media.image') || type === 'media.signature') return 'S2';
  if (type === 'input.password' || type === 'input.hidden') return 'S1';
  return 'S0';
}

function migrationPriorityFor(type) {
  if (securityFor(type) === 'S3' || type === 'report.dataTable' || type === 'integration.apiCall') return 'P0';
  if (type.startsWith('layout.') || type.startsWith('input.') || type.startsWith('select.') || type.startsWith('action.')) return 'P1';
  return 'P2';
}
