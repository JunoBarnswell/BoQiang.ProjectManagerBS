import { useImStore } from '../state/imStore';

export function ImConnectionStatus() {
  const status = useImStore((state) => state.connectionStatus);
  const color = status === 'connected' ? 'bg-emerald-500' : status === 'connecting' || status === 'reconnecting' ? 'bg-amber-500' : 'bg-gray-400';
  const label = status === 'connected' ? '已连接' : status === 'connecting' ? '连接中' : status === 'reconnecting' ? '重连中' : '离线';

  return (
    <span className="inline-flex items-center gap-1 text-xs text-gray-500">
      <span className={`h-2 w-2 rounded-full ${color}`} />
      {label}
    </span>
  );
}
