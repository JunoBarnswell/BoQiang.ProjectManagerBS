export interface HealthDto {
  project: string;
  service: string;
  status: string;
  time: string;
}

export interface EchoRequest {
  message: string;
}

export interface EchoDto {
  message: string;
  serverMessage: string;
  receivedAt: string;
}

export interface BatchDeleteRequest {
  ids: string[];
}

export interface BatchStatusUpdateRequest {
  ids: string[];
  status: string;
}

export interface GridPageResult<TItem> {
  items: TItem[];
  summary?: Record<string, unknown> | null;
  total: number;
}

export interface OptionItemDto {
  color?: string | null;
  disabled?: boolean;
  label: string;
  value: string;
}
