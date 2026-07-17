type WorkerTaskStatus = 'failed' | 'queued' | 'running' | 'succeeded';

export interface WorkerTaskRecord {
  id: string;
  label: string;
  progress: number;
  status: WorkerTaskStatus;
}

export class WorkerManager {
  private readonly tasks = new Map<string, WorkerTaskRecord>();

  public enqueue(label: string): WorkerTaskRecord {
    const task: WorkerTaskRecord = {
      id: `worker_${crypto.randomUUID().replaceAll('-', '')}`,
      label,
      progress: 0,
      status: 'queued'
    };
    this.tasks.set(task.id, task);
    return task;
  }

  public finish(taskId: string): WorkerTaskRecord | null {
    const task = this.tasks.get(taskId);
    if (!task) {
      return null;
    }

    task.progress = 100;
    task.status = 'succeeded';
    return task;
  }

  public list(): WorkerTaskRecord[] {
    return Array.from(this.tasks.values());
  }

  public start(taskId: string): WorkerTaskRecord | null {
    const task = this.tasks.get(taskId);
    if (!task) {
      return null;
    }

    task.status = 'running';
    task.progress = Math.max(task.progress, 5);
    return task;
  }
}
