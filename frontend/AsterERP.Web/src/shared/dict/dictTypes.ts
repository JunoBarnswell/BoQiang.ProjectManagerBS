export interface DictOption {
  color?: string;
  disabled?: boolean;
  label: string;
  value: string;
}

export interface DictStateSnapshot {
  options: DictOption[];
  revision: number;
}
