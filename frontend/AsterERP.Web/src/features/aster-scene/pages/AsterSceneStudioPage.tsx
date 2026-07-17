import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useParams } from 'react-router-dom';

import { useI18n } from '@/core/i18n/I18nProvider';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneState } from '../components/AsterSceneState';
import { DccWorkbenchShell } from '../components/dcc/EditorShell';
import { useAsterSceneAutosave } from '../core/autosave/useAsterSceneAutosave';
import { computeSceneDocumentHash, createClientMutationId, normalizeSceneDocument } from '../core/scene-document/documentKernel';
import { useAsterSceneDocumentStore } from '../state/documentStore';
import '../styles/aster-scene.css';

export function AsterSceneStudioPage() {
  const { translate: t } = useI18n();
  const { projectId = '' } = useParams();
  const queryClient = useQueryClient();
  const { dirty, document, documentHash, markSaved, project, reset, revision } = useAsterSceneDocumentStore();
  const autosave = useAsterSceneAutosave(projectId);

  const documentQuery = useQuery({
    enabled: Boolean(projectId),
    queryFn: ({ signal }) => asterSceneApi.projects.document(projectId, signal),
    queryKey: ['asterscene', 'document', projectId]
  });
  const assetsQuery = useQuery({
    enabled: Boolean(projectId),
    queryFn: ({ signal }) => asterSceneApi.assets.list({ pageSize: 100, projectId }, signal),
    queryKey: ['asterscene', 'assets', projectId]
  });
  const versionsQuery = useQuery({
    enabled: Boolean(projectId),
    queryFn: ({ signal }) => asterSceneApi.publish.versions(projectId, { pageSize: 100 }, signal),
    queryKey: ['asterscene', 'publish-versions', projectId]
  });

  useEffect(() => {
    const payload = documentQuery.data?.data;
    if (payload) {
      reset({
        document: payload.document,
        documentHash: payload.documentHash,
        project: payload.project,
        revision: payload.revision
      });
    }
  }, [documentQuery.data, reset]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!document) {
        return null;
      }

      const normalizedDocument = normalizeSceneDocument(document);
      const nextHash = await computeSceneDocumentHash(normalizedDocument);
      return asterSceneApi.projects.saveDocument(projectId, {
        clientMutationId: createClientMutationId('manual'),
        document: normalizedDocument,
        documentHash: nextHash,
        expectedRevision: revision,
        saveSource: 'Manual'
      });
    },
    onSuccess: (response) => {
      if (response?.data) {
        markSaved(response.data.revision, response.data.documentHash);
      }
    }
  });

  const publishMutation = useMutation({
    mutationFn: () =>
      asterSceneApi.publish.execute(projectId, {
        clientMutationId: createClientMutationId('publish'),
        documentHash,
        expectedRevision: revision,
        qualityGateMode: 'Strict',
        visibility: 'Public'
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asterscene', 'document', projectId] })
  });
  const rollbackMutation = useMutation({
    mutationFn: (publishCode: string) =>
      asterSceneApi.publish.rollback(projectId, publishCode, {
        clientMutationId: createClientMutationId('rollback'),
        documentHash,
        expectedRevision: revision
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['asterscene', 'document', projectId] });
      void queryClient.invalidateQueries({ queryKey: ['asterscene', 'publish-versions', projectId] });
    }
  });

  if (documentQuery.isLoading) {
    return <AsterSceneState title={t('asterscene.studio.loading')} />;
  }

  if (documentQuery.isError || !document || !project) {
    return <AsterSceneState title={t('asterscene.studio.failed')} description={t('asterscene.studio.failedDescription')} />;
  }

  return (
    <DccWorkbenchShell
      assets={assetsQuery.data?.data.items ?? []}
      autosavePending={autosave.isPending}
      dirty={dirty}
      document={document}
      documentHash={documentHash}
      onPublish={() => publishMutation.mutate()}
      onRollback={(version) => rollbackMutation.mutate(version.publishCode)}
      onSave={() => saveMutation.mutate()}
      project={project}
      publishPending={publishMutation.isPending}
      rollbackPending={rollbackMutation.isPending}
      revision={revision}
      savePending={saveMutation.isPending}
      t={t}
      versions={versionsQuery.data?.data.items ?? []}
    />
  );
}
