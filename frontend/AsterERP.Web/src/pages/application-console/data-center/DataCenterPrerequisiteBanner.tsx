import { AppIcon } from '../../../shared/icons/AppIcon';

interface DataCenterPrerequisiteBannerProps {
  message?: string | null;
}

export function DataCenterPrerequisiteBanner({ message }: DataCenterPrerequisiteBannerProps) {
  if (!message) {
    return null;
  }

  return (
    <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
      <AppIcon className="mt-0.5 h-4 w-4 shrink-0" name="alertTriangle" />
      <span>{message}</span>
    </div>
  );
}
