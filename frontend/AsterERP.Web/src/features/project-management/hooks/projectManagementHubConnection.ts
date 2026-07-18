import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';

import { getAccessToken } from '../../../core/http/tokenStorage';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

export type ProjectManagementHubConnectionState = 'connected' | 'connecting' | 'reconnecting' | 'disconnected';

type HubEventHandler = (event: unknown) => void;

interface ProjectManagementHubLifecycle {
  connected?: () => void;
  closed?: () => void;
  reconnected?: () => void;
  reconnecting?: () => void;
  stateChanged?: (state: ProjectManagementHubConnectionState) => void;
}

interface ProjectSubscription {
  joined: boolean;
  joining?: Promise<void>;
  listeners: Set<() => void>;
}

interface HubEntry {
  connection: HubConnection;
  consumers: number;
  eventDispatchers: Map<string, (...args: unknown[]) => void>;
  eventHandlers: Map<string, Set<HubEventHandler>>;
  key: string;
  lifecycleListeners: Set<ProjectManagementHubLifecycle>;
  projects: Map<string, ProjectSubscription>;
  startPromise?: Promise<void>;
  state: ProjectManagementHubConnectionState;
  stopTimer?: ReturnType<typeof setTimeout>;
}

const hubs = new Map<string, HubEntry>();

export interface ProjectManagementHubConnectionLease {
  dispose: () => void;
  subscribe: <TEvent>(eventName: string, handler: (event: TEvent) => void) => () => void;
  subscribeLifecycle: (lifecycle: ProjectManagementHubLifecycle) => () => void;
  subscribeProject: (projectId: string, onJoined: () => void) => () => void;
}

export function acquireProjectManagementHubConnection(
  signalRUrl: string,
  scope: ProjectManagementWorkspaceScope,
): ProjectManagementHubConnectionLease | undefined {
  if (!scope.isAvailable) return undefined;

  const key = `${signalRUrl}|${scope.tenantId}|${scope.appCode}`;
  let entry = hubs.get(key);
  if (!entry) {
    entry = createHubEntry(key, signalRUrl);
    hubs.set(key, entry);
  }
  if (entry.stopTimer) {
    clearTimeout(entry.stopTimer);
    entry.stopTimer = undefined;
  }
  entry.consumers += 1;
  void ensureStarted(entry);

  const cleanups = new Set<() => void>();
  let disposed = false;
  const track = (cleanup: () => void) => {
    cleanups.add(cleanup);
    return () => {
      cleanups.delete(cleanup);
      cleanup();
    };
  };

  return {
    subscribe: <TEvent>(eventName: string, handler: (event: TEvent) => void) => {
      const handlers = entry.eventHandlers.get(eventName) ?? new Set<HubEventHandler>();
      if (!entry.eventHandlers.has(eventName)) {
        const dispatcher = (...args: unknown[]) => {
          const event = args[0];
          for (const currentHandler of entry.eventHandlers.get(eventName) ?? []) currentHandler(event);
        };
        entry.eventHandlers.set(eventName, handlers);
        entry.eventDispatchers.set(eventName, dispatcher);
        entry.connection.on(eventName, dispatcher);
      }
      const wrappedHandler: HubEventHandler = (event) => handler(event as TEvent);
      handlers.add(wrappedHandler);
      return track(() => removeEventHandler(entry, eventName, wrappedHandler));
    },
    subscribeLifecycle: (lifecycle) => {
      entry.lifecycleListeners.add(lifecycle);
      lifecycle.stateChanged?.(entry.state);
      if (entry.state === 'connected') lifecycle.connected?.();
      return track(() => entry.lifecycleListeners.delete(lifecycle));
    },
    subscribeProject: (projectId, onJoined) => {
      const project = entry.projects.get(projectId) ?? { joined: false, listeners: new Set<() => void>() };
      entry.projects.set(projectId, project);
      project.listeners.add(onJoined);
      if (project.joined) onJoined();
      else void joinProject(entry, projectId);
      return track(() => removeProjectSubscription(entry, projectId, onJoined));
    },
    dispose: () => {
      if (disposed) return;
      disposed = true;
      for (const cleanup of cleanups) cleanup();
      cleanups.clear();
      releaseHubEntry(entry);
    },
  };
}

function createHubEntry(key: string, signalRUrl: string): HubEntry {
  const connection = new HubConnectionBuilder()
    .withUrl(signalRUrl, { accessTokenFactory: () => getAccessToken() })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
  const entry: HubEntry = {
    connection,
    consumers: 0,
    eventDispatchers: new Map(),
    eventHandlers: new Map(),
    key,
    lifecycleListeners: new Set(),
    projects: new Map(),
    state: 'disconnected',
  };
  connection.onreconnecting(() => {
    for (const project of entry.projects.values()) project.joined = false;
    updateState(entry, 'reconnecting');
    notifyLifecycle(entry, 'reconnecting');
  });
  connection.onreconnected(async () => {
    updateState(entry, 'connected');
    await rejoinProjects(entry);
    notifyLifecycle(entry, 'reconnected');
  });
  connection.onclose(() => {
    for (const project of entry.projects.values()) project.joined = false;
    updateState(entry, 'disconnected');
    notifyLifecycle(entry, 'closed');
  });
  return entry;
}

async function ensureStarted(entry: HubEntry) {
  if (entry.connection.state === HubConnectionState.Connected || entry.startPromise) return entry.startPromise;
  if (entry.connection.state === HubConnectionState.Reconnecting || entry.connection.state === HubConnectionState.Connecting) return undefined;

  updateState(entry, 'connecting');
  entry.startPromise = entry.connection.start()
    .then(async () => {
      updateState(entry, 'connected');
      await rejoinProjects(entry);
      notifyLifecycle(entry, 'connected');
    })
    .catch(() => {
      updateState(entry, 'disconnected');
    })
    .finally(() => {
      entry.startPromise = undefined;
    });
  return entry.startPromise;
}

async function rejoinProjects(entry: HubEntry) {
  await Promise.all([...entry.projects.keys()].map((projectId) => joinProject(entry, projectId)));
}

function joinProject(entry: HubEntry, projectId: string) {
  const project = entry.projects.get(projectId);
  if (!project || project.joined || project.joining || entry.connection.state !== HubConnectionState.Connected) return project?.joining;

  project.joining = entry.connection.invoke('JoinProjectManagementProject', projectId)
    .then(() => {
      project.joined = true;
      if (entry.projects.get(projectId) !== project) {
        void entry.connection.invoke('LeaveProjectManagementProject', projectId).catch(() => undefined);
        return;
      }
      for (const listener of project.listeners) listener();
    })
    .catch(() => undefined)
    .finally(() => {
      project.joining = undefined;
    });
  return project.joining;
}

function removeEventHandler(entry: HubEntry, eventName: string, handler: HubEventHandler) {
  const handlers = entry.eventHandlers.get(eventName);
  if (!handlers) return;
  handlers.delete(handler);
  if (handlers.size > 0) return;
  const dispatcher = entry.eventDispatchers.get(eventName);
  if (dispatcher) entry.connection.off(eventName, dispatcher);
  entry.eventDispatchers.delete(eventName);
  entry.eventHandlers.delete(eventName);
}

function removeProjectSubscription(entry: HubEntry, projectId: string, listener: () => void) {
  const project = entry.projects.get(projectId);
  if (!project) return;
  project.listeners.delete(listener);
  if (project.listeners.size > 0) return;
  entry.projects.delete(projectId);
  if (project.joined && entry.connection.state === HubConnectionState.Connected) {
    void entry.connection.invoke('LeaveProjectManagementProject', projectId).catch(() => undefined);
  }
}

function releaseHubEntry(entry: HubEntry) {
  entry.consumers -= 1;
  if (entry.consumers > 0 || entry.stopTimer) return;
  entry.stopTimer = setTimeout(() => {
    entry.stopTimer = undefined;
    if (entry.consumers > 0) return;
    hubs.delete(entry.key);
    void entry.connection.stop();
  }, 0);
}

function updateState(entry: HubEntry, state: ProjectManagementHubConnectionState) {
  entry.state = state;
  for (const lifecycle of entry.lifecycleListeners) lifecycle.stateChanged?.(state);
}

function notifyLifecycle(entry: HubEntry, event: 'connected' | 'closed' | 'reconnected' | 'reconnecting') {
  for (const lifecycle of entry.lifecycleListeners) lifecycle[event]?.();
}
