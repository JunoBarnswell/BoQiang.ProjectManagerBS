import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, CreditCard, XCircle } from 'lucide-react';

import { useI18n } from '@/core/i18n/I18nProvider';
import { PermissionButton } from '@/shared/auth/PermissionButton';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneLayout } from '../components/AsterSceneLayout';
import { AsterSceneState } from '../components/AsterSceneState';
import { createClientMutationId } from '../core/scene-document/documentKernel';
import { asterScenePermissions } from '../model/permissions';

export function AsterScenePricingPage() {
  const { translate: t } = useI18n();
  const queryClient = useQueryClient();
  const plansQuery = useQuery({
    queryFn: ({ signal }) => asterSceneApi.subscriptions.plans(signal),
    queryKey: ['asterscene', 'plans']
  });
  const usageQuery = useQuery({
    queryFn: ({ signal }) => asterSceneApi.usage.summary(signal),
    queryKey: ['asterscene', 'usage-summary'],
    retry: false
  });
  const currentSubscriptionQuery = useQuery({
    queryFn: ({ signal }) => asterSceneApi.subscriptions.current(signal),
    queryKey: ['asterscene', 'subscription-current'],
    retry: false
  });
  const subscribeMutation = useMutation({
    mutationFn: (planCode: string) => asterSceneApi.subscriptions.subscribe({ clientMutationId: createClientMutationId('sub'), planCode }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asterscene'] })
  });
  const cancelMutation = useMutation({
    mutationFn: () =>
      asterSceneApi.subscriptions.cancel({
        clientMutationId: createClientMutationId('sub-cancel'),
        reason: t('asterscene.pricing.cancelReason')
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asterscene'] })
  });
  const plans = plansQuery.data?.data ?? [];
  const currentSubscription = currentSubscriptionQuery.data?.data;

  return (
    <AsterSceneLayout eyebrow={t('asterscene.pricing.eyebrow')} title={t('asterscene.pricing.title')}>
      {usageQuery.data?.data ? (
        <section className="as-band as-metrics">
          <span>{t('asterscene.pricing.plan')} {usageQuery.data.data.planCode}</span>
          <span>{t('asterscene.pricing.storage')} {usageQuery.data.data.storageGbUsed.toFixed(2)} / {usageQuery.data.data.storageGbLimit} GB</span>
          <span>{t('asterscene.pricing.aiCredits')} {usageQuery.data.data.aiCreditsRemaining}</span>
          <span>{t('asterscene.pricing.published')} {usageQuery.data.data.publishedWorksUsed} / {usageQuery.data.data.publishedWorksLimit}</span>
          {currentSubscription ? <span>{t('asterscene.pricing.status')} {currentSubscription.status}</span> : null}
          {currentSubscription?.status === 'Active' ? (
            <PermissionButton
              className="as-button"
              code={asterScenePermissions.subscriptionManage}
              disabled={cancelMutation.isPending}
              iconStart={false}
              onClick={() => cancelMutation.mutate()}
              type="button"
            >
              <XCircle size={16} /> {t('asterscene.pricing.cancel')}
            </PermissionButton>
          ) : null}
        </section>
      ) : null}
      {plansQuery.isLoading ? <AsterSceneState title={t('asterscene.pricing.loading')} /> : null}
      <section className="as-plan-grid">
        {plans.map((plan) => (
          <article className="as-plan" key={plan.planCode}>
            <h2>{plan.planName}</h2>
            <strong>${plan.priceMonthly}<span>/{t('asterscene.pricing.month')}</span></strong>
            <p><Check size={16} /> {plan.storageGb} GB {t('asterscene.pricing.storageUnit')}</p>
            <p><Check size={16} /> {plan.aiCreditsMonthly} {t('asterscene.pricing.aiCreditsUnit')}</p>
            <p><Check size={16} /> {plan.publishedWorks} {t('asterscene.pricing.publishedWorksUnit')}</p>
            <PermissionButton
              className="as-button as-button--primary"
              code={asterScenePermissions.subscriptionManage}
              disabled={subscribeMutation.isPending}
              iconStart={false}
              onClick={() => subscribeMutation.mutate(plan.planCode)}
              type="button"
            >
              <CreditCard size={16} /> {t('asterscene.pricing.choose')}
            </PermissionButton>
          </article>
        ))}
      </section>
    </AsterSceneLayout>
  );
}
