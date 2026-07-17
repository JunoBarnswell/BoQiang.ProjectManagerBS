import { ShieldAlert, WandSparkles } from 'lucide-react';

interface AiToolIntentCardProps {
  active?: boolean;
  description: string;
  disabled?: boolean;
  requiresConfirmation?: boolean;
  title: string;
  onClick?: () => void;
}

export function AiToolIntentCard({ active, description, disabled, requiresConfirmation, title, onClick }: AiToolIntentCardProps) {
  const className = [
    'w-full rounded-md border px-3 py-2 text-left transition',
    active ? 'border-primary-300 bg-primary-50 text-primary-900' : 'border-slate-200 bg-white',
    onClick && !disabled ? 'hover:border-primary-200 hover:bg-slate-50' : '',
    disabled ? 'opacity-60' : '',
    onClick ? 'cursor-pointer' : 'cursor-default'
  ].join(' ');
  const content = (
    <>
      <span className="flex items-center gap-2 text-sm font-medium">
        <WandSparkles className="h-4 w-4" />
        {title}
        {requiresConfirmation ? <ShieldAlert className="ml-auto h-4 w-4 text-amber-500" /> : null}
      </span>
      <span className="mt-1 block text-xs text-slate-500">{description}</span>
    </>
  );

  if (!onClick) {
    return <div className={className}>{content}</div>;
  }

  return (
    <button
      className={className}
      disabled={disabled}
      type="button"
      onClick={onClick}
    >
      {content}
    </button>
  );
}
