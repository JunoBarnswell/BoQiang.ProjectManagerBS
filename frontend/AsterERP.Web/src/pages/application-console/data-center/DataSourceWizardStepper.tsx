export type DataSourceWizardStep = 'type' | 'connection' | 'diagnosis' | 'confirm';

interface DataSourceWizardStepperProps {
  activeStep: DataSourceWizardStep;
  diagnosisReady: boolean;
  onChange: (step: DataSourceWizardStep) => void;
}

const steps: Array<{ id: DataSourceWizardStep; label: string }> = [
  { id: 'type', label: '类型' },
  { id: 'connection', label: '连接' },
  { id: 'diagnosis', label: '诊断' },
  { id: 'confirm', label: '确认保存' }
];

export function DataSourceWizardStepper({ activeStep, diagnosisReady, onChange }: DataSourceWizardStepperProps) {
  return (
    <nav aria-label="数据源保存步骤" className="mb-4 grid grid-cols-4 gap-1 rounded-md bg-slate-100 p-1">
      {steps.map((step, index) => {
        const disabled = step.id === 'confirm' && !diagnosisReady;
        return (
          <button
            aria-current={activeStep === step.id ? 'step' : undefined}
            className={`rounded px-2 py-1.5 text-xs ${activeStep === step.id ? 'bg-white font-semibold text-primary-700 shadow-sm' : 'text-slate-500'}`}
            disabled={disabled}
            key={step.id}
            type="button"
            onClick={() => onChange(step.id)}
          >
            {index + 1}. {step.label}
          </button>
        );
      })}
    </nav>
  );
}
