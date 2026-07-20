import type { projectManagementWorkbenchMessagesZhCN } from '../zh-CN/projectManagementWorkbench';

export const projectManagementWorkbenchMessagesEnUS = {
  'projectManagement.workbench.project': 'Project',
  'projectManagement.workbench.requirements': 'Tasks',
  'projectManagement.workbench.createRequirement': 'New task',
  'projectManagement.workbench.selector.title': 'Projects',
  'projectManagement.workbench.selector.description': 'Choose a project to open its overview and tasks workspace.',
  'projectManagement.workbench.selector.create': 'New project',
  'projectManagement.workbench.selector.searchAria': 'Search projects',
  'projectManagement.workbench.selector.searchPlaceholder': 'Search by project name or code',
  'projectManagement.workbench.selector.name': 'Name',
  'projectManagement.workbench.selector.health': 'Health',
  'projectManagement.workbench.selector.owner': 'Owner',
  'projectManagement.workbench.selector.dueDate': 'Target date',
  'projectManagement.workbench.selector.status': 'Status',
  'projectManagement.workbench.selector.loading': 'Loading projects…',
  'projectManagement.workbench.selector.loadFailed': 'Could not load projects. Refresh and try again.',
  'projectManagement.workbench.selector.empty': 'No matching projects',
  'projectManagement.workbench.overview.loading': 'Loading project overview…',
  'projectManagement.workbench.overview.notFound': 'The project does not exist or you do not have access.',
  'projectManagement.workbench.overview.metric.total': 'Tasks',
  'projectManagement.workbench.overview.metric.completed': 'Completed',
  'projectManagement.workbench.overview.metric.inProgress': 'In progress',
  'projectManagement.workbench.overview.metric.pending': 'Pending',
  'projectManagement.workbench.overview.metric.storyPoints': 'Story points',
  'projectManagement.workbench.overview.milestones': 'Milestone progress',
  'projectManagement.workbench.overview.risk': 'Risk overview',
  'projectManagement.workbench.overview.workload': 'Workload (estimated)',
  'projectManagement.workbench.overview.activity': 'Recent activity',
  'projectManagement.workbench.overview.noActivity': 'No project activity yet',
  'projectManagement.workbench.overview.distribution': 'Task type distribution',
  'projectManagement.workbench.risk.high': 'High risk',
  'projectManagement.workbench.risk.medium': 'Medium risk',
  'projectManagement.workbench.risk.low': 'Low risk',
  'projectManagement.workbench.risk.closed': 'Closed',
  'projectManagement.workbench.unit.minutes': 'min',
  'projectManagement.workbench.workloadValue': '{estimated} / {capacity} min',
  'projectManagement.workbench.unknown': 'Not set'
  ,'projectManagement.workbench.back': 'Back to projects'
  ,'projectManagement.workbench.overview': 'Overview'
  ,'projectManagement.workbench.requirementsNav': 'Tasks'
  ,'projectManagement.workbench.future': 'Available in a later release'
  ,'projectManagement.workbench.defects': 'Defects'
  ,'projectManagement.workbench.workItems': 'Work items'
  ,'projectManagement.workbench.iterations': 'Iterations'
  ,'projectManagement.workbench.milestones': 'Milestones'
  ,'projectManagement.workbench.reports': 'Reports'
  ,'projectManagement.workbench.settings': 'Settings'
  ,'projectManagement.workbench.breadcrumb': 'Project / {code}'
} satisfies Record<keyof typeof projectManagementWorkbenchMessagesZhCN, string>;
